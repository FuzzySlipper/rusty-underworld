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
        Assert.Equal(open * 4 + solid * 8, mesh.Positions.Length);
        Assert.Equal(open * 6 + solid * 36, mesh.Triangles.Length);
        Assert.Equal(4096, mesh.NavCells.Length);
        Assert.All(mesh.Triangles, i => Assert.InRange(i, 0, mesh.Positions.Length - 1));
        Assert.Equal(open, mesh.NavCells.Count(c => c.Walkable));

        string json = LevelCollisionMesh.ToJson(mesh, "navigation/uw-l1", "artifact/uw/l1/static-mesh");
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"walkable\":true", json);
        _output.WriteLine($"L1: {solid} solid, {open} open, {mesh.Positions.Length} verts, {mesh.Triangles.Length / 3} tris");
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

    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
