using AbyssRpg.Kit.Presentation;
using Rusty.Engine;

namespace AbyssRpg.Host;

/// <summary>
/// Menu surfaces over UI streams: main menu, pause, options, save slots,
/// and the death screen. Each surface publishes one projection per show;
/// Journey Onward resolves through the save UX. Content stays in the
/// projections (thin strings); no gameplay state lives here.
/// </summary>
public sealed class AbyssMenus : IDisposable
{
    private readonly IUiService _ui;
    private readonly Dictionary<string, UiStream> _streams = [];
    private uint _sequence;
    private bool _disposed;

    public AbyssMenus(IEngineContext engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _ui = engine.Ui;
    }

    public void ShowMain(IReadOnlyList<AbyssSaveUx.SlotDescription> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        string? journey = AbyssSaveUx.JourneyOnward(slots);
        Publish("abyss.main", new Dictionary<string, string>
        {
            ["title"] = "Abyss",
            ["journey"] = journey ?? "",
            ["slots"] = slots.Count.ToString(),
        });
    }

    public void ShowPause() =>
        Publish("abyss.pause", new Dictionary<string, string> { ["mode"] = "paused" });

    public void ShowOptions(AbyssOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Publish("abyss.options", new Dictionary<string, string>
        {
            ["invertY"] = options.InvertY.ToString(),
            ["detail"] = options.Detail.ToString(),
        });
    }

    public void ShowSlots(IReadOnlyList<AbyssSaveUx.SlotDescription> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var values = new Dictionary<string, string> { ["count"] = slots.Count.ToString() };
        for (int i = 0; i < slots.Count; i++)
            values[$"slot{i}"] = $"{slots[i].Key}|{slots[i].Label}";
        Publish("abyss.slots", values);
    }

    public void ShowDeath(string respawn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(respawn);
        Publish("abyss.death", new Dictionary<string, string> { ["respawn"] = respawn });
    }

    private void Publish(string stream, IReadOnlyDictionary<string, string> values)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_streams.TryGetValue(stream, out UiStream? handle))
            _streams[stream] = handle = _ui.OpenStream(new UiStreamRequest(stream, "abyss.ui.map.v1"));
        var builder = new UiValueBuilder();
        uint root = builder.Object(values.Select(kv => (kv.Key, builder.String(kv.Value))).ToArray());
        _ui.PublishProjection(new UiProjection(handle, ++_sequence, builder.Build(root)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _streams.Clear();
    }
}
