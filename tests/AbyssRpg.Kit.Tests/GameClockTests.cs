using AbyssRpg.Kit.Time;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class GameClockTests
{
    [Fact]
    public void Advance_accumulates_including_rest_spans()
    {
        var clock = new GameClock();
        clock.Advance(100);
        clock.Advance(8 * 60 * 60); // an eight-hour sleep expressed in ticks by the caller
        Assert.Equal((ulong)(100 + 8 * 60 * 60), clock.ElapsedTicks);
    }

    [Fact]
    public void Advance_overflow_is_an_error_not_a_wrap()
    {
        var clock = new GameClock(ulong.MaxValue);
        Assert.Throws<OverflowException>(() => clock.Advance(1));
    }

    [Fact]
    public void Snapshot_round_trips()
    {
        var clock = new GameClock();
        clock.Advance(12345);
        GameClockSnapshot snapshot = clock.Capture();
        var restored = new GameClock();
        restored.Restore(snapshot);
        Assert.Equal((ulong)12345, restored.ElapsedTicks);
    }
}
