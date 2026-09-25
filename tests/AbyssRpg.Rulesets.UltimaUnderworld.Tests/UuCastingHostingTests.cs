using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCastingHostingTests
{
    [Fact]
    public void Cast_flows_gate_roll_admit_and_upkeep()
    {
        var hosting = new UuCastingHosting(new Random(11));

        // Unknown runes never pass the gate.
        UuCastingHosting.CastOutcome unknown = hosting.AttemptCast("ZZ", 10, 30, 30, false, 0, 255, 1);
        Assert.Equal(UuCastGates.GateResult.NotASpell, unknown.Gate);

        // Broke apprentice: level gate holds (circle 3 needs level 5+).
        UuCastingHosting.CastOutcome low = hosting.AttemptCast("OG", 1, 30, 0, false, 0, 255, 1);
        Assert.Equal(UuCastGates.GateResult.LevelTooLow, low.Gate);

        // Archmage light: casts, admits to maintained, effect expires.
        UuCastingHosting.CastOutcome light = hosting.AttemptCast("IL", 10, 30, 30, false, 0, 255, 1);
        Assert.Equal(UuCastGates.GateResult.Cast, light.Gate);
        Assert.False(light.Backfired);
        Assert.NotNull(light.Effect);
        Assert.Single(hosting.Panel().Maintained);
        hosting.Upkeep(light.Effect.ExpiresAtTicks);
        Assert.Empty(hosting.Panel().Effects);

        // Instant damage: no effect instance, no maintained.
        UuCastingHosting.CastOutcome arrow = hosting.AttemptCast("OJ", 10, 30, 30, false, 0, 255, 2);
        Assert.Equal(UuCastGates.GateResult.Cast, arrow.Gate);
        Assert.Null(arrow.Effect);
        Assert.Single(hosting.Panel().Maintained);
    }
}
