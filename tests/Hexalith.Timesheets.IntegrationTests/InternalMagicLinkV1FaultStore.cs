using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>One-shot migration stage failures through the real persisted SDK store seam.</summary>
internal sealed class InternalMagicLinkV1FaultStore(InMemoryReadModelStore inner, bool sdkFailure)
    : IReadModelStore, IReadModelBatchStore, IReadModelConditionalEraser
{
    public int Fired { get; private set; }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class => inner.GetAsync<TValue>(storeName, key, cancellationToken);

    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        where TValue : class => inner.SaveAsync(storeName, key, value, cancellationToken);

    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
        where TValue : class => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);

    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => inner.TryEraseAsync(storeName, key, etag, cancellationToken);

    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => inner.TryReadEtagAsync(storeName, key, cancellationToken);

    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
        if (Fired == 0 && batch.Operations.Any(operation => operation.ValueTypeName.Contains("IndexTreeNode", StringComparison.Ordinal)))
        {
            Fired++;
            if (sdkFailure) throw new InvalidOperationException("Simulated SDK stage availability failure.");
            throw new IOException("Simulated store interruption after private stage write.");
        }
        return result;
    }
}
