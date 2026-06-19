using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimesheetPeriodAuthorizationTests
{
    [Fact]
    public async Task Period_submission_dispatches_draft_entry_transitions_and_period_event_only_when_all_entries_are_valid()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId draft1 = new("time-entry-1");
        TimeEntryId draft2 = new("time-entry-2");
        TimeEntryId submitted = new("time-entry-3");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(draft1, draft2, submitted),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [draft1] = RecordedState(draft1),
                [draft2] = RecordedState(draft2, new DateOnly(2026, 6, 20)),
                [submitted] = SubmittedState(submitted)
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeTrue();
        result.HasBlockedEntries.ShouldBeFalse();
        result.EntryResults.Count.ShouldBe(2);
        result.EntryResults.SelectMany(static entry => entry.DomainResult?.Events ?? [])
            .OfType<TimeEntrySubmitted>()
            .Select(static item => item.SubmissionScope)
            .ShouldBe([TimeEntrySubmissionScope.TimesheetPeriod, TimeEntrySubmissionScope.TimesheetPeriod]);
        TimesheetPeriodSubmitted period = result.PeriodResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetPeriodSubmitted>();
        period.IncludedTimeEntryIds.ShouldBe([draft1, draft2, submitted]);
    }

    [Fact]
    public async Task Period_submission_blocks_whole_period_when_one_entry_needs_correction()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId valid = new("time-entry-1");
        TimeEntryId rejected = new("time-entry-2");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(valid, rejected),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [valid] = RecordedState(valid),
                [rejected] = RejectedState(rejected)
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeFalse();
        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBe([valid]);
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-2")
            && item.Field == "approvalState"
            && item.Code == "invalid-transition"
            && item.Guidance == "Entry needs correction.");
        result.PeriodResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Period_submission_blocks_cross_boundary_entries_without_silently_submitting_subset()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId valid = new("time-entry-1");
        TimeEntryId outside = new("time-entry-2");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(valid, outside),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [valid] = RecordedState(valid),
                [outside] = RecordedState(outside, new DateOnly(2026, 7, 1))
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBe([valid]);
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-2")
            && item.Field == "serviceDate"
            && item.Code == "outside-period");
    }

    [Fact]
    public async Task Period_submission_blocks_missing_entries_without_dispatching_valid_subset()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId valid = new("time-entry-1");
        TimeEntryId missing = new("time-entry-2");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(valid, missing),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [valid] = RecordedState(valid)
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeFalse();
        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBe([valid]);
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-2")
            && item.Field == "timeEntryId"
            && item.Code == "missing");
    }

    [Fact]
    public async Task Period_submission_blocks_entries_for_another_contributor_without_dispatching_valid_subset()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId valid = new("time-entry-1");
        TimeEntryId anotherContributor = new("time-entry-2");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(valid, anotherContributor),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [valid] = RecordedState(valid),
                [anotherContributor] = RecordedState(anotherContributor, contributor: OtherContributor())
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeFalse();
        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBe([valid]);
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-2")
            && item.Field == "contributor"
            && item.Code == "mismatch");
    }

    [Fact]
    public async Task Period_submission_blocks_when_activity_type_catalog_is_not_fresh()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId entryId = new("time-entry-1");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(entryId),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [entryId] = RecordedState(entryId)
            },
            StaleCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeFalse();
        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBeEmpty();
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-1")
            && item.Field == "activityTypeCatalog"
            && item.Code == "not-fresh"
            && item.Guidance == "Projection is rebuilding.");
    }

    [Fact]
    public async Task Period_submission_blocks_superseded_locked_entries()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId valid = new("time-entry-1");
        TimeEntryId superseded = new("time-entry-2");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(valid, superseded),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [valid] = RecordedState(valid),
                [superseded] = SupersededSubmittedState(superseded)
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeFalse();
        result.EntryResults.ShouldBeEmpty();
        result.ValidTimeEntryIds.ShouldBe([valid]);
        result.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-2")
            && item.Field == "lockState"
            && item.Code == "superseded");
    }

    [Fact]
    public async Task Period_submission_denial_copy_hides_raw_authority_details()
    {
        ITimesheetsAccessGuard guard = AllowingGuard();
        guard.AuthorizeAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request => request != null && request.Project == Project()),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(
                TimesheetsDenialCategory.AmbiguousAuthority,
                "tenant-1 project-1 role Owner upstream detail")));
        TimesheetPeriodSubmissionCommandService service = new(guard);
        TimeEntryId entryId = new("time-entry-1");

        TimesheetPeriodSubmissionCommandResult result = await service.SubmitAsync(
            Context(),
            Command(entryId),
            null,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [entryId] = RecordedState(entryId)
            },
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimesheetPeriodBlockingEntryGuidance guidance = result.BlockingGuidance.ShouldHaveSingleItem();
        guidance.Guidance.ShouldBe("Authority cannot be resolved.");
        guidance.Guidance.ShouldNotContain("project", Case.Insensitive);
        guidance.Guidance.ShouldNotContain("role", Case.Insensitive);
        result.EntryResults.ShouldBeEmpty();
    }

    private static ITimesheetsAccessGuard AllowingGuard()
    {
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed()));
        return guard;
    }

    private static SubmitTimesheetPeriod Command(params TimeEntryId[] ids)
        => new(
            new TimesheetPeriodId("period-1"),
            Contributor(),
            new TimesheetPeriodRequest(TimesheetPeriodKind.Monthly, new DateOnly(2026, 6, 19)),
            ids);

    private static TimeEntryState RecordedState(
        TimeEntryId timeEntryId,
        DateOnly? serviceDate = null,
        PartyReference? contributor = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            serviceDate ?? new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable));
        return state;
    }

    private static TimeEntryState SupersededSubmittedState(TimeEntryId timeEntryId)
    {
        TimeEntryState state = RejectedState(timeEntryId);
        state.Apply(new TimeEntryCorrected(
            timeEntryId,
            new TimeEntryCorrectionId("correction-1"),
            Tenant(),
            Submitter(),
            SubmittedAtUtc().AddHours(1),
            CorrectionValues(),
            CorrectionValues() with { DurationMinutes = 75 },
            new TimeEntryRejectionReason("Entry needs correction."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Superseded));
        state.Apply(new TimeEntrySubmitted(
            timeEntryId,
            Submitter(),
            Tenant(),
            SubmittedAtUtc().AddHours(2),
            new TimeEntrySubmissionId("submission-resubmitted"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimeEntryState SubmittedState(TimeEntryId timeEntryId)
    {
        TimeEntryState state = RecordedState(timeEntryId);
        state.Apply(new TimeEntrySubmitted(
            timeEntryId,
            Submitter(),
            Tenant(),
            SubmittedAtUtc(),
            new TimeEntrySubmissionId("submission-existing"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimeEntryState RejectedState(TimeEntryId timeEntryId)
    {
        TimeEntryState state = SubmittedState(timeEntryId);
        state.Apply(new TimeEntryRejected(
            timeEntryId,
            new PartyReference("approver-1"),
            Tenant(),
            SubmittedAtUtc().AddMinutes(30),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Rejected,
            new(
                ApprovalAuthorityAction.EntryRejection,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "policy",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Entry needs correction.")));
        return state;
    }

    private static ActivityTypeCatalogReadModel FreshCatalog()
        => new(
            [
                new(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogReadModel StaleCatalog()
        => FreshCatalog() with { ProjectionFreshness = ProjectionFreshnessMetadata.Stale("catalog-checkpoint") };

    private static TimeEntryCorrectionValues CorrectionValues()
        => new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimesheetsRequestContext Context()
        => new(Tenant(), Submitter(), "correlation-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference OtherContributor() => new("contributor-2");

    private static PartyReference Submitter() => new("submitter-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TenantTimesheetPeriodPolicy Policy() => new("UTC", DayOfWeek.Monday);

    private static DateTimeOffset SubmittedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
}
