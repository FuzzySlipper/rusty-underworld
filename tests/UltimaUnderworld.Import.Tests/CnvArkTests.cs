using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

public sealed class CnvArkTests
{
    private readonly ITestOutputHelper _output;

    public CnvArkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Shipped_cnv_headers_and_imports_decode()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/CNV.ARK");
        if (archive is null) return;

        CnvArkReader.DialoguePack pack = CnvArkReader.ReadPack(archive, "UW/DATA/CNV.ARK");

        Assert.NotEmpty(pack.Conversations);
        Assert.Equal("UW1", pack.Provenance.SourceGame);
        CnvArkReader.ConversationHeader first = pack.Conversations[0];
        Assert.Equal(1, first.Index); // slot 0 is empty
        Assert.Equal(1774, first.CodeSize);
        Assert.Equal(3585, first.StringBlock);
        Assert.Equal(34, first.MemorySlots);
        Assert.Equal(82, first.Imports.Count);
        CnvArkReader.ConversationImport import = first.Imports[0];
        Assert.Equal("find_barter_total", import.Name);
        Assert.Equal(0x32, import.IdOrAddress);
        Assert.False(import.IsVariable);
        Assert.Equal(0x129, import.ReturnType);
        Assert.Equal(first.CodeSize, first.Code.Length);
        _output.WriteLine($"CNV: {pack.Conversations.Count} conversations");
    }

    [Fact]
    public void Bablob_slots_match_conversation_memory()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/CNV.ARK");
        byte[]? bablobs = RequireDataOrSkip("UW/DATA/BABGLOBS.DAT");
        if (archive is null || bablobs is null) return;

        CnvArkReader.DialoguePack pack = CnvArkReader.ReadPack(archive, "UW/DATA/CNV.ARK");
        IReadOnlyList<CnvArkReader.BablobEntry> entries = CnvArkReader.ReadBablobs(bablobs);

        Assert.True(entries.Count > 10);
        foreach (CnvArkReader.BablobEntry entry in entries.Take(10))
        {
            CnvArkReader.ConversationHeader convo = pack.Conversations.First(c => c.Index == entry.ConversationNo);
            Assert.Equal(convo.MemorySlots, entry.Size);
        }
    }

    [Fact]
    public void Rejects_truncated_archives()
    {
        Assert.Throws<ArgumentException>(() => CnvArkReader.ReadPack([], "CNV.ARK"));
        Assert.Throws<ArgumentException>(() => CnvArkReader.ReadPack([0x02, 0x00, 0xFF], "CNV.ARK"));
    }

    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
