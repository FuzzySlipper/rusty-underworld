using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using Rusty.Engine;
using Rusty.Engine.Input;
using System.Text;
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
        Assert.Equal(KeyboardControl.KeyQ, UuLocomotionPolicy.UuBindings.CrouchKey);
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
    public void Driven_input_maps_run_swim_and_fly()
    {
        var policy = new UuLocomotionPolicy(UuMovementTuning.Default);

        var (runControls, run) = policy.BeginStep(
            [Key(KeyboardControl.ShiftLeft), Key(KeyboardControl.KeyW)], 0.016f, canMove: true, swimming: false, flying: false);
        Assert.True(run.Running);
        Assert.Equal(UuMovementTuning.Default.RunSpeed, runControls.ForwardSpeed);

        var (swimControls, swim) = policy.BeginStep(
            [Key(KeyboardControl.KeyW)], 0.016f, canMove: true, swimming: true, flying: false);
        Assert.True(swim.Swimming);
        Assert.False(swim.JumpRequested);
        Assert.False(swimControls.JumpPressed);
        Assert.Equal(UuMovementTuning.Default.SwimSpeed, swimControls.ForwardSpeed);

        var (flyUp, up) = policy.BeginStep(
            [Key(KeyboardControl.Space)], 0.016f, canMove: true, swimming: false, flying: true);
        Assert.Equal(UuMovementTuning.Default.FlySpeed, up.Ascend);
        Assert.Equal(UuMovementTuning.Default.FlySpeed, flyUp.VerticalVelocity);
        Assert.False(up.JumpRequested);

        var policy2 = new UuLocomotionPolicy(UuMovementTuning.Default);
        var (flyDown, down) = policy2.BeginStep(
            [Key(KeyboardControl.KeyQ)], 0.016f, canMove: true, swimming: false, flying: true);
        Assert.Equal(-UuMovementTuning.Default.FlySpeed, down.Ascend);
        Assert.Equal(-UuMovementTuning.Default.FlySpeed, flyDown.VerticalVelocity);
    }

    private static ProductInputEvent Key(KeyboardControl key) => new(
        InputEventKind.Key, InputEdge.Pressed, InputDevice.None, InputChannel.None, InputAxis.None, key,
        PointerButton.None, ControllerButton.None, ControllerAxis.None, InputClearReason.None,
        InputValueKind.None, InputPhase.None, InputProvenance.None, default, default, default,
        0F, 0F, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(""), ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

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
