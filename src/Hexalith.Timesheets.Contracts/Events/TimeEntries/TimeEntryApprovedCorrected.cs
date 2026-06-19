using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryApprovedCorrected(
    TimeEntryId TimeEntryId,
    TimeEntryCorrectionId TimeEntryCorrectionId,
    TenantReference Tenant,
    PartyReference CorrectedBy,
    DateTimeOffset CorrectedAtUtc,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues CorrectedValues,
    TimeEntryCorrectionReason Reason,
    TimeEntryApprovalDecisionId SourceApprovalDecisionId,
    TimeEntryApprovalScope SourceApprovalScope,
    TimeEntryApprovalState ApprovalState,
    TimeEntryCorrectionState CorrectionState);
