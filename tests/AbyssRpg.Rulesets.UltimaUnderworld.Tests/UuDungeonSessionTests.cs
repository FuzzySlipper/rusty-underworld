using AbyssRpg.Kit.Time;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuDungeonSessionTests
{
    private readonly ITestOutputHelper _output;

    public UuDungeonSessionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Remove_move_door_delta_round_trip()
    {
        var level = new AdmittedLevel(1,
            [new AdmittedTile(0, 0, 1, 5), new AdmittedTile(1, 0, 1, 0)],
            [new AdmittedObject(0, 0, 0), new AdmittedObject(5, 44, 0)]);
        var state = new UuLevelState(level);

        state.RemoveObject(5);
        Assert.False(state.IsLive(5));
        Assert.Throws<InvalidOperationException>(() => state.MoveObject(5, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.RemoveObject(9999));

        UuLevelDelta delta = state.CaptureDelta();
        var restored = new UuLevelState(level);
        restored.ApplyDelta(delta);
        Assert.False(restored.IsLive(5));
        Assert.Throws<ArgumentException>(() => restored.ApplyDelta(delta with { LevelNumber = 2 }));
    }

    [Fact]
    public void Travel_advances_the_clock_and_keeps_deltas()
    {
        var clock = new GameClock();
        var one = new UuLevelState(new AdmittedLevel(1, [], []));
        var session = new UuDungeonSession(clock, one);
        var two = new UuLevelState(new AdmittedLevel(2, [], []));
        session.Admit(two);

        session.TravelTo(2, costTicks: 500);
        Assert.Equal(2, session.CurrentLevel);
        Assert.Equal((ulong)500, clock.ElapsedTicks);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.TravelTo(9, 0));
    }

    [Fact]
    public void Shipped_level_1_admits_and_round_trips()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/LEV.ARK");
        byte[]? terrain = RequireDataOrSkip("UW/DATA/TERRAIN.DAT");
        if (archive is null || terrain is null) return;

        LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, 1, terrain);
        var admitted = new AdmittedLevel(
            pack.LevelNumber,
            pack.Tiles.Select(t => new AdmittedTile(t.X, t.Y, t.Type, t.ObjectHead)).ToArray(),
            pack.Objects.Select(o => new AdmittedObject(o.Index, o.ItemId, o.Next)).ToArray());

        var clock = new GameClock();
        var session = new UuDungeonSession(clock, new UuLevelState(admitted));
        UuLevelState current = session.Current;
        int victim = pack.Objects.First(o => o.Index > 0 && o.ItemId != 0).Index;
        current.RemoveObject(victim);
        UuLevelDelta delta = session.Unload(1);
        Assert.Contains(victim, delta.RemovedObjects);

        var restored = new UuLevelState(admitted);
        restored.ApplyDelta(delta);
        Assert.False(restored.IsLive(victim));
    }

    // Returns null after writing a SKIP notice when operator data is absent.
    private byte[]? RequireDataOrSkip(string relative) =>
        TestData_Optional(relative) is string path ? File.ReadAllBytes(path) : null;

    private string? TestData_Optional(string relative)
    {
        try
        {
            string root = FindRepoRoot();
            string path = Path.Combine(root, "local", "extracted", "uw", relative);
            if (!File.Exists(path))
            {
                _output.WriteLine($"SKIP: operator UW1 file missing, nothing checked: {relative}");
                return null;
            }

            return path;
        }
        catch (DirectoryNotFoundException)
        {
            _output.WriteLine($"SKIP: operator UW1 data root missing, nothing checked: {relative}");
            return null;
        }
    }

    private static string FindRepoRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
