using AbyssRpg.Kit;

namespace AbyssRpg.Host;

/// <summary>
/// The one product entry, declared once: identity, title, content and UI roots,
/// the lifecycle and player-action intents the DOM may claim, the one UI
/// projection stream/contract pair the shell is bound to, and the built-in
/// bundle this product opens. The Host selects the built-in UW ruleset and
/// default bundle only here; it never interprets UW rules or reads game data.
/// The csproj declares the same intent names and projection pair to the Engine,
/// because the staged manifest is what the browser shell receives.
/// </summary>
public sealed record AbyssProductEntry(
    string Id,
    string Title,
    string ContentRoot,
    string UiRoot,
    IReadOnlyList<string> LifecycleIntents,
    IReadOnlyList<string> ActionIntents,
    string UiProjectionStream,
    string UiProjectionContract,
    GameBundleId DefaultBundle)
{
    public static AbyssProductEntry Default { get; } = new(
        Id: "abyssrpg.product",
        Title: "AbyssRpg: The Stygian Abyss",
        ContentRoot: "content",
        UiRoot: "ui",
        LifecycleIntents:
        [
            "abyss.lifecycle.start",
            "abyss.lifecycle.pause",
            "abyss.lifecycle.resume",
            "abyss.lifecycle.stop",
        ],
        ActionIntents:
        [
            "abyss.action.quicksave",
            "abyss.action.journey-onward",
            "abyss.action.respawn",
        ],
        UiProjectionStream: "abyss.hud",
        UiProjectionContract: "abyss.ui.snapshot.v1",
        DefaultBundle: BuiltInRulesets.DefaultBundle);

    /// <summary>Every direct intent the DOM may claim, lifecycle and player actions together.</summary>
    public IReadOnlyList<string> DeclaredIntents => [.. LifecycleIntents, .. ActionIntents];
}
