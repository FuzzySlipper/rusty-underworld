using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuItemCastingTests
{
    [Fact]
    public void Charges_spend_wands_crack_on_the_dregs_and_worn_holds()
    {
        Assert.Equal(2, UuItemCasting.SpendCharge(3));
        Assert.Equal(0, UuItemCasting.SpendCharge(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuItemCasting.SpendCharge(0));

        // The dregs cast fires at zero, then the wand cracks.
        Assert.Equal((2, false), UuItemCasting.FireWand(3));
        Assert.Equal((0, false), UuItemCasting.FireWand(1));
        Assert.Equal((0, true), UuItemCasting.FireWand(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuItemCasting.FireWand(-1));

        Assert.True(UuItemCasting.WornEffectActive(true));
        Assert.False(UuItemCasting.WornEffectActive(false));
    }

    [Fact]
    public void Lore_names_only_on_crit_with_potion_allowlist()
    {
        Assert.True(UuLorePolicy.IsEnchantablePotion(187));
        Assert.True(UuLorePolicy.IsEnchantablePotion(188));
        Assert.False(UuLorePolicy.IsEnchantablePotion(186));
        Assert.False(UuLorePolicy.IsEnchantablePotion(189));

        // Maxed lore crits on 24/31 rolls (30 + d - 8 >= 29 ⟺ d >= 7).
        int named = Enumerable.Range(0, 200).Count(i => UuLorePolicy.NamesEnchantment(30, new Random(i)));
        Assert.InRange(named, 135, 175);
        // Untrained lore never crits (d - 3 peaks at 27 < 29): names nothing.
        for (int i = 0; i < 20; i++)
            Assert.False(UuLorePolicy.NamesEnchantment(0, new Random(i)));
    }
}
