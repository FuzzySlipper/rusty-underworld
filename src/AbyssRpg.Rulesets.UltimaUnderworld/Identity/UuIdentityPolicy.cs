using AbyssRpg.Kit.World;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Identity;

/// <summary>
/// UW1 durable-identity conventions over the Kit allocator/directory:
/// the avatar is actor 1 (reserved, never allocated); NPCs and summons take
/// dynamic actor ids from 2 up; level objects carry authored item ids of
/// level * 1024 + index; dynamically spawned items allocate from 10240 up,
/// above every authored id. Transient Engine handles never escape.
/// </summary>
public static class UuIdentityPolicy
{
    public const ulong AvatarId = 1;
    public const ulong FirstDynamicActorId = 2;
    public const ulong FirstDynamicItemId = 10 * 1024;

    public static DurableIdentityReference AvatarIdentity { get; } = new(DurableIdentityKind.Actor, AvatarId);

    public static DurableIdentityReference LevelObjectIdentity(int level, int index)
    {
        if (level < 1 || level > 9)
            throw new ArgumentOutOfRangeException(nameof(level), "Levels are 1-9.");
        if (index < 0 || index >= 1024)
            throw new ArgumentOutOfRangeException(nameof(index), "Level object indices are 0-1023.");
        return new DurableIdentityReference(DurableIdentityKind.Item, checked((ulong)(level * 1024 + index)));
    }

    /// <summary>
    /// The durable identity of a level's floor: the owner that holds the level's
    /// loose objects. It is one per level and outlives travel away and back.
    /// </summary>
    public static DurableIdentityReference LevelStorageIdentity(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        return new DurableIdentityReference(DurableIdentityKind.Container, LevelStorageIdBase + checked((ulong)level));
    }

    private const ulong LevelStorageIdBase = 0x1_0000;

    public static DurableIdentityAllocator NewGameActorAllocator() =>
        new(DurableIdentityKind.Actor, FirstDynamicActorId, reserved: [AvatarId]);

    public static DurableIdentityAllocator NewGameItemAllocator() =>
        new(DurableIdentityKind.Item, FirstDynamicItemId);
}
