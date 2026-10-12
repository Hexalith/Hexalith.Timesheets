using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Invokes a test hook before selected reads while retaining real in-memory ETags.</summary>
internal sealed class PromotionHookReadModelStore(InMemoryReadModelStore inner) : IReadModelStore
{
    internal Func<Type, string, Task>? BeforeReadAsync { get; set; }

    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        if (BeforeReadAsync is { } hook) await hook(typeof(TValue), key).ConfigureAwait(false);
        return await inner.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
