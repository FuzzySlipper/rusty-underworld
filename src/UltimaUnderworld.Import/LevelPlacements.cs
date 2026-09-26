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
        int HomeTileX, int HomeTileY, int Heading, int WhoAmI);

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

    /// <summary>Byte offset of the mobile record's heading (donor: uwobject npc_heading).</summary>
    private const int MobileHeadingOffset = 0x18;

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
                    homeX, homeY, Heading(obj), WhoAmI(obj));
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
    /// The facing a record carries: a mobile keeps it in the low five bits at
    /// offset 0x18 (donor: <c>uwobject.npc_heading</c>), and a static object in
    /// bits 7-9 of the word at offset 2 (donor: <c>uwobject.heading</c>). Both
    /// are in 32 steps per full turn; -1 means the record holds none.
    /// </summary>
    /// <summary>
    /// A mobile record's whoami byte, which selects the conversation it holds
    /// (donor: <c>uwobject.npc_whoami</c>, byte 0x1A; 0 means the creature's own
    /// kind speaks, 255 means it answers nothing).
    /// </summary>
    private static int WhoAmI(LevArkReader.LevelObject obj) =>
        obj.IsMobile && obj.Raw.Length > MobileWhoAmIOffset ? obj.Raw[MobileWhoAmIOffset] : 0;

    private const int MobileWhoAmIOffset = 0x1A;

    private static int Heading(LevArkReader.LevelObject obj)
    {
        if (obj.IsMobile)
        {
            return obj.Raw.Length > MobileHeadingOffset
                ? obj.Raw[MobileHeadingOffset] & 0x1F
                : -1;
        }

        if (obj.Raw.Length < 4) return -1;
        int word = obj.Raw[2] | (obj.Raw[3] << 8);
        return (word >> 7) & 0x7;
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
            // next in the chain, owner (container), link (first content), the
            // mobile's own tile (-1 when the record holds none), the record's
            // facing in its own 32-step units, and the whoami byte that selects
            // the conversation a creature holds.
            objects = placements.Objects.Select(obj => new[]
            {
                obj.Index, obj.Mobile ? 1 : 0, obj.ItemId, obj.Flags, obj.Quality, obj.Next, obj.Owner, obj.Link,
                obj.HomeTileX, obj.HomeTileY, obj.Heading, obj.WhoAmI,
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }
}
