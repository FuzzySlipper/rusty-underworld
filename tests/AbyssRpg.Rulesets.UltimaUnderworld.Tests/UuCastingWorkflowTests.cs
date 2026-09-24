using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCastingWorkflowTests
{
    [Fact]
    public void Stability_rolls_follow_classes()
    {
        var rng = new Random(2);
        Assert.Equal(1, UuSpellStability.RollStability(UuSpellStability.StableClass, rng));
        Assert.InRange(UuSpellStability.RollStability(UuSpellStability.UnstableHighClass, rng), 3, 72);
        Assert.InRange(UuSpellStability.RollStability(UuSpellStability.UnstableMidClass, rng), 2, 16);
        Assert.InRange(UuSpellStability.RollStability(UuSpellStability.UnstableLowClass, rng), 2, 6);
        Assert.Equal(0, UuSpellStability.RollStability(99, rng));
    }

    [Fact]
    public void Maintained_gate_replaces_then_evicts()
    {
        var maintained = new UuMaintainedSpells();
        Assert.Null(maintained.Admit(1, "IS", 3));
        Assert.Null(maintained.Admit(2, "VIL", 9));
        Assert.Null(maintained.Admit(3, "HP", 15));

        // Same spell replaces itself.
        var same = maintained.Admit(2, "VIL", 9);
        Assert.Equal(2, same!.SpellId);
        Assert.Equal(3, maintained.Spells.Count);

        // Stronger similar (IVS outranks IS) replaces the weaker kin.
        var weaker = maintained.Admit(4, "IVS", 12);
        Assert.Equal(1, weaker!.SpellId);
        Assert.DoesNotContain(maintained.Spells, s => s.SpellId == 1);

        // Full with no kin: lowest cost (cost 9, spell 2) evicted.
        var evicted = maintained.Admit(5, "KM", 21);
        Assert.Equal(9, evicted!.Cost);
        Assert.True(maintained.Dismiss(5));
        Assert.False(maintained.Dismiss(99));
    }

    [Fact]
    public void Workflow_applies_or_primes_with_expiry()
    {
        var rng = new Random(4);
        ulong duration = UuCastingWorkflow.DurationTicks(10.0, 255.0, rng);
        Assert.InRange(duration, (ulong)(0.8 * 10 * 255), (ulong)(1.1 * 10 * 255) + 1);

        var (primed, noEffect) = UuCastingWorkflow.Apply(true, 7, 1000, 1);
        Assert.Equal(UuCastingWorkflow.WorkflowResult.PrimedForAim, primed);
        Assert.Null(noEffect);

        var (applied, effect) = UuCastingWorkflow.Apply(false, 7, 1000, 5);
        Assert.Equal(UuCastingWorkflow.WorkflowResult.Applied, applied);
        Assert.False(UuCastingWorkflow.IsExpired(effect!, 999));
        Assert.True(UuCastingWorkflow.IsExpired(effect!, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuCastingWorkflow.DurationTicks(0, 255, rng));
    }
}
