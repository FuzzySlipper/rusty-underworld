using System.Reflection;
using Rusty.Engine;

namespace AbyssRpg.Host.Tests;

/// <summary>In-memory Engine persistence + context doubles (test-only).</summary>
internal sealed class InMemoryPersistenceService : IPersistenceService
{
    private readonly Dictionary<(string Scope, string Key), Entry> _values = [];
    private readonly Dictionary<ulong, Entry?> _blobs = [];
    private ulong _nextBlob;
    private string _scope = "";

    internal void Put(string scope, string key, byte[] payload) => _values[(scope, key)] = new(1, payload.ToArray());

    public PersistenceStore OpenStore(PersistenceOpenRequest request)
    {
        _scope = request.Scope;
        return new(new PersistenceStoreHandle(1), static () => { });
    }

    public PersistenceSaveReceipt Save(PersistenceSaveRequest request)
    {
        (string Scope, string Key) key = (_scope, request.Key);
        bool present = _values.TryGetValue(key, out Entry? existing);
        if ((request.RevisionGuard == PersistenceRevisionGuard.Absent && present)
            || (request.RevisionGuard == PersistenceRevisionGuard.Exact && (!present || existing!.Revision != request.ExpectedRevision)))
            return new PersistenceSaveReceipt(PersistenceSaveOutcome.RevisionConflict, existing?.Revision ?? 0);
        ulong revision = present ? checked(existing!.Revision + 1) : 1;
        _values[key] = new(revision, request.Payload.ToArray());
        return new PersistenceSaveReceipt(revision);
    }

    public PersistenceDeleteReceipt Delete(PersistenceDeleteRequest request)
    {
        (string Scope, string Key) key = (_scope, request.Key);
        _values.TryGetValue(key, out Entry? existing);
        if (existing is null) return new(PersistenceDeleteOutcome.Missing, 0);
        _values.Remove(key);
        return new(PersistenceDeleteOutcome.Deleted, existing.Revision);
    }

    public PersistenceBlob Load(PersistenceLoadRequest request)
    {
        Entry? value = _values.TryGetValue((_scope, request.Key), out Entry? found) ? found : null;
        ulong handle = ++_nextBlob;
        _blobs.Add(handle, value);
        return new(new PersistenceBlobHandle(handle), static () => { });
    }

    public PersistenceBlobInfo DescribeBlob(PersistenceBlob blob)
    {
        Entry? value = Require(blob);
        return value is null ? new(false, 0, 0) : new(true, value.Revision, checked((nuint)value.Payload.Length));
    }

    public void CopyBlob(PersistenceCopyBlobRequest request) => Require(request.Blob)?.Payload.CopyTo(request.Destination);

    public ReadOnlyMemory<byte> ReadBlobBytes(PersistenceBlob blob) => Require(blob)?.Payload.ToArray() ?? [];

    private Entry? Require(PersistenceBlob blob) => _blobs.TryGetValue(blob.Handle.Value, out Entry? value)
        ? value : throw new InvalidOperationException("Unknown persistence blob.");

    private sealed record Entry(ulong Revision, byte[] Payload);
}

internal class EngineContextFake : DispatchProxy
{
    private IPersistenceService? _persistence;

    public static IEngineContext Create(IPersistenceService persistence)
    {
        IEngineContext context = DispatchProxy.Create<IEngineContext, EngineContextFake>();
        ((EngineContextFake)(object)context)._persistence = persistence;
        return context;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name switch
    {
        "get_Persistence" => _persistence ?? throw new InvalidOperationException("Persistence not set."),
        _ => throw new NotSupportedException(method?.Name),
    };
}
