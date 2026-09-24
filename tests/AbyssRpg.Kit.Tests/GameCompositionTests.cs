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
}
