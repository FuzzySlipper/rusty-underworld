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
        Assert.Equal(
            [
                "abyss.action.quicksave", "abyss.action.journey-onward",
                .. AbyssSaveSlots.Keys().Select((_, position) => AbyssProductEntry.SlotIntent(position)),
                "abyss.action.respawn",
            ],
            AbyssProductEntry.Default.ActionIntents);
        Assert.Equal("abyss.hud", AbyssProductEntry.Default.UiProjectionStream);
        Assert.Equal("abyss.ui.snapshot.v1", AbyssProductEntry.Default.UiProjectionContract);
        Assert.Equal(
            AbyssProductEntry.Default.LifecycleIntents.Count + 2 + AbyssSaveSlots.Keys().Count() + 1,
            AbyssProductEntry.Default.DeclaredIntents.Count);
        Assert.Equal(
            AbyssProductEntry.Default.DeclaredIntents.Count,
            AbyssProductEntry.Default.DeclaredIntents.Distinct(StringComparer.Ordinal).Count());
        BuiltInSelection selection = BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld);
        Assert.Equal(BuiltInRulesets.DefaultBundle, selection.Bundle);
        Assert.Equal(BuiltInRulesets.DefaultBundle, AbyssProductEntry.Default.DefaultBundle);
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInRulesets.Resolve(new RulesetId("nope")));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInRulesets.CreateRuleset(new RulesetId("nope")));
    }

    /// <summary>
    /// The staged manifest is the only declaration the browser shell sees, and
    /// the entry and the companion each keep their own copy. A rename in one of
    /// them would otherwise fail silently at runtime: a control that sends an
    /// undeclared intent, or a HUD that filters on a contract nothing publishes.
    /// </summary>
    [Fact]
    public void The_project_declaration_matches_the_entry()
    {
        string root = RepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "src", "AbyssRpg.Host", "AbyssRpg.Host.csproj"));
        var xml = System.Xml.Linq.XDocument.Parse(project);
        var intents = xml.Descendants()
            .Where(node => node.Name.LocalName == "RustyEngineProductInputIntent")
            .Select(node => node.Attribute("Include")?.Value ?? node.Value.Trim())
            .ToArray();
        var mappings = xml.Descendants()
            .Where(node => node.Name.LocalName == "RustyEngineProductInputMapping")
            .Select(node => node.Attribute("Intent")?.Value ?? "")
            .ToArray();
        var projections = xml.Descendants()
            .Where(node => node.Name.LocalName is "RustyEngineProductUiProjectionStream" or "RustyEngineProductUiProjectionContract")
            .ToArray();

        Assert.Equal(
            AbyssProductEntry.Default.DeclaredIntents.OrderBy(name => name, StringComparer.Ordinal),
            intents.OrderBy(name => name, StringComparer.Ordinal));
        Assert.NotEmpty(mappings);
        Assert.All(mappings, intent => Assert.Contains(intent, AbyssProductEntry.Default.DeclaredIntents));
        Assert.Contains(projections, node => node.Name.LocalName == "RustyEngineProductUiProjectionStream"
            && node.Value.Trim() == AbyssProductEntry.Default.UiProjectionStream);
        Assert.Contains(projections, node => node.Name.LocalName == "RustyEngineProductUiProjectionContract"
            && node.Value.Trim() == AbyssProductEntry.Default.UiProjectionContract);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
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
