using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Interrupts normal drain after certified repair without changing durable SDK behavior.</summary>
/// <param name="inner">The store preserved across recreation.</param>
internal sealed class V6AbortDrainFaultStore(InMemoryReadModelStore inner) : IReadModelStore, IReadModelBatchStore, IReadModelConditionalEraser
{
    /// <summary>Gets or sets whether to interrupt after persisting the immutable repair reservation.</summary>
    public bool InterruptRepairReservation { get; set; }

    /// <summary>Gets or sets the selected metadata delete to interrupt after commit.</summary>
    public string? InterruptRepairedKey { get; set; }

    /// <summary>Gets or sets whether to interrupt after persisting a prepared drain entry.</summary>
    public bool InterruptPrepared { get; set; }

    /// <summary>Gets or sets the selected shard write to interrupt after commit.</summary>
    public string? InterruptCommittedKey { get; set; }

    /// <inheritdoc />
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => inner.TryEraseAsync(storeName, key, etag, cancellationToken);

    /// <inheritdoc />
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => inner.TryReadEtagAsync(storeName, key, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        if (saved && InterruptRepairReservation && value.GetType().GetProperty("AbortRepair")?.GetValue(value) is not null
            && value.GetType().GetProperty("AbortRepairCompleted")?.GetValue(value) is false)
        {
            InterruptRepairReservation = false;
            throw new IOException("Interrupted after durable repair reservation");
        }
        if (saved && InterruptPrepared && value.GetType().GetProperty("AbortRepairCompleted")?.GetValue(value) is true
            && value.GetType().GetProperty("Journal")?.GetValue(value) is Array journal
            && journal.Cast<object>().Any(item => item.GetType().GetProperty("Prepared")?.GetValue(item) is not null
                || item.GetType().GetProperty("PreparedChunks")?.GetValue(item) is not null))
        {
            InterruptPrepared = false;
            throw new IOException("Interrupted after durable prepared drain");
        }
        return saved;
    }

    /// <inheritdoc />
    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
        if (result.IsSuccess && InterruptRepairedKey is { } repairedKey && batch.Operations.Any(operation =>
            operation.Kind == ReadModelBatchOperationKind.Delete && operation.Key == repairedKey))
        {
            InterruptRepairedKey = null;
            throw new IOException("Interrupted after committed metadata repair");
        }
        if (InterruptCommittedKey is { } key && batch.Operations.Any(operation =>
            operation.Kind == ReadModelBatchOperationKind.Write && operation.Key == key))
        {
            InterruptCommittedKey = null;
            throw new IOException("Interrupted after committed drain");
        }
        return result;
    }
}
