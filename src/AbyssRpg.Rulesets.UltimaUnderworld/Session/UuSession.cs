using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Time;
using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Identity;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using Rusty.Engine.Entities;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>
/// The UW ruleset session composition: one continuous dungeon across all
/// levels, owning the Kit services (actors, directory, clock) plus UW policy
/// (dungeon states, locomotion, identity ledgers). New game builds the avatar
/// from creation results and admits the first level; travel swaps admitted
/// levels with clock costs; save captures clock, ledgers, and deltas for the
/// Host owner (UW-T25). Engine update admission and presentation plug in
/// with the Host entry (UW-T01).
/// </summary>
public sealed class UuSession : IDisposable
{
    private readonly Dictionary<int, UuLevelDelta> _storedDeltas = [];
    private readonly Dictionary<int, UuEntityAdmission.Admission> _admissions = [];
    private readonly Dictionary<int, DurableIdentityReference> _placedActors = [];
    private bool _disposed;

    public ActorsState Actors { get; }
    public EntityDirectory Directory { get; }
    public GameClock Clock { get; }
    public DurableIdentityAllocator ActorIdentities { get; private set; }
    public DurableIdentityAllocator ItemIdentities { get; private set; }
    public UuDungeonSession Dungeon { get; }
    public UuLocomotionPolicy Locomotion { get; }
    public UuMovementTuning MovementTuning { get; }
    public PlayerActorState Avatar { get; }

    /// <summary>Survival accumulators ticked by updates.</summary>
    public Survival.UuSurvivalState Survival { get; } = new();

    /// <summary>Conversation quest/game variables.</summary>
    public Kit.Knowledge.QuestVariables Quests { get; } = new();

    /// <summary>Automap coverage by level.</summary>
    public Dictionary<int, Kit.Knowledge.AutomapPage> Automap { get; } = [];

    /// <summary>Quill notes (per-level pages).</summary>
    public Kit.Knowledge.QuillNotes Notes { get; } = new();

    /// <summary>Planted world seed (respawn identity).</summary>
    public long WorldSeed { get; }

    /// <summary>Respawn anchor planted at creation.</summary>
    public UuRespawnAnchor Anchor { get; }

    /// <summary>Avatar locomotion medium. Set by survival/spell systems; the product reads it per update.</summary>
    public bool Swimming { get; set; }

    /// <summary>Avatar flight. Set by spell/item systems; the product reads it per update.</summary>
    public bool Flying { get; set; }

    /// <summary>
    /// The live placed object at a level and object index, with the tile it
    /// hangs on, or null when the level admits no such object.
    /// </summary>
    public AdmittedObject? PlacedObjectAt(int level, int objectIndex, out (int X, int Y) tile)
    {
        tile = default;
        if (!_admissions.TryGetValue(level, out UuEntityAdmission.Admission? admission)) return null;
        if (!admission.Objects.TryGetValue(objectIndex, out AdmittedObject? obj)) return null;
        // An object without a tile is inside a container rather than lying on
        // the floor, so the caller is told that by a negative position.
        tile = admission.Tiles.TryGetValue(objectIndex, out (int X, int Y) found) ? found : (-1, -1);
        return obj;
    }

    /// <summary>The item admission a level currently has, or null when it is not admitted.</summary>
    public UuEntityAdmission.Admission? PlacedAdmission(int level) =>
        _admissions.TryGetValue(level, out UuEntityAdmission.Admission? admission) ? admission : null;

    /// <summary>The object indexes a level admits as live entities right now.</summary>
    public IReadOnlyCollection<int> PlacedObjectIndexes(int level) =>
        _admissions.TryGetValue(level, out UuEntityAdmission.Admission? admission)
            ? admission.Objects.Keys.ToArray()
            : [];

    private UuSession(
        ActorsState actors,
        EntityDirectory directory,
        GameClock clock,
        DurableIdentityAllocator actorIdentities,
        DurableIdentityAllocator itemIdentities,
        UuDungeonSession dungeon,
        UuLocomotionPolicy locomotion,
        UuMovementTuning movementTuning,
        PlayerActorState avatar,
        long worldSeed,
        UuRespawnAnchor anchor)
    {
        Actors = actors;
        Directory = directory;
        Clock = clock;
        ActorIdentities = actorIdentities;
        ItemIdentities = itemIdentities;
        Dungeon = dungeon;
        Locomotion = locomotion;
        MovementTuning = movementTuning;
        Avatar = avatar;
        WorldSeed = worldSeed;
        Anchor = anchor;
    }

    public static UuSession NewGame(
        UuCreationFlow.CreationResult choices,
        UuVitalsPolicy.Vitals vitals,
        AdmittedLevel firstLevel,
        ActorPose spawnPose,
        UuMovementTuning? tuning = null,
        long worldSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(vitals);
        ArgumentNullException.ThrowIfNull(firstLevel);

        // One entity store for the whole session: actors and level objects share
        // it, so their Engine entity ids are unique together.
        var directory = new EntityDirectory();
        var actors = new ActorsState(directory);
        var clock = new GameClock();
        PlayerActorState avatar = UuAvatarFactory.CreateAvatar(actors, spawnPose, choices, vitals);
        var dungeon = new UuDungeonSession(clock, new UuLevelState(firstLevel));
        var movementTuning = tuning ?? UuMovementTuning.Default;
        var session = new UuSession(
            actors, directory, clock,
            UuIdentityPolicy.NewGameActorAllocator(),
            UuIdentityPolicy.NewGameItemAllocator(),
            dungeon,
            new UuLocomotionPolicy(movementTuning),
            movementTuning,
            avatar,
            worldSeed,
            UuRespawnAnchor.FromPose(firstLevel.LevelNumber, spawnPose, worldSeed));
        session._admissions[firstLevel.LevelNumber] =
            UuEntityAdmission.AdmitLevel(directory, firstLevel, dungeon.Current);
        return session;
    }

    /// <summary>Travel to a supplied level: unload (delta + abandon), pay the clock cost, admit.</summary>
    public void TravelTo(AdmittedLevel target, ulong costTicks)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);

        int from = Dungeon.CurrentLevel;
        _storedDeltas[from] = Dungeon.Unload(from);
        if (_admissions.Remove(from, out var admission))
            UuEntityAdmission.AbandonLevel(Directory, admission);

        var state = new UuLevelState(target);
        if (_storedDeltas.TryGetValue(target.LevelNumber, out var stored))
            state.ApplyDelta(stored);
        Dungeon.Admit(state);
        Dungeon.TravelTo(target.LevelNumber, costTicks);
        _admissions[target.LevelNumber] = UuEntityAdmission.AdmitLevel(Directory, target, state);
    }

    /// <summary>
    /// Capture the slice snapshot (non-destructive). Avatar pose comes from
    /// the Host spatial owner. Restoring level deltas rides with
    /// travel/admission; this restore covers clock, avatar, survival,
    /// knowledge, stored deltas, and identity allocators.
    /// </summary>
    public UuSessionSnapshot CaptureSnapshot(AvatarPoseDto pose)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pose);
        return new UuSessionSnapshot(
            Clock.ElapsedTicks,
            Dungeon.CurrentLevel,
            Avatar.Stats.GetTrack(Creation.UuAvatarFactory.DefeatTrack).Current,
            Avatar.Stats.GetTrack(Creation.UuAvatarFactory.ManaTrack).Current,
            Survival.Hunger,
            Survival.Fatigue,
            Survival.Poison,
            Survival.Drunkenness,
            WorldSeed,
            Anchor,
            _storedDeltas.Values.Append(Dungeon.Unload(Dungeon.CurrentLevel)).Select(ToDeltaDto).ToArray(),
            Automap.Select(kv => new AutomapPageDto(kv.Key, kv.Value.EncodePage())).ToArray(),
            Enumerable.Range(0, Kit.Knowledge.QuestVariables.SlotCount)
                .Select(slot => new QuestVarDto(slot, Quests.Get(slot)))
                .ToArray(),
            Notes.Levels
                .SelectMany(level => Notes.Notes(level).Select(note => new NoteDto(level, note.Text, note.X, note.Y)))
                .ToArray(),
            pose,
            ActorIdentities.CaptureState().Kinds,
            ItemIdentities.CaptureState().Kinds);
    }

    public void RestoreSnapshot(UuSessionSnapshot snapshot)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        Clock.Restore(new GameClockSnapshot(snapshot.ClockTicks));
        Avatar.Stats.GetTrack(Creation.UuAvatarFactory.DefeatTrack).Current = snapshot.Hp;
        Avatar.Stats.GetTrack(Creation.UuAvatarFactory.ManaTrack).Current = snapshot.Mana;
        Survival.Hunger = snapshot.Hunger;
        Survival.Fatigue = snapshot.Fatigue;
        Survival.Poison = snapshot.Poison;
        Survival.Drunkenness = snapshot.Drunkenness;
        _storedDeltas.Clear();
        foreach (UuLevelDeltaDto delta in snapshot.Deltas)
            _storedDeltas[delta.LevelNumber] = FromDeltaDto(delta);
        // The admitted level was rebuilt from content, so the saved changes to
        // it -- opened doors, removed and moved objects, dropped items -- have
        // to be applied to the live level state, and the entities of objects
        // the save says are gone must not stand in the restored world.
        if (_storedDeltas.TryGetValue(Dungeon.CurrentLevel, out UuLevelDelta? current))
        {
            Dungeon.Current.ApplyDelta(current);
            if (_admissions.TryGetValue(Dungeon.CurrentLevel, out UuEntityAdmission.Admission? admission))
            {
                foreach (int removed in current.RemovedObjects)
                {
                    if (admission.Identities.TryGetValue(removed, out DurableIdentityReference identity))
                        Directory.Destroy(identity);
                }
            }

            // A placement the save says is gone must not stand there as an
            // actor either: a critter that was struck down is not waiting at
            // full strength for the next load.
            foreach (int removed in current.RemovedObjects)
            {
                if (_placedActors.Remove(removed, out DurableIdentityReference actor))
                    Actors.Entities.Destroy(actor);
            }
        }
        Quests.Clear();
        foreach (QuestVarDto quest in snapshot.QuestVars) Quests.Set(quest.Slot, quest.Value);
        foreach (AutomapPageDto page in snapshot.Automap)
        {
            if (!Automap.TryGetValue(page.Level, out Kit.Knowledge.AutomapPage? existing))
                Automap[page.Level] = existing = new Kit.Knowledge.AutomapPage();
            existing.DecodeInto(page.Rle);
        }

        Notes.Clear();
        foreach (NoteDto note in snapshot.Notes) Notes.Place(note.Level, note.Text, note.X, note.Y);
        // Identity allocators ride with the slice: without them a restored
        // session would re-issue identities the snapshot still references.
        ActorIdentities = DurableIdentityAllocator.Restore(new DurableIdentityState(snapshot.ActorIdentities));
        ItemIdentities = DurableIdentityAllocator.Restore(new DurableIdentityState(snapshot.ItemIdentities));
    }

    private static UuLevelDelta FromDeltaDto(UuLevelDeltaDto dto) => new(
        dto.LevelNumber,
        dto.RemovedObjects.ToArray(),
        dto.MovedObjects.ToDictionary(m => m.Index, m => (m.TileX, m.TileY)),
        dto.OpenedDoors.Select(door => (door.X, door.Y)).ToArray(),
        dto.Dropped.Select(d => new DroppedPlacement(d.TileX, d.TileY, d.ItemId, d.Quality, d.Quantity, d.IdentityValue)).ToArray());

    private static UuLevelDeltaDto ToDeltaDto(UuLevelDelta delta) => new(
        delta.LevelNumber,
        delta.RemovedObjects.ToArray(),
        delta.MovedObjects.Select(kv => new MovedObjectDto(kv.Key, kv.Value.TileX, kv.Value.TileY)).ToArray(),
        delta.OpenedDoors.Select(door => new DoorDto(door.X, door.Y)).ToArray(),
        delta.Dropped.Select(d => new DroppedDto(d.TileX, d.TileY, d.ItemId, d.Quality, d.Quantity, d.IdentityValue)).ToArray());

    /// <summary>
    /// Remembers which actor a placed object index became, so a save that says
    /// the placement is gone also removes the actor standing there. Critter
    /// admission registers here; nothing else places actors on the level.
    /// </summary>
    public void RegisterPlacedActor(int placementIndex, ActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (placementIndex <= 0 || placementIndex > UuLevelState.MaxObjectIndex)
            throw new ArgumentOutOfRangeException(nameof(placementIndex), "A placement index is 1-1023.");
        _placedActors[placementIndex] = ActorsState.Identity(actor.DurableId);
    }

    /// <summary>The actor a placement index became, if it is still standing.</summary>
    public bool TryGetPlacedActor(int placementIndex, out ActorState actor)
    {
        if (_placedActors.TryGetValue(placementIndex, out DurableIdentityReference identity)
            && Actors.Entities.TryResolve(identity, out _))
        {
            actor = Actors.Get(checked((long)identity.Value));
            return true;
        }

        actor = null!;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // The actors and the level objects share one directory, so it is
        // disposed once, through the actors' owner.
        Actors.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

