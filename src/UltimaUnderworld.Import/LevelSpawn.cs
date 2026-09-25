namespace UltimaUnderworld.Import;

/// <summary>
/// Deterministic spawn selection from imported geometry: the open tile with the
/// most open space around it wins, ties break on (row, column). The original's
/// own start tile lives in state this importer does not read yet, so the choice
/// is derived from the level rather than recalled — and the manifest records it
/// as derived so no reader mistakes it for original data.
/// </summary>
public static class LevelSpawn
{
    public sealed record Spawn(int TileX, int TileY, double X, double Y, double Z, double YawRadians, int OpenNeighbors);

    public static Spawn Choose(LevArkReader.LevelPack pack, LevelRenderMesh.Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        LevelRenderMesh.Options p = options ?? new LevelRenderMesh.Options();
        int dimension = LevArkReader.TileDimension;
        var tiles = new LevArkReader.Tile[dimension, dimension];
        foreach (LevArkReader.Tile tile in pack.Tiles) tiles[tile.X, tile.Y] = tile;

        Spawn? best = null;
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                LevArkReader.Tile tile = tiles[x, y];
                if (tile is null || tile.Type != LevArkReader.TileOpen) continue;
                int open = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= dimension || ny >= dimension) continue;
                        if (tiles[nx, ny] is { Type: LevArkReader.TileOpen }) open++;
                    }
                }

                var candidate = new Spawn(
                    x, y,
                    (x + 0.5d) * p.UnitsPerTile,
                    tile.FloorHeight * p.HeightUnitsPerStep,
                    (y + 0.5d) * p.UnitsPerTile,
                    0d,
                    open);
                if (best is null
                    || candidate.OpenNeighbors > best.OpenNeighbors
                    || (candidate.OpenNeighbors == best.OpenNeighbors
                        && (candidate.TileY != best.TileY
                            ? candidate.TileY < best.TileY
                            : candidate.TileX < best.TileX)))
                {
                    best = candidate;
                }
            }
        }

        return best ?? throw new InvalidOperationException("The level has no open tile to spawn on.");
    }
}
