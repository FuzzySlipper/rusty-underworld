using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

public sealed class AllLevelsTests
{
    private readonly ITestOutputHelper _output;

    public AllLevelsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void All_nine_levels_decode_validate_and_admit()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        for (int level = 1; level <= 9; level++)
        {
            LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, level, terrain);
            Assert.Equal(4096, pack.Tiles.Count);
            Assert.NotEmpty(pack.Objects);
            IReadOnlyList<LevArkReader.PlacementIssue> issues =
                LevArkReader.ValidatePlacement(pack, terrain);
            Assert.DoesNotContain(issues, i => i.Kind is "chain-index-out-of-range" or "chain-cycle");
            bool lavaLevel = level >= 5;
            Assert.Equal(
                lavaLevel,
                issues.Any(i => i.Kind == "spawn-on-lava"));
            _output.WriteLine($"L{level}: {pack.Tiles.Count(t => t.Type == LevArkReader.TileSolid)} solid, {pack.Objects.Count} objects");
        }
    }

    [Fact]
    public void String_blocks_decode_with_provenance()
    {
        byte[]? strings = RequireDataOrSkip("UW/DATA/STRINGS.PAK");
        if (strings is null) return;

        StringsPakReader.DecodedStrings decoded = StringsPakReader.Decode(strings);
        Assert.True(decoded.Blocks.Count >= 7);
        Assert.True(decoded.Blocks.Values.Sum(block => block.Count) > 1000);
    }

    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
