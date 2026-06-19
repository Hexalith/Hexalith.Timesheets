using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public static class TimesheetPeriod
{
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
}
