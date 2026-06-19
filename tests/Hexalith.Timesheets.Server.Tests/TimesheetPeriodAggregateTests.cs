using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimesheetPeriodAggregateTests
{
    [Fact]
    public void Submit_weekly_period_emits_tenant_local_boundary_and_utc_audit_evidence()
    {
        SubmitTimesheetPeriod command = WeeklyCommand();

        TimesheetPeriodSubmitted submitted = SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(
                command,
                null,
                Tenant(),
                Submitter(),
                SubmittedAtUtc(),
                ParisPolicy()));

        submitted.TimesheetPeriodId.ShouldBe(command.TimesheetPeriodId);
        submitted.Tenant.ShouldBe(Tenant());
        submitted.Contributor.ShouldBe(Contributor());
        submitted.Submitter.ShouldBe(Submitter());
        submitted.SubmittedAtUtc.ShouldBe(SubmittedAtUtc());
        submitted.PeriodKind.ShouldBe(TimesheetPeriodKind.Weekly);
        submitted.LocalStartDate.ShouldBe(new DateOnly(2026, 3, 23));
        submitted.LocalEndDate.ShouldBe(new DateOnly(2026, 3, 29));
        submitted.PeriodKey.ShouldBe("2026-03-23/2026-03-29");
        submitted.TenantTimeZoneId.ShouldBe("Europe/Paris");
        submitted.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Submitted);
        submitted.IncludedTimeEntryIds.ShouldBe([new TimeEntryId("time-entry-1"), new TimeEntryId("time-entry-2")]);
    }

    [Fact]
    public void Submit_monthly_period_uses_month_boundary_across_cross_midnight_audit_instant()
    {
        TimesheetPeriodSubmitted submitted = SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(
                new(
                    PeriodId(),
                    Contributor(),
                    new TimesheetPeriodRequest(TimesheetPeriodKind.Monthly, new DateOnly(2026, 10, 25)),
                    [new("time-entry-1")]),
                null,
                Tenant(),
                Submitter(),
                new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero),
                ParisPolicy()));

        submitted.PeriodKey.ShouldBe("2026-10");
        submitted.LocalStartDate.ShouldBe(new DateOnly(2026, 10, 1));
        submitted.LocalEndDate.ShouldBe(new DateOnly(2026, 10, 31));
        submitted.SubmittedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Submit_weekly_period_keeps_date_only_boundary_stable_across_dst_spring_forward()
    {
        // 2026-03-29 is the Europe/Paris spring-forward day (02:00 -> 03:00 local skips an hour).
        // The audit instant maps to the skipped local window; tenant-local period keys are
        // calendar dates and must stay stable while the audit instant remains a UTC offset.
        TimesheetPeriodSubmitted submitted = SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(
                new(
                    PeriodId(),
                    Contributor(),
                    new TimesheetPeriodRequest(TimesheetPeriodKind.Weekly, new DateOnly(2026, 3, 29)),
                    [new("time-entry-1")]),
                null,
                Tenant(),
                Submitter(),
                new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero),
                ParisPolicy()));

        submitted.PeriodKey.ShouldBe("2026-03-23/2026-03-29");
        submitted.LocalStartDate.ShouldBe(new DateOnly(2026, 3, 23));
        submitted.LocalEndDate.ShouldBe(new DateOnly(2026, 3, 29));
        submitted.TenantTimeZoneId.ShouldBe("Europe/Paris");
        submitted.SubmittedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Submit_monthly_period_keeps_date_only_boundary_stable_across_dst_fall_back()
    {
        // 2026-10-25 is the Europe/Paris fall-back day (03:00 -> 02:00 local repeats an hour).
        // The audit instant maps to the ambiguous local window; month boundary keys stay
        // calendar dates while the audit instant remains a UTC offset.
        TimesheetPeriodSubmitted submitted = SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(
                new(
                    PeriodId(),
                    Contributor(),
                    new TimesheetPeriodRequest(TimesheetPeriodKind.Monthly, new DateOnly(2026, 10, 25)),
                    [new("time-entry-1")]),
                null,
                Tenant(),
                Submitter(),
                new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero),
                ParisPolicy()));

        submitted.PeriodKey.ShouldBe("2026-10");
        submitted.LocalStartDate.ShouldBe(new DateOnly(2026, 10, 1));
        submitted.LocalEndDate.ShouldBe(new DateOnly(2026, 10, 31));
        submitted.TenantTimeZoneId.ShouldBe("Europe/Paris");
        submitted.SubmittedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Boundary_calculator_fails_closed_on_unknown_period_kind()
        => Should.Throw<ArgumentOutOfRangeException>(() =>
            TenantLocalPeriodBoundaryCalculator.Calculate(
                new TimesheetPeriodRequest(TimesheetPeriodKind.Unknown, new DateOnly(2026, 6, 19)),
                ParisPolicy()));

    [Fact]
    public void Submit_duplicate_same_period_evidence_is_noop()
    {
        TimesheetPeriodState state = new();
        TimesheetPeriodSubmitted submitted = SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(WeeklyCommand(), null, Tenant(), Submitter(), SubmittedAtUtc(), ParisPolicy()));
        state.Apply(submitted);

        TimesheetsDomainResult result = TimesheetPeriod.Handle(
            WeeklyCommand(),
            state,
            Tenant(),
            Submitter(),
            SubmittedAtUtc(),
            ParisPolicy());

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Submit_same_period_id_with_different_membership_is_rejected()
    {
        TimesheetPeriodState state = new();
        state.Apply(SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(WeeklyCommand(), null, Tenant(), Submitter(), SubmittedAtUtc(), ParisPolicy())));

        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            WeeklyCommand() with { TimeEntryIds = [new("time-entry-1"), new("time-entry-3")] },
            state,
            Tenant(),
            Submitter(),
            SubmittedAtUtc(),
            ParisPolicy()));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "timesheetPeriodId" && error.Code == "conflict");
    }

    [Fact]
    public void Submit_rejects_non_utc_timestamp_empty_membership_and_unknown_period_kind()
    {
        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            new(PeriodId(), Contributor(), new(TimesheetPeriodKind.Unknown, new DateOnly(2026, 6, 19)), []),
            null,
            Tenant(),
            Submitter(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.FromHours(2)),
            ParisPolicy()));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "submittedAtUtc" && error.Code == "utc-required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "period.periodKind" && error.Code == "invalid");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "timeEntryIds" && error.Code == "required");
    }

    [Fact]
    public void Approve_submitted_period_emits_grouped_period_decision()
    {
        TimesheetPeriodState state = SubmittedState();
        ApproveTimesheetPeriod command = ApproveCommand();

        TimesheetPeriodApproved approved = SingleSuccess<TimesheetPeriodApproved>(
            TimesheetPeriod.Handle(
                command,
                state,
                Approver(),
                Tenant(),
                DecidedAtUtc(),
                AuthoritySource(ApprovalAuthorityAction.PeriodApproval)));

        approved.TimesheetPeriodId.ShouldBe(PeriodId());
        approved.Tenant.ShouldBe(Tenant());
        approved.Contributor.ShouldBe(Contributor());
        approved.Approver.ShouldBe(Approver());
        approved.DecidedAtUtc.ShouldBe(DecidedAtUtc());
        approved.TimesheetPeriodApprovalDecisionId.ShouldBe(command.TimesheetPeriodApprovalDecisionId);
        approved.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
        approved.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.PeriodApproval);
        approved.IncludedTimeEntryIds.ShouldBe([new TimeEntryId("time-entry-1"), new TimeEntryId("time-entry-2")]);
    }

    [Fact]
    public void Reject_submitted_period_emits_grouped_period_and_selected_entry_evidence()
    {
        TimesheetPeriodState state = SubmittedState();
        RejectTimesheetPeriod command = RejectCommand();

        TimesheetPeriodRejected rejected = SingleSuccess<TimesheetPeriodRejected>(
            TimesheetPeriod.Handle(
                command,
                state,
                Approver(),
                Tenant(),
                DecidedAtUtc(),
                AuthoritySource(ApprovalAuthorityAction.PeriodRejection)));

        rejected.TimesheetPeriodId.ShouldBe(PeriodId());
        rejected.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Rejected);
        rejected.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.PeriodRejection);
        rejected.AffectedTimeEntryIds.ShouldBe([new TimeEntryId("time-entry-1")]);
        rejected.Reason.Value.ShouldBe("Period contains entries needing correction.");
        rejected.RejectedEntries.Single().Reason.Value.ShouldBe("Missing customer evidence.");
    }

    [Fact]
    public void Approve_rejects_non_utc_missing_authority_and_non_submitted_state()
    {
        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            ApproveCommand(),
            null,
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.FromHours(2)),
            null));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "decidedAtUtc" && error.Code == "utc-required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "authoritySource" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "timesheetPeriodId" && error.Code == "not-submitted");
    }

    [Fact]
    public void Reject_rejects_blank_reason_and_affected_entry_outside_submitted_period()
    {
        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            RejectCommand() with
            {
                RejectedEntries =
                [
                    new(new TimeEntryId("time-entry-3"), new TimeEntryRejectionReason("Missing customer evidence."))
                ]
            },
            SubmittedState(),
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection)));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "rejectedEntries.timeEntryId" && error.Code == "not-in-period");
    }

    [Fact]
    public void Reject_requires_period_and_entry_rejection_reasons()
    {
        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            new RejectTimesheetPeriod(
                PeriodId(),
                PeriodDecisionId(),
                [],
                null!),
            SubmittedState(),
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection)));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "reason" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "rejectedEntries" && error.Code == "required");
    }

    [Fact]
    public void Approve_duplicate_same_period_decision_is_noop()
    {
        TimesheetPeriodState state = SubmittedState();
        state.Apply(SingleSuccess<TimesheetPeriodApproved>(TimesheetPeriod.Handle(
            ApproveCommand(),
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval))));

        TimesheetsDomainResult result = TimesheetPeriod.Handle(
            ApproveCommand(),
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval));

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Reject_duplicate_same_period_decision_is_noop_but_same_id_different_reason_conflicts()
    {
        TimesheetPeriodState state = SubmittedState();
        state.Apply(SingleSuccess<TimesheetPeriodRejected>(TimesheetPeriod.Handle(
            RejectCommand(),
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection))));

        TimesheetPeriod.Handle(
            RejectCommand(),
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection))
            .IsNoOp.ShouldBeTrue();

        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            RejectCommand() with { Reason = new TimesheetPeriodRejectionReason("Different period reason.") },
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection)));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "timesheetPeriodApprovalDecisionId" && error.Code == "conflict");
    }

    [Fact]
    public void Terminal_period_decision_rejects_conflicting_later_decision()
    {
        TimesheetPeriodState state = SubmittedState();
        state.Apply(SingleSuccess<TimesheetPeriodApproved>(TimesheetPeriod.Handle(
            ApproveCommand(),
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval))));

        TimesheetsRejection rejection = SingleRejection(TimesheetPeriod.Handle(
            ApproveCommand() with { TimesheetPeriodApprovalDecisionId = new("different-decision") },
            state,
            Approver(),
            Tenant(),
            DecidedAtUtc(),
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval)));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "periodState" && error.Code == "terminal-state");
    }

    private static SubmitTimesheetPeriod WeeklyCommand()
        => new(
            PeriodId(),
            Contributor(),
            new TimesheetPeriodRequest(TimesheetPeriodKind.Weekly, new DateOnly(2026, 3, 29)),
            [new("time-entry-1"), new("time-entry-2")]);

    private static ApproveTimesheetPeriod ApproveCommand()
        => new(PeriodId(), PeriodDecisionId());

    private static RejectTimesheetPeriod RejectCommand()
        => new(
            PeriodId(),
            PeriodDecisionId(),
            [
                new(new TimeEntryId("time-entry-1"), new TimeEntryRejectionReason("Missing customer evidence."))
            ],
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."));

    private static TimesheetPeriodState SubmittedState()
    {
        TimesheetPeriodState state = new();
        state.Apply(SingleSuccess<TimesheetPeriodSubmitted>(
            TimesheetPeriod.Handle(WeeklyCommand(), null, Tenant(), Submitter(), SubmittedAtUtc(), ParisPolicy())));
        return state;
    }

    private static TimesheetPeriodId PeriodId() => new("period-1");

    private static TimesheetPeriodApprovalDecisionId PeriodDecisionId() => new("period-decision-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("contributor-1");

    private static PartyReference Submitter() => new("submitter-1");

    private static PartyReference Approver() => new("approver-1");

    private static DateTimeOffset SubmittedAtUtc() => new(2026, 3, 29, 0, 30, 0, TimeSpan.Zero);

    private static DateTimeOffset DecidedAtUtc() => new(2026, 3, 29, 1, 30, 0, TimeSpan.Zero);

    private static TenantTimesheetPeriodPolicy ParisPolicy() => new("Europe/Paris", DayOfWeek.Monday);

    private static ApprovalAuthoritySourceAttribution AuthoritySource(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static T SingleSuccess<T>(TimesheetsDomainResult result)
    {
        result.IsSuccess.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<T>();
    }

    private static TimesheetsRejection SingleRejection(TimesheetsDomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }
}
