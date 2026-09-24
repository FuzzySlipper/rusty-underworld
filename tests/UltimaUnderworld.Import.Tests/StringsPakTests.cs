using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

// Golden values below were observed from the shipped UW1 STRINGS.PAK
// (operator ISO, never committed) and cross-checked against the donor's
// named string constants (GameStrings.str_you_see_ = 260 etc.).
public sealed class StringsPakTests
{
    private readonly ITestOutputHelper _output;

    public StringsPakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Rejects_empty_and_truncated_input()
    {
        Assert.Throws<InvalidDataException>(() => StringsPakReader.Decode(ReadOnlySpan<byte>.Empty));
        Assert.Throws<InvalidDataException>(() => StringsPakReader.Decode(new byte[] { 1, 0, (byte)'A', 0, 0, 0 }));
    }

    [Fact]
    public void Decodes_shipped_strings_with_donor_named_goldens()
    {
        if (RequireDataOrSkip("UW/DATA/STRINGS.PAK") is not byte[] raw) return;
        var decoded = StringsPakReader.Decode(raw);

        Assert.Equal(122, decoded.Blocks.Count);
        Assert.Equal(512, decoded.Blocks[1].Count);
        Assert.Equal("You see ", decoded.GetString(1, 260));
        Assert.Equal("It looks to be that of ", decoded.GetString(1, 22));
        Assert.Equal("You have advanced in ", decoded.GetString(1, 29));
        Assert.Equal("a_hand axe", decoded.GetString(4, 0));
        Assert.Equal("a_battle axe", decoded.GetString(4, 1));
    }

    [Fact]
    public void GetString_rejects_unknown_block_and_index()
    {
        if (RequireDataOrSkip("UW/DATA/STRINGS.PAK") is not byte[] raw) return;
        var decoded = StringsPakReader.Decode(raw);

        Assert.Throws<ArgumentOutOfRangeException>(() => decoded.GetString(9999, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => decoded.GetString(1, 1_000_000));
    }

    // Returns null after writing a SKIP notice when operator data is absent;
    // callers return early so the log, not silence, records what was skipped.
    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
