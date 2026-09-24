using System.Security.Cryptography;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Controls;
using Rusty.Engine;

namespace AbyssRpg.Host;

/// <summary>
/// Scene bootstrap: hash bundled mesh bytes into a content artifact record
/// and open the spatial session the product steps. Mesh bytes are emitted
/// Import-side (LevelCollisionMesh) and ride in the content bundle (P07);
/// the artifact resolves them by path+sha at launch. NavigationGridId 0
/// addresses the level's single nav grid.
/// </summary>
public static class AbyssSceneBootstrap
{
    public static AbyssRpg.Kit.Controls.SpatialContentArtifact PrepareLevel(
        byte[] meshJson, int levelNumber)
    {
        ArgumentNullException.ThrowIfNull(meshJson);
        byte[] hash = SHA256.HashData(meshJson);
        var sha = new ContentSha256(
            BitConverter.ToUInt64(hash, 0),
            BitConverter.ToUInt64(hash, 8),
            BitConverter.ToUInt64(hash, 16),
            BitConverter.ToUInt64(hash, 24));
        return new SpatialContentArtifact(
            $"uw1/level-{levelNumber}/collision.json", sha, NavigationGridId: 0);
    }

    public static AbyssSpatialSession OpenSpatial(
        IEngineContext engine, SpatialContentArtifact artifact,
        SpatialTuning tuning, PlayerControlState player) =>
        new(engine, artifact, tuning, player);
}
