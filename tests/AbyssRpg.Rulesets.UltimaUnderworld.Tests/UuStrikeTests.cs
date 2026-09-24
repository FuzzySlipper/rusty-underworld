using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuStrikeTests
{
    [Fact]
    public void To_hit_ladder_matches_the_combat_roll()
    {
        // Fixed seeds pin the ladder shape, not exact draws.
        var rng = new Random(42);
        int crits = 0, fails = 0, successes = 0, critFails = 0;
        for (int i = 0; i < 2000; i++)
        {
            switch (UuStrikeResolution.RollToHit(15, 15, rng))
            {
                case UuStrikeResolution.StrikeResult.CritSuccess: crits++; break;
                case UuStrikeResolution.StrikeResult.Success: successes++; break;
                case UuStrikeResolution.StrikeResult.Fail: fails++; break;
                case UuStrikeResolution.StrikeResult.CritFail: critFails++; break;
            }
        }

        // Even roll spreads d(0-30): ~3/31 critfail, ~13/31 fail,
        // ~13/31 success, ~2/31 critsuccess.
        Assert.InRange(critFails, 100, 300);
        Assert.InRange(fails, 600, 1100);
        Assert.InRange(successes, 600, 1100);
        Assert.InRange(crits, 30, 250);
        Assert.Equal(UuStrikeResolution.StrikeResult.CritFail, UuStrikeResolution.RollToHit(0, 100, new Random(1)));
    }

    [Fact]
    public void Defence_damage_and_armour_compose()
    {
        Assert.Equal(15 + (10 / 2), UuStrikeResolution.DefenceScore(15, 10));
        Assert.Equal(2, UuStrikeResolution.DamageMaximum(0, 0));
        Assert.Equal(6 + (20 / 5), UuStrikeResolution.DamageMaximum(6, 20));

        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
            Assert.InRange(UuStrikeResolution.RollDamage(12, rng), 1, 12);
        for (int i = 0; i < 50; i++)
            Assert.InRange(UuStrikeResolution.RollCriticalDamage(12, rng), 12, 24);

        Assert.Equal(0, UuStrikeResolution.AbsorbWithArmour(3, 5));
        Assert.Equal(4, UuStrikeResolution.AbsorbWithArmour(9, 5));
        Assert.Equal(1, UuStrikeResolution.ScaleByCharge(10, 0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuStrikeResolution.RollDamage(0, rng));
    }

    [Fact]
    public void Armour_coverage_sums_per_part_with_shield()
    {
        var worn = new UuArmourCoverage.WornProtection(Head: 2, Torso: 3, Hands: 1, Legs: 2, Feet: 2, Shield: 2);
        Assert.Equal(2, UuArmourCoverage.ProtectionForPart(worn, UuBodyPart.Head));
        Assert.Equal(5, UuArmourCoverage.ProtectionForPart(worn, UuBodyPart.Torso));
        Assert.Equal(3, UuArmourCoverage.ProtectionForPart(worn, UuBodyPart.Arms));
        Assert.Equal(4, UuArmourCoverage.ProtectionForPart(worn, UuBodyPart.Legs));
    }
}
