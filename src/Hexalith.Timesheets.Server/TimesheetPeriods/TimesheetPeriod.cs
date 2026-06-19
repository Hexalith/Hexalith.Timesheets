using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public static class TimesheetPeriod
{
    public static TimesheetsDomainResult Handle(
        ApproveTimesheetPeriod command,
        TimesheetPeriodState? state,
        PartyReference? approver,
        TenantReference? tenant,
        DateTimeOffset decidedAtUtc,
        ApprovalAuthoritySourceAttribution? authoritySource)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<TimesheetsFieldError> errors = [];
        ValidateDecision(
            command.TimesheetPeriodId,
            command.TimesheetPeriodApprovalDecisionId,
            state,
            approver,
            tenant,
            decidedAtUtc,
            authoritySource,
            ApprovalAuthorityAction.PeriodApproval,
            TimesheetPeriodApprovalState.Approved,
            errors);

        if (IsDuplicateDecision(
            state,
            command.TimesheetPeriodApprovalDecisionId,
            TimesheetPeriodApprovalState.Approved,
            state?.IncludedTimeEntryIds ?? [],
            null,
            [])
            && errors.Count == 0)
        {
            return TimesheetsDomainResult.NoOp();
        }

        AddSameDecisionConflict(
            state,
            command.TimesheetPeriodApprovalDecisionId,
            TimesheetPeriodApprovalState.Approved,
            errors);

        if (errors.Count > 0)
        {
            return Reject("Timesheet Period approval failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new TimesheetPeriodApproved(
                command.TimesheetPeriodId,
                tenant!,
                state!.Contributor!,
                approver!,
                decidedAtUtc.ToUniversalTime(),
                command.TimesheetPeriodApprovalDecisionId,
                TimesheetPeriodApprovalState.Approved,
                authoritySource!,
                [.. state.IncludedTimeEntryIds])
        ]);
    }

    public static TimesheetsDomainResult Handle(
        RejectTimesheetPeriod command,
        TimesheetPeriodState? state,
        PartyReference? approver,
        TenantReference? tenant,
        DateTimeOffset decidedAtUtc,
        ApprovalAuthoritySourceAttribution? authoritySource)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> rejectedEntries = command.RejectedEntries ?? [];
        IReadOnlyList<TimeEntryId> affectedEntryIds = Distinct(rejectedEntries.Select(static entry => entry.TimeEntryId));
        List<TimesheetsFieldError> errors = [];
        ValidateDecision(
            command.TimesheetPeriodId,
            command.TimesheetPeriodApprovalDecisionId,
            state,
            approver,
            tenant,
            decidedAtUtc,
            authoritySource,
            ApprovalAuthorityAction.PeriodRejection,
            TimesheetPeriodApprovalState.Rejected,
            errors);
        ValidatePeriodRejection(command, state, rejectedEntries, affectedEntryIds, errors);

        if (IsDuplicateDecision(
            state,
            command.TimesheetPeriodApprovalDecisionId,
            TimesheetPeriodApprovalState.Rejected,
            affectedEntryIds,
            command.Reason,
            rejectedEntries)
            && errors.Count == 0)
        {
            return TimesheetsDomainResult.NoOp();
        }

        AddSameDecisionConflict(
            state,
            command.TimesheetPeriodApprovalDecisionId,
            TimesheetPeriodApprovalState.Rejected,
            errors);

        if (errors.Count > 0)
        {
            return Reject("Timesheet Period rejection failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new TimesheetPeriodRejected(
                command.TimesheetPeriodId,
                tenant!,
                state!.Contributor!,
                approver!,
                decidedAtUtc.ToUniversalTime(),
                command.TimesheetPeriodApprovalDecisionId,
                TimesheetPeriodApprovalState.Rejected,
                authoritySource!,
                affectedEntryIds,
                command.Reason,
                rejectedEntries)
        ]);
    }

    public static TimesheetsDomainResult Handle(
        SubmitTimesheetPeriod command,
        TimesheetPeriodState? state,
        TenantReference? tenant,
        PartyReference? submitter,
        DateTimeOffset submittedAtUtc,
        TenantTimesheetPeriodPolicy? periodPolicy)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<TimesheetsFieldError> errors = [];
        TenantLocalPeriodBoundary? boundary = ValidateSubmission(
            command,
            tenant,
            submitter,
            submittedAtUtc,
            periodPolicy,
            errors);

        if (state?.IsSubmitted == true && state.TimesheetPeriodId == command.TimesheetPeriodId)
        {
            if (boundary is not null
                && state.Contributor == command.Contributor
                && state.Boundary == boundary
                && SameMembership(state.IncludedTimeEntryIds, command.TimeEntryIds))
            {
                return TimesheetsDomainResult.NoOp();
            }

            errors.Add(new("timesheetPeriodId", "conflict", "Timesheet Period ID already exists with different period evidence."));
        }

        if (errors.Count > 0)
        {
            return Reject("Timesheet Period submission failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new TimesheetPeriodSubmitted(
                command.TimesheetPeriodId,
                tenant!,
                command.Contributor,
                submitter!,
                submittedAtUtc.ToUniversalTime(),
                boundary!.PeriodKind,
                boundary.PeriodKey,
                boundary.LocalStartDate,
                boundary.LocalEndDate,
                boundary.TenantTimeZoneId,
                Distinct(command.TimeEntryIds),
                TimesheetPeriodApprovalState.Submitted)
        ]);
    }

    private static void ValidateDecision(
        TimesheetPeriodId? timesheetPeriodId,
        TimesheetPeriodApprovalDecisionId? decisionId,
        TimesheetPeriodState? state,
        PartyReference? approver,
        TenantReference? tenant,
        DateTimeOffset decidedAtUtc,
        ApprovalAuthoritySourceAttribution? authoritySource,
        ApprovalAuthorityAction expectedAction,
        TimesheetPeriodApprovalState resultingState,
        List<TimesheetsFieldError> errors)
    {
        if (timesheetPeriodId is null || string.IsNullOrWhiteSpace(timesheetPeriodId.Value))
        {
            errors.Add(new("timesheetPeriodId", "required", "Timesheet Period ID is required."));
        }

        if (decisionId is null || string.IsNullOrWhiteSpace(decisionId.Value))
        {
            errors.Add(new("timesheetPeriodApprovalDecisionId", "required", "Timesheet Period approval decision ID is required."));
        }

        if (approver is null || string.IsNullOrWhiteSpace(approver.PartyId))
        {
            errors.Add(new("approver", "required", "Approver Party reference is required."));
        }

        if (tenant is null || string.IsNullOrWhiteSpace(tenant.TenantId))
        {
            errors.Add(new("tenant", "required", "Tenant reference is required."));
        }

        if (decidedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new("decidedAtUtc", "utc-required", "Approval decision timestamp must be a UTC instant."));
        }

        ValidateAuthoritySource(authoritySource, expectedAction, errors);

        if (state?.IsSubmitted != true)
        {
            errors.Add(new("timesheetPeriodId", "not-submitted", "Timesheet Period must be submitted before approval."));
            return;
        }

        if (state.TimesheetPeriodId != timesheetPeriodId)
        {
            errors.Add(new("timesheetPeriodId", "mismatch", "Timesheet Period decision must target the handled Timesheet Period."));
        }

        if (state.PeriodState is TimesheetPeriodApprovalState.Approved or TimesheetPeriodApprovalState.Rejected)
        {
            if (state.TimesheetPeriodApprovalDecisionId == decisionId
                && state.PeriodState == resultingState)
            {
                return;
            }

            errors.Add(new("periodState", "terminal-state", "Approved or Rejected Timesheet Periods cannot receive another approval decision."));
            return;
        }

        if (state.PeriodState != TimesheetPeriodApprovalState.Submitted)
        {
            errors.Add(new("periodState", "invalid-transition", "Only Submitted Timesheet Periods can be approved or rejected."));
        }

        if (state.Tenant is null || string.IsNullOrWhiteSpace(state.Tenant.TenantId))
        {
            errors.Add(new("state.tenant", "required", "Submitted Timesheet Period tenant evidence is required."));
        }

        if (state.Contributor is null || string.IsNullOrWhiteSpace(state.Contributor.PartyId))
        {
            errors.Add(new("state.contributor", "required", "Submitted Timesheet Period contributor evidence is required."));
        }
    }

    private static void ValidateAuthoritySource(
        ApprovalAuthoritySourceAttribution? authoritySource,
        ApprovalAuthorityAction expectedAction,
        List<TimesheetsFieldError> errors)
    {
        if (authoritySource is null)
        {
            errors.Add(new("authoritySource", "required", "Approval authority source is required."));
            return;
        }

        if (authoritySource.Action == ApprovalAuthorityAction.Unknown)
        {
            errors.Add(new("authoritySource.action", "unknown", "Approval authority action is required."));
        }
        else if (authoritySource.Action != expectedAction)
        {
            errors.Add(new("authoritySource.action", "mismatch", "Approval authority action does not match the decision."));
        }

        if (authoritySource.Source == ApprovalAuthoritySource.Unknown)
        {
            errors.Add(new("authoritySource.source", "unknown", "Approval authority source is required."));
        }

        if (authoritySource.DecisionState == ApprovalAuthorityDecisionState.Unknown)
        {
            errors.Add(new("authoritySource.decisionState", "unknown", "Approval authority decision state is required."));
        }
        else if (authoritySource.DecisionState != ApprovalAuthorityDecisionState.Allowed)
        {
            errors.Add(new("authoritySource.decisionState", "not-allowed", "Approval authority must allow the decision."));
        }

        if (string.IsNullOrWhiteSpace(authoritySource.PolicyKey))
        {
            errors.Add(new("authoritySource.policyKey", "required", "Approval authority policy key is required."));
        }

        if (string.IsNullOrWhiteSpace(authoritySource.PolicyVersion))
        {
            errors.Add(new("authoritySource.policyVersion", "required", "Approval authority policy version is required."));
        }

        if (authoritySource.Freshness is null)
        {
            errors.Add(new("authoritySource.freshness", "required", "Approval authority freshness is required."));
        }
    }

    private static void ValidatePeriodRejection(
        RejectTimesheetPeriod command,
        TimesheetPeriodState? state,
        IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> rejectedEntries,
        IReadOnlyList<TimeEntryId> affectedEntryIds,
        List<TimesheetsFieldError> errors)
    {
        ValidatePeriodRejectionReason(command.Reason, "reason", errors);

        if (rejectedEntries.Count == 0)
        {
            errors.Add(new("rejectedEntries", "required", "At least one rejected Time Entry is required."));
            return;
        }

        if (affectedEntryIds.Count != rejectedEntries.Count)
        {
            errors.Add(new("rejectedEntries", "distinct", "Rejected Time Entries must be distinct."));
        }

        for (int i = 0; i < rejectedEntries.Count; i++)
        {
            TimesheetPeriodSelectedEntryRejectionEvidence rejected = rejectedEntries[i];
            if (rejected.TimeEntryId is null || string.IsNullOrWhiteSpace(rejected.TimeEntryId.Value))
            {
                errors.Add(new($"rejectedEntries[{i}].timeEntryId", "required", "Rejected Time Entry ID is required."));
            }

            ValidateEntryRejectionReason(rejected.Reason, $"rejectedEntries[{i}].reason", errors);
        }

        if (state?.IsSubmitted != true)
        {
            return;
        }

        HashSet<TimeEntryId> included = state.IncludedTimeEntryIds.ToHashSet();
        foreach (TimeEntryId affectedId in affectedEntryIds)
        {
            if (!included.Contains(affectedId))
            {
                errors.Add(new("rejectedEntries.timeEntryId", "not-in-period", "Rejected Time Entry must be included in the submitted Timesheet Period."));
            }
        }
    }

    private static void ValidatePeriodRejectionReason(
        TimesheetPeriodRejectionReason? reason,
        string field,
        List<TimesheetsFieldError> errors)
    {
        if (reason is null)
        {
            errors.Add(new(field, "required", "Period rejection reason is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(reason.Value))
        {
            errors.Add(new(field, "blank", "Period rejection reason cannot be blank."));
        }

        if (reason.Value.Length > TimesheetPeriodRejectionReason.MaxLength)
        {
            errors.Add(new(field, "too-long", "Period rejection reason exceeds the maximum supported length."));
        }
    }

    private static void ValidateEntryRejectionReason(
        TimeEntryRejectionReason? reason,
        string field,
        List<TimesheetsFieldError> errors)
    {
        if (reason is null)
        {
            errors.Add(new(field, "required", "Entry rejection reason is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(reason.Value))
        {
            errors.Add(new(field, "blank", "Entry rejection reason cannot be blank."));
        }

        if (reason.Value.Length > TimeEntryRejectionReason.MaxLength)
        {
            errors.Add(new(field, "too-long", "Entry rejection reason exceeds the maximum supported length."));
        }
    }

    private static TenantLocalPeriodBoundary? ValidateSubmission(
        SubmitTimesheetPeriod command,
        TenantReference? tenant,
        PartyReference? submitter,
        DateTimeOffset submittedAtUtc,
        TenantTimesheetPeriodPolicy? periodPolicy,
        List<TimesheetsFieldError> errors)
    {
        if (command.TimesheetPeriodId is null || string.IsNullOrWhiteSpace(command.TimesheetPeriodId.Value))
        {
            errors.Add(new("timesheetPeriodId", "required", "Timesheet Period ID is required."));
        }

        if (command.Contributor is null || string.IsNullOrWhiteSpace(command.Contributor.PartyId))
        {
            errors.Add(new("contributor", "required", "Contributor Party reference is required."));
        }

        if (tenant is null || string.IsNullOrWhiteSpace(tenant.TenantId))
        {
            errors.Add(new("tenant", "required", "Tenant reference is required."));
        }

        if (submitter is null || string.IsNullOrWhiteSpace(submitter.PartyId))
        {
            errors.Add(new("submitter", "required", "Submitter Party reference is required."));
        }

        if (submittedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new("submittedAtUtc", "utc-required", "Submission timestamp must be a UTC instant."));
        }

        if (command.Period is null)
        {
            errors.Add(new("period", "required", "Timesheet Period request is required."));
        }
        else if (command.Period.PeriodKind is not (TimesheetPeriodKind.Weekly or TimesheetPeriodKind.Monthly))
        {
            errors.Add(new("period.periodKind", "invalid", "Timesheet Period kind must be Weekly or Monthly."));
        }

        if (command.TimeEntryIds is null || command.TimeEntryIds.Count == 0)
        {
            errors.Add(new("timeEntryIds", "required", "At least one Time Entry ID is required."));
        }
        else if (Distinct(command.TimeEntryIds).Count != command.TimeEntryIds.Count)
        {
            errors.Add(new("timeEntryIds", "distinct", "Timesheet Period entries must be distinct."));
        }

        if (periodPolicy is null)
        {
            errors.Add(new("periodPolicy", "required", "Tenant period policy is required."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(periodPolicy.TenantTimeZoneId))
        {
            errors.Add(new("periodPolicy.timeZoneId", "required", "Tenant time-zone ID is required."));
            return null;
        }

        if (command.Period is null
            || command.Period.PeriodKind is not (TimesheetPeriodKind.Weekly or TimesheetPeriodKind.Monthly))
        {
            return null;
        }

        try
        {
            return TenantLocalPeriodBoundaryCalculator.Calculate(command.Period, periodPolicy);
        }
        catch (TimeZoneNotFoundException)
        {
            errors.Add(new("periodPolicy.timeZoneId", "invalid", "Tenant time-zone ID is invalid."));
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            errors.Add(new("periodPolicy.timeZoneId", "invalid", "Tenant time-zone ID is invalid."));
            return null;
        }
    }

    private static TimesheetsDomainResult Reject(string message, IReadOnlyList<TimesheetsFieldError> errors)
        => TimesheetsDomainResult.Rejection([
            new(TimesheetsRejectionCode.ValidationFailed, message, errors)
        ]);

    private static List<TimeEntryId> Distinct(IEnumerable<TimeEntryId> ids)
        => ids.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToList();

    private static bool SameMembership(
        IEnumerable<TimeEntryId> left,
        IEnumerable<TimeEntryId> right)
        => Distinct(left).SequenceEqual(Distinct(right));

    private static bool IsDuplicateDecision(
        TimesheetPeriodState? state,
        TimesheetPeriodApprovalDecisionId? decisionId,
        TimesheetPeriodApprovalState resultingState,
        IEnumerable<TimeEntryId> affectedEntryIds,
        TimesheetPeriodRejectionReason? reason,
        IEnumerable<TimesheetPeriodSelectedEntryRejectionEvidence> rejectedEntries)
        => state?.PeriodState == resultingState
            && state.TimesheetPeriodApprovalDecisionId == decisionId
            && SameMembership(state.AffectedTimeEntryIds, affectedEntryIds)
            && state.RejectionReason == reason
            && SameRejectedEntries(state.RejectedEntries, rejectedEntries);

    private static void AddSameDecisionConflict(
        TimesheetPeriodState? state,
        TimesheetPeriodApprovalDecisionId? decisionId,
        TimesheetPeriodApprovalState resultingState,
        List<TimesheetsFieldError> errors)
    {
        if (state is not null
            && state.TimesheetPeriodApprovalDecisionId == decisionId
            && state.PeriodState == resultingState)
        {
            errors.Add(new(
                "timesheetPeriodApprovalDecisionId",
                "conflict",
                "Timesheet Period approval decision ID already exists with different decision evidence."));
        }
    }

    private static bool SameRejectedEntries(
        IEnumerable<TimesheetPeriodSelectedEntryRejectionEvidence> left,
        IEnumerable<TimesheetPeriodSelectedEntryRejectionEvidence> right)
        => left
            .OrderBy(static entry => entry.TimeEntryId?.Value ?? string.Empty, StringComparer.Ordinal)
            .SequenceEqual(right.OrderBy(static entry => entry.TimeEntryId?.Value ?? string.Empty, StringComparer.Ordinal));
}
