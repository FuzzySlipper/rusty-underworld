using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

// Golden values observed from the shipped UW1 LEV.ARK + TERRAIN.DAT
// (operator ISO, never committed) and cross-checked against donor block
// addressing (tile blocks 0-8, texture 18-26, automap absent→blank).
public sealed class LevelTests
{
    private readonly ITestOutputHelper _output;

    public LevelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Rejects_bad_level_and_truncated_archive()
    {
        byte[] archive = new byte[64];
        Assert.Throws<ArgumentOutOfRangeException>(() => LevArkReader.ReadLevel(archive, 0, new byte[1024]));
        Assert.Throws<InvalidDataException>(() => LevArkReader.ReadLevel(archive, 1, new byte[1024]));
        Assert.Throws<ArgumentOutOfRangeException>(() => LevArkReader.ReadLevel(new byte[300_000], 10, new byte[1024]));
    }

    [Fact]
    public void Reads_level_1_golden_pack()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        var pack = LevArkReader.ReadLevel(archive, 1, terrain);
        Assert.Equal(4096, pack.Tiles.Count);
        Assert.Equal(1595, pack.Tiles.Count(t => t.Type == LevArkReader.TileOpen));
        Assert.Equal(2060, pack.Tiles.Count(t => t.Type == LevArkReader.TileSolid));
        Assert.Equal(1024, pack.Objects.Count);
        Assert.Equal(249, pack.Objects.Count(o => o.IsMobile && o.ItemId != 0));
        Assert.Equal(64, pack.Textures.Entries.Count);
        Assert.Equal(57, pack.Textures.CeilingEntry);
        Assert.Equal(4096, pack.Automap.Length);
        Assert.All(pack.Automap, b => Assert.Equal(0, b)); // shipped automap blocks are absent → blank
        Assert.Equal("UW1", pack.Provenance.SourceGame);
    }

    [Fact]
    public void Shipped_lava_levels_report_spawns_and_upper_levels_are_clean()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        // Donor-model census of lava-floor tiles per level (L1-L9):
        // 0,0,0,0,168,736,315,1529,49. Levels 1-3 have no lava floors at all;
        // levels 5+ do, so the validator must fire there and stay quiet above.
        for (int level = 1; level <= 3; level++)
        {
            var pack = LevArkReader.ReadLevel(archive, level, terrain);
            Assert.Empty(LevArkReader.ValidatePlacement(pack, terrain));
        }

        for (int level = 5; level <= 9; level++)
        {
            var pack = LevArkReader.ReadLevel(archive, level, terrain);
            var issues = LevArkReader.ValidatePlacement(pack, terrain);
            Assert.Contains(issues, i => i.Kind == "spawn-on-lava");
        }
    }

    [Fact]
    public void Detector_fires_on_a_synthetic_lava_spawn()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        var pack = LevArkReader.ReadLevel(archive, 1, terrain);
        LevArkReader.Tile anchor = pack.Tiles.First(t => t.ObjectHead == 0 && t.Type == LevArkReader.TileOpen);
        int actual = pack.Textures.Entries[anchor.FloorTexture + 48];
        byte[] lavaTerrain = (byte[])terrain.Clone();
        int wordAt = (TerrainDatReader.Uw1TerrainBase + actual) * 2;
        lavaTerrain[wordAt] = 0x20;
        lavaTerrain[wordAt + 1] = 0x00;

        // Plant object 1 on the anchor tile through a synthetic head.
        var tiles = pack.Tiles.ToArray();
        tiles[anchor.Y * 64 + anchor.X] = anchor with { ObjectHead = 1 };
        var rigged = pack with { Tiles = tiles };

        var issues = LevArkReader.ValidatePlacement(rigged, lavaTerrain);
        Assert.Contains(issues, i => i.Kind == "spawn-on-lava" && i.ObjectIndex == 1);
    }

    [Fact]
    public void Reads_palette_and_light_tables()
    {
        byte[]? pals = RequireDataOrSkip("UW/DATA/PALS.DAT");
        byte[]? light = RequireDataOrSkip("UW/DATA/LIGHT.DAT");
        byte[]? shades = RequireDataOrSkip("UW/DATA/SHADES.DAT");
        if (pals is null || light is null || shades is null) return;

        var tables = PaletteTableReader.Read(pals, light, shades);
        Assert.Equal(8, tables.Palettes.Count);
        Assert.Equal(768, tables.Palettes[0].Length);
        Assert.Equal(new byte[] { 0, 0, 1 }, tables.Palettes[0][..3]);
        Assert.Equal(16, tables.Light.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, tables.Light[0][..4]);
        Assert.Equal(96, tables.Shades.Length);
        Assert.Throws<InvalidDataException>(() => PaletteTableReader.Read(new byte[10], light, shades));
    }

    // Returns null after writing a SKIP notice when operator data is absent.
    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
