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

    public UuLocomotionPolicy(UuMovementTuning tuning, FpsInputConfig? config = null)
    {
        _tuning = (tuning ?? throw new ArgumentNullException(nameof(tuning))).Validate();
        _input = new FpsInput((config ?? FpsInputConfig.Standard) with { Bindings = UuBindings });
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

    /// <summary>
    /// One admitted input slice read through the product's selected FPS
    /// configuration: locomotion controls, the movement intent, the integrated
    /// look receipt, and the primary attack button facts combat consumes.
    /// </summary>
    public readonly record struct UuPlayerStep(
        CharacterStepControls Controls,
        UuMoveIntent Intent,
        LookReceipt Look,
        bool AttackHeld,
        bool AttackPressed,
        bool AttackReleased);

    public (CharacterStepControls Controls, UuMoveIntent Intent) BeginStep(
        ReadOnlySpan<ProductInputEvent> inputs, float seconds, bool canMove, bool swimming, bool flying)
    {
        UuPlayerStep step = ReadStep(new LookState(0f, 0f), inputs, seconds, canMove, swimming, flying);
        return (step.Controls, step.Intent);
    }

    /// <summary>
    /// Reads one slice and commits look to the authoritative player control
    /// state. Pointer and controller look integrate through Engine Look, so the
    /// selected sensitivity, invert and pitch clamps apply once per update.
    /// </summary>
    public UuPlayerStep BeginPlayerStep(
        PlayerControlState player, ReadOnlySpan<ProductInputEvent> inputs, float seconds,
        bool canMove, bool swimming, bool flying)
    {
        ArgumentNullException.ThrowIfNull(player);
        UuPlayerStep step = ReadStep(
            new LookState(player.YawRadians, player.PitchRadians), inputs, seconds, canMove, swimming, flying);
        player.YawRadians = step.Look.After.YawRadians;
        player.PitchRadians = step.Look.After.PitchRadians;
        return step;
    }

    private UuPlayerStep ReadStep(
        LookState look, ReadOnlySpan<ProductInputEvent> inputs, float seconds,
        bool canMove, bool swimming, bool flying)
    {
        if (!float.IsFinite(seconds) || seconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        FpsInputFrame frame = _input.Consume(inputs, seconds);
        LookReceipt receipt = _input.IntegrateLook(look, frame);

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
        return new UuPlayerStep(
            controls, intent, receipt,
            AttackHeld: _input.Physical.Held(PointerButton.Primary),
            AttackPressed: _input.Physical.Pressed(PointerButton.Primary),
            AttackReleased: _input.Physical.Released(PointerButton.Primary));
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
