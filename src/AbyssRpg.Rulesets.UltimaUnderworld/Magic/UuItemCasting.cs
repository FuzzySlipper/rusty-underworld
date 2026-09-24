using AbyssRpg.Rulesets.UltimaUnderworld.Combat;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Item-borne casting reuses the same resolution: scrolls cast their bound
/// spell once (consumed), wands spend linked charges (quality countdown),
/// potions quaff into instant effects, worn enchantments hold presence
/// while equipped. No separate hit rules — damage shares the melee path.
/// </summary>
public static class UuItemCasting
{
    public enum ItemSource
    {
        Scroll,
        Wand,
        Potion,
        WornEnchantment,
    }

    /// <summary>
    /// Fire a wand: charges count down, and the dregs cast fires at zero
    /// before the wand cracks (donor fires CastObjectSpell before the
    /// quality check, cracking only when already empty). Returns the
    /// remaining charges and whether this cast cracked the wand.
    /// </summary>
    public static (int Remaining, bool Cracked) FireWand(int charges)
    {
        if (charges < 0) throw new ArgumentOutOfRangeException(nameof(charges));
        return charges == 0 ? (0, true) : (charges - 1, false);
    }

    /// <summary>Charges left after one scroll/potion use (single-use items).</summary>
    public static int SpendCharge(int charges) =>
        charges <= 0
            ? throw new ArgumentOutOfRangeException(nameof(charges))
            : charges - 1;

    /// <summary>Worn enchantments work from the moment worn; identification only names them.</summary>
    public static bool WornEffectActive(bool equipped) => equipped;
}

/// <summary>
/// Lore identification: a Lore roll (difficulty 8, donor look check) names
/// the enchantment only on a critical success; lesser results leave the
/// item working but unnamed. Enchantable potion ids are 187-188 in UW1
/// (donor IsPotion allowlist).
/// </summary>
public static class UuLorePolicy
{
    public const int IdentifyDifficulty = 8;
    public const int EnchantablePotionFirst = 187;
    public const int EnchantablePotionLast = 188;

    public static bool IsEnchantablePotion(int itemId) =>
        itemId >= EnchantablePotionFirst && itemId <= EnchantablePotionLast;

    public static bool NamesEnchantment(int loreSkill, Random rng) =>
        UuStrikeResolution.RollToHit(loreSkill, IdentifyDifficulty, rng)
            == UuStrikeResolution.StrikeResult.CritSuccess;
}
