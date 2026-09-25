using AbyssRpg.Kit;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

/// <summary>
/// The Host-owned pieces that do not need a live session: the declared entry,
/// the save envelope, and the Engine persistence guard. Session behavior is
/// covered by <see cref="OrdinaryCompositionTests"/>.
/// </summary>
public sealed class HostEntryTests
{
    [Fact]
    public void Entry_is_declared_once_with_selection_and_projection()
    {
        Assert.Equal("abyssrpg.product", AbyssProductEntry.Default.Id);
        Assert.Equal(["abyss.lifecycle.start", "abyss.lifecycle.pause", "abyss.lifecycle.resume", "abyss.lifecycle.stop"],
            AbyssProductEntry.Default.LifecycleIntents);
        Assert.Equal(["abyss.action.quicksave", "abyss.action.journey-onward", "abyss.action.respawn"],
            AbyssProductEntry.Default.ActionIntents);
        Assert.Equal("abyss.hud", AbyssProductEntry.Default.UiProjectionStream);
        Assert.Equal("abyss.ui.snapshot.v1", AbyssProductEntry.Default.UiProjectionContract);
        Assert.Equal(7, AbyssProductEntry.Default.DeclaredIntents.Count);
        Assert.Equal(
            AbyssProductEntry.Default.DeclaredIntents.Count,
            AbyssProductEntry.Default.DeclaredIntents.Distinct(StringComparer.Ordinal).Count());
        BuiltInSelection selection = BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld);
        Assert.Equal(BuiltInRulesets.DefaultBundle, selection.Bundle);
        Assert.Equal(BuiltInRulesets.DefaultBundle, AbyssProductEntry.Default.DefaultBundle);
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInRulesets.Resolve(new RulesetId("nope")));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInRulesets.CreateRuleset(new RulesetId("nope")));
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
    public void Save_envelope_round_trips_over_engine_persistence()
    {
        IEngineContext engine = EngineContextFake.Create(new InMemoryPersistenceService());
        using var store = new AbyssSaveStore(engine, "abyssrpg.saves");

        var saved = new AbyssSaveEnvelope(
            "abyssrpg.ultima-underworld", [1, 2, 3], new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
        store.Save("slot0", saved);
        var loaded = store.Load("slot0");
        Assert.True(loaded.Present);
        Assert.Equal("abyssrpg.ultima-underworld", loaded.State!.Ruleset);
        Assert.Equal([1, 2, 3], loaded.State.Payload.ToArray());
        Assert.Equal(saved.SavedAtUtc, loaded.State.SavedAtUtc);

        var missing = store.Load("nope");
        Assert.False(missing.Present);

        store.Delete("slot0");
        Assert.False(store.Load("slot0").Present);

        Assert.Throws<ArgumentException>(() => store.Save("bad", new AbyssSaveEnvelope("", [1])));
        Assert.Throws<ArgumentException>(() => store.Save("bad", new AbyssSaveEnvelope("r", [])));
    }

    [Fact]
    public void Created_saves_stamp_the_write_time()
    {
        AbyssSaveEnvelope created = AbyssSaveEnvelope.Create([1]);
        Assert.Equal("abyssrpg.ultima-underworld", created.Ruleset);
        Assert.NotEqual(default, created.SavedAtUtc);
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
}
