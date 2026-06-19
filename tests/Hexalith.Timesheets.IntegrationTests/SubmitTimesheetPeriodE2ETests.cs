using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class SubmitTimesheetPeriodE2ETests
{
    [Fact]
    public async Task Period_submission_blocks_rejected_entries_then_submits_two_drafts_with_already_submitted_entry()
    {
        AllowAllAccessGuard accessGuard = new();
        TimesheetPeriodSubmissionCommandService service = new(accessGuard);
        TimeEntryId firstDraft = new("time-entry-1");
        TimeEntryId secondDraft = new("time-entry-2");
        TimeEntryId alreadySubmitted = new("time-entry-3");
        TimeEntryId rejected = new("time-entry-4");
        Dictionary<TimeEntryId, TimeEntryState?> states = new()
        {
            [firstDraft] = RecordedState(firstDraft),
            [secondDraft] = RecordedState(secondDraft, new DateOnly(2026, 6, 20)),
            [alreadySubmitted] = SubmittedState(alreadySubmitted),
            [rejected] = RejectedState(rejected)
        };

        TimesheetPeriodSubmissionCommandResult blocked = await service.SubmitAsync(
            Context(),
            Command("period-blocked", firstDraft, secondDraft, alreadySubmitted, rejected),
            null,
            states,
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        blocked.WasPeriodDispatched.ShouldBeFalse();
        blocked.EntryResults.ShouldBeEmpty();
        blocked.ValidTimeEntryIds.ShouldBe([firstDraft, secondDraft, alreadySubmitted]);
        blocked.BlockingGuidance.ShouldContain(static item =>
            item.TimeEntryId == new TimeEntryId("time-entry-4")
            && item.Field == "approvalState"
            && item.Guidance == "Entry needs correction.");

        TimesheetPeriodSubmissionCommandResult submitted = await service.SubmitAsync(
            Context(),
            Command("period-submitted", firstDraft, secondDraft, alreadySubmitted),
            null,
            states,
            FreshCatalog(),
            Policy(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        submitted.WasPeriodDispatched.ShouldBeTrue();
        submitted.BlockingGuidance.ShouldBeEmpty();
        submitted.EntryResults.Count.ShouldBe(2);
        submitted.EntryResults.SelectMany(static entry => entry.DomainResult?.Events ?? [])
            .OfType<TimeEntrySubmitted>()
            .Select(static item => item.TimeEntryId)
            .ShouldBe([firstDraft, secondDraft]);
        TimesheetPeriodSubmitted period = submitted.PeriodResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetPeriodSubmitted>();
        period.IncludedTimeEntryIds.ShouldBe([firstDraft, secondDraft, alreadySubmitted]);
        period.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Submitted);
        period.PeriodKey.ShouldBe("2026-06");
        accessGuard.Requests.Count(static request => request.Operation == TimesheetsOperation.Command)
            .ShouldBeGreaterThanOrEqualTo(6);
    }

    private static SubmitTimesheetPeriod Command(string periodId, params TimeEntryId[] ids)
        => new(
            new TimesheetPeriodId(periodId),
            Contributor(),
            new TimesheetPeriodRequest(TimesheetPeriodKind.Monthly, new DateOnly(2026, 6, 19)),
            ids);

    private static TimeEntryState RecordedState(TimeEntryId timeEntryId, DateOnly? serviceDate = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
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

    private static TimesheetsRequestContext Context()
        => new(Tenant(), Submitter(), "correlation-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference Submitter() => new("submitter-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TenantTimesheetPeriodPolicy Policy() => new("UTC", DayOfWeek.Monday);

    private static DateTimeOffset SubmittedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(request.UiAction ?? TimesheetsUiAction.Capture));
        }
    }
}
