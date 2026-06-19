using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryCorrected(
    TimeEntryId TimeEntryId,
    TimeEntryCorrectionId TimeEntryCorrectionId,
    TenantReference Tenant,
    PartyReference CorrectedBy,
    DateTimeOffset CorrectedAtUtc,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues CorrectedValues,
    TimeEntryRejectionReason RejectionReason,
    TimeEntryApprovalDecisionId RejectionDecisionId,
    TimeEntryApprovalState ApprovalState,
    TimeEntryCorrectionState CorrectionState);
