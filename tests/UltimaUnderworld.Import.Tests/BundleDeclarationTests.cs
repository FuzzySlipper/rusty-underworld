using System.Text.Json;
using Xunit;

namespace UltimaUnderworld.Import.Tests;

public sealed class BundleDeclarationTests
{
    private static string ContentRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "content", "abyss", "bundles", "stygian-abyss.json")))
            dir = Directory.GetParent(dir)?.FullName!;
        return Path.Combine(dir!, "content", "abyss");
    }

    [Fact]
    public void Stygian_bundle_declares_packs_that_exist()
    {
        string root = ContentRoot();
        using JsonDocument bundle = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "bundles", "stygian-abyss.json")));
        Assert.Equal("abyssrpg.stygian-abyss", bundle.RootElement.GetProperty("id").GetString());
        Assert.Equal("abyssrpg.ultima-underworld", bundle.RootElement.GetProperty("ruleset").GetString());

        foreach (JsonElement pack in bundle.RootElement.GetProperty("contentPacks").EnumerateArray())
        {
            string id = pack.GetString()!;
            string file = id.Replace("abyssrpg.", "") + ".json";
            // Level packs are operator-generated; authored packs must exist.
            if (id == "abyssrpg.level-1") continue;
            Assert.True(
                File.Exists(Path.Combine(root, "content-packs", file)),
                $"Missing content pack {file}.");
        }

        using JsonDocument tuning = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "content-packs", "stygian-default-tuning.json")));
        Assert.Equal(255, tuning.RootElement.GetProperty("clockTicksPerSecond").GetInt32());
    }
}
