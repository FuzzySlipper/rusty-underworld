using AbyssRpg.Kit.Npc;
using AbyssRpg.Rulesets.UltimaUnderworld.Social;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuNpcTests
{
    [Fact]
    public void Presence_tracks_stations()
    {
        var presence = new NpcPresence();
        presence.Place("murgo", new NpcStation(1, 10, 12));
        Assert.Equal(new NpcStation(1, 10, 12), presence.Station("murgo"));
        Assert.True(presence.Remove("murgo"));
        Assert.Throws<KeyNotFoundException>(() => presence.Station("murgo"));
    }

    [Fact]
    public void Attitudes_shift_and_hostility_reads()
    {
        Assert.Equal(UuAttitude.Hostile, UuAttitude.Provoke(UuAttitude.Upset));
        Assert.Equal(UuAttitude.Hostile, UuAttitude.Provoke(UuAttitude.Hostile));
        Assert.Equal(UuAttitude.Friendly, UuAttitude.Soothe(UuAttitude.Friendly));
        Assert.True(UuAttitude.IsHostile(UuAttitude.Hostile));
        Assert.False(UuAttitude.IsHostile(UuAttitude.Upset));
    }

    [Fact]
    public void Schedules_hold_stations_by_hour()
    {
        var schedule = new UuNpcSchedule();
        schedule.Assign(8, "shop");
        schedule.Assign(20, "home");
        Assert.Equal("shop", schedule.StationAt(8));
        Assert.Equal("shop", schedule.StationAt(19));
        Assert.Equal("home", schedule.StationAt(23));
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Assign(24, "x"));
    }

    [Fact]
    public void Fear_routes_the_hurt_and_outleveled()
    {
        var rng = new Random(11);
        Assert.True(UuFleeCheck.ShouldFlee(true, 5, 30, 5, 3, rng));
        Assert.False(UuFleeCheck.ShouldFlee(false, 5, 30, 5, 3, rng));
        Assert.False(UuFleeCheck.ShouldFlee(true, 20, 30, 5, 3, rng));
        Assert.False(UuFleeCheck.ShouldFlee(true, 5, 30, 1, 3, rng));
    }
}
