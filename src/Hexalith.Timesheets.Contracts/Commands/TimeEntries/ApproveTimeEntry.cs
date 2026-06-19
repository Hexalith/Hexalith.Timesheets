using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimeEntries;

public sealed record ApproveTimeEntry(
    TimeEntryId TimeEntryId,
    TimeEntryApprovalDecisionId TimeEntryApprovalDecisionId);
