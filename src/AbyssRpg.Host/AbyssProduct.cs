using AbyssRpg.Kit;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.Persistence;

namespace AbyssRpg.Host;

/// <summary>
/// The stageable product: one Engine-admitted update over the composed session
/// the built-in ruleset builds. This type resolves the default bundle, creates
/// the session, routes admitted input and time into it, publishes the one UI
/// projection the shell is bound to, and owns the Host's save slots and
/// lifecycle. It interprets no Ultima Underworld rules and names no ruleset
/// type: everything game-shaped arrives through the Kit seams
/// (<see cref="IGameRuleset"/>, <see cref="IGameSession"/>,
/// <see cref="ISessionStatusSource"/>, <see cref="ISaveableGameSession"/>).
/// </summary>
public sealed class AbyssProduct : IEngineProduct, IDebugCommandModuleSource, IDebugCommandModule
{
    private readonly IEngineContext _context;
    private readonly BuiltInSelection _selection;
    private readonly IGameRuleset _ruleset;
    private readonly ProductContent _content;
    private readonly AbyssSaveStore _store;
    private readonly AbyssSaveSlots _slots;
    private readonly AbyssUiProjection _projection;
    private readonly AbyssOptions _options = AbyssOptions.Defaults;
    private readonly ResolvedGameComposition _composition;
    private IGameSession? _session;
    private AbyssLifecycleMode _lifecycle = AbyssLifecycleMode.Stopped;
    private ProductMode _mode = ProductMode.Playing;
    private int _lastAutosaveLevel = -1;
    private string _hostOutcome = "";
    private IReadOnlyList<AbyssSlotSummary> _slotCache = [];
    private bool _slotsDirty = true;
    private bool _shutdown;
    private bool _pointerCaptured;
    private bool _disposed;

    public BuiltInSelection Selection => _selection;

    /// <summary>The resolved bundle, packs and tuning this product launched with.</summary>
    public ResolvedCompositionIdentity CompositionIdentity => _composition.Identity;

    /// <summary>Engine composition entry: the default built-in selection over admitted content.</summary>
    public AbyssProduct(ProductCreateContext context)
        : this(
            context?.Engine ?? throw new ArgumentNullException(nameof(context)),
            context.Content,
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld))
    {
    }

    public AbyssProduct(IEngineContext context, ProductContent content, BuiltInSelection selection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(selection);
        _selection = selection;
        _context = context;
        _content = content;
        _composition = ResolveComposition(content, selection.Bundle);
        _ruleset = BuiltInRulesets.CreateRuleset(selection.Ruleset);
        _store = new AbyssSaveStore(context, "abyssrpg.saves");
        _slots = new AbyssSaveSlots(_store);
        _projection = new AbyssUiProjection(context, AbyssProductEntry.Default);
        try
        {
            _session = CreateSession(null);
        }
        catch
        {
            _store.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves the default authored bundle over the admitted content. A bundle
    /// that cannot resolve is a launch failure with its diagnostics attached,
    /// because playing without content would be a silently empty dungeon.
    /// </summary>
    public static ResolvedGameComposition ResolveComposition(ProductContent content, GameBundleId bundle)
    {
        ArgumentNullException.ThrowIfNull(content);
        GameCompositionResolution resolution = GameCompositionResolver.Resolve(content, bundle);
        if (resolution.Composition is { } composition && resolution.IsResolved) return composition;
        string diagnostics = resolution.Diagnostics.Count == 0
            ? "no diagnostics were reported"
            : string.Join(" ", resolution.Diagnostics.Select(diagnostic => diagnostic.Message));
        throw new InvalidOperationException(
            $"Game bundle '{bundle.Value}' did not resolve over the admitted content: {diagnostics} "
            + "An imported level pack is operator-produced: run scripts/import-level.sh, then rebuild.");
    }

    public AbyssLifecycleMode LifecycleMode => _lifecycle;

    public ProductMode Mode => _mode;

    /// <summary>The live ruleset session.</summary>
    public IGameSession? Session => _session;

    public void Start()
    {
        ThrowIfShutdown();
        if (_lifecycle != AbyssLifecycleMode.Stopped)
            throw new InvalidOperationException($"Cannot start from {_lifecycle}.");
        _lifecycle = AbyssLifecycleMode.Running;
        _mode = ProductMode.Playing;
        _session ??= CreateSession(null);
        ApplyMode();
        _session.PublishInitial();
        Publish();
    }

    public void Attach()
    {
        // Renderer attachment reads committed Engine state; the session publishes
        // its scene and camera when it starts, so there is nothing to attach.
    }

    public void Pause()
    {
        ThrowIfShutdown();
        if (_lifecycle != AbyssLifecycleMode.Running)
            throw new InvalidOperationException($"Cannot pause from {_lifecycle}.");
        _lifecycle = AbyssLifecycleMode.Paused;
        _mode = ProductMode.Paused;
        ApplyMode();
        Publish();
    }

    public void Resume()
    {
        ThrowIfShutdown();
        if (_lifecycle != AbyssLifecycleMode.Paused)
            throw new InvalidOperationException($"Cannot resume from {_lifecycle}.");
        _lifecycle = AbyssLifecycleMode.Running;
        _mode = ProductMode.Playing;
        ApplyMode();
        Publish();
    }

    /// <summary>
    /// Menu-driven pause: the world holds still while the product keeps admitting
    /// input and publishing, because a resume has to arrive through the same
    /// admitted update that this pause came from. The Engine lifecycle stays
    /// running; only <see cref="Pause"/> stops Engine admission, and then no
    /// update can resume anything.
    /// </summary>
    public void PausePlay()
    {
        ThrowIfShutdown();
        if (_mode == ProductMode.Paused) return;
        _mode = ProductMode.Paused;
        ApplyMode();
        Publish();
    }

    /// <summary>
    /// Begins or continues play from the menu: a released session is built again
    /// from the composition, and a paused one resumes. The Engine lifecycle is
    /// untouched, because it is already admitting this intent.
    /// </summary>
    public void BeginPlay()
    {
        ThrowIfShutdown();
        if (_session is null)
        {
            _session = CreateSession(null);
            _session.PublishInitial();
        }

        _mode = ProductMode.Playing;
        ApplyMode();
        Publish();
    }

    /// <summary>Menu-driven resume back into the live session.</summary>
    public void ResumePlay()
    {
        ThrowIfShutdown();
        if (_mode == ProductMode.Playing) return;
        _mode = ProductMode.Playing;
        ApplyMode();
        Publish();
    }

    /// <summary>
    /// Quits to the menu: the world is released and a later Start builds a fresh
    /// session from the composition. This slice has no title screen, so the
    /// paused menu is the title.
    /// </summary>
    public void QuitToMenu()
    {
        ThrowIfShutdown();
        ReleaseSession();
        _mode = ProductMode.Paused;
        _hostOutcome = "Session stopped.";
        Publish();
    }

    private void ReleaseSession()
    {
        _session?.Dispose();
        _session = null;
        _lastAutosaveLevel = -1;
        MarkSlotsDirty();
    }

    public void Restart()
    {
        ThrowIfShutdown();
        ReleaseSession();
        _lifecycle = AbyssLifecycleMode.Stopped;
        _mode = ProductMode.Playing;
        _hostOutcome = "Session restarted.";
        Start();
    }

    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _session?.Dispose();
        _session = null;
        _projection.Dispose();
        _store.Dispose();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (_shutdown || _lifecycle != AbyssLifecycleMode.Running) return ProductUpdateResult.None;

        // Menu actions arrive through the admitted lane and are handled first,
        // so a paused or released session can still be resumed or restarted.
        foreach (ProductInputEvent input in update.Input) HandleIntent(input);
        if (_session is null || _mode != ProductMode.Playing) return ProductUpdateResult.None;

        ProductUpdateResult result = _session.Update(update);
        if (_session is IModeAwareGameSession modes && modes.PendingModeRequest is { } request && request != _mode)
        {
            _mode = request;
            ApplyMode();
        }

        AutosaveOnLevelChange();
        Publish();
        return result;
    }

    /// <summary>
    /// Declared direct intents the shell may claim. Physical mappings deliver the
    /// same intents, so a menu button and a key reach one code path.
    /// </summary>
    private void HandleIntent(ProductInputEvent input)
    {
        // Losing pointer lock is the Escape path: the world pauses and the menu
        // becomes authoritative, instead of the DOM guessing at menu state. The
        // lane also reports one loss while the pointer was never captured, so
        // only a loss after real gameplay input pauses.
        if (input.Kind == InputEventKind.Clear)
        {
            if (input.ClearReason == InputClearReason.PointerLockLoss && _pointerCaptured)
            {
                _pointerCaptured = false;
                PausePlay();
            }

            return;
        }

        if (input.Provenance == InputProvenance.Physical) _pointerCaptured = true;

        if (input.Kind is not (InputEventKind.DirectDigital or InputEventKind.MappedDigital)) return;
        // A direct UI intent carries its active fact in X with no edge; a mapped
        // intent carries the physical press. One-shot actions fire on the press:
        // a held edge would repeat a quicksave or rebuild the world every tick.
        bool active = input.Kind == InputEventKind.DirectDigital
            ? input.X > 0f
            : input.Edge == InputEdge.Pressed;
        if (!active) return;
        string intent = System.Text.Encoding.UTF8.GetString(input.Intent.Span);
        switch (intent)
        {
            case "abyss.lifecycle.start":
                _pointerCaptured = true;
                BeginPlay();
                break;
            case "abyss.lifecycle.pause":
                PausePlay();
                break;
            case "abyss.lifecycle.resume":
                // The shell re-captures gameplay focus for this intent, so a
                // following lock loss is another Escape rather than launch.
                _pointerCaptured = true;
                ResumePlay();
                break;
            case "abyss.lifecycle.stop":
                QuitToMenu();
                break;
            case "abyss.action.quicksave":
                Quicksave();
                break;
            case "abyss.action.journey-onward":
                LoadJourneyOnward();
                break;
            case "abyss.action.respawn":
                Respawn();
                break;
            case var slot when slot.StartsWith(LoadSlotIntentPrefix, StringComparison.Ordinal):
                LoadSlotAt(slot[LoadSlotIntentPrefix.Length..]);
                break;
            default:
                break;
        }
    }

    /// <summary>The intent prefix a menu uses to name the slot it wants resumed.</summary>
    public const string LoadSlotIntentPrefix = "abyss.action.load-slot-";

    /// <summary>
    /// Resumes the slot at one position of the menu's own ordering (newest
    /// first). A save written between the projection and the click shifts the
    /// positions, so the slot that was actually resumed is reported by name.
    /// </summary>
    public bool LoadSlotAt(string position)
    {
        ThrowIfShutdown();
        if (!int.TryParse(position, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
            || number < 1)
        {
            _hostOutcome = "That slot is not a save this product owns.";
            Publish();
            return false;
        }

        IReadOnlyList<AbyssSlotSummary> slots = Slots();
        if (number > slots.Count)
        {
            _hostOutcome = "There is no save in that slot.";
            Publish();
            return false;
        }

        return LoadKey(slots[number - 1].Key);
    }

    private void ApplyMode()
    {
        if (_session is IModeAwareGameSession modes) modes.ApplyProductMode(_mode);
    }

    private GameSessionContext SessionContext() => new(_context, _composition);

    private IGameSession CreateSession(RulesetSavePayload? saved)
    {
        GameSessionContext context = SessionContext();
        return saved is null
            ? _ruleset.CreateSession(context)
            : ((ISaveableGameRuleset)_ruleset).CreateSession(context, saved);
    }

    /// <summary>
    /// Autosave when the avatar reaches a new level (re-entry rewrites). The
    /// payload is ruleset-owned; the Host chooses the slot and the moment.
    /// </summary>
    private void AutosaveOnLevelChange()
    {
        if (_session is not ISaveableGameSession saveable || _session is not ISessionStatusSource status) return;
        int level = status.Status.Level;
        if (level == _lastAutosaveLevel) return;
        // Slots are named per level and the shipped dungeon has nine; a bundle
        // that admits another level still plays, it just has no autosave name.
        if (level is < 1 or > AbyssSaveSlots.LevelCount) return;
        try
        {
            _slots.Autosave(level, saveable.CaptureSave().Bytes.ToArray());
        }
        catch (Exception error) when (error is AbyssSaveFormatException or NotSupportedException or IOException)
        {
            // A save outage must not fault the admitted update, and the level is
            // only marked saved once its bytes are durable.
            _hostOutcome = $"Autosave failed: {error.Message}";
            Publish();
            return;
        }

        _lastAutosaveLevel = level;
        MarkSlotsDirty();
    }

    /// <summary>Writes the next quicksave slot and reports the slot it wrote.</summary>
    public string Quicksave()
    {
        ThrowIfShutdown();
        if (_session is not ISaveableGameSession saveable) return "This session cannot be saved yet.";
        string key = _slots.Quicksave(saveable.CaptureSave().Bytes.ToArray());
        MarkSlotsDirty();
        _hostOutcome = $"Saved to {key}.";
        Publish();
        return key;
    }

    /// <summary>
    /// Loads the newest autosave, else the respawn anchor, by replacing the whole
    /// session from its ruleset-owned payload.
    /// </summary>
    public bool LoadJourneyOnward()
    {
        ThrowIfShutdown();
        // A save written by another ruleset would only fail after the live world
        // was gone, so the choice is made over this product's own saves.
        string? key = AbyssSaveUx.JourneyOnward(
            Slots()
                .Where(slot => string.Equals(slot.Ruleset, _composition.Identity.Ruleset.Value, StringComparison.Ordinal))
                .Select(slot => new AbyssSaveUx.SlotDescription(slot.Key, slot.SavedAtUtc, slot.Label))
                .ToArray());
        if (key is null)
        {
            _hostOutcome = "No save to journey onward from.";
            Publish();
            return false;
        }

        return LoadKey(key);
    }

    /// <summary>
    /// Loads one save slot by key. Journey Onward resolves which slot that is for
    /// the automatic path; the menu names a slot outright, so a save a player
    /// made can be resumed rather than only written.
    /// </summary>
    public bool LoadSlot(string key)
    {
        ThrowIfShutdown();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return LoadKey(key);
    }

    private bool LoadKey(string key)
    {
        ProductStateLoad<AbyssSaveEnvelope> loaded = _store.Load(key);
        if (!loaded.Present || loaded.State is null)
        {
            _hostOutcome = $"Save '{key}' could not be read.";
            Publish();
            return false;
        }

        var payload = new RulesetSavePayload(new RulesetId(loaded.State.Ruleset), loaded.State.Payload);
        // Everything that can be decided without destroying the running world is
        // decided first, so a refused load leaves the session in place.
        try
        {
            if (_ruleset is ISaveableGameRuleset saveableRuleset)
                saveableRuleset.ValidateSavedSession(SessionContext(), payload);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException
            or AbyssSaveFormatException or System.Text.Json.JsonException)
        {
            _hostOutcome = $"Save '{key}' could not be loaded: {error.Message}";
            Publish();
            return false;
        }

        try
        {
            ReplaceSession(payload);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException
            or AbyssSaveFormatException or System.Text.Json.JsonException)
        {
            _hostOutcome = $"Save '{key}' could not be loaded: {error.Message}";
            Publish();
            return false;
        }

        _hostOutcome = $"Resumed {key}.";
        _mode = ProductMode.Playing;
        ApplyMode();
        _session?.PublishInitial();
        Publish();
        return true;
    }

    /// <summary>Returns the avatar to the level anchor: the defeat outcome this slice offers.</summary>
    public bool Respawn()
    {
        ThrowIfShutdown();
        if (_session is ISessionStatusSource status && !status.Status.Defeated)
        {
            // Nothing to return from: the lane is reachable outside the menu,
            // and a healthy respawn would teleport and heal the avatar. The
            // refusal is published like any other, so an optimistic companion
            // focus is corrected by the next projection.
            _hostOutcome = "Nothing to return from; the avatar is standing.";
            Publish();
            return false;
        }

        if (_session is not IRespawnableGameSession respawnable)
        {
            // The companion asked for gameplay focus with this action; publish
            // the refusal so the projection settles the shell's mode again.
            _hostOutcome = "This session cannot return to the anchor.";
            Publish();
            return false;
        }
        respawnable.RespawnAtAnchor();
        _mode = ProductMode.Playing;
        ApplyMode();
        Publish();
        return true;
    }

    private void ReplaceSession(RulesetSavePayload? saved)
    {
        ReleaseSession();
        _hostOutcome = "";
        _session = CreateSession(saved);
    }

    /// <summary>The slots this product owns, newest first: what Journey Onward resolves over.</summary>
    public IReadOnlyList<AbyssSlotSummary> Slots()
    {
        if (!_slotsDirty) return _slotCache;
        List<AbyssSlotSummary> slots = [];
        foreach (string key in AbyssSaveSlots.Keys())
        {
            ProductStateLoad<AbyssSaveEnvelope> loaded;
            try
            {
                loaded = _store.Load(key);
            }
            catch (Exception error) when (error is AbyssSaveFormatException or NotSupportedException or IOException)
            {
                // An unreadable store degrades the menu; it must not fault the
                // admitted update that publishes it.
                continue;
            }

            if (!loaded.Present || loaded.State is null) continue;
            slots.Add(new AbyssSlotSummary(
                key,
                AbyssSaveSlots.Describe(key),
                loaded.State.SavedAtUtc,
                loaded.State.Ruleset));
        }

        slots.Sort((left, right) => right.SavedAtUtc.CompareTo(left.SavedAtUtc));
        _slotCache = slots;
        _slotsDirty = false;
        return _slotCache;
    }

    private void MarkSlotsDirty() => _slotsDirty = true;

    private void Publish()
    {
        SessionStatus? status = (_session as ISessionStatusSource)?.Status;
        string mode = _lifecycle switch
        {
            AbyssLifecycleMode.Stopped => AbyssModes.Stopped,
            AbyssLifecycleMode.Paused => AbyssModes.Paused,
            _ => _mode switch
            {
                ProductMode.Dead => AbyssModes.Dead,
                ProductMode.Modal => AbyssModes.Modal,
                ProductMode.Paused => AbyssModes.Paused,
                _ => AbyssModes.Playing,
            },
        };
        bool defeated = status?.Defeated ?? false;
        IReadOnlyList<AbyssSlotSummary> slots = Slots();
        string journey = AbyssSaveUx.JourneyOnward(
            slots.Select(slot => new AbyssSaveUx.SlotDescription(slot.Key, slot.SavedAtUtc, slot.Label)).ToArray()) ?? "";
        AbyssMenuState menu = AbyssMenuState.From(mode, defeated, _session is not null, journey, slots, _options);
        string outcome = _hostOutcome.Length > 0 ? _hostOutcome : status?.Outcome ?? "";
        _projection.Publish(new AbyssUiSnapshot(
            Ready: status is not null,
            Mode: mode,
            Menu: menu,
            Level: status?.Level ?? 0,
            Avatar: status?.AvatarName ?? "",
            Hp: status?.Hp ?? 0,
            MaxHp: status?.MaxHp ?? 0,
            Mana: status?.Mana ?? 0,
            MaxMana: status?.MaxMana ?? 0,
            Charge: status?.ChargeFraction ?? 0f,
            YawRadians: status?.YawRadians ?? 0f,
            Wind: status?.WindIndex ?? 0,
            Outcome: outcome,
            Defeated: defeated,
            Swimming: status?.Swimming ?? false,
            Flying: status?.Flying ?? false,
            PresentActors: status?.PresentActors ?? 0,
            Slots: slots.Count,
            Conversation: Conversation(status),
            LightRadius: status?.LightRadius ?? 0));
    }

    /// <summary>
    /// The conversation the live session is in, as the projection carries it, or
    /// null when the avatar is not talking to anyone.
    /// </summary>
    private static AbyssConversationView? Conversation(SessionStatus? status) =>
        status?.Conversation is { } talk
            ? new AbyssConversationView(talk.Speaker, talk.Lines, talk.Prompts, talk.Attitude, talk.LastTrade)
            : null;

    /// <summary>Registers this product's commands plus the live session's own module.</summary>
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(this);
        // The ruleset owns one module for its lifetime and re-points it at each
        // session, because the generated catalog keeps one module per type.
        if (_ruleset is IDebuggableGameRuleset debuggable) registrar.Register(debuggable.DebugModule);
    }

    [DebugCommand("abyss.product", Description = "Product identity, bundle, mode and resolved composition.")]
    public string ProductInfo()
    {
        ResolvedCompositionIdentity identity = _composition.Identity;
        string packs = string.Join(",", identity.ContentPacks.Select(pack => pack.Value));
        return $"id={AbyssProductEntry.Default.Id} bundle={identity.Bundle.Value} ruleset={identity.Ruleset.Value} "
            + $"tuning={identity.Tuning.Value} packs=[{packs}] lifecycle={_lifecycle} mode={_mode}";
    }

    [DebugCommand("abyss.save", Description = "Write a quicksave slot and report its key.")]
    public string Save() => Quicksave();

    [DebugCommand("abyss.load", Description = "Load the newest autosave, else the respawn anchor.")]
    public string Load()
    {
        LoadJourneyOnward();
        return _hostOutcome;
    }

    [DebugCommand("abyss.slots", Description = "List the save slots this product owns, newest first.")]
    public string SlotList()
    {
        IReadOnlyList<AbyssSlotSummary> slots = Slots();
        return slots.Count == 0
            ? "no saves"
            : string.Join("; ", slots.Select(slot => $"{slot.Key} ({slot.Label}, {slot.SavedAtUtc:O}, {slot.Ruleset})"));
    }

    [DebugCommand("abyss.pause", Description = "Pause play and show the menu.")]
    public string PauseCommand()
    {
        if (_lifecycle == AbyssLifecycleMode.Running) Pause();
        return $"lifecycle={_lifecycle} mode={_mode}";
    }

    [DebugCommand("abyss.play", Description = "Resume play: the paused menu's resume path.")]
    public string PlayCommand()
    {
        if (_lifecycle == AbyssLifecycleMode.Paused) Resume();
        else if (_lifecycle == AbyssLifecycleMode.Stopped) Start();
        return $"lifecycle={_lifecycle} mode={_mode}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    private void ThrowIfShutdown() => ObjectDisposedException.ThrowIf(_shutdown, this);
}
