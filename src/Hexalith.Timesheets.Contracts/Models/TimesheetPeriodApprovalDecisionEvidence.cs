using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodApprovalDecisionEvidence(
    TimesheetPeriodId TimesheetPeriodId,
    TimesheetPeriodApprovalDecisionId TimesheetPeriodApprovalDecisionId,
    PartyReference Approver,
    TenantReference Tenant,
    DateTimeOffset DecidedAtUtc,
    TimesheetPeriodApprovalState PeriodState,
    ApprovalAuthoritySourceAttribution AuthoritySource,
    IReadOnlyList<TimeEntryId> AffectedTimeEntryIds)
{
    public TimesheetPeriodRejectionReason? PeriodRejectionReason { get; init; }

    public IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> RejectedEntries { get; init; } = [];
}
