using System.Threading.Channels;

using Microsoft.Extensions.Time.Testing;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Exposes scheduled poll delays so tests advance time only after the service has registered its timer.</summary>
internal sealed class PollingTestTimeProvider : FakeTimeProvider
{
    private readonly Channel<bool> _polls = Channel.CreateUnbounded<bool>();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        if (dueTime == TimeSpan.FromMilliseconds(10) && period == Timeout.InfiniteTimeSpan)
        {
            _polls.Writer.TryWrite(true);
        }

        return timer;
    }

    /// <summary>Waits for the next poll timer, with a real-time guard against a broken test arrangement.</summary>
    internal Task WaitForPollAsync(CancellationToken cancellationToken)
        => _polls.Reader.ReadAsync(cancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
}
