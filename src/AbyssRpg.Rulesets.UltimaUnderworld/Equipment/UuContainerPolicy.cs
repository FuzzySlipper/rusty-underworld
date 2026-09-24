namespace AbyssRpg.Rulesets.UltimaUnderworld.Equipment;

/// <summary>
/// UW container policy: capacity in 0.1-stone units and an acceptance mask
/// from the OBJECTS.DAT container row (mask 0xFFFF takes all; above 512 the
/// remainder names an accepted content type — runes 0, arrows 1, scrolls 2,
/// edibles 3, keys 4; at or below 512 it names one specific item id).
/// Mask reading follows the primary donor (u16 at row +1); content-type
/// assignment for concrete items belongs to the content catalogs.
/// </summary>
public static class UuContainerPolicy
{
    // Content types for mask values 513-517 (mask - 512).
    public const int ContentRunes = 0;
    public const int ContentArrows = 1;
    public const int ContentScrolls = 2;
    public const int ContentEdibles = 3;
    public const int ContentKeys = 4;

    public static bool AcceptsType(int mask, int contentType, int contentItemId)
    {
        if (mask == 0xFFFF) return true;
        if (mask > 512) return (mask - 512) == contentType;
        return mask == contentItemId;
    }

    public static bool FitsWeight(int capacityTenthStones, int currentLoadTenthStones, int contentMassTenthStones)
    {
        if (capacityTenthStones < 0 || currentLoadTenthStones < 0 || contentMassTenthStones < 0)
            throw new ArgumentOutOfRangeException("Container weights must be non-negative.");
        return checked(currentLoadTenthStones + contentMassTenthStones) <= capacityTenthStones;
    }
}
