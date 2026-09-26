namespace AbyssRpg.Rulesets.UltimaUnderworld.Time;

/// <summary>
/// UW1 clock policy over the Kit tick counter: 15,300 ticks per game minute
/// (donor game_time = ClockValue / 0x3BC4), 1,440 minutes per day.
/// The donor's pocket-watch derivations use slightly different divisors
/// (0x3C00, 0xE1000); this policy uses the single gameplay divisor until a
/// pocket-watch task verifies the original.
/// UW1 clock state only; UW2 timer triggers are out of scope (see
/// docs/gameplay-design.md, donor-area dispositions).
/// </summary>
public static class UuClockPolicy
{
    public const ulong TicksPerGameMinute = 15300;
    public const ulong GameMinutesPerDay = 1440;

    /// <summary>
    /// What falling a level costs the dungeon clock: a game minute. The donor's own
    /// number for a fall was not established, so this is our tuning rather than a
    /// faithful value, and it lives here because the clock is what it spends.
    /// </summary>
    public static ulong FallCostTicks => TicksPerGameMinute;

    public static ulong GameMinutes(ulong ticks) => ticks / TicksPerGameMinute;

    public static ulong DayCount(ulong ticks) => GameMinutes(ticks) / GameMinutesPerDay;

    public static ulong MinuteOfDay(ulong ticks) => GameMinutes(ticks) % GameMinutesPerDay;
}
