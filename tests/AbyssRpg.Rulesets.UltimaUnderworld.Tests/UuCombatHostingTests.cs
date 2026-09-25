using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Rusty.Engine.Mechanics;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCombatHostingTests
{
    private static StatsComponent Tracks(double hp)
    {
        var stats = new StatsComponent();
        stats.AddTrack(UuAvatarFactory.DefeatTrack, new Track(hp, hp));
        return stats;
    }

    [Fact]
    public void Charge_builds_release_hits_and_defeat_routes()
    {
        var combat = new UuCombatHosting();
        Assert.Equal(0f, combat.ChargeFraction);
        combat.Hold(0.5);
        Assert.Equal(0.5f, combat.ChargeFraction);
        combat.Hold(1.0);
        Assert.Equal(1f, combat.ChargeFraction);

        var attacker = Tracks(30);
        var target = Tracks(5);
        UuCombatHosting.StrikeOutcome outcome = combat.Release(attacker, target, 30, 5, 6, new Random(4));
        Assert.True(outcome.Hit);
        Assert.True(outcome.Damage >= 1);
        Assert.Equal(0f, combat.ChargeFraction); // release resets
        if (outcome.TargetDefeated)
            Assert.Equal(UuDeathPolicy.Respawn.AtAnchor, UuCombatHosting.RouteDefeat(false));

        // Tap (no hold) still lands at least 1 on a hit.
        var weak = Tracks(50);
        UuCombatHosting.StrikeOutcome tap = combat.Release(attacker, weak, 30, 5, 6, new Random(4));
        Assert.True(tap.Hit);
        Assert.True(tap.Damage >= 1);

        // Wild miss deals nothing.
        UuCombatHosting.StrikeOutcome miss = combat.Release(attacker, Tracks(50), 0, 30, 6, new Random(9));
        Assert.False(miss.Hit);
        Assert.Equal(0, miss.Damage);
    }
}
