namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Pure, infrastructure-free statistics used by the command performance lane to turn the
/// per-iteration acknowledgement durations into the recorded NFR10 p95 evidence.
/// <para>
/// Extracted from <see cref="CaptureAndGovernanceCommandPerformanceLaneTests"/> so the
/// nearest-rank percentile math that the recorded "Verdict: pass" depends on is itself covered
/// by fast unit tests (<see cref="PerformanceStatisticsTests"/>), not only exercised inside the
/// opt-in timing lane. A miscomputed percentile would silently falsify the evidence document.
/// </para>
/// </summary>
internal static class PerformanceStatistics
{
    /// <summary>
    /// Computes the nearest-rank percentile over an ascending-sorted sample of durations.
    /// </summary>
    /// <param name="sortedDurations">The per-iteration durations, sorted ascending.</param>
    /// <param name="percentile">The percentile to read, in the inclusive range [0, 1].</param>
    /// <returns>The sample value at the nearest rank for <paramref name="percentile"/>.</returns>
    internal static double NearestRankPercentile(double[] sortedDurations, double percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedDurations);
        if (sortedDurations.Length == 0)
        {
            throw new ArgumentException("Cannot compute a percentile over an empty sample.", nameof(sortedDurations));
        }

        int rank = (int)Math.Ceiling(percentile * sortedDurations.Length);
        int index = Math.Clamp(rank - 1, 0, sortedDurations.Length - 1);
        return sortedDurations[index];
    }
}
