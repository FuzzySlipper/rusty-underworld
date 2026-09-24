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
    private UuSession? _session;
    private double _tickCarry;
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
        _store = new AbyssSaveStore(context, "abyssrpg.saves");
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
        _tickCarry = 0;
        _lifecycle.Stop();
    }

    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _session?.Dispose();
        _session = null;
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
        return ProductUpdateResult.None;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    private void ThrowIfShutdown() => ObjectDisposedException.ThrowIf(_shutdown, this);
}
