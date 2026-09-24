namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 palette/light tables as fixed-size raw records: PALS.DAT holds
/// 8 palettes of 768 bytes (256 × 3-byte RGB triplets), LIGHT.DAT 16 tables
/// of 256 bytes, SHADES.DAT a 96-byte shading table. Palette *meaning*
/// (cycles, fades) is presentation policy for a later media task; the
/// importer only guarantees shape.
/// Texture art (W64.TR/F32.TR) is out of scope here — see UW-T35.
/// Sizes observed from the shipped files; layout reference:
/// UnderworldGodot palette/shade/light loading.
/// </summary>
public static class PaletteTableReader
{
    public const int PaletteCount = 8;
    public const int PaletteSize = 768;
    public const int LightTableCount = 16;
    public const int LightTableSize = 256;
    public const int ShadesLength = 96;

    public sealed record PaletteTables(IReadOnlyList<byte[]> Palettes, IReadOnlyList<byte[]> Light, byte[] Shades);

    public static PaletteTables Read(ReadOnlySpan<byte> pals, ReadOnlySpan<byte> light, ReadOnlySpan<byte> shades)
    {
        if (pals.Length != PaletteCount * PaletteSize)
            throw new InvalidDataException($"PALS.DAT is {pals.Length} bytes; {PaletteCount * PaletteSize} required.");
        if (light.Length != LightTableCount * LightTableSize)
            throw new InvalidDataException($"LIGHT.DAT is {light.Length} bytes; {LightTableCount * LightTableSize} required.");
        if (shades.Length != ShadesLength)
            throw new InvalidDataException($"SHADES.DAT is {shades.Length} bytes; {ShadesLength} required.");

        var palettes = new byte[PaletteCount][];
        for (int i = 0; i < PaletteCount; i++) palettes[i] = pals.Slice(i * PaletteSize, PaletteSize).ToArray();
        var lights = new byte[LightTableCount][];
        for (int i = 0; i < LightTableCount; i++) lights[i] = light.Slice(i * LightTableSize, LightTableSize).ToArray();
        return new PaletteTables(palettes, lights, shades.ToArray());
    }
}
