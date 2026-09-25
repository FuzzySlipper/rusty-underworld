using System.Numerics;
using System.Reflection;
using Rusty.Engine;

namespace AbyssRpg.Host.Tests;

/// <summary>In-memory Engine persistence + context doubles (test-only).</summary>
internal sealed class InMemoryPersistenceService : IPersistenceService
{
    private readonly Dictionary<(string Scope, string Key), Entry> _values = [];
    private readonly Dictionary<ulong, Entry?> _blobs = [];
    private ulong _nextBlob;
    private string _scope = "";

    /// <summary>Fails the next write once, the way a transient persistence outage would.</summary>
    internal bool FailNextSave { get; set; }

    internal void Put(string scope, string key, byte[] payload) => _values[(scope, key)] = new(1, payload.ToArray());

    public PersistenceStore OpenStore(PersistenceOpenRequest request)
    {
        _scope = request.Scope;
        return new(new PersistenceStoreHandle(1), static () => { });
    }

    public PersistenceSaveReceipt Save(PersistenceSaveRequest request)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new IOException("transient outage");
        }

        (string Scope, string Key) key = (_scope, request.Key);
        bool present = _values.TryGetValue(key, out Entry? existing);
        if ((request.RevisionGuard == PersistenceRevisionGuard.Absent && present)
            || (request.RevisionGuard == PersistenceRevisionGuard.Exact && (!present || existing!.Revision != request.ExpectedRevision)))
            return new PersistenceSaveReceipt(PersistenceSaveOutcome.RevisionConflict, existing?.Revision ?? 0);
        ulong revision = present ? checked(existing!.Revision + 1) : 1;
        _values[key] = new(revision, request.Payload.ToArray());
        return new PersistenceSaveReceipt(revision);
    }

    public PersistenceDeleteReceipt Delete(PersistenceDeleteRequest request)
    {
        (string Scope, string Key) key = (_scope, request.Key);
        _values.TryGetValue(key, out Entry? existing);
        if (existing is null) return new(PersistenceDeleteOutcome.Missing, 0);
        _values.Remove(key);
        return new(PersistenceDeleteOutcome.Deleted, existing.Revision);
    }

    public PersistenceBlob Load(PersistenceLoadRequest request)
    {
        Entry? value = _values.TryGetValue((_scope, request.Key), out Entry? found) ? found : null;
        ulong handle = ++_nextBlob;
        _blobs.Add(handle, value);
        return new(new PersistenceBlobHandle(handle), static () => { });
    }

    public PersistenceBlobInfo DescribeBlob(PersistenceBlob blob)
    {
        Entry? value = Require(blob);
        return value is null ? new(false, 0, 0) : new(true, value.Revision, checked((nuint)value.Payload.Length));
    }

    public void CopyBlob(PersistenceCopyBlobRequest request) => Require(request.Blob)?.Payload.CopyTo(request.Destination);

    public ReadOnlyMemory<byte> ReadBlobBytes(PersistenceBlob blob) => Require(blob)?.Payload.ToArray() ?? [];

    private Entry? Require(PersistenceBlob blob) => _blobs.TryGetValue(blob.Handle.Value, out Entry? value)
        ? value : throw new InvalidOperationException("Unknown persistence blob.");

    private sealed record Entry(ulong Revision, byte[] Payload);
}

internal class EngineContextFake : DispatchProxy
{
    private IPersistenceService? _persistence;
    private ISpatialService? _spatial;
    private IContentService? _content;
    private IUiService? _ui;
    private ICameraViewService? _cameraView;
    private IGraphicsService? _graphics;

    public static IEngineContext Create(
        IPersistenceService? persistence = null,
        ISpatialService? spatial = null,
        IContentService? content = null,
        IUiService? ui = null,
        ICameraViewService? cameraView = null,
        IGraphicsService? graphics = null)
    {
        IEngineContext context = DispatchProxy.Create<IEngineContext, EngineContextFake>();
        var fake = (EngineContextFake)(object)context;
        fake._persistence = persistence;
        fake._spatial = spatial;
        fake._content = content;
        fake._ui = ui;
        fake._cameraView = cameraView;
        fake._graphics = graphics;
        return context;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        "get_Persistence" => _persistence ?? throw new NotSupportedException(method?.Name),
        "get_Spatial" => _spatial ?? throw new NotSupportedException(method?.Name),
        "get_Content" => _content ?? throw new NotSupportedException(method?.Name),
        "get_Ui" => _ui ?? throw new NotSupportedException(method?.Name),
        "get_CameraView" => _cameraView ?? throw new NotSupportedException(method?.Name),
        "get_Graphics" => _graphics ?? throw new NotSupportedException(method?.Name),
        _ => throw new NotSupportedException(method?.Name),
    };
}

/// <summary>Records the Engine camera crossing: one active camera, updated each admitted step.</summary>
internal class CameraViewDouble : DispatchProxy
{
    internal ICameraViewService Service { get; private set; } = null!;
    internal int CreateCalls { get; private set; }
    internal int UpdateCalls { get; private set; }
    internal int ActiveCalls { get; private set; }
    internal CameraDescriptor? LastDescriptor { get; private set; }
    internal bool ActiveCleared { get; private set; }

    internal static CameraViewDouble Create()
    {
        ICameraViewService service = DispatchProxy.Create<ICameraViewService, CameraViewDouble>();
        CameraViewDouble result = (CameraViewDouble)(object)service;
        result.Service = service;
        return result;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        nameof(ICameraViewService.CreateCamera) => Create((CameraDescriptor)arguments![0]!),
        nameof(ICameraViewService.SetActiveCamera) => Active(),
        nameof(ICameraViewService.ClearActiveCamera) => ClearActive(),
        nameof(ICameraViewService.UpdateCamera) => Update((CameraUpdateRequest)arguments![0]!),
        nameof(ICameraViewService.SetBackgroundColor) => null,
        _ => throw new NotSupportedException(method?.Name),
    };

    private Camera Create(CameraDescriptor descriptor)
    {
        CreateCalls++;
        LastDescriptor = descriptor;
        return new Camera(new CameraHandle(1), static () => { });
    }

    private object? Active()
    {
        ActiveCalls++;
        return null;
    }

    private object? ClearActive()
    {
        ActiveCleared = true;
        return null;
    }

    private object? Update(CameraUpdateRequest request)
    {
        UpdateCalls++;
        LastDescriptor = request.Descriptor;
        return null;
    }
}

/// <summary>
/// Hand-written graphics double: DispatchProxy cannot intercept a member whose
/// signature carries a byref-like span, and the scene publish is exactly that
/// member. Everything this test suite does not exercise throws, so a new
/// production call is a loud failure rather than a silent pass.
/// </summary>
internal class SpatialContentDouble : DispatchProxy
{
    internal IContentService Service { get; private set; } = null!;

    internal static SpatialContentDouble Create()
    {
        IContentService service = DispatchProxy.Create<IContentService, SpatialContentDouble>();
        SpatialContentDouble result = (SpatialContentDouble)(object)service;
        result.Service = service;
        return result;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        nameof(IContentService.ResolveReference) => new ContentReference(new ContentReferenceHandle(1), static () => { }),
        _ => throw new NotSupportedException(method?.Name),
    };
}

internal class EngineSpatialDouble : DispatchProxy
{
    internal ISpatialService Service { get; private set; } = null!;

    /// <summary>Sessions the Engine was asked to create; nothing else proves work was reached.</summary>
    internal int SessionsCreated { get; private set; }

    /// <summary>The transform the next character step reports; a walking avatar lands here.</summary>
    internal Vector3 StepTranslation { get; set; } = new(1, 4, 0);

    /// <summary>Every step delta the product proposed, in order.</summary>
    internal List<float> StepSeconds { get; } = [];

    /// <summary>Every motion the product proposed, in order.</summary>
    internal List<CharacterMotion> StepMotions { get; } = [];

    internal static EngineSpatialDouble Create(Vector3? stepTranslation = null)
    {
        ISpatialService service = DispatchProxy.Create<ISpatialService, EngineSpatialDouble>();
        EngineSpatialDouble result = (EngineSpatialDouble)(object)service;
        result.Service = service;
        if (stepTranslation is { } translation) result.StepTranslation = translation;
        return result;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        nameof(ISpatialService.DefaultCharacterControllerConfig) => RepresentativeConfig(),
        nameof(ISpatialService.ValidateCharacterControllerConfig) => null,
        nameof(ISpatialService.CreateSession) => Created(),
        nameof(ISpatialService.ReplaceContentArtifact) => new SpatialContentArtifactReplaceReceipt(),
        nameof(ISpatialService.ProposeCharacterStep) => Step((CharacterStepRequest)arguments![0]!),
        _ => throw new NotSupportedException(method?.Name),
    };

    private SpatialSession Created()
    {
        SessionsCreated += 1;
        return new SpatialSession(new SpatialSessionHandle(1), static () => { });
    }

    private CharacterStepReceipt Step(CharacterStepRequest request)
    {
        StepSeconds.Add(request.Command.StepSeconds);
        StepMotions.Add(request.Motion);
        return Receipt(request);
    }

    private CharacterStepReceipt Receipt(CharacterStepRequest request) => default(CharacterStepReceipt) with
    {
        Generation = 1,
        Transform = new Transform(StepTranslation, Quaternion.Identity, Vector3.One),
        Motion = request.Motion with { Grounded = true, LastCommandSequence = request.Command.Sequence },
        Displacement = new Vector3(1, 0, 0),
    };

    private static CharacterControllerConfig RepresentativeConfig() => default(CharacterControllerConfig) with
    {
        Shape = new CharacterShapeConfig(2.2f, 1.3f, .45f, .03f, .02f),
        Ground = new CharacterGroundConfig(6f, 5f, 4f, 31f, 42f, 7f, 3f, 2f),
        Air = new CharacterAirConfig(4f, 10f, 1f, 4f, 1f, 0f),
        Vertical = new CharacterVerticalConfig(18f, 48f, 46f, 6f, .4f),
        Jump = new CharacterJumpConfig(.2f, .15f, 0f, false),
        Surface = new CharacterSurfaceConfig(.9f, .02f, 16f, 9f, .35f, .04f, .2f, 8f, .2f),
        Recovery = new CharacterRecoveryConfig(.7f, 18f, .002f, .003f),
        Platform = new CharacterPlatformConfig(true, true, true, .8f, 0f, .03f),
        ExternalMotion = new CharacterExternalMotionConfig(1f, 0f, 40f, 70f, 1f, 400f),
        Solver = new CharacterSolverConfig(4, 7, 3, 24, 1, 8f, 48),
    };
}

internal sealed class GraphicsDouble : IGraphicsService
{
    internal MeshResourceCreateRequest? MeshRequest { get; private set; }
    internal IReadOnlyList<AppearanceFact> LastSnapshot { get; private set; } = [];
    internal int SnapshotCalls { get; private set; }
    internal int MaterialCalls { get; private set; }
    internal int AppearanceCalls { get; private set; }

    public Material CreateMaterial(MaterialRequest arg0)
    {
        MaterialCalls++;
        return new Material(new MaterialHandle(1), static () => { });
    }

    public MeshResource CreateMeshResource(MeshResourceCreateRequest arg0)
    {
        MeshRequest = arg0;
        return new MeshResource(new MeshResourceHandle(1), static () => { });
    }

    public Appearance CreateMeshAppearance(MeshResource arg0)
    {
        AppearanceCalls++;
        return new Appearance(new AppearanceHandle(1), static () => { });
    }

    public void PublishSnapshot(ReadOnlySpan<AppearanceFact> values)
    {
        SnapshotCalls++;
        LastSnapshot = values.ToArray();
    }

    public RenderResourceInfo OpenResource(RenderResourceRequest arg0) => throw new NotSupportedException("OpenResource");
    public RenderResourceInfo OpenResourceFromContent(RenderResourceContentRequest arg0) => throw new NotSupportedException("OpenResourceFromContent");
    public Appearance CreateStaticMeshFromContentReference(StaticMeshContentReferenceRequest arg0) => throw new NotSupportedException("CreateStaticMeshFromContentReference");
    public void UpdateMaterial(MaterialUpdateRequest arg0) => throw new NotSupportedException("UpdateMaterial");
    public Material ReplaceMaterial(MaterialUpdateRequest arg0) => throw new NotSupportedException("ReplaceMaterial");
    public Appearance CreatePrimitive(PrimitiveAppearanceRequest arg0) => throw new NotSupportedException("CreatePrimitive");
    public Appearance ReplacePrimitive(PrimitiveAppearanceReplaceRequest arg0) => throw new NotSupportedException("ReplacePrimitive");
    public MeshPartition PartitionMesh(MeshPartitionRequest arg0) => throw new NotSupportedException("PartitionMesh");
    public MeshPartitionReadout ReadMeshPartition(MeshPartition arg0) => throw new NotSupportedException("ReadMeshPartition");
    public MeshResource TakeMeshPartitionPart(MeshPartitionPartRequest arg0) => throw new NotSupportedException("TakeMeshPartitionPart");
    public Appearance CreateStaticMesh(StaticMeshAppearanceRequest arg0) => throw new NotSupportedException("CreateStaticMesh");
    public Appearance CreateStaticMeshFromContent(StaticMeshContentAppearanceRequest arg0) => throw new NotSupportedException("CreateStaticMeshFromContent");
    public Appearance ReplaceStaticMesh(Appearance arg0, StaticMeshAppearanceRequest arg1) => throw new NotSupportedException("ReplaceStaticMesh");
    public Appearance ReplaceStaticMeshFromContent(Appearance arg0, StaticMeshContentAppearanceRequest arg1) => throw new NotSupportedException("ReplaceStaticMeshFromContent");
    public void UpdateStaticMeshMaterials(StaticMeshMaterialUpdateRequest arg0) => throw new NotSupportedException("UpdateStaticMeshMaterials");
    public Appearance CreateSprite(SpriteAppearanceRequest arg0) => throw new NotSupportedException("CreateSprite");
    public Appearance ReplaceSprite(SpriteAppearanceReplaceRequest arg0) => throw new NotSupportedException("ReplaceSprite");
    public SpriteAtlas CreateSpriteAtlas(SpriteAtlasCreateRequest arg0) => throw new NotSupportedException("CreateSpriteAtlas");
    public Appearance CreateSpriteFromAtlas(SpriteFromAtlasRequest arg0) => throw new NotSupportedException("CreateSpriteFromAtlas");
    public Appearance ReplaceSpriteFromAtlas(SpriteFromAtlasReplaceRequest arg0) => throw new NotSupportedException("ReplaceSpriteFromAtlas");
    public void SetSpriteFrame(SpriteFrameUpdateRequest arg0) => throw new NotSupportedException("SetSpriteFrame");
    public void SetSpriteViewport(SpriteViewportUpdateRequest arg0) => throw new NotSupportedException("SetSpriteViewport");
    public SpriteReadout ReadSprite(Appearance arg0) => throw new NotSupportedException("ReadSprite");
    public SpritePlayback CreateSpritePlayback(SpritePlaybackCreateRequest arg0) => throw new NotSupportedException("CreateSpritePlayback");
    public SpritePlaybackReadout ControlSpritePlayback(SpritePlaybackControlRequest arg0) => throw new NotSupportedException("ControlSpritePlayback");
    public SpritePlaybackReadout SelectSpritePlaybackFrame(SpritePlaybackFrameSelectionRequest arg0) => throw new NotSupportedException("SelectSpritePlaybackFrame");
    public SpritePlaybackAdvanceLeaseReceipt AdvanceSpritePlayback(SpritePlaybackAdvanceRequest arg0) => throw new NotSupportedException("AdvanceSpritePlayback");
    public SpritePlaybackSample SampleSpritePlayback(SpritePlaybackSampleRequest arg0) => throw new NotSupportedException("SampleSpritePlayback");
    public SpritePlaybackReadout ReadSpritePlayback(SpritePlayback arg0) => throw new NotSupportedException("ReadSpritePlayback");
    public Light CreateLight(LightRequest arg0) => throw new NotSupportedException("CreateLight");
    public void UpdateLight(LightUpdateRequest arg0) => throw new NotSupportedException("UpdateLight");
    public Light ReplaceLight(LightUpdateRequest arg0) => throw new NotSupportedException("ReplaceLight");
    public LightReadout ReadLight(Light arg0) => throw new NotSupportedException("ReadLight");
    public PresentationReadout ReadPresentation() => throw new NotSupportedException("ReadPresentation");
    public Material CreateAuthoredMaterial(AuthoredMaterialAppearanceRequest arg0) => throw new NotSupportedException("CreateAuthoredMaterial");
}

internal class UiDouble : DispatchProxy
{
    internal IUiService Service { get; private set; } = null!;
    internal UiProjection? LastProjection { get; private set; }
    internal int OpenCalls { get; private set; }

    internal static UiDouble Create()
    {
        IUiService service = DispatchProxy.Create<IUiService, UiDouble>();
        UiDouble result = (UiDouble)(object)service;
        result.Service = service;
        return result;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        nameof(IUiService.OpenStream) => Open(),
        nameof(IUiService.PublishProjection) => Publish((UiProjection)arguments![0]!),
        _ => throw new NotSupportedException(method?.Name),
    };

    private UiStream Open()
    {
        OpenCalls++;
        return new UiStream(new UiStreamHandle(1), static () => { });
    }

    private object? Publish(UiProjection projection)
    {
        LastProjection = projection;
        return null;
    }
}
