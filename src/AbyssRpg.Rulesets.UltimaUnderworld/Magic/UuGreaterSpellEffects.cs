namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Greater spell effects (circles 5-8): mind-effect forward radius (3
/// tiles — Ally, Confusion, Paralyze, Curse, Smite apply status to whatever
/// the caller finds there), Flame Wind 14-18, Greater Heal (full vitality),
/// Cure Poison (clears), Reveal (2-tile automap radius), Tremor (boulders
/// in 6m). Maintained greater spells (Daylight, Freeze Time, Iron Flesh,
/// Roaming Sight, Fly, Levitate, Invisibility, Telekinesis, Missile
/// Protection) admit through the max-3 gate.
/// Donor magnitudes: CastX AoE radii, SpawnCascadingEffect 14-18,
/// CastGreaterHeal full, CastCurePoison zero, CastReveal radius 2.
/// </summary>
public static class UuGreaterSpellEffects
{
    public const double MindEffectRadiusTiles = 3.0;
    public const int FlameWindMinDamage = 14;
    public const int FlameWindMaxDamage = 18;
    public const int RevealRadiusTiles = 2;
    public const double TremorRadiusMeters = 6.0;

    public static int FlameWindDamage(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.Next(FlameWindMinDamage, FlameWindMaxDamage + 1);
    }

    public static int GreaterHealAmount(int vitality) => vitality;

    public static int CurePoisonResult() => 0;
}
