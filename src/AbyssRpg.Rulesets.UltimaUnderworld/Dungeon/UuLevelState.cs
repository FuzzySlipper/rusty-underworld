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

    /// <summary>Drops and throws placed back on the level (take reverses).</summary>
    public List<DroppedPlacement> Dropped { get; } = [];

    public void Drop(DroppedPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.TileX < 0 || placement.TileX >= 64 || placement.TileY < 0 || placement.TileY >= 64)
            throw new ArgumentOutOfRangeException("Tile is outside the 64x64 map.");
        Dropped.Add(placement);
    }

    public bool Lift(DroppedPlacement placement) => Dropped.Remove(placement);

    /// <summary>
    /// Trigger placements that have already fired, by object index. A trigger that
    /// has fired does not fire again until it is released, and the level's own state
    /// is where that lives, so it survives a save and a level's re-admission.
    /// </summary>
    public HashSet<int> FiredTriggers { get; } = [];

    /// <summary>Records that a trigger has fired, and answers whether it was new.</summary>
    public bool Fire(int objectIndex) => FiredTriggers.Add(objectIndex);

    /// <summary>Releases a trigger, so an occupying one can fire again.</summary>
    public bool Release(int objectIndex) => FiredTriggers.Remove(objectIndex);

    public UuLevelDelta CaptureDelta() => new(
        LevelNumber,
        RemovedObjects.ToArray(),
        MovedObjects.ToDictionary(e => e.Key, e => e.Value),
        OpenedDoors.ToArray(),
        Dropped.ToArray())
    {
        FiredTriggers = FiredTriggers.ToArray(),
    };

    public void ApplyDelta(UuLevelDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.LevelNumber != LevelNumber)
            throw new ArgumentException($"Delta is for level {delta.LevelNumber}, not {LevelNumber}.", nameof(delta));
        RemovedObjects.UnionWith(delta.RemovedObjects);
        foreach (var move in delta.MovedObjects) MovedObjects[move.Key] = move.Value;
        foreach (var door in delta.OpenedDoors) OpenedDoors.Add(door);
        Dropped.AddRange(delta.Dropped);
        FiredTriggers.UnionWith(delta.FiredTriggers);
    }
}

/// <summary>A dropped or thrown object resting on a tile (identity included).</summary>
public sealed record DroppedPlacement(int TileX, int TileY, int ItemId, int Quality, int Quantity, ulong IdentityValue);

/// <summary>Persistable per-level change record. The Host save owner stores these (UW-T25).</summary>
public sealed record UuLevelDelta(
    int LevelNumber,
    int[] RemovedObjects,
    Dictionary<int, (int TileX, int TileY)> MovedObjects,
    (int X, int Y)[] OpenedDoors,
    DroppedPlacement[] Dropped)
{
    /// <summary>
    /// Where this level's admitted creatures stand and how hurt they are. Part of
    /// the level's own state, beside what was removed, moved or opened: a creature
    /// the player wounded waits wounded when they come back, and the state travels
    /// with the level into a save.
    /// </summary>
    public UuLevelActor[] Actors { get; init; } = [];

    /// <summary>Trigger placements that had fired when this level was captured.</summary>
    public int[] FiredTriggers { get; init; } = [];
}

/// <summary>One admitted creature's own state, by the placement index it was admitted from.</summary>
public sealed record UuLevelActor(
    int Index,
    float X,
    float Y,
    float Z,
    float HeadingYawRadians,
    double Health);
