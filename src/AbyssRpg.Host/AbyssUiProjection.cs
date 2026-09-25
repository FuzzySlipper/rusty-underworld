using AbyssRpg.Kit.Presentation;
using Rusty.Engine;

namespace AbyssRpg.Host;

/// <summary>
/// One save slot as the product can actually report it: the key it wrote, when
/// the write happened, and the ruleset identity the payload belongs to. Slot
/// contents stay opaque here.
/// </summary>
public sealed record AbyssSlotSummary(string Key, string Label, DateTime SavedAtUtc, string Ruleset);

/// <summary>
/// The authoritative menu/lifecycle state the DOM renders. It is a value, not a
/// screen: the product decides it from live mode, defeat and save facts, and
/// the shell shows it without owning any of it.
/// </summary>
public sealed record AbyssMenuState(
    bool Visible,
    string Mode,
    bool CanStart,
    bool CanResume,
    bool CanSave,
    bool CanLoad,
    bool CanRespawn,
    string JourneyOnward,
    IReadOnlyList<AbyssSlotSummary> Slots,
    bool InvertY,
    string Detail)
{
    public static AbyssMenuState From(
        string mode, bool defeated, bool sessionPresent, string journeyOnward,
        IReadOnlyList<AbyssSlotSummary> slots, AbyssOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(slots);
        bool playing = mode == AbyssModes.Playing;
        return new AbyssMenuState(
            Visible: !playing || defeated,
            Mode: mode,
            CanStart: !sessionPresent,
            CanResume: !playing && !defeated && sessionPresent,
            // Saving a paused world is the ordinary pause-menu save; a defeated
            // or released session has nothing worth capturing.
            CanSave: sessionPresent && !defeated,
            CanLoad: sessionPresent && journeyOnward.Length > 0,
            CanRespawn: defeated,
            JourneyOnward: journeyOnward,
            Slots: slots,
            InvertY: options.InvertY,
            Detail: options.Detail.ToString());
    }
}

/// <summary>Product mode names the projection and the DOM agree on.</summary>
public static class AbyssModes
{
    public const string Playing = "playing";
    public const string Paused = "paused";
    public const string Modal = "modal";
    public const string Dead = "dead";
    public const string Stopped = "stopped";
}

/// <summary>
/// The one projection the shell receives. A product UI projection is bound to a
/// single stream and contract pair in the staged manifest, so every value the
/// DOM reads — vitals, charge, outcome, mode, menu, slots — rides in this
/// snapshot rather than in side streams the shell can never subscribe to.
/// </summary>
public sealed record AbyssUiSnapshot(
    bool Ready,
    string Mode,
    AbyssMenuState Menu,
    int Level,
    string Avatar,
    int Hp,
    int MaxHp,
    int Mana,
    int MaxMana,
    float Charge,
    float YawRadians,
    int Wind,
    string Outcome,
    bool Defeated,
    bool Swimming,
    bool Flying,
    int PresentActors,
    int Slots)
{
    public static uint Write(UiValueBuilder builder, AbyssUiSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(snapshot);
        AbyssMenuState menu = snapshot.Menu;
        uint slots = builder.Array(menu.Slots
            .Select(slot => builder.Object(
                ("key", builder.String(slot.Key)),
                ("label", builder.String(slot.Label)),
                ("savedAtUtc", builder.String(slot.SavedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)))))
            .ToArray());
        uint menuValue = builder.Object(
            ("visible", builder.Boolean(menu.Visible)),
            ("mode", builder.String(menu.Mode)),
            ("canStart", builder.Boolean(menu.CanStart)),
            ("canResume", builder.Boolean(menu.CanResume)),
            ("canSave", builder.Boolean(menu.CanSave)),
            ("canLoad", builder.Boolean(menu.CanLoad)),
            ("canRespawn", builder.Boolean(menu.CanRespawn)),
            ("journeyOnward", builder.String(menu.JourneyOnward)),
            ("invertY", builder.Boolean(menu.InvertY)),
            ("detail", builder.String(menu.Detail)),
            ("slots", slots));
        return builder.Object(
            ("ready", builder.Boolean(snapshot.Ready)),
            ("mode", builder.String(snapshot.Mode)),
            ("menu", menuValue),
            ("level", builder.Number(snapshot.Level)),
            ("avatar", builder.String(snapshot.Avatar)),
            ("hp", builder.Number(snapshot.Hp)),
            ("maxHp", builder.Number(snapshot.MaxHp)),
            ("mana", builder.Number(snapshot.Mana)),
            ("maxMana", builder.Number(snapshot.MaxMana)),
            ("charge", builder.Number(snapshot.Charge)),
            ("yawRadians", builder.Number(snapshot.YawRadians)),
            ("wind", builder.Number(snapshot.Wind)),
            ("outcome", builder.String(snapshot.Outcome)),
            ("defeated", builder.Boolean(snapshot.Defeated)),
            ("swimming", builder.Boolean(snapshot.Swimming)),
            ("flying", builder.Boolean(snapshot.Flying)),
            ("presentActors", builder.Number(snapshot.PresentActors)),
            ("slots", builder.Number(snapshot.Slots)));
    }
}

/// <summary>
/// The Host's projection writer over the declared stream. It opens the stream
/// lazily so a headless run never needs a UI service, and every publish carries
/// a strictly increasing sequence because the shell rejects anything else.
/// </summary>
public sealed class AbyssUiProjection : IDisposable
{
    private readonly IUiService? _ui;
    private UiStream? _stream;
    private uint _sequence;
    private bool _disposed;

    public AbyssUiProjection(IEngineContext engine, AbyssProductEntry entry)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            _ui = engine.Ui;
        }
        catch (NotSupportedException)
        {
            _ui = null;
        }
    }

    public void Publish(AbyssUiSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_disposed || _ui is null) return;
        _stream ??= _ui.OpenStream(new UiStreamRequest(
            AbyssProductEntry.Default.UiProjectionStream,
            AbyssProductEntry.Default.UiProjectionContract));
        var builder = new UiValueBuilder();
        uint root = AbyssUiSnapshot.Write(builder, snapshot);
        _ui.PublishProjection(new UiProjection(_stream, ++_sequence, builder.Build(root)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream = null;
    }
}
