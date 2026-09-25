using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuSessionTests
{
    private static readonly CreationTables Tables = new(
        [new(20, 16, 12, 12)],
        [1, 7, 2, 11, 12]);

    private static UuCreationFlow.CreationResult Choices()
    {
        var flow = new UuCreationFlow(Tables, new Random(7));
        flow.SubmitGender(0);
        flow.SubmitHandedness(1);
        flow.SubmitClass(0);
        flow.OfferSkillChoices();
        flow.SubmitSkillChoice(0);
        flow.FinishSkills();
        flow.SubmitPortrait(1);
        flow.SubmitDifficulty(0);
        flow.SubmitName("Avatar");
        return flow.Confirm(true)!;
    }

    private static AdmittedLevel Level(int number) => new(
        number,
        [new AdmittedTile(0, 0, 1, 5), new AdmittedTile(1, 0, 1, 0)],
        [new AdmittedObject(0, 0, 0), new AdmittedObject(5, 44, 0)]);

    [Fact]
    public void A_snapshot_round_trip_keeps_the_levels_own_changes()
    {
        UuCreationFlow.CreationResult choices = Choices();
        var vitals = UuVitalsPolicy.Recalculate(choices.Attributes[0], 1, 0, choices.Attributes[2]);
        AdmittedLevel level = Level(1);
        using var session = UuSession.NewGame(
            choices, vitals, level, new ActorPose(new WorldPoint(0, 0, 0), 0f));

        // Play changes the level: a door opens, a placed object is taken.
        session.Dungeon.Current.SetDoor(1, 0, true);
        session.Dungeon.Current.RemoveObject(5);
        UuSessionSnapshot snapshot = session.CaptureSnapshot(new AvatarPoseDto(0, 0, 0, 0));

        using var restored = UuSession.NewGame(
            choices, vitals, level, new ActorPose(new WorldPoint(0, 0, 0), 0f));
        Assert.False(restored.Dungeon.Current.OpenedDoors.Contains((1, 0)));
        Assert.True(restored.Dungeon.Current.IsLive(5));

        restored.RestoreSnapshot(snapshot);
        Assert.Contains((1, 0), restored.Dungeon.Current.OpenedDoors);
        Assert.False(restored.Dungeon.Current.IsLive(5));
        // The entity of an object the save says is gone is not left standing.
        Assert.False(restored.Directory.TryResolve(
            AbyssRpg.Rulesets.UltimaUnderworld.Identity.UuIdentityPolicy.LevelObjectIdentity(1, 5), out _));
    }

    [Fact]
    public void New_game_travel_and_save_round_trip()
    {
        UuCreationFlow.CreationResult choices = Choices();
        var vitals = UuVitalsPolicy.Recalculate(choices.Attributes[0], 1, 0, choices.Attributes[2]);
        using var session = UuSession.NewGame(
            choices, vitals, Level(1), new ActorPose(new WorldPoint(0, 0, 0), 0f));

        Assert.Equal(1, session.Dungeon.CurrentLevel);
        Assert.Equal(1L, session.Avatar.DurableId);
        Assert.Equal(choices.Attributes[0], session.Avatar.Stats.GetStat(Rusty.Engine.Mechanics.StatId.Parse("abyss.strength")).BaseValue);

        session.Dungeon.Current.RemoveObject(5);
        session.TravelTo(Level(2), costTicks: 100);
        Assert.Equal(2, session.Dungeon.CurrentLevel);
        Assert.Equal((ulong)100, session.Clock.ElapsedTicks);

        // Level-1 entities were abandoned with the travel.
        Assert.False(session.Directory.TryResolve(
            new AbyssRpg.Kit.World.DurableIdentityReference(AbyssRpg.Kit.World.DurableIdentityKind.Item, 1029), out _));

        // Re-travel replays the stored delta: the removal persists.
        session.TravelTo(Level(1), costTicks: 50);
        Assert.Equal(1, session.Dungeon.CurrentLevel);
        Assert.False(session.Dungeon.Current.IsLive(5));
        Assert.Equal((ulong)150, session.Clock.ElapsedTicks);

        Session.UuSessionSnapshot snap = session.CaptureSnapshot(new Session.AvatarPoseDto(0, 0, 0, 0f));
        Assert.Equal((ulong)150, snap.ClockTicks);
        Assert.Contains(snap.Deltas, d => d.LevelNumber == 1 && d.RemovedObjects.Contains(5));
        Assert.NotEmpty(snap.ActorIdentities);
        Assert.NotEmpty(snap.ItemIdentities);
        Assert.Throws<ObjectDisposedException>(() => { session.Dispose(); session.CaptureSnapshot(new Session.AvatarPoseDto(0, 0, 0, 0f)); });
    }
}
