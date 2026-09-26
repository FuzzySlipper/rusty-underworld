using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCastingHostingTests
{
    private static UuCastingHosting Shelf(params int[] runes)
    {
        var hosting = new UuCastingHosting(new Random(11));
        foreach (int rune in runes)
        {
            hosting.CollectRune(rune);
            hosting.ShelfRune(rune);
        }

        return hosting;
    }

    [Fact]
    public void Cast_flows_gate_roll_admit_and_upkeep()
    {
        // Shelf letters I+L: find their indices first.
        int i = FindRune('I');
        int l = FindRune('L');

        // Empty shelf never passes the gate.
        UuCastingHosting.CastOutcome unknown = Shelf().AttemptCast(10, 30, 30, false, 0, 255, 1);
        Assert.Equal(UuCastGates.GateResult.NotASpell, unknown.Gate);

        // Archmage light: casts, admits to maintained, effect expires with its slot.
        var hosting = Shelf(i, l);
        UuCastingHosting.CastOutcome light = hosting.AttemptCast(10, 30, 30, false, 0, 255, 1);
        Assert.Equal(UuCastGates.GateResult.Cast, light.Gate);
        Assert.False(light.Backfired);
        Assert.False(light.PrimedForAim);
        Assert.Equal(3, light.ManaCost);
        Assert.NotNull(light.Effect);
        Assert.Single(hosting.Panel().Maintained);
        hosting.Upkeep(light.Effect.ExpiresAtTicks);
        Assert.Empty(hosting.Panel().Effects);
        Assert.Empty(hosting.Panel().Maintained);

        // Zero-duration maintained (Curse) holds until dismissed, never throws.
        var curse = Shelf(FindRune('A'), FindRune('S'));
        UuCastingHosting.CastOutcome as_ = curse.AttemptCast(15, 30, 30, false, 0, 255, 7);
        Assert.Equal(UuCastGates.GateResult.Cast, as_.Gate);
        Assert.NotNull(as_.Effect);
        curse.Upkeep(ulong.MaxValue - 1);
        Assert.Single(curse.Panel().Maintained);

        // Aimed instant: primed signal, no effect, no maintained.
        var arrow = Shelf(FindRune('O'), FindRune('J'));
        UuCastingHosting.CastOutcome primed = arrow.AttemptCast(10, 30, 30, false, 0, 255, 2);
        Assert.Equal(UuCastGates.GateResult.Cast, primed.Gate);
        Assert.True(primed.PrimedForAim);
        Assert.Null(primed.Effect);
        Assert.Empty(arrow.Panel().Maintained);
    }

    private static int FindRune(char letter)
    {
        for (int index = 0; index < 24; index++)
            if (UuRuneCatalog.Letter(index) == letter) return index;
        throw new InvalidOperationException($"No rune '{letter}'.");
    }

    [Fact]
    public void A_maintained_light_spell_is_what_the_light_radius_asks_about()
    {
        // The runes are found the way the other casting tests find them rather than
        // hardcoded, and the cast roll is retried until one lands.
        UuCastingHosting hosting = Shelf(FindRune('I'), FindRune('L'));
        Assert.False(hosting.MaintainsFamily(UuSpellCatalog.Family.Light));

        for (int attempt = 0; attempt < 50
            && hosting.AttemptCast(10, 30, 30, false, 0, 255, 1).Gate != UuCastGates.GateResult.Cast; attempt++)
        {
            hosting = Shelf(FindRune('I'), FindRune('L'));
        }

        Assert.True(hosting.MaintainsFamily(UuSpellCatalog.Family.Light));
        Assert.False(hosting.MaintainsFamily(UuSpellCatalog.Family.Damage));
    }

    [Fact]
    public void A_maintained_light_ends_when_its_own_duration_runs_out()
    {
        // The maintained list is the casting owner's, and a spell it holds ends when
        // the effect carrying it expires: the light a player cast does not burn
        // forever, and nothing else has to time it.
        UuCastingHosting hosting = Shelf(FindRune('I'), FindRune('L'));
        for (int attempt = 0; attempt < 50
            && hosting.AttemptCast(10, 30, 30, false, 0, 255, 1).Gate != UuCastGates.GateResult.Cast; attempt++)
        {
            hosting = Shelf(FindRune('I'), FindRune('L'));
        }

        Assert.True(hosting.MaintainsFamily(UuSpellCatalog.Family.Light));
        UuSpellCatalog.SpellEntry light = UuSpellCatalog.FindByRunes("IL")!;
        // It holds a deadline of its own rather than relying on an effect to end it.
        Assert.True(
            hosting.Panel().Maintained[0].ExpiresAtTicks > 0,
            "the held spell carries the deadline its duration gives it");

        // The deadline is the spell's own duration with the game's spread (0.8 to 1.1
        // of the table value, at the 255 ticks a second this cast was made with), so
        // short of the spread's floor it holds and past its ceiling it does not.
        double ticks = light.Duration * 255d;
        hosting.Upkeep((ulong)(0.7 * ticks));
        Assert.True(hosting.MaintainsFamily(UuSpellCatalog.Family.Light));
        hosting.Upkeep((ulong)(1.2 * ticks));
        Assert.False(hosting.MaintainsFamily(UuSpellCatalog.Family.Light));
    }
}
