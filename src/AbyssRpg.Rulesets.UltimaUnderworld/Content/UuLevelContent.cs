using System.Text.Json;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
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
    string? PlacementsPath,
    ActorPose Spawn,
    string ProvenanceSource,
    string ProvenanceOrigin,
    int LiveObjects,
    int MobileObjects);

/// <summary>
/// The level's placement set as the runtime admits it: every tile with its
/// floor height and chain head, every object slot with its chain and container
/// fields, and the world scale the importer used, so a tile maps to a position.
/// </summary>
public sealed record UuLevelPlacements(
    double UnitsPerTile,
    double HeightUnitsPerStep,
    AdmittedTile[] Tiles,
    AdmittedObject[] Objects)
{
    /// <summary>World center of a tile at its floor height.</summary>
    public WorldPoint TileCenter(int tileX, int tileY, int floorHeight) => new(
        (float)((tileX + 0.5d) * UnitsPerTile),
        (float)(floorHeight * HeightUnitsPerStep),
        (float)((tileY + 0.5d) * UnitsPerTile));

    /// <summary>The tile a world position stands on, or null outside the grid.</summary>
    public AdmittedTile? TileAt(AbyssRpg.Kit.Controls.WorldPoint position) => Tile(
        (int)Math.Floor(position.X / UnitsPerTile),
        (int)Math.Floor(position.Z / UnitsPerTile));

    public AdmittedTile? Tile(int tileX, int tileY) =>
        tileX is >= 0 and < TileDimension && tileY is >= 0 and < TileDimension
            ? Tiles[(tileY * TileDimension) + tileX]
            : null;

    /// <summary>The placement record for an object slot, or null when it has none.</summary>
    public AdmittedObject? Object(int objectIndex)
    {
        _objectsByIndex ??= Objects.ToDictionary(obj => obj.Index);
        return _objectsByIndex.TryGetValue(objectIndex, out AdmittedObject? found) ? found : null;
    }

    private Dictionary<int, AdmittedObject>? _objectsByIndex;

    public const int TileDimension = 64;
}

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
        // Placements are optional so a level imported before them still loads;
        // such a level admits no placed object.
        string? placementsPath = root.TryGetProperty("placements", out JsonElement placements)
            && placements.ValueKind == JsonValueKind.Object
            && placements.TryGetProperty("path", out JsonElement placementsValue)
            && placementsValue.ValueKind == JsonValueKind.String
            ? placementsValue.GetString()
            : null;
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
            placementsPath,
            pose,
            source,
            String(provenance, "origin"),
            (int)OptionalNumber(provenance, "liveObjects"),
            (int)OptionalNumber(provenance, "mobileObjects"));
    }

    /// <summary>Reads the placement artifact the manifest names.</summary>
    public static UuLevelPlacements ReadPlacements(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        using JsonDocument document = Parse(payload, payloadLabel);
        JsonElement root = document.RootElement;
        if (Number(root, "schemaVersion") != SchemaVersion)
            throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion {SchemaVersion}.");

        double unitsPerTile = Number(root, "unitsPerTile");
        double heightUnitsPerStep = Number(root, "heightUnitsPerStep");
        if (unitsPerTile <= 0d) throw new InvalidOperationException($"'{payloadLabel}' declares a non-positive tile scale.");
        // A degenerate step scale would put every floor at the same height and
        // let a placement land inside geometry.
        if (heightUnitsPerStep <= 0d)
        {
            throw new InvalidOperationException($"'{payloadLabel}' declares a non-positive height step scale.");
        }

        JsonElement tilesElement = Required(root, "tiles");
        // The artifact is a full row-major grid because the runtime indexes it
        // by tile; a short one would silently mis-place every object.
        if (tilesElement.GetArrayLength() != UuLevelPlacements.TileDimension * UuLevelPlacements.TileDimension)
        {
            throw new InvalidOperationException(
                $"'{payloadLabel}' must carry a full {UuLevelPlacements.TileDimension}x{UuLevelPlacements.TileDimension} tile grid.");
        }

        var tiles = new AdmittedTile[tilesElement.GetArrayLength()];
        int at = 0;
        foreach (JsonElement row in tilesElement.EnumerateArray())
        {
            if (row.GetArrayLength() != 6)
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' tile rows must be [x, y, type, head, floorHeight, door].");
            }

            // The runtime indexes tiles by position, so a row that names a
            // different tile than its place in the grid would silently move
            // every placement on the level.
            int x = (int)row[0].GetDouble();
            int y = (int)row[1].GetDouble();
            if (x != at % UuLevelPlacements.TileDimension || y != at / UuLevelPlacements.TileDimension)
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' tile row {at} names tile ({x},{y}); the grid is row-major.");
            }

            tiles[at++] = new AdmittedTile(
                x, y, (int)row[2].GetDouble(),
                (int)row[3].GetDouble(), (int)row[4].GetDouble(), row[5].GetDouble() != 0d);
        }

        JsonElement objectsElement = Required(root, "objects");
        var objects = new AdmittedObject[objectsElement.GetArrayLength()];
        at = 0;
        foreach (JsonElement row in objectsElement.EnumerateArray())
        {
            // A row written before the whoami byte existed carries eleven
            // columns; the creature simply holds no conversation of its own.
            int columns = row.GetArrayLength();
            if (columns is not (11 or 12))
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' object rows must be "
                    + "[index, mobile, itemId, flags, quality, next, owner, link, homeX, homeY, heading, whoami].");
            }

            objects[at++] = new AdmittedObject(
                (int)row[0].GetDouble(),
                (int)row[2].GetDouble(),
                (int)row[5].GetDouble(),
                Owner: (int)row[6].GetDouble(),
                Link: (int)row[7].GetDouble(),
                Quality: (int)row[4].GetDouble(),
                Mobile: row[1].GetDouble() != 0d,
                HomeTileX: (int)row[8].GetDouble(),
                HomeTileY: (int)row[9].GetDouble(),
                Heading: (int)row[10].GetDouble(),
                WhoAmI: columns == 12 ? (int)row[11].GetDouble() : 0);
        }

        return new UuLevelPlacements(unitsPerTile, heightUnitsPerStep, tiles, objects);
    }

    private static double OptionalNumber(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number)
            ? number
            : 0d;

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
