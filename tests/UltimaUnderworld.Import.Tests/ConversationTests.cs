using System.Text.Json;
using Xunit;

namespace UltimaUnderworld.Import.Tests;

/// <summary>
/// The conversation content the import produces: the operator's own CNV.ARK read
/// into runtime-facing scripts, and the string blocks those scripts index.
/// </summary>
public sealed class ConversationTests
{
    [Fact]
    public void The_operators_conversations_and_their_strings_are_emitted()
    {
        byte[] cnv = File.ReadAllBytes(TestData.Find("UW/DATA/CNV.ARK"));
        byte[] strings = File.ReadAllBytes(TestData.Find("UW/DATA/STRINGS.PAK"));
        ConversationPack.Catalog catalog = ConversationPack.Emit(CnvArkReader.ReadPack(cnv, "UW/DATA/CNV.ARK"));
        ConversationPack.Strings blocks = ConversationPack.EmitStrings(
            catalog,
            StringsPakReader.Decode(strings, "UW/DATA/STRINGS.PAK"),
            UwTableProvenance.FromBytes("UW1", "UW/DATA/STRINGS.PAK", strings));

        Assert.True(catalog.Conversations.Count > 50, "the game's own conversations are all carried");
        Assert.All(catalog.Conversations, conversation => Assert.True(conversation.Code.Length > 0));

        // The scripts the game trades through name the trade imports, which is
        // what makes barter reachable through talk rather than a second path.
        Assert.Contains(
            catalog.Conversations.SelectMany(conversation => conversation.Imports),
            import => import.Name == "do_offer");

        // A script reads its own string block, so the pack carries whole blocks:
        // the ones the scripts name, plus the name and message blocks.
        Assert.Contains(ConversationPack.NameBlock, blocks.Blocks.Keys);
        Assert.Contains(ConversationPack.MessageBlock, blocks.Blocks.Keys);
        foreach (ConversationPack.Conversation conversation in catalog.Conversations.Where(c => c.StringBlock > 0))
            Assert.Contains(conversation.StringBlock, blocks.Blocks.Keys);
    }

    [Fact]
    public void A_conversation_pack_carries_its_scripts_and_provenance()
    {
        byte[] cnv = File.ReadAllBytes(TestData.Find("UW/DATA/CNV.ARK"));
        ConversationPack.Catalog catalog = ConversationPack.Emit(CnvArkReader.ReadPack(cnv, "UW/DATA/CNV.ARK"));

        using JsonDocument document = JsonDocument.Parse(ConversationPack.ToJson(catalog));
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("UW1", root.GetProperty("source").GetProperty("SourceGame").GetString());
        JsonElement first = root.GetProperty("conversations")[0];
        // Every member the runtime reader requires is present.
        foreach (string member in new[] { "index", "codeSize", "stringBlock", "memorySlots", "imports", "code" })
            Assert.True(first.TryGetProperty(member, out _), $"missing {member}");
    }
}
