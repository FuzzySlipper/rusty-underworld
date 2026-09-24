using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;
using Rusty.Engine.Persistence;

namespace AbyssRpg.Host;

/// <summary>
/// Host-owned save envelope over Engine persistence: every save carries the
/// ruleset identity plus opaque payload bytes. The Host never interprets
/// payload content — meaning belongs to the ruleset (UW-T25).
/// </summary>
public sealed class AbyssSaveStore : IDisposable
{
    private readonly ProductStateStore<PersistedEnvelope> _state;

    public AbyssSaveStore(IEngineContext engine, string scope)
    {
        _state = new ProductStateStore<PersistedEnvelope>(
            engine,
            scope,
            new JsonProductStateCodec<PersistedEnvelope>(AbyssSaveJsonContext.Default.PersistedEnvelope));
    }

    public ProductStateLoad<AbyssSaveEnvelope> Load(string key)
    {
        try
        {
            ProductStateLoad<PersistedEnvelope> loaded = _state.Load(key);
            return loaded.Present
                ? new ProductStateLoad<AbyssSaveEnvelope>(true, loaded.Revision, loaded.State?.ToEnvelope())
                : new ProductStateLoad<AbyssSaveEnvelope>(false, loaded.Revision, null);
        }
        catch (JsonException error)
        {
            throw new AbyssSaveFormatException("The persisted AbyssRpg save is malformed.", error);
        }
        catch (ArgumentException error)
        {
            throw new AbyssSaveFormatException("The persisted AbyssRpg save contains invalid data.", error);
        }
        catch (InvalidOperationException error) when (error is not AbyssSaveFormatException)
        {
            throw new AbyssSaveFormatException("The persisted AbyssRpg save contains invalid data.", error);
        }
    }

    public PersistenceSaveReceipt Save(string key, AbyssSaveEnvelope value, PersistenceRevisionGuard guard = PersistenceRevisionGuard.Any, ulong expectedRevision = 0)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Ruleset))
            throw new ArgumentException("The save ruleset is required.", nameof(value));
        if (value.Payload is null || value.Payload.Length == 0)
            throw new ArgumentException("The save payload is required.", nameof(value));
        return _state.Save(key, PersistedEnvelope.From(value), guard, expectedRevision);
    }

    public void Delete(string key) => _state.Delete(key);

    public void Dispose() => _state.Dispose();

    internal sealed record PersistedEnvelope(string Ruleset, byte[] Payload)
    {
        internal static PersistedEnvelope From(AbyssSaveEnvelope value) =>
            new(value.Ruleset, value.Payload.ToArray());

        internal AbyssSaveEnvelope ToEnvelope()
        {
            if (string.IsNullOrWhiteSpace(Ruleset))
                throw new ArgumentException("The save ruleset is required.");
            if (Payload is null || Payload.Length == 0)
                throw new ArgumentException("The save payload is required.");
            return new AbyssSaveEnvelope(Ruleset, Payload);
        }
    }
}

/// <summary>Ruleset save payload with its identity. Payload meaning is ruleset-owned.</summary>
public sealed record AbyssSaveEnvelope(string Ruleset, byte[] Payload);

/// <summary>Corrupt persisted save data, rejected before anything is built from it.</summary>
public sealed class AbyssSaveFormatException : InvalidOperationException
{
    public AbyssSaveFormatException(string message) : base(message) { }
    public AbyssSaveFormatException(string message, Exception innerException) : base(message, innerException) { }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AbyssSaveStore.PersistedEnvelope))]
internal partial class AbyssSaveJsonContext : JsonSerializerContext;
