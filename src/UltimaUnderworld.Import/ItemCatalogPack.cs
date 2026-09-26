using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Emits the runtime-facing item catalog from the operator's own data: the
/// common object table (COMOBJ.DAT) carries each item id's mass, height, radius
/// and pickup flag, and the string archive's block 4 carries its name indexed by
/// item id (donor: <c>src/utility/StringLoader.cs</c> GetSimpleObjectNameUW).
/// Quantities and articles in the raw names are placeholders, not part of it.
/// </summary>
public static class ItemCatalogPack
{
    public const int SchemaVersion = 1;
    public const string PackId = "abyssrpg.item-catalog";

    /// <summary>The string block that holds item names, indexed by item id.</summary>
    public const int NameBlock = 4;

    public sealed record ItemRow(
        int ItemId,
        string Name,
        int MonetaryValue,
        int MassTenthStones,
        int Height,
        int Radius,
        bool CanBePickedUp,
        int Class,
        int MinorClass,
        int ClassIndex);

    public sealed record Catalog(
        IReadOnlyList<ItemRow> Items,
        UwTableProvenance CommonObjects,
        UwTableProvenance Strings);

    public static Catalog Emit(
        IReadOnlyList<CommonObjDatReader.CommonObjRow> commonObjects,
        StringsPakReader.DecodedStrings strings,
        UwTableProvenance commonProvenance,
        UwTableProvenance stringsProvenance)
    {
        ArgumentNullException.ThrowIfNull(commonObjects);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(commonProvenance);
        ArgumentNullException.ThrowIfNull(stringsProvenance);

        IReadOnlyList<string> names = strings.Blocks.TryGetValue(NameBlock, out IReadOnlyList<string>? block)
            ? block
            : [];
        ItemRow[] items = commonObjects
            .Select((row, itemId) => new ItemRow(
                itemId,
                CleanName(itemId < names.Count ? names[itemId] : null),
                row.MonetaryValue,
                row.MassTenthStones,
                row.Height,
                row.Radius,
                row.CanBePickedUp,
                // The class of an item id follows the donor's object record
                // layout: majorclass = item_id >> 6, minorclass = (item_id & 0x30) >> 4,
                // classindex = item_id & 0xF (src/World/uwobject.cs).
                itemId >> 6,
                (itemId & 0x30) >> 4,
                itemId & 0xF))
            .ToArray();
        return new Catalog(items, commonProvenance, stringsProvenance);
    }

    /// <summary>
    /// The name without its quantity placeholder or article: a raw entry reads
    /// like "a_&amp;torch" or "some&amp;arrows", and only the noun is the item's.
    /// </summary>
    public static string CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string value = raw;
        int underscore = value.IndexOf('_', StringComparison.Ordinal);
        if (underscore >= 0) value = value[(underscore + 1)..];
        int ampersand = value.IndexOf('&', StringComparison.Ordinal);
        if (ampersand >= 0) value = value[..ampersand];
        return value.Trim();
    }

    public static string ToJson(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var document = new
        {
            schemaVersion = SchemaVersion,
            source = new
            {
                commonObjects = catalog.CommonObjects,
                strings = catalog.Strings,
            },
            items = catalog.Items.Select(item => new
            {
                itemId = item.ItemId,
                name = item.Name,
                value = item.MonetaryValue,
                massTenthStones = item.MassTenthStones,
                height = item.Height,
                radius = item.Radius,
                canPickUp = item.CanBePickedUp,
                @class = item.Class,
                minorClass = item.MinorClass,
                classIndex = item.ClassIndex,
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }
}
