using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Collision + navigation mesh emission from a decoded level: solid tiles
/// become boxes, open tiles become floor quads at their floor height, and
/// every tile becomes one navigation cell (open = walkable). Slopes emit
/// flat (their steepness rides with a later pass); door tiles emit open
/// (door blocking rides with door objects); ceilings are not emitted.
/// Scale constants are Ours (units per tile edge, units per height step)
/// until playtest calibration; the tile selection structure is donor data.
/// Output shape follows the Engine's spatial content schema (schemaVersion 1
/// triangle soup + navigation grid, as consumed via SpatialContentArtifact).
/// </summary>
public static class LevelCollisionMesh
{
    public sealed record MeshParameters(double UnitsPerTile = 8.0, double HeightUnitsPerStep = 1.0);

    public sealed record NavCell(int Column, int Row, int Level, double SupportHeight, bool Walkable);

    public sealed record CollisionMesh(
        double[] BoundsMin,
        double[] BoundsMax,
        float[][] Positions,
        int[] Triangles,
        NavCell[] NavCells);

    public static CollisionMesh Emit(LevArkReader.LevelPack pack, MeshParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        MeshParameters p = parameters ?? new MeshParameters();
        if (p.UnitsPerTile <= 0d || !double.IsFinite(p.UnitsPerTile))
            throw new ArgumentOutOfRangeException(nameof(parameters));
        if (p.HeightUnitsPerStep < 0d || !double.IsFinite(p.HeightUnitsPerStep))
            throw new ArgumentOutOfRangeException(nameof(parameters));

        var positions = new List<float[]>();
        var triangles = new List<int>();
        var cells = new List<NavCell>();
        double size = 64 * p.UnitsPerTile;

        int Add(float x, float y, float z)
        {
            positions.Add([x, y, z]);
            return positions.Count - 1;
        }

        void Quad(int a, int b, int c, int d)
        {
            triangles.AddRange([a, b, c, a, c, d]);
        }

        foreach (LevArkReader.Tile tile in pack.Tiles)
        {
            double x0 = tile.X * p.UnitsPerTile;
            double z0 = tile.Y * p.UnitsPerTile;
            double x1 = x0 + p.UnitsPerTile;
            double z1 = z0 + p.UnitsPerTile;
            double floorY = tile.FloorHeight * p.HeightUnitsPerStep;
            bool open = tile.Type != LevArkReader.TileSolid;

            if (open)
            {
                // Floor quad (up-facing), plus nothing above: ledges are cliffs.
                int v0 = Add((float)x0, (float)floorY, (float)z0);
                int v1 = Add((float)x1, (float)floorY, (float)z0);
                int v2 = Add((float)x1, (float)floorY, (float)z1);
                int v3 = Add((float)x0, (float)floorY, (float)z1);
                Quad(v0, v2, v1, v3);
            }
            else
            {
                // Full box one tile tall above the floor height.
                double top = floorY + p.UnitsPerTile;
                int b0 = Add((float)x0, (float)floorY, (float)z0);
                int b1 = Add((float)x1, (float)floorY, (float)z0);
                int b2 = Add((float)x1, (float)floorY, (float)z1);
                int b3 = Add((float)x0, (float)floorY, (float)z1);
                int t0 = Add((float)x0, (float)top, (float)z0);
                int t1 = Add((float)x1, (float)top, (float)z0);
                int t2 = Add((float)x1, (float)top, (float)z1);
                int t3 = Add((float)x0, (float)top, (float)z1);
                Quad(b0, b1, b2, b3); // bottom (harmless, keeps boxes closed)
                Quad(t0, t2, t1, t3); // top
                Quad(b0, t0, t1, b1); // sides
                Quad(b1, t1, t2, b2);
                Quad(b2, t2, t3, b3);
                Quad(b3, t3, t0, b0);
            }

            cells.Add(new NavCell(
                tile.X, tile.Y,
                (int)Math.Round(floorY / 0.25),
                floorY,
                open));
        }

        return new CollisionMesh(
            [0, 0, 0],
            [size, p.UnitsPerTile, size],
            positions.ToArray(),
            triangles.ToArray(),
            cells.ToArray());
    }

    /// <summary>Serialize to the Engine's spatial content JSON (triangle soup + nav grid).</summary>
    public static string ToJson(CollisionMesh mesh, string navId, string meshArtifactId)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrWhiteSpace(navId);
        ArgumentException.ThrowIfNullOrWhiteSpace(meshArtifactId);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("staticMeshArtifactId", meshArtifactId);
            writer.WriteStartObject("bounds");
            writer.WriteStartArray("min");
            foreach (double v in mesh.BoundsMin) writer.WriteNumberValue(v);
            writer.WriteEndArray();
            writer.WriteStartArray("max");
            foreach (double v in mesh.BoundsMax) writer.WriteNumberValue(v);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartObject("collision");
            writer.WriteStartArray("positions");
            foreach (float[] p in mesh.Positions)
            {
                writer.WriteStartArray();
                foreach (float v in p) writer.WriteNumberValue(v);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("triangles");
            foreach (int t in mesh.Triangles) writer.WriteNumberValue(t);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartObject("navigation");
            writer.WriteString("id", navId);
            writer.WriteStartObject("config");
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("cellSize", 8.0);
            writer.WriteNumber("levelQuantum", 0.25);
            writer.WriteNumber("maximumSlopeDegrees", 45);
            writer.WriteNumber("requiredHeadroom", 1.8);
            writer.WriteNumber("supportProbeDrop", 0.05);
            writer.WriteEndObject();
            writer.WriteStartArray("cells");
            foreach (NavCell c in mesh.NavCells)
            {
                writer.WriteStartObject();
                writer.WriteNumber("column", c.Column);
                writer.WriteNumber("row", c.Row);
                writer.WriteNumber("level", c.Level);
                writer.WriteNumber("supportHeight", c.SupportHeight);
                writer.WriteBoolean("walkable", c.Walkable);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
