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

    [Fact]
    public void Projection_applies_period_approval_without_flattening_entry_lock_state_source()
    {
        TimeEntryId entryId = new("time-entry-1");

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m1", 1, Recorded(entryId)),
                Event("m2", 2, EntrySubmitted(entryId)),
                Event("m3", 3, EntryApproved(entryId)),
                Event("m4", 4, PeriodSubmitted(entryId)),
                Event("m5", 5, PeriodApproved(entryId))
            ],
            FreshCheckpoint(5));

        model.ShouldNotBeNull();
        model.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
        model.PeriodDecision.ShouldNotBeNull();
        model.PeriodDecision.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
        model.PeriodDecision.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.PeriodApproval);
        model.AffectedEntryIds.ShouldBe([entryId]);
        TimesheetPeriodEntrySummary entry = model.EntrySummaries.ShouldHaveSingleItem();
        entry.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        entry.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
    }

    [Fact]
    public void Projection_applies_period_rejection_with_selected_entry_reasons_and_separate_entry_state()
    {
        TimeEntryId rejectedEntry = new("time-entry-1");
        TimeEntryId submittedEntry = new("time-entry-2");

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m1", 1, Recorded(rejectedEntry)),
                Event("m2", 2, Recorded(submittedEntry)),
                Event("m3", 3, EntrySubmitted(rejectedEntry)),
                Event("m4", 4, EntrySubmitted(submittedEntry)),
                Event("m5", 5, EntryRejected(rejectedEntry)),
                Event("m6", 6, PeriodSubmitted(rejectedEntry, submittedEntry)),
                Event("m7", 7, PeriodRejected(rejectedEntry))
            ],
            FreshCheckpoint(7));

        model.ShouldNotBeNull();
        model.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Rejected);
        model.PeriodDecision.ShouldNotBeNull();
        model.PeriodDecision.PeriodRejectionReason.ShouldNotBeNull().Value.ShouldBe("Period contains entries needing correction.");
        model.PeriodDecision.RejectedEntries.ShouldHaveSingleItem().Reason.Value.ShouldBe("Missing customer evidence.");
        model.EntrySummaries.Single(entry => entry.TimeEntryId == rejectedEntry)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        model.EntrySummaries.Single(entry => entry.TimeEntryId == submittedEntry)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    [Fact]
    public void Projection_deduplicates_duplicate_period_decision_delivery()
    {
        TimeEntryId entryId = new("time-entry-1");
        TimesheetPeriodProjectionEvent decision = Event("m5", 5, PeriodApproved(entryId));

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m1", 1, Recorded(entryId)),
                Event("m2", 2, EntrySubmitted(entryId)),
                Event("m3", 3, EntryApproved(entryId)),
                Event("m4", 4, PeriodSubmitted(entryId)),
                decision,
                decision
            ],
            FreshCheckpoint(5));

        model.ShouldNotBeNull();
        model.PeriodDecision.ShouldNotBeNull();
        model.AffectedEntryIds.ShouldBe([entryId]);
    }

    [Fact]
    public void Projection_keeps_rebuilding_when_period_decision_arrives_before_entry_evidence()
    {
        TimeEntryId missing = new("time-entry-1");

        TimesheetPeriodSummaryReadModel? model = Projector().Project(
            "tenant-1",
            PeriodId(),
            [
                Event("m1", 1, PeriodSubmitted(missing)),
                Event("m2", 2, PeriodApproved(missing))
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
        model.PeriodDecision.ShouldNotBeNull();
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Rebuilding);
        model.IncompleteEntryEvidenceIds.ShouldBe([missing]);
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

    private static TimeEntryApproved EntryApproved(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("period-decision-1"),
            TimeEntryApprovalState.Approved,
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval),
            TimeEntryApprovalScope.TimesheetPeriod);

    private static TimeEntryRejected EntryRejected(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("period-decision-1"),
            TimeEntryApprovalState.Rejected,
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection),
            TimeEntryApprovalScope.TimesheetPeriod,
            new TimeEntryRejectionReason("Missing customer evidence."));

    private static TimesheetPeriodApproved PeriodApproved(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Approver(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimesheetPeriodApprovalDecisionId("period-decision-1"),
            TimesheetPeriodApprovalState.Approved,
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval),
            ids);

    private static TimesheetPeriodRejected PeriodRejected(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Approver(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimesheetPeriodApprovalDecisionId("period-decision-1"),
            TimesheetPeriodApprovalState.Rejected,
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection),
            ids,
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."),
            ids.Select(static id => new TimesheetPeriodSelectedEntryRejectionEvidence(
                id,
                new TimeEntryRejectionReason("Missing customer evidence."))).ToArray());

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequence)
        => new("tenant-1", TimesheetPeriodSummaryProjection.ProjectionName, sequence, ProjectionFreshness.Fresh);

    private static TimesheetPeriodId PeriodId() => new("period-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference Submitter() => new("submitter-1");

    private static PartyReference Approver() => new("approver-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static ApprovalAuthoritySourceAttribution AuthoritySource(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);
}
