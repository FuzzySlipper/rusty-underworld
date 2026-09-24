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
    }
}
