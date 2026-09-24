using AbyssRpg.Kit.Controls;
using Rusty.Engine;
using Rusty.Engine.Input;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Movement;

/// <summary>
/// Ruleset-owned UW locomotion policy over Engine FpsInput: walk by default,
/// run while Shift is held with movement, jump on Space (never while
/// swimming), fly vertical on Space/E up and Q down. The original has no
/// crouch, so the FpsInput crouch channel carries fly-descend (Q) and is
/// otherwise ignored. Engine owns collision; the Kit stepping call site
/// plugs in with the session (UW-T02/T08).
/// </summary>
public sealed class UuLocomotionPolicy
{
    private readonly UuMovementTuning _tuning;
    private FpsInput _input;

    public UuLocomotionPolicy(UuMovementTuning tuning)
    {
        _tuning = (tuning ?? throw new ArgumentNullException(nameof(tuning))).Validate();
        _input = new FpsInput(FpsInputConfig.Standard with { Bindings = UuBindings });
    }

    /// <summary>
    /// UW bindings: Standard WASD + mouse-look with Shift run, Space jump,
    /// E use, and Q on the crouch channel for fly-descend.
    /// </summary>
    public static FpsInputBindings UuBindings { get; } = FpsInputBindings.Standard with
    {
        CrouchKey = KeyboardControl.KeyQ,
    };

    /// <summary>Focus and product-mode changes must not leave held keys driving later steps.</summary>
    public void Neutralize() => _input.Physical.Clear();

    public readonly record struct UuMoveIntent(
        bool Running,
        bool Swimming,
        bool Flying,
        bool JumpRequested,
        float Ascend);

    public (CharacterStepControls Controls, UuMoveIntent Intent) BeginStep(
        ReadOnlySpan<ProductInputEvent> inputs, float seconds, bool canMove, bool swimming, bool flying)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        FpsInputFrame frame = _input.Consume(inputs, seconds);

        bool moving = frame.Movement != System.Numerics.Vector2.Zero;
        bool running = canMove && !swimming && frame.SprintHeld && moving;
        float planar = swimming ? _tuning.SwimSpeed : running ? _tuning.RunSpeed : _tuning.WalkSpeed;
        bool jump = canMove && !swimming && !flying && frame.JumpPressed;

        float ascend = 0f;
        if (flying && canMove)
        {
            if (frame.JumpHeld || frame.UseHeld) ascend = _tuning.FlySpeed;
            else if (frame.CrouchHeld) ascend = -_tuning.FlySpeed;
        }

        var controls = new CharacterStepControls(
            JumpPressed: jump,
            JumpHeld: canMove && !swimming && !flying && frame.JumpHeld,
            ForwardSpeed: planar,
            BackwardSpeed: planar,
            StrafeSpeed: swimming ? _tuning.SwimSpeed : _tuning.StrafeSpeed,
            JumpSpeed: _tuning.JumpSpeed,
            VerticalVelocity: flying && canMove ? ascend : null);
        var intent = new UuMoveIntent(running, swimming, flying, jump, ascend);
        return (controls, intent);
    }
}

/// <summary>UW movement hazards as pure policy: falls, drowning air, lava, encumbrance.</summary>
public static class UuMovementHazards
{
    public static float FallDamage(float fallFeet, UuMovementTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (!float.IsFinite(fallFeet) || fallFeet < 0f)
            throw new ArgumentOutOfRangeException(nameof(fallFeet));
        return fallFeet <= tuning.FreeFallFeet ? 0f : (fallFeet - tuning.FreeFallFeet) * tuning.FallDamagePerFoot;
    }

    public static float AirSeconds(int swimSkill, bool encumbered, UuMovementTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (swimSkill < 0) throw new ArgumentOutOfRangeException(nameof(swimSkill));
        float air = tuning.BaseAirSeconds + (swimSkill * tuning.AirSecondsPerSwim);
        return encumbered ? air * tuning.EncumberedAirMultiplier : air;
    }

    public static float EncumberedSpeed(float speed, UuMovementTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return speed * tuning.EncumberedSpeedMultiplier;
    }

    public static float EncumberedJump(float jump, UuMovementTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return jump * tuning.EncumberedJumpMultiplier;
    }
}
