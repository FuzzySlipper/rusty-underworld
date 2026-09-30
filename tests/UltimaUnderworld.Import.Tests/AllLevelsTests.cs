using AbyssRpg.TestSupport;
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

    [OperatorDataFact("UW/DATA/LEV.ARK", "UW/DATA/TERRAIN.DAT")]
    public void All_nine_levels_decode_validate_and_admit()
    {
        byte[] archive = OperatorData.Read("UW/DATA/LEV.ARK");
        byte[] terrain = OperatorData.Read("UW/DATA/TERRAIN.DAT");

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

    [OperatorDataFact("UW/DATA/STRINGS.PAK")]
    public void String_blocks_decode_with_provenance()
    {
        byte[] strings = OperatorData.Read("UW/DATA/STRINGS.PAK");

        StringsPakReader.DecodedStrings decoded = StringsPakReader.Decode(strings, "UW/DATA/STRINGS.PAK");
        Assert.Equal("UW1", decoded.Provenance.SourceGame);
        Assert.True(decoded.Blocks.Count >= 7);
        Assert.True(decoded.Blocks.Values.Sum(block => block.Count) > 1000);
    }
}
