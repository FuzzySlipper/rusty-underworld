using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Traps;

/// <summary>
/// Trigger types come from the imported object table (UnderworldGodot
/// src/objectdata/triggerobjectdat.cs). Trap effects are never tile triggers.
/// MOVE is adapted from collision contact to tile entry in our tile dungeon;
/// pressure compares the tile's weight, in tenths of stones, to its threshold
/// (src/triggers/trigger.cs and a_pressure_trigger.cs).
/// </summary>
public static class UuTriggerPolicy
{
    // UW1 trigger types (donor src/objectdata/triggerobjectdat.cs triggertypes).
    // UW1 shares 6 between ENTER and UNLOCK and 7 between PRESSURE and OPEN; which
    // one a record means depends on the caller that fires it (a tile or a verb).
    public const int Move = 0;
    public const int StepOn = 1;
    public const int Pickup = 2;
    public const int Use = 4;
    public const int Look = 5;
    public const int Enter = 6;
    public const int Pressure = 7;
    public const int Open = 7;
    public const int Exit = 14;
    public const int PressureRelease = 15;

    /// <summary>Whether a tile trigger compares weight rather than presence.</summary>
    public static bool IsPressure(int type) => type is Pressure or PressureRelease;

    public static int? Type(UuObjectTables tables, int itemId) =>
        itemId is >= 416 and <= 447 ? tables.TriggerTypes[itemId & 15] : null;

    public static bool Fires(int type, bool entering, long weight, int threshold) => type switch
    {
        Move or StepOn or Enter => entering,
        Exit => !entering,
        Pressure => weight >= threshold,
        PressureRelease => weight <= threshold,
        _ => false,             // PICKUP/USE/LOOK are interactions; UW2-only types never run.
    };

    public static long ObjectWeight(AdmittedObject obj, UuItemCatalog items) =>
        checked((long)obj.Quantity * (items.Find(obj.ItemId)?.MassTenthStones ?? 0));
}
