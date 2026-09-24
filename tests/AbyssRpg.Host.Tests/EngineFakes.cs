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

    internal void Put(string scope, string key, byte[] payload) => _values[(scope, key)] = new(1, payload.ToArray());

    public PersistenceStore OpenStore(PersistenceOpenRequest request)
    {
        _scope = request.Scope;
        return new(new PersistenceStoreHandle(1), static () => { });
    }

    public PersistenceSaveReceipt Save(PersistenceSaveRequest request)
    {
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

    public static IEngineContext Create(
        IPersistenceService? persistence = null,
        ISpatialService? spatial = null,
        IContentService? content = null)
    {
        IEngineContext context = DispatchProxy.Create<IEngineContext, EngineContextFake>();
        var fake = (EngineContextFake)(object)context;
        fake._persistence = persistence;
        fake._spatial = spatial;
        fake._content = content;
        return context;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        "get_Persistence" => _persistence ?? throw new NotSupportedException(method?.Name),
        "get_Spatial" => _spatial ?? throw new NotSupportedException(method?.Name),
        "get_Content" => _content ?? throw new NotSupportedException(method?.Name),
        _ => throw new NotSupportedException(method?.Name),
    };
}

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

    internal static EngineSpatialDouble Create()
    {
        ISpatialService service = DispatchProxy.Create<ISpatialService, EngineSpatialDouble>();
        EngineSpatialDouble result = (EngineSpatialDouble)(object)service;
        result.Service = service;
        return result;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        nameof(ISpatialService.DefaultCharacterControllerConfig) => RepresentativeConfig(),
        nameof(ISpatialService.ValidateCharacterControllerConfig) => null,
        nameof(ISpatialService.CreateSession) => new SpatialSession(new SpatialSessionHandle(1), static () => { }),
        nameof(ISpatialService.ReplaceContentArtifact) => new SpatialContentArtifactReplaceReceipt(),
        nameof(ISpatialService.ProposeCharacterStep) => Step((CharacterStepRequest)arguments![0]!),
        _ => throw new NotSupportedException(method?.Name),
    };

    private static CharacterStepReceipt Step(CharacterStepRequest request) => default(CharacterStepReceipt) with
    {
        Generation = 1,
        Transform = new Transform(new Vector3(1, 4, 0), Quaternion.Identity, Vector3.One),
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
