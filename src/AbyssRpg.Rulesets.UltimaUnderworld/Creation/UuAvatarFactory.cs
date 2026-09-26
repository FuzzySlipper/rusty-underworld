using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Inventory;
using AbyssRpg.Kit.World;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// Builds the avatar entity: Kit player construction plus the finishing work
/// the Kit deliberately leaves out — pose body, creation-rolled stats and
/// vitals tracks, and inventory/equipment components (the kit-survey F1
/// adaptation). The avatar starts with empty containers; starting loadout is
/// a content task.
/// </summary>
public static class UuAvatarFactory
{
    public static readonly CapacityMetricId WeightMetric = CapacityMetricId.Parse("abyss.classic-weight");
    public static readonly TrackId DefeatTrack = TrackId.Parse("abyss.defeat");
    public static readonly TrackId ManaTrack = TrackId.Parse("abyss.mana");

    /// <summary>
    /// Raises the avatar's mana pool to a larger one. Mana belongs to the
    /// survival and progression owners; focused tests that are about another
    /// system use this to give the avatar the pool that system needs.
    /// </summary>
    public static void GrantManaForTest(PlayerActorState avatar, double maximum)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        StatsComponent stats = avatar.Actor.Get<StatsComponent>();
        Track existing = stats.GetTrack(ManaTrack);
        if (maximum <= existing.MaximumValue) return;
        stats.RemoveTrack(ManaTrack);
        stats.AddTrack(ManaTrack, new Track(maximum, current: maximum));
    }

    public static PlayerActorState CreateAvatar(
        ActorsState actors,
        ActorPose spawnPose,
        UuCreationFlow.CreationResult choices,
        UuVitalsPolicy.Vitals vitals)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(vitals);

        var stats = new StatsComponent();
        stats.AddStat(StatId.Parse("abyss.strength"), new Stat(choices.Attributes[0]));
        stats.AddStat(StatId.Parse("abyss.dexterity"), new Stat(choices.Attributes[1]));
        stats.AddStat(StatId.Parse("abyss.intelligence"), new Stat(choices.Attributes[2]));
        for (int skill = 0; skill < choices.Skills.Length; skill++)
            stats.AddStat(StatId.Parse($"abyss.skill.{skill}"), new Stat(choices.Skills[skill]));
        stats.AddTrack(DefeatTrack, new Track(vitals.MaxHp, current: vitals.MaxHp));
        stats.AddTrack(ManaTrack, new Track(vitals.MaxMana, current: vitals.MaxMana));

        PlayerActorState player = actors.CreatePlayer(
            1,
            new EntityTypeId("abyss.avatar"),
            stats,
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
