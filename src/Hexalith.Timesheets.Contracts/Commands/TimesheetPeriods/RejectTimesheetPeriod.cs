using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;

public sealed record RejectTimesheetPeriod(
    TimesheetPeriodId TimesheetPeriodId,
    TimesheetPeriodApprovalDecisionId TimesheetPeriodApprovalDecisionId,
    IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> RejectedEntries,
    TimesheetPeriodRejectionReason Reason);
