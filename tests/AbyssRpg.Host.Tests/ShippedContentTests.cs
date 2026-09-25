using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using Xunit;

namespace AbyssRpg.Host.Tests;

/// <summary>
/// The shipped authored content has to compose into a playable slice, which is
/// a property of the content rather than of any one system: an avatar who
/// cannot land a blow is a broken composition, not a hard fight.
/// </summary>
public sealed class ShippedContentTests
{
    private static string ContentRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "content", "abyss", "content-packs")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir!, "content");
    }

    private static UuCreationCatalog.Catalog ShippedClasses() => UuCreationCatalog.Read(
        File.ReadAllBytes(Path.Combine(ContentRoot(), "abyss", "content-packs", "avatar-options.json")),
        File.ReadAllBytes(Path.Combine(ContentRoot(), "abyss", "content-packs", "classes.json")),
        "avatar options", "classes");

    [Fact]
    public void A_shipped_class_rolls_an_attack_skill_that_can_land_a_blow()
    {
        UuCreationCatalog.Catalog catalog = ShippedClasses();
        UuCreationFlow.CreationResult choices = UuCreationCatalog.RollDefault(catalog, new Random(11));
        int attack = choices.Skills[0];
        Assert.True(attack > 0, $"the shipped classes roll attack skill {attack}");

        // The strike's to-hit roll is attack + rng(0..30) - defence, and the
        // session strikes at defence 15, so a strike needs attack of at least
        // one for any roll to reach a success.
        bool canHit = false;
        var rng = new Random(7);
        for (int i = 0; i < 200 && !canHit; i++)
        {
            canHit = UuStrikeResolution.RollToHit(attack, 15, rng)
                is UuStrikeResolution.StrikeResult.Success or UuStrikeResolution.StrikeResult.CritSuccess;
        }

        Assert.True(canHit, $"attack skill {attack} never reaches a success against defence 15");
    }

    [Fact]
    public void Every_shipped_class_starts_with_the_same_walk_so_any_choice_can_fight()
    {
        UuCreationCatalog.Catalog catalog = ShippedClasses();
        for (int classIndex = 0; classIndex < 8; classIndex++)
        {
            var defaults = catalog.Defaults with { ClassIndex = classIndex };
            UuCreationCatalog.Catalog selected = catalog with { Defaults = defaults };
            UuCreationFlow.CreationResult choices = UuCreationCatalog.RollDefault(selected, new Random(3));
            Assert.True(choices.Skills[0] > 0, $"class {classIndex} rolls attack skill {choices.Skills[0]}");
        }
    }
}
