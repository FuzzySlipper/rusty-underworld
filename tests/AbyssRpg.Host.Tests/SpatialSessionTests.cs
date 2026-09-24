using System.Numerics;
using System.Reflection;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class SpatialSessionTests
{
    [Fact]
    public void Step_runs_policy_through_collision_into_consequences()
    {
        var spatial = SpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(spatial: spatial.Service, content: ContentDouble.Create().Service);
        var player = new PlayerControlState(new WorldPoint(0, 10, 0), 0f, 0f);
        player.Motion = Motion(grounded: false, peakY: 10f);
        using var session = new AbyssSpatialSession(
            engine,
            new SpatialContentArtifact("spatial/test", new ContentSha256(1, 2, 3, 4), 1),
            new SpatialTuning(.5d, 8, 8, 1),
            player);
        var policy = new UuLocomotionPolicy(UuMovementTuning.Default);
        var update = new ProductUpdateState(1f / 60f);

        AbyssSpatialSession.LocomotionStepResult step = session.StepLocomotion(
            policy, [], update, UuMovementTuning.Default,
            canMove: true, swimming: false, flying: false);

        Assert.NotNull(step.Receipt);
        Assert.Equal((10f - 4f - 2f) * 4f, step.FallDamage);
        Assert.True(step.Moved);
        Assert.Equal(new Vector3(1, 4, 0), player.Position!.Value.ToVector());
    }

    private static CharacterMotion Motion(bool grounded, float peakY) => new(
        ControlledVelocity: Vector3.Zero, ExternalVelocity: Vector3.Zero,
        Grounded: grounded, Stance: CharacterStance.Standing,
        JumpBufferRemaining: 0f, CoyoteRemaining: 0f, LandingLockoutRemaining: 0f,
        SupportEntityPresent: false, SupportEntity: 0,
        SupportLocalAnchor: Vector3.Zero, SupportPreviousTranslation: Vector3.Zero,
        SupportPreviousRotation: Quaternion.Identity, SupportPointVelocity: Vector3.Zero,
        FallOriginY: peakY, PeakY: peakY, LastCommandSequence: 0, CollisionWorldHash: 0);

    private class ContentDouble : DispatchProxy
    {
        internal IContentService Service { get; private set; } = null!;

        internal static ContentDouble Create()
        {
            IContentService service = DispatchProxy.Create<IContentService, ContentDouble>();
            ContentDouble result = (ContentDouble)(object)service;
            result.Service = service;
            return result;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
        {
            nameof(IContentService.ResolveReference) => new ContentReference(new ContentReferenceHandle(1), static () => { }),
            _ => throw new NotSupportedException(method?.Name),
        };
    }

    private class SpatialDouble : DispatchProxy
    {
        internal ISpatialService Service { get; private set; } = null!;

        internal static SpatialDouble Create()
        {
            ISpatialService service = DispatchProxy.Create<ISpatialService, SpatialDouble>();
            SpatialDouble result = (SpatialDouble)(object)service;
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
}
