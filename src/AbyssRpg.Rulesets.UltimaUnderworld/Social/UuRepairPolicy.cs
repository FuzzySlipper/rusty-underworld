using AbyssRpg.Rulesets.UltimaUnderworld.Combat;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Social;

/// <summary>
/// Repair: time cost (max 15 minutes of durability*3 - skill - quality/2,
/// always charged), then a Repair-vs-durability roll — critical failure
/// risks ruin (save on d64 &lt;= skill+quality or destroyed; survivors lose
/// 4-11), failure holds, success adds skill/5+3 (cap 63), critical restores
/// to 63. NPC repair runs the same roll with the smith's skill (fee rides
/// with barter). Theft caught in range costs one attitude step (never past
/// Hostile; already-hostile stays). Donor: Anvil repair sequence, Critter
/// StolenFrom.
/// </summary>
public static class UuRepairPolicy
{
    public const int MaxQuality = 63;

    public static int RepairMinutes(int durability, int repairSkill, int quality) =>
        Math.Max(15, durability * 3 - repairSkill - quality / 2);

    public enum RepairResult
    {
        Destroyed,
        Damaged,
        Held,
        Improved,
        Restored,
    }

    public static (RepairResult Result, int Quality) Repair(int quality, int durability, int repairSkill, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        UuStrikeResolution.StrikeResult roll = UuStrikeResolution.RollToHit(repairSkill, durability, rng);
        return roll switch
        {
            UuStrikeResolution.StrikeResult.CritFail => Ruin(quality, repairSkill, rng),
            UuStrikeResolution.StrikeResult.Fail => (RepairResult.Held, quality),
            UuStrikeResolution.StrikeResult.CritSuccess => (RepairResult.Restored, MaxQuality),
            _ => (RepairResult.Improved, Math.Min(quality + repairSkill / 5 + 3, MaxQuality)),
        };
    }

    private static (RepairResult Result, int Quality) Ruin(int quality, int repairSkill, Random rng)
    {
        if (rng.Next(0, 64) > repairSkill + quality) return (RepairResult.Destroyed, 0);
        int damaged = quality - rng.Next(4, 12);
        return damaged <= 0 ? (RepairResult.Destroyed, 0) : (RepairResult.Damaged, damaged);
    }

    /// <summary>Caught theft: one attitude step down (T29 ladder), floored at Hostile.</summary>
    public static int TheftCaught(int attitude) => UuAttitude.Provoke(attitude);
}
