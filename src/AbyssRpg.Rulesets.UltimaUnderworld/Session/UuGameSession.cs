using System.Numerics;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Kit.Inventory;
using System.Runtime.InteropServices;
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
    private readonly ProjectileFlights _projectiles = new();
    public IReadOnlyList<ProjectileFlight> Projectiles => _projectiles.Active;

    /// <summary>Melee reach in Engine units; a swing resolves against an opponent inside it.</summary>
    public const float MeleeReach = 6f;

    /// <summary>A hand's reach: what the use channel can name from where the avatar stands.</summary>
    public const float InteractionReach = 2.5f;

    /// <summary>A static door is majorclass 5 (donor: a_door_trap.cs searches for it).</summary>
    private const int DoorMajorClass = 5;

    /// <summary>A moving door is majorclass 7, minorclass 0, classindex 0xF, the donor's fallback.</summary>
    private const int MovingDoorMajorClass = 7;

    private const int MovingDoorClassIndex = 0xF;

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
            // The avatar is an owner from the start: an item it carries is not
            // re-placed by a level admitted later.
            _levelItems.Owner(session.Avatar.Actor.Entity);
            session.HeldItems = store;
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
                _combat.ChargeFraction, _player.YawRadians, _outcome, LightRadius);
            return new SessionStatus(
                _session.Dungeon.CurrentLevel,
                _session.Clock.ElapsedTicks,
                _avatarName,
                hud.Hp, hud.MaxHp, hud.Mana, hud.MaxMana, hud.ChargeFraction, hud.YawRadians, hud.WindIndex,
                _outcome, _session.Avatar.IsDefeated, _session.Swimming, _session.Flying,
                PresentActors, ConversationView, LightRadius,
                _session.Automap.TryGetValue(_session.Dungeon.CurrentLevel, out Kit.Knowledge.AutomapPage? mapped)
                    ? mapped.EncodePage()
                    : "",
                AvatarTile.X, AvatarTile.Y);
        }
    }

    private string _avatarName = "Avatar";

    private UuLevelPlacements? _placements;

    /// <summary>The imported levels this session may travel between, when the entry built one.</summary>
    private readonly UuLevelCatalog? _catalog;

    /// <summary>The imported item catalog, when the bundle carries one.</summary>
    private readonly UuItemCatalog? _items;

    private UuObjectTables? _objectTables;
    private (int Level, int X, int Y)? _triggerTile;

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

    /// <summary>What the avatar carries right now: the pack the Engine holds for it.</summary>
    public Rusty.Engine.Mechanics.InventoryView CarriedItems =>
        _session.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();

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
        long WorldSeed,
        string DefaultAvatarName);

    private static Prepared Prepare(GameSessionContext context, RulesetSavePayload? saved)
    {
        ResolvedGameComposition composition = context.Composition;
        UuTuningProfile tuning = UuTuningProfile.Read(
            composition.Tuning.Payload, $"tuning '{composition.Tuning.Id.Value}'");
        ContentPack avatarPack = composition.RequireContentPack(new ContentPackId("abyssrpg.avatar-options"));
        ContentPack classPack = composition.RequireContentPack(new ContentPackId("abyssrpg.classes"));

        // Decode before anything Engine-owned exists: a corrupt or foreign-level
        // save must fail without leaving a half-built world behind. The saved level
        // is the session's level, not the bundle's lowest: an autosave written deep
        // in the dungeon is a place to continue from, and the bundle's first level
        // is only where a new game starts.
        UuSessionSnapshot? savedSnapshot = saved is null
            ? null
            : UuSessionSnapshotCodec.Decode(saved.Bytes.ToArray());
        (ContentPack levelPack, int levelNumber) = RequireLevelPack(composition, savedSnapshot?.Level);

        UuLevelDefinition level = UuLevelContent.Read(levelPack.Payload, $"content pack '{levelPack.Id.Value}'");
        if (level.Level != levelNumber)
            throw new InvalidOperationException($"Level pack '{levelPack.Id.Value}' declares level {level.Level}.");

        UuCreationCatalog.Catalog creation = UuCreationCatalog.Read(
            avatarPack.Payload, classPack.Payload,
            $"content pack '{avatarPack.Id.Value}'", $"content pack '{classPack.Id.Value}'");
        // A new game plants its seed once and the save carries it, together with
        // the avatar creation produced: a load rebuilds that avatar and that seed
        // rather than rolling either again, so a save opened by another process
        // is the same character with the same maximum health.
        long worldSeed = savedSnapshot?.WorldSeed ?? Random.Shared.NextInt64();
        UuCreationFlow.CreationResult choices = savedSnapshot?.Avatar is { } savedAvatar
            ? RequireSavedAvatar(savedAvatar, creation)
            : UuCreationCatalog.RollDefault(creation, SeededRandom(worldSeed));
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
        Random rng = SeededRandom(prepared.WorldSeed);
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
            _objectTables = prepared.Tables,
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
                // The world the Engine owns -- what each owner holds, and the
                // state of the creatures standing on the level -- is restored
                // after the level's own delta, because a placement the save says
                // is gone has no actor or item to put anything back into.
                gameSession.RestoreWorld(savedSnapshot);
                player.Restore(
                    new WorldPoint(savedSnapshot.AvatarPose.X, savedSnapshot.AvatarPose.Y, savedSnapshot.AvatarPose.Z),
                    DetachedMotion(savedSnapshot.AvatarPose.Y));
                player.YawRadians = savedSnapshot.AvatarPose.YawRadians;
                // A load resumes the clock where the save stopped it; the span
                // before that was consumed by the session that wrote it.
                gameSession._consumedClock = session.Clock.ElapsedTicks;
                gameSession._triggerTile = (session.Dungeon.CurrentLevel, gameSession.AvatarTile.X, gameSession.AvatarTile.Y);
            }
            catch
            {
                gameSession.Dispose();
                throw;
            }
        }

        return gameSession;
    }

    /// <summary>The session's one random source, from the seed the save carries.</summary>
    private static Random SeededRandom(long seed) => new(unchecked((int)(seed ^ (seed >>> 32))));

    /// <summary>
    /// The avatar a save carries, refused when its shape does not fit the
    /// creation catalog: a truncated attribute or skill row would otherwise
    /// surface as an index overrun deep inside the avatar factory.
    /// </summary>
    private static UuCreationFlow.CreationResult RequireSavedAvatar(
        UuCreationFlow.CreationResult avatar, UuCreationCatalog.Catalog creation)
    {
        if (avatar.Attributes is not { Length: 3 } || avatar.Skills?.Length != UuSkillRolls.SkillCount)
            throw new InvalidOperationException("The save's avatar does not carry three attributes and every skill.");
        if (avatar.ClassIndex < 0 || avatar.ClassIndex >= creation.Tables.Classes.Count)
            throw new InvalidOperationException($"The save's avatar class {avatar.ClassIndex} is not in the class pack.");
        return avatar with { Name = avatar.Name ?? "" };
    }

    // The vitals policy reads the mana skill index; UW1 skill 8 governs casting.
    private const int SkillIndexForVitals = 8;

    private static (ContentPack Pack, int Level) RequireLevelPack(
        ResolvedGameComposition composition, int? requiredLevel = null)
    {
        ContentPack? best = null;
        int bestLevel = 0;
        foreach (ContentPack pack in composition.ContentPacks)
        {
            const string prefix = "abyssrpg.level-";
            if (!pack.Id.Value.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(pack.Id.Value[prefix.Length..], out int number) || number < 1) continue;
            if (requiredLevel is { } wanted && number != wanted) continue;
            if (best is null || number < bestLevel)
            {
                best = pack;
                bestLevel = number;
            }
        }

        if (best is null || bestLevel < 1)
        {
            throw new InvalidOperationException(requiredLevel is { } level
                ? $"Save is on level {level}; the bundle admits no level {level}. "
                    + "Import that level before loading this save."
                : "The default bundle carries no imported level pack. Run the operator import "
                    + "(scripts/import-level.sh) before launching, then rebuild.");
        }

        return (best, bestLevel);
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
            PublishScene(appearance);
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

    /// <summary>
    /// Publishes one description of the admitted scene: the level's own geometry
    /// and one fact per thing standing on it, each keyed by the durable identity
    /// the runtime assigns that record, so what is drawn and what is saved are the
    /// same world. Called when the level changes and whenever play changes what
    /// stands on it -- a thing taken, a door opened, an opponent struck down.
    /// </summary>
    private void PublishScene(Appearance? levelAppearance)
    {
        int level = _session.Dungeon.CurrentLevel;
        var facts = new List<AppearanceFact>
        {
            new(
                LevelObjectId,
                HasParentObject: false,
                ParentObjectId: 0,
                new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                levelAppearance ?? _appearance!,
                Visible: true,
                RenderLayer.Scene),
        };
        if (_session.PlacedAdmission(level) is { } admission)
        {
            HashSet<(int X, int Y)> open = [.. _session.Dungeon.Current.OpenedDoors];
            foreach ((int index, EntityId _) in admission.ByIndex.OrderBy(entry => entry.Key))
            {
                if (!admission.Objects.TryGetValue(index, out AdmittedObject? obj)) continue;
                // What the level state says is gone is not drawn; what a record
                // holds is inside it; and what the avatar carries is on them. The
                // inventory world answers the last of those, so it is the same
                // authority a save reads rather than a second list to keep.
                if (!_session.Dungeon.Current.IsLive(index)) continue;
                if (obj.Owner != 0) continue;
                if (admission.Holders.TryGetValue(index, out int heldBy) && heldBy != 0) continue;
                if (admission.ByIndex.TryGetValue(index, out EntityId drawn)
                    && IsCarriedOff(admission, level, drawn))
                {
                    continue;
                }

                // A mobile's tile lives on its own record when the level's tile list
                // does not carry it.
                if (!admission.Tiles.TryGetValue(index, out (int X, int Y) tile))
                {
                    if (obj.HomeTileX < 0 || obj.HomeTileY < 0) continue;
                    tile = (obj.HomeTileX, obj.HomeTileY);
                }
                AdmittedTile? placed = _placements?.Tile(tile.X, tile.Y);
                bool door = placed?.Door == true || UuObjectShape.IsDoorItem(obj.ItemId);
                bool openDoor = door && open.Contains((tile.X, tile.Y));
                UuObjectShape shape = UuObjectShape.Of(obj.ItemId, obj.Mobile, door);
                int band = _placements is not { } grid
                    ? 0
                    : BandAt(
                        grid.TileCenter(tile.X, tile.Y, placed?.FloorHeight ?? 0).X,
                        grid.TileCenter(tile.X, tile.Y, placed?.FloorHeight ?? 0).Z);
                if (band >= Presentation.UuLightBands.Hidden) continue;
                Appearance appearance = ObjectAppearance(shape, band);
                WorldPoint center = _placements?.TileCenter(tile.X, tile.Y, placed?.FloorHeight ?? 0)
                    ?? new WorldPoint((tile.X + 0.5f) * 8f, 0.92f, (tile.Y + 0.5f) * 8f);
                facts.Add(new AppearanceFact(
                    Identity.UuIdentityPolicy.LevelObjectIdentity(level, index).Value,
                    HasParentObject: false,
                    ParentObjectId: 0,
                    new Transform(
                        new Vector3(center.X, center.Y + (shape.Height / 2f), center.Z),
                        Quaternion.Identity,
                        new Vector3(shape.Width, shape.Height, shape.Depth)),
                    appearance,
                    // An open door stands aside; what stands in the light is drawn.
                    Visible: !openDoor,
                    RenderLayer.Scene));
            }
        }

        // Creatures are admitted as actors, not as level records, and they are drawn
        // where they stand now rather than where content placed them.
        if (_placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? creatures))
        {
            UuObjectShape creatureShape = UuObjectShape.Of(0, mobile: true, door: false);
            foreach (ActorState creature in creatures.Values)
            {
                // A fallen creature is a corpse where it fell, not a gap: it stays
                // drawn, lying down, until it is looted and its record is gone.
                UuObjectShape shape = creature.IsDefeated ? UuObjectShape.Corpse : creatureShape;
                int creatureBand = BandAt(creature.Position.X, creature.Position.Z);
                if (creatureBand >= Presentation.UuLightBands.Hidden) continue;
                facts.Add(new AppearanceFact(
                    checked((ulong)creature.DurableId),
                    HasParentObject: false,
                    ParentObjectId: 0,
                    new Transform(
                        new Vector3(
                            creature.Position.X,
                            creature.Position.Y + (shape.Height / 2f),
                            creature.Position.Z),
                        Quaternion.Identity,
                        new Vector3(shape.Width, shape.Height, shape.Depth)),
                    ObjectAppearance(shape, creatureBand),
                    Visible: true,
                    RenderLayer.Scene));
            }
        }

        foreach (ProjectileFlight flight in _projectiles.Active)
        {
            var shape = new UuObjectShape(UuObjectShapeKind.Projectile, .35f, .35f, .35f, new Color(1f, .4f, .05f, 1f));
            // Transient flight identities occupy a separate JSON-safe presentation range.
            facts.Add(new AppearanceFact((1UL << 40) + flight.Id, false, 0,
                new Transform(flight.Position, Quaternion.Identity, new Vector3(.35f)),
                ObjectAppearance(shape, 0), true, RenderLayer.Scene));
        }
        _context.Engine.Graphics.PublishSnapshot(CollectionsMarshal.AsSpan(facts));
        _publishedScene = SceneInputs();
    }

    /// <summary>
    /// Everything the published scene is drawn from. Removals are what play does
    /// to the level, so it is signed by how many records are still live rather
    /// than by the admission-time count; where things stand, where the avatar
    /// stands and how far its light reaches decide what is drawn and in which band,
    /// so they sign it too.
    /// </summary>
    private readonly record struct SceneSignature(
        int Level,
        int Objects,
        int Doors,
        int Carried,
        int Creatures,
        int Fallen,
        int Placement,
        int AvatarTileX,
        int AvatarTileY,
        int Light);

    private SceneSignature SceneInputs()
    {
        int level = _session.Dungeon.CurrentLevel;
        UuLevelState state = _session.Dungeon.Current;
        var placement = new HashCode();
        foreach (KeyValuePair<int, (int TileX, int TileY)> moved in state.MovedObjects.OrderBy(entry => entry.Key))
            placement.Add(moved);
        foreach (DroppedPlacement dropped in state.Dropped) placement.Add(dropped);
        if (_placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? creatures))
        {
            // Quantized to a tenth of a unit: a creature that moves is redrawn,
            // one that stands still is not redrawn by floating-point noise.
            foreach ((int index, ActorState creature) in creatures.OrderBy(entry => entry.Key))
            {
                placement.Add(index);
                placement.Add((int)MathF.Round(creature.Position.X * 10f));
                placement.Add((int)MathF.Round(creature.Position.Z * 10f));
            }
        }

        (int avatarX, int avatarY) = AvatarTile;
        return new SceneSignature(
            level,
            LiveObjectCount(level),
            state.OpenedDoors.Count,
            CarriedItems.UniqueItems.Count,
            StandingCreatureCount(level),
            FallenCreatureCount(level),
            placement.ToHashCode(),
            avatarX,
            avatarY,
            LightRadius);
    }


    /// <summary>
    /// How far the avatar's own light reaches, in tiles: the lamp they carry plus
    /// whatever a light spell adds. This is product state, and the drawn world and
    /// the automap are both consequences of it rather than separate systems.
    /// </summary>
    public int LightRadius
    {
        get
        {
            int radius = BaseLightRadius;
            // A light spell widens it while it holds; the maintained-spell owner
            // answers that, not a second timer here.
            if (_casting.MaintainsFamily(Magic.UuSpellCatalog.Family.Light)) radius += SpellLightBonus;

            return radius;
        }
    }

    /// <summary>Engine units per tile in the admitted level: the import's own scale.</summary>
    private float TileUnits => _placements?.UnitsPerTile is > 0 ? (float)_placements.UnitsPerTile : 8f;

    /// <summary>The light the avatar always carries: enough to see what is underfoot.</summary>
    public const int BaseLightRadius = 6;

    /// <summary>What a light spell adds to the radius while it holds.</summary>
    public const int SpellLightBonus = 8;

    /// <summary>Tiles the avatar has seen on the current level.</summary>
    public int MappedTiles => _session.Automap.TryGetValue(_session.Dungeon.CurrentLevel, out Kit.Knowledge.AutomapPage? page)
        ? page.MappedCount
        : 0;

    /// <summary>The tile the avatar stands on, for the map's view of the world.</summary>
    private (int X, int Y) AvatarTile => _player.Position is { } position
        ? ((int)Math.Floor(position.X / TileUnits), (int)Math.Floor(position.Z / TileUnits))
        : (0, 0);

    /// <summary>Tile events run once inside the admitted update, with exit before entry.</summary>
    private void TickTriggers()
    {
        if (_placements is null || _objectTables is null || _player.Position is not { } position) return;
        AdmittedTile? here = _placements.TileAt(position);
        if (here is null) return;
        int level = _session.Dungeon.CurrentLevel;
        var current = (Level: level, X: here.X, Y: here.Y);
        if (_triggerTile == current) return;
        var previous = _triggerTile;
        _triggerTile = current;
        if (previous is { } left && left.Level == level && _placements.Tile(left.X, left.Y) is { } oldTile)
            RunTileTriggers(oldTile, entering: false);
        if (_session.Dungeon.CurrentLevel == level && _player.Position == position)
            RunTileTriggers(here, entering: true);
    }

    private void RunTileTriggers(AdmittedTile tile, bool entering)
    {
        if (_placements is null || _objectTables is null) return;
        int level = _session.Dungeon.CurrentLevel;
        UuLevelState state = _session.Dungeon.Current;
        WorldPoint? startingPosition = _player.Position;
        int index = tile.ObjectHead;
        var visited = new HashSet<int>();
        while (index != 0 && visited.Add(index) && _placements.Object(index) is { } obj)
        {
            index = obj.Next;
            if (!state.IsLive(obj.Index) || !obj.AvatarTriggerEnabled || Traps.UuTriggerPolicy.Type(_objectTables, obj.ItemId) is not { } type) continue;
            bool pressure = type is 7 or 15;
            if (!entering && !pressure && obj.RepeatsTrigger) state.Release(obj.Index);
            long weight = pressure ? TileWeight(tile, obj.Height, entering) : 0;
            if (!Traps.UuTriggerPolicy.Fires(type, entering, weight, obj.PressureThreshold))
            {
                if (pressure && obj.RepeatsTrigger) state.Release(obj.Index);
                continue;
            }
            // Our one-shot state consumes this trigger, rather than deleting a
            // potentially shared linked effect chain as the donor does.
            if (!state.Fire(obj.Index)) continue;
            FireTrigger(obj);
            if (!entering && !pressure && obj.RepeatsTrigger) state.Release(obj.Index);
            // A pit can replace the admitted level. Never continue its old chain
            // against the new level's records.
            if (_session.Dungeon.CurrentLevel != level || _player.Position != startingPosition) return;
        }
    }

    private UuLevelPlacements? _pressurePlacements;
    private (AdmittedObject Trigger, AdmittedTile Tile)[] _pressureTiles = [];
    private readonly Dictionary<int, long> _pressureWeights = [];

    // Weight changes caused by taking, dropping or moving an object are observed
    // in the same admitted update as the ordinary interaction. No second clock.
    private void EnsurePressureState()
    {
        if (_placements is null || _objectTables is null) return;
        if (!ReferenceEquals(_pressurePlacements, _placements))
        {
            _pressurePlacements = _placements;
            _pressureWeights.Clear();
            var admission = _session.PlacedAdmission(_session.Dungeon.CurrentLevel);
            _pressureTiles = _placements.Objects
                .Where(o => o.AvatarTriggerEnabled && Traps.UuTriggerPolicy.Type(_objectTables, o.ItemId) is 7 or 15)
                .Select(o => (Trigger: o, Tile: admission is not null && admission.Tiles.TryGetValue(o.Index, out var at)
                    ? _placements.Tile(at.X, at.Y) : null))
                .Where(pair => pair.Tile is not null)
                .Select(pair => (pair.Trigger, pair.Tile!)).ToArray();
            foreach (var (trigger, tile) in _pressureTiles)
                _pressureWeights[trigger.Index] = TileWeight(tile, trigger.Height, AvatarTile == (tile.X, tile.Y));
        }
    }

    private void TickPressureChanges()
    {
        EnsurePressureState();
        if (_objectTables is null) return;
        var state = _session.Dungeon.Current;
        int level = state.LevelNumber;
        foreach (var (trigger, tile) in _pressureTiles)
        {
            if (!state.IsLive(trigger.Index)) continue;
            long weight = TileWeight(tile, trigger.Height, AvatarTile == (tile.X, tile.Y));
            bool changed = _pressureWeights.TryGetValue(trigger.Index, out long previous) && previous != weight;
            _pressureWeights[trigger.Index] = weight;
            if (!changed) continue;
            int type = Traps.UuTriggerPolicy.Type(_objectTables, trigger.ItemId)!.Value;
            if (!Traps.UuTriggerPolicy.Fires(type, true, weight, trigger.PressureThreshold))
            {
                if (trigger.RepeatsTrigger) state.Release(trigger.Index);
                continue;
            }
            if (state.Fire(trigger.Index)) FireTrigger(trigger);
            if (_session.Dungeon.CurrentLevel != level) return;
        }
    }

    private long TileWeight(AdmittedTile tile, double height, bool avatarPresent)
    {
        if (_placements is null || _items is null) return 0;
        int level = _session.Dungeon.CurrentLevel;
        long weight = 0;
        int index = tile.ObjectHead;
        var visited = new HashSet<int>();
        UuEntityAdmission.Admission? admission = _session.PlacedAdmission(level);
        while (index != 0 && visited.Add(index) && _placements.Object(index) is { } obj)
        {
            index = obj.Next;
            if (!_session.Dungeon.Current.IsLive(obj.Index) || (height != 0 && obj.Height != height)) continue;
            if (_session.Dungeon.Current.MovedObjects.TryGetValue(obj.Index, out var moved)
                && moved != (tile.X, tile.Y)) continue;
            if (admission is not null && admission.ByIndex.TryGetValue(obj.Index, out EntityId entity)
                && IsCarriedOff(admission, level, entity)) continue;
            weight += Traps.UuTriggerPolicy.ObjectWeight(obj, _items);
        }

        foreach (var moved in _session.Dungeon.Current.MovedObjects)
        {
            if (moved.Value != (tile.X, tile.Y) || visited.Contains(moved.Key)
                || !_session.Dungeon.Current.IsLive(moved.Key)
                || _placements.Object(moved.Key) is not { } obj) continue;
            if (height != 0 && height != tile.FloorHeight) continue;
            if (admission is not null && admission.ByIndex.TryGetValue(obj.Index, out EntityId entity)
                && IsCarriedOff(admission, level, entity)) continue;
            weight += Traps.UuTriggerPolicy.ObjectWeight(obj, _items);
        }
        foreach (DroppedPlacement dropped in _session.Dungeon.Current.Dropped)
            if ((dropped.TileX, dropped.TileY) == (tile.X, tile.Y) && (height == 0 || height == tile.FloorHeight))
                weight += (long)dropped.Quantity * (_items.Find(dropped.ItemId)?.MassTenthStones ?? 0);

        if (avatarPresent && (height == 0 || tile.FloorHeight == height))
        {
            // Our avatar uses the catalog's adventurer mass. This differs from
            // the donor's player object plus PlayerWeightPlusObjectInHand;
            // there is no separate cursor-held object in this product.
            weight += _items.Find(UuAvatarFactory.BodyItemId)?.MassTenthStones ?? 0;
            weight += InventoryWeight(_session.Avatar.Actor.Entity, new HashSet<ulong>());
        }
        return weight;
    }

    private long InventoryWeight(EntityId owner, HashSet<ulong> visited)
    {
        if (_levelItems is null || _items is null || !visited.Add(owner.Value)
            || _session.HeldItems?.TryGetInventory(owner, out _) != true) return 0;
        long weight = 0;
        foreach (Rusty.Engine.Mechanics.UniqueInventoryItem item in _levelItems.Read(owner).UniqueItems)
        {
            string definition = item.Definition.Value;
            if (definition.StartsWith(UuItemDefinitions.ItemIdPrefix, StringComparison.Ordinal)
                && int.TryParse(definition.AsSpan(UuItemDefinitions.ItemIdPrefix.Length), out int itemId))
            {
                int quantity = 1;
                Kit.World.DurableIdentityReference identity = _session.Directory.IdentityOf(item.Entity);
                if (identity.Value < Identity.UuIdentityPolicy.FirstDynamicItemId)
                {
                    int sourceLevel = (int)(identity.Value / 1024);
                    int sourceIndex = (int)(identity.Value % 1024);
                    quantity = _catalog?.Placements(sourceLevel)?.Object(sourceIndex)?.Quantity ?? 1;
                }
                weight += (long)quantity * (_items.Find(itemId)?.MassTenthStones ?? 0);
            }
            weight += InventoryWeight(item.Entity, visited);
        }
        return weight;
    }

    /// <summary>
    /// Fires one trigger's chain of linked effects. The chain walk is the trap
    /// owner's; what each fired kind does is applied through the owner that already
    /// does it, so there is no second implementation of opening a door.
    /// </summary>
    private void FireTrigger(AdmittedObject trigger) =>
        FireEffects(trigger.Link, trigger.Quality, trigger.Owner);

    private void FireEffects(int head, int tileX, int tileY)
    {
        // RunTrapFromTrigger in the donor reads the destination tile from the
        // trigger's quality/owner, which need not be the tile holding the leg.
        AdmittedObject? teleport = null;
        string? message = null;
        FireChainFrom(head, tileX, tileY, [], trap => teleport = trap, text => message = text);
        // The donor queues teleportation until the source chain has finished.
        if (teleport is not null) ApplyTeleportTrap(teleport);
        if (!string.IsNullOrEmpty(message))
            _outcome = teleport is null ? message : $"{_outcome} {message}";
    }

    private void ApplyTeleportTrap(AdmittedObject trap)
    {
        // a_teleport_trap.cs and teleportation.cs TeleportUW1: zero means current
        // level, otherwise the one-based level number; quality/owner are the tile.
        int level = trap.DestinationLevel == 0 ? _session.Dungeon.CurrentLevel : trap.DestinationLevel;
        UuLevelPlacements? destination;
        try { destination = _catalog?.Placements(level); }
        catch (InvalidOperationException)
        {
            _outcome = $"The teleport destination level {level} is not in this bundle.";
            return;
        }
        if (destination?.Tile(trap.Quality, trap.Owner) is not { } tile || !UuTileKind.IsOpen(tile))
        {
            _outcome = $"The teleport destination ({trap.Quality},{trap.Owner}) on level {level} is not open.";
            return;
        }
        if (level != _session.Dungeon.CurrentLevel) TravelToLevel(level, 0);
        PlaceOnTile(trap.Quality, trap.Owner);
        _outcome = $"You teleport to ({trap.Quality},{trap.Owner}) on level {level}.";
    }

    /// <summary>
    /// Runs one chain of a trigger's graph and applies what each node does, using
    /// the link of the node that carries the effect rather than the head's: a
    /// chain's later nodes name their own targets (donor: trap.FireChain and the
    /// trigger legs it hands back to their caller).
    /// </summary>
    private void FireChainFrom(int head, int tileX, int tileY, HashSet<int> visited, Action<AdmittedObject> teleport, Action<string> showText)
    {
        int level = _session.Dungeon.CurrentLevel;
        IReadOnlyList<(int Index, Traps.UuTrapDispatch.ChainNode Node, Traps.UuTrapDispatch.TrapKind Kind)> fired =
            Traps.UuTrapDispatch.FireChainNodes(
                index => _placements?.Object(index) is { } linked
                    ? new Traps.UuTrapDispatch.ChainNode(
                        Traps.UuTrapDispatch.ClassifyItem(linked.ItemId), linked.Link, linked.Next)
                    : new Traps.UuTrapDispatch.ChainNode(Traps.UuTrapDispatch.TrapKind.Unknown, 0, 0),
                head);

        foreach ((int index, Traps.UuTrapDispatch.ChainNode node, Traps.UuTrapDispatch.TrapKind kind) in fired)
        {
            if (!visited.Add(index)) return;
            switch (kind)
            {
                case Traps.UuTrapDispatch.TrapKind.Door:
                    ApplyDoorTrap(index, tileX, tileY);
                    break;
                case Traps.UuTrapDispatch.TrapKind.Pit:
                    FallThroughPit(tileX, tileY);
                    break;
                case Traps.UuTrapDispatch.TrapKind.Damage:
                    if (_placements?.Object(index) is { } struck)
                    {
                        ApplyDamageTrap(struck);
                    }

                    break;
                case Traps.UuTrapDispatch.TrapKind.TriggerLeg:
                    if (_placements?.Object(index) is { } leg)
                        FireChainFrom(node.Link, leg.Quality, leg.Owner, visited, teleport, showText);
                    break;
                case Traps.UuTrapDispatch.TrapKind.Teleport:
                    if (_placements?.Object(index) is { } destination) teleport(destination);
                    break;
                case Traps.UuTrapDispatch.TrapKind.TextString:
                    // UW1 a_text_string_trap.cs: block 9, sixty-four entries per level.
                    if (_placements?.Object(index) is { } textTrap)
                        showText(_strings?.Text(9, 64 * (level - 1) + textTrap.Owner) ?? "");
                    break;
                case Traps.UuTrapDispatch.TrapKind.Spell:
                    if (_placements?.Object(index) is { } spellTrap)
                    {
                        Track health = _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack);
                        var cast = _casting.CastTrap(spellTrap.Quality, spellTrap.Owner,
                            (int)(health.MaximumValue - health.Current), _session.Clock.ElapsedTicks, _tuning.ClockTicksPerSecond);
                        if (cast.ProjectileItem != 0)
                        {
                            LaunchTrapProjectile(spellTrap, cast.ProjectileItem, tileX, tileY);
                        }
                        else if (cast.MaintainedRunes is not null)
                            _outcome = "A trap lights your surroundings.";
                        else if (cast.Supported)
                        {
                            health.Restore(cast.Healing);
                            _outcome = $"A healing trap restores {cast.Healing} health.";
                        }
                        else _outcome = $"Trap spell {spellTrap.Quality}:{spellTrap.Owner} is not supported yet.";
                    }
                    break;
                case Traps.UuTrapDispatch.TrapKind.Null:
                    break;
                default:
                    showText($"Trap effect {kind} is not supported yet.");
                    return;
            }
            if (_session.Dungeon.CurrentLevel != level) return;
        }
    }

    /// <summary>
    /// A damage trap strikes the avatar. Its own record carries what it does: the
    /// trap's quality is the base damage and a non-zero owner means it poisons
    /// rather than wounds (donor: UnderworldGodot
    /// <c>src/traps/a_damagetrap.cs</c> Activate -> ApplyDamageTrap, which reads
    /// <c>trapObj.quality</c> and <c>trapObj.owner != 0</c>).
    /// </summary>
    private void ApplyDamageTrap(AdmittedObject trap)
    {
        int damage = trap.Quality;
        if (damage <= 0) return;
        if (trap.Owner != 0)
        {
            // A trap with an owner poisons rather than wounds, and the poison it
            // leaves is capped at fifteen (donor: a_damagetrap.cs sets
            // play_poison to min(basedamage, 0xF)). The survival owner carries it,
            // so the damage it does over time is that owner's business, not this
            // trap's.
            int poisoned = Math.Max(_session.Survival.Poison, Math.Min(damage, 0xF));
            _session.Survival.Poison = poisoned;
            _outcome = $"A trap's poison burns you: poison {poisoned}.";
            return;
        }

        ApplyDefeatDamage(damage, $"A trap strikes you for {damage}.");
    }

    /// <summary>
    /// A pit drops the avatar a level, through the travel path the session already
    /// has, and the dungeon clock pays for the fall. The avatar lands on the tile it
    /// fell from when that tile is open below, and on the nearest open tile when it
    /// is not -- a pit whose mouth is walled in below must not drop the avatar into
    /// geometry.
    /// </summary>
    private void FallThroughPit(int tileX, int tileY)
    {
        int below = _session.Dungeon.CurrentLevel + 1;
        try
        {
            _catalog?.Definition(below);
        }
        catch (InvalidOperationException)
        {
            // Nothing imported below: the pit is a hole the bundle cannot follow the
            // avatar through, and the refusal says which level is missing.
            _outcome = $"The pit drops away into level {below}, which this bundle does not carry.";
            return;
        }

        TravelToLevel(below, Time.UuClockPolicy.FallCostTicks);
        (int X, int Y) landing = NearestOpenTile(below, tileX, tileY);
        PlaceOnTile(landing.X, landing.Y);
        _outcome = $"You fall through the pit to level {below}.";
    }

    /// <summary>The tile fallen from when it is open, or the nearest open one to it.</summary>
    private (int X, int Y) NearestOpenTile(int level, int tileX, int tileY)
    {
        UuLevelPlacements? placements = _catalog?.Placements(level) ?? _placements;
        if (placements is null) return (tileX, tileY);
        const int Search = 8;
        for (int ring = 0; ring <= Search; ring++)
        {
            for (int y = tileY - ring; y <= tileY + ring; y++)
            {
                for (int x = tileX - ring; x <= tileX + ring; x++)
                {
                    // Only the ring's edge is new at each step.
                    if (ring > 0 && Math.Abs(x - tileX) != ring && Math.Abs(y - tileY) != ring) continue;
                    if (placements.Tile(x, y) is not { } tile || !Dungeon.UuTileKind.IsOpen(tile)) continue;
                    return (x, y);
                }
            }
        }

        return (tileX, tileY);
    }

    /// <summary>A door trap opens, closes or toggles the door its own link names.</summary>
    private void ApplyDoorTrap(int trapIndex, int tileX, int tileY)
    {
        int level = _session.Dungeon.CurrentLevel;
        UuLevelState state = _session.Dungeon.Current;
        if (_placements?.Object(trapIndex) is not { } trap) return;

        // The trap's own quality is the action: 1 opens, 2 closes, 3 toggles.
        Traps.UuTrapDispatch.DoorTrapAction action = Traps.UuTrapDispatch.DoorAction(trap.Quality);
        if (action == Traps.UuTrapDispatch.DoorTrapAction.None) return;

        // A door trap does not name its door: it searches the object chain of the
        // tile the trigger fired on for a door (donor: UnderworldGodot
        // src/traps/a_door_trap.cs Activate, which looks up
        // Tiles[triggerX, triggerY].indexObjectList for majorclass 5, or the moving
        // door at majorclass 7 minorclass 0 classindex 0xF). That tile is the one
        // the avatar is standing on, and the door stands in it.
        if (!TileHasDoor(level, tileX, tileY)) return;

        bool open = action switch
        {
            Traps.UuTrapDispatch.DoorTrapAction.Open => true,
            Traps.UuTrapDispatch.DoorTrapAction.Close => false,
            _ => !state.OpenedDoors.Contains((tileX, tileY)),
        };
        state.SetDoor(tileX, tileY, open);
        _outcome = open ? "A mechanism opens the door." : "A mechanism closes the door.";
    }

    /// <summary>
    /// Whether a tile's own object chain carries a door: a static door is the
    /// donor's majorclass 5 minorclass 0, and a moving door is majorclass 7,
    /// minorclass 0, classindex 0xF.
    /// </summary>
    private bool TileHasDoor(int level, int tileX, int tileY)
    {
        if (_placements?.Tile(tileX, tileY) is not { } tile) return false;
        int index = tile.ObjectHead;
        var seen = new HashSet<int>();
        while (index != 0 && seen.Add(index))
        {
            if (_session.PlacedObjectAt(level, index, out _) is not { } obj) return false;
            int item = obj.ItemId;
            int major = item >> 6;
            int minor = (item & 0x30) >> 4;
            if (major == DoorMajorClass && minor == 0) return true;
            if (major == MovingDoorMajorClass && minor == 0 && (item & 0xF) == MovingDoorClassIndex) return true;
            index = obj.Next;
        }

        return false;
    }

    /// <summary>Records what the avatar's light reaches on this level's map.</summary>
    private void RevealAroundAvatar()
    {
        if (_player.Position is not { } position) return;
        int tileX = (int)Math.Floor(position.X / TileUnits);
        int tileY = (int)Math.Floor(position.Z / TileUnits);
        if (!_session.Automap.TryGetValue(_session.Dungeon.CurrentLevel, out Kit.Knowledge.AutomapPage? page))
            _session.Automap[_session.Dungeon.CurrentLevel] = page = new Kit.Knowledge.AutomapPage();
        // Only what the avatar can see goes on the map: a disc would draw the far
        // side of a wall the player has never looked at.
        for (int y = tileY - LightRadius; y <= tileY + LightRadius; y++)
        {
            for (int x = tileX - LightRadius; x <= tileX + LightRadius; x++)
            {
                if (x < 0 || y < 0 || x >= Kit.Knowledge.AutomapPage.Dimension || y >= Kit.Knowledge.AutomapPage.Dimension)
                    continue;
                if (SeesTile(x, y)) page.Reveal(x, y);
            }
        }

    }

    /// <summary>Which light band a world position falls in; the outer band is darkness.</summary>
    private int BandAt(float x, float z)
    {
        if (_player.Position is not { } position) return 0;
        float dx = x - position.X;
        float dz = z - position.Z;
        int band = Presentation.UuLightBands.Band(
            MathF.Sqrt((dx * dx) + (dz * dz)), LightRadius * TileUnits);
        // A light standing on the floor lights the room around it too, so what is
        // near one is drawn even when the avatar has walked away from it.
        foreach ((WorldPoint where, int reach) in PlacedLights())
        {
            float lightX = x - where.X;
            float lightZ = z - where.Z;
            int near = Presentation.UuLightBands.Band(
                MathF.Sqrt((lightX * lightX) + (lightZ * lightZ)), reach * TileUnits);
            if (near < band) band = near;
        }

        if (band >= Presentation.UuLightBands.Hidden) return band;

        // Light does not pass through walls: what is behind one is not drawn, however
        // close the lamp is to it.
        return SeesTile((int)Math.Floor(x / TileUnits), (int)Math.Floor(z / TileUnits))
            ? band
            : Presentation.UuLightBands.Hidden;
    }

    /// <summary>
    /// The lights standing on this level: admitted objects whose item id is a light
    /// that is burning, with the radius the item catalog gives that item.
    /// </summary>
    private IEnumerable<(WorldPoint Where, int Reach)> PlacedLights()
    {
        if (_placements is null) yield break;
        if (_session.PlacedAdmission(_session.Dungeon.CurrentLevel) is not { } admission) yield break;
        foreach ((int index, AdmittedObject obj) in admission.Objects)
        {
            if (!Dungeon.UuLightSources.IsLit(obj.ItemId)) continue;
            if (!_session.Dungeon.Current.IsLive(index)) continue;
            if (admission.Holders.TryGetValue(index, out int heldBy) && heldBy != 0) continue;
            if (!admission.Tiles.TryGetValue(index, out (int X, int Y) tile)) continue;
            // The item catalog gives each light its own radius; a light the catalog
            // does not define reaches the default rather than nothing.
            int reach = _items?.Find(obj.ItemId)?.Radius is int radius and > 0
                ? radius
                : DefaultLightReach;
            AdmittedTile? placed = _placements.Tile(tile.X, tile.Y);
            yield return (_placements.TileCenter(tile.X, tile.Y, placed?.FloorHeight ?? 0), reach);
        }
    }

    /// <summary>What a burning light on the floor reaches, in tiles.</summary>
    public const int DefaultLightReach = 4;

    /// <summary>Whether the avatar has an unobstructed line to a tile.</summary>
    private bool SeesTile(int tileX, int tileY)
    {
        if (_placements is null || _player.Position is not { } position) return true;
        int fromX = (int)Math.Floor(position.X / TileUnits);
        int fromY = (int)Math.Floor(position.Z / TileUnits);
        HashSet<(int X, int Y)> open = [.. _session.Dungeon.Current.OpenedDoors];
        return Dungeon.UuSight.HasLineOfSight(fromX, fromY, tileX, tileY, (x, y) =>
        {
            AdmittedTile? tile = _placements.Tile(x, y);
            // Outside the level, solid, or a shut door: sight stops there. An open
            // door is a doorway.
            if (tile is null || !Dungeon.UuTileKind.IsOpen(tile)) return true;
            return tile.Door && !open.Contains((x, y));
        });
    }

    /// <summary>Whether the avatar carries an admitted object away from its level.</summary>
    private bool IsCarriedOff(UuEntityAdmission.Admission admission, int level, EntityId item) =>
        _session.HeldItems is { } held
        && held.TryGetContainer(item, out EntityId holder)
        && !LevelOwns(admission, level, holder);

    /// <summary>Whether an owner belongs to the level: its floor, its records, its creatures.</summary>
    private bool LevelOwns(UuEntityAdmission.Admission admission, int level, EntityId owner) =>
        owner.Value == _levelItems?.Floor(level).Value
        || admission.ByIndex.Values.Any(candidate => candidate.Value == owner.Value)
        || (_placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? actors)
            && actors.Values.Any(actor => actor.Actor.Entity.Value == owner.Value));

    /// <summary>How many of a level's creatures have been struck down.</summary>
    private int FallenCreatureCount(int level) =>
        _placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? fallen)
            ? fallen.Values.Count(creature => creature.IsDefeated)
            : 0;

    /// <summary>How many of a level's creatures are still standing.</summary>
    private int StandingCreatureCount(int level) =>
        _placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? creatures)
            ? creatures.Values.Count(creature => !creature.IsDefeated)
            : 0;

    /// <summary>How many of a level's admitted records are still standing.</summary>
    private int LiveObjectCount(int level) =>
        _session.PlacedAdmission(level) is { } admission
            ? admission.Objects.Keys.Count(_session.Dungeon.Current.IsLive)
            : 0;

    /// <summary>The appearance of one shape class, created once and shared.</summary>
    private Appearance ObjectAppearance(UuObjectShape shape, int band)
    {
        var key = (shape.Class, band);
        if (_objectAppearances.TryGetValue(key, out Appearance? existing)) return existing;
        Appearance created = _context.Engine.Graphics.CreatePrimitive(new PrimitiveAppearanceRequest(
            shape.Class is UuObjectShapeKind.Rune ? PrimitiveGeometry.Sphere : PrimitiveGeometry.Cube,
            Wireframe: false,
            Presentation.UuLightBands.Shade(shape.Color, band)));
        _objectAppearances[key] = created;
        return created;
    }

    private bool IsContainerItem(int itemId) => Content.UuObjectTablesContent.IsContainerItem(itemId);

    private readonly Dictionary<(UuObjectShapeKind Class, int Band), Appearance> _objectAppearances = [];

    /// <summary>What the published scene was drawn from, so it is redrawn only when it changes.</summary>
    private SceneSignature? _publishedScene;

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
        // Everything timed in the step runs on the dungeon clock: the span since
        // the last step, which is this step's own time plus whatever a fall, a
        // journey or a rest spent since then.
        double elapsed = DungeonSecondsSinceLastStep();

        EnsurePressureState();
        UuLocomotionPolicy.UuPlayerStep step = _locomotion.BeginPlayerStep(
            _player, update.Input, (float)seconds, canMove: true, _session.Swimming, _session.Flying);
        StepLocomotion(step, update, seconds);
        TickTriggers();
        TickProjectiles((float)elapsed);
        TickAttack(step, elapsed);
        if (step.UsePressed && !_session.Flying) Interact();
        if (step.LookPressed) Examine();
        TickPressureChanges();
        _casting.Upkeep(_session.Clock.ElapsedTicks);
        TickSurvival(elapsed);
        _camera.Update(_player);
        RevealAroundAvatar();
        // What stands on the level changes when play takes, opens, strikes or
        // walks: the scene is one description, so it is republished once, after
        // this step's changes, whenever its inputs differ.
        if (_scenePublished && _appearance is not null && _publishedScene != SceneInputs())
            PublishScene(_appearance);

        // A defeated avatar asks for the dead mode on every step it is still
        // played: a mode the Engine lifecycle restored over it is asked again.
        if (_session.Avatar.IsDefeated) _pendingMode = ProductMode.Dead;

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

    private void LaunchProjectile(int item, ulong source, Vector3 position, Vector3 direction)
    {
        if (_objectTables is null) return;
        int damage = UuStrikeResolution.RollDamage(
            UuMissilePolicy.ProjectileMaximum(_objectTables.ProjectileDamage[item - 16]), _rng);
        _projectiles.Launch(source, item, damage, position,
            Vector3.Normalize(direction) * _tuning.ProjectileSpeed, _tuning.ProjectileLifetimeSeconds);
    }

    private void LaunchTrapProjectile(AdmittedObject trap, int item, int targetX, int targetY)
    {
        if (_placements is null) return;
        // Static caster coordinates come from its tile chain and normalized local
        // offsets (donor motion_projectile.cs PrepareProjectileObject).
        int x = targetX, y = targetY;
        if (_session.PlacedAdmission(_session.Dungeon.CurrentLevel) is { } admission
            && admission.Tiles.TryGetValue(trap.Index, out var tile)) (x, y) = tile;
        Vector3 position = new((float)((x + trap.TileOffsetX) * _placements.UnitsPerTile),
            (float)(trap.Height * _placements.HeightUnitsPerStep),
            (float)((y + trap.TileOffsetY) * _placements.UnitsPerTile));
        float heading = trap.Heading * MathF.Tau / 8f;
        LaunchProjectile(item, 0, position, new Vector3(MathF.Sin(heading), 0, MathF.Cos(heading)));
        _outcome = "A trap releases a spell.";
    }

    private void TickProjectiles(float seconds)
    {
        bool hadFlights = _projectiles.Active.Count > 0;
        if (!hadFlights) return;
        var colliders = new List<SpatialEntityCollider>
        {
            _movement.ProjectCharacterCollider(_player, (ulong)_session.Avatar.DurableId),
        };
        foreach (ActorState actor in PlacedCritters.Values)
        {
            if (actor.IsDefeated) continue;
            Vector3 at = actor.Position.ToVector();
            UuObjectShape shape = UuObjectShape.Of(0, true, false);
            colliders.Add(new SpatialEntityCollider((ulong)actor.DurableId,
                at - new Vector3(shape.Width / 2, 0, shape.Depth / 2),
                at + new Vector3(shape.Width / 2, shape.Height, shape.Depth / 2),
                0, 0, true, false, false));
        }
        _projectiles.Step(seconds, _movement, colliders, (flight, hit) =>
        {
            if (hit.Entity == (ulong)_session.Avatar.DurableId)
                ApplyDefeatDamage(flight.Damage, $"A spell strikes you for {flight.Damage}.");
            else if (PlacedCritters.Values.FirstOrDefault(a => (ulong)a.DurableId == hit.Entity) is { } target)
            {
                UuCombatHosting.StrikeOutcome strike = UuCombatHosting.ApplyImpact(target.Stats, flight.Damage);
                if (strike.TargetDefeated) RecordDefeat(target);
                _outcome = $"The spell strikes for {flight.Damage}.";
            }
            else _outcome = "The spell strikes the dungeon.";
        });
        if (_appearance is not null) PublishScene(_appearance);
    }

    /// <summary>
    /// Dungeon seconds since the previous step consumed the clock. The clock's
    /// sub-tick carry is part of the reading, so an ordinary step reads exactly
    /// its own length rather than a whole-tick approximation of it.
    /// </summary>
    private double DungeonSecondsSinceLastStep()
    {
        double now = _session.Clock.ElapsedTicks + _tickCarry;
        double elapsed = Math.Max(0d, now - _consumedClock) / _tuning.ClockTicksPerSecond;
        _consumedClock = now;
        return elapsed;
    }

    /// <summary>The clock reading, in ticks, that timed systems have consumed up to.</summary>
    private double _consumedClock;

    private void TickAttack(UuLocomotionPolicy.UuPlayerStep step, double seconds)
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

        // One resolution decides what is in reach and what it accepts; the verbs
        // below call their owning policy and nothing scans the level for itself.
        UuReachTarget target = InReach(position);
        switch (target.Kind)
        {
            // A living creature is talked to: the conversation owner runs the script
            // its own record names, and the transcript is what the projection shows.
            case UuReachKind.Creature when target.Actor is { } talker:
                Talk(talker);
                return;

            // A fallen opponent is looted where it lies: what it carried is held by
            // its own record, so the same transfer serves a corpse and a container.
            case UuReachKind.Corpse when target.Actor is { } fallen:
                LootFallen(fallen);
                return;

            // A door tile opens and closes through the level state the save carries,
            // so the change survives a load.
            case UuReachKind.Door when target.Door is { } door:
                bool open = !_session.Dungeon.Current.OpenedDoors.Contains((door.X, door.Y));
                _session.Dungeon.Current.SetDoor(door.X, door.Y, open);
                _outcome = open ? "You open the door." : "You close the door.";
                // UW1 door.cs dispatches OPEN (7); its CLOSE dispatch is UW2-only.
                if (open)
                {
                    int doorIndex = door.ObjectHead;
                    var seen = new HashSet<int>();
                    while (doorIndex != 0 && seen.Add(doorIndex) && _placements.Object(doorIndex) is { } doorObject)
                    {
                        if (doorObject.ItemId is >= 320 and <= 335) RunObjectTriggers(doorObject, 7);
                        doorIndex = doorObject.Next;
                    }
                }
                return;

            case UuReachKind.Container or UuReachKind.Item when target.Object is { } placed:
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
    private void Examine()
    {
        if (_player.Position is not { } position) return;
        UuReachTarget target = InReach(position);
        _outcome = target.Object is { } obj ? $"You examine the {Describe(obj.ItemId)}."
            : target.Kind == UuReachKind.Door ? "You examine the door." : "There is nothing here to examine.";
        if (target.Object is { } placed) RunObjectTriggers(placed, 5);
    }

    private bool RunObjectTriggers(AdmittedObject used, int type)
    {
        if (_placements is null || _objectTables is null || used.LinkIsQuantity) return false;
        int level = _session.Dungeon.CurrentLevel;
        var state = _session.Dungeon.Current;
        var seen = new HashSet<int>();
        int index = used.Link;
        bool fired = false;
        // trigger.cs TriggerObjectLink walks next links past locks/contents to
        // matching trigger records; link is not an object reference for quantities.
        while (index != 0 && seen.Add(index) && _placements.Object(index) is { } trigger)
        {
            index = trigger.Next;
            if (!state.IsLive(trigger.Index)) continue;
            if (type == 4 && trigger.Flags == 0 && trigger.ItemId is >= 384 and < 416
                && Traps.UuTrapDispatch.ClassifyItem(trigger.ItemId) != Traps.UuTrapDispatch.TrapKind.Door)
            {
                if (!state.Fire(trigger.Index)) continue;
                var at = _session.PlacedAdmission(level) is { } admission && admission.Tiles.TryGetValue(used.Index, out var tile)
                    ? tile : AvatarTile;
                FireEffects(trigger.Index, at.Item1, at.Item2);
                return true;
            }
            if (!trigger.AvatarTriggerEnabled || Traps.UuTriggerPolicy.Type(_objectTables, trigger.ItemId) != type) continue;
            if (type == 5 && trigger.SearchDifficulty > 0)
            {
                int search = (int)_session.Avatar.Stats.GetStat(StatId.Parse($"abyss.skill.{UuSkillCatalog.SearchIndex}")).Value;
                if (Creation.UuSkillRolls.SkillCheck(search, trigger.SearchDifficulty, _rng) <= Creation.UuSkillRolls.CheckResult.Fail)
                    continue;
            }
            if (trigger.RepeatsTrigger) state.Release(trigger.Index);
            if (!state.Fire(trigger.Index)) continue;
            FireTrigger(trigger);
            fired = true;
            if (_session.Dungeon.CurrentLevel != level) break;
        }
        return fired;
    }

    private void Use(AdmittedObject placed)
    {
        if (RunObjectTriggers(placed, 4)) return;
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

        if (_items?.Find(placed.ItemId)?.CanPickUp != true)
        {
            _outcome = $"You cannot take the {Describe(placed.ItemId)}.";
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
            RunObjectTriggers(placed, 2);
            return;
        }

        _outcome = $"You take the {Describe(placed.ItemId)}.";
        RunObjectTriggers(placed, 2);
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
            _rng,
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

    /// <summary>Loots a fallen opponent through the inventory owner that holds what it carried.</summary>
    private void LootFallen(ActorState fallen)
    {
        if (_levelItems is not { } carried)
        {
            _outcome = $"The {Describe(FallenItemId(fallen))} carries nothing.";
            return;
        }

        InventoryContainerTransferReceipt looted = carried.Loot(fallen.Actor.Entity, _session.Avatar.Actor.Entity);
        int count = looted.UniqueItems.Count;
        _outcome = count == 0
            ? $"The {Describe(FallenItemId(fallen))} carries nothing."
            : $"You loot {count} item{(count == 1 ? "" : "s")} from the {Describe(FallenItemId(fallen))}.";
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

    /// <summary>
    /// What the avatar's use verb applies to: the nearest thing in reach, and what
    /// it accepts. One resolution for every verb, so a creature, a corpse, a door
    /// and an object on the floor are found the same way and a target's reach is
    /// decided in one place.
    /// </summary>
    /// <remarks>
    /// A living creature, then a fallen one, then the door tile underfoot, then what
    /// lies on the floor: the order the use channel has always resolved in, kept so
    /// that standing on a corpse with a creature in reach still talks rather than
    /// loots. Within a family, the nearest wins, and a tie keeps the object the
    /// level's chain names first -- the one on top of what the tile carries.
    /// </remarks>
    public UuReachTarget InReach(WorldPoint position)
    {
        UuReachTarget found = UuReachTarget.Nothing;

        foreach (ActorState actor in PlacedCritters.Values)
        {
            float distance = actor.Position.HorizontalDistanceTo(position);
            if (distance > InteractionReach) continue;
            if (distance >= found.Distance) continue;
            found = UuReachTarget.AtActor(actor, distance, actor.IsDefeated);
        }

        if (found.Kind is UuReachKind.Creature or UuReachKind.Corpse) return found;

        if (_placements?.TileAt(position) is { Door: true } door)
        {
            found = UuReachTarget.AtDoor(door, 0f);
        }

        float nearest = InteractionReach;
        AdmittedObject? nearestObject = null;
        foreach (int index in _session.PlacedObjectIndexes(_session.Dungeon.CurrentLevel))
        {
            // What the level no longer admits is not in reach: a taken or
            // destroyed object has left the place it lay.
            if (!_session.Dungeon.Current.IsLive(index)) continue;
            if (_session.PlacedObjectAt(_session.Dungeon.CurrentLevel, index, out (int X, int Y) tile) is not { } obj)
                continue;
            // Only what lies on the floor is in reach: a container's contents are
            // reached by using the container.
            if (tile.X < 0) continue;
            if (Traps.UuTrapDispatch.IsTrap(obj.ItemId)) continue;
            AdmittedTile? on = _placements?.Tile(tile.X, tile.Y);
            if (on is null) continue;
            WorldPoint center = _placements!.TileCenter(on.X, on.Y, on.FloorHeight);
            float distance = center.HorizontalDistanceTo(position);
            if (distance > nearest) continue;
            // A tie keeps the object the chain names first: that is the one on top
            // of what the tile carries, so a stack is taken from the top.
            if (nearestObject is not null && distance >= nearest) continue;
            nearestObject = obj;
            nearest = distance;
        }

        // The doorway the avatar stands in wins over anything standing in it: the use
        // channel has always opened the door rather than taken the leaf.
        if (found.Kind == UuReachKind.None && nearestObject is not null)
        {
            bool container = Content.UuObjectTablesContent.IsContainerItem(nearestObject.ItemId);
            found = UuReachTarget.AtObject(nearestObject, nearest, container,
                _items?.Find(nearestObject.ItemId)?.CanPickUp == true);
        }

        return found;
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
    /// the Engine refuse the next character step, which faults and pauses the
    /// live session.
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
        _projectiles.Clear(); // Flights cannot cross an unloaded level. Saves retain live flights.
        _session.TravelTo(admitted, costTicks);
        AbandonActors(from);

        // The Engine stands the avatar in the new level's collision before any
        // step is proposed. The new level is drawn last, once its placements,
        // inhabitants and the avatar's pose are the ones the scene reads.
        _movement.ReplaceContent(definition.Collision);
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
        ApplySavedWorld(level);
        _session.ApplyActors(_session.StoredDelta(level)?.Actors ?? []);

        WorldPoint anchor = AnchorPositionOf(definition);
        _player.Restore(anchor, DetachedMotion(anchor.Y));
        PublishLevel(scene);
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

        if (outcome.Gate == UuCastGates.GateResult.Cast && outcome.PrimedForAim)
        {
            string runes = UuRuneCatalog.SpellLetters(_casting.Panel().Shelf);
            int item = runes switch { "OJ" => 23, "OG" => 21, "PF" => 20, _ => 0 };
            if (item != 0 && _player.Position is { } position)
            {
                float yaw = _player.YawRadians, pitch = _player.PitchRadians;
                Vector3 direction = new(MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
                LaunchProjectile(item, (ulong)_session.Avatar.DurableId, position.ToVector(), direction);
                _outcome = "You release the spell.";
                return outcome with { PrimedForAim = false };
            }
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
            new AvatarPoseDto(position.X, position.Y, position.Z, _player.YawRadians)) with
        {
            Holdings = CaptureHoldings(),
            Projectiles = _projectiles.Active.ToArray(),
            Casting = _casting.Capture(),
        };
        return new RulesetSavePayload(UuGameRuleset.RulesetIdentity, UuSessionSnapshotCodec.Encode(snapshot));
    }

    /// <summary>
    /// What the owners on the live level hold, plus the avatar's own pack. Play
    /// can only reach what stands on the level it is on, so those are the owners
    /// whose holdings can differ from the content; the avatar is captured
    /// wherever it travelled.
    /// </summary>
    private UuHoldingDto[] CaptureHoldings()
    {
        if (_levelItems is not { } items) return [];
        int level = _session.Dungeon.CurrentLevel;
        var rows = new List<UuHoldingDto>
        {
            Holding(level, UuHoldingDto.AvatarOwner, items.Read(_session.Avatar.Actor.Entity)),
            Holding(level, UuHoldingDto.FloorOwner, items.Read(items.Floor(level))),
        };
        foreach (int index in OwnerIndexes(level))
        {
            InventoryView held = items.Read(OwnerEntity(level, index));
            if (held.UniqueItems.Count > 0) rows.Add(Holding(level, index, held));
        }

        return rows.ToArray();
    }

    /// <summary>
    /// Every admitted owner on a level: the records that hold something (the
    /// level states that by linking, or by a creature carrying it) and the
    /// creatures standing on it. A plain prop holds nothing and is not an owner,
    /// so it never gets an inventory it has no use for.
    /// </summary>
    private IEnumerable<int> OwnerIndexes(int level)
    {
        if (_session.PlacedAdmission(level) is not { } admission) yield break;
        var owners = new SortedSet<int>(admission.Holders.Values);
        if (_placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? actors))
        {
            foreach (int index in actors.Keys) owners.Add(index);
        }

        foreach (int index in owners)
        {
            if (admission.Objects.ContainsKey(index) || OwnerIsActor(level, index)) yield return index;
        }
    }

    private bool OwnerIsActor(int level, int index) =>
        _placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? actors)
        && actors.ContainsKey(index);

    /// <summary>The entity that holds what a placed record holds.</summary>
    private EntityId OwnerEntity(int level, int index)
    {
        if (_placedCrittersByLevel.TryGetValue(level, out IReadOnlyDictionary<int, ActorState>? actors)
            && actors.TryGetValue(index, out ActorState? actor))
        {
            return actor.Actor.Entity;
        }

        return _session.PlacedAdmission(level)!.ByIndex[index];
    }

    private UuHoldingDto Holding(int level, int ownerIndex, InventoryView held) =>
        new(
            level,
            ownerIndex,
            [
                .. held.UniqueItems
                    // A view can name an entity the session no longer materializes;
                    // that item is not in the world, so the save does not carry it.
                    .Where(item => _session.Directory.Store.IsAlive(item.Entity))
                    .Select(item => new UuHeldItemDto(
                        // The durable identity, not the runtime entity: the identity
                        // is what a rebuilt session resolves the same item back to.
                        _session.Directory.IdentityOf(item.Entity).Value,
                        item.Definition.Value)),
            ]);


    /// <summary>
    /// Puts the world back the way the save left it: each owner holds what it
    /// held, and each creature stands where it stood with the wounds it had. An
    /// item already held by the right owner is left alone, so a restore applied
    /// twice changes nothing.
    /// </summary>
    private void RestoreWorld(UuSessionSnapshot snapshot)
    {
        _projectiles.Restore(snapshot.Projectiles ?? []);
        if (snapshot.Casting is not null) _casting.Restore(snapshot.Casting);
        if (_levelItems is null) return;
        // Held until each level is admitted: a save names owners on levels the
        // restored session has not stood in yet, and their contents are applied
        // when admission rebuilds them rather than all at once here.
        _savedHoldings = snapshot.Holdings ?? [];
        ApplySavedWorld(_session.Dungeon.CurrentLevel);
        // The level's creatures are admitted from content at full strength; what
        // the save says they became rides in the level's own delta.
        _session.ApplyActors(_session.StoredDelta(_session.Dungeon.CurrentLevel)?.Actors ?? []);
    }

    /// <summary>
    /// Puts back everything the save recorded for one level: what each of its
    /// owners held and how its creatures stood. Called once the level's own
    /// content has been admitted, which is what creates the owners.
    /// </summary>
    private void ApplySavedWorld(int level)
    {
        if (_levelItems is not { } items) return;
        // A level's saved world is input to its first admission, not a standing
        // authority over it: re-applying it after every travel would undo what the
        // player did since the load and heal what they wounded.
        if (!_appliedSavedWorld.Add(level)) return;
        // Where every item of this level stands right now, as content admission
        // left it. An item the save places somewhere else is moved out of the
        // owner it was admitted into, through the same transfer play uses, so the
        // owner it left does not keep a second helping of it.
        Dictionary<ulong, EntityId> standing = StandingItems(items, level);
        foreach (UuHoldingDto holding in _savedHoldings)
        {
            if (holding.Level != level
                && !(level == _session.Dungeon.CurrentLevel && holding.OwnerIndex == UuHoldingDto.AvatarOwner))
            {
                continue;
            }

            EntityId destination = holding.OwnerIndex switch
            {
                UuHoldingDto.AvatarOwner => _session.Avatar.Actor.Entity,
                UuHoldingDto.FloorOwner => items.Floor(level),
                _ => OwnerEntity(holding.Level, holding.OwnerIndex),
            };
            foreach (UuHeldItemDto item in holding.Items)
            {
                if (standing.TryGetValue(item.Identity, out EntityId from)
                    && from.Value != destination.Value
                    && _session.Directory.TryResolve(
                        new AbyssRpg.Kit.World.DurableIdentityReference(
                            AbyssRpg.Kit.World.DurableIdentityKind.Item, item.Identity),
                        out EntityId entity))
                {
                    items.Take(entity, from, destination);
                    standing[item.Identity] = destination;
                    continue;
                }

                items.TryHold(item.Identity, item.Definition, destination);
                standing[item.Identity] = destination;
            }
        }

    }

    /// <summary>Every item this level holds right now, by durable identity.</summary>
    private Dictionary<ulong, EntityId> StandingItems(UuLevelItems items, int level)
    {
        var standing = new Dictionary<ulong, EntityId>();
        Collect(items.Read(items.Floor(level)), items.Floor(level), standing);
        Collect(items.Read(_session.Avatar.Actor.Entity), _session.Avatar.Actor.Entity, standing);
        foreach (int index in OwnerIndexes(level))
        {
            EntityId owner = OwnerEntity(level, index);
            Collect(items.Read(owner), owner, standing);
        }

        return standing;

        void Collect(InventoryView held, EntityId owner, Dictionary<ulong, EntityId> into)
        {
            foreach (Rusty.Engine.Mechanics.UniqueInventoryItem item in held.UniqueItems)
            {
                if (!_session.Directory.Store.IsAlive(item.Entity)) continue;
                into[_session.Directory.IdentityOf(item.Entity).Value] = owner;
            }
        }
    }

    /// <summary>The saved world, kept until every level it names has been admitted.</summary>
    private UuHoldingDto[] _savedHoldings = [];

    /// <summary>The levels whose saved world has already been put back.</summary>
    private readonly HashSet<int> _appliedSavedWorld = [];

    /// <summary>Returns the avatar to the level spawn and restores its vitals: the defeat outcome.</summary>
    public void RespawnAtAnchor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _player.Restore(AnchorPosition(), DetachedMotion(AnchorPosition().Y));
        _player.YawRadians = _level.Spawn.HeadingYawRadians;
        _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Current =
            _session.Avatar.Stats.GetTrack(UuAvatarFactory.DefeatTrack).Maximum.Value;
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
