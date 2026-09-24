using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuSpellCatalogTests
{
    [Fact]
    public void Catalog_covers_circles_1_to_4_with_raw_costs()
    {
        Assert.Equal(23, UuSpellCatalog.Circles1To4.Count);
        // Listed-circle anomalies keep raw costs (donor-code decision).
        Assert.Equal(6, UuSpellCatalog.FindByRunes("UP")!.Cost);
        Assert.Equal(12, UuSpellCatalog.FindByRunes("IS")!.Cost);
        Assert.Equal(24, UuSpellCatalog.FindByRunes("AS")!.Cost);
        Assert.Equal(9, UuSpellCatalog.FindByRunes("YP")!.Cost);
        Assert.Null(UuSpellCatalog.FindByRunes("ZZZ"));
    }

    [Fact]
    public void Damage_spells_aim_and_heals_scale()
    {
        Assert.True(UuSpellCatalog.FindByRunes("OJ")!.NeedsAim);
        Assert.True(UuSpellCatalog.FindByRunes("OG")!.NeedsAim);
        Assert.False(UuSpellCatalog.FindByRunes("IL")!.NeedsAim);

        Assert.Equal(5, UuSpellEffects.HealAmount("IBM", 20));
        Assert.Equal(25, UuSpellEffects.HealAmount("IM", 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuSpellEffects.HealAmount("OJ", 20));
    }

    [Fact]
    public void Maintained_families_admit_through_the_gate()
    {
        var maintained = new UuMaintainedSpells();
        UuSpellCatalog.SpellEntry light = UuSpellCatalog.FindByRunes("IL")!;
        UuSpellCatalog.SpellEntry protection = UuSpellCatalog.FindByRunes("BIS")!;

        Assert.Null(UuSpellEffects.AdmitMaintained(maintained, light, 1));
        Assert.Null(UuSpellEffects.AdmitMaintained(maintained, protection, 2));
        Assert.Equal(1500.0, light.Duration);
        Assert.Equal(90.0, protection.Duration);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UuSpellEffects.AdmitMaintained(maintained, UuSpellCatalog.FindByRunes("OJ")!, 3));
    }
}
