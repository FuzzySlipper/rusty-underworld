using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using Rusty.Engine;
using Rusty.Engine.Persistence;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class UpdateWiringTests
{
    [Fact]
    public void Long_session_ticks_survival_autosaves_and_selects_music()
    {
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service);
        using var product = new AbyssProduct(engine, BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        using var session = HostEntryTests.TestSession();
        product.AttachSession(session);
        using var spatial = new AbyssSpatialSession(
            engine,
            new SpatialContentArtifact("spatial/test", new ContentSha256(1, 2, 3, 4), 1),
            new SpatialTuning(.5d, 8, 8, 1),
            new PlayerControlState(new WorldPoint(0, 10, 0), 0f, 0f));
        product.AttachSpatialSession(spatial);
        product.Start();

        for (ulong step = 2; step < 4000; step++)
            product.Update(HostEntryTests.SixtyHzUpdate(step));
        Assert.True(session.Clock.ElapsedTicks > 0);
        Assert.True(session.Survival.HungerTimer < 60.0); // survival ticked
        Assert.Equal(UuMusicPolicy.Situation.Exploring, product.CurrentMusicSituation);

        // A starving avatar takes defeat damage and the music turns to Death.
        session.Survival.Hunger = AbyssRpg.Rulesets.UltimaUnderworld.Survival.UuSurvivalState.HungerCap;
        session.Avatar.Stats.GetTrack(
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current = 1.0;
        for (ulong step = 4000; step < 8000; step++)
            product.Update(HostEntryTests.SixtyHzUpdate(step));
        Assert.Equal(UuMusicPolicy.Situation.Death, product.CurrentMusicSituation);

        // Replacing the session re-arms the autosave guard.
        using var fresh = HostEntryTests.TestSession();
        product.AttachSession(fresh);
        product.Update(HostEntryTests.SixtyHzUpdate(8000));
        ProductStateLoad<AbyssSaveEnvelope> reloaded =
            new AbyssSaveStore(engine, "abyssrpg.saves").Load(AbyssSaveSlots.AutosaveKey(1));
        Assert.True(reloaded.Present);

        // Autosave fired for level 1 with a decodable snapshot.
        ProductStateLoad<AbyssSaveEnvelope> loaded =
            new AbyssSaveStore(engine, "abyssrpg.saves").Load(AbyssSaveSlots.AutosaveKey(1));
        Assert.True(loaded.Present);
        var snapshot = AbyssRpg.Rulesets.UltimaUnderworld.Session.UuSessionSnapshotCodec.Decode(
            loaded.State!.Payload);
        Assert.Equal(1, snapshot.Level);
    }
}
