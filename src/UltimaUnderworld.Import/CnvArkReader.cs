namespace UltimaUnderworld.Import;

/// <summary>
/// CNV.ARK normalization (UW1 layout): conversation count, per-conversation
/// headers (code size, string block, memory slots, imported globals), the
/// imported function/variable table, and raw code words. BABGLOBS.DAT gives
/// the per-conversation global slot sizes (bglobal semantics: conversations
/// address shared short globals by slot).
/// Donor layout: cnvarkloader.cs LoadCnvArkUW1, bglobal.cs LoadGlobals.
/// UW2 uses a different header width and is rejected.
/// </summary>
public static class CnvArkReader
{
    public const int ImportTypeVariable = 0x010F;
    public const int ImportTypeFunction = 0x0111;

    public sealed record ConversationImport(string Name, int IdOrAddress, bool IsVariable, int ReturnType);

    public sealed record ConversationHeader(
        int Index,
        int CodeSize,
        int StringBlock,
        int MemorySlots,
        IReadOnlyList<ConversationImport> Imports,
        short[] Code);

    public sealed record DialoguePack(
        IReadOnlyList<ConversationHeader> Conversations,
        UwTableProvenance Provenance);

    public sealed record BablobEntry(int ConversationNo, int Size);

    public static DialoguePack ReadPack(byte[] archive, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        if (archive.Length < 2) throw new ArgumentException("Archive too small.", nameof(archive));

        int count = ReadU16(archive, 0);
        var conversations = new List<ConversationHeader>();
        for (int i = 0; i < count; i++)
        {
            int tableAt = 2 + i * 4;
            if (tableAt + 4 > archive.Length)
                throw new ArgumentException($"Offset table truncated at conversation {i}.", nameof(archive));
            int address = ReadU32(archive, tableAt);
            if (address == 0) continue;
            conversations.Add(ReadConversation(archive, i, address));
        }

        return new DialoguePack(
            conversations,
            UwTableProvenance.FromBytes("UW1", sourceFile, archive));
    }

    private static ConversationHeader ReadConversation(byte[] archive, int index, int address)
    {
        int codeSize = ReadU16(archive, address + 0x4);
        int stringBlock = ReadU16(archive, address + 0xA);
        int memorySlots = ReadU16(archive, address + 0xC);
        int importCount = ReadU16(archive, address + 0xE);

        var imports = new List<ConversationImport>();
        int pointer = address + 0x10;
        for (int f = 0; f < importCount; f++)
        {
            int length = ReadU16(archive, pointer);
            string name = System.Text.Encoding.ASCII.GetString(archive, pointer + 2, length);
            int id = ReadU16(archive, pointer + length + 2);
            int type = ReadU16(archive, pointer + length + 6);
            int returns = ReadU16(archive, pointer + length + 8);
            imports.Add(new ConversationImport(name, id, type == ImportTypeVariable, returns));
            pointer += length + 10;
        }

        var code = new short[codeSize];
        for (int c = 0; c < codeSize; c++)
            code[c] = (short)ReadU16(archive, pointer + c * 2);
        return new ConversationHeader(index, codeSize, stringBlock, memorySlots, imports, code);
    }

    public static IReadOnlyList<BablobEntry> ReadBablobs(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var entries = new List<BablobEntry>();
        for (int at = 0; at + 4 <= data.Length; at += 4)
            entries.Add(new BablobEntry(ReadU16(data, at), ReadU16(data, at + 2)));
        return entries;
    }

    private static int ReadU16(byte[] data, int at) =>
        (at < 0 || at + 2 > data.Length)
            ? throw new ArgumentException($"Read past end at {at}.")
            : data[at] | (data[at + 1] << 8);

    private static int ReadU32(byte[] data, int at) =>
        (at < 0 || at + 4 > data.Length)
            ? throw new ArgumentException($"Read past end at {at}.")
            : data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24);
}
