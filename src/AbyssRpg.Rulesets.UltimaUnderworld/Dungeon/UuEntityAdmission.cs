using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Identity;
using Rusty.Engine.Entities;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Materializes an admitted level's placed objects as directory entities with
/// durable level-object identities, by walking every tile chain. NPC
/// materialization rides with the critter task (UW-T13); this pass covers
/// items and props. Engine presentation (meshes, lights) plugs in with the
/// session (UW-T02).
/// </summary>
public static class UuEntityAdmission
{
    public sealed record Admission(
        IReadOnlyDictionary<int, EntityId> ByIndex,
        IReadOnlyDictionary<int, DurableIdentityReference> Identities,
        IReadOnlyDictionary<int, AdmittedObject> Objects,
        IReadOnlyDictionary<int, (int X, int Y)> Tiles);

    public static Admission AdmitLevel(EntityDirectory directory, AdmittedLevel level, UuLevelState state)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(state);
        if (state.LevelNumber != level.LevelNumber)
            throw new ArgumentException("Level state is for a different level.", nameof(state));

        var byIndex = new Dictionary<int, EntityId>();
        var identities = new Dictionary<int, DurableIdentityReference>();
        var live = new Dictionary<int, AdmittedObject>();
        var tiles = new Dictionary<int, (int X, int Y)>();
        var objects = level.Objects.ToDictionary(o => o.Index);
        foreach (AdmittedTile tile in level.Tiles)
        {
            int index = tile.ObjectHead;
            var visited = new HashSet<int>();
            while (index != 0)
            {
                if (index <= 0 || index > UuLevelState.MaxObjectIndex || !visited.Add(index))
                    break;
                if (!objects.TryGetValue(index, out AdmittedObject? obj))
                    break;
                // Empty slots and removed objects contribute no entity, but
                // the walk continues: chains stay linked past them. A critter
                // slot becomes an actor instead (UuCritterAdmission).
                if (obj.ItemId != 0 && state.IsLive(index) && !IsCritter(obj))
                {
                    var identity = UuIdentityPolicy.LevelObjectIdentity(level.LevelNumber, index);
                    if (!directory.TryResolve(identity, out EntityId entity))
                        entity = directory.CreateItemEntity(identity, new EntityTypeId($"abyss.item.{obj.ItemId}"));
                    byIndex[index] = entity;
                    identities[index] = identity;
                    live[index] = obj;
                    // A tile can carry a chain of objects; the walk knows the
                    // tile each link hangs on, which is where the object is.
                    tiles.TryAdd(index, (tile.X, tile.Y));
                }

                index = obj.Next;
            }
        }

        // A container's contents hang off its own link, not off a tile, so they
        // are admitted here and recorded without a tile: they are inside the
        // container, not lying on the floor.
        foreach (AdmittedObject container in objects.Values)
        {
            if (!IsContainer(container) || !state.IsLive(container.Index)) continue;
            int index = container.Link;
            var visited = new HashSet<int>();
            while (index != 0)
            {
                if (index <= 0 || index > UuLevelState.MaxObjectIndex || !visited.Add(index))
                    break;
                if (!objects.TryGetValue(index, out AdmittedObject? content))
                    break;
                if (content.ItemId != 0 && state.IsLive(index) && !IsCritter(content))
                {
                    var identity = UuIdentityPolicy.LevelObjectIdentity(level.LevelNumber, index);
                    if (!directory.TryResolve(identity, out EntityId entity))
                        entity = directory.CreateItemEntity(identity, new EntityTypeId($"abyss.item.{content.ItemId}"));
                    byIndex[index] = entity;
                    identities[index] = identity;
                    live[index] = content;
                }

                index = content.Next;
            }
        }

        return new Admission(byIndex, identities, live, tiles);
    }

    private static bool IsCritter(AdmittedObject obj) =>
        obj.Mobile && Content.UuObjectTablesContent.IsCritterItem(obj.ItemId);

    private static bool IsContainer(AdmittedObject obj) =>
        !obj.Mobile && Content.UuObjectTablesContent.IsContainerItem(obj.ItemId);

    public static void AbandonLevel(EntityDirectory directory, Admission admission)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(admission);
        // Abandonment is idempotent: play may already have consumed or
        // destroyed admitted entities.
        foreach (DurableIdentityReference identity in admission.Identities.Values)
            directory.Destroy(identity);
    }
}
