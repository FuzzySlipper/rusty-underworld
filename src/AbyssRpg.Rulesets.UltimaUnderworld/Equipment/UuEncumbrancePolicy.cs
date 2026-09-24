namespace AbyssRpg.Rulesets.UltimaUnderworld.Equipment;

/// <summary>
/// UW encumbrance policy: carrying past the weight maximum (300 + STR*13)
/// engages the movement penalties (T10 multipliers); anything heavier cannot
/// be picked up. Threshold structure is Ours; the maximum itself is donor
/// (RecalculateHPManaMaxWeight).
/// </summary>
public static class UuEncumbrancePolicy
{
    public static bool IsEncumbered(int carriedTenthStones, int maxTenthStones)
    {
        if (carriedTenthStones < 0 || maxTenthStones < 0)
            throw new ArgumentOutOfRangeException("Weights must be non-negative.");
        return carriedTenthStones > maxTenthStones;
    }

    public static bool CanLift(int carriedTenthStones, int maxTenthStones, int massTenthStones)
    {
        if (massTenthStones < 0) throw new ArgumentOutOfRangeException(nameof(massTenthStones));
        return !IsEncumbered(carriedTenthStones, maxTenthStones)
            && checked(carriedTenthStones + massTenthStones) <= maxTenthStones;
    }
}
