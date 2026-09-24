using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Objects;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuObjectLifecycleTests
{
    private static UuObjectInstance Arrow(int quantity, ulong id = 5000) => new(
        new DurableIdentityReference(DurableIdentityKind.Item, id), ItemId: 16, Quality: 0, Quantity: quantity, IsQuant: true);

    [Fact]
    public void Combine_merges_stacks_and_split_divides_them()
    {
        UuObjectInstance? merged = UuObjectVerbs.Combine(Arrow(5), Arrow(7));
        Assert.NotNull(merged);
        Assert.Equal(12, merged!.Quantity);
        Assert.Equal(5000UL, merged.Identity.Value); // survivor keeps its identity

        Assert.Null(UuObjectVerbs.Combine(Arrow(5), Arrow(7) with { Quality = 1 }));
        Assert.Null(UuObjectVerbs.Combine(Arrow(5), Arrow(7) with { ItemId = 17 }));
        var sword = new UuObjectInstance(
            new DurableIdentityReference(DurableIdentityKind.Item, 6000), ItemId: 0, Quality: 0, Quantity: 1, IsQuant: false);
        Assert.Null(UuObjectVerbs.Combine(sword, sword));

        (UuObjectInstance kept, UuObjectInstance taken) = UuObjectVerbs.Split(Arrow(12), 5);
        Assert.Equal(7, kept.Quantity);
        Assert.Equal(5, taken.Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => UuObjectVerbs.Split(Arrow(12), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuObjectVerbs.Split(Arrow(12), 12));
        Assert.Throws<InvalidOperationException>(() => UuObjectVerbs.Split(sword, 1));
    }

    [Fact]
    public void Npcs_are_not_takeable_and_takeables_are()
    {
        var npc = new UuObjectInstance(
            new DurableIdentityReference(DurableIdentityKind.Actor, 2), ItemId: 64, Quality: 0, Quantity: 1, IsQuant: false);
        Assert.Equal(1, npc.MajorClass);
        Assert.False(npc.CanBeTaken);
        Assert.True(Arrow(1).CanBeTaken);
    }

    [Fact]
    public void Drop_and_lift_round_trip_through_level_state()
    {
        var level = new AdmittedLevel(1, [], []);
        var state = new UuLevelState(level);
        var placement = new DroppedPlacement(3, 4, ItemId: 16, Quality: 0, Quantity: 5, IdentityValue: 5000);
        state.Drop(placement);
        Assert.Contains(placement, state.Dropped);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Drop(placement with { TileX = 64 }));

        UuLevelDelta delta = state.CaptureDelta();
        var restored = new UuLevelState(level);
        restored.ApplyDelta(delta);
        Assert.Contains(placement, restored.Dropped);
        Assert.True(restored.Lift(placement));
        Assert.Empty(restored.Dropped);
    }
}
