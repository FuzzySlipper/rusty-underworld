using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuMusicTests
{
    [Fact]
    public void Situations_select_tracks_and_buses_route()
    {
        Assert.Equal(UuMusicPolicy.Track.Combat, UuMusicPolicy.Select(UuMusicPolicy.Situation.Combat, 0));
        Assert.Equal(UuMusicPolicy.Track.Warning, UuMusicPolicy.Select(UuMusicPolicy.Situation.Warning, 0));
        Assert.Equal(UuMusicPolicy.Track.Victory, UuMusicPolicy.Select(UuMusicPolicy.Situation.Victory, 0));
        Assert.Equal(UuMusicPolicy.Track.Death, UuMusicPolicy.Select(UuMusicPolicy.Situation.Death, 0));
        Assert.Equal(UuMusicPolicy.Track.Automap, UuMusicPolicy.Select(UuMusicPolicy.Situation.Automap, 0));
        Assert.Equal(UuMusicPolicy.Track.Injured, UuMusicPolicy.Select(UuMusicPolicy.Situation.Injured, 0));
        Assert.Equal(UuMusicPolicy.Track.LevelUp, UuMusicPolicy.Select(UuMusicPolicy.Situation.LevelUp, 0));
        Assert.Equal(UuMusicPolicy.Track.ExploringA, UuMusicPolicy.Select(UuMusicPolicy.Situation.Exploring, 0));
        Assert.Equal(UuMusicPolicy.Track.ExploringD, UuMusicPolicy.Select(UuMusicPolicy.Situation.Exploring, 7));

        Assert.Equal(new UuMusicPolicy.ClipRef("music", "a.ogg"), UuMusicPolicy.RouteMusic("a.ogg"));
        Assert.Equal(new UuMusicPolicy.ClipRef("speech", "s.ogg"), UuMusicPolicy.RouteSpeech("s.ogg"));
        Assert.Equal(new UuMusicPolicy.ClipRef("sfx", "e.ogg"), UuMusicPolicy.RouteEffect("e.ogg"));
    }
}
