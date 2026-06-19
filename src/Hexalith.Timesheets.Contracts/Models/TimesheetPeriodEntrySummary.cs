using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodEntrySummary(
    TimeEntryId TimeEntryId,
    TimeEntryApprovalState ApprovalState,
    TimeEntryCorrectionState CorrectionState,
    TimeEntryLockState LockState,
    ProjectionFreshnessMetadata ProjectionFreshness);
