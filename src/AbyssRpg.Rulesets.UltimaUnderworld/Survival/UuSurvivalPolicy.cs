namespace AbyssRpg.Rulesets.UltimaUnderworld.Survival;

/// <summary>
/// Survival state ticked over real seconds: hunger (+1/60s, cap 255,
/// starving at 224+ with damage every 60s), fatigue (+1/300s, cap 30,
/// fatigued at 27+ with damage every 90s), poison (wears 25-35s per point,
/// damages every 15-20s), drunkenness (wears 2-5s per point). Damage rolls
/// use the shared d6 path; the caller applies them to vitals.
/// Donor: PlayerObject survival block (per-second timers, thresholds,
/// ranges). No code shared.
/// </summary>
public sealed class UuSurvivalState
{
    public const int HungerCap = 255;
    public const int StarvingAt = 224;
    public const int FatigueCap = 30;
    public const int FatiguedAt = 27;

    public int Hunger { get; set; }
    public int Fatigue { get; set; }
    public int Poison { get; set; }
    public int Drunkenness { get; set; }

    public double HungerTimer { get; set; } = 60.0;
    public double HungerDamageTimer { get; set; } = 60.0;
    public double FatigueTimer { get; set; } = 300.0;
    public double FatigueDamageTimer { get; set; } = 90.0;
    public double PoisonTimer { get; set; } = 30.0;
    public double PoisonDamageTimer { get; set; } = 17.5;
    public double DrunkTimer { get; set; } = 3.5;
}

public sealed record SurvivalTickResult(int HungerDamage, int FatigueDamage, int PoisonDamage);

public static class UuSurvivalPolicy
{
    /// <summary>
    /// Poison protection (resistance spell family) triples both poison
    /// timers: wear-off quickens, injury slows. Donor PlayerObject rates.
    /// </summary>
    public static SurvivalTickResult Tick(UuSurvivalState state, double seconds, Random rng, bool hasPoisonProtection = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rng);
        if (!double.IsFinite(seconds) || seconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        double protection = hasPoisonProtection ? 3.0 : 1.0;

        int hungerDamage = 0, fatigueDamage = 0, poisonDamage = 0;

        state.HungerTimer -= seconds;
        if (state.HungerTimer < 0d)
        {
            state.HungerTimer = 60.0;
            state.Hunger = Math.Min(state.Hunger + 1, UuSurvivalState.HungerCap);
        }

        if (state.Hunger >= UuSurvivalState.StarvingAt)
        {
            state.HungerDamageTimer -= seconds;
            if (state.HungerDamageTimer < 0d)
            {
                state.HungerDamageTimer = 60.0;
                hungerDamage = Combat.UuStrikeResolution.RollDamage(3, rng);
            }
        }

        state.FatigueTimer -= seconds;
        if (state.FatigueTimer < 0d)
        {
            state.FatigueTimer = 300.0;
            state.Fatigue = Math.Min(state.Fatigue + 1, UuSurvivalState.FatigueCap);
        }

        if (state.Fatigue >= UuSurvivalState.FatiguedAt)
        {
            state.FatigueDamageTimer -= seconds;
            if (state.FatigueDamageTimer < 0d)
            {
                state.FatigueDamageTimer = 90.0;
                fatigueDamage = Combat.UuStrikeResolution.RollDamage(3, rng);
            }
        }

        if (state.Poison > 0)
        {
            state.PoisonTimer -= seconds * protection;
            if (state.PoisonTimer <= 0d)
            {
                state.Poison--;
                state.PoisonTimer = 25.0 + rng.NextDouble() * 10.0;
            }

            state.PoisonDamageTimer -= seconds / protection;
            if (state.PoisonDamageTimer <= 0d)
            {
                poisonDamage = Combat.UuStrikeResolution.RollDamage(Math.Max(1, state.Poison), rng);
                state.PoisonDamageTimer = 15.0 + rng.NextDouble() * 5.0;
            }
        }

        if (state.Drunkenness > 0)
        {
            state.DrunkTimer -= seconds;
            if (state.DrunkTimer <= 0d)
            {
                state.Drunkenness--;
                state.DrunkTimer = 2.0 + rng.NextDouble() * 3.0;
            }
        }

        return new SurvivalTickResult(hungerDamage, fatigueDamage, poisonDamage);
    }
}
