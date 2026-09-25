using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using Rusty.Engine;

namespace AbyssRpg.Host;

/// <summary>
/// The stageable product: Engine update admission over an attached UW
/// session. Each admitted update while running advances the session clock
/// (the product game-time inside admitted updates the dungeon needs);
/// paused updates admit nothing. Locomotion stepping and presentation plug
/// in with the spatial session; session→bytes save codecs ride with UW-T25.
/// </summary>
public sealed class AbyssProduct : IEngineProduct
{
    public const double ClockTicksPerSecond = 15300.0 / 60.0;

    private readonly AbyssSaveStore _store;
    private readonly AbyssProductLifecycle _lifecycle = new();
    private readonly IEngineContext _context;
    private UuSession? _session;
    private AbyssSpatialSession? _spatial;
    private UiStream? _hud;
    private ulong _hudSequence;
    private double _tickCarry;
    private readonly Random _rng = new();
    private readonly AbyssSaveSlots _slots;
    private int _lastAutosaveLevel = -1;
    private bool _shutdown;
    private bool _disposed;

    public BuiltInSelection Selection { get; }

    /// <summary>Engine composition entry: default built-in selection.</summary>
    public AbyssProduct(ProductCreateContext context)
        : this(
            context?.Engine ?? throw new ArgumentNullException(nameof(context)),
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld))
    {
    }

    public AbyssProduct(IEngineContext context, BuiltInSelection selection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selection);
        Selection = selection;
        _context = context;
        _store = new AbyssSaveStore(context, "abyssrpg.saves");
        _slots = new AbyssSaveSlots(_store);
    }

    public AbyssLifecycleMode Mode => _lifecycle.Mode;

    /// <summary>Attach (or replace) the session this product admits updates into.</summary>
    public void AttachSession(UuSession? session)
    {
        ThrowIfShutdown();
        _session?.Dispose();
        _session = session;
        _tickCarry = 0;
    }

    /// <summary>Attach (or replace) the spatial session locomotion steps through.</summary>
    public void AttachSpatialSession(AbyssSpatialSession? spatial)
    {
        ThrowIfShutdown();
        _spatial?.Dispose();
        _spatial = spatial;
    }

    public void Start()
    {
        ThrowIfShutdown();
        _lifecycle.Start();
    }

    public void Attach()
    {
    }

    public void Pause()
    {
        ThrowIfShutdown();
        _lifecycle.Pause();
    }

    public void Resume()
    {
        ThrowIfShutdown();
        _lifecycle.Resume();
    }

    public void Restart()
    {
        ThrowIfShutdown();
        _session?.Dispose();
        _session = null;
        _spatial?.Dispose();
        _spatial = null;
        _tickCarry = 0;
        _lifecycle.Stop();
    }

    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _session?.Dispose();
        _session = null;
        _spatial?.Dispose();
        _spatial = null;
        _hud?.Dispose();
        _hud = null;
        _store.Dispose();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (_shutdown || _lifecycle.Mode != AbyssLifecycleMode.Running || _session is null)
            return ProductUpdateResult.None;
        double seconds = update.Facts.FixedDeltaSeconds;
        if (!double.IsFinite(seconds) || seconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(update));
        _tickCarry += seconds * ClockTicksPerSecond;
        ulong whole = (ulong)_tickCarry;
        _tickCarry -= whole;
        if (whole > 0) _session.Clock.Advance(whole);
        if (_spatial is not null)
        {
            var state = new AbyssRpg.Kit.Controls.ProductUpdateState((float)seconds);
            foreach (ProductInputEvent input in update.Input) state.Add(input);
            AbyssSpatialSession.LocomotionStepResult step = _spatial.StepLocomotion(
                _session.Locomotion, update.Input, state, _session.MovementTuning,
                canMove: true, _session.Swimming, _session.Flying);
            // Landing injuries land on the avatar's health track. Defeat
            // itself rides with the defeat outcome owner.
            if (step.FallDamage > 0f)
                _session.Avatar.Stats.GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack)
                    .Spend(step.FallDamage);
        }

        TickSurvival(seconds);
        AutosaveOnLevelChange();
        PublishHud();
        return ProductUpdateResult.None;
    }

    /// <summary>Current music situation: Death on defeat, else Exploring.
    /// Combat/map situations ride with P03-P06 hosting.</summary>
    public AbyssRpg.Rulesets.UltimaUnderworld.Presentation.UuMusicPolicy.Situation CurrentMusicSituation =>
        _session is not null && _session.Avatar.IsDefeated
            ? AbyssRpg.Rulesets.UltimaUnderworld.Presentation.UuMusicPolicy.Situation.Death
            : AbyssRpg.Rulesets.UltimaUnderworld.Presentation.UuMusicPolicy.Situation.Exploring;

    private void TickSurvival(double seconds)
    {
        if (_session is null) return;
        var result = AbyssRpg.Rulesets.UltimaUnderworld.Survival.UuSurvivalPolicy.Tick(
            _session.Survival, seconds, _rng);
        if (result.HungerDamage + result.FatigueDamage + result.PoisonDamage > 0)
            _session.Avatar.Stats.GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack)
                .Spend(result.HungerDamage + result.FatigueDamage + result.PoisonDamage);
    }

    /// <summary>
    /// Autosave when the avatar reaches a level with no save yet. Spell
    /// upkeep and NPC/schedule updates ride with P03-P05 hosting, which
    /// owns that runtime state.
    /// </summary>
    private void AutosaveOnLevelChange()
    {
        if (_session is null || _spatial is null) return;
        int level = _session.Dungeon.CurrentLevel;
        if (level == _lastAutosaveLevel) return;
        _lastAutosaveLevel = level;
        var position = _spatial.Player.Position;
        var snapshot = _session.CaptureSnapshot(new AbyssRpg.Rulesets.UltimaUnderworld.Session.AvatarPoseDto(
            position?.X ?? 0f, position?.Y ?? 0f, position?.Z ?? 0f,
            _spatial.Player.YawRadians));
        _slots.Autosave(level, AbyssRpg.Rulesets.UltimaUnderworld.Session.UuSessionSnapshotCodec.Encode(snapshot));
    }

    /// <summary>
    /// Publish the HUD snapshot. The stream opens lazily so headless
    /// operation never requires a UI service. Charge reads empty until
    /// attack state integrates; the outcome line rides with presentation.
    /// </summary>
    private void PublishHud()
    {
        if (_session is null) return;
        IUiService ui;
        try
        {
            ui = _context.Ui;
        }
        catch (NotSupportedException)
        {
            return;
        }

        _hud ??= ui.OpenStream(new UiStreamRequest("abyss.hud", "abyss.ui.snapshot.v1"));
        var builder = new AbyssRpg.Kit.Presentation.UiValueBuilder();
        var values = AbyssRpg.Rulesets.UltimaUnderworld.Presentation.UuHudProjection.Read(
            _session.Avatar.Stats,
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack,
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.ManaTrack,
            chargeFraction: 0f,
            yawRadians: _spatial?.Player.YawRadians ?? 0f,
            outcome: "");
        uint root = AbyssRpg.Rulesets.UltimaUnderworld.Presentation.UuHudProjection.WriteUi(builder, values);
        ui.PublishProjection(new UiProjection(_hud, ++_hudSequence, builder.Build(root)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    private void ThrowIfShutdown() => ObjectDisposedException.ThrowIf(_shutdown, this);
}
