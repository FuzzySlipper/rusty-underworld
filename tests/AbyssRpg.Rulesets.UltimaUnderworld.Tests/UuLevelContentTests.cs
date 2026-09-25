using System.Text;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// The level manifest's declared content identity is what the Engine resolves
/// the collision artifact by. The importer writes the identity as
/// "sha256:&lt;hex&gt;"; this pins that the reader turns it into the same Engine
/// value the Kit produces from the same bytes.
/// </summary>
public sealed class UuLevelContentTests
{
    [Fact]
    public void Declared_identity_matches_the_engine_value_for_the_same_digest()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("level collision bytes");
        string declared = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

        ContentSha256 parsed = UuLevelContent.ParseSha256(declared, "manifest");
        Assert.Equal(ContentHashing.Of(bytes), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256:")]
    [InlineData("sha256:00ff")]
    [InlineData("abc123")]
    [InlineData("sha256:zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void A_malformed_identity_is_refused_by_name(string declared)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuLevelContent.ParseSha256(declared, "content pack 'abyssrpg.level-1'"));
        Assert.Contains("abyssrpg.level-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_manifest_round_trips_its_declared_identity()
    {
        byte[] collision = Encoding.UTF8.GetBytes("[collision]");
        string sha = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(collision));
        string manifest = $$"""
        {
          "schemaVersion": 1,
          "level": 3,
          "collision": { "path": "abyss/imports/level-3/collision.json", "sha256": "{{sha}}" },
          "render": { "path": "abyss/imports/level-3/render.json" },
          "spawn": { "tileX": 2, "tileY": 5, "x": 20.0, "y": 1.0, "z": 44.0, "yawRadians": 0.5, "origin": "derived-open-space" },
          "provenance": { "source": "UW1", "origin": "LEV.ARK", "tiles": 4096, "objects": 128 }
        }
        """;

        UuLevelDefinition level = UuLevelContent.Read(Encoding.UTF8.GetBytes(manifest), "content pack 'abyssrpg.level-3'");
        Assert.Equal(3, level.Level);
        Assert.Equal("abyss/imports/level-3/collision.json", level.Collision.Path);
        Assert.Equal(ContentHashing.Of(collision), level.Collision.Sha256);
        Assert.Equal("LEV.ARK", level.ProvenanceOrigin);
        Assert.Equal("UW1", level.ProvenanceSource);
        Assert.Equal(20.0, level.Spawn.Position.X, 3);
        Assert.Equal(44.0, level.Spawn.Position.Z, 3);
        Assert.Equal(0.5f, level.Spawn.HeadingYawRadians, 3);
    }

    [Fact]
    public void A_manifest_from_another_source_is_refused()
    {
        string manifest = """
        {
          "schemaVersion": 1,
          "level": 1,
          "collision": { "path": "a.json", "sha256": "sha256:0000000000000000000000000000000000000000000000000000000000000000" },
          "render": { "path": "b.json" },
          "spawn": { "tileX": 0, "tileY": 0, "x": 0.0, "y": 0.0, "z": 0.0, "yawRadians": 0.0, "origin": "derived-open-space" },
          "provenance": { "source": "UW2", "origin": "LEV.ARK", "tiles": 1, "objects": 0 }
        }
        """;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuLevelContent.Read(Encoding.UTF8.GetBytes(manifest), "content pack 'abyssrpg.level-1'"));
        Assert.Contains("UW1", error.Message, StringComparison.Ordinal);
    }
}
