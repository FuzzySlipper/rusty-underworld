using System.Text.Json;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// One imported level as the runtime reads it: the Engine collision/navigation
/// artifact it must admit, the product render mesh the slice draws, and the
/// spawn the importer derived. The importer writes this payload; the ruleset is
/// the only runtime owner that interprets it, and it refuses a payload whose
/// declared source is not the emulated game.
/// </summary>
public sealed record UuLevelDefinition(
    int Level,
    SpatialContentArtifact Collision,
    string RenderPath,
    ActorPose Spawn,
    string ProvenanceSource,
    string ProvenanceOrigin);

public static class UuLevelContent
{
    public const int SchemaVersion = 1;
    public const string SourceGame = "UW1";

    public static UuLevelDefinition Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        using JsonDocument document = Parse(payload, payloadLabel);
        JsonElement root = document.RootElement;
        if (Number(root, "schemaVersion") != SchemaVersion)
            throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion {SchemaVersion}.");
        int level = (int)Number(root, "level");
        if (level < 1) throw new InvalidOperationException($"'{payloadLabel}' declares an invalid level number.");

        JsonElement collision = Object(root, "collision");
        string collisionPath = String(collision, "path");
        string collisionSha = String(collision, "sha256");
        JsonElement render = Object(root, "render");
        string renderPath = String(render, "path");
        JsonElement spawn = Object(root, "spawn");
        JsonElement provenance = Object(root, "provenance");
        string source = String(provenance, "source");
        if (!string.Equals(source, SourceGame, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"'{payloadLabel}' was imported from '{source}'; this ruleset imports {SourceGame} only.");

        var pose = new ActorPose(
            new WorldPoint(
                (float)Number(spawn, "x"),
                (float)Number(spawn, "y"),
                (float)Number(spawn, "z")),
            (float)Number(spawn, "yawRadians"));

        return new UuLevelDefinition(
            level,
            new SpatialContentArtifact(collisionPath, ParseSha256(collisionSha, payloadLabel), NavigationGridId: 0),
            renderPath,
            pose,
            source,
            String(provenance, "origin"));
    }

    /// <summary>Parses the importer's "sha256:&lt;hex&gt;" identity into the Engine's content words.</summary>
    public static ContentSha256 ParseSha256(string value, string payloadLabel)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.StartsWith("sha256:", StringComparison.Ordinal)
            || value.Length != "sha256:".Length + (ContentHashing.Sha256Bytes * 2))
        {
            throw new InvalidOperationException($"'{payloadLabel}' must declare a 'sha256:<64 hex>' content identity.");
        }

        byte[] digest;
        try
        {
            digest = Convert.FromHexString(value.AsSpan("sha256:".Length));
        }
        catch (FormatException error)
        {
            throw new InvalidOperationException($"'{payloadLabel}' declares a malformed SHA-256 digest.", error);
        }

        return ContentHashing.FromSha256(digest);
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

    private static JsonElement Required(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new InvalidOperationException($"Required '{name}' is missing.");

    private static JsonElement Object(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidOperationException($"'{name}' must be an object.");
    }

    private static string String(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidOperationException($"'{name}' must be a non-empty string.");
    }

    private static double Number(JsonElement root, string name)
    {
        JsonElement value = Required(root, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)
            ? number
            : throw new InvalidOperationException($"'{name}' must be a finite number.");
    }
}
