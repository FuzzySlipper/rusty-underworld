namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// UW1 avatar vitals policy: hit points grow with strength and level,
/// mana with the Mana skill and intelligence, carrying capacity with
/// strength. Behavior reference: UnderworldGodot
/// src/player/playerdat.cs (RecalculateHPManaMaxWeight). No code is shared.
/// </summary>
public static class UuVitalsPolicy
{
    public sealed record Vitals(int MaxHp, int MaxMana, int MaxWeight);

    public static Vitals Recalculate(int strength, int level, int manaSkill, int intelligence)
    {
        if (strength < 0) throw new ArgumentOutOfRangeException(nameof(strength));
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        if (intelligence < 0) throw new ArgumentOutOfRangeException(nameof(intelligence));
        return new Vitals(
            MaxHp: 0x1E + ((strength * level) / 5),
            MaxMana: ((manaSkill + 1) * intelligence) >> 3,
            MaxWeight: 300 + (strength * 13));
    }
}
