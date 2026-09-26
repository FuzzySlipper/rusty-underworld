using System.Globalization;
using System.Text.Json;
using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The conversations the import produced: one script per conversation number.
/// A creature's record names which it holds; the runtime runs the script, so this
/// type only reads and keys them.
/// </summary>
public sealed class UuConversationCatalog
{
    public const string PackId = "abyssrpg.conversations";

    private readonly Dictionary<int, UuConversationVm.ConversationScript> _scripts;

    private UuConversationCatalog(Dictionary<int, UuConversationVm.ConversationScript> scripts) =>
        _scripts = scripts;

    public int Count => _scripts.Count;

    /// <summary>The script a conversation number holds, or null when the pack has none.</summary>
    public UuConversationVm.ConversationScript? Script(int conversation) =>
        _scripts.TryGetValue(conversation, out UuConversationVm.ConversationScript? script) ? script : null;

    /// <summary>Every conversation number the pack carries.</summary>
    public IReadOnlyCollection<int> Numbers => _scripts.Keys;

    public static UuConversationCatalog Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{payloadLabel}' is not valid JSON: {error.Message}", error);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (UuContentJson.Number(root, "schemaVersion") != 1)
                throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion 1.");

            var scripts = new Dictionary<int, UuConversationVm.ConversationScript>();
            foreach (JsonElement row in UuContentJson.Array(root, "conversations").EnumerateArray())
            {
                int index = (int)UuContentJson.Number(row, "index");
                short[] code = UuContentJson.Array(row, "code")
                    .EnumerateArray()
                    .Select(entry => (short)entry.GetInt32())
                    .ToArray();
                var imports = UuContentJson.Array(row, "imports")
                    .EnumerateArray()
                    .Select(entry => new UuConversationVm.ScriptImport(
                        UuContentJson.Text(entry, "name"),
                        (int)UuContentJson.Number(entry, "idOrAddress"),
                        UuContentJson.Boolean(entry, "isVariable"),
                        (int)UuContentJson.Number(entry, "returnType")))
                    .ToArray();
                if (!scripts.TryAdd(index, new UuConversationVm.ConversationScript(
                    index,
                    (int)UuContentJson.Number(row, "codeSize"),
                    (int)UuContentJson.Number(row, "stringBlock"),
                    (int)UuContentJson.Number(row, "memorySlots"),
                    imports,
                    code)))
                {
                    throw new InvalidOperationException($"'{payloadLabel}' defines conversation {index} twice.");
                }
            }

            if (scripts.Count == 0)
                throw new InvalidOperationException($"'{payloadLabel}' defines no conversations.");

            return new UuConversationCatalog(scripts);
        }
    }
}

/// <summary>
/// The string blocks the conversations read. A script indexes its own block by
/// number at run time, so the pack carries whole blocks and this type answers a
/// block and index. A missing block or index is empty text, which is what a
/// script sees when the game has nothing to say (donor:
/// src/utility/StringLoader.cs GetString).
/// </summary>
public sealed class UuStrings
{
    public const string PackId = "abyssrpg.strings";

    /// <summary>Block 7 holds creature names and the stock conversation lines.</summary>
    public const int ConversationBlock = 7;

    private readonly Dictionary<int, IReadOnlyList<string>> _blocks;

    private UuStrings(Dictionary<int, IReadOnlyList<string>> blocks) => _blocks = blocks;

    public int Count => _blocks.Count;

    /// <summary>The text at one block and index, or empty when the pack has none.</summary>
    public string Text(int block, int index)
    {
        if (!_blocks.TryGetValue(block, out IReadOnlyList<string>? entries)) return "";
        return index >= 0 && index < entries.Count ? entries[index] : "";
    }

    /// <summary>The provider the conversation VM reads strings through.</summary>
    public Func<int, int, string> Provider => Text;

    /// <summary>A creature's own name, which lives in the conversation block at whoami+16.</summary>
    public string CreatureName(int whoami) => whoami <= 0 ? "" : Text(ConversationBlock, whoami + 16).Trim();

    public static UuStrings Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{payloadLabel}' is not valid JSON: {error.Message}", error);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (UuContentJson.Number(root, "schemaVersion") != 1)
                throw new InvalidOperationException($"'{payloadLabel}' must declare schemaVersion 1.");

            JsonElement blocks = UuContentJson.Required(root, "blocks");
            if (blocks.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("'blocks' must be an object keyed by block number.");

            var parsed = new Dictionary<int, IReadOnlyList<string>>();
            foreach (JsonProperty block in blocks.EnumerateObject())
            {
                if (!int.TryParse(block.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    throw new InvalidOperationException($"'{payloadLabel}' names block '{block.Name}', which is not a number.");
                parsed[number] = block.Value.EnumerateArray().Select(entry => entry.GetString() ?? "").ToArray();
            }

            if (parsed.Count == 0)
                throw new InvalidOperationException($"'{payloadLabel}' defines no string blocks.");

            return new UuStrings(parsed);
        }
    }
}
