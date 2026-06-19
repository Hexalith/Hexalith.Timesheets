using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimesheetPeriodSummaryProjectionTests
{
    [Fact]
    public void Projection_replays_period_and_entry_evidence_with_separate_states()
    {
        TimeEntryId draft = new("time-entry-1");
        TimeEntryId submitted = new("time-entry-2");

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m4", 4, PeriodSubmitted(draft, submitted)),
                Event("m1", 1, Recorded(draft)),
                Event("m2", 2, Recorded(submitted)),
                Event("m3", 3, EntrySubmitted(submitted))
            ],
            FreshCheckpoint(4));

        model.ShouldNotBeNull();
        model.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Submitted);
        model.PeriodKind.ShouldBe(TimesheetPeriodKind.Monthly);
        model.PeriodKey.ShouldBe("2026-06");
        model.EntrySummaries.Count.ShouldBe(2);
        model.EntrySummaries.Single(item => item.TimeEntryId == draft)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.EntrySummaries.Single(item => item.TimeEntryId == submitted)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        model.IncompleteEntryEvidenceIds.ShouldBeEmpty();
    }

    [Fact]
    public void Projection_deduplicates_duplicate_delivery_by_message_id()
    {
        TimeEntryId entryId = new("time-entry-1");
        TimesheetPeriodProjectionEvent period = Event("m2", 2, PeriodSubmitted(entryId));

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m1", 1, Recorded(entryId)),
                period,
                period
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.IncludedTimeEntryIds.ShouldHaveSingleItem().ShouldBe(entryId);
        model.EntrySummaries.ShouldHaveSingleItem().TimeEntryId.ShouldBe(entryId);
    }

    [Fact]
    public void Projection_marks_rebuilding_when_period_arrives_before_entry_evidence()
    {
        TimeEntryId missing = new("time-entry-1");

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [Event("m1", 1, PeriodSubmitted(missing))],
            FreshCheckpoint(1));

        model.ShouldNotBeNull();
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Rebuilding);
        model.IncompleteEntryEvidenceIds.ShouldBe([missing]);
        model.EntrySummaries.ShouldBeEmpty();
    }

    private static TimesheetPeriodSummaryProjection Projector() => new();

    private static TimesheetPeriodProjectionEvent Event(string messageId, long sequence, object payload)
        => new(messageId, sequence, payload);

    private static TimesheetPeriodSubmitted PeriodSubmitted(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Submitter(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "UTC",
            ids,
            TimesheetPeriodApprovalState.Submitted);

    private static TimeEntryRecorded Recorded(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted EntrySubmitted(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            Submitter(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.TimesheetPeriod,
            TimeEntryApprovalState.Submitted);

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequence)
        => new("tenant-1", TimesheetPeriodSummaryProjection.ProjectionName, sequence, ProjectionFreshness.Fresh);

    private static TimesheetPeriodId PeriodId() => new("period-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference Submitter() => new("submitter-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");
}
