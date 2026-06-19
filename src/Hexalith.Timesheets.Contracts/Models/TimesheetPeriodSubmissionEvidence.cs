using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodSubmissionEvidence(
    TimesheetPeriodId TimesheetPeriodId,
    TenantReference Tenant,
    PartyReference Contributor,
    PartyReference Submitter,
    DateTimeOffset SubmittedAtUtc,
    TenantLocalPeriodBoundary Boundary,
    IReadOnlyList<TimeEntryId> IncludedTimeEntryIds,
    TimesheetPeriodApprovalState PeriodState);
