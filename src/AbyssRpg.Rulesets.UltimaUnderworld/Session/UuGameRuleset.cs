using AbyssRpg.Kit;
using Rusty.Engine.Debugging;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>
/// The compiled Ultima Underworld ruleset as the Host's built-in seam sees it:
/// one identity and one session factory. Creating a session reads the resolved
/// bundle's content; the Host never interprets it.
/// </summary>
public sealed class UuGameRuleset : IGameRuleset, ISaveableGameRuleset, IDebuggableGameRuleset
{
    private readonly UuSessionDebugModule _debug = new();

    public static RulesetId RulesetIdentity { get; } = new("abyssrpg.ultima-underworld");

    public RulesetId Id => RulesetIdentity;

    public IDebugCommandModule DebugModule => _debug;

    public IGameSession CreateSession(GameSessionContext context) => Attach(UuGameSession.Create(context));

    public IGameSession CreateSession(GameSessionContext context, RulesetSavePayload saved)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(saved);
        if (!saved.Ruleset.Equals(RulesetIdentity))
            throw new InvalidOperationException($"Save belongs to ruleset '{saved.Ruleset.Value}'.");
        return Attach(UuGameSession.Create(context, saved));
    }

    // The module outlives sessions, so it is re-pointed rather than replaced.
    private IGameSession Attach(UuGameSession session)
    {
        _debug.Attach(session);
        return session;
    }
}
