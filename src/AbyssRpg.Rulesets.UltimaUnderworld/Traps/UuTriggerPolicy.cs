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
    public static int? Type(UuObjectTables tables, int itemId) =>
        itemId is >= 416 and <= 447 ? tables.TriggerTypes[itemId & 15] : null;

    public static bool Fires(int type, bool entering, long weight, int threshold) => type switch
    {
        0 or 1 or 6 => entering, // MOVE, STEP_ON, ENTER
        14 => !entering,         // EXIT
        7 => weight >= threshold,
        15 => weight <= threshold,
        _ => false,             // PICKUP/USE/LOOK are interactions; UW2-only types never run.
    };

    public static long ObjectWeight(AdmittedObject obj, UuItemCatalog items) =>
        checked((long)obj.Quantity * (items.Find(obj.ItemId)?.MassTenthStones ?? 0));
}
