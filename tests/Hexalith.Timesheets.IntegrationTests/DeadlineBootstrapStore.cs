using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Blocks bootstrap IO until the real HTTP sender cancellation reaches the read.</summary>
/// <param name="inner">The durable fixture store.</param>
internal sealed class DeadlineBootstrapStore(InMemoryReadModelStore inner) : IReadModelStore
{
    private readonly System.Collections.Concurrent.ConcurrentBag<CancellationToken> _tokens = [];
    private int _cancelled;
    /// <summary>Gets the number of blocked reads that observed cancellation.</summary>
    public int CancelledReads => Volatile.Read(ref _cancelled);
    /// <summary>Gets the actual tokens passed into blocked bootstrap reads.</summary>
    public IEnumerable<CancellationToken> Tokens => _tokens;
    /// <inheritdoc />
    public async Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
    {
        if (key == MagicLinkTokenHashCapabilityIndexProjection.ShardKey(0))
        {
            _tokens.Add(cancellationToken);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _cancelled);
                throw;
            }
        }
        return await inner.GetAsync<T>(storeName, key, cancellationToken);
    }
    /// <inheritdoc />
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);
    /// <inheritdoc />
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
