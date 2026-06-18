using Hexalith.Timesheets.Projections;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class ProjectionPlaceholderTests
{
    [Fact]
    public void Projection_checkpoint_serves_reads_only_when_fresh()
    {
        new TimesheetsProjectionCheckpoint("tenant-test", "approved-time-ledger", 42, ProjectionFreshness.Fresh)
            .CanServeReads.ShouldBeTrue();

        foreach (ProjectionFreshness freshness in new[]
        {
            ProjectionFreshness.Unknown,
            ProjectionFreshness.Rebuilding,
            ProjectionFreshness.Stale,
            ProjectionFreshness.Unavailable
        })
        {
            new TimesheetsProjectionCheckpoint("tenant-test", "approved-time-ledger", 42, freshness)
                .CanServeReads.ShouldBeFalse(freshness.ToString());
        }
    }
}
