using System.Numerics;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using AbyssRpg.Rulesets.UltimaUnderworld.Survival;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>
/// The composed Ultima Underworld session the product entry admits updates
/// into: one ruleset session state, one Engine spatial session walking the
/// imported level, one first-person camera over it, the visible level mesh, the
/// combat charge state, the casting owner's upkeep, and survival on the one
/// dungeon clock. Composition happens here because assembling this game's named
/// Kit services with this game's policy is ruleset work; the Host only starts,
/// pauses, resumes, and stops what it gets back.
/// </summary>
public sealed class UuGameSession : IGameSession, IModeAwareGameSession, ISaveableGameSession, ISessionStatusSource, IRespawnableGameSession
{
    /// <summary>Skill index the melee check rolls against (UW1 skill 0 is Attack).</summary>
    public const int AttackSkill = 0;

    /// <summary>Melee reach in Engine units; a swing resolves against an opponent inside it.</summary>
    public const float MeleeReach = 6f;

    /// <summary>A hand's reach: what the use channel can name from where the avatar stands.</summary>
    public const float InteractionReach = 2.5f;

    private const float EyeHeight = 1.6f;

    private readonly GameSessionContext _context;
    private readonly UuTuningProfile _tuning;
    private UuLevelDefinition _level;
    private readonly UuLevelScene _scene;
    private readonly UuSession _session;
    private readonly SpatialMovementSystem _movement;
    private readonly PlayerControlState _player;
    private readonly FirstPersonCameraSystem _camera;
    private readonly UuLocomotionPolicy _locomotion;
    private readonly UuCombatHosting _combat = new();
    private readonly UuCastingHosting _casting;
    private readonly Random _rng;
    private Material? _material;
    private MeshResource? _mesh;
    private Appearance? _appearance;

    /// <summary>Level resource generations kept alive until the session ends.</summary>
    private readonly List<(Appearance? Appearance, MeshResource? Mesh, Material? Material)> _retiredLevelResources = [];
    private ProductMode _mode = ProductMode.Playing;
    private ProductMode? _pendingMode;
    private bool _defeatRequested;
    private double _tickCarry;
    private bool _scenePublished;
    private bool _disposed;

    /// <summary>True once the world is released; the ruleset's debug module reads this.</summary>
    public bool Disposed => _disposed;

    private UuGameSession(
        GameSessionContext context,
        UuTuningProfile tuning,
        UuLevelDefinition level,
        UuLevelScene scene,
        UuLevelPlacements? placements,
        UuLevelCatalog? catalog,
        UuItemCatalog? items,
        UuSession session,
        SpatialMovementSystem movement,
        PlayerControlState player,
        FirstPersonCameraSystem camera,
        UuLocomotionPolicy locomotion,
        UuCastingHosting casting,
        Random rng)
    {
        _context = context;
        _tuning = tuning;
        _level = level;
        _scene = scene;
        _placements = placements;
        _catalog = catalog;
        _items = items;
        _session = session;
        _movement = movement;
        _player = player;
        _camera = camera;
        _locomotion = locomotion;
        _casting = casting;
        _rng = rng;
    }

    public UuSession State => _session;

    public UuMovementTuning MovementTuning => _tuning.Movement;

    public UuCastingHosting Casting => _casting;

    /// <summary>Live facts for the product's projection. Cheap: no Engine calls.</summary>
    public SessionStatus Status
    {
        get
        {
            StatsComponent stats = _session.Avatar.Stats;
            UuHudValues hud = UuHudProjection.Read(
                stats, UuAvatarFactory.DefeatTrack, UuAvatarFactory.ManaTrack,
                _combat.ChargeFraction, _player.YawRadians, _outcome);
            return new SessionStatus(
                _session.Dungeon.CurrentLevel,
                _session.Clock.ElapsedTicks,
                _avatarName,
                hud.Hp, hud.MaxHp, hud.Mana, hud.MaxMana, hud.ChargeFraction, hud.YawRadians, hud.WindIndex,
                _outcome, _session.Avatar.IsDefeated, _session.Swimming, _session.Flying,
                PresentActors);
        }
    }

    private string _avatarName = "Avatar";

    private UuLevelPlacements? _placements;

    /// <summary>The imported levels this session may travel between, when the entry built one.</summary>
    private readonly UuLevelCatalog? _catalog;

    /// <summary>The imported item catalog, when the bundle carries one.</summary>
    private readonly UuItemCatalog? _items;

    /// <summary>
    /// The actor each placed critter slot became, per level, so a defeat can be
    /// recorded against the level that holds the placement and a target search
    /// never reaches an inhabitant of another level.
    /// </summary>
    private readonly Dictionary<int, IReadOnlyDictionary<int, ActorState>> _placedCrittersByLevel = [];

    private IReadOnlyDictionary<int, ActorState> PlacedCritters =>
        _placedCrittersByLevel.TryGetValue(_session.Dungeon.CurrentLevel, out var map)
            ? map
            : EmptyCritters;

    private static readonly IReadOnlyDictionary<int, ActorState> EmptyCritters =
        new Dictionary<int, ActorState>();

    /// <summary>Live placed actors around the avatar; the status line reports it.</summary>
    public int PresentActors
    {
        get
        {
            int actors = 0;
            foreach (ActorState _ in _session.Actors.All) actors++;
            return actors;
        }
    }

    public ulong ClockTicks => _session.Clock.ElapsedTicks;

    public WorldPoint? PlayerPosition => _player.Position;

    public float PlayerYawRadians => _player.YawRadians;

    private string _outcome = "";

    /// <summary>
    /// Resolves the default bundle's content into one live session: the
    /// operator-imported level the dungeon starts on, the authored avatar and
    /// class packs, and the shipped tuning profile. A bundle without the level
    /// import is reported as the operator step it is missing, not as a crash.
    /// </summary>
    public static UuGameSession Create(GameSessionContext context) => Create(context, saved: null);

    /// <summary>
    /// Refuses a saved payload the composition cannot restore, before the Host
    /// releases the live session to make room for it. It reads the same content
    /// the restore reads and allocates nothing Engine-owned.
    /// </summary>
    public static void Validate(GameSessionContext context, RulesetSavePayload saved)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(saved);
        Prepared prepared = Prepare(context, saved);
        if (prepared.Snapshot is null) return;
        // Restoring is what reads the snapshot's values, so the dry run restores
        // into a throwaway Kit world: it owns no Engine resource, and a payload
        // whose values are out of range is refused before the Host releases the
        // live session rather than after.
        UuSession probe = UuSession.NewGame(
            prepared.Choices, prepared.Vitals, prepared.FirstLevel, prepared.Level.Spawn,
            prepared.Tuning.Movement, worldSeed: prepared.WorldSeed);
        try
        {
            probe.RestoreSnapshot(prepared.Snapshot);
        }
        finally
        {
            probe.Dispose();
        }
    }

    /// <summary>
    /// Everything the composition decides before any Engine resource exists:
    /// the authored packs, the imported level, the rolled avatar, and the saved
    /// payload decoded and level-checked.
    /// </summary>
    private sealed record Prepared(
        UuTuningProfile Tuning,
        UuLevelDefinition Level,
        UuCreationFlow.CreationResult Choices,
        UuVitalsPolicy.Vitals Vitals,
        AdmittedLevel FirstLevel,
        UuLevelPlacements? Placements,
        UuObjectTables? Tables,
        UuItemCatalog? Items,
        UuSessionSnapshot? Snapshot,
        int WorldSeed,
        string DefaultAvatarName);

    private static Prepared Prepare(GameSessionContext context, RulesetSavePayload? saved)
    {
        ResolvedGameComposition composition = context.Composition;
        UuTuningProfile tuning = UuTuningProfile.Read(
            composition.Tuning.Payload, $"tuning '{composition.Tuning.Id.Value}'");
        ContentPack avatarPack = composition.RequireContentPack(new ContentPackId("abyssrpg.avatar-options"));
        ContentPack classPack = composition.RequireContentPack(new ContentPackId("abyssrpg.classes"));
        (ContentPack levelPack, int levelNumber) = RequireLevelPack(composition);

        UuLevelDefinition level = UuLevelContent.Read(levelPack.Payload, $"content pack '{levelPack.Id.Value}'");
        if (level.Level != levelNumber)
            throw new InvalidOperationException($"Level pack '{levelPack.Id.Value}' declares level {level.Level}.");

        // Decode before anything Engine-owned exists: a corrupt or foreign-level
        // save must fail without leaving a half-built world behind.
        UuSessionSnapshot? savedSnapshot = saved is null
            ? null
            : UuSessionSnapshotCodec.Decode(saved.Bytes.ToArray());
        if (savedSnapshot is not null && savedSnapshot.Level != level.Level)
        {
            throw new InvalidOperationException(
                $"Save is on level {savedSnapshot.Level}; the bundle admits level {level.Level}. "
                + "Import that level before loading this save.");
        }

        UuCreationCatalog.Catalog creation = UuCreationCatalog.Read(
            avatarPack.Payload, classPack.Payload,
            $"content pack '{avatarPack.Id.Value}'", $"content pack '{classPack.Id.Value}'");
        int worldSeed = composition.Identity.Bundle.Value.GetHashCode(StringComparison.Ordinal);
        var rng = new Random(unchecked(worldSeed));
        UuCreationFlow.CreationResult choices = UuCreationCatalog.RollDefault(creation, rng);
        UuVitalsPolicy.Vitals vitals = UuVitalsPolicy.Recalculate(
            choices.Attributes[0], 1, choices.Skills[SkillIndexForVitals],
            choices.Attributes[2]);

        // The level's placements come from the operator's own import: every
        // tile with its chain, and every object slot with its container fields.
        // A level imported before placements existed still admits (empty), and
        // the critter tables are their own pack so a level can play without them.
        UuLevelPlacements? placements = level.PlacementsPath is { Length: > 0 } path
            ? UuLevelContent.ReadPlacements(
                composition.Content.ReadBytes(path), $"content pack '{levelPack.Id.Value}' placements")
            : null;
        ContentPack? tablesPack = composition.ContentPacks
            .SingleOrDefault(pack => pack.Id.Value == UuObjectTablesContent.PackId);
        UuObjectTables? tables = tablesPack is null
            ? null
            : UuObjectTablesContent.Read(tablesPack.Payload, $"content pack '{tablesPack.Id.Value}'");
        ContentPack? itemsPack = composition.ContentPacks
            .SingleOrDefault(pack => pack.Id.Value == UuItemCatalogContent.PackId);
        UuItemCatalog? items = itemsPack is null
            ? null
            : UuItemCatalogContent.Read(itemsPack.Payload, $"content pack '{itemsPack.Id.Value}'");
        AdmittedLevel firstLevel = placements is null
            ? new AdmittedLevel(level.Level, [], [])
            : new AdmittedLevel(level.Level, placements.Tiles, placements.Objects);

        return new Prepared(
            tuning, level, choices, vitals, firstLevel, placements, tables, items, savedSnapshot, worldSeed,
            creation.Defaults.Name);
    }

    public static UuGameSession Create(GameSessionContext context, RulesetSavePayload? saved)
    {
        ArgumentNullException.ThrowIfNull(context);
        ProductContent content = context.Composition.Content;
        Prepared prepared = Prepare(context, saved);
        UuTuningProfile tuning = prepared.Tuning;
        UuLevelDefinition level = prepared.Level;
        UuSessionSnapshot? savedSnapshot = prepared.Snapshot;

        string renderPath = level.RenderPath;
        UuLevelScene scene = UuLevelSceneContent.Read(content.ReadBytes(renderPath), renderPath);

        UuCreationFlow.CreationResult choices = prepared.Choices;
        UuVitalsPolicy.Vitals vitals = prepared.Vitals;
        var rng = new Random(unchecked(prepared.WorldSeed));
        UuSession session = UuSession.NewGame(
            choices, vitals, prepared.FirstLevel, level.Spawn, tuning.Movement,
            worldSeed: prepared.WorldSeed);
        var spatialTuning = new SpatialTuning(0.5d, 8, 8, 1);
        SpatialMovementSystem movement;
        try
        {
            movement = new SpatialMovementSystem(context.Engine.Spatial, context.Engine.Content, level.Collision, spatialTuning);
        }
        catch
        {
            session.Dispose();
            throw;
        }
        // The imported spawn stands on a tile surface; the Engine's character is
        // a capsule placed by its center, so the surface height is offset by the
        // controller's own standing half-height and contact skin.
        PlayerControlState player = new(
            new WorldPoint(
                level.Spawn.Position.X,
                level.Spawn.Position.Y + movement.StandingCenterOffset,
                level.Spawn.Position.Z),
            level.Spawn.HeadingYawRadians,
            0f);
        FirstPersonCameraSystem camera;
        try
        {
            camera = new FirstPersonCameraSystem(
                context.Engine.CameraView, player, new FirstPersonCameraTuning(EyeHeight, 75d, 0.05d, 2000d));
        }
        catch
        {
            movement.Dispose();
            session.Dispose();
            throw;
        }

        var gameSession = new UuGameSession(
            context,
            tuning,
            level,
            scene,
            prepared.Placements,
            new UuLevelCatalog(context.Composition),
            prepared.Items,
            session,
            movement,
            player,
            camera,
            new UuLocomotionPolicy(tuning.Movement),
            new UuCastingHosting(rng),
            rng)
        {
            _avatarName = choices.Name.Length == 0 ? prepared.DefaultAvatarName : choices.Name,
        };

        // Placed critters become actors in the same admitted world the item
        // pass filled, so a swing or an interaction meets a real presence. A
        // restored save then removes the actors of placements it says are gone.
        if (prepared.Placements is { } placedLevel && prepared.Tables is { } objectTables)
        {
            gameSession._placedCrittersByLevel[prepared.FirstLevel.LevelNumber] = UuCritterAdmission
                .AdmitLevel(session, prepared.FirstLevel, placedLevel, objectTables)
                .ByObjectIndex;
        }

        if (savedSnapshot is not null)
        {
            try
            {
                session.RestoreSnapshot(savedSnapshot);
                player.Restore(
                    new WorldPoint(savedSnapshot.AvatarPose.X, savedSnapshot.AvatarPose.Y, savedSnapshot.AvatarPose.Z),
                    DetachedMotion(savedSnapshot.AvatarPose.Y));
                player.YawRadians = savedSnapshot.AvatarPose.YawRadians;
            }
            catch
            {
                gameSession.Dispose();
                throw;
            }
        }

        return gameSession;
    }

    // The vitals policy reads the mana skill index; UW1 skill 8 governs casting.
    private const int SkillIndexForVitals = 8;

    private static (ContentPack Pack, int Level) RequireLevelPack(ResolvedGameComposition composition)
    {
        ContentPack? best = null;
        int bestLevel = 0;
        foreach (ContentPack pack in composition.ContentPacks)
        {
            const string prefix = "abyssrpg.level-";
            if (!pack.Id.Value.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(pack.Id.Value[prefix.Length..], out int number) || number < 1) continue;
            if (best is null || number < bestLevel)
            {
                best = pack;
                bestLevel = number;
            }
        }

        return best is null || bestLevel < 1
            ? throw new InvalidOperationException(
                "The default bundle carries no imported level pack. Run the operator import "
                + "(scripts/import-level.sh) before launching, then rebuild.")
            : (best, bestLevel);
    }

    /// <summary>
    /// Admits the imported level's collision, creates the visible level mesh and
    /// the first-person camera. Engine resource selection is a product decision,
    /// so it happens once, on Start, from the admitted level content.
    /// </summary>
    public void PublishInitial()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_scenePublished) return;
        PublishLevel(_scene);
    }

    /// <summary>
    /// Draws one level's geometry. The previous generation of resources is kept
    /// until the session ends: the renderer may still hold what it was drawing,
    /// so a level change retires its resources rather than releasing them under
    /// a frame that is already in flight.
    /// </summary>
    private void PublishLevel(UuLevelScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Material? material = null;
        MeshResource? mesh = null;
        Appearance? appearance = null;
        try
        {
            material = _context.Engine.Graphics.CreateMaterial(new MaterialRequest(
                new Color(1f, 1f, 1f, 1f),
                default,
                Roughness: 0.95f,
                new Color(1f, 1f, 1f, 1f),
                EmissionColor: default,
                EmissionIntensity: 0.12f,
                DoubleSided: false,
                MaterialAlphaMode.Opaque,
                0.5f));
            mesh = _context.Engine.Graphics.CreateMeshResource(new MeshResourceCreateRequest(
                scene.Positions,
                scene.Normals,
                ReadOnlyMemory<Vector2>.Empty,
                scene.Colors,
                scene.Indices,
                new MeshGroup[] { new(0, 0, (uint)scene.Indices.Length) },
                new MeshMaterialBinding[] { new(0, material) }));
            appearance = _context.Engine.Graphics.CreateMeshAppearance(mesh);
            _context.Engine.Graphics.PublishSnapshot(
            new AppearanceFact[]
            {
                new AppearanceFact(
                    LevelObjectId,
                    HasParentObject: false,
                    ParentObjectId: 0,
                    new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                    appearance,
                    Visible: true,
                    RenderLayer.Scene),
            });
            RetireLevelResources();
            _material = material;
            _mesh = mesh;
            _appearance = appearance;
            material = null;
            mesh = null;
            appearance = null;
            _scenePublished = true;
        }
        finally
        {
            // A failed publish disposes what it created, appearance first: a
            // retained appearance owns the mesh, and the mesh owns its material.
            appearance?.Dispose();
            mesh?.Dispose();
            material?.Dispose();
        }

        _context.Engine.CameraView.SetBackgroundColor(new SetBackgroundColorRequest(new Color(0.03f, 0.03f, 0.05f, 1f)));
        _camera.Update(_player);
    }

    /// <summary>Keeps the generation being replaced until the session releases it.</summary>
    private void RetireLevelResources()
    {
        if (_appearance is null && _mesh is null && _material is null) return;
        _retiredLevelResources.Add((_appearance, _mesh, _material));
        _appearance = null;
        _mesh = null;
        _material = null;
    }

    /// <summary>Stable Engine object id for the one level appearance this session owns.</summary>
    public const ulong LevelObjectId = 1;

    public ProductUpdateResult Update(ProductUpdate update)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        double seconds = update.Facts.FixedDeltaSeconds;
        if (!double.IsFinite(seconds) || seconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(update));

        // Paused, modal and dead modes keep presentation live and hold the world still.
        if (_mode != ProductMode.Playing)
        {
            _camera.Update(_player);
            return ProductUpdateResult.None;
        }

        _tickCarry += seconds * _tuning.ClockTicksPerSecond;
        ulong whole = (ulong)_tickCarry;
        _tickCarry -= whole;
        if (whole > 0) _session.Clock.Advance(whole);

        UuLocomotionPolicy.UuPlayerStep step = _locomotion.BeginPlayerStep(
            _player, update.Input, (float)seconds, canMove: true, _session.Swimming, _session.Flying);
        StepLocomotion(step, update, seconds);
        TickAttack(step, (float)seconds);
        if (step.UsePressed && !_session.Flying) Interact();
        _casting.Upkeep(_session.Clock.ElapsedTicks);
        TickSurvival(seconds);
        _camera.Update(_player);

        if (_session.Avatar.IsDefeated && !_defeatRequested)
        {
            _defeatRequested = true;
            _pendingMode = ProductMode.Dead;
        }

        return ProductUpdateResult.None;
    }

    private void StepLocomotion(UuLocomotionPolicy.UuPlayerStep step, ProductUpdate update, double seconds)
    {
        var state = new ProductUpdateState((float)seconds);
        foreach (ProductInputEvent input in update.Input) state.Add(input);
        CharacterMotion before = _player.Motion;
        CharacterStepReceipt? receipt = _movement.Step(_player, state, null, step.Controls);
        if (receipt is not { } confirmed) return;
        float fallDamage = UuStepConsequences.LandingDamage(before, confirmed, _tuning.Movement);
        if (fallDamage > 0f) ApplyDefeatDamage(fallDamage, "The fall hurts.");
        _session.Avatar.Actor.Get<ActorBody>().Pose =
            new ActorPose(WorldPoint.From(confirmed.Transform.Translation), _player.YawRadians);
    }

    private void TickAttack(UuLocomotionPolicy.UuPlayerStep step, float seconds)
    {
        if (step.AttackHeld || step.AttackPressed) _combat.Hold(seconds);
        if (!step.AttackReleased) return;
        ActorState? target = NearestOpponent();
        if (target is null)
        {
            _combat.Reset();
            _outcome = "Your swing meets empty air.";
            return;
        }

        int attackSkill = (int)_session.Avatar.Stats.GetStat(StatId.Parse($"abyss.skill.{AttackSkill}")).Value;
        UuCombatHosting.StrikeOutcome strike = _combat.Release(
            _session.Avatar.Stats, target.Stats, attackSkill, difficulty: 15, damageSides: 6, _rng);
        if (strike.Hit && strike.TargetDefeated) RecordDefeat(target);
        _outcome = strike.Hit
            ? strike.TargetDefeated
                ? $"You strike for {strike.Damage} and it falls."
                : $"You strike for {strike.Damage}."
            : "You miss.";
    }

    /// <summary>
    /// The admitted interaction verb: the avatar uses what it stands at. A door
    /// tile opens and closes through the level state the save carries, so the
    /// change survives a load; anywhere else the outcome says so.
    /// </summary>
    /// <summary>
    /// The admitted use channel: what the avatar is standing at decides. A door
    /// tile opens or closes, an object the level placed is named from the
    /// imported item catalog, and anything else answers that there is nothing
    /// here — one dispatch, not a verb per owner.
    /// </summary>
    private void Interact()
    {
        if (_placements is null || _player.Position is not { } position)
        {
            _outcome = "There is nothing here to use.";
            return;
        }

        if (_placements.TileAt(position) is { Door: true } tile)
        {
            bool open = !_session.Dungeon.Current.OpenedDoors.Contains((tile.X, tile.Y));
            _session.Dungeon.Current.SetDoor(tile.X, tile.Y, open);
            _outcome = open ? "You open the door." : "You close the door.";
            return;
        }

        if (PlacedObjectInReach(position) is { } placed)
        {
            _outcome = $"You see {Describe(placed.ItemId)} here.";
            return;
        }

        _outcome = "There is nothing here to use.";
    }

    /// <summary>The nearest object the level placed, within a hand's reach.</summary>
    private AdmittedObject? PlacedObjectInReach(WorldPoint position)
    {
        AdmittedObject? nearest = null;
        float nearestDistance = InteractionReach;
        foreach (int index in _session.PlacedObjectIndexes(_session.Dungeon.CurrentLevel))
        {
            if (_session.PlacedObjectAt(_session.Dungeon.CurrentLevel, index, out (int X, int Y) tile) is not { } obj)
                continue;
            AdmittedTile? on = _placements?.Tile(tile.X, tile.Y);
            if (on is null) continue;
            WorldPoint center = _placements!.TileCenter(on.X, on.Y, on.FloorHeight);
            float distance = center.HorizontalDistanceTo(position);
            if (distance > nearestDistance) continue;
            nearest = obj;
            nearestDistance = distance;
        }

        return nearest;
    }

    /// <summary>What the avatar calls an item it can see: its imported name, or its kind.</summary>
    private string Describe(int itemId)
    {
        string name = _items?.Find(itemId)?.Name ?? "";
        return name.Length > 0 ? name : "something";
    }

    /// <summary>
    /// Records a fallen placement in the level state, which is what the save
    /// carries: without it the same critter is standing at full strength after
    /// a load.
    /// </summary>
    private void RecordDefeat(ActorState target)
    {
        foreach (KeyValuePair<int, ActorState> placed in PlacedCritters)
        {
            if (placed.Value.DurableId != target.DurableId) continue;
            _session.Dungeon.Current.RemoveObject(placed.Key);
            return;
        }
    }

    private ActorState? NearestOpponent()
    {
        ActorState? nearest = null;
        float nearestDistance = MeleeReach;
        WorldPoint origin = _player.Position ?? _level.Spawn.Position;
        // Only this level's inhabitants are in reach: an actor of another level
        // stands in a world the avatar is not in.
        foreach (ActorState actor in PlacedCritters.Values)
        {
            // A fallen opponent is scenery, not a target.
            if (actor.IsDefeated) continue;
            float distance = actor.Position.HorizontalDistanceTo(origin);
            if (distance > nearestDistance) continue;
            nearest = actor;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void TickSurvival(double seconds)
    {
        SurvivalTickResult result = UuSurvivalPolicy.Tick(_session.Survival, seconds, _rng);
        int damage = result.HungerDamage + result.FatigueDamage + result.PoisonDamage;
        if (damage > 0) ApplyDefeatDamage(damage, "You are failing.");
    }

    /// <summary>Applies harm on the defeat track, clamped: starvation and falls can exceed what is left.</summary>
    public void ApplyDefeatDamage(float damage, string outcome)
    {
        if (!float.IsFinite(damage) || damage <= 0f) throw new ArgumentOutOfRangeException(nameof(damage));
        Track track = _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack);
        track.Current = Math.Max(0, track.Current - damage);
        _outcome = outcome;
    }

    /// <summary>
    /// Operator probe: stand the avatar on a tile of the admitted level. The
    /// Engine spatial step owns ordinary movement; this places the avatar for
    /// probing a level position without walking there. Only a tile the level
    /// admits as open is accepted: a capsule placed inside solid geometry makes
    /// the Engine refuse the next character step, which taints the runtime and
    /// costs the live session.
    /// </summary>
    public void PlaceOnTile(int tileX, int tileY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_placements is null) throw new InvalidOperationException("This session admits no placements.");
        AdmittedTile? tile = _placements.Tile(tileX, tileY)
            ?? throw new ArgumentOutOfRangeException(nameof(tileX), $"Tile ({tileX},{tileY}) is outside the level.");
        if (!UuTileKind.IsOpen(tile))
        {
            throw new InvalidOperationException(
                $"Tile ({tileX},{tileY}) is not open in the admitted level, so the avatar cannot stand there.");
        }
        // The Engine's controller stands on the floor by its own half-height,
        // so a placed avatar is the tile center raised to the capsule's
        // standing center; a capsule placed at floor level is inside geometry
        // and the next step proposal is refused.
        WorldPoint center = _placements.TileCenter(tile.X, tile.Y, tile.FloorHeight);
        var standing = new WorldPoint(center.X, center.Y + _movement.StandingCenterOffset, center.Z);
        _player.Restore(standing, DetachedMotion(standing.Y));
    }

    /// <summary>
    /// Moves the avatar to another imported level of the same dungeon: the
    /// level's own content is admitted (collision, visible geometry, items and
    /// critters), the level left behind keeps its state and loses its actors,
    /// and the target search follows the avatar. Travel cost is the caller's:
    /// the gameplay caller that owns a transition (stairs, a pit, a moongate)
    /// decides what a transition costs in clock time.
    /// </summary>
    public void TravelToLevel(int level, ulong costTicks)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_catalog is null) throw new InvalidOperationException("This session has no imported level catalog.");
        int from = _session.Dungeon.CurrentLevel;
        if (level == from)
            throw new InvalidOperationException($"The avatar is already on level {level}.");

        UuLevelDefinition definition = _catalog.Definition(level);
        AdmittedLevel admitted = _catalog.Admit(level);
        UuLevelScene scene = _catalog.Scene(level);
        UuLevelPlacements? placements = _catalog.Placements(level);

        // The level left behind keeps what happened on it, in the session's own
        // stored deltas, and its actors leave the world with it.
        _session.TravelTo(admitted, costTicks);
        AbandonActors(from);

        // The Engine stands the avatar in the new level's collision before any
        // step is proposed, then the new level's geometry is drawn.
        _movement.ReplaceContent(definition.Collision);
        PublishLevel(scene);
        _level = definition;
        _placements = placements;

        // A restored level's dead placements are already gone from its state, so
        // admission skips them and the actors of the living ones join the world.
        if (placements is not null && _catalog.Tables is { } tables)
        {
            _placedCrittersByLevel[level] = UuCritterAdmission
                .AdmitLevel(_session, admitted, placements, tables)
                .ByObjectIndex;
        }

        WorldPoint anchor = AnchorPositionOf(definition);
        _player.Restore(anchor, DetachedMotion(anchor.Y));
        _camera.Update(_player);
        _outcome = $"You descend to level {level}.";
    }

    /// <summary>Releases the actors of a level the avatar has left.</summary>
    private void AbandonActors(int level)
    {
        if (!_placedCrittersByLevel.Remove(level, out IReadOnlyDictionary<int, ActorState>? actors)) return;
        foreach (ActorState actor in actors.Values)
            _session.Actors.Entities.Destroy(AbyssRpg.Kit.Actors.ActorsState.Identity(actor.DurableId));
    }

    /// <summary>The anchor pose of a level: its spawn raised to the capsule's standing center.</summary>
    private WorldPoint AnchorPositionOf(UuLevelDefinition level) => new(
        level.Spawn.Position.X,
        level.Spawn.Position.Y + _movement.StandingCenterOffset,
        level.Spawn.Position.Z);

    /// <summary>The nearest placed actors to the avatar, with their distance.</summary>
    public IReadOnlyList<(ActorState Actor, float Distance)> NearestActors(int take)
    {
        WorldPoint origin = _player.Position ?? _level.Spawn.Position;
        return PlacedCritters.Values
            .Select(actor => (Actor: actor, Distance: actor.Position.HorizontalDistanceTo(origin)))
            .OrderBy(entry => entry.Distance)
            .Take(take)
            .ToArray();
    }

    /// <summary>The imported item catalog this session reads names and mass from, when the bundle carries one.</summary>
    public UuItemCatalog? Items => _items;

    /// <summary>Where the avatar's capsule center stands, before any new proposal.</summary>
    public WorldPoint? AvatarPosition => _player.Position;

    /// <summary>The door tiles this session's level state has opened.</summary>
    public IReadOnlyCollection<(int X, int Y)> OpenedDoors => _session.Dungeon.Current.OpenedDoors;

    /// <summary>Collects one rune into the shelf through the casting owner (debug and pickup callers).</summary>
    public void CollectRune(int index) => _casting.CollectRune(index);

    /// <summary>Shelf runes and attempt a cast through the casting owner; mana is charged on success.</summary>
    public UuCastingHosting.CastOutcome AttemptCast(int spellId)
    {
        UuCastingHosting.CastOutcome outcome = _casting.AttemptCast(
            characterLevel: 1,
            mana: (int)_session.Avatar.Stats.GetTrack(UuAvatarFactory.ManaTrack).Current,
            castingSkill: (int)_session.Avatar.Stats.GetStat(StatId.Parse($"abyss.skill.{SkillIndexForVitals}")).Value,
            delayed: false,
            nowTicks: _session.Clock.ElapsedTicks,
            ticksPerSecond: _tuning.ClockTicksPerSecond,
            spellId);
        if (outcome.ManaCost > 0)
        {
            Track mana = _session.Avatar.Stats.GetTrack(UuAvatarFactory.ManaTrack);
            mana.Current = Math.Max(0, mana.Current - outcome.ManaCost);
        }

        if (outcome.Backfired)
        {
            ApplyDefeatDamage(outcome.BackfireDamage, "The spell backfires.");
            return outcome;
        }

        _outcome = outcome.Gate == UuCastGates.GateResult.Cast
            ? outcome.PrimedForAim ? "The spell waits for a target." : "The spell takes hold."
            : $"The cast fails ({outcome.Gate}).";
        return outcome;
    }

    public void ApplyProductMode(ProductMode mode)
    {
        _mode = mode;
        if (mode != ProductMode.Playing)
        {
            _locomotion.Neutralize();
            _combat.Reset();
        }
    }

    public ProductMode? PendingModeRequest
    {
        get
        {
            ProductMode? request = _pendingMode;
            _pendingMode = null;
            return request;
        }
    }

    public bool PendingModeRequestClosesModal => false;

    public ProductMode Mode => _mode;

    public RulesetSavePayload CaptureSave()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WorldPoint position = _player.Position ?? _level.Spawn.Position;
        UuSessionSnapshot snapshot = _session.CaptureSnapshot(
            new AvatarPoseDto(position.X, position.Y, position.Z, _player.YawRadians));
        return new RulesetSavePayload(UuGameRuleset.RulesetIdentity, UuSessionSnapshotCodec.Encode(snapshot));
    }

    /// <summary>Returns the avatar to the level spawn and restores its vitals: the defeat outcome.</summary>
    public void RespawnAtAnchor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _player.Restore(AnchorPosition(), DetachedMotion(AnchorPosition().Y));
        _player.YawRadians = _level.Spawn.HeadingYawRadians;
        _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current =
            _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Maximum.Value;
        _defeatRequested = false;
        _pendingMode = ProductMode.Playing;
        _outcome = "You wake at the anchor.";
    }

    /// <summary>The anchor's capsule-center pose, matching the launch placement.</summary>
    private WorldPoint AnchorPosition() => new(
        _level.Spawn.Position.X,
        _level.Spawn.Position.Y + _movement.StandingCenterOffset,
        _level.Spawn.Position.Z);

    private static CharacterMotion DetachedMotion(float anchorY) => new(
        ControlledVelocity: Vector3.Zero,
        ExternalVelocity: Vector3.Zero,
        Grounded: false,
        Stance: Rusty.Engine.CharacterStance.Standing,
        JumpBufferRemaining: 0f, CoyoteRemaining: 0f, LandingLockoutRemaining: 0f,
        SupportEntityPresent: false, SupportEntity: 0,
        SupportLocalAnchor: Vector3.Zero,
        SupportPreviousTranslation: Vector3.Zero,
        SupportPreviousRotation: Quaternion.Identity,
        SupportPointVelocity: Vector3.Zero,
        FallOriginY: anchorY, PeakY: anchorY,
        LastCommandSequence: 0, CollisionWorldHash: 0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? failures = null;

        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }

        if (_scenePublished)
        {
            // A retained appearance must leave the published snapshot before disposal.
            Attempt(() => _context.Engine.Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty));
        }

        Attempt(() => _appearance?.Dispose());
        Attempt(() => _mesh?.Dispose());
        Attempt(() => _material?.Dispose());
        foreach ((Appearance? appearance, MeshResource? mesh, Material? material) in _retiredLevelResources)
        {
            Attempt(() => appearance?.Dispose());
            Attempt(() => mesh?.Dispose());
            Attempt(() => material?.Dispose());
        }

        Attempt(_camera.Dispose);
        Attempt(_movement.Dispose);
        Attempt(_session.Dispose);

        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }
}
