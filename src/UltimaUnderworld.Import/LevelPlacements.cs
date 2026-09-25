using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Emits one level's placement set: every tile's chain head and every placed
/// object with the chain fields the runtime needs. Object *meaning* (what an
/// item id is, whether a mobile is a critter) is ruleset policy and lives in
/// the object-tables pack, not here.
/// </summary>
public static class LevelPlacements
{
    public const int SchemaVersion = 1;

    public sealed record PlacedTile(int X, int Y, int Type, int ObjectHead, int FloorHeight, bool Door);

    /// <summary>
    /// One object slot. A mobile carries its own tile in the record's home
    /// fields (HomeTileX/Y), because mobile objects are positioned there rather
    /// than by a tile chain; -1 means the record holds no home tile.
    /// </summary>
    public sealed record PlacedObject(
        int Index, bool Mobile, int ItemId, int Flags, int Quality, int Next, int Owner, int Link,
        int HomeTileX, int HomeTileY);

    public sealed record Placements(
        int Level,
        double UnitsPerTile,
        double HeightUnitsPerStep,
        IReadOnlyList<PlacedTile> Tiles,
        IReadOnlyList<PlacedObject> Objects,
        int LiveObjects,
        int MobileObjects);

    /// <summary>Byte offset of the mobile record's home-tile word (donor: uwobject npc_xhome/npc_yhome).</summary>
    private const int MobileHomeOffset = 0x16;

    public static Placements Emit(LevArkReader.LevelPack pack, LevelRenderMesh.Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        LevelRenderMesh.Options mesh = options ?? new LevelRenderMesh.Options();
        PlacedTile[] tiles = pack.Tiles
            .Select(tile => new PlacedTile(tile.X, tile.Y, tile.Type, tile.ObjectHead, tile.FloorHeight, tile.Door))
            .ToArray();
        PlacedObject[] objects = pack.Objects
            .Select(obj =>
            {
                (int homeX, int homeY) = HomeTile(obj);
                return new PlacedObject(
                    obj.Index, obj.IsMobile, obj.ItemId, obj.Flags, obj.Quality, obj.Next, obj.Owner, obj.Link,
                    homeX, homeY);
            })
            .ToArray();
        return new Placements(
            pack.LevelNumber,
            mesh.UnitsPerTile,
            mesh.HeightUnitsPerStep,
            tiles,
            objects,
            objects.Count(obj => obj.ItemId != 0),
            objects.Count(obj => obj.ItemId != 0 && obj.Mobile));
    }

    /// <summary>
    /// The tile a mobile object stands on, read from the little-endian word at
    /// offset 0x16 of its record: y is bits 4-9 and x is bits 10-15, matching
    /// the donor's <c>uwobject.npc_yhome</c>/<c>npc_xhome</c> accessors.
    /// </summary>
    private static (int X, int Y) HomeTile(LevArkReader.LevelObject obj)
    {
        if (!obj.IsMobile || obj.Raw.Length < MobileHomeOffset + 2) return (-1, -1);
        int word = obj.Raw[MobileHomeOffset] | (obj.Raw[MobileHomeOffset + 1] << 8);
        return ((word >> 10) & 0x3F, (word >> 4) & 0x3F);
    }

    public static string ToJson(Placements placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        var document = new
        {
            schemaVersion = SchemaVersion,
            level = placements.Level,
            liveObjects = placements.LiveObjects,
            mobileObjects = placements.MobileObjects,
            unitsPerTile = placements.UnitsPerTile,
            heightUnitsPerStep = placements.HeightUnitsPerStep,
            // One row per tile in row-major order: x, y, type, the head of the
            // tile's object chain (0 for none), the tile's floor height, and
            // whether the tile is a door.
            tiles = placements.Tiles.Select(tile => new[]
            {
                tile.X, tile.Y, tile.Type, tile.ObjectHead, tile.FloorHeight, tile.Door ? 1 : 0,
            }),
            // One row per object slot: index, mobile, item id, flags, quality,
            // next in the chain, owner (container), link (first content), and
            // the mobile's own tile (-1 when the record holds none).
            objects = placements.Objects.Select(obj => new[]
            {
                obj.Index, obj.Mobile ? 1 : 0, obj.ItemId, obj.Flags, obj.Quality, obj.Next, obj.Owner, obj.Link,
                obj.HomeTileX, obj.HomeTileY,
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }
}
