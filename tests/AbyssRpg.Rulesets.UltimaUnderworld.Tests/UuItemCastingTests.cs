using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuItemCastingTests
{
    [Fact]
    public void Charges_spend_and_worn_holds()
    {
        Assert.Equal(2, UuItemCasting.SpendCharge(3));
        Assert.Equal(0, UuItemCasting.SpendCharge(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuItemCasting.SpendCharge(0));
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

        // Maxed lore crits except on the unluckiest rolls (30 + d - 3 >= 29 fails only for d in {0,1}).
        int named = Enumerable.Range(0, 200).Count(i => UuLorePolicy.NamesEnchantment(30, new Random(i)));
        Assert.InRange(named, 180, 200);
        // Untrained lore never crits (d - 3 peaks at 27 < 29): names nothing.
        for (int i = 0; i < 20; i++)
            Assert.False(UuLorePolicy.NamesEnchantment(0, new Random(i)));
    }
}
