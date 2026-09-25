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

    private const float EyeHeight = 1.6f;

    private readonly GameSessionContext _context;
    private readonly UuTuningProfile _tuning;
    private readonly UuLevelDefinition _level;
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

    private int PresentActors
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

    public static UuGameSession Create(GameSessionContext context, RulesetSavePayload? saved)
    {
        ArgumentNullException.ThrowIfNull(context);
        ResolvedGameComposition composition = context.Composition;
        ProductContent content = composition.Content;

        UuTuningProfile tuning = UuTuningProfile.Read(
            composition.Tuning.Payload, $"tuning '{composition.Tuning.Id.Value}'");
        ContentPack avatarPack = composition.RequireContentPack(new ContentPackId("abyssrpg.avatar-options"));
        ContentPack classPack = composition.RequireContentPack(new ContentPackId("abyssrpg.classes"));
        (ContentPack levelPack, int levelNumber) = RequireLevelPack(composition);

        UuLevelDefinition level = UuLevelContent.Read(levelPack.Payload, $"content pack '{levelPack.Id.Value}'");
        if (level.Level != levelNumber)
            throw new InvalidOperationException($"Level pack '{levelPack.Id.Value}' declares level {level.Level}.");
        string renderPath = level.RenderPath;
        UuLevelScene scene = UuLevelSceneContent.Read(content.ReadBytes(renderPath), renderPath);

        UuCreationCatalog.Catalog creation = UuCreationCatalog.Read(
            avatarPack.Payload, classPack.Payload,
            $"content pack '{avatarPack.Id.Value}'", $"content pack '{classPack.Id.Value}'");
        var rng = new Random(unchecked((int)composition.Identity.Bundle.Value.GetHashCode(StringComparison.Ordinal)));
        UuCreationFlow.CreationResult choices = UuCreationCatalog.RollDefault(creation, rng);
        UuVitalsPolicy.Vitals vitals = UuVitalsPolicy.Recalculate(
            choices.Attributes[0], 1, choices.Skills[SkillIndexForVitals],
            choices.Attributes[2]);

        // The imported pack admits collision, geometry and a spawn; tiles and
        // placements are not imported yet, so no object or actor joins the
        // world and the interaction owners stay unreached. Den #8590 carries
        // placement admission and the admitted Use/interact dispatch.
        var firstLevel = new AdmittedLevel(level.Level, [], []);
        UuSession session = UuSession.NewGame(
            choices, vitals, firstLevel, level.Spawn, tuning.Movement,
            worldSeed: composition.Identity.Bundle.Value.GetHashCode(StringComparison.Ordinal));
        var spatialTuning = new SpatialTuning(0.5d, 8, 8, 1);
        var movement = new SpatialMovementSystem(context.Engine.Spatial, context.Engine.Content, level.Collision, spatialTuning);
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
            throw;
        }

        var gameSession = new UuGameSession(
            context,
            tuning,
            level,
            scene,
            session,
            movement,
            player,
            camera,
            new UuLocomotionPolicy(tuning.Movement),
            new UuCastingHosting(rng),
            rng)
        {
            _avatarName = choices.Name.Length == 0 ? creation.Defaults.Name : choices.Name,
        };

        if (saved is not null)
        {
            UuSessionSnapshot snapshot = UuSessionSnapshotCodec.Decode(saved.Bytes.ToArray());
            if (snapshot.Level != level.Level)
            {
                gameSession.Dispose();
                throw new InvalidOperationException(
                    $"Save is on level {snapshot.Level}; the bundle admits level {level.Level}. "
                    + "Import that level before loading this save.");
            }

            session.RestoreSnapshot(snapshot);
            player.Restore(
                new WorldPoint(snapshot.AvatarPose.X, snapshot.AvatarPose.Y, snapshot.AvatarPose.Z),
                DetachedMotion(snapshot.AvatarPose.Y));
            player.YawRadians = snapshot.AvatarPose.YawRadians;
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
                _scene.Positions,
                _scene.Normals,
                ReadOnlyMemory<Vector2>.Empty,
                _scene.Colors,
                _scene.Indices,
                new MeshGroup[] { new(0, 0, (uint)_scene.Indices.Length) },
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
        _outcome = strike.Hit
            ? strike.TargetDefeated
                ? $"You strike for {strike.Damage} and it falls."
                : $"You strike for {strike.Damage}."
            : "You miss.";
    }

    private ActorState? NearestOpponent()
    {
        ActorState? nearest = null;
        float nearestDistance = MeleeReach;
        WorldPoint origin = _player.Position ?? _level.Spawn.Position;
        foreach (ActorState actor in _session.Actors.All)
        {
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
        Attempt(_camera.Dispose);
        Attempt(_movement.Dispose);
        Attempt(_session.Dispose);

        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }
}
