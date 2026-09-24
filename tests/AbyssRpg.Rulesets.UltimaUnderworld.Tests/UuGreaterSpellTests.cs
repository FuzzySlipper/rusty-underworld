using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuGreaterSpellTests
{
    [Fact]
    public void Catalog_covers_circles_5_to_8()
    {
        Assert.Equal(25, UuSpellCatalog.Circles5To8.Count);
        Assert.Equal(UuSpellCatalog.Family.Heal, UuSpellCatalog.FindByRunes("AN")!.SpellFamily);
        Assert.Equal(UuSpellCatalog.Family.Damage, UuSpellCatalog.FindByRunes("FH")!.SpellFamily);
        Assert.Equal(UuSpellCatalog.Family.Light, UuSpellCatalog.FindByRunes("VIL")!.SpellFamily);
        Assert.Equal(UuSpellCatalog.Family.Protection, UuSpellCatalog.FindByRunes("IVS")!.SpellFamily);
        Assert.True(UuSpellCatalog.FindByRunes("PF")!.NeedsAim);
        Assert.False(UuSpellCatalog.FindByRunes("ACM")!.NeedsAim); // AoE, not aimed
        Assert.Equal(48, UuSpellCatalog.Circles1To4.Count + UuSpellCatalog.Circles5To8.Count);
    }

    [Fact]
    public void Greater_magnitudes_follow_the_donor()
    {
        var rng = new Random(6);
        for (int i = 0; i < 30; i++)
            Assert.InRange(UuGreaterSpellEffects.FlameWindDamage(rng), 14, 17);
        Assert.Equal(40, UuGreaterSpellEffects.GreaterHealAmount(40));
        Assert.Equal(0, UuGreaterSpellEffects.CurePoisonResult());
        Assert.Equal(3.0, UuGreaterSpellEffects.MindEffectRadiusTiles);
        Assert.Equal(2, UuGreaterSpellEffects.RevealRadiusTiles);
    }

    [Fact]
    public void Greater_maintained_admit_through_the_gate()
    {
        var maintained = new UuMaintainedSpells();
        Assert.Null(UuSpellEffects.AdmitMaintained(
            maintained, UuSpellCatalog.FindByRunes("VIL")!, 1));
        Assert.Null(UuSpellEffects.AdmitMaintained(
            maintained, UuSpellCatalog.FindByRunes("AT")!, 2));
        Assert.Equal(300.0, UuSpellCatalog.FindByRunes("VIL")!.Duration);
    }
}
