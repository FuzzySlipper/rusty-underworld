using AbyssRpg.Kit.Actors;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>What a verb in the avatar's reach resolves to.</summary>
public enum UuReachKind
{
    /// <summary>Nothing in reach.</summary>
    None,

    /// <summary>A living creature: it can be talked to.</summary>
    Creature,

    /// <summary>A creature that has been struck down: it can be looted where it lies.</summary>
    Corpse,

    /// <summary>The door tile the avatar stands at: it can be opened or closed.</summary>
    Door,

    /// <summary>A container on the floor: using it yields what it holds.</summary>
    Container,

    /// <summary>An object lying on the floor: it can be taken, or used.</summary>
    Item,
}

/// <summary>The verbs a target accepts. Attack is not among them: it rides the charge channel.</summary>
[Flags]
public enum UuVerbs
{
    None = 0,
    Talk = 1,
    Loot = 2,
    Take = 4,
    Use = 8,
    Open = 16,
}

/// <summary>
/// One answer to "what is the nearest thing this verb applies to", with what that
/// target accepts. Every use-channel verb resolves through this rather than
/// scanning the level for itself, so a target's reach and its verbs are decided
/// once.
/// </summary>
public readonly record struct UuReachTarget(
    UuReachKind Kind,
    float Distance,
    UuVerbs Verbs,
    ActorState? Actor = null,
    AdmittedObject? Object = null,
    AdmittedTile? Door = null)
{
    /// <summary>Nothing in reach accepts anything.</summary>
    public static UuReachTarget Nothing => new(UuReachKind.None, float.PositiveInfinity, UuVerbs.None);

    public bool Accepts(UuVerbs verb) => (Verbs & verb) != 0;

    /// <summary>A door target for the tile the avatar stands at.</summary>
    public static UuReachTarget AtDoor(AdmittedTile tile, float distance) =>
        new(UuReachKind.Door, distance, UuVerbs.Open, Door: tile);

    /// <summary>A creature target, living or fallen.</summary>
    public static UuReachTarget AtActor(ActorState actor, float distance, bool defeated) =>
        defeated
            ? new UuReachTarget(UuReachKind.Corpse, distance, UuVerbs.Loot, Actor: actor)
            : new UuReachTarget(UuReachKind.Creature, distance, UuVerbs.Talk, Actor: actor);

    /// <summary>An object on the floor: a container is used, a loose thing is taken.</summary>
    public static UuReachTarget AtObject(AdmittedObject obj, float distance, bool container) =>
        container
            ? new UuReachTarget(UuReachKind.Container, distance, UuVerbs.Use | UuVerbs.Loot, Object: obj)
            : new UuReachTarget(UuReachKind.Item, distance, UuVerbs.Take | UuVerbs.Use, Object: obj);
}
