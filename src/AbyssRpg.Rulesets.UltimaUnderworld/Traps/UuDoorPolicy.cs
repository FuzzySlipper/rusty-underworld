using AbyssRpg.Rulesets.UltimaUnderworld.Combat;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Traps;

/// <summary>
/// Doors: lockpicking (skill vs lock level x3; master and dead locks
/// unpickable; critical failure breaks the pick), bashing through door
/// quality (unbreakable class holds), and battering wear (each bash costs
/// the weapon 1 quality — Ours, no donor number found). Secrets open on a
/// Search check against content difficulty and publish discovery.
/// Moongates travel fixed endpoint pairs (content).
/// </summary>
public static class UuDoorPolicy
{
    public const int MasterLockLevel = 0xE;
    public const int DeadLockLevel = 0xF;
    public const int MasterLockSkill = 0x20;
    public const int BashWearPerHit = 1;

    public enum PickResult
    {
        Opened,
        Held,
        BrokePick,
        Unpickable,
    }

    public static PickResult Pick(int lockLevel, int pickSkill, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (lockLevel == DeadLockLevel) return PickResult.Unpickable;
        if (lockLevel == MasterLockLevel && pickSkill < MasterLockSkill) return PickResult.Unpickable;
        UuStrikeResolution.StrikeResult roll = UuStrikeResolution.RollToHit(pickSkill, lockLevel * 3, rng);
        return roll switch
        {
            UuStrikeResolution.StrikeResult.CritFail => PickResult.BrokePick,
            UuStrikeResolution.StrikeResult.Fail => PickResult.Held,
            _ => PickResult.Opened,
        };
    }

    /// <summary>Bash a door: quality absorbs damage (shifted by class); unbreakable holds.</summary>
    public static int BashDoor(int quality, int qualityClass, int damage) =>
        qualityClass >= 3 ? quality : Math.Max(0, quality - (damage >> qualityClass));

    public static int BashWeaponWear(int weaponQuality) => Math.Max(0, weaponQuality - BashWearPerHit);

    public static bool SearchSecret(int searchSkill, int difficulty, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return UuStrikeResolution.RollToHit(searchSkill, difficulty, rng) switch
        {
            UuStrikeResolution.StrikeResult.Fail => false,
            UuStrikeResolution.StrikeResult.CritFail => false,
            _ => true,
        };
    }

    public sealed record MoongateEndpoint(int Level, int TileX, int TileY);

    public static MoongateEndpoint Travel(IReadOnlyList<MoongateEndpoint> endpoints, int gateIndex)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if ((uint)gateIndex >= (uint)endpoints.Count)
            throw new ArgumentOutOfRangeException(nameof(gateIndex));
        return endpoints[gateIndex];
    }
}
