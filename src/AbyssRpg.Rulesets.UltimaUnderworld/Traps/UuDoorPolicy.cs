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

    /// <summary>
    /// Pick a lock: roll first (PickLock + 1 vs lock x3, donor player flow;
    /// the critter routine omits the +1 — disagreement recorded, player flow
    /// followed), then force dead/master locks to hold. A critical failure
    /// breaks the pick only if a second DEX check (vs 20) also fails.
    /// </summary>
    public static PickResult Pick(int lockLevel, int pickSkill, int dexterity, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        UuStrikeResolution.StrikeResult roll = UuStrikeResolution.RollToHit(pickSkill + 1, lockLevel * 3, rng);
        bool locked = lockLevel == DeadLockLevel
            || (lockLevel == MasterLockLevel && pickSkill < MasterLockSkill);
        if (roll == UuStrikeResolution.StrikeResult.CritFail)
        {
            return UuStrikeResolution.RollToHit(dexterity, 20, rng) switch
            {
                UuStrikeResolution.StrikeResult.Fail => PickResult.BrokePick,
                UuStrikeResolution.StrikeResult.CritFail => PickResult.BrokePick,
                _ => PickResult.Held,
            };
        }

        if (locked) return PickResult.Unpickable;
        return roll == UuStrikeResolution.StrikeResult.Fail ? PickResult.Held : PickResult.Opened;
    }

    /// <summary>
    /// Bash a door: pristine (63) and unbreakable-class doors hold; quality
    /// absorbs the rest shifted by class. Callers unlock and open on zero.
    /// </summary>
    public static int BashDoor(int quality, int qualityClass, int damage) =>
        quality >= 63 || qualityClass >= 3 ? quality : Math.Max(0, quality - (damage >> qualityClass));

    public static int BashWeaponWear(int weaponQuality) => Math.Max(0, weaponQuality - BashWearPerHit);

    /// <summary>
    /// Search a secret: caller marks the door discovered/open and reveals
    /// it on the automap when true.
    /// </summary>
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
