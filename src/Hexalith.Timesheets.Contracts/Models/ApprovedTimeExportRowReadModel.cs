using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeExportRowReadModel(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    TimeEntryTargetReference Target,
    DateOnly ServiceDate,
    int DurationMinutes,
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope ActivityTypeScope,
    BillableState BillableState,
    TimeEntryApprovalDecisionEvidence ApprovalDecision,
    ApprovedTimeLedgerRowState RowState)
{
    public TimeEntryApprovedCorrectionEvidence? ApprovedCorrection { get; init; }

    public TimeEntryCorrectionEvidence? Correction { get; init; }

    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage { get; init; } = [];

    public AiEffortMetrics? AiMetrics { get; init; }

    public TimesheetsCommentPolicyDecision CommentExportState { get; init; } =
        TimesheetsCommentPolicyDecision.Unknown;

    public TimeEntryComment? Comment { get; init; }
}
