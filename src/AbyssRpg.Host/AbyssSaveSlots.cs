using Rusty.Engine.Persistence;

namespace AbyssRpg.Host;

/// <summary>
/// Save slot scheme over the save store: per-level autosaves, a rolling ring of
/// quicksaves, and the respawn anchor slot. Slot keys are a Host convention
/// because the Engine's persistence service offers no enumeration; the product
/// therefore knows exactly which keys it owns and probes those. The slot bytes
/// are ruleset snapshots and stay opaque here. The quicksave cursor is
/// session-scoped: a fresh process restarts the ring at 0.
/// </summary>
public sealed class AbyssSaveSlots
{
    public const int QuicksaveCount = 3;
    public const int LevelCount = 9;
    public const string AnchorKey = "anchor";
    public const string AutosavePrefix = "autosave/";

    private readonly AbyssSaveStore _store;
    private int _quicksaveNext;

    public AbyssSaveSlots(AbyssSaveStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static string AutosaveKey(int level) =>
        level >= 1 && level <= LevelCount
            ? $"{AutosavePrefix}level-{level}"
            : throw new ArgumentOutOfRangeException(nameof(level));

    public static string QuicksaveKey(int index) =>
        (uint)index < QuicksaveCount
            ? $"quicksave/{index}"
            : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Every key this scheme can hold. Absent slots simply read as absent.</summary>
    public static IEnumerable<string> Keys()
    {
        yield return AnchorKey;
        for (int level = 1; level <= LevelCount; level++) yield return AutosaveKey(level);
        for (int index = 0; index < QuicksaveCount; index++) yield return QuicksaveKey(index);
    }

    /// <summary>A human label for one of this scheme's keys.</summary>
    public static string Describe(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key == AnchorKey) return "Respawn anchor";
        if (key.StartsWith(AutosavePrefix, StringComparison.Ordinal))
            return $"Autosave {key[AutosavePrefix.Length..]}";
        if (key.StartsWith("quicksave/", StringComparison.Ordinal))
            return $"Quicksave {key["quicksave/".Length..]}";
        return key;
    }

    public void Autosave(int level, byte[] snapshot) =>
        _store.Save(AutosaveKey(level), AbyssSaveEnvelope.Create(snapshot));

    public void PlantAnchor(byte[] snapshot) =>
        _store.Save(AnchorKey, AbyssSaveEnvelope.Create(snapshot));

    public byte[]? LoadAnchor() => LoadBytes(AnchorKey);

    public string Quicksave(byte[] snapshot)
    {
        string key = QuicksaveKey(_quicksaveNext);
        _store.Save(key, AbyssSaveEnvelope.Create(snapshot));
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
