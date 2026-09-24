using AbyssRpg.Rulesets.UltimaUnderworld.Social;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuBarterTests
{
    [Fact]
    public void Generous_offers_close_and_tired_traders_walk()
    {
        var rng = new Random(7);
        // NPC asks 10, player offers 30: relative -20 - charm -> Yes.
        var (result, patience) = UuBarterPolicy.Offer(10, 30, 5, 30, 0, 3, rng);
        Assert.Equal(UuBarterPolicy.TradeResult.Accepted, result);
        Assert.Equal(0, patience);

        // Insulting offer with an impatient trader: Bad then Tired.
        var (bad, p1) = UuBarterPolicy.Offer(100, 5, 0, 0, 0, 1, new Random(1));
        Assert.Equal(UuBarterPolicy.TradeResult.TraderTired, bad);
        Assert.Equal(1, p1);
    }

    [Fact]
    public void Appraisal_bands_and_variance()
    {
        Assert.Equal(0, UuBarterPolicy.AppraisalVariance(30));
        Assert.Equal(10, UuBarterPolicy.AppraisalVariance(0));
        Assert.Equal(-1, UuBarterPolicy.DealBand(-5));
        Assert.Equal(0, UuBarterPolicy.DealBand(4));
        Assert.Equal(2, UuBarterPolicy.DealBand(10));
    }

    [Fact]
    public void Demands_yield_or_turn_hostile()
    {
        // Strong hero vs weak trader: yields with one attitude step.
        var (yielded, shift) = UuBarterPolicy.Demand(12, 10, 30, 30, 20, 2, 1, 10, 10);
        Assert.Equal(UuBarterPolicy.DemandResult.Yielded, yielded);
        Assert.Equal(-1, shift);

        // Weak hero vs strong trader: hostile refusal.
        var (refused, none) = UuBarterPolicy.Demand(0, 1, 5, 30, 200, 2, 8, 40, 40);
        Assert.Equal(UuBarterPolicy.DemandResult.RefusedHostile, refused);
        Assert.Equal(0, none);
    }
}
