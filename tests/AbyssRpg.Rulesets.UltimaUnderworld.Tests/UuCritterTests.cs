using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Ai;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Critters;
using AbyssRpg.Rulesets.UltimaUnderworld.Identity;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCritterTests
{
    private static readonly UuCritterFactory.CritterDefinition Goblin = new(
        ItemId: 64, AvgHp: 12, Strength: 14, Dexterity: 12, Intelligence: 6, Speed: 3, CorpseIndex: 2);

    [Fact]
    public void Retreat_triggers_on_crit_below_third()
    {
        Assert.True(UuCritterPolicy.ShouldRetreat(3, 12, receivedCrit: true));
        Assert.False(UuCritterPolicy.ShouldRetreat(4, 12, receivedCrit: true));
        Assert.False(UuCritterPolicy.ShouldRetreat(1, 12, receivedCrit: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuCritterPolicy.ShouldRetreat(1, 0, true));
    }

    [Fact]
    public void State_decision_orders_dead_retreat_blind_range()
    {
        Assert.Equal(PursuitState.Dead, UuCritterPolicy.DecideState(true, true, dead: true, retreating: false));
        Assert.Equal(PursuitState.Retreat, UuCritterPolicy.DecideState(true, true, dead: false, retreating: true));
        Assert.Equal(PursuitState.Idle, UuCritterPolicy.DecideState(false, false, dead: false, retreating: false));
        Assert.Equal(PursuitState.Attack, UuCritterPolicy.DecideState(true, true, dead: false, retreating: false));
        Assert.Equal(PursuitState.Chase, UuCritterPolicy.DecideState(true, false, dead: false, retreating: false));

        Assert.Equal(UuCritterPolicy.VerticalKind.Flier, UuCritterPolicy.LocomotionKind(false, true));
        Assert.Equal(UuCritterPolicy.VerticalKind.Swimmer, UuCritterPolicy.LocomotionKind(true, false));
        Assert.Equal(UuCritterPolicy.VerticalKind.Walker, UuCritterPolicy.LocomotionKind(false, false));

        Assert.Equal(0, UuCritterPolicy.CorpseItemId(0));
        Assert.Equal(0xC2, UuCritterPolicy.CorpseItemId(2));
        UuCritterPolicy.DefaultSenses.Validate();
    }

    [Fact]
    public void Factory_builds_a_tracked_critter_with_memory()
    {
        using var actors = new ActorsState();
        var identities = UuIdentityPolicy.NewGameActorAllocator();
        ActorState critter = UuCritterFactory.CreateCritter(
            actors, identities, new ActorPose(new WorldPoint(1, 0, 2), 0f), Goblin);

        Assert.NotNull(critter.Actor.Get<PursuitMemoryComponent>());
        Assert.Equal(PursuitState.Idle, critter.Actor.Get<PursuitMemoryComponent>().State);
        Assert.Equal(12.0, critter.Actor.Get<Rusty.Engine.Mechanics.StatsComponent>()
            .GetTrack(Rusty.Engine.Mechanics.TrackId.Parse("abyss.defeat")).Maximum.Value);
        Assert.True(actors.TryGet(2, out _)); // first dynamic actor id
    }
}
