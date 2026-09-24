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
        var spatial = EngineSpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(spatial: spatial.Service, content: SpatialContentDouble.Create().Service);
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
}
