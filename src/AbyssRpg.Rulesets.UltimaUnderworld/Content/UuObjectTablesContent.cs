using System.Text.Json;
using AbyssRpg.Rulesets.UltimaUnderworld.Critters;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The runtime slice of the emulated game's object definition tables: which
/// item ids are critters and with what statistics, and which are containers and
/// with what capacity. Item identity comes from item id classes, which the
/// donor documents in <c>src/World/uwobject.cs</c> as
/// <c>majorclass = item_id &gt;&gt; 6</c>, <c>minorclass = (item_id &amp; 0x30) &gt;&gt; 4</c>,
/// and <c>classindex = item_id &amp; 0xF</c>: critters are majorclass 1
/// (item id 64-127) and containers are majorclass 2 with minorclass 0
/// (item id 128-143).
/// </summary>
public sealed record UuObjectTables(
    IReadOnlyDictionary<int, UuCritterFactory.CritterDefinition> Critters,
    IReadOnlyDictionary<int, UuContainerDefinition> Containers);

/// <summary>One container item id's source capacity and mask, with its class index.</summary>
public sealed record UuContainerDefinition(
    int ItemId,
    int ClassIndex,
    int CapacityTenthStones,
    int ObjectsMask,
    int Slots);

public static class UuObjectTablesContent
{
    public const int SchemaVersion = 1;
    public const string PackId = "abyssrpg.object-tables";
    public const string SourceGame = "UW1";

    /// <summary>Item id class of a placed object, per the donor's object record layout.</summary>
    public static int MajorClass(int itemId) => itemId >> 6;

    /// <summary>True for an item id the critter table defines (majorclass 1: 64-127).</summary>
    public static bool IsCritterItem(int itemId) => MajorClass(itemId) == 1;

    /// <summary>True for an item id the container table defines (majorclass 2, minorclass 0: 128-143).</summary>
    public static bool IsContainerItem(int itemId) => MajorClass(itemId) == 2 && ((itemId & 0x30) >> 4) == 0;

    public static UuObjectTables Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        using JsonDocument document = Parse(payload, payloadLabel);
        JsonElement root = document.RootElement;
        if (Number(root, "schemaVersion") != SchemaVersion)
            throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion {SchemaVersion}.");
        if (root.TryGetProperty("source", out JsonElement source) && source.ValueKind == JsonValueKind.Object)
        {
            string? game = Text(source, "SourceGame") ?? Text(source, "game");
            if (game is not null && !string.Equals(game, SourceGame, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{payloadLabel}' was imported from '{game}'; this ruleset imports {SourceGame} only.");
        }

        var critters = new Dictionary<int, UuCritterFactory.CritterDefinition>();
        foreach (JsonElement row in Required(root, "critters").EnumerateArray())
        {
            int itemId = (int)Number(row, "itemId");
            if (!IsCritterItem(itemId))
                throw new InvalidOperationException($"'{payloadLabel}' defines critter item id {itemId}, which is not majorclass 1.");
            critters[itemId] = new UuCritterFactory.CritterDefinition(
                ItemId: itemId,
                AvgHp: (int)Number(row, "avgHp"),
                Strength: (int)Number(row, "strength"),
                Dexterity: (int)Number(row, "dexterity"),
                Intelligence: (int)Number(row, "intelligence"),
                Speed: (int)Number(row, "speed"),
                CorpseIndex: (int)Number(row, "corpseIndex"));
        }

        var containers = new Dictionary<int, UuContainerDefinition>();
        foreach (JsonElement row in Required(root, "containers").EnumerateArray())
        {
            int itemId = (int)Number(row, "itemId");
            if (!IsContainerItem(itemId))
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' defines container item id {itemId}, which is not majorclass 2 minorclass 0.");
            }

            containers[itemId] = new UuContainerDefinition(
                itemId,
                itemId & 0xF,
                (int)Number(row, "capacityTenthStones"),
                (int)Number(row, "objectsMask"),
                (int)Number(row, "slots"));
        }

        if (critters.Count == 0 || containers.Count == 0)
            throw new InvalidOperationException($"'{payloadLabel}' must define at least one critter and one container.");

        return new UuObjectTables(critters, containers);
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> payload, string label)
    {
        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{label}' is not valid JSON: {error.Message}", error);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement Required(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidOperationException($"Required '{name}' is missing.");

    private static double Number(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number
            : throw new InvalidOperationException($"'{name}' must be a finite number.");
    }
}
