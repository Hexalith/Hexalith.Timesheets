using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryApprovedCorrectionEvidence(
    TimeEntryId TimeEntryId,
    TimeEntryCorrectionId TimeEntryCorrectionId,
    PartyReference CorrectedBy,
    TenantReference Tenant,
    DateTimeOffset CorrectedAtUtc,
    TimeEntryCorrectionReason Reason,
    TimeEntryApprovalDecisionId SourceApprovalDecisionId,
    TimeEntryApprovalScope SourceApprovalScope,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues CorrectedValues,
    TimeEntryApprovalState ApprovalState,
    TimeEntryCorrectionState CorrectionState);
