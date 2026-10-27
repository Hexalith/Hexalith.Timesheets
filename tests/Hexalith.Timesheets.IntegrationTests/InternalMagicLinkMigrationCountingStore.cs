using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Counts all migration store I/O while preserving real SDK storage behavior.</summary>
internal sealed class InternalMagicLinkMigrationCountingStore(InMemoryReadModelStore inner)
    : IReadModelStore, IReadModelBatchStore, IReadModelConditionalEraser
{
    /// <summary>Gets the total number of store operations.</summary>
    public int Calls { get; private set; }

    /// <summary>Starts request accounting after unrelated host startup registrations finish.</summary>
    public void ResetCalls() => Calls = 0;

    /// <inheritdoc />
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        Calls++;
        return inner.GetAsync<TValue>(storeName, key, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReadModelEntry<TValue>> GetCompactedAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        Calls++;
        return inner.GetCompactedAsync<TValue>(storeName, key, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
    {
        Calls++;
        return inner.SaveAsync(storeName, key, value, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
    {
        Calls++;
        return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryEraseAsync(string storeName, string key, string etag,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return inner.TryEraseAsync(storeName, key, etag, cancellationToken);
    }

    /// <inheritdoc />
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return inner.TryReadEtagAsync(storeName, key, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return inner.ExecuteAsync(batch, cancellationToken);
    }
}
