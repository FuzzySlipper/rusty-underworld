using System.Text;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class GameCompositionTests
{
    private static ProductContentFile File(string path, string json) =>
        new(Encoding.UTF8.GetBytes(path), Encoding.UTF8.GetBytes(json));

    private static ProductContent Composition(params ProductContentFile[] files) => new(files);

    private const string Provenance = """ ,"provenance":{"source":"UW1","origin":"UW/DATA/LEV.ARK","sha256":"ab"} """;

    private static ProductContentFile[] BundleWithPack(
        string bundleKind = "abyssrpg.game-bundle",
        string packKind = "abyssrpg.content-pack",
        string tuningKind = "abyssrpg.tuning-profile",
        string provenance = Provenance) =>
    [
        File("bundles/test.bundle.json",
            "{\"kind\":\"" + bundleKind + "\",\"id\":\"test.bundle\",\"ruleset\":\"test.ruleset\",\"contentPacks\":[{\"id\":\"test.pack\"}],\"tuning\":{\"id\":\"test.tuning\"}}"),
        File("packs/test.pack.json",
            "{\"kind\":\"" + packKind + "\",\"id\":\"test.pack\",\"ruleset\":\"test.ruleset\",\"dependencies\":[],\"payload\":\"payload/pack.bin\"" + provenance + "}"),
        File("tuning/test.tuning.json",
            "{\"kind\":\"" + tuningKind + "\",\"id\":\"test.tuning\",\"ruleset\":\"test.ruleset\",\"payload\":\"payload/tuning.bin\"}"),
        File("payload/pack.bin", """{"pack":true}"""),
        File("payload/tuning.bin", """{"tuning":true}"""),
    ];

    [Fact]
    public void Resolves_bundle_pack_and_tuning_with_provenance()
    {
        GameCompositionResolution resolution = GameCompositionResolver.Resolve(
            Composition(BundleWithPack()), new GameBundleId("test.bundle"));

        Assert.True(resolution.IsResolved);
        ResolvedGameComposition composition = resolution.RequireComposition();
        Assert.Equal("test.ruleset", composition.Ruleset.Value);
        ContentPack pack = composition.RequireContentPack(new ContentPackId("test.pack"));
        Assert.Equal("""{"pack":true}""", Encoding.UTF8.GetString(pack.Payload.Span));
        Assert.Equal("UW1", pack.Provenance?.Source);
        Assert.Equal("UW/DATA/LEV.ARK", pack.Provenance?.Origin);
        Assert.Equal("""{"tuning":true}""", Encoding.UTF8.GetString(composition.Tuning.Payload.Span));
    }

    [Fact]
    public void A_bundle_admits_the_packs_under_its_imported_roots_without_naming_them()
    {
        string Bundle(string roots) =>
            "{\"kind\":\"abyssrpg.game-bundle\",\"id\":\"test.bundle\",\"ruleset\":\"test.ruleset\",\"contentPacks\":[{\"id\":\"test.pack\"}],\"tuning\":{\"id\":\"test.tuning\"}" + roots + "}";
        ProductContentFile Imported(string path, string id, string ruleset) =>
            File(path, "{\"kind\":\"abyssrpg.content-pack\",\"id\":\"" + id + "\",\"ruleset\":\"" + ruleset + "\",\"dependencies\":[],\"payload\":\"payload/pack.bin\"}");
        ProductContentFile[] authored = [.. BundleWithPack().Where(file => !Encoding.UTF8.GetString(file.Path.Span).StartsWith("bundles/", StringComparison.Ordinal))];
        ProductContentFile[] imports =
        [
            Imported("imports/level-2/level-2.pack.json", "test.level-2", "test.ruleset"),
            Imported("imports/level-1/level-1.pack.json", "test.level-1", "test.ruleset"),
            Imported("imports/other/other.pack.json", "other.level-1", "other.ruleset"),
            Imported("elsewhere/stray.pack.json", "test.stray", "test.ruleset"),
        ];

        ResolvedGameComposition withImports = GameCompositionResolver.Resolve(
            Composition([File("bundles/test.bundle.json", Bundle(",\"importedPackRoots\":[\"imports\"]")), .. authored, .. imports]),
            new GameBundleId("test.bundle")).RequireComposition();
        // Named packs first, then the imported root's own packs in id order; another
        // ruleset's import and a pack outside the root are not admitted.
        Assert.Equal(
            ["test.pack", "test.level-1", "test.level-2"],
            withImports.ContentPacks.Select(pack => pack.Id.Value));
        Assert.Equal(["imports"], withImports.Bundle.ImportedPackRoots);

        // A checkout without the import resolves to what it has.
        ResolvedGameComposition withoutImports = GameCompositionResolver.Resolve(
            Composition([File("bundles/test.bundle.json", Bundle(",\"importedPackRoots\":[\"imports\"]")), .. authored]),
            new GameBundleId("test.bundle")).RequireComposition();
        Assert.Equal(["test.pack"], withoutImports.ContentPacks.Select(pack => pack.Id.Value));

        GameCompositionResolution invalid = GameCompositionResolver.Resolve(
            Composition([File("bundles/test.bundle.json", Bundle(",\"importedPackRoots\":[\"../imports\"]")), .. authored]),
            new GameBundleId("test.bundle"));
        Assert.False(invalid.IsResolved);
    }

    [Fact]
    public void Provenance_is_optional()
    {
        GameCompositionResolution resolution = GameCompositionResolver.Resolve(
            Composition(BundleWithPack(provenance: "")), new GameBundleId("test.bundle"));

        Assert.True(resolution.IsResolved);
        Assert.Null(resolution.RequireComposition().RequireContentPack(new ContentPackId("test.pack")).Provenance);
    }

    [Fact]
    public void Rejects_unknown_kinds_and_missing_packs()
    {
        GameCompositionResolution wrongKind = GameCompositionResolver.Resolve(
            Composition(BundleWithPack(bundleKind: "worldrpg.game-bundle")), new GameBundleId("test.bundle"));
        Assert.False(wrongKind.IsResolved);

        GameCompositionResolution missing = GameCompositionResolver.Resolve(
            Composition(BundleWithPack()[..3]), new GameBundleId("test.bundle"));
        Assert.False(missing.IsResolved);

        GameCompositionResolution absent = GameCompositionResolver.Resolve(
            Composition(BundleWithPack()), new GameBundleId("nope"));
        Assert.False(absent.IsResolved);
        Assert.Throws<InvalidOperationException>(() => absent.RequireComposition());
    }

    [Fact]
    public void Rejects_ruleset_mismatch()
    {
        ProductContentFile[] files = BundleWithPack();
        files[1] = File("packs/test.pack.json",
            "{\"kind\":\"abyssrpg.content-pack\",\"id\":\"test.pack\",\"ruleset\":\"other\",\"dependencies\":[],\"payload\":\"payload/pack.bin\"}");
        Assert.False(GameCompositionResolver.Resolve(Composition(files), new GameBundleId("test.bundle")).IsResolved);
    }

    [Fact]
    public void Orders_dependencies_before_dependents_and_rejects_cycles()
    {
        ProductContentFile[] files = BundleWithPack();
        files[0] = File("bundles/test.bundle.json",
            "{\"kind\":\"abyssrpg.game-bundle\",\"id\":\"test.bundle\",\"ruleset\":\"test.ruleset\",\"contentPacks\":[{\"id\":\"test.top\"}],\"tuning\":{\"id\":\"test.tuning\"}}");
        files[1] = File("packs/test.pack.json",
            "{\"kind\":\"abyssrpg.content-pack\",\"id\":\"test.pack\",\"ruleset\":\"test.ruleset\",\"dependencies\":[],\"payload\":\"payload/pack.bin\"}");
        var top = File("packs/test.top.pack.json",
            "{\"kind\":\"abyssrpg.content-pack\",\"id\":\"test.top\",\"ruleset\":\"test.ruleset\",\"dependencies\":[{\"id\":\"test.pack\"}],\"payload\":\"payload/pack.bin\"}");
        GameCompositionResolution ordered = GameCompositionResolver.Resolve(
            Composition([.. files, top]), new GameBundleId("test.bundle"));
        Assert.True(ordered.IsResolved);
        Assert.Equal(["test.pack", "test.top"],
            ordered.RequireComposition().ContentPacks.Select(p => p.Id.Value));

        // Cycle: top depends on pack, pack depends on top.
        files[1] = File("packs/test.pack.json",
            "{\"kind\":\"abyssrpg.content-pack\",\"id\":\"test.pack\",\"ruleset\":\"test.ruleset\",\"dependencies\":[{\"id\":\"test.top\"}],\"payload\":\"payload/pack.bin\"}");
        Assert.False(GameCompositionResolver.Resolve(
            Composition([.. files, top]), new GameBundleId("test.bundle")).IsResolved);
    }
}
