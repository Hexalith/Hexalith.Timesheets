using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Models a dependency that finishes successfully even after ignoring cancellation.</summary>
internal sealed class NonCooperativeReadModelStore(IReadModelStore inner) : IReadModelStore
{
    /// <summary>Gets or sets a read hook that deliberately ignores dependency cancellation.</summary>
    internal Func<Type, string, Task>? BeforeReadAsync { get; set; }

    /// <summary>Gets the last dependency task so a test can observe its eventual completion.</summary>
    internal Task? LastRead { get; private set; }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        Task<ReadModelEntry<TValue>> read = ReadCoreAsync<TValue>(storeName, key);
        LastRead = read;
        return read;
    }

    private async Task<ReadModelEntry<TValue>> ReadCoreAsync<TValue>(string storeName, string key) where TValue : class
    {
        if (BeforeReadAsync is { } before) await before(typeof(TValue), key).ConfigureAwait(false);
        return await inner.GetAsync<TValue>(storeName, key, CancellationToken.None).ConfigureAwait(false);
    }

    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
