using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimeEntries;

public sealed record RejectTimeEntry(
    TimeEntryId TimeEntryId,
    TimeEntryApprovalDecisionId TimeEntryApprovalDecisionId,
    TimeEntryRejectionReason Reason);
