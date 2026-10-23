using System.Diagnostics;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Tracks raw read costs and a shared monotonic deadline for one loader invocation.</summary>
internal sealed class MagicLinkStateLoadingBudget(MagicLinkStateLoadingOptions options)
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private int _pages;
    private int _events;
    private long _payloadBytes;

    /// <summary>Charges a page before dispatch and bounds even a non-cooperative asynchronous read.</summary>
    internal async Task<StreamReadPage> ReadAsync(
        Func<CancellationToken, Task<StreamReadPage>> read,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable(cancellationToken);
        TimeSpan remaining = options.Timeout - _elapsed.Elapsed;
        if (++_pages > options.MaxPages || remaining <= TimeSpan.Zero)
            throw new TimeoutException("The authoritative history read allowance was exhausted.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining);
        StreamReadPage page = await read(deadline.Token).WaitAsync(remaining, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfUnavailable(cancellationToken);
        foreach (StreamReadEvent item in page.Events)
        {
            _events = checked(_events + 1);
            _payloadBytes = checked(_payloadBytes + (item.Payload?.LongLength ?? 0));
            if (_events > options.MaxEvents || _payloadBytes > options.MaxPayloadBytes)
                throw new InvalidOperationException("The authoritative history read allowance was exhausted.");
        }
        return page;
    }

    /// <summary>Refuses publication after caller cancellation or expiry of the invocation deadline.</summary>
    internal void ThrowIfUnavailable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_elapsed.Elapsed >= options.Timeout)
            throw new TimeoutException("The authoritative history read allowance was exhausted.");
    }
}
