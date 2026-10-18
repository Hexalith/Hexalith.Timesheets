using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.OperationalReports;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Projections.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class ProjectionCheckpointTenantScopeTests
{
    [Theory]
    [InlineData("evidence")]
    [InlineData("list")]
    [InlineData("period")]
    [InlineData("ledger")]
    [InlineData("project-report")]
    [InlineData("work-report")]
    [InlineData("tenant-catalog")]
    [InlineData("project-catalog")]
    public void MismatchedCheckpointIsRejectedBeforeEnumeratingHistory(string projection)
    {
        bool enumerated = false;
        var checkpoint = new TimesheetsProjectionCheckpoint("tenant-2", "caller-projection", 1, ProjectionFreshness.Fresh);
        Action project = projection switch
        {
            "evidence" => () => new TimeEntryEvidenceProjection().Project(
                "tenant-1", new TimeEntryId("entry-1"), Events<TimeEntryProjectionEvent>(), checkpoint),
            "list" => () => new TimeEntryEvidenceListProjection().Project(
                "tenant-1", Events<TimeEntryProjectionEvent>(), checkpoint, new QueryTimeEntries()),
            "period" => () => new TimesheetPeriodSummaryProjection().Project(
                "tenant-1", new TimesheetPeriodId("period-1"), Events<TimesheetPeriodProjectionEvent>(), checkpoint),
            "ledger" => () => new ApprovedTimeLedgerProjection().Project(
                "tenant-1", Events<TimeEntryProjectionEvent>(), checkpoint, new QueryApprovedTimeLedger()),
            "project-report" => () => new ActualTimeReportProjection().ProjectByProject(
                "tenant-1", Events<TimeEntryProjectionEvent>(), checkpoint, new QueryProjectActualTimeReport()),
            "work-report" => () => new ActualTimeReportProjection().ProjectByWork(
                "tenant-1", Events<TimeEntryProjectionEvent>(), checkpoint, new QueryWorkActualTimeReport()),
            "tenant-catalog" => () => new TenantActivityTypeCatalogProjection().Project(
                "tenant-1", Events<ActivityTypeProjectionEvent>(), checkpoint),
            "project-catalog" => () => new TenantActivityTypeCatalogProjection().ProjectForProject(
                "tenant-1", new ProjectReference("project-1"), Events<ActivityTypeProjectionEvent>(), checkpoint),
            _ => throw new ArgumentOutOfRangeException(nameof(projection))
        };

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(project);

        enumerated.ShouldBeFalse();
        exception.Message.ShouldNotContain("tenant-1");
        exception.Message.ShouldNotContain("tenant-2");

        IEnumerable<T> Events<T>()
        {
            enumerated = true;
            yield break;
        }
    }
}
