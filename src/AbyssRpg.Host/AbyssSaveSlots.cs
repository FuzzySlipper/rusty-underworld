using Rusty.Engine.Persistence;

namespace AbyssRpg.Host;

/// <summary>
/// Save slot scheme over the save store: per-level autosaves, a rolling
/// ring of quicksaves, and the respawn anchor slot written at level entry.
/// The death path loads the anchor slot (wiring rides with the defeat
/// outcome owner); the slot bytes are ruleset snapshots. The quicksave
/// cursor is session-scoped: a fresh process restarts the ring at 0.
/// </summary>
public sealed class AbyssSaveSlots
{
    public const int QuicksaveCount = 3;
    public const string AnchorKey = "anchor";

    private readonly AbyssSaveStore _store;
    private int _quicksaveNext;

    public AbyssSaveSlots(AbyssSaveStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static string AutosaveKey(int level) => $"autosave/level-{level}";

    public static string QuicksaveKey(int index) =>
        (uint)index < QuicksaveCount
            ? $"quicksave/{index}"
            : throw new ArgumentOutOfRangeException(nameof(index));

    public void Autosave(int level, byte[] snapshot) =>
        _store.Save(AutosaveKey(level), new AbyssSaveEnvelope("abyssrpg.ultima-underworld", snapshot));

    public void PlantAnchor(byte[] snapshot) =>
        _store.Save(AnchorKey, new AbyssSaveEnvelope("abyssrpg.ultima-underworld", snapshot));

    public byte[]? LoadAnchor() => LoadBytes(AnchorKey);

    public string Quicksave(byte[] snapshot)
    {
        string key = QuicksaveKey(_quicksaveNext);
        _store.Save(key, new AbyssSaveEnvelope("abyssrpg.ultima-underworld", snapshot));
        _quicksaveNext = (_quicksaveNext + 1) % QuicksaveCount;
        return key;
    }

    public byte[]? LoadAutosave(int level) => LoadBytes(AutosaveKey(level));

    private byte[]? LoadBytes(string key)
    {
        ProductStateLoad<AbyssSaveEnvelope> loaded = _store.Load(key);
        return loaded.Present ? loaded.State?.Payload : null;
    }
}
