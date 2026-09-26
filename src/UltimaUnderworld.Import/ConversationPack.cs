using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Emits the runtime-facing conversation content: one normalized script per
/// conversation number from the operator's own CNV.ARK, and the string blocks
/// those scripts read. The runtime runs the scripts on its own conversation VM;
/// nothing here interprets them.
/// </summary>
public static class ConversationPack
{
    public const int SchemaVersion = 1;
    public const string PackId = "abyssrpg.conversations";
    public const string StringsPackId = "abyssrpg.strings";

    public sealed record Conversation(
        int Index,
        int CodeSize,
        int StringBlock,
        int MemorySlots,
        IReadOnlyList<CnvArkReader.ConversationImport> Imports,
        short[] Code);

    public sealed record Catalog(
        IReadOnlyList<Conversation> Conversations,
        UwTableProvenance Provenance);

    /// <summary>
    /// The string blocks a set of conversations can read, taken whole: a script
    /// indexes its own block by number at run time, so the pack carries blocks
    /// rather than pre-resolved lines.
    /// </summary>
    public sealed record Strings(
        IReadOnlyDictionary<int, IReadOnlyList<string>> Blocks,
        UwTableProvenance Provenance);

    public static Catalog Emit(CnvArkReader.DialoguePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Conversation[] conversations = pack.Conversations
            .Select(header => new Conversation(
                header.Index,
                header.CodeSize,
                header.StringBlock,
                header.MemorySlots,
                header.Imports,
                header.Code))
            .ToArray();
        return new Catalog(conversations, pack.Provenance);
    }

    /// <summary>
    /// The blocks the conversations reference, plus the two the game's own name
    /// and message text live in: block 7 holds creature names and conversation
    /// prompts, block 1 the stock messages (donor: src/utility/StringLoader.cs
    /// and src/conversation/conversationinitialisation.cs).
    /// </summary>
    public static Strings EmitStrings(
        Catalog catalog,
        StringsPakReader.DecodedStrings decoded,
        UwTableProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(provenance);

        var wanted = new SortedSet<int> { NameBlock, MessageBlock };
        foreach (Conversation conversation in catalog.Conversations)
        {
            if (conversation.StringBlock > 0) wanted.Add(conversation.StringBlock);
        }

        var blocks = new SortedDictionary<int, IReadOnlyList<string>>();
        foreach (int block in wanted)
        {
            if (decoded.Blocks.TryGetValue(block, out IReadOnlyList<string>? entries)) blocks[block] = entries;
        }

        return new Strings(blocks, provenance);
    }

    /// <summary>The string block nameless creatures are named from, and the stock messages.</summary>
    public const int NameBlock = 7;
    public const int MessageBlock = 1;

    public static string ToJson(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var document = new
        {
            schemaVersion = SchemaVersion,
            source = catalog.Provenance,
            conversations = catalog.Conversations.Select(conversation => new
            {
                index = conversation.Index,
                codeSize = conversation.CodeSize,
                stringBlock = conversation.StringBlock,
                memorySlots = conversation.MemorySlots,
                imports = conversation.Imports.Select(import => new
                {
                    name = import.Name,
                    idOrAddress = import.IdOrAddress,
                    isVariable = import.IsVariable,
                    returnType = import.ReturnType,
                }),
                code = conversation.Code,
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }

    public static string ToJson(Strings strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        var document = new
        {
            schemaVersion = SchemaVersion,
            source = strings.Provenance,
            blocks = strings.Blocks.ToDictionary(
                entry => entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry => entry.Value),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }
}
