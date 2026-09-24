using AbyssRpg.Kit.Time;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// One continuous dungeon session: a single Engine session spans all nine
/// levels (session-per-dungeon, never per-level), and level travel swaps the
/// admitted level state while advancing the game clock by the edge cost.
/// Unloaded levels keep their deltas; Engine content replacement
/// (ReplaceContent) and presentation plug in at admission with UW-T02.
/// </summary>
public sealed class UuDungeonSession
{
    private readonly Dictionary<int, UuLevelState> _levels = [];
    private readonly GameClock _clock;

    public int CurrentLevel { get; private set; }

    public UuDungeonSession(GameClock clock, UuLevelState firstLevel)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(firstLevel);
        _clock = clock;
        _levels[firstLevel.LevelNumber] = firstLevel;
        CurrentLevel = firstLevel.LevelNumber;
    }

    public UuLevelState Current => _levels[CurrentLevel];

    /// <summary>Admit a level state (fresh or delta-restored) without traveling.</summary>
    public void Admit(UuLevelState level)
    {
        ArgumentNullException.ThrowIfNull(level);
        _levels[level.LevelNumber] = level;
    }

    public UuLevelDelta Unload(int level)
    {
        if (!_levels.TryGetValue(level, out UuLevelState? state))
            throw new ArgumentOutOfRangeException(nameof(level), $"Level {level} is not admitted.");
        return state.CaptureDelta();
    }

    /// <summary>Travel to an admitted level, paying the edge cost in clock ticks.</summary>
    public void TravelTo(int level, ulong costTicks)
    {
        if (!_levels.TryGetValue(level, out _))
            throw new ArgumentOutOfRangeException(nameof(level), $"Level {level} is not admitted.");
        _clock.Advance(costTicks);
        CurrentLevel = level;
    }
}
