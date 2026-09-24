namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 TERRAIN.DAT: 16-bit words (word j is the u16LE at file byte 2j).
/// UW1 terrain kind for a tile is the word Terrain[46 + actualTexture], where
/// actualTexture is the texture-map value for the tile's FLOOR-section entry
/// (tile floor field + 48). Lava is the full word 0x20, water 0x10.
/// Behavior reference: UnderworldGodot src/loaders/terraindatloader.cs
/// (word reads, GetTerrainTypeNo UW1 branch, Lava/Water constants),
/// src/World/tilemaprender.cs (floor field + 48) and src/World/tileinfo.cs.
/// No code is shared.
/// </summary>
public static class TerrainDatReader
{
    public const int Uw1TerrainBase = 46;
    public const int LavaWord = 0x20;
    public const int WaterWord = 0x10;

    public static int TerrainWord(ReadOnlySpan<byte> terrain, int actualTexture)
    {
        int at = (Uw1TerrainBase + actualTexture) * 2;
        if (at < 0 || at + 2 > terrain.Length)
            throw new InvalidDataException($"TERRAIN.DAT has {terrain.Length} bytes; terrain word at byte {at} is out of range.");
        return terrain[at] | (terrain[at + 1] << 8);
    }

    public static bool IsLava(ReadOnlySpan<byte> terrain, int actualTexture) =>
        TerrainWord(terrain, actualTexture) == LavaWord;

    public static bool IsWater(ReadOnlySpan<byte> terrain, int actualTexture) =>
        TerrainWord(terrain, actualTexture) == WaterWord;
}
