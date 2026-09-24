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

    /// <summary>Charges left after one use; wands die at zero (quality countdown).</summary>
    public static int SpendCharge(int charges)
    {
        if (charges <= 0) throw new ArgumentOutOfRangeException(nameof(charges));
        return charges - 1;
    }

    /// <summary>Worn enchantments work from the moment worn; identification only names them.</summary>
    public static bool WornEffectActive(bool equipped) => equipped;
}

/// <summary>
/// Lore identification: a Lore roll (difficulty 3, donor NameEnchantment
/// default) names the enchantment only on a critical success; lesser
/// results leave the item working but unnamed. Enchantable potion ids
/// are 187-188 in UW1 (donor IsPotion allowlist).
/// </summary>
public static class UuLorePolicy
{
    public const int IdentifyDifficulty = 3;
    public const int EnchantablePotionFirst = 187;
    public const int EnchantablePotionLast = 188;

    public static bool IsEnchantablePotion(int itemId) =>
        itemId >= EnchantablePotionFirst && itemId <= EnchantablePotionLast;

    public static bool NamesEnchantment(int loreSkill, Random rng) =>
        UuStrikeResolution.RollToHit(loreSkill, IdentifyDifficulty, rng)
            == UuStrikeResolution.StrikeResult.CritSuccess;
}
