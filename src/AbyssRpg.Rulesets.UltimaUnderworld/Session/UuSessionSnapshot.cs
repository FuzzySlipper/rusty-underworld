using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>Respawn anchor planted at creation: entry level, spawn pose, world seed.</summary>
public sealed record UuRespawnAnchor(int Level, float X, float Y, float Z, float YawRadians, long Seed)
{
    public static UuRespawnAnchor FromPose(int level, AbyssRpg.Kit.Actors.ActorPose pose, long seed) =>
        new(level, pose.Position.X, pose.Position.Y, pose.Position.Z, pose.HeadingYawRadians, seed);
}

/// <summary>
/// Slice snapshot DTO: avatar vitals, survival, clock, level + deltas,
/// knowledge, seed, and anchor — one current schema, no versions. The Host
/// stores the serialized bytes; meaning stays ruleset-owned.
/// </summary>
public sealed record UuSessionSnapshot(
    ulong ClockTicks,
    int Level,
    double Hp,
    double Mana,
    int Hunger,
    int Fatigue,
    int Poison,
    int Drunkenness,
    long WorldSeed,
    UuRespawnAnchor Anchor,
    UuLevelDeltaDto[] Deltas,
    AutomapPageDto[] Automap,
    QuestVarDto[] QuestVars,
    NoteDto[] Notes,
    AvatarPoseDto AvatarPose,
    AbyssRpg.Kit.World.KindAllocatorState[] ActorIdentities,
    AbyssRpg.Kit.World.KindAllocatorState[] ItemIdentities,
    UuHoldingDto[]? Holdings = null,
    UuCreatureDto[]? Creatures = null);

/// <summary>
/// What one owner holds. The level and owner index locate the owner, which a
/// level's own admission rebuilds from content; each item carries its durable
/// identity and its definition, so an item the avatar carried off another level
/// is put back as the same item rather than as a copy or a lookalike.
/// </summary>
public sealed record UuHoldingDto(int Level, int OwnerIndex, UuHeldItemDto[] Items)
{
    /// <summary>The owner index of the avatar's own pack.</summary>
    public const int AvatarOwner = -1;

    /// <summary>The owner index of a level's loose objects.</summary>
    public const int FloorOwner = -2;
}

/// <summary>One held item: the identity it keeps across a save, and what it is.</summary>
public sealed record UuHeldItemDto(ulong Identity, string Definition);

/// <summary>
/// One admitted creature's own state: where it stands and how hurt it is. The
/// level's delta already records whether it is gone; this records the state of
/// the ones still standing, which the delta cannot express.
/// </summary>
public sealed record UuCreatureDto(
    int Level,
    int Index,
    float X,
    float Y,
    float Z,
    float HeadingYawRadians,
    double Health);

public sealed record UuLevelDeltaDto(
    int LevelNumber,
    int[] RemovedObjects,
    MovedObjectDto[] MovedObjects,
    DoorDto[] OpenedDoors,
    DroppedDto[] Dropped);

public sealed record MovedObjectDto(int Index, int TileX, int TileY);
public sealed record DoorDto(int X, int Y);
public sealed record DroppedDto(int TileX, int TileY, int ItemId, int Quality, int Quantity, ulong IdentityValue);
public sealed record AutomapPageDto(int Level, string Rle);
public sealed record QuestVarDto(int Slot, int Value);
public sealed record NoteDto(int Level, string Text, int X, int Y);
public sealed record AvatarPoseDto(float X, float Y, float Z, float YawRadians);

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(UuSessionSnapshot))]
[JsonSerializable(typeof(AbyssRpg.Kit.World.KindAllocatorState))]
[JsonSerializable(typeof(AbyssRpg.Kit.World.DurableIdentityKind))]
internal partial class UuSessionSnapshotJsonContext : JsonSerializerContext;

public static class UuSessionSnapshotCodec
{
    public static byte[] Encode(UuSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.SerializeToUtf8Bytes(snapshot, UuSessionSnapshotJsonContext.Default.UuSessionSnapshot);
    }

    public static UuSessionSnapshot Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return JsonSerializer.Deserialize(bytes, UuSessionSnapshotJsonContext.Default.UuSessionSnapshot)
            ?? throw new InvalidOperationException("Snapshot decoded to null.");
    }
}
