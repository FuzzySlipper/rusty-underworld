using AbyssRpg.TestSupport;
using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

// Golden values below were observed from the shipped UW1 STRINGS.PAK
// (operator ISO, never committed) and cross-checked against the donor's
// named string constants (GameStrings.str_you_see_ = 260 etc.).
public sealed class StringsPakTests
{


    [Fact]
    public void Rejects_empty_and_truncated_input()
    {
        Assert.Throws<InvalidDataException>(() => StringsPakReader.Decode(ReadOnlySpan<byte>.Empty));
        Assert.Throws<InvalidDataException>(() => StringsPakReader.Decode(new byte[] { 1, 0, (byte)'A', 0, 0, 0 }));
    }

    [PinnedReleaseFact("UW/DATA/STRINGS.PAK")]
    public void Decodes_shipped_strings_with_donor_named_goldens()
    {
        byte[] raw = OperatorData.Read("UW/DATA/STRINGS.PAK");
        var decoded = StringsPakReader.Decode(raw);

        Assert.Equal(122, decoded.Blocks.Count);
        Assert.Equal(512, decoded.Blocks[1].Count);
        Assert.Equal("You see ", decoded.GetString(1, 260));
        Assert.Equal("It looks to be that of ", decoded.GetString(1, 22));
        Assert.Equal("You have advanced in ", decoded.GetString(1, 29));
        Assert.Equal("a_hand axe", decoded.GetString(4, 0));
        Assert.Equal("a_battle axe", decoded.GetString(4, 1));
    }

    [OperatorDataFact("UW/DATA/STRINGS.PAK")]
    public void GetString_rejects_unknown_block_and_index()
    {
        byte[] raw = OperatorData.Read("UW/DATA/STRINGS.PAK");
        var decoded = StringsPakReader.Decode(raw);

        Assert.Throws<ArgumentOutOfRangeException>(() => decoded.GetString(9999, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => decoded.GetString(1, 1_000_000));
    }
}
