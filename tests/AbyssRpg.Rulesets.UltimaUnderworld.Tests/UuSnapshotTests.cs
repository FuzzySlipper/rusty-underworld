using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuSnapshotTests
{
    internal static UuSession TestSession(long seed = 1234)
    {
        var tables = new CreationTables([new(20, 16, 12, 12)], [1, 7, 2, 11, 12]);
        var flow = new UuCreationFlow(tables, new Random(7));
        flow.SubmitGender(0);
        flow.SubmitHandedness(1);
        flow.SubmitClass(0);
        flow.OfferSkillChoices();
        flow.SubmitSkillChoice(0);
        flow.FinishSkills();
        flow.SubmitPortrait(1);
        flow.SubmitDifficulty(0);
        flow.SubmitName("Avatar");
        var choices = flow.Confirm(true)!;
        var vitals = UuVitalsPolicy.Recalculate(choices.Attributes[0], 1, 0, choices.Attributes[2]);
        return UuSession.NewGame(
            choices, vitals, new AdmittedLevel(1, [], []),
            new ActorPose(new WorldPoint(0, 0, 0), 0f),
            worldSeed: seed);
    }

    [Fact]
    public void Snapshot_round_trips_through_json()
    {
        using var session = TestSession();
        session.Clock.Advance(1000);
        session.Survival.Hunger = 50;
        session.Quests.Set(0, 2);
        session.Automap[1] = new AbyssRpg.Kit.Knowledge.AutomapPage();
        session.Automap[1].Reveal(5, 5);
        session.Notes.Place(1, "trap", 5, 5);
        session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current = 20.0;

        UuSessionSnapshot snapshot = session.CaptureSnapshot(new AvatarPoseDto(1, 2, 3, 0.5f));
        byte[] bytes = UuSessionSnapshotCodec.Encode(snapshot);
        UuSessionSnapshot revived = UuSessionSnapshotCodec.Decode(bytes);

        Assert.Equal((ulong)1000, revived.ClockTicks);
        Assert.Equal(1, revived.Level);
        Assert.Equal(20.0, revived.Hp);
        Assert.Equal(50, revived.Hunger);
        Assert.Equal(1234, revived.WorldSeed);
        Assert.Equal(1, revived.Anchor.Level);
        Assert.Single(revived.Automap);
        Assert.NotEmpty(revived.ActorIdentities);
        Assert.NotEmpty(revived.ItemIdentities);
        Assert.Equal(64, revived.QuestVars.Length);
        Assert.Equal(2, revived.QuestVars[0].Value);
        Assert.Single(revived.Notes);
        Assert.Equal((1f, 2f, 3f), (revived.AvatarPose.X, revived.AvatarPose.Y, revived.AvatarPose.Z));
    }

    [Fact]
    public void Restore_returns_mutated_state()
    {
        using var session = TestSession();
        UuSessionSnapshot snapshot = session.CaptureSnapshot(new AvatarPoseDto(0, 0, 0, 0f));

        session.Clock.Advance(500);
        session.Survival.Hunger = 90;
        session.Quests.Set(5, 9);
        session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current = 5.0;

        ulong before = session.ActorIdentities.NextIdentity(AbyssRpg.Kit.World.DurableIdentityKind.Actor);
        session.ActorIdentities.Allocate(AbyssRpg.Kit.World.DurableIdentityKind.Actor);
        session.RestoreSnapshot(snapshot);
        Assert.Equal(before, session.ActorIdentities.NextIdentity(AbyssRpg.Kit.World.DurableIdentityKind.Actor));
        Assert.Equal(
            snapshot.ItemIdentities.Single(k => k.Kind == AbyssRpg.Kit.World.DurableIdentityKind.Item).NextIdentity,
            session.ItemIdentities.NextIdentity(AbyssRpg.Kit.World.DurableIdentityKind.Item));
        Assert.Equal((ulong)0, session.Clock.ElapsedTicks);
        Assert.Equal(0, session.Survival.Hunger);
        Assert.Equal(0, session.Quests.Get(5)); // full restore rolls back quest writes
        Assert.Equal(34.0, session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current);

        // Double restore is idempotent (notes replace, not append).
        session.Notes.Place(1, "extra", 0, 0);
        session.RestoreSnapshot(snapshot);
        session.RestoreSnapshot(snapshot);
        Assert.Empty(session.Notes.Notes(1));
    }
}
