using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimeEntryEvidenceProjectionTests
{
    [Fact]
    public void Projection_exposes_recorded_draft_evidence_with_freshness_metadata()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45))],
            FreshCheckpoint(1));

        model.ShouldNotBeNull();
        model.TimeEntryId.ShouldBe(TimeEntryId());
        model.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        model.Contributor.ShouldBe(Contributor());
        model.ActivityTypeId.ShouldBe(ActivityId());
        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.DurationMinutes.ShouldBe(45);
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        model.ProjectionFreshness.Cursor.ShouldBe("1");
    }

    [Fact]
    public void Projection_is_idempotent_for_duplicate_delivery()
    {
        TimeEntryProjectionEvent[] once = [Event("m1", 1, Recorded("time-entry-1", 45))];

        TimeEntryEvidenceReadModel? replayedOnce = Projector().Project("tenant-1", TimeEntryId(), once, FreshCheckpoint(1));
        TimeEntryEvidenceReadModel? replayedDuplicates = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [.. once, once[0]],
            FreshCheckpoint(1));

        replayedDuplicates.ShouldBe(replayedOnce);
    }

    [Fact]
    public void Projection_orders_events_by_sequence_number_and_ignores_other_entries()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m3", 3, Recorded("time-entry-1", 90)),
                Event("m2", 2, Recorded("other-entry", 15)),
                Event("m1", 1, Recorded("time-entry-1", 30))
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.DurationMinutes.ShouldBe(90);
    }

    [Theory]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable)]
    public void Projection_freshness_metadata_does_not_present_unfresh_checkpoint_as_fresh(
        ProjectionFreshness freshness,
        ProjectionFreshnessState expectedState)
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45))],
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 1, freshness));

        model.ShouldNotBeNull();
        model.ProjectionFreshness.State.ShouldBe(expectedState);
    }

    private static TimeEntryEvidenceProjection Projector() => new();

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequenceNumber)
        => new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");
}
