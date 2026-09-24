namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 TERRAIN.DAT: a flat byte array of terrain descriptors.
/// UW1 terrain kind for a tile is the high nibble of
/// Terrain[46 + actualTexture], where actualTexture is the texture-map value
/// for the tile's floor texture (the +210 floor offset minus 256 gives 46).
/// Lava is 0x20, water is 0x10.
/// Behavior reference: UnderworldGodot src/loaders/terraindatloader.cs
/// (TerrainTypes, GetTerrainTypeNo UW1 branch). No code is shared.
/// </summary>
public static class TerrainDatReader
{
    public const int Uw1TerrainBase = 46;
    public const int LavaNibble = 0x20 >> 4;
    public const int WaterNibble = 0x10 >> 4;

    public static int TerrainNibble(ReadOnlySpan<byte> terrain, int actualTexture)
    {
        int at = Uw1TerrainBase + actualTexture;
        if (at < 0 || at >= terrain.Length)
            throw new InvalidDataException($"TERRAIN.DAT has {terrain.Length} bytes; terrain entry {at} is out of range.");
        return (terrain[at] >> 4) & 0xF;
    }

    public static bool IsLava(ReadOnlySpan<byte> terrain, int actualTexture) =>
        TerrainNibble(terrain, actualTexture) == LavaNibble;

    public static bool IsWater(ReadOnlySpan<byte> terrain, int actualTexture) =>
        TerrainNibble(terrain, actualTexture) == WaterNibble;
}
