using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;

public sealed record TimesheetPeriodSubmitted(
    TimesheetPeriodId TimesheetPeriodId,
    TenantReference Tenant,
    PartyReference Contributor,
    PartyReference Submitter,
    DateTimeOffset SubmittedAtUtc,
    TimesheetPeriodKind PeriodKind,
    string PeriodKey,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TenantTimeZoneId,
    IReadOnlyList<TimeEntryId> IncludedTimeEntryIds,
    TimesheetPeriodApprovalState PeriodState);
