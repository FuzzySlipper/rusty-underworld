using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Critters;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Materializes the level's placed critters as actors. A critter is a mobile
/// object whose item id is majorclass 1 (64-127) and whose statistics come from
/// the imported object tables; it stands on the tile its own record names. The
/// item admission pass skips these slots, so one placement never becomes both
/// an item entity and an actor.
/// </summary>
public static class UuCritterAdmission
{
    public sealed record Admission(IReadOnlyList<ActorState> Actors, IReadOnlyDictionary<int, ActorState> ByObjectIndex);

    /// <summary>Steps per full turn in the record's own heading units.</summary>
    public const int HeadingSteps = 32;

    /// <summary>
    /// The facing to stand an actor at: a record heading is in 32 steps per
    /// turn, and a record that carries none faces along the level's own zero.
    /// </summary>
    public static float Facing(int heading) =>
        heading < 0 ? 0f : (float)(heading % HeadingSteps * (2d * Math.PI / HeadingSteps));

    public static Admission AdmitLevel(
        Session.UuSession session,
        AdmittedLevel level,
        UuLevelPlacements placements,
        UuObjectTables tables,
        Func<int, int, int, WorldPoint>? positionOf = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(tables);
        Func<int, int, int, WorldPoint> position = positionOf ?? placements.TileCenter;
        var actors = new List<ActorState>();
        var byObjectIndex = new Dictionary<int, ActorState>();
        foreach (AdmittedObject placed in level.Objects)
        {
            if (!placed.Mobile || !UuObjectTablesContent.IsCritterItem(placed.ItemId)) continue;
            if (placed.HomeTileX < 0 || placed.HomeTileY < 0) continue;
            if (!tables.Critters.TryGetValue(placed.ItemId, out UuCritterFactory.CritterDefinition? definition)) continue;
            if (!session.Dungeon.Current.IsLive(placed.Index)) continue;
            AdmittedTile? tile = placements.Tile(placed.HomeTileX, placed.HomeTileY);
            if (tile is null) continue;

            var pose = new ActorPose(position(tile.X, tile.Y, tile.FloorHeight), Facing(placed.Heading));
            ActorState actor = UuCritterFactory.CreateCritter(
                session.Actors, session.ActorIdentities, pose, definition);
            actors.Add(actor);
            byObjectIndex[placed.Index] = actor;
            // The session owns the placement-to-actor link so a save that
            // removes the placement also removes the actor.
            session.RegisterPlacedActor(placed.Index, actor);
        }

        return new Admission(actors, byObjectIndex);
    }
}
