using AbyssRpg.Kit;

namespace AbyssRpg.Host;

/// <summary>
/// Explicit built-in selection seam: the one compiled ruleset and the
/// default bundle. Content packs and tuning resolve later (UW-T05); this
/// seam names only identities, never content.
/// </summary>
public static class BuiltInRulesets
{
    public static RulesetId UltimaUnderworld { get; } = new("abyssrpg.ultima-underworld");

    public static GameBundleId DefaultBundle { get; } = new("abyssrpg.stygian-abyss");

    public static BuiltInSelection Resolve(RulesetId id)
    {
        if (!id.Equals(UltimaUnderworld))
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown built-in ruleset.");
        return new BuiltInSelection(id, DefaultBundle);
    }
}

public sealed record BuiltInSelection(RulesetId Ruleset, GameBundleId Bundle);
