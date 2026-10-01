using System.Buffers.Binary;
using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>Normalized UW1 viewing distances and palette remaps. Layout evidence:
/// UnderworldGodot src/loaders/paletteloader.cs and shadesdatloader.cs.
/// RGB channels are six-bit source values shifted two bits by the donor;
/// viewing distance is the low nibble at byte six of each 12-byte shade record.
/// The product uses these tables with its own four distance bands.</summary>
public static class LightingPack
{
    public const string PackId = "abyssrpg.lighting";
    public static string Emit(byte[] pals, byte[] light, byte[] shades)
    {
        var tables = PaletteTableReader.Read(pals, light, shades);
        var colors = tables.Light.Select(map => map.Select(index => new[] {
            (tables.Palettes[0][index * 3] << 2) / 255f,
            (tables.Palettes[0][index * 3 + 1] << 2) / 255f,
            (tables.Palettes[0][index * 3 + 2] << 2) / 255f,
        }).ToArray()).ToArray();
        return JsonSerializer.Serialize(new {
            schemaVersion = 1,
            sources = new[] {
                UwTableProvenance.FromBytes("UW1", "UW/DATA/PALS.DAT", pals),
                UwTableProvenance.FromBytes("UW1", "UW/DATA/LIGHT.DAT", light),
                UwTableProvenance.FromBytes("UW1", "UW/DATA/SHADES.DAT", shades),
            },
            viewingDistances = Enumerable.Range(0, 8).Select(i =>
                BinaryPrimitives.ReadUInt16LittleEndian(shades.AsSpan(i * 12 + 6, 2)) & 15).ToArray(),
            colors,
        });
    }
}
