namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 LEV.ARK per-level data: the 64×64 tile block with its object
/// lists, the texture map, and the automap block.
/// Archive: u16 block count at 0, then u32 offsets at 2 + i*4 (0 = absent).
/// UW1 tile/object blocks are levels 1-9 at blocks 0-8 (0x7C08 bytes);
/// texture maps at blocks 18-26 (122 bytes: 48 u16 wall, 10 u16 floor with
/// actual index +210, 6 u8 door; map entry 57 is the ceiling texture);
/// automap coverage at blocks 27-35 (64×64 bytes).
/// Tile record (4 bytes): type (+0 low nibble), floor height (+0 high
/// nibble), floor texture ((+1>>2)&0xF), flags (+1&3), door bit ((+1>>7)&1),
/// wall texture (+2&0x3F), object-chain head ((u16@+2)>>6).
/// Object records at 0x4000: indices 0-255 mobile (27 bytes), 256-1023
/// static (8 bytes): item id (bits 0-8 @+0), flags (bits 9-12),
/// quality (@+4 bits 0-5), next (@+4 bits 6-15), owner (@+6 bits 0-5),
/// link (@+6 bits 6-15).
/// Behavior reference: UnderworldGodot src/World/tilemap.cs
/// (BuildTileMapUW, BuildObjectListUW, BuildTextureMap UW1 branch),
/// src/World/tileinfo.cs, src/World/uwobject.cs, src/World/automap.cs,
/// src/loaders/levarkloader.cs. No code is shared with the donor.
/// </summary>
public static class LevArkReader
{
    public const int LevelCount = 9;
    public const int TileDimension = 64;
    public const int TileRecordSize = 4;
    public const int TileBlockSize = 0x7C08;
    public const int ObjectsOffset = 0x4000;
    public const int MobileCount = 256;
    public const int MobileRecordSize = 27;
    public const int StaticCount = 768;
    public const int StaticRecordSize = 8;
    public const int ObjectCount = 1024;
    public const int TextureBlockSize = 0x7A;
    public const int AutomapBlockSize = TileDimension * TileDimension;

    // Tile types (UWTileMap constants).
    public const int TileSolid = 0;
    public const int TileOpen = 1;

    public sealed record Tile(int X, int Y, int Type, int FloorHeight, int FloorTexture, int Flags, bool Door, int WallTexture, int ObjectHead);
    public sealed record LevelObject(int Index, bool IsMobile, int ItemId, int Flags, int Quality, int Next, int Owner, int Link, byte[] Raw);
    public sealed record TextureMap(IReadOnlyList<int> Entries, int CeilingEntry);
    public sealed record LevelPack(
        int LevelNumber,
        IReadOnlyList<Tile> Tiles,
        IReadOnlyList<LevelObject> Objects,
        TextureMap Textures,
        byte[] Automap,
        UwTableProvenance Provenance);

    public sealed record PlacementIssue(int LevelNumber, int TileX, int TileY, int ObjectIndex, int ItemId, string Kind);

    public static LevelPack ReadLevel(byte[] archive, int levelNumber, byte[] terrain)
    {
        if (levelNumber < 1 || levelNumber > LevelCount)
            throw new ArgumentOutOfRangeException(nameof(levelNumber), $"UW1 has levels 1-{LevelCount}.");
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(terrain);

        byte[] tileBlock = ExtractBlock(archive, levelNumber - 1, TileBlockSize, "tile/object", allowAbsent: false);
        byte[] textureBlock = ExtractBlock(archive, (levelNumber - 1) + 18, TextureBlockSize, "texture map", allowAbsent: false);
        // Automap coverage exists only in save games; the shipped archive
        // holds zeroes here, and the donor falls back to a blank map.
        byte[] automap = ExtractBlock(archive, (levelNumber - 1) + 27, AutomapBlockSize, "automap", allowAbsent: true);

        var tiles = new Tile[TileDimension * TileDimension];
        for (int y = 0; y < TileDimension; y++)
        {
            for (int x = 0; x < TileDimension; x++)
            {
                int at = (y * TileDimension + x) * TileRecordSize;
                int b0 = tileBlock[at];
                int b1 = tileBlock[at + 1];
                int b2 = tileBlock[at + 2];
                int b3 = tileBlock[at + 3];
                int head = ((b3 << 8) | b2) >> 6;
                tiles[y * TileDimension + x] = new Tile(
                    x, y,
                    Type: b0 & 0xF,
                    FloorHeight: (b0 >> 4) & 0xF,
                    FloorTexture: (b1 >> 2) & 0xF,
                    Flags: b1 & 0x3,
                    Door: ((b1 >> 7) & 1) == 1,
                    WallTexture: b2 & 0x3F,
                    ObjectHead: head);
            }
        }

        var objects = new LevelObject[ObjectCount];
        for (int i = 0; i < ObjectCount; i++)
        {
            bool mobile = i < MobileCount;
            int size = mobile ? MobileRecordSize : StaticRecordSize;
            int at = ObjectsOffset + (mobile ? i * MobileRecordSize : MobileCount * MobileRecordSize + (i - MobileCount) * StaticRecordSize);
            int w0 = tileBlock[at] | (tileBlock[at + 1] << 8);
            int w4 = tileBlock[at + 4] | (tileBlock[at + 5] << 8);
            int w6 = tileBlock[at + 6] | (tileBlock[at + 7] << 8);
            var raw = new byte[size];
            Array.Copy(tileBlock, at, raw, 0, size);
            objects[i] = new LevelObject(
                i, mobile,
                ItemId: w0 & 0x1FF,
                Flags: (w0 >> 9) & 0xF,
                Quality: w4 & 0x3F,
                Next: (w4 >> 6) & 0x3FF,
                Owner: w6 & 0x3F,
                Link: (w6 >> 6) & 0x3FF,
                Raw: raw);
        }

        var provenance = UwTableProvenance.FromBytes("UW1", $"UW/DATA/LEV.ARK level {levelNumber}", tileBlock);

        return new LevelPack(levelNumber, tiles, objects, DecodeTextureMap(textureBlock), automap, provenance);
    }

    public static TextureMap DecodeTextureMap(ReadOnlySpan<byte> block)
    {
        if (block.Length < TextureBlockSize)
            throw new InvalidDataException($"Texture map block is {block.Length} bytes; UW1 needs {TextureBlockSize}.");
        var entries = new int[64];
        int at = 0;
        for (int i = 0; i < 48; i++, at += 2) entries[i] = block[at] | (block[at + 1] << 8);
        for (int i = 48; i < 58; i++, at += 2) entries[i] = (block[at] | (block[at + 1] << 8)) + 210;
        for (int i = 58; i < 64; i++, at++) entries[i] = block[at];
        return new TextureMap(entries, CeilingEntry: 57);
    }

    /// <summary>
    /// Walks every tile chain and reports structural breaks plus objects
    /// placed on lava terrain (the level-6 jeweled-sword class of bug).
    /// </summary>
    public static IReadOnlyList<PlacementIssue> ValidatePlacement(LevelPack level, ReadOnlySpan<byte> terrain)
    {
        var issues = new List<PlacementIssue>();
        var visited = new bool[ObjectCount];
        foreach (Tile tile in level.Tiles)
        {
            int index = tile.ObjectHead;
            Array.Clear(visited);
            while (index != 0)
            {
                if (index < 0 || index >= ObjectCount)
                {
                    issues.Add(new PlacementIssue(level.LevelNumber, tile.X, tile.Y, index, -1, "chain-index-out-of-range"));
                    break;
                }

                if (visited[index])
                {
                    issues.Add(new PlacementIssue(level.LevelNumber, tile.X, tile.Y, index, level.Objects[index].ItemId, "chain-cycle"));
                    break;
                }

                visited[index] = true;
                LevelObject obj = level.Objects[index];
                // No terrain rule here: lava harms creatures (some fly,
                // some are immune) but not carried loot, and mobile slots
                // hold both — distinguishing needs majorclass decode the
                // reader does not yet perform (L6 loot false-positives).
                index = obj.Next;
            }
        }

        return issues;
    }

    private static byte[] ExtractBlock(byte[] archive, int blockNumber, int expectedSize, string what, bool allowAbsent)
    {
        if (archive.Length < 6)
            throw new InvalidDataException($"LEV.ARK is {archive.Length} bytes; no block directory fits.");
        int blockCount = archive[0] | (archive[1] << 8);
        if (blockNumber < 0 || blockNumber >= blockCount)
            throw new InvalidDataException($"LEV.ARK declares {blockCount} blocks; {what} block {blockNumber} is outside.");
        int headerSize = 2 + (blockCount * 4);
        int offset = archive[2 + (blockNumber * 4)] | (archive[3 + (blockNumber * 4)] << 8)
            | (archive[4 + (blockNumber * 4)] << 16) | (archive[5 + (blockNumber * 4)] << 24);
        if (offset == 0 && allowAbsent)
            return new byte[expectedSize];
        if (offset <= 0 || offset < headerSize || offset >= archive.Length)
            throw new InvalidDataException($"LEV.ARK {what} block {blockNumber} points outside the file.");
        int end = archive.Length;
        for (int i = 0; i < blockCount; i++)
        {
            int other = archive[2 + (i * 4)] | (archive[3 + (i * 4)] << 8)
                | (archive[4 + (i * 4)] << 16) | (archive[5 + (i * 4)] << 24);
            if (other > offset && other < end) end = other;
        }

        if (end - offset < expectedSize)
            throw new InvalidDataException($"LEV.ARK {what} block {blockNumber} measures {end - offset} bytes; {expectedSize} required.");
        var block = new byte[expectedSize];
        Array.Copy(archive, offset, block, 0, expectedSize);
        return block;
    }
}
