using AbyssRpg.Kit;

namespace AbyssRpg.Host;

/// <summary>
/// The one product entry, declared once: identity, title, content and UI
/// roots, lifecycle intents, and the built-in bundle this product opens.
/// The Host selects the built-in UW ruleset and default bundle only here;
/// it never interprets UW rules or reads game data.
/// </summary>
public sealed record AbyssProductEntry(
    string Id,
    string Title,
    string ContentRoot,
    string UiRoot,
    IReadOnlyList<string> LifecycleIntents,
    GameBundleId DefaultBundle)
{
    public static AbyssProductEntry Default { get; } = new(
        Id: "abyssrpg.product",
        Title: "AbyssRpg: The Stygian Abyss",
        ContentRoot: "content",
        UiRoot: "ui",
        LifecycleIntents: ["abyss.lifecycle.start", "abyss.lifecycle.pause", "abyss.lifecycle.resume", "abyss.lifecycle.stop"],
        DefaultBundle: BuiltInRulesets.DefaultBundle);
}
