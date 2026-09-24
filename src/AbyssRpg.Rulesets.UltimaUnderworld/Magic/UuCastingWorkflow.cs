namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Casting workflow stages: validate (T18 gates) → cost (mana spend) →
/// target (aim spells prime for a point, others apply at once) → apply
/// (effect instance with game-time expiry). Spell content (costs, durations,
/// aim set) arrives with the spell catalog (UW-T20); the workflow only
/// orders the stages. Godot's AdvanceInitialMagicRound is deliberately not
/// copied (round-bootstrap helper, not game behavior).
/// </summary>
public static class UuCastingWorkflow
{
    public enum WorkflowResult
    {
        Applied,
        PrimedForAim,
    }

    public sealed record SpellEffectInstance(int SpellId, ulong ExpiresAtTicks, int Stability);

    /// <summary>Effect duration: table seconds scaled 0.8-1.1, in clock ticks.</summary>
    public static ulong DurationTicks(double tableSeconds, double ticksPerSecond, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (!double.IsFinite(tableSeconds) || tableSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(tableSeconds));
        if (!double.IsFinite(ticksPerSecond) || ticksPerSecond <= 0d)
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        return (ulong)((0.8 + rng.NextDouble() * 0.3) * tableSeconds * ticksPerSecond);
    }

    public static bool IsExpired(SpellEffectInstance instance, ulong nowTicks) =>
        nowTicks >= instance.ExpiresAtTicks;

    public static (WorkflowResult Result, SpellEffectInstance? Effect) Apply(
        bool needsAim, int spellId, ulong expiresAtTicks, int stability) =>
        needsAim
            ? (WorkflowResult.PrimedForAim, null)
            : (WorkflowResult.Applied, new SpellEffectInstance(spellId, expiresAtTicks, stability));
}
