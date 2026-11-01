using System.IO;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Exercises framework body-read size failures and caller cancellation at the HTTP boundary.</summary>
/// <param name="failure">A platform read failure, or no failure to wait for cancellation.</param>
internal sealed class HttpBoundaryReadFailureStream(Exception? failure = null) : MemoryStream
{
    /// <summary>Gets the signal that the framework has begun reading the body.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the signal that the server body reader observed request cancellation.</summary>
    internal TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        return failure is not null ? ValueTask.FromException<int>(failure) : new(WaitForCancellationAsync(cancellationToken));
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private async Task<int> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Canceled.TrySetResult();
            throw;
        }
    }
}
