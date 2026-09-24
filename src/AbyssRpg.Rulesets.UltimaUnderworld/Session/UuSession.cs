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
    private bool _disposed;

    public ActorsState Actors { get; }
    public EntityDirectory Directory { get; }
    public GameClock Clock { get; }
    public DurableIdentityAllocator ActorIdentities { get; }
    public DurableIdentityAllocator ItemIdentities { get; }
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

        var actors = new ActorsState();
        var directory = new EntityDirectory();
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

    public UuSaveData CaptureSave()
    {
        ThrowIfDisposed();
        var deltas = new Dictionary<int, UuLevelDelta>(_storedDeltas)
        {
            [Dungeon.CurrentLevel] = Dungeon.Unload(Dungeon.CurrentLevel),
        };
        return new UuSaveData(
            Clock.Capture(),
            ActorIdentities.CaptureState(),
            ItemIdentities.CaptureState(),
            deltas);
    }

    /// <summary>
    /// Capture the slice snapshot (non-destructive). Avatar pose comes from
    /// the Host spatial owner. Restoring level deltas rides with
    /// travel/admission; this restore covers clock, avatar, survival,
    /// knowledge, and stored deltas.
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
            pose);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Actors.Dispose();
        Directory.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>Persistable session record. The Host save owner stores it (UW-T25).</summary>
public sealed record UuSaveData(
    GameClockSnapshot Clock,
    DurableIdentityState ActorIdentities,
    DurableIdentityState ItemIdentities,
    Dictionary<int, UuLevelDelta> LevelDeltas);
