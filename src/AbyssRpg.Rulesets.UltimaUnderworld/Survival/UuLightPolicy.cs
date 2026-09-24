namespace AbyssRpg.Rulesets.UltimaUnderworld.Survival;

/// <summary>
/// Light burn-down: each quality step lasts 300 * duration / 64 seconds
/// (donor LightSource step formula, durations table-driven per light kind).
/// Torch and candle rules (hand light radius, shoulder slots, dousing on
/// sleep) ride with equipment and rest; this policy owns only the burn.
/// </summary>
public static class UuLightPolicy
{
    /// <summary>Seconds per quality step for a table duration.</summary>
    public static double SecondsPerQualityStep(int duration) => duration <= 0
        ? throw new ArgumentOutOfRangeException(nameof(duration))
        : 300.0 * duration / 64.0;

    /// <summary>Advance a burn countdown; returns true when a quality step is spent.</summary>
    public static bool TickBurn(ref double countdown, double seconds, int duration)
    {
        if (!double.IsFinite(seconds) || seconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        countdown -= seconds;
        if (countdown > 0d) return false;
        countdown += SecondsPerQualityStep(duration);
        return true;
    }
}
