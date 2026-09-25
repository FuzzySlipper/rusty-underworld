using AbyssRpg.Kit;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;

namespace AbyssRpg.Host;

/// <summary>
/// Explicit built-in selection seam: the one compiled ruleset, its identity, and
/// the default bundle. This is the only Host type that names the concrete
/// ruleset, so replacing or adding a compiled ruleset changes exactly one
/// catalog; everything downstream holds <see cref="IGameRuleset"/> and
/// <see cref="IGameSession"/>.
/// </summary>
public static class BuiltInRulesets
{
    public static RulesetId UltimaUnderworld { get; } = UuGameRuleset.RulesetIdentity;

    public static GameBundleId DefaultBundle { get; } = new("abyssrpg.stygian-abyss");

    public static BuiltInSelection Resolve(RulesetId id)
    {
        if (!id.Equals(UltimaUnderworld))
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown built-in ruleset.");
        return new BuiltInSelection(id, DefaultBundle);
    }

    /// <summary>The compiled ruleset instance this product selects.</summary>
    public static IGameRuleset CreateRuleset(RulesetId id)
    {
        _ = Resolve(id);
        return new UuGameRuleset();
    }
}

public sealed record BuiltInSelection(RulesetId Ruleset, GameBundleId Bundle);
