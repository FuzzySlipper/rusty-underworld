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
    public PlayerActorState Avatar { get; }

    private UuSession(
        ActorsState actors,
        EntityDirectory directory,
        GameClock clock,
        DurableIdentityAllocator actorIdentities,
        DurableIdentityAllocator itemIdentities,
        UuDungeonSession dungeon,
        UuLocomotionPolicy locomotion,
        PlayerActorState avatar)
    {
        Actors = actors;
        Directory = directory;
        Clock = clock;
        ActorIdentities = actorIdentities;
        ItemIdentities = itemIdentities;
        Dungeon = dungeon;
        Locomotion = locomotion;
        Avatar = avatar;
    }

    public static UuSession NewGame(
        UuCreationFlow.CreationResult choices,
        UuVitalsPolicy.Vitals vitals,
        AdmittedLevel firstLevel,
        ActorPose spawnPose,
        UuMovementTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(vitals);
        ArgumentNullException.ThrowIfNull(firstLevel);

        var actors = new ActorsState();
        var directory = new EntityDirectory();
        var clock = new GameClock();
        PlayerActorState avatar = UuAvatarFactory.CreateAvatar(actors, spawnPose, choices, vitals);
        var dungeon = new UuDungeonSession(clock, new UuLevelState(firstLevel));
        var session = new UuSession(
            actors, directory, clock,
            UuIdentityPolicy.NewGameActorAllocator(),
            UuIdentityPolicy.NewGameItemAllocator(),
            dungeon,
            new UuLocomotionPolicy(tuning ?? UuMovementTuning.Default),
            avatar);
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
