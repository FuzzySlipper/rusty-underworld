namespace AbyssRpg.Rulesets.UltimaUnderworld.Movement;

/// <summary>
/// UW movement tuning. Structure matches the original (walk/run/swim/fly,
/// free-fall allowance, lava damage, skill-scaled air); every number is Ours
/// until playtest calibration — speeds are Engine units per second.
/// Manual structure: climb ≤2 ft walking, higher needs jump/fly (pp. 4-5,
/// 20); swim is automatic with no jump or attack, banks ≤2 ft to exit, air
/// follows Swimming skill + encumbrance (p. 21); fly E ascend / Q
/// descend-land (p. 21); lava damages (p. 21); encumbrance slows movement and
/// shortens jumps and swim time (pp. 18, 21).
/// </summary>
public sealed record UuMovementTuning(
    float WalkSpeed,
    float RunSpeed,
    float StrafeSpeed,
    float JumpSpeed,
    float SwimSpeed,
    float FlySpeed,
    float FreeFallFeet,
    float FallDamagePerFoot,
    float LavaDamagePerSecond,
    float BaseAirSeconds,
    float AirSecondsPerSwim,
    float EncumberedAirMultiplier,
    float EncumberedJumpMultiplier,
    float EncumberedSpeedMultiplier,
    float FeetPerUnit)
{
    public static UuMovementTuning Default { get; } = new(
        WalkSpeed: 4.0f,
        RunSpeed: 7.0f,
        StrafeSpeed: 3.5f,
        JumpSpeed: 5.0f,
        SwimSpeed: 2.0f,
        FlySpeed: 5.0f,
        FreeFallFeet: 2.0f,
        FallDamagePerFoot: 4.0f,
        LavaDamagePerSecond: 10.0f,
        BaseAirSeconds: 30.0f,
        AirSecondsPerSwim: 6.0f,
        EncumberedAirMultiplier: 0.5f,
        EncumberedJumpMultiplier: 0.7f,
        EncumberedSpeedMultiplier: 0.8f,
        FeetPerUnit: 1.0f);

    public UuMovementTuning Validate()
    {
        foreach (float value in new[]
        {
            WalkSpeed, RunSpeed, StrafeSpeed, JumpSpeed, SwimSpeed, FlySpeed,
            FreeFallFeet, FallDamagePerFoot, LavaDamagePerSecond,
            BaseAirSeconds, AirSecondsPerSwim,
        })
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Movement tuning must be finite and non-negative.");
        }

        // Engine-units-to-feet scale (Ours until playtest calibration).
        if (!float.IsFinite(FeetPerUnit) || FeetPerUnit <= 0f)
            throw new ArgumentOutOfRangeException(nameof(FeetPerUnit));

        foreach (float factor in new[] { EncumberedAirMultiplier, EncumberedJumpMultiplier, EncumberedSpeedMultiplier })
        {
            if (!float.IsFinite(factor) || factor <= 0f || factor > 1f)
                throw new ArgumentOutOfRangeException(nameof(factor), "Encumbrance factors must be in (0, 1].");
        }

        return this;
    }
}
