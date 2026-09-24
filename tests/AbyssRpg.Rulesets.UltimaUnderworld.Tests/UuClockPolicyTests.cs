using AbyssRpg.Rulesets.UltimaUnderworld.Time;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuClockPolicyTests
{
    [Fact]
    public void Minute_day_and_minute_of_day_follow_the_single_divisor()
    {
        ulong ticks = UuClockPolicy.TicksPerGameMinute * (1440UL * 2 + 61);
        Assert.Equal(1440UL * 2 + 61, UuClockPolicy.GameMinutes(ticks));
        Assert.Equal(2UL, UuClockPolicy.DayCount(ticks));
        Assert.Equal(61UL, UuClockPolicy.MinuteOfDay(ticks));
    }

    [Fact]
    public void Zero_is_the_start_of_day_zero()
    {
        Assert.Equal(0UL, UuClockPolicy.DayCount(0));
        Assert.Equal(0UL, UuClockPolicy.MinuteOfDay(0));
    }
}
