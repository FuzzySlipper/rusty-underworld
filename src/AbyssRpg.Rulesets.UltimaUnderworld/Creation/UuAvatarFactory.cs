using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Inventory;
using AbyssRpg.Kit.World;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// Builds the avatar entity: Kit player construction plus the finishing work
/// the Kit deliberately leaves out — pose body and inventory/equipment
/// components (the kit-survey F1 adaptation). The avatar starts with empty
/// containers; starting loadout is a content task.
/// </summary>
public static class UuAvatarFactory
{
    public static readonly CapacityMetricId WeightMetric = CapacityMetricId.Parse("abyss.classic-weight");

    public sealed record AvatarStores(InventoryStore Store);

    public static PlayerActorState CreateAvatar(
        ActorsState actors,
        ActorPose spawnPose,
        UuCreationFlow.CreationResult choices,
        UuVitalsPolicy.Vitals vitals)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(vitals);

        PlayerActorState player = actors.CreatePlayer(
            1,
            new EntityTypeId("abyss.avatar"),
            new StatsComponent(),
            "abyss.defeat");
        EntityId entity = player.Actor.Entity;

        // Finishing work: the Kit constructor attaches stats, targeting,
        // attack, effects, and vitals only.
        player.Actor.Add(new ActorBody(spawnPose));

        var store = new InventoryStore();
        store.RegisterInventory(new InventoryState(entity, [new InventoryCapacityLimit(WeightMetric, ulong.MaxValue)]));
        store.RegisterEquipment(new EquipmentState(entity));
        player.Actor.Add(new InventoryComponent(store, entity));
        player.Actor.Add(new EquipmentComponent(store, entity));

        return player;
    }
}
