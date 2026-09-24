namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Maintained-effect stability classes: stable effects hold (stability 1),
/// unstable ones roll their class dice (0x80: 3d24, 0x40: 2d8, 0: 2d3).
/// Stability ticks down on the game clock; the effect ends at zero.
/// Donor: SpellCasting.PlayerActiveStatusEffectSpells stability switch.
/// </summary>
public static class UuSpellStability
{
    public const int StableClass = 1;
    public const int UnstableHighClass = 0x80;
    public const int UnstableMidClass = 0x40;
    public const int UnstableLowClass = 0;

    public static int RollStability(int stabilityClass, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return stabilityClass switch
        {
            StableClass => 1,
            UnstableHighClass => Roll(3, 24, rng),
            UnstableMidClass => Roll(2, 8, rng),
            UnstableLowClass => Roll(2, 3, rng),
            _ => 0,
        };
    }

    private static int Roll(int dice, int sides, Random rng)
    {
        int total = 0;
        for (int i = 0; i < dice; i++) total += rng.Next(1, sides + 1);
        return total;
    }
}
