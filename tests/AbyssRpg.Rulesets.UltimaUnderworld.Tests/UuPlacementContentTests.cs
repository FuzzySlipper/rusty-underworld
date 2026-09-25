using System.Text;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Critters;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// The placement artifact and the object tables it is interpreted with: item id
/// classes decide what a placed slot is, and a tile maps to a world position.
/// </summary>
public sealed class UuPlacementContentTests
{
    private const string Tables = """
    {
      "schemaVersion": 1,
      "source": { "SourceGame": "UW1", "SourceFile": "UW/DATA/OBJECTS.DAT", "ByteLength": 10, "Sha256Hex": "00" },
      "critters": [
        { "itemId": 64, "level": 1, "avgHp": 12, "strength": 14, "dexterity": 12, "intelligence": 6, "speed": 3, "corpseIndex": 2, "swimmer": false, "flier": false, "faction": 3 },
        { "itemId": 127, "level": 5, "avgHp": 30, "strength": 20, "dexterity": 8, "intelligence": 4, "speed": 6, "corpseIndex": 5, "swimmer": true, "flier": false, "faction": 1 }
      ],
      "containers": [
        { "itemId": 128, "capacityTenthStones": 125, "objectsMask": 255, "slots": 255 },
        { "itemId": 143, "capacityTenthStones": 40, "objectsMask": 7, "slots": 3 }
      ]
    }
    """;

    private static string Placements(
        string objectRows, int headAtOrigin = 500, int headAtOneZero = 0, int floorAtOneZero = 2)
    {
        var tiles = new StringBuilder();
        for (int y = 0; y < UuLevelPlacements.TileDimension; y++)
        {
            for (int x = 0; x < UuLevelPlacements.TileDimension; x++)
            {
                if (tiles.Length > 0) tiles.Append(',');
                (int head, int floor) = (x, y) switch
                {
                    (0, 0) => (headAtOrigin, 0),
                    (1, 0) => (headAtOneZero, floorAtOneZero),
                    _ => (0, 0),
                };
                int door = (x, y) == (2, 2) ? 1 : 0;
                tiles.Append($"[{x},{y},0,{head},{floor},{door}]");
            }
        }

        return $$"""
        {
          "schemaVersion": 1,
          "level": 1,
          "unitsPerTile": 8.0,
          "heightUnitsPerStep": 1.0,
          "liveObjects": 2,
          "mobileObjects": 1,
          "tiles": [{{tiles}}],
          "objects": [{{objectRows}}]
        }
        """;
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(127, true)]
    [InlineData(63, false)]
    [InlineData(128, false)]
    public void Critter_item_ids_are_majorclass_one(int itemId, bool expected) =>
        Assert.Equal(expected, UuObjectTablesContent.IsCritterItem(itemId));

    [Theory]
    [InlineData(128, true)]
    [InlineData(143, true)]
    [InlineData(144, false)]
    [InlineData(176, false)]
    [InlineData(127, false)]
    public void Container_item_ids_are_majorclass_two_minorclass_zero(int itemId, bool expected) =>
        Assert.Equal(expected, UuObjectTablesContent.IsContainerItem(itemId));

    [Fact]
    public void Object_tables_read_critters_and_containers_by_item_id()
    {
        UuObjectTables tables = UuObjectTablesContent.Read(Encoding.UTF8.GetBytes(Tables), "object tables");
        Assert.Equal(2, tables.Critters.Count);
        Assert.Equal(12, tables.Critters[64].AvgHp);
        Assert.True(tables.Critters[127].CorpseIndex == 5);
        Assert.Equal(125, tables.Containers[128].CapacityTenthStones);
        Assert.Equal(3, tables.Containers[143].Slots);
        Assert.Equal(0xF, tables.Containers[143].ClassIndex);
    }

    [Fact]
    public void A_table_from_the_other_game_is_refused()
    {
        string foreign = Tables.Replace("\"UW1\"", "\"UW2\"", StringComparison.Ordinal);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuObjectTablesContent.Read(Encoding.UTF8.GetBytes(foreign), "object tables"));
        Assert.Contains("UW2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Placements_read_tiles_objects_and_the_world_scale()
    {
        UuLevelPlacements placements = UuLevelContent.ReadPlacements(
            Encoding.UTF8.GetBytes(Placements("[500,0,200,0,0,0,0,0,-1,-1,12]")), "placements");

        Assert.Equal(8.0, placements.UnitsPerTile);
        Assert.Equal(UuLevelPlacements.TileDimension * UuLevelPlacements.TileDimension, placements.Tiles.Length);
        Assert.Equal(500, placements.Tile(0, 0)!.ObjectHead);
        Assert.Equal(2, placements.Tile(1, 0)!.FloorHeight);
        Assert.Null(placements.Tile(64, 0));
        Assert.Null(placements.Tile(-1, 0));
        Assert.Equal(new AbyssRpg.Kit.Controls.WorldPoint(4f, 0f, 4f), placements.TileCenter(0, 0, 0));
        Assert.True(placements.Tile(2, 2)!.Door);
        Assert.False(placements.Tile(0, 0)!.Door);
        Assert.Equal((2, 2), (placements.TileAt(new AbyssRpg.Kit.Controls.WorldPoint(20f, 0f, 20f))!.X,
            placements.TileAt(new AbyssRpg.Kit.Controls.WorldPoint(20f, 0f, 20f))!.Y));
        Assert.Equal(new AbyssRpg.Kit.Controls.WorldPoint(12f, 2f, 4f), placements.TileCenter(1, 0, 2));
    }

    [Fact]
    public void A_truncated_tile_grid_is_refused()
    {
        // One row short of a full grid: the runtime indexes tiles by position,
        // so a short artifact would mis-place every object.
        string truncated = Placements("[500,0,200,0,0,0,0,0,-1,-1,12]")
            .Replace(",[63,63,0,0,0,0]", "", StringComparison.Ordinal);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuLevelContent.ReadPlacements(Encoding.UTF8.GetBytes(truncated), "placements"));
        Assert.Contains("tile grid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_object_row_of_the_wrong_width_is_refused()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuLevelContent.ReadPlacements(
                Encoding.UTF8.GetBytes(Placements("[500,0,200]")), "placements"));
        Assert.Contains("object rows", error.Message, StringComparison.Ordinal);
    }
}
