using System.Numerics;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Kit.Inventory;
using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using AbyssRpg.Rulesets.UltimaUnderworld.Survival;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;
using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;
using Rusty.Engine.Debugging;

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
        UuConversationCatalog? conversations,
        UuStrings? strings,
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
        _conversations = conversations;
        _strings = strings;
        _session = session;
        if (items is not null)
        {
            InventoryStore store = session.Avatar.Actor.Get<InventoryComponent>().Store;
            UuItemDefinitions definitions = new(items);
            _levelItems = new UuLevelItems(
                store,
                session.Directory,
                new MechanicsInventoryContainerCoordinator(store, session.Directory, definitions.Definitions),
                definitions,
                ownerIndex => ResolveLevelObject(session, ownerIndex));
        }
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
                PresentActors, ConversationView);
        }
    }

    private string _avatarName = "Avatar";

    private UuLevelPlacements? _placements;

    /// <summary>The imported levels this session may travel between, when the entry built one.</summary>
    private readonly UuLevelCatalog? _catalog;

    /// <summary>The imported item catalog, when the bundle carries one.</summary>
    private readonly UuItemCatalog? _items;

    /// <summary>The conversations the import produced, when the bundle carries them.</summary>
    private readonly UuConversationCatalog? _conversations;

    /// <summary>The last conversation's transcript and panel, which the projection publishes.</summary>
    private string[] _transcript = [];
    private UuConversationHosting.PanelData? _panel;

    /// <summary>The string blocks those conversations read.</summary>
    private readonly UuStrings? _strings;

    /// <summary>The inventory side of the levels' placements, when the bundle carries a catalog.</summary>
    private readonly UuLevelItems? _levelItems;

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
        UuConversationCatalog? Conversations,
        UuStrings? Strings,
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
        ContentPack? conversationsPack = composition.ContentPacks
            .SingleOrDefault(pack => pack.Id.Value == UuConversationCatalog.PackId);
        UuConversationCatalog? conversations = conversationsPack is null
            ? null
            : UuConversationCatalog.Read(conversationsPack.Payload, $"content pack '{conversationsPack.Id.Value}'");
        ContentPack? stringsPack = composition.ContentPacks
            .SingleOrDefault(pack => pack.Id.Value == UuStrings.PackId);
        UuStrings? strings = stringsPack is null
            ? null
            : UuStrings.Read(stringsPack.Payload, $"content pack '{stringsPack.Id.Value}'");
        AdmittedLevel firstLevel = placements is null
            ? new AdmittedLevel(level.Level, [], [])
            : new AdmittedLevel(level.Level, placements.Tiles, placements.Objects);

        return new Prepared(
            tuning, level, choices, vitals, firstLevel, placements, tables, items, conversations, strings,
            savedSnapshot, worldSeed,
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
            prepared.Conversations,
            prepared.Strings,
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

        // The level's items are held by owners: the floor for a loose object,
        // the container that names it, and the critter that carries it.
        if (gameSession._levelItems is { } levelItems
            && session.PlacedAdmission(prepared.FirstLevel.LevelNumber) is { } admitted)
        {
            levelItems.AdoptLevel(prepared.FirstLevel.LevelNumber, admitted);
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

        // A living creature is talked to: the conversation owner runs the script
        // its own record names, and the transcript is what the projection shows.
        if (LiveOpponentInReach(position) is { } talker)
        {
            Talk(talker);
            return;
        }

        // A fallen opponent is looted where it lies: what it carried is held by
        // its own record, so the same transfer serves a corpse and a container.
        if (_levelItems is { } carried && FallenOpponentInReach(position) is { } fallen)
        {
            InventoryContainerTransferReceipt looted = carried.Loot(fallen.Actor.Entity, _session.Avatar.Actor.Entity);
            int count = looted.UniqueItems.Count;
            _outcome = count == 0
                ? $"The {Describe(FallenItemId(fallen))} carries nothing."
                : $"You loot {count} item{(count == 1 ? "" : "s")} from the {Describe(FallenItemId(fallen))}.";
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
            Use(placed);
            return;
        }

        _outcome = "There is nothing here to use.";
    }

    /// <summary>
    /// The Engine entity of a placed object on the level the avatar stands on:
    /// a container's contents name the container, a critter's carried objects
    /// name the critter, so both resolve here.
    /// </summary>
    private static EntityId? ResolveLevelObject(UuSession session, int objectIndex)
    {
        int level = session.Dungeon.CurrentLevel;
        if (session.PlacedAdmission(level) is { } admission
            && admission.ByIndex.TryGetValue(objectIndex, out EntityId entity))
        {
            return entity;
        }

        return session.TryGetPlacedActor(objectIndex, out ActorState actor) ? actor.Actor.Entity : null;
    }

    /// <summary>
    /// Uses the object the avatar stands at: a container yields what it holds,
    /// anything else is taken. Both are one transfer between owners, so the
    /// Engine keeps owning what is where, and a taken object leaves the level
    /// state the save carries.
    /// </summary>
    private void Use(AdmittedObject placed)
    {
        if (_levelItems is not { } items)
        {
            _outcome = $"You see {Describe(placed.ItemId)} here.";
            return;
        }

        int level = _session.Dungeon.CurrentLevel;
        if (Session.UuGameSession.PlacedEntity(_session, level, placed.Index) is not { } entity)
        {
            _outcome = $"You see {Describe(placed.ItemId)} here.";
            return;
        }

        EntityId avatar = _session.Avatar.Actor.Entity;
        bool isContainer = Content.UuObjectTablesContent.IsContainerItem(placed.ItemId);
        if (isContainer)
        {
            InventoryContainerTransferReceipt looted = items.Loot(entity, avatar);
            int count = looted.UniqueItems.Count;
            _outcome = count == 0
                ? $"The {Describe(placed.ItemId)} is empty."
                : $"You loot {count} item{(count == 1 ? "" : "s")} from the {Describe(placed.ItemId)}.";
            return;
        }

        // Taking leaves the floor: the level state records the removal, which is
        // what a save carries, and the object moves into the avatar's inventory.
        EntityId from = items.TryContainerOf(entity, out EntityId container) ? container : items.Floor(level);
        items.Take(entity, from, avatar);
        _session.Dungeon.Current.RemoveObject(placed.Index);
        if (TryRuneIndex(placed.ItemId, out int rune))
        {
            CollectRune(rune);
            ShelfRune(rune);
            _outcome = $"You take the runestone and lay it on the shelf.";
            return;
        }

        _outcome = $"You take the {Describe(placed.ItemId)}.";
    }

    /// <summary>The nearest living creature within a hand's reach, if any.</summary>
    private ActorState? LiveOpponentInReach(WorldPoint position)
    {
        ActorState? nearest = null;
        float nearestDistance = InteractionReach;
        foreach (ActorState actor in PlacedCritters.Values)
        {
            if (actor.IsDefeated) continue;
            float distance = actor.Position.HorizontalDistanceTo(position);
            if (distance > nearestDistance) continue;
            nearest = actor;
            nearestDistance = distance;
        }

        return nearest;
    }

    /// <summary>
    /// Talks to a creature the level placed. Its record selects the
    /// conversation: a whoami byte names one outright, and a creature without
    /// one speaks its own kind's generic conversation, which the donor numbers
    /// 256 + (item id - 64) (donor:
    /// src/conversation/conversationinitialisation.cs GetConversationNumber).
    /// </summary>
    private void Talk(ActorState talker)
    {
        int itemId = ItemIdOf(talker);
        int whoami = WhoAmIOf(talker);
        if (whoami == NoResponseWhoAmI)
        {
            _outcome = "You get no response.";
            return;
        }

        int conversation = whoami != 0 ? whoami : GenericConversationBase + (itemId - FirstCritterItemId);
        UuConversationVm.ConversationScript? script = _conversations?.Script(conversation);
        if (script is null || script.CodeSize == 0)
        {
            _outcome = "You get no response.";
            return;
        }

        string npc = _strings?.CreatureName(whoami) is { Length: > 0 } name ? name : $"creature {itemId}";
        UuConversationHosting.TalkResult result = UuConversationHosting.Talk(
            _session,
            script,
            _strings?.Provider ?? ((_, _) => ""),
            npc,
            tray: Tray(talker),
            avatar: new UuConversationHosting.AvatarPresence(
                CharmSkill: (int)_session.Avatar.Stats.GetStat(StatId.Parse("abyss.skill.15")).Value,
                Level: 1,
                Hp: (int)_session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current,
                Vitality: (int)_session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current),
            talker: new UuConversationHosting.NpcRecord(
                Level: 1,
                Hp: (int)talker.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current,
                MaxHp: (int)talker.Stats.GetTrack(UuAvatarFactory.DefeatTrack).MaximumValue));
        _transcript = result.Transcript.ToArray();
        _panel = result.Panel;
        _speaker = npc;
        _outcome = _transcript.Length > 0
            ? _transcript[^1]
            : _strings?.Text(UuStrings.ConversationBlock, 1) is { Length: > 0 } nothing ? nothing : "You get no response.";
    }

    /// <summary>
    /// What each side of a trade holds. The values are the imported monetary
    /// values of the items carried (donor: src/loaders/comobjloader.cs
    /// monetaryvalue), so a merchant's own stock and the avatar's pack are what
    /// the barter policy weighs.
    /// </summary>
    private UuConversationHosting.BarterTray Tray(ActorState talker)
    {
        int npcValue = 0;
        int playerValue = 0;
        if (_levelItems is { } items)
        {
            foreach (Rusty.Engine.Mechanics.UniqueInventoryItem held in items.Read(talker.Actor.Entity).UniqueItems)
                npcValue += ValueOf(held.Definition.Value);
            foreach (Rusty.Engine.Mechanics.UniqueInventoryItem held in items.Read(_session.Avatar.Actor.Entity).UniqueItems)
                playerValue += ValueOf(held.Definition.Value);
        }

        return new UuConversationHosting.BarterTray(
            NpcValue: npcValue,
            PlayerValue: playerValue,
            CharmSkill: (int)_session.Avatar.Stats.GetStat(StatId.Parse("abyss.skill.15")).Value,
            AppraisalSkill: (int)_session.Avatar.Stats.GetStat(StatId.Parse("abyss.skill.18")).Value,
            MaxPatience: 3);
    }

    /// <summary>The imported monetary value of one runtime item identity.</summary>
    private int ValueOf(string itemDefinition)
    {
        if (_items is null) return 0;
        string id = itemDefinition.StartsWith(UuItemDefinitions.ItemIdPrefix, StringComparison.Ordinal)
            ? itemDefinition[UuItemDefinitions.ItemIdPrefix.Length..]
            : itemDefinition;
        return int.TryParse(id, out int itemId) ? _items.Find(itemId)?.MonetaryValue ?? 0 : 0;
    }

    /// <summary>The item id a placed creature was admitted from.</summary>
    private int ItemIdOf(ActorState actor) => FallenItemId(actor);

    /// <summary>The whoami byte the creature's own placement record carries.</summary>
    private int WhoAmIOf(ActorState actor)
    {
        foreach (KeyValuePair<int, ActorState> placed in PlacedCritters)
        {
            if (placed.Value.DurableId != actor.DurableId) continue;
            return _placements?.Object(placed.Key)?.WhoAmI ?? 0;
        }

        return 0;
    }

    /// <summary>The conversation a creature without a whoami byte speaks, and the whoami that answers nothing.</summary>
    public const int GenericConversationBase = 256;
    public const int FirstCritterItemId = 64;
    public const int NoResponseWhoAmI = 255;

    /// <summary>The nearest fallen opponent within a hand's reach, if any.</summary>
    private ActorState? FallenOpponentInReach(WorldPoint position)
    {
        ActorState? nearest = null;
        float nearestDistance = InteractionReach;
        foreach (ActorState actor in PlacedCritters.Values)
        {
            if (!actor.IsDefeated) continue;
            float distance = actor.Position.HorizontalDistanceTo(position);
            if (distance > nearestDistance) continue;
            nearest = actor;
            nearestDistance = distance;
        }

        return nearest;
    }

    /// <summary>The item id a placed opponent was admitted from, for naming it.</summary>
    private int FallenItemId(ActorState actor)
    {
        foreach (KeyValuePair<int, ActorState> placed in PlacedCritters)
        {
            if (placed.Value.DurableId != actor.DurableId) continue;
            // A critter slot is an actor, not an item entity, so its item id
            // comes from the level's own placement record.
            return _placements?.Object(placed.Key)?.ItemId ?? 0;
        }

        return 0;
    }

    /// <summary>The nearest object the level placed, within a hand's reach.</summary>
    private AdmittedObject? PlacedObjectInReach(WorldPoint position)
    {
        AdmittedObject? nearest = null;
        float nearestDistance = InteractionReach;
        foreach (int index in _session.PlacedObjectIndexes(_session.Dungeon.CurrentLevel))
        {
            // What the level no longer admits is not in reach: a taken or
            // destroyed object has left the place it lay.
            if (!_session.Dungeon.Current.IsLive(index)) continue;
            if (_session.PlacedObjectAt(_session.Dungeon.CurrentLevel, index, out (int X, int Y) tile) is not { } obj)
                continue;
            // Only what lies on the floor is in reach: a container's contents
            // are reached by using the container.
            if (tile.X < 0) continue;
            AdmittedTile? on = _placements?.Tile(tile.X, tile.Y);
            if (on is null) continue;
            WorldPoint center = _placements!.TileCenter(on.X, on.Y, on.FloorHeight);
            float distance = center.HorizontalDistanceTo(position);
            // Out of reach, or farther than what is already in hand.
            if (distance > nearestDistance) continue;
            // A tie keeps the object the chain names first: that is the one on
            // top of what the tile carries, so a stack is taken from the top.
            if (nearest is not null && distance >= nearestDistance) continue;
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

        if (_levelItems is { } levelItems && _session.PlacedAdmission(level) is { } admittedLevel)
            levelItems.AdoptLevel(level, admittedLevel);

        WorldPoint anchor = AnchorPositionOf(definition);
        _player.Restore(anchor, DetachedMotion(anchor.Y));
        _camera.Update(_player);
        _outcome = $"You descend to level {level}.";
    }

    /// <summary>
    /// The Engine entity of a placed object on one level, whether it is held by
    /// an owner or still loose.
    /// </summary>
    public static EntityId? PlacedEntity(UuSession session, int level, int objectIndex) =>
        session.PlacedAdmission(level) is { } admission
        && admission.ByIndex.TryGetValue(objectIndex, out EntityId entity)
            ? entity
            : null;

    /// <summary>
    /// The rune a runestone lays on the shelf. The donor's item range for
    /// runestones is 232-255 (donor: src/objects/runestone.cs IsRunestone), and
    /// the shelf's order is that range's order.
    /// </summary>
    public static bool TryRuneIndex(int itemId, out int runeIndex)
    {
        runeIndex = itemId - FirstRunestoneItemId;
        return itemId >= FirstRunestoneItemId && itemId <= LastRunestoneItemId;
    }

    /// <summary>First and last item id the donor's runestone range covers.</summary>
    public const int FirstRunestoneItemId = 232;
    public const int LastRunestoneItemId = 255;

    /// <summary>Lays an owned runestone on the casting shelf so a spell can be cast from it.</summary>
    public void ShelfRune(int index) => _casting.ShelfRune(index);

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

    /// <summary>The conversation the avatar is in: the transcript so far, and the panel with it.</summary>
    public UuConversationHosting.TalkResult? Conversation =>
        _panel is null ? null : new UuConversationHosting.TalkResult(_transcript, _panel);

    /// <summary>The same conversation as the product-facing view a projection carries.</summary>
    private ConversationView? ConversationView => _panel is null
        ? null
        : new ConversationView(_speaker, _transcript, _panel.Prompts, _panel.Attitude, _panel.LastTrade ?? "");

    /// <summary>Who the avatar is speaking to, named from the conversation strings.</summary>
    private string _speaker = "";

    /// <summary>The imported item catalog this session reads names and mass from, when the bundle carries one.</summary>
    public UuItemCatalog? Items => _items;

    /// <summary>The level's objects as inventory: the floor, containers and what a critter carries.</summary>
    public UuLevelItems? LevelItems => _levelItems;

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
