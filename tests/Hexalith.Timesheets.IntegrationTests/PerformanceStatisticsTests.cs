using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Fast, infrastructure-free unit tests for <see cref="PerformanceStatistics"/>.
/// <para>
/// The NFR10 capture/governance verdict recorded in <c>docs/performance-evidence.md</c> is only
/// as trustworthy as the nearest-rank percentile math that produces the recorded p95 numbers, so
/// that math is locked down here independently of the opt-in <c>TIMESHEETS_PERF</c> timing lane.
/// These tests run in the normal fast baseline (no env-var gate, no infrastructure, no timing).
/// </para>
/// </summary>
public sealed class PerformanceStatisticsTests
{
    [Theory]
    [InlineData(0.95, 95.0)]  // ceil(0.95 * 100) = 95 -> index 94 -> value 95
    [InlineData(0.50, 50.0)]  // ceil(0.50 * 100) = 50 -> index 49 -> value 50
    [InlineData(0.99, 99.0)]  // ceil(0.99 * 100) = 99 -> index 98 -> value 99
    [InlineData(1.00, 100.0)] // ceil(1.00 * 100) = 100 -> index 99 -> max
    public void NearestRankPercentile_reads_the_expected_rank_over_one_to_one_hundred(
        double percentile,
        double expected)
    {
        double[] sorted = Enumerable.Range(1, 100).Select(static value => (double)value).ToArray();

        PerformanceStatistics.NearestRankPercentile(sorted, percentile).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.20, 10.0)] // ceil(0.20 * 5) = 1 -> index 0
    [InlineData(0.50, 30.0)] // ceil(0.50 * 5) = 3 -> index 2
    [InlineData(0.95, 50.0)] // ceil(0.95 * 5) = 5 -> index 4 (max)
    public void NearestRankPercentile_uses_nearest_rank_on_a_small_sample(
        double percentile,
        double expected)
    {
        double[] sorted = [10.0, 20.0, 30.0, 40.0, 50.0];

        PerformanceStatistics.NearestRankPercentile(sorted, percentile).ShouldBe(expected);
    }

    [Fact]
    public void NearestRankPercentile_clamps_a_zero_percentile_to_the_minimum()
    {
        double[] sorted = [10.0, 20.0, 30.0, 40.0, 50.0];

        // ceil(0) = 0 -> rank - 1 = -1 -> clamped to index 0 (the minimum), never out of range.
        PerformanceStatistics.NearestRankPercentile(sorted, 0.0).ShouldBe(10.0);
    }

    [Fact]
    public void NearestRankPercentile_returns_the_single_value_for_a_one_element_sample()
    {
        double[] sorted = [0.0042];

        PerformanceStatistics.NearestRankPercentile(sorted, 0.95).ShouldBe(0.0042);
        PerformanceStatistics.NearestRankPercentile(sorted, 0.50).ShouldBe(0.0042);
    }

    [Fact]
    public void NearestRankPercentile_returns_an_actual_member_of_the_sample()
    {
        double[] sorted = [0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007];

        double p95 = PerformanceStatistics.NearestRankPercentile(sorted, 0.95);

        // The reported p95 must be a real observed duration, not an interpolated value.
        sorted.ShouldContain(p95);
    }

    [Fact]
    public void NearestRankPercentile_rejects_an_empty_sample()
        => Should.Throw<ArgumentException>(
            static () => PerformanceStatistics.NearestRankPercentile([], 0.95));
}
