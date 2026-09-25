using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Visible-geometry emission from a decoded level, beside the collision mesh
/// that shares its tile selection. Floors emit per open-run quad at the run's
/// floor height; solid tiles emit their top and every side that faces open
/// space, so no interior face is uploaded. Ceilings are not emitted yet: the
/// ceiling height is not part of the tile record this reader decodes, and a
/// guessed ceiling would read as architecture.
/// Surface colors come from the level's own texture indices through a small
/// fixed palette (Ours until the texture/media import lands): they are
/// provisional presentation, not the original art.
/// The emitted JSON is a product-owned runtime contract interpreted by the
/// ruleset; it is deliberately not the Engine's spatial content schema.
/// </summary>
public static class LevelRenderMesh
{
    public sealed record Options(double UnitsPerTile = 8.0, double HeightUnitsPerStep = 1.0);

    public sealed record RenderMesh(
        double[] BoundsMin,
        double[] BoundsMax,
        float[][] Positions,
        float[][] Normals,
        float[][] Colors,
        int[] Indices);

    // Muted provisional palette; index selects by the level's own texture id.
    private static readonly float[][] FloorPalette =
    [
        [0.34f, 0.29f, 0.22f, 1f],
        [0.38f, 0.31f, 0.22f, 1f],
        [0.30f, 0.26f, 0.21f, 1f],
        [0.36f, 0.33f, 0.26f, 1f],
        [0.40f, 0.34f, 0.24f, 1f],
        [0.31f, 0.30f, 0.27f, 1f],
        [0.28f, 0.25f, 0.20f, 1f],
        [0.35f, 0.30f, 0.25f, 1f],
    ];

    private static readonly float[][] WallPalette =
    [
        [0.46f, 0.45f, 0.42f, 1f],
        [0.41f, 0.40f, 0.37f, 1f],
        [0.50f, 0.47f, 0.42f, 1f],
        [0.38f, 0.37f, 0.35f, 1f],
        [0.44f, 0.41f, 0.36f, 1f],
        [0.36f, 0.35f, 0.33f, 1f],
        [0.48f, 0.44f, 0.38f, 1f],
        [0.43f, 0.42f, 0.40f, 1f],
    ];

    private static readonly float[] WallTopColor = [0.30f, 0.29f, 0.27f, 1f];

    public static RenderMesh Emit(LevArkReader.LevelPack pack, Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Options p = options ?? new Options();
        if (p.UnitsPerTile <= 0d || !double.IsFinite(p.UnitsPerTile))
            throw new ArgumentOutOfRangeException(nameof(options), "UnitsPerTile must be finite and positive.");
        if (p.HeightUnitsPerStep <= 0d || !double.IsFinite(p.HeightUnitsPerStep))
            throw new ArgumentOutOfRangeException(nameof(options), "HeightUnitsPerStep must be finite and positive.");

        var positions = new List<float[]>();
        var normals = new List<float[]>();
        var colors = new List<float[]>();
        var indices = new List<int>();

        int dimension = LevArkReader.TileDimension;
        var tiles = new LevArkReader.Tile[dimension, dimension];
        foreach (LevArkReader.Tile tile in pack.Tiles) tiles[tile.X, tile.Y] = tile;

        double Height(LevArkReader.Tile tile) => tile.FloorHeight * p.HeightUnitsPerStep;
        bool Solid(int x, int y, double height) =>
            x >= 0 && y >= 0 && x < dimension && y < dimension
            && tiles[x, y] is { } neighbour
            && neighbour.Type != LevArkReader.TileOpen
            && Math.Abs(Height(neighbour) - height) < 1e-9;

        void Quad(float[] a, float[] b, float[] c, float[] d, float[] normal, float[] color)
        {
            int start = positions.Count;
            foreach (float[] vertex in new[] { a, b, c, d })
            {
                positions.Add(vertex);
                normals.Add(normal);
                colors.Add(color);
            }

            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }

        for (int y = 0; y < dimension; y++)
        {
            int x = 0;
            while (x < dimension)
            {
                LevArkReader.Tile tile = tiles[x, y];
                if (tile is null) { x++; continue; }
                double floor = Height(tile);
                bool open = tile.Type == LevArkReader.TileOpen;
                int runEnd = x;
                while (runEnd + 1 < dimension
                    && tiles[runEnd + 1, y] is { } next
                    && (next.Type == LevArkReader.TileOpen) == open
                    && Math.Abs(Height(next) - floor) < 1e-9
                    && (!open || next.FloorTexture == tile.FloorTexture))
                {
                    runEnd++;
                }

                float x0 = (float)(x * p.UnitsPerTile);
                float x1 = (float)((runEnd + 1) * p.UnitsPerTile);
                float z0 = (float)(y * p.UnitsPerTile);
                float z1 = (float)((y + 1) * p.UnitsPerTile);
                float top = (float)(floor + p.UnitsPerTile);

                if (open)
                {
                    float[] floorColor = FloorPalette[tile.FloorTexture % FloorPalette.Length];
                    Quad(
                        [x0, (float)floor, z0], [x0, (float)floor, z1],
                        [x1, (float)floor, z1], [x1, (float)floor, z0],
                        [0f, 1f, 0f], floorColor);
                }
                else
                {
                    float[] wallColor = WallPalette[tile.WallTexture % WallPalette.Length];
                    Quad(
                        [x0, top, z0], [x0, top, z1],
                        [x1, top, z1], [x1, top, z0],
                        [0f, 1f, 0f], WallTopColor);

                    // Side faces, one quad per exposed tile edge. Neighbors at a
                    // different floor height are exposed too: that face is the
                    // ledge the step or cliff is seen against.
                    for (int edge = x; edge <= runEnd; edge++)
                    {
                        float ex0 = (float)(edge * p.UnitsPerTile);
                        float ex1 = (float)((edge + 1) * p.UnitsPerTile);
                        float bottom = (float)floor;
                        if (!Solid(edge, y - 1, floor))
                        {
                            Quad(
                                [ex0, bottom, z0], [ex1, bottom, z0],
                                [ex1, top, z0], [ex0, top, z0],
                                [0f, 0f, -1f], wallColor);
                        }

                        if (!Solid(edge, y + 1, floor))
                        {
                            Quad(
                                [ex1, bottom, z1], [ex0, bottom, z1],
                                [ex0, top, z1], [ex1, top, z1],
                                [0f, 0f, 1f], wallColor);
                        }
                    }

                    if (!Solid(x - 1, y, floor))
                    {
                        Quad(
                            [x0, (float)floor, z1], [x0, (float)floor, z0],
                            [x0, top, z0], [x0, top, z1],
                            [-1f, 0f, 0f], wallColor);
                    }

                    if (!Solid(runEnd + 1, y, floor))
                    {
                        Quad(
                            [x1, (float)floor, z0], [x1, (float)floor, z1],
                            [x1, top, z1], [x1, top, z0],
                            [1f, 0f, 0f], wallColor);
                    }
                }

                x = runEnd + 1;
            }
        }

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        foreach (float[] vertex in positions)
        {
            minX = Math.Min(minX, vertex[0]); maxX = Math.Max(maxX, vertex[0]);
            minY = Math.Min(minY, vertex[1]); maxY = Math.Max(maxY, vertex[1]);
            minZ = Math.Min(minZ, vertex[2]); maxZ = Math.Max(maxZ, vertex[2]);
        }

        return new RenderMesh(
            [minX, minY, minZ], [maxX, maxY, maxZ],
            positions.ToArray(), normals.ToArray(), colors.ToArray(), indices.ToArray());
    }

    /// <summary>
    /// Serialize the product-owned render schema. Positions/normals/colors are
    /// parallel streams; indices address them in complete triangles.
    /// </summary>
    public static string ToJson(RenderMesh mesh, int level, Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Options p = options ?? new Options();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("level", level);
            writer.WriteNumber("unitsPerTile", p.UnitsPerTile);
            writer.WriteNumber("heightUnitsPerStep", p.HeightUnitsPerStep);
            writer.WriteStartObject("bounds");
            writer.WriteStartArray("min");
            foreach (double v in mesh.BoundsMin) writer.WriteNumberValue(v);
            writer.WriteEndArray();
            writer.WriteStartArray("max");
            foreach (double v in mesh.BoundsMax) writer.WriteNumberValue(v);
            writer.WriteEndArray();
            writer.WriteEndObject();
            WriteVectors(writer, "positions", mesh.Positions);
            WriteVectors(writer, "normals", mesh.Normals);
            WriteVectors(writer, "colors", mesh.Colors);
            writer.WriteStartArray("indices");
            foreach (int index in mesh.Indices) writer.WriteNumberValue(index);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteVectors(Utf8JsonWriter writer, string name, float[][] values)
    {
        writer.WriteStartArray(name);
        foreach (float[] value in values)
        {
            writer.WriteStartArray();
            foreach (float component in value) writer.WriteNumberValue(component);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}
