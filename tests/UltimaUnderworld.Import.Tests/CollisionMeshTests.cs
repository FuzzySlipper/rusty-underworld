using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

public sealed class CollisionMeshTests
{
    private readonly ITestOutputHelper _output;

    public CollisionMeshTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Shipped_level_1_emits_closed_indexed_mesh()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, 1, terrain);
        LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);

        int solid = pack.Tiles.Count(t => t.Type == LevArkReader.TileSolid);
        int open = pack.Tiles.Count - solid;
        Assert.Equal(2060, solid);
        Assert.Equal(2036, open);
        Assert.Equal(open * 4 + solid * 8, mesh.Positions.Length);
        Assert.Equal(open * 6 + solid * 36, mesh.Triangles.Length);
        Assert.Equal(4096, mesh.NavCells.Length);
        Assert.All(mesh.Triangles, i => Assert.InRange(i, 0, mesh.Positions.Length - 1));
        Assert.Equal(open, mesh.NavCells.Count(c => c.Walkable));
        // Bounds enclose every emitted vertex (Engine ingest requirement).
        foreach (float[] v in mesh.Positions)
        {
            Assert.InRange(v[0], mesh.BoundsMin[0], mesh.BoundsMax[0]);
            Assert.InRange(v[1], mesh.BoundsMin[1], mesh.BoundsMax[1]);
            Assert.InRange(v[2], mesh.BoundsMin[2], mesh.BoundsMax[2]);
        }

        string json = LevelCollisionMesh.ToJson(mesh, "navigation/uw-l1", "artifact/uw/l1/static-mesh");
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"walkable\":true", json);
        _output.WriteLine($"L1: {solid} solid, {open} open, {mesh.Positions.Length} verts, {mesh.Triangles.Length / 3} tris");

        // Independent read-back: the emitted document parses and carries
        // the same counts through the JSON layer.
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement root = parsed.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(mesh.Positions.Length, root.GetProperty("collision").GetProperty("positions").GetArrayLength());
        // Triangles emit as nested triplets per the Engine contract.
        System.Text.Json.JsonElement tris = root.GetProperty("collision").GetProperty("triangles");
        Assert.Equal(mesh.Triangles.Length / 3, tris.GetArrayLength());
        foreach (System.Text.Json.JsonElement tri in tris.EnumerateArray())
            Assert.Equal(3, tri.GetArrayLength());
        Assert.Equal(4096, root.GetProperty("navigation").GetProperty("cells").GetArrayLength());
    }

    [Fact]
    public void Slopes_and_doors_emit_flat_and_open()
    {
        var pack = new LevArkReader.LevelPack(
            1,
            [
                new LevArkReader.Tile(0, 0, 0, 0, 0, 0, false, 0, 0),
                new LevArkReader.Tile(1, 0, 2, 3, 0, 0, false, 0, 0),
                new LevArkReader.Tile(0, 1, 1, 1, 0, 0, true, 0, 0),
            ],
            [],
            new LevArkReader.TextureMap([], 0),
            [],
            new UwTableProvenance("UW1", "TEST", 0, "00"));
        LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);

        Assert.Equal(2 * 4 + 8, mesh.Positions.Length);
        Assert.Equal(2, mesh.NavCells.Count(c => c.Walkable));
        Assert.Equal(3.0, mesh.NavCells.First(c => c.Column == 1 && c.Row == 0).SupportHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelCollisionMesh.Emit(pack, new LevelCollisionMesh.MeshParameters(UnitsPerTile: 0)));
    }

    [Fact]
    public void Diagonals_emit_flat_and_walkable_by_documented_approximation()
    {
        var pack = new LevArkReader.LevelPack(
            1,
            [new LevArkReader.Tile(0, 0, 4, 2, 0, 0, false, 0, 0)],
            [],
            new LevArkReader.TextureMap([], 0),
            [],
            new UwTableProvenance("UW1", "TEST", 0, "00"));
        LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);

        // Type 4 is a diagonal wedge; until wedges emit, it reads as floor.
        Assert.Single(mesh.NavCells);
        Assert.True(mesh.NavCells[0].Walkable);
        Assert.Equal(4, mesh.Positions.Length); // one flat quad, not a box
    }

    [Fact]
    public void Every_triangle_faces_outward()
    {
        var pack = new LevArkReader.LevelPack(
            1,
            [
                new LevArkReader.Tile(0, 0, 1, 0, 0, 0, false, 0, 0),
                new LevArkReader.Tile(1, 0, 0, 0, 0, 0, false, 0, 0),
            ],
            [],
            new LevArkReader.TextureMap([], 0),
            [],
            new UwTableProvenance("UW1", "TEST", 0, "00"));
        LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);

        // Emission order: open floor quad (2 tris), then box bottom/top/sides.
        float NormalY(int tri)
        {
            float[] a = mesh.Positions[mesh.Triangles[tri * 3]];
            float[] b = mesh.Positions[mesh.Triangles[tri * 3 + 1]];
            float[] c = mesh.Positions[mesh.Triangles[tri * 3 + 2]];
            return ((b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]));
        }

        Assert.Equal(14, mesh.Triangles.Length / 3); // 2 floor + 12 box
        Assert.True(NormalY(0) > 0f);
        Assert.True(NormalY(1) > 0f); // floor faces up, both triangles
        Assert.True(NormalY(2) < 0f);
        Assert.True(NormalY(3) < 0f); // box bottom faces down
        Assert.True(NormalY(4) > 0f);
        Assert.True(NormalY(5) > 0f); // box top faces up
    }

    [Fact]
    public void Non_default_parameters_stay_self_consistent()
    {
        var pack = new LevArkReader.LevelPack(
            1,
            [new LevArkReader.Tile(0, 0, 1, 4, 0, 0, false, 0, 0)],
            [],
            new LevArkReader.TextureMap([], 0),
            [],
            new UwTableProvenance("UW1", "TEST", 0, "00"));
        var parameters = new LevelCollisionMesh.MeshParameters(UnitsPerTile: 4.0, HeightUnitsPerStep: 2.0, LevelQuantum: 0.5);
        LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);
        string json = LevelCollisionMesh.ToJson(mesh, "nav", "mesh", parameters);

        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement config = parsed.RootElement.GetProperty("navigation").GetProperty("config");
        Assert.Equal(4.0, config.GetProperty("cellSize").GetDouble());
        Assert.Equal(0.5, config.GetProperty("levelQuantum").GetDouble());
        Assert.Equal(16, parsed.RootElement.GetProperty("navigation").GetProperty("cells")[0].GetProperty("level").GetInt32());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelCollisionMesh.Emit(pack, parameters with { LevelQuantum = 0 }));
    }

    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
