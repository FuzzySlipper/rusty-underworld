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
    public sealed record Admission(IReadOnlyDictionary<int, EntityId> ByIndex, IReadOnlyDictionary<int, DurableIdentityReference> Identities);

    public static Admission AdmitLevel(EntityDirectory directory, AdmittedLevel level, UuLevelState state)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(state);
        if (state.LevelNumber != level.LevelNumber)
            throw new ArgumentException("Level state is for a different level.", nameof(state));

        var byIndex = new Dictionary<int, EntityId>();
        var identities = new Dictionary<int, DurableIdentityReference>();
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
                // the walk continues: chains stay linked past them.
                if (obj.ItemId != 0 && state.IsLive(index))
                {
                    var identity = UuIdentityPolicy.LevelObjectIdentity(level.LevelNumber, index);
                    if (!directory.TryResolve(identity, out EntityId entity))
                        entity = directory.CreateItemEntity(identity, new EntityTypeId($"abyss.item.{obj.ItemId}"));
                    byIndex[index] = entity;
                    identities[index] = identity;
                }

                index = obj.Next;
            }
        }

        return new Admission(byIndex, identities);
    }

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
