using AbyssRpg.Kit.Controls;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class SceneBootstrapTests
{
    [Fact]
    public void Mesh_bytes_hash_into_resolvable_artifact()
    {
        byte[] mesh = System.Text.Encoding.UTF8.GetBytes("""{"schemaVersion":1}""");
        SpatialContentArtifact artifact = AbyssSceneBootstrap.PrepareLevel(mesh, 1);
        Assert.Equal("uw1/level-1/collision.json", artifact.Path);
        Assert.Equal(0ul, artifact.NavigationGridId);

        SpatialContentArtifact again = AbyssSceneBootstrap.PrepareLevel(mesh, 1);
        Assert.Equal(artifact.Sha256, again.Sha256);
        Assert.NotEqual(
            artifact.Sha256,
            AbyssSceneBootstrap.PrepareLevel(System.Text.Encoding.UTF8.GetBytes("{}"), 1).Sha256);
    }

    [Fact]
    public void Spawn_opens_a_steppable_spatial_session()
    {
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service);
        SpatialContentArtifact artifact = AbyssSceneBootstrap.PrepareLevel([1, 2, 3], 1);
        using var spatial = AbyssSceneBootstrap.OpenSpatial(
            engine, artifact,
            new SpatialTuning(.5d, 8, 8, 1),
            new PlayerControlState(new WorldPoint(0, 10, 0), 0f, 0f));
        Assert.NotNull(spatial.Player);
    }
}
