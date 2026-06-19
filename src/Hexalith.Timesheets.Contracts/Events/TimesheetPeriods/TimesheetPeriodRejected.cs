using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;

public sealed record TimesheetPeriodRejected(
    TimesheetPeriodId TimesheetPeriodId,
    TenantReference Tenant,
    PartyReference Contributor,
    PartyReference Approver,
    DateTimeOffset DecidedAtUtc,
    TimesheetPeriodApprovalDecisionId TimesheetPeriodApprovalDecisionId,
    TimesheetPeriodApprovalState PeriodState,
    ApprovalAuthoritySourceAttribution AuthoritySource,
    IReadOnlyList<TimeEntryId> AffectedTimeEntryIds,
    TimesheetPeriodRejectionReason Reason,
    IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> RejectedEntries);
