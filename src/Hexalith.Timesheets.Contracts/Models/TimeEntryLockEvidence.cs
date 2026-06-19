using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryLockEvidence(
    TimeEntryLockState LockState,
    TimeEntryApprovalDecisionId? SourceApprovalDecisionId,
    TimeEntryApprovalScope SourceApprovalScope,
    PartyReference? LockedBy,
    DateTimeOffset? LockedAtUtc,
    string Explanation)
{
    public static TimeEntryLockEvidence Unlocked { get; } = new(
        TimeEntryLockState.Unlocked,
        null,
        TimeEntryApprovalScope.Unknown,
        null,
        null,
        "Direct edits are allowed for the current entry state.");

    public static TimeEntryLockEvidence Approved(
        TimeEntryApprovalDecisionId sourceApprovalDecisionId,
        TimeEntryApprovalScope sourceApprovalScope,
        PartyReference lockedBy,
        DateTimeOffset lockedAtUtc)
        => new(
            TimeEntryLockState.LockedFromDirectEdit,
            sourceApprovalDecisionId,
            sourceApprovalScope,
            lockedBy,
            lockedAtUtc,
            "Approved entries are locked from direct edits.");

    public static TimeEntryLockEvidence Superseded(string explanation = "Superseded entries are locked from direct edits.")
        => new(
            TimeEntryLockState.SupersededLocked,
            null,
            TimeEntryApprovalScope.Unknown,
            null,
            null,
            explanation);
}
