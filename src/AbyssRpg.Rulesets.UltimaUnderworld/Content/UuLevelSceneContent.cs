using System.Numerics;
using System.Text.Json;
using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// One level's visible geometry: parallel position/normal/color streams with
/// triangle indices, exactly the shape the Engine's mesh-resource admission
/// takes. The importer emits it from the same tile selection the collision
/// artifact uses; the ruleset only interprets the payload.
/// </summary>
public sealed record UuLevelScene(
    int Level,
    Vector3[] Positions,
    Vector3[] Normals,
    Color[] Colors,
    uint[] Indices)
{
    public int TriangleCount => Indices.Length / 3;
}

public static class UuLevelSceneContent
{
    public const int SchemaVersion = 1;

    public static UuLevelScene Read(ReadOnlyMemory<byte> payload, string payloadLabel)
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
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out JsonElement version)
                || version.ValueKind != JsonValueKind.Number || version.GetInt32() != SchemaVersion)
            {
                throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion {SchemaVersion}.");
            }

            Vector3[] positions = Vectors(root, "positions", 3, payloadLabel);
            Vector3[] normals = Vectors(root, "normals", 3, payloadLabel);
            Color[] colors = Colors(root, payloadLabel);
            uint[] indices = Indices(root, payloadLabel);
            if (normals.Length != positions.Length)
                throw new InvalidOperationException($"'{payloadLabel}' normals must match positions.");
            if (colors.Length != positions.Length)
                throw new InvalidOperationException($"'{payloadLabel}' colors must match positions.");
            if (positions.Length < 3 || indices.Length < 3 || indices.Length % 3 != 0)
                throw new InvalidOperationException($"'{payloadLabel}' must carry complete triangles.");
            foreach (uint index in indices)
            {
                if (index >= positions.Length)
                    throw new InvalidOperationException($"'{payloadLabel}' has an index outside its vertex streams.");
            }

            int level = root.TryGetProperty("level", out JsonElement levelElement) && levelElement.ValueKind == JsonValueKind.Number
                ? levelElement.GetInt32()
                : 0;
            return new UuLevelScene(level, positions, normals, colors, indices);
        }
    }

    private static JsonElement Array(JsonElement root, string name, string label) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidOperationException($"'{label}' requires an array '{name}'.");

    private static Vector3[] Vectors(JsonElement root, string name, int components, string label)
    {
        JsonElement array = Array(root, name, label);
        var result = new Vector3[array.GetArrayLength()];
        int at = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != components)
                throw new InvalidOperationException($"'{label}' {name}[{at}] must hold {components} numbers.");
            var values = new float[components];
            int component = 0;
            foreach (JsonElement value in element.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float number) || !float.IsFinite(number))
                    throw new InvalidOperationException($"'{label}' {name}[{at}] must hold finite numbers.");
                values[component++] = number;
            }

            result[at++] = new Vector3(values[0], values[1], values[2]);
        }

        return result;
    }

    private static Color[] Colors(JsonElement root, string label)
    {
        JsonElement array = Array(root, "colors", label);
        var result = new Color[array.GetArrayLength()];
        int at = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 4)
                throw new InvalidOperationException($"'{label}' colors[{at}] must hold four numbers.");
            var values = new float[4];
            int component = 0;
            foreach (JsonElement value in element.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float number) || !float.IsFinite(number))
                    throw new InvalidOperationException($"'{label}' colors[{at}] must hold finite numbers.");
                values[component++] = number;
            }

            result[at++] = new Color(values[0], values[1], values[2], values[3]);
        }

        return result;
    }

    private static uint[] Indices(JsonElement root, string label)
    {
        JsonElement array = Array(root, "indices", label);
        var result = new uint[array.GetArrayLength()];
        int at = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint index))
                throw new InvalidOperationException($"'{label}' indices[{at}] must be an unsigned integer.");
            result[at++] = index;
        }

        return result;
    }
}
