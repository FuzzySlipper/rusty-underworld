namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 COMOBJ.DAT: one 11-byte record per object definition starting at
/// offset 2. Only donor-documented fields are named; the rest stays raw bytes
/// so uncertain bits are never laundered into false knowledge.
/// Layout reference: UnderworldGodot src/loaders/comobjloader.cs (PTR =
/// offset + item_id * 11; height +0; radius/mass u16 +1 with radius in bits
/// 0-2 and mass in 0.1-stone tenths in bits 4-15; pickup is bit 5 of +3).
/// No code is shared with the donor.
/// </summary>
public static class CommonObjDatReader
{
    public const int RecordOffset = 2;
    public const int RecordSize = 11;

    public sealed record CommonObjRow(
        int Height,
        int Radius,
        int MassTenthStones,
        int MonetaryValue,
        bool CanBePickedUp,
        byte[] Raw);

    public static IReadOnlyList<CommonObjRow> Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < RecordOffset + RecordSize)
            throw new InvalidDataException($"COMOBJ.DAT is {data.Length} bytes; one 11-byte record needs {RecordOffset + RecordSize}.");
        if ((data.Length - RecordOffset) % RecordSize != 0)
            throw new InvalidDataException($"COMOBJ.DAT is {data.Length} bytes, not a whole number of 11-byte records.");

        int count = (data.Length - RecordOffset) / RecordSize;
        var rows = new CommonObjRow[count];
        for (int i = 0; i < count; i++)
        {
            int at = RecordOffset + (i * RecordSize);
            int radiusMass = data[at + 1] | (data[at + 2] << 8);
            // The int16 at +4 is the item's monetary value (donor:
            // src/loaders/comobjloader.cs monetaryvalue).
            int value = data[at + 4] | (data[at + 5] << 8);
            rows[i] = new CommonObjRow(
                Height: data[at],
                Radius: radiusMass & 0x7,
                MassTenthStones: (radiusMass >> 4) & 0xFFF,
                MonetaryValue: value,
                CanBePickedUp: ((data[at + 3] >> 5) & 1) == 1,
                Raw: data.Slice(at, RecordSize).ToArray());
        }

        return rows;
    }
}
