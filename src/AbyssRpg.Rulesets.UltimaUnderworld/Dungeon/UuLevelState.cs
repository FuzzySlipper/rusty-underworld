namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Runtime state of one admitted dungeon level: which placed objects are
/// still live, what moved where, and which doors changed. Unloading captures
/// a delta; re-admission replays it, so kills, takes, placements, and door
/// states persist across level transitions and save/load.
/// </summary>
public sealed class UuLevelState
{
    public int LevelNumber { get; }
    public HashSet<int> RemovedObjects { get; } = [];
    public Dictionary<int, (int TileX, int TileY)> MovedObjects { get; } = [];
    public HashSet<(int X, int Y)> OpenedDoors { get; } = [];

    private readonly AdmittedLevel _level;

    public UuLevelState(AdmittedLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        _level = level;
        LevelNumber = level.LevelNumber;
    }

    public bool IsLive(int objectIndex) => !RemovedObjects.Contains(objectIndex);

    public const int MaxObjectIndex = 1023;

    public void RemoveObject(int objectIndex)
    {
        if (objectIndex <= 0 || objectIndex > MaxObjectIndex)
            throw new ArgumentOutOfRangeException(nameof(objectIndex));
        RemovedObjects.Add(objectIndex);
        MovedObjects.Remove(objectIndex);
    }

    public void MoveObject(int objectIndex, int tileX, int tileY)
    {
        if (objectIndex <= 0 || objectIndex > MaxObjectIndex)
            throw new ArgumentOutOfRangeException(nameof(objectIndex));
        if (tileX < 0 || tileX >= 64 || tileY < 0 || tileY >= 64)
            throw new ArgumentOutOfRangeException("Tile is outside the 64x64 map.");
        if (!IsLive(objectIndex))
            throw new InvalidOperationException($"Object {objectIndex} was removed.");
        MovedObjects[objectIndex] = (tileX, tileY);
    }

    public void SetDoor(int tileX, int tileY, bool open)
    {
        if (tileX < 0 || tileX >= 64 || tileY < 0 || tileY >= 64)
            throw new ArgumentOutOfRangeException("Tile is outside the 64x64 map.");
        if (open) OpenedDoors.Add((tileX, tileY));
        else OpenedDoors.Remove((tileX, tileY));
    }

    public UuLevelDelta CaptureDelta() => new(
        LevelNumber,
        RemovedObjects.ToArray(),
        MovedObjects.ToDictionary(e => e.Key, e => e.Value),
        OpenedDoors.ToArray());

    public void ApplyDelta(UuLevelDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.LevelNumber != LevelNumber)
            throw new ArgumentException($"Delta is for level {delta.LevelNumber}, not {LevelNumber}.", nameof(delta));
        RemovedObjects.UnionWith(delta.RemovedObjects);
        foreach (var move in delta.MovedObjects) MovedObjects[move.Key] = move.Value;
        foreach (var door in delta.OpenedDoors) OpenedDoors.Add(door);
    }
}

/// <summary>Persistable per-level change record. The Host save owner stores these (UW-T25).</summary>
public sealed record UuLevelDelta(
    int LevelNumber,
    int[] RemovedObjects,
    Dictionary<int, (int TileX, int TileY)> MovedObjects,
    (int X, int Y)[] OpenedDoors);
