using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Ai;
using AbyssRpg.Kit.World;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Critters;

/// <summary>
/// Builds critter actors from table rows: Kit actor construction (stats,
/// pose, vitals) plus a pursuit-memory component seeded Idle. Corpses are
/// created through the Kit CorpseLootCoordinator by the session death path,
/// not here.
/// </summary>
public static class UuCritterFactory
{
    public sealed record CritterDefinition(
        int ItemId,
        int AvgHp,
        int Strength,
        int Dexterity,
        int Intelligence,
        int Speed,
        int CorpseIndex);

    public static ActorState CreateCritter(
        ActorsState actors,
        DurableIdentityAllocator identities,
        ActorPose spawnPose,
        CritterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(definition);

        // NPCs resolve through ActorsState by durable actor id; session
        // Directory mapping for NPCs rides with presence (UW-T29).
        DurableIdentityReference identity = identities.Allocate(DurableIdentityKind.Actor);
        var stats = new StatsComponent();
        stats.AddStat(StatId.Parse("abyss.strength"), new Stat(definition.Strength));
        stats.AddStat(StatId.Parse("abyss.dexterity"), new Stat(definition.Dexterity));
        stats.AddStat(StatId.Parse("abyss.intelligence"), new Stat(definition.Intelligence));
        stats.AddTrack(TrackId.Parse("abyss.defeat"), new Track(definition.AvgHp, current: definition.AvgHp));

        ActorState actor = actors.CreateActor(
            (long)identity.Value,
            new EntityTypeId($"abyss.critter.{definition.ItemId}"),
            stats,
            spawnPose,
            "abyss.defeat");
        actor.Actor.Add(new PursuitMemoryComponent());
        return actor;
    }
}
