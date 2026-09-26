using System.Text.Json;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The item catalog the import produces: every item id the emulated game defines,
/// with the name the string archive gives it, its mass, and whether it can be
/// picked up. Item identity follows the donor's object record layout
/// (<c>majorclass = item_id &gt;&gt; 6</c>), so the class fields here are the
/// runtime's own reading of an item id rather than a second table.
/// </summary>
public sealed record UuItemCatalog(IReadOnlyDictionary<int, UuItemDefinition> Items)
{
    /// <summary>The item id's definition, or null when the catalog does not define it.</summary>
    public UuItemDefinition? Find(int itemId) => Items.TryGetValue(itemId, out UuItemDefinition? item) ? item : null;
}

/// <summary>One item id: its name, its mass in tenths of stones, and its pickup flag.</summary>
public sealed record UuItemDefinition(
    int ItemId,
    string Name,
    int MonetaryValue,
    int MassTenthStones,
    int Height,
    int Radius,
    bool CanPickUp);

public static class UuItemCatalogContent
{
    public const string PackId = "abyssrpg.item-catalog";
    public const int SchemaVersion = 1;

    public static UuItemCatalog Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{payloadLabel}' is not valid JSON: {error.Message}", error);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (Number(root, "schemaVersion") != SchemaVersion)
                throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion {SchemaVersion}.");

            var items = new Dictionary<int, UuItemDefinition>();
            foreach (JsonElement row in Array(root, "items").EnumerateArray())
            {
                int itemId = (int)Number(row, "itemId");
                if (itemId < 0)
                    throw new InvalidOperationException($"'{payloadLabel}' declares item id {itemId}.");
                if (!items.TryAdd(itemId, new UuItemDefinition(
                    itemId,
                    Text(row, "name"),
                    (int)UuContentJson.OptionalNumber(row, "value"),
                    (int)Number(row, "massTenthStones"),
                    (int)Number(row, "height"),
                    (int)Number(row, "radius"),
                    Boolean(row, "canPickUp"))))
                {
                    throw new InvalidOperationException($"'{payloadLabel}' defines item id {itemId} twice.");
                }
            }

            if (items.Count == 0)
                throw new InvalidOperationException($"'{payloadLabel}' defines no items.");

            return new UuItemCatalog(items);
        }
    }

    private static JsonElement Required(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidOperationException($"Required '{name}' is missing.");

    private static JsonElement Array(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidOperationException($"'{name}' must be an array.");
    }

    private static string Text(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : throw new InvalidOperationException($"'{name}' must be a string.");
    }

    private static bool Boolean(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException($"'{name}' must be a boolean."),
        };
    }

    private static double Number(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number
            : throw new InvalidOperationException($"'{name}' must be a finite number.");
    }
}
