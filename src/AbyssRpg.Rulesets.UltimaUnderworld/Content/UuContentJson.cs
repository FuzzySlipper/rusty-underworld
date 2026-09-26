using System.Text.Json;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The small JSON reads every content reader in this ruleset shares: a required
/// member of a declared shape, and values whose kind is checked with the error
/// naming what was expected. A pack with a broken member fails when it is read,
/// not later when a mechanic happens to touch it.
/// </summary>
public static class UuContentJson
{
    public static JsonElement Required(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out JsonElement value))
            throw new InvalidOperationException($"Required '{name}' is missing.");
        return value;
    }

    public static JsonElement Array(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidOperationException($"'{name}' must be an array.");
    }

    public static string Text(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : throw new InvalidOperationException($"'{name}' must be a string.");
    }

    public static bool Boolean(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException($"'{name}' must be a boolean."),
        };
    }

    public static double Number(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number
            : throw new InvalidOperationException($"'{name}' must be a finite number.");
    }

    public static double OptionalNumber(JsonElement root, string name, double fallback = 0d)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out JsonElement value)) return fallback;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number
            : fallback;
    }
}
