using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

/// <summary>
/// The placement artifact carries every tile and object slot the runtime walks,
/// and the object-table pack carries the item id classes the runtime interprets
/// them with. Both are operator-data gated: the shapes are asserted from a
/// synthetic pack, and the real import is asserted when local/ is present.
/// </summary>
public sealed class LevelPlacementsTests
{
    private static LevArkReader.LevelPack Pack()
    {
        // Two tiles and two objects: one static on a chain, one mobile with a
        // home tile in its record, which is where a mobile is positioned from.
        var tiles = new LevArkReader.Tile[LevArkReader.TileDimension * LevArkReader.TileDimension];
        for (int y = 0; y < LevArkReader.TileDimension; y++)
        {
            for (int x = 0; x < LevArkReader.TileDimension; x++)
                tiles[(y * LevArkReader.TileDimension) + x] = new LevArkReader.Tile(x, y, 1, 2, 0, 0, false, 0, (x, y) == (0, 0) ? 7 : 0);
        }

        byte[] mobileRaw = new byte[LevArkReader.MobileRecordSize];
        // Home tile (9, 5): x is bits 10-15 and y is bits 4-9 of the word at 0x16.
        int home = (9 << 10) | (5 << 4);
        mobileRaw[0x16] = (byte)(home & 0xFF);
        mobileRaw[0x17] = (byte)(home >> 8);

        var objects = new LevArkReader.LevelObject[LevArkReader.ObjectCount];
        objects[0] = new LevArkReader.LevelObject(0, true, 64, 0, 0, 0, 0, 0, mobileRaw);
        objects[1] = new LevArkReader.LevelObject(7, false, 200, 0, 3, 0, 0, 0, new byte[LevArkReader.StaticRecordSize]);
        for (int i = 2; i < objects.Length; i++)
        {
            objects[i] = new LevArkReader.LevelObject(
                i, i < LevArkReader.MobileCount, 0, 0, 0, 0, 0, 0,
                new byte[i < LevArkReader.MobileCount ? LevArkReader.MobileRecordSize : LevArkReader.StaticRecordSize]);
        }

        return new LevArkReader.LevelPack(
            1, tiles, objects,
            new LevArkReader.TextureMap(new int[64], 0), [],
            UwTableProvenance.FromBytes("UW1", "UW/DATA/LEV.ARK level 1", [1, 2, 3]));
    }

    [Fact]
    public void Placements_emit_every_tile_and_object_slot()
    {
        LevelPlacements.Placements placements = LevelPlacements.Emit(Pack());

        Assert.Equal(1, placements.Level);
        Assert.Equal(LevArkReader.TileDimension * LevArkReader.TileDimension, placements.Tiles.Count);
        Assert.Equal(LevArkReader.ObjectCount, placements.Objects.Count);
        Assert.Equal(2, placements.LiveObjects);
        Assert.Equal(1, placements.MobileObjects);
        Assert.Equal(2, placements.Tiles[0].FloorHeight);
        Assert.Equal(7, placements.Tiles[0].ObjectHead);
    }

    [Fact]
    public void A_mobile_object_carries_the_tile_its_record_names()
    {
        LevelPlacements.Placements placements = LevelPlacements.Emit(Pack());
        LevelPlacements.PlacedObject critter = placements.Objects[0];
        Assert.True(critter.Mobile);
        Assert.Equal(64, critter.ItemId);
        Assert.Equal(9, critter.HomeTileX);
        Assert.Equal(5, critter.HomeTileY);
        // A static slot has no home tile: it is placed by its tile chain.
        Assert.Equal(-1, placements.Objects[1].HomeTileX);
    }

    [Fact]
    public void The_emitted_json_keeps_the_row_shape_the_runtime_reads()
    {
        string json = LevelPlacements.ToJson(LevelPlacements.Emit(Pack()));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(8.0, root.GetProperty("unitsPerTile").GetDouble());
        Assert.Equal(1.0, root.GetProperty("heightUnitsPerStep").GetDouble());
        Assert.Equal(6, root.GetProperty("tiles")[0].GetArrayLength());
        Assert.Equal(0, root.GetProperty("tiles")[0][5].GetInt32());
        Assert.Equal(10, root.GetProperty("objects")[0].GetArrayLength());
        Assert.Equal(9, root.GetProperty("objects")[0][8].GetInt32());
    }

    [Fact]
    public void Object_tables_are_keyed_by_item_id_class()
    {
        byte[] data = File.ReadAllBytes(TestData.Find("UW/DATA/OBJECTS.DAT"));
        ObjectTablePack.Tables tables = ObjectTablePack.Emit(
            ObjectsDatReader.Read(data), UwTableProvenance.FromBytes("UW1", "UW/DATA/OBJECTS.DAT", data));

        Assert.Equal(64, tables.Critters.Count);
        Assert.Equal(64, tables.Critters[0].ItemId);
        Assert.Equal(127, tables.Critters[^1].ItemId);
        Assert.All(tables.Critters, row => Assert.InRange(row.ItemId, 64, 127));
        Assert.Equal(16, tables.Containers.Count);
        Assert.Equal(128, tables.Containers[0].ItemId);
        Assert.Equal(143, tables.Containers[^1].ItemId);

        string json = ObjectTablePack.ToJson(tables);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(64, document.RootElement.GetProperty("critters").GetArrayLength());
        Assert.Equal(16, document.RootElement.GetProperty("containers").GetArrayLength());
    }

    [Fact]
    public void The_imported_level_places_objects_and_critters()
    {
        LevArkReader.LevelPack pack = LevArkReader.ReadLevel(
            File.ReadAllBytes(TestData.Find("UW/DATA/LEV.ARK")),
            1,
            File.ReadAllBytes(TestData.Find("UW/DATA/TERRAIN.DAT")));
        LevelPlacements.Placements placements = LevelPlacements.Emit(pack);

        Assert.Equal(1012, placements.LiveObjects);
        Assert.Equal(249, placements.MobileObjects);

        // Every tile chain terminates inside the object array, and no object is
        // reached twice: the walk the runtime performs is well formed.
        var byIndex = placements.Objects.ToDictionary(obj => obj.Index);
        var reached = new HashSet<int>();
        foreach (LevelPlacements.PlacedTile tile in placements.Tiles)
        {
            int index = tile.ObjectHead;
            var visited = new HashSet<int>();
            while (index != 0)
            {
                Assert.True(byIndex.ContainsKey(index), $"chain link {index} is not an object slot");
                Assert.True(visited.Add(index), $"chain revisits {index}");
                Assert.True(reached.Add(index), $"object {index} is on two chains");
                index = byIndex[index].Next;
            }
        }

        // Level 1 places critters, and the ones that carry a home tile are the
        // ones the runtime can stand on a tile.
        var placedCritters = placements.Objects
            .Where(obj => obj.Mobile && obj.ItemId is >= 64 and <= 127 && obj.HomeTileX >= 0)
            .ToArray();
        Assert.NotEmpty(placedCritters);
        Assert.All(placedCritters, critter => Assert.InRange(critter.HomeTileY, 0, 63));
    }
}
