using System.Text.Json;
using Xunit;

namespace UltimaUnderworld.Import.Tests;

/// <summary>
/// The authored bundle is what the product entry resolves at launch, so its
/// shape is checked here: descriptor grammar, payload paths that exist, and the
/// operator-produced level pack, which is generated rather than committed.
/// </summary>
public sealed class BundleDeclarationTests
{
    private static string ContentRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "content", "abyss", "bundles", "stygian-abyss.bundle.json")))
            dir = Directory.GetParent(dir)?.FullName!;
        return Path.Combine(dir!, "content");
    }

    private static JsonDocument Read(string root, string relative) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relative)));

    [Fact]
    public void Stygian_bundle_declares_resolvable_packs_and_tuning()
    {
        string root = ContentRoot();
        using JsonDocument bundle = Read(root, "abyss/bundles/stygian-abyss.bundle.json");
        Assert.Equal("abyssrpg.game-bundle", bundle.RootElement.GetProperty("kind").GetString());
        Assert.Equal("abyssrpg.stygian-abyss", bundle.RootElement.GetProperty("id").GetString());
        Assert.Equal("abyssrpg.ultima-underworld", bundle.RootElement.GetProperty("ruleset").GetString());

        List<string> authored = [];
        foreach (JsonElement pack in bundle.RootElement.GetProperty("contentPacks").EnumerateArray())
        {
            string id = pack.GetProperty("id").GetString()!;
            // Level, object-table and item-catalog packs are operator-produced
            // from the game's own data, so they are generated into the imports tree
            // rather than committed; the descriptor is found wherever the
            // resolver would find it under the content root.
            if (id.StartsWith("abyssrpg.level-", StringComparison.Ordinal)
                || id == "abyssrpg.object-tables"
                || id == "abyssrpg.item-catalog")
            {
                string generated = Directory
                    .EnumerateFiles(Path.Combine(root, "abyss", "imports"), $"{id}.pack.json", SearchOption.AllDirectories)
                    .FirstOrDefault() ?? "";
                Assert.True(
                    generated.Length > 0 || !Directory.Exists(Path.Combine(root, "abyss", "imports")),
                    $"A generated pack must carry its descriptor: {id}");
                if (generated.Length > 0)
                {
                    using JsonDocument generatedDescriptor = JsonDocument.Parse(File.ReadAllText(generated));
                    string generatedPayload = generatedDescriptor.RootElement.GetProperty("payload").GetString()!;
                    Assert.True(
                        File.Exists(Path.Combine(root, generatedPayload)),
                        $"Missing generated payload {generatedPayload}.");
                }

                continue;
            }

            authored.Add(id);
            using JsonDocument descriptor = Read(root, $"abyss/packs/{id}.pack.json");
            Assert.Equal("abyssrpg.content-pack", descriptor.RootElement.GetProperty("kind").GetString());
            Assert.Equal(id, descriptor.RootElement.GetProperty("id").GetString());
            Assert.Equal(
                "abyssrpg.ultima-underworld",
                descriptor.RootElement.GetProperty("ruleset").GetString());
            string payload = descriptor.RootElement.GetProperty("payload").GetString()!;
            Assert.True(File.Exists(Path.Combine(root, payload)), $"Missing payload {payload}.");
        }

        Assert.Contains("abyssrpg.classes", authored);
        using JsonDocument tuning = Read(root, "abyss/tuning/abyssrpg.stygian-default.tuning.json");
        Assert.Equal("abyssrpg.tuning-profile", tuning.RootElement.GetProperty("kind").GetString());
        Assert.Equal("abyssrpg.stygian-default", tuning.RootElement.GetProperty("id").GetString());
        string tuningPayload = tuning.RootElement.GetProperty("payload").GetString()!;
        using JsonDocument profile = Read(root, tuningPayload);
        Assert.Equal(255, profile.RootElement.GetProperty("clockTicksPerSecond").GetInt32());
        Assert.Equal("default", profile.RootElement.GetProperty("movement").GetString());
    }

    [Fact]
    public void Authored_pack_payloads_carry_their_ids()
    {
        string root = ContentRoot();
        foreach (string name in new[] { "avatar-options", "classes", "starting-kit" })
        {
            using JsonDocument payload = Read(root, $"abyss/content-packs/{name}.json");
            Assert.Equal($"abyssrpg.{name}", payload.RootElement.GetProperty("id").GetString());
        }

        using JsonDocument classes = Read(root, "abyss/content-packs/classes.json");
        Assert.Equal(8, classes.RootElement.GetProperty("classes").GetArrayLength());
        using JsonDocument options = Read(root, "abyss/content-packs/avatar-options.json");
        Assert.Equal(8, options.RootElement.GetProperty("classes").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(options.RootElement.GetProperty("defaultName").GetString()));
    }
}
