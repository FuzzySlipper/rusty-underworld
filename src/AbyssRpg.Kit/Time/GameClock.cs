namespace AbyssRpg.Kit.Time;

/// <summary>
/// One product game-time counter advanced only inside Engine-admitted updates
/// or explicit rest operations. Ticks are opaque: the ruleset defines what a
/// tick means (e.g. ticks per game minute). No wall clock, no thread,
/// no second loop — callers pass elapsed ticks explicitly.
/// </summary>
public sealed class GameClock
{
    public ulong ElapsedTicks { get; private set; }

    public GameClock(ulong startTicks = 0)
    {
        ElapsedTicks = startTicks;
    }

    /// <summary>Advances the clock, including long rest/sleep spans.</summary>
    public void Advance(ulong ticks)
    {
        ElapsedTicks = checked(ElapsedTicks + ticks);
    }

    public GameClockSnapshot Capture() => new(ElapsedTicks);

    public void Restore(GameClockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ElapsedTicks = snapshot.ElapsedTicks;
    }
}

/// <summary>Save-capture record for the clock. The Host persists it (UW-T25).</summary>
public sealed record GameClockSnapshot(ulong ElapsedTicks);
