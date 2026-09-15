namespace Hexalith.Timesheets.Projections;

/// <summary>
/// Marks a deterministic failure raised by a handler's own fold/merge delegate, so it can be told
/// apart from the transient store and retry-budget failures <see cref="Exception"/>s that
/// <c>ReadModelWritePolicy.UpdateAsync</c> surfaces with the same CLR types.
/// </summary>
internal sealed class ProjectionFoldException(Exception innerException)
    : Exception("A projection fold delegate failed deterministically.", innerException);
