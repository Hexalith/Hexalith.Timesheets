using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryCorrectionEvidence(
    TimeEntryId TimeEntryId,
    TimeEntryCorrectionId TimeEntryCorrectionId,
    PartyReference CorrectedBy,
    TenantReference Tenant,
    DateTimeOffset CorrectedAtUtc,
    TimeEntryRejectionReason RejectionReason,
    TimeEntryApprovalDecisionId RejectionDecisionId,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues CorrectedValues,
    TimeEntryCorrectionState CorrectionState);
