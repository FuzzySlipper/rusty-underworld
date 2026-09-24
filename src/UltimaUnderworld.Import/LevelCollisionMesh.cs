using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Collision + navigation mesh emission from a decoded level: solid tiles
/// become boxes, open tiles become floor quads at their floor height, and
/// every tile becomes one navigation cell (open = walkable). Slopes emit
/// flat (their steepness rides with a later pass); door tiles emit open
/// (door blocking rides with door objects); ceilings are not emitted.
/// Scale constants are Ours (units per tile edge, units per height step,
/// nav level quantum) until playtest calibration; the tile selection
/// structure is donor data. Diagonal wedge tiles (types 2-5) emit as full
/// flat quads like slopes — the wedge halves they cover read as floor.
/// Output shape follows the Engine's spatial content schema (schemaVersion 1
/// triangle soup + navigation grid, as consumed via SpatialContentArtifact).
/// </summary>
public static class LevelCollisionMesh
{
    public sealed record MeshParameters(double UnitsPerTile = 8.0, double HeightUnitsPerStep = 1.0, double LevelQuantum = 0.25);

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
        if (p.LevelQuantum <= 0d || !double.IsFinite(p.LevelQuantum))
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
                Quad(v0, v3, v2, v1);
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
                Quad(t0, t3, t2, t1); // top
                Quad(b0, t0, t1, b1); // sides
                Quad(b1, t1, t2, b2);
                Quad(b2, t2, t3, b3);
                Quad(b3, t3, t0, b0);
            }

            cells.Add(new NavCell(
                tile.X, tile.Y,
                (int)Math.Round(floorY / p.LevelQuantum),
                floorY,
                open));
        }

        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (float[] v in positions)
        {
            if (v[1] < minY) minY = v[1];
            if (v[1] > maxY) maxY = v[1];
        }

        return new CollisionMesh(
            [0, minY, 0],
            [size, maxY, size],
            positions.ToArray(),
            triangles.ToArray(),
            cells.ToArray());
    }

    /// <summary>
    /// Serialize to the Engine's spatial content JSON (triangle soup +
    /// nav grid). Triangles emit as nested triplets per the Engine contract;
    /// config echoes the emission parameters so non-default scales stay
    /// self-consistent.
    /// </summary>
    public static string ToJson(CollisionMesh mesh, string navId, string meshArtifactId, MeshParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentException.ThrowIfNullOrWhiteSpace(navId);
        ArgumentException.ThrowIfNullOrWhiteSpace(meshArtifactId);
        MeshParameters p = parameters ?? new MeshParameters();
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
            foreach (float[] vertex in mesh.Positions)
            {
                writer.WriteStartArray();
                foreach (float v in vertex) writer.WriteNumberValue(v);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("triangles");
            if (mesh.Triangles.Length % 3 != 0)
                throw new InvalidOperationException("Triangle indices must form complete triplets.");
            for (int i = 0; i < mesh.Triangles.Length; i += 3)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(mesh.Triangles[i]);
                writer.WriteNumberValue(mesh.Triangles[i + 1]);
                writer.WriteNumberValue(mesh.Triangles[i + 2]);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartObject("navigation");
            writer.WriteString("id", navId);
            writer.WriteStartObject("config");
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("cellSize", p.UnitsPerTile);
            writer.WriteNumber("levelQuantum", p.LevelQuantum);
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
