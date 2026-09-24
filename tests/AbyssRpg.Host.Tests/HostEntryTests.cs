using AbyssRpg.Kit;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class HostEntryTests
{
    [Fact]
    public void Entry_is_declared_once_with_selection()
    {
        Assert.Equal("abyssrpg.product", AbyssProductEntry.Default.Id);
        Assert.Equal(["abyss.lifecycle.start", "abyss.lifecycle.pause", "abyss.lifecycle.resume", "abyss.lifecycle.stop"],
            AbyssProductEntry.Default.LifecycleIntents);
        BuiltInSelection selection = BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld);
        Assert.Equal(BuiltInRulesets.DefaultBundle, selection.Bundle);
        Assert.Equal(BuiltInRulesets.DefaultBundle, AbyssProductEntry.Default.DefaultBundle);
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInRulesets.Resolve(new RulesetId("nope")));
    }

    [Fact]
    public void Lifecycle_starts_pauses_resumes_and_stops()
    {
        var lifecycle = new AbyssProductLifecycle();
        Assert.Equal(AbyssLifecycleMode.Stopped, lifecycle.Mode);
        Assert.Throws<InvalidOperationException>(() => lifecycle.Pause());
        Assert.Throws<InvalidOperationException>(() => lifecycle.Resume());

        lifecycle.Start();
        Assert.Equal(AbyssLifecycleMode.Running, lifecycle.Mode);
        Assert.Throws<InvalidOperationException>(() => lifecycle.Start());

        lifecycle.Pause();
        Assert.Equal(AbyssLifecycleMode.Paused, lifecycle.Mode);
        lifecycle.Resume();
        Assert.Equal(AbyssLifecycleMode.Running, lifecycle.Mode);
        lifecycle.Stop();
        Assert.Equal(AbyssLifecycleMode.Stopped, lifecycle.Mode);
    }

    [Fact]
    public void Admitted_updates_advance_the_session_clock_and_pause_admits_nothing()
    {
        IEngineContext engine = EngineContextFake.Create(new InMemoryPersistenceService());
        using var product = new AbyssProduct(engine, BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));

        // No session yet: updates are admitted but move nothing.
        product.Start();
        Assert.Equal(ProductUpdateResult.None, product.Update(SixtyHzUpdate(1)));

        using var session = TestSession();
        product.AttachSession(session);
        for (ulong step = 2; step < 62; step++) product.Update(SixtyHzUpdate(step));
        Assert.Equal((ulong)255, session.Clock.ElapsedTicks); // 60 updates at 255 ticks/s

        product.Pause();
        product.Update(SixtyHzUpdate(62));
        Assert.Equal((ulong)255, session.Clock.ElapsedTicks);
        product.Resume();
        product.Update(SixtyHzUpdate(63));
        Assert.True(session.Clock.ElapsedTicks > 255);
    }

    [Fact]
    public void Save_envelope_round_trips_over_engine_persistence()
    {
        IEngineContext engine = EngineContextFake.Create(new InMemoryPersistenceService());
        using var store = new AbyssSaveStore(engine, "abyssrpg.saves");

        var saved = new AbyssSaveEnvelope("abyssrpg.ultima-underworld", [1, 2, 3]);
        store.Save("slot0", saved);
        var loaded = store.Load("slot0");
        Assert.True(loaded.Present);
        Assert.Equal("abyssrpg.ultima-underworld", loaded.State!.Ruleset);
        Assert.Equal([1, 2, 3], loaded.State.Payload.ToArray());

        var missing = store.Load("nope");
        Assert.False(missing.Present);

        store.Delete("slot0");
        Assert.False(store.Load("slot0").Present);

        Assert.Throws<ArgumentException>(() => store.Save("bad", new AbyssSaveEnvelope("", [1])));
        Assert.Throws<ArgumentException>(() => store.Save("bad", new AbyssSaveEnvelope("r", [])));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"Ruleset\":null,\"Payload\":[]}")]
    [InlineData("{\"Ruleset\":\"\",\"Payload\":[]}")]
    public void Save_envelope_rejects_corrupt_slots_as_typed_failures(string payload)
    {
        var persistence = new InMemoryPersistenceService();
        persistence.Put("abyssrpg.saves", "slot", System.Text.Encoding.UTF8.GetBytes(payload));
        using var store = new AbyssSaveStore(EngineContextFake.Create(persistence), "abyssrpg.saves");

        Assert.Throws<AbyssSaveFormatException>(() => store.Load("slot"));
    }

    [Fact]
    public void Save_honors_the_engine_revision_guard()
    {
        var persistence = new InMemoryPersistenceService();
        using var store = new AbyssSaveStore(EngineContextFake.Create(persistence), "abyssrpg.saves");
        var saved = new AbyssSaveEnvelope("abyssrpg.ultima-underworld", [1, 2, 3]);

        var first = store.Save("slot", saved, Rusty.Engine.PersistenceRevisionGuard.Absent);
        var conflict = store.Save("slot", saved, Rusty.Engine.PersistenceRevisionGuard.Exact, first.Revision + 1);
        Assert.Equal(Rusty.Engine.PersistenceSaveOutcome.RevisionConflict, conflict.Outcome);
        Assert.Equal(first.Revision, conflict.Revision);
        Assert.Equal(first.Revision, store.Load("slot").Revision);
    }

    [Fact]
    public void Product_update_steps_locomotion_through_the_spatial_session()
    {
        var engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service);
        using var product = new AbyssProduct(engine, BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        using var session = TestSession();
        product.AttachSession(session);
        using var spatialSession = new AbyssSpatialSession(
            engine,
            new AbyssRpg.Kit.Controls.SpatialContentArtifact("spatial/test", new ContentSha256(1, 2, 3, 4), 1),
            new AbyssRpg.Kit.Controls.SpatialTuning(.5d, 8, 8, 1),
            new AbyssRpg.Kit.Controls.PlayerControlState(new AbyssRpg.Kit.Controls.WorldPoint(0, 10, 0), 0f, 0f));
        product.AttachSpatialSession(spatialSession);
        product.Start();

        // The avatar spawns mid-air: restore the continuation apex so the
        // landing reads as a 6-foot fall (spawners own this Restore).
        spatialSession.Player.Restore(
            new AbyssRpg.Kit.Controls.WorldPoint(0, 10, 0),
            FallMotion(peakY: 10f));

        for (ulong step = 1; step <= 60; step++) product.Update(SixtyHzUpdate(step));

        Assert.Equal((ulong)255, session.Clock.ElapsedTicks);
        Assert.Equal(4f, spatialSession.Player.Position!.Value.Y);

        // The 6-foot landing injured the avatar: 34 max HP minus 16.
        Assert.Equal(18.0, session.Avatar.Stats
            .GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current);
    }

    [Fact]
    public void Product_update_publishes_the_hud_snapshot()
    {
        var ui = UiDouble.Create();
        var engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service);
        using var product = new AbyssProduct(engine, BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        using var session = TestSession();
        product.AttachSession(session);
        using var spatialSession = new AbyssSpatialSession(
            engine,
            new AbyssRpg.Kit.Controls.SpatialContentArtifact("spatial/test", new ContentSha256(1, 2, 3, 4), 1),
            new AbyssRpg.Kit.Controls.SpatialTuning(.5d, 8, 8, 1),
            new AbyssRpg.Kit.Controls.PlayerControlState(new AbyssRpg.Kit.Controls.WorldPoint(0, 10, 0), 0f, 0f));
        product.AttachSpatialSession(spatialSession);
        product.Start();

        // No fall this time: apex at spawn height means no landing injury.
        spatialSession.Player.Restore(
            new AbyssRpg.Kit.Controls.WorldPoint(0, 10, 0),
            FallMotion(peakY: 4f));
        product.Update(SixtyHzUpdate(1));

        Assert.Equal(1, ui.OpenCalls); // stream opens once, lazily
        Assert.NotNull(ui.LastProjection);
        Assert.Equal(34.0, HudNumber(ui.LastProjection!.Value, "maxHp"));
        Assert.Equal(34.0, HudNumber(ui.LastProjection!.Value, "hp"));
        product.Update(SixtyHzUpdate(2));
        Assert.Equal(1, ui.OpenCalls);
    }

    private static double HudNumber(UiProjection projection, string field)
    {
        Rusty.Engine.UiValue built = projection.Value;
        Rusty.Engine.StructuredValueNode root = built.Nodes.Span[(int)built.Root];
        for (uint i = 0; i < root.ChildCount; i++)
        {
            Rusty.Engine.StructuredValueNode node = built.Nodes.Span[(int)built.Edges.Span[(int)(root.FirstEdge + i)]];
            string name = System.Text.Encoding.UTF8.GetString(
                built.Utf8.Span.Slice((int)node.KeyOffset, (int)node.KeyLen));
            if (name == field) return node.NumberValue;
        }

        throw new KeyNotFoundException($"HUD field '{field}' missing.");
    }

    private static Rusty.Engine.CharacterMotion FallMotion(float peakY) => new(
        ControlledVelocity: System.Numerics.Vector3.Zero,
        ExternalVelocity: System.Numerics.Vector3.Zero,
        Grounded: false,
        Stance: Rusty.Engine.CharacterStance.Standing,
        JumpBufferRemaining: 0f, CoyoteRemaining: 0f, LandingLockoutRemaining: 0f,
        SupportEntityPresent: false, SupportEntity: 0,
        SupportLocalAnchor: System.Numerics.Vector3.Zero,
        SupportPreviousTranslation: System.Numerics.Vector3.Zero,
        SupportPreviousRotation: System.Numerics.Quaternion.Identity,
        SupportPointVelocity: System.Numerics.Vector3.Zero,
        FallOriginY: peakY, PeakY: peakY, LastCommandSequence: 0, CollisionWorldHash: 0);

    private static ProductUpdate SixtyHzUpdate(ulong step) => new(        new ProductUpdateFacts(
            ProductUpdateMode.Realtime, ProductLifecycleState.Running,
            step, step, step, step, 60, 1, 0, 1d / 60d),
        ReadOnlySpan<ProductInputEvent>.Empty);

    private static AbyssRpg.Rulesets.UltimaUnderworld.Session.UuSession TestSession()
    {
        var tables = new AbyssRpg.Rulesets.UltimaUnderworld.Creation.CreationTables(
            [new(20, 16, 12, 12)], [1, 7, 2, 11, 12]);
        var flow = new AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuCreationFlow(tables, new Random(7));
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
        var vitals = AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuVitalsPolicy.Recalculate(
            choices.Attributes[0], 1, 0, choices.Attributes[2]);
        var level = new AbyssRpg.Rulesets.UltimaUnderworld.Dungeon.AdmittedLevel(1, [], []);
        return AbyssRpg.Rulesets.UltimaUnderworld.Session.UuSession.NewGame(
            choices, vitals, level,
            new AbyssRpg.Kit.Actors.ActorPose(new AbyssRpg.Kit.Controls.WorldPoint(0, 0, 0), 0f));
    }
}
