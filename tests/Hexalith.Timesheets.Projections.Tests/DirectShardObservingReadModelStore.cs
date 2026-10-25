using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Observes only direct legacy shard reads while retaining the SDK fake's storage semantics.</summary>
internal sealed class DirectShardObservingReadModelStore(InMemoryReadModelStore inner) : IReadModelStore
{
    private readonly object _gate = new();
    private int _activeReads;

    /// <summary>Gets each direct physical shard address inspected by bootstrap.</summary>
    public ConcurrentDictionary<string, byte> DirectReads { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the peak number of simultaneous direct shard reads.</summary>
    public int MaximumConcurrentReads { get; private set; }

    /// <summary>Gets the number of shard reads that have started but have not completed.</summary>
    public int ActiveReads
    {
        get { lock (_gate) return _activeReads; }
    }

    /// <summary>Gets or sets a synchronous hook at the store invocation boundary.</summary>
    public Action<string>? BeforeGet { get; set; }

    /// <summary>Gets or sets a fault/cancellation hook before the direct read reaches storage.</summary>
    public Func<string, CancellationToken, Task>? BeforeDirectRead { get; set; }

    /// <inheritdoc />
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        BeforeGet?.Invoke(key);
        if (!key.StartsWith("timesheets:magic-links:token-hash-capability-index:v2:shard:", StringComparison.Ordinal))
            return inner.GetAsync<TValue>(storeName, key, cancellationToken);
        DirectReads.TryAdd(key, 0);
        return ReadDirectShardAsync<TValue>(storeName, key, cancellationToken);
    }

    private async Task<ReadModelEntry<TValue>> ReadDirectShardAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken) where TValue : class
    {
        lock (_gate)
        {
            _activeReads++;
            MaximumConcurrentReads = Math.Max(MaximumConcurrentReads, _activeReads);
        }
        try
        {
            if (BeforeDirectRead is not null) await BeforeDirectRead(key, cancellationToken);
            return await inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }
        finally
        {
            lock (_gate) _activeReads--;
        }
    }

    /// <inheritdoc />
    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
