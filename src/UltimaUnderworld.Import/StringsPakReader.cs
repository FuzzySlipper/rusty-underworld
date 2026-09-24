namespace UltimaUnderworld.Import;

/// <summary>
/// Decodes UW1 STRINGS.PAK: a Huffman-coded string archive.
/// Layout: u16 node count, then that many 4-byte nodes (symbol, parent,
/// left, right); u16 block count, then per block (u16 block number, u32
/// data address). Each block holds a u16 entry count, an entry-offset table,
/// then MSB-first Huffman bits decoded from the last node as root; a leaf is
/// a node whose left AND right are both 255, and the '|' leaf ends an entry.
/// Behavior reference: UnderworldGodot src/utility/StringLoader.cs
/// (GameStrings.LoadStringsPak); no code is shared with the donor. UW2
/// archives are never read here.
/// </summary>
public static class StringsPakReader
{
    private const byte LeafEdge = 255;
    private const char EntryTerminator = '|';

    public sealed record DecodedStrings(IReadOnlyDictionary<int, IReadOnlyList<string>> Blocks)
    {
        public string GetString(int block, int index)
        {
            if (!Blocks.TryGetValue(block, out IReadOnlyList<string>? entries))
                throw new ArgumentOutOfRangeException(nameof(block), $"No string block {block}.");
            if (index < 0 || index >= entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Block {block} holds {entries.Count} strings.");
            return entries[index];
        }
    }

    public static DecodedStrings Decode(ReadOnlySpan<byte> data)
    {
        int cursor = 0;
        int nodeCount = ReadU16(data, ref cursor);
        if (nodeCount <= 0)
            throw new InvalidDataException($"STRINGS.PAK declares {nodeCount} Huffman nodes.");

        const int NodeSize = 4;
        if (data.Length < 2 + (nodeCount * NodeSize) + 2)
            throw new InvalidDataException("STRINGS.PAK is too short for its Huffman tree.");
        byte[] symbols = new byte[nodeCount];
        byte[] left = new byte[nodeCount];
        byte[] right = new byte[nodeCount];
        for (int node = 0; node < nodeCount; node++)
        {
            symbols[node] = data[cursor];
            // byte 1 is the parent link, unused by the decoder.
            left[node] = data[cursor + 2];
            right[node] = data[cursor + 3];
            cursor += NodeSize;
        }

        int blockCount = ReadU16(data, ref cursor);
        if (blockCount <= 0)
            throw new InvalidDataException($"STRINGS.PAK declares {blockCount} blocks.");

        var blocks = new Dictionary<int, IReadOnlyList<string>>(blockCount);
        for (int b = 0; b < blockCount; b++)
        {
            int blockNumber = ReadU16(data, ref cursor);
            uint address = ReadU32(data, ref cursor);
            blocks[blockNumber] = DecodeBlock(data, symbols, left, right, nodeCount, address, blockNumber);
        }

        return new DecodedStrings(blocks);
    }

    private static IReadOnlyList<string> DecodeBlock(
        ReadOnlySpan<byte> data, byte[] symbols, byte[] left, byte[] right,
        int nodeCount, uint address, int blockNumber)
    {
        if (address + 2 > (uint)data.Length)
            throw new InvalidDataException($"Block {blockNumber} address runs past end of file.");
        int entryCount = data[(int)address] | (data[(int)address + 1] << 8);
        int stream = (int)address + 2 + (entryCount * 2);
        if (stream > data.Length)
            throw new InvalidDataException($"Block {blockNumber} bitstream runs past end of file.");

        var entries = new List<string>(entryCount);
        var text = new System.Text.StringBuilder();

        for (int entry = 0; entry < entryCount; entry++)
        {
            // The bit reader restarts at a fresh byte for every entry: leftover
            // bits of the previous byte are dropped at entry boundaries.
            int bit = 0;
            int raw = 0;
            text.Clear();
            while (true)
            {
                int node = nodeCount - 1; // root: last node
                while (left[node] != LeafEdge && right[node] != LeafEdge)
                {
                    if (bit == 0)
                    {
                        if (stream >= data.Length)
                            throw new InvalidDataException($"Block {blockNumber} string data ends at entry {entry}.");
                        bit = 8;
                        raw = data[stream++];
                    }

                    node = (raw & 0x80) == 0x80 ? right[node] : left[node];
                    if (node >= nodeCount)
                        throw new InvalidDataException($"Block {blockNumber} Huffman walk left the tree at entry {entry}.");
                    raw <<= 1;
                    bit--;
                }

                char symbol = (char)symbols[node];
                if (symbol == EntryTerminator)
                    break;
                text.Append(symbol);
            }

            entries.Add(text.ToString());
        }

        return entries;
    }

    private static ushort ReadU16(ReadOnlySpan<byte> data, ref int cursor)
    {
        if (cursor < 0 || cursor + 2 > data.Length)
            throw new InvalidDataException($"Read past end of file at offset {cursor}.");
        ushort value = (ushort)(data[cursor] | (data[cursor + 1] << 8));
        cursor += 2;
        return value;
    }

    private static uint ReadU32(ReadOnlySpan<byte> data, ref int cursor)
    {
        if (cursor < 0 || cursor + 4 > data.Length)
            throw new InvalidDataException($"Read past end of file at offset {cursor}.");
        uint value = (uint)(data[cursor] | (data[cursor + 1] << 8) | (data[cursor + 2] << 16) | (data[cursor + 3] << 24));
        cursor += 4;
        return value;
    }
}
