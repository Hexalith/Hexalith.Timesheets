using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimesheetPeriods;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApproveOrRejectTimesheetPeriodE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Period_approval_locks_two_submitted_entries_and_records_period_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        StaticAuthorityResolver authority = new(ApprovalAuthorityAction.PeriodApproval);
        TimesheetPeriodApprovalCommandService service = new(accessGuard, authority);
        TimeEntryId first = new("time-entry-1");
        TimeEntryId second = new("time-entry-2");

        TimesheetPeriodApprovalCommandResult result = await service.ApproveAsync(
            Context(),
            new ApproveTimesheetPeriod(PeriodId(), PeriodDecisionId()),
            SubmittedPeriodState(first, second),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [first] = SubmittedState(first),
                [second] = SubmittedState(second)
            },
            PeriodProjection(ProjectionFreshnessMetadata.Fresh, first, second),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeTrue();
        result.EntryResults.SelectMany(static item => item.DomainResult?.Events ?? [])
            .OfType<TimeEntryApproved>()
            .Select(static approved => approved.ApprovalScope)
            .ShouldBe([TimeEntryApprovalScope.TimesheetPeriod, TimeEntryApprovalScope.TimesheetPeriod]);
        result.PeriodResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetPeriodApproved>()
            .PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
    }

    [Fact]
    public async Task Period_rejection_records_selected_entry_reason_without_rejecting_unaffected_entries()
    {
        AllowAllAccessGuard accessGuard = new();
        StaticAuthorityResolver authority = new(ApprovalAuthorityAction.PeriodRejection);
        TimesheetPeriodApprovalCommandService service = new(accessGuard, authority);
        TimeEntryId rejected = new("time-entry-1");
        TimeEntryId unaffected = new("time-entry-2");

        TimesheetPeriodApprovalCommandResult result = await service.RejectAsync(
            Context(),
            new RejectTimesheetPeriod(
                PeriodId(),
                PeriodDecisionId(),
                [new(rejected, new TimeEntryRejectionReason("Missing customer evidence."))],
                new TimesheetPeriodRejectionReason("Period contains entries needing correction.")),
            SubmittedPeriodState(rejected, unaffected),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [rejected] = SubmittedState(rejected),
                [unaffected] = SubmittedState(unaffected)
            },
            PeriodProjection(ProjectionFreshnessMetadata.Fresh, rejected, unaffected),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasPeriodDispatched.ShouldBeTrue();
        result.EntryResults.ShouldHaveSingleItem()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRejected>()
            .Reason.Value.ShouldBe("Missing customer evidence.");
        result.PeriodResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetPeriodRejected>()
            .AffectedTimeEntryIds.ShouldBe([rejected]);
    }

    [Fact]
    public async Task Period_rejection_replay_preserves_period_detail_entry_states_and_reason_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        StaticAuthorityResolver authority = new(ApprovalAuthorityAction.PeriodRejection);
        TimesheetPeriodApprovalCommandService service = new(accessGuard, authority);
        TimeEntryId rejected = new("time-entry-1");
        TimeEntryId unaffected = new("time-entry-2");
        RejectTimesheetPeriod command = new(
            PeriodId(),
            PeriodDecisionId(),
            [new(rejected, new TimeEntryRejectionReason("Missing customer evidence."))],
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."));

        TimesheetPeriodApprovalCommandResult result = await service.RejectAsync(
            Context(),
            command,
            SubmittedPeriodState(rejected, unaffected),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [rejected] = SubmittedState(rejected),
                [unaffected] = SubmittedState(unaffected)
            },
            PeriodProjection(ProjectionFreshnessMetadata.Fresh, rejected, unaffected),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntryRejected entryRejected = result.EntryResults.ShouldHaveSingleItem()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRejected>();
        TimesheetPeriodRejected periodRejected = result.PeriodResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetPeriodRejected>();

        TimesheetPeriodSummaryReadModel? detail = new TimesheetPeriodSummaryProjection().Project(
            Tenant().TenantId,
            PeriodId(),
            [
                Event("m1", 1, Recorded(rejected)),
                Event("m2", 2, Recorded(unaffected)),
                Event("m3", 3, EntrySubmitted(rejected)),
                Event("m4", 4, EntrySubmitted(unaffected)),
                Event("m5", 5, PeriodSubmitted(rejected, unaffected)),
                Event("m6", 6, entryRejected),
                Event("m7", 7, periodRejected)
            ],
            FreshCheckpoint(7));

        detail.ShouldNotBeNull();
        detail.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Rejected);
        detail.AffectedEntryIds.ShouldBe([rejected]);
        detail.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        detail.PeriodDecision.ShouldNotBeNull().ShouldSatisfyAllConditions(
            decision => decision.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Rejected),
            decision => decision.PeriodRejectionReason.ShouldNotBeNull().Value.ShouldBe("Period contains entries needing correction."),
            decision => decision.RejectedEntries.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                rejectedEntry => rejectedEntry.TimeEntryId.ShouldBe(rejected),
                rejectedEntry => rejectedEntry.Reason.Value.ShouldBe("Missing customer evidence.")));
        detail.EntrySummaries.Single(entry => entry.TimeEntryId == rejected)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        detail.EntrySummaries.Single(entry => entry.TimeEntryId == unaffected)
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);

        string json = JsonSerializer.Serialize(detail, JsonOptions);

        json.ShouldContain("\"periodState\":\"Rejected\"");
        json.ShouldContain("\"approvalState\":\"Rejected\"");
        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("Missing customer evidence.");
        json.ShouldContain("Period contains entries needing correction.");
        json.ShouldNotContain("eventStoreEnvelope", Case.Insensitive);
        json.ShouldNotContain("rawClaims", Case.Insensitive);
        json.ShouldNotContain("commandBody", Case.Insensitive);
    }

    [Fact]
    public async Task Period_approval_blocks_stale_projection_and_unresolved_authority_without_period_event()
    {
        AllowAllAccessGuard accessGuard = new();
        StaticAuthorityResolver authority = new(ApprovalAuthorityAction.PeriodApproval);
        TimesheetPeriodApprovalCommandService service = new(accessGuard, authority);
        TimeEntryId entryId = new("time-entry-1");

        TimesheetPeriodApprovalCommandResult stale = await service.ApproveAsync(
            Context(),
            new ApproveTimesheetPeriod(PeriodId(), PeriodDecisionId()),
            SubmittedPeriodState(entryId),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [entryId] = SubmittedState(entryId)
            },
            PeriodProjection(ProjectionFreshnessMetadata.Stale("checkpoint"), entryId),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        stale.WasPeriodDispatched.ShouldBeFalse();
        stale.PeriodResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        stale.BlockingGuidance.ShouldContain(static item => item.Guidance == "Projection is rebuilding.");

        StaticAuthorityResolver deniedAuthority = new(
            ApprovalAuthorityAction.PeriodApproval,
            ApprovalAuthorityResolutionResult.Denied(
                TimesheetsDenialCategory.AmbiguousAuthority,
                "tenant-1 project-1 role Owner upstream detail",
                AuthoritySource(ApprovalAuthorityAction.PeriodApproval)));
        TimesheetPeriodApprovalCommandService deniedService = new(accessGuard, deniedAuthority);

        TimesheetPeriodApprovalCommandResult denied = await deniedService.ApproveAsync(
            Context(),
            new ApproveTimesheetPeriod(PeriodId(), PeriodDecisionId()),
            SubmittedPeriodState(entryId),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [entryId] = SubmittedState(entryId)
            },
            PeriodProjection(ProjectionFreshnessMetadata.Fresh, entryId),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        denied.WasPeriodDispatched.ShouldBeFalse();
        denied.PeriodResult.ShouldBeNull();
        denied.AuthorityResolution.ShouldNotBeNull().Reason.ShouldBe("Authority cannot be resolved.");

        string json = JsonSerializer.Serialize(denied.AuthorityResolution, JsonOptions);

        json.ShouldContain("Authority cannot be resolved.");
        json.ShouldNotContain("tenant-1 project-1 role Owner upstream detail");
        json.ShouldNotContain("Owner");
        json.ShouldNotContain("upstream detail");
    }

    private static TimesheetPeriodState SubmittedPeriodState(params TimeEntryId[] ids)
    {
        TimesheetPeriodState state = new();
        state.Apply(PeriodSubmitted(ids));
        return state;
    }

    private static TimeEntryState SubmittedState(TimeEntryId timeEntryId)
    {
        TimeEntryState state = new();
        state.Apply(Recorded(timeEntryId));
        state.Apply(EntrySubmitted(timeEntryId));
        return state;
    }

    private static TimesheetPeriodProjectionEvent Event(string messageId, long sequence, object payload)
        => new(messageId, sequence, payload);

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
            SubmittedAtUtc(),
            new TimeEntrySubmissionId("submission-existing"),
            TimeEntrySubmissionScope.TimesheetPeriod,
            TimeEntryApprovalState.Submitted);

    private static TimesheetPeriodSubmitted PeriodSubmitted(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Submitter(),
            SubmittedAtUtc(),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "UTC",
            ids,
            TimesheetPeriodApprovalState.Submitted);

    private static TimesheetPeriodSummaryReadModel PeriodProjection(
        ProjectionFreshnessMetadata freshness,
        params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Submitter(),
            SubmittedAtUtc(),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "UTC",
            ids,
            TimesheetPeriodApprovalState.Submitted,
            freshness)
        {
            EntrySummaries = ids.Select(static id => new TimesheetPeriodEntrySummary(
                id,
                TimeEntryApprovalState.Submitted,
                TimeEntryCorrectionState.None,
                TimeEntryLockState.Unlocked,
                ProjectionFreshnessMetadata.Fresh)).ToArray()
        };

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequence)
        => new(Tenant().TenantId, TimesheetPeriodSummaryProjection.ProjectionName, sequence, ProjectionFreshness.Fresh);

    private static TimesheetsRequestContext Context() => new(Tenant(), Submitter(), "correlation-1");

    private static TimesheetPeriodId PeriodId() => new("period-1");

    private static TimesheetPeriodApprovalDecisionId PeriodDecisionId() => new("period-decision-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference Submitter() => new("submitter-1");

    private static PartyReference Approver() => new("approver-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static DateTimeOffset SubmittedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset DecidedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static ApprovalAuthoritySourceAttribution AuthoritySource(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(request.UiAction ?? TimesheetsUiAction.Capture));
    }

    private sealed class StaticAuthorityResolver : ITimesheetsApprovalAuthorityResolver
    {
        private readonly ApprovalAuthorityResolutionResult _result;

        public StaticAuthorityResolver(ApprovalAuthorityAction action)
            : this(action, ApprovalAuthorityResolutionResult.Allowed(AuthoritySource(action)))
        {
        }

        public StaticAuthorityResolver(
            ApprovalAuthorityAction action,
            ApprovalAuthorityResolutionResult result)
        {
            Action = action;
            _result = result;
        }

        public ApprovalAuthorityAction Action { get; }

        public ValueTask<ApprovalAuthorityResolutionResult> ResolveAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            request.Action.ShouldBe(Action);
            return ValueTask.FromResult(_result);
        }
    }
}
