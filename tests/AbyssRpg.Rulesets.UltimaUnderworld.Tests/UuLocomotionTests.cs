using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using Rusty.Engine.Input;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuLocomotionTests
{
    [Fact]
    public void Bindings_keep_standard_movement_with_q_descend()
    {
        Assert.Equal(FpsInputBindings.Standard.ForwardKey, UuLocomotionPolicy.UuBindings.ForwardKey);
        Assert.Equal(FpsInputBindings.Standard.JumpKey, UuLocomotionPolicy.UuBindings.JumpKey);
        Assert.Equal(FpsInputBindings.Standard.SprintKey, UuLocomotionPolicy.UuBindings.SprintKey);
    }

    [Fact]
    public void Tuning_rejects_bad_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (UuMovementTuning.Default with { WalkSpeed = float.NaN }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (UuMovementTuning.Default with { EncumberedSpeedMultiplier = 1.5f }).Validate());
        UuMovementTuning.Default.Validate();
    }

    [Fact]
    public void Idle_input_yields_no_motion()
    {
        var policy = new UuLocomotionPolicy(UuMovementTuning.Default);
        var (controls, intent) = policy.BeginStep([], 0.016f, canMove: true, swimming: false, flying: false);
        Assert.False(intent.Running);
        Assert.False(intent.JumpRequested);
        Assert.Equal(0f, intent.Ascend);
        Assert.False(controls.JumpPressed);
        Assert.Null(controls.VerticalVelocity);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.BeginStep([], 0f, true, false, false));
    }

    [Fact]
    public void Hazards_follow_documented_structure()
    {
        var tuning = UuMovementTuning.Default;
        Assert.Equal(0f, UuMovementHazards.FallDamage(2.0f, tuning));
        Assert.Equal(4.0f, UuMovementHazards.FallDamage(3.0f, tuning));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuMovementHazards.FallDamage(-1f, tuning));
        Assert.Equal(30.0f + (5 * 6.0f), UuMovementHazards.AirSeconds(5, false, tuning));
        Assert.Equal((30.0f + (5 * 6.0f)) * 0.5f, UuMovementHazards.AirSeconds(5, true, tuning));
        Assert.Equal(4.0f * 0.8f, UuMovementHazards.EncumberedSpeed(4.0f, tuning));
        Assert.Equal(5.0f * 0.7f, UuMovementHazards.EncumberedJump(5.0f, tuning));
    }
}
