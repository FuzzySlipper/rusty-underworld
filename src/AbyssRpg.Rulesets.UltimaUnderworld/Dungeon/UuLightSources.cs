namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Which items are light sources, read from the item id's own class fields — the
/// same layout the item catalog documents (<c>majorclass = item_id &gt;&gt; 6</c>,
/// <c>classindex = (item_id &gt;&gt; 2) &amp; 0xF</c>).
/// </summary>
/// <remarks>
/// Donor: UnderworldGodot <c>src/interaction/use.cs</c> branches on
/// <c>classindex &lt;= 7</c> inside majorclass 1 as "lights", and
/// <c>src/interaction/pickup.cs</c> turns a lit light off with
/// <c>item_id -= 4</c> — one classindex step — so the lit variant of a light is
/// its id plus four.
/// </remarks>
public static class UuLightSources
{
    /// <summary>The majorclass the emulated game's light sources live in.</summary>
    public const int LightMajorClass = 1;

    /// <summary>The first classindex a lit light occupies; below it the same item is unlit.</summary>
    public const int LitClassIndex = 4;

    /// <summary>How many classindex steps separate an item from its lit variant.</summary>
    public const int LitStep = 4;

    public static int MajorClass(int itemId) => itemId >> 6;

    public static int ClassIndex(int itemId) => (itemId >> 2) & 0xF;

    /// <summary>Whether an id is any light source, lit or not.</summary>
    public static bool IsLight(int itemId) =>
        MajorClass(itemId) == LightMajorClass && ClassIndex(itemId) <= 7;

    /// <summary>Whether an id is a light source that is currently giving light.</summary>
    public static bool IsLit(int itemId) =>
        IsLight(itemId) && ClassIndex(itemId) >= LitClassIndex;

    /// <summary>The same light, lit or unlit.</summary>
    public static int Lit(int itemId) => IsLight(itemId) ? itemId + LitStep : itemId;

    public static int Unlit(int itemId) => IsLight(itemId) ? itemId - LitStep : itemId;
}
