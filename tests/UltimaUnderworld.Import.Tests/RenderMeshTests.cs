using System.Text.Json;
using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

/// <summary>
/// The emitted visible geometry: closed, indexed, in bounds, and consistent with
/// the collision mesh it shares a tile selection with. The spawn is derived from
/// the same level, so it is checked against the tile it names.
/// </summary>
public sealed class RenderMeshTests
{
    private readonly ITestOutputHelper _output;

    public RenderMeshTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Shipped_level_1_emits_indexed_render_geometry()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, 1, terrain);
        LevelRenderMesh.RenderMesh mesh = LevelRenderMesh.Emit(pack);

        Assert.Equal(mesh.Positions.Length, mesh.Normals.Length);
        Assert.Equal(mesh.Positions.Length, mesh.Colors.Length);
        Assert.Equal(0, mesh.Indices.Length % 3);
        Assert.True(mesh.Indices.Length > 0);
        Assert.All(mesh.Indices, index => Assert.InRange(index, 0, mesh.Positions.Length - 1));
        foreach (float[] vertex in mesh.Positions)
        {
            Assert.InRange(vertex[0], mesh.BoundsMin[0], mesh.BoundsMax[0]);
            Assert.InRange(vertex[1], mesh.BoundsMin[1], mesh.BoundsMax[1]);
            Assert.InRange(vertex[2], mesh.BoundsMin[2], mesh.BoundsMax[2]);
        }

        // Same tile selection, same extent: render and collision agree on the
        // space the level occupies even though they emit different surfaces.
        LevelCollisionMesh.CollisionMesh collision = LevelCollisionMesh.Emit(pack);
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal(collision.BoundsMin[axis], mesh.BoundsMin[axis], 3);
            Assert.Equal(collision.BoundsMax[axis], mesh.BoundsMax[axis], 3);
        }

        string json = LevelRenderMesh.ToJson(mesh, 1);
        using var parsed = JsonDocument.Parse(json);
        JsonElement root = parsed.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("level").GetInt32());
        Assert.Equal(mesh.Positions.Length, root.GetProperty("positions").GetArrayLength());
        Assert.Equal(mesh.Positions.Length, root.GetProperty("normals").GetArrayLength());
        Assert.Equal(mesh.Positions.Length, root.GetProperty("colors").GetArrayLength());
        Assert.Equal(mesh.Indices.Length, root.GetProperty("indices").GetArrayLength());
        _output.WriteLine($"L1 render: {mesh.Positions.Length} verts, {mesh.Indices.Length / 3} tris");
    }

    [Fact]
    public void Spawn_stands_on_an_open_tile_inside_the_level()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, 1, terrain);
        LevelSpawn.Spawn spawn = LevelSpawn.Choose(pack);
        LevArkReader.Tile tile = pack.Tiles.Single(t => t.X == spawn.TileX && t.Y == spawn.TileY);
        Assert.Equal(LevArkReader.TileOpen, tile.Type);
        Assert.Equal(tile.FloorHeight, spawn.Y, 3);
        Assert.Equal((spawn.TileX + 0.5d) * 8d, spawn.X, 3);
        Assert.Equal((spawn.TileY + 0.5d) * 8d, spawn.Z, 3);
        Assert.Equal(8, spawn.OpenNeighbors);
        _output.WriteLine($"spawn tile ({spawn.TileX},{spawn.TileY}) at ({spawn.X},{spawn.Y},{spawn.Z})");
    }

    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
