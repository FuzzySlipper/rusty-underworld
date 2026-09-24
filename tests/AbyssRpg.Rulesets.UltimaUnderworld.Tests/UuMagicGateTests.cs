using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuMagicGateTests
{
    [Fact]
    public void Rune_letters_follow_the_shelf_encoding()
    {
        Assert.Equal(24, UuRuneCatalog.Names.Count);
        Assert.Equal('A', UuRuneCatalog.Letter(0));
        Assert.Equal('Y', UuRuneCatalog.Letter(23)); // the X-becomes-Y quirk
        Assert.Equal(23, UuRuneCatalog.Index('Y'));
        Assert.Equal("KM", UuRuneCatalog.SpellLetters([10, 12])); // index 10 -> K, 12 -> M
        Assert.Throws<ArgumentOutOfRangeException>(() => UuRuneCatalog.Letter(24));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuRuneCatalog.Index('X')); // no X rune
        Assert.Throws<ArgumentOutOfRangeException>(() => UuRuneCatalog.Index('Z'));
    }

    [Fact]
    public void Shelf_tracks_ownership_and_placement()
    {
        var shelf = new UuRuneShelf();
        shelf.AddRunestone(10);
        shelf.AddRunestone(12);
        Assert.True(shelf.OwnsAll([10, 12]));
        Assert.False(shelf.OwnsAll([10, 11]));
        shelf.Place(10);
        shelf.Place(12);
        Assert.Equal([10, 12], shelf.Shelf);
        shelf.AddRunestone(0);
        shelf.Place(0);
        shelf.AddRunestone(1);
        shelf.Place(1); // shelf full at 3: silently refuses the 4th
        Assert.Equal([10, 12, 0], shelf.Shelf);
        Assert.Throws<InvalidOperationException>(() => shelf.Place(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => shelf.AddRunestone(24));
        shelf.Clear();
        Assert.Empty(shelf.Shelf);
    }

    [Fact]
    public void Cast_gates_fire_in_donor_order()
    {
        Assert.Equal(3, UuCastGates.ManaCost(1));
        Assert.Equal(24, UuCastGates.ManaCost(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuCastGates.ManaCost(0));
        Assert.Equal(1, UuCastGates.CircleForCost(3));
        Assert.Equal(8, UuCastGates.CircleForCost(24));
        Assert.False(UuCastGates.CanAttemptCast(1));
        Assert.True(UuCastGates.CanAttemptCast(2));

        Assert.Equal(UuCastGates.GateResult.NotASpell, UuCastGates.CheckGates(false, 10, 1, 99, false));
        Assert.Equal(UuCastGates.GateResult.LevelTooLow, UuCastGates.CheckGates(true, 1, 2, 99, false));
        Assert.Equal(UuCastGates.GateResult.Cast, UuCastGates.CheckGates(true, 3, 2, 99, false)); // (3+1)/2 = 2
        Assert.Equal(UuCastGates.GateResult.NotEnoughMana, UuCastGates.CheckGates(true, 10, 2, 5, false));
        Assert.Equal(UuCastGates.GateResult.StillDelayed, UuCastGates.CheckGates(true, 10, 2, 99, true));

        Assert.Equal(UuCastGates.GateResult.Backfire,
            UuCastGates.ResolveRoll(AbyssRpg.Rulesets.UltimaUnderworld.Combat.UuStrikeResolution.StrikeResult.CritFail));
        Assert.Equal(UuCastGates.GateResult.Fizzle,
            UuCastGates.ResolveRoll(AbyssRpg.Rulesets.UltimaUnderworld.Combat.UuStrikeResolution.StrikeResult.Fail));

        // A trained caster clears a first-circle roll; backfire stays in 1-5.
        var rng = new Random(5);
        for (int i = 0; i < 50; i++)
            Assert.InRange(UuCastGates.RollBackfire(rng), 1, 5);
        Assert.Equal(UuCastGates.GateResult.Cast, UuCastGates.RollCast(30, 1, new Random(1)));

        Assert.Equal("Casting", UuSkillCatalog.Name(UuSkillCatalog.CastingIndex));
        Assert.Equal(20, UuSkillCatalog.Names.Count);
    }
}
