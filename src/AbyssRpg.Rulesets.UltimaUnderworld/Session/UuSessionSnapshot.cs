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
    AvatarPoseDto AvatarPose);

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
