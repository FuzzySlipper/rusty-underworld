namespace AbyssRpg.Rulesets.UltimaUnderworld.Presentation;

/// <summary>
/// Music track selection: exploring cycles four tracks; combat, warning
/// heartbeat, injured, victory, level-up, death, and automap override.
/// Playback rides Engine Audio voices from ordinary files (no XMI synth);
/// the Host maps tracks to clip assets. Speech and SFX route to their own
/// buses alongside music.
/// Donor states: Music.cs EMusicState.
/// </summary>
public static class UuMusicPolicy
{
    public enum Track
    {
        ExploringA,
        ExploringB,
        ExploringC,
        ExploringD,
        Combat,
        Warning,
        Injured,
        Victory,
        LevelUp,
        Death,
        Automap,
    }

    public enum Situation
    {
        Exploring,
        Combat,
        Warning,
        Injured,
        Victory,
        LevelUp,
        Death,
        Automap,
    }

    public static Track Select(Situation situation, int exploringIndex) => situation switch
    {
        Situation.Combat => Track.Combat,
        Situation.Warning => Track.Warning,
        Situation.Injured => Track.Injured,
        Situation.Victory => Track.Victory,
        Situation.LevelUp => Track.LevelUp,
        Situation.Death => Track.Death,
        Situation.Automap => Track.Automap,
        _ => (Track)(exploringIndex & 3),
    };

    public static class Buses
    {
        public const string Music = "music";
        public const string Speech = "speech";
        public const string Effects = "sfx";
    }

    public sealed record ClipRef(string Bus, string File);

    public static ClipRef RouteMusic(string file) => new(Buses.Music, file);

    public static ClipRef RouteSpeech(string file) => new(Buses.Speech, file);

    public static ClipRef RouteEffect(string file) => new(Buses.Effects, file);
}
