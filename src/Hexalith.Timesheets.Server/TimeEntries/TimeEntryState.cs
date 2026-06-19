using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntryState
{
    public bool IsRecorded { get; private set; }

    public TimeEntryId? TimeEntryId { get; private set; }

    public TimeEntryTargetReference? Target { get; private set; }

    public PartyReference? Contributor { get; private set; }

    public ActivityTypeId? ActivityTypeId { get; private set; }

    public ActivityTypeScope ActivityTypeScope { get; private set; }

    public DateOnly ServiceDate { get; private set; }

    public int DurationMinutes { get; private set; }

    public BillableState BillableState { get; private set; }

    public TimeEntryApprovalState ApprovalState { get; private set; }

    public ContributorCategory ContributorCategory { get; private set; }

    public AiEffortMetrics? AiMetrics { get; private set; }

    public TimeEntryComment? Comment { get; private set; }

    public TimeEntrySubmissionId? TimeEntrySubmissionId { get; private set; }

    public PartyReference? Submitter { get; private set; }

    public TenantReference? SubmissionTenant { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public TimeEntrySubmissionScope SubmissionScope { get; private set; }

    public TimeEntryApprovalDecisionId? TimeEntryApprovalDecisionId { get; private set; }

    public PartyReference? Approver { get; private set; }

    public TenantReference? ApprovalTenant { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public ApprovalAuthoritySourceAttribution? ApprovalAuthoritySource { get; private set; }

    public TimeEntryApprovalScope ApprovalScope { get; private set; }

    public TimeEntryRejectionReason? RejectionReason { get; private set; }

    public TimeEntryCorrectionState CorrectionState { get; private set; } = TimeEntryCorrectionState.None;

    public TimeEntryCorrectionId? TimeEntryCorrectionId { get; private set; }

    public PartyReference? CorrectedBy { get; private set; }

    public TenantReference? CorrectionTenant { get; private set; }

    public DateTimeOffset? CorrectedAtUtc { get; private set; }

    public TimeEntryApprovalDecisionId? RejectionDecisionId { get; private set; }

    public TimeEntryCorrectionValues? PreviousValues { get; private set; }

    public TimeEntryCorrectionValues? CorrectedValues { get; private set; }

    public TimeEntryLockState LockState => CorrectionState == TimeEntryCorrectionState.Superseded
        ? TimeEntryLockState.SupersededLocked
        : ApprovalState == TimeEntryApprovalState.Approved
            ? TimeEntryLockState.LockedFromDirectEdit
            : TimeEntryLockState.Unlocked;

    public bool IsLockedFromDirectEdit => LockState is TimeEntryLockState.LockedFromDirectEdit
        or TimeEntryLockState.SupersededLocked;

    public void Apply(TimeEntryRecorded recorded)
    {
        ArgumentNullException.ThrowIfNull(recorded);

        IsRecorded = true;
        TimeEntryId = recorded.TimeEntryId;
        Target = recorded.Target;
        Contributor = recorded.Contributor;
        ActivityTypeId = recorded.ActivityTypeId;
        ActivityTypeScope = recorded.ActivityTypeScope;
        ServiceDate = recorded.ServiceDate;
        DurationMinutes = recorded.DurationMinutes;
        BillableState = recorded.BillableState;
        ApprovalState = recorded.ApprovalState;
        ContributorCategory = recorded.ContributorCategory;
        AiMetrics = recorded.AiMetrics;
        Comment = recorded.Comment;
    }

    public void Apply(TimeEntrySubmitted submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        ApprovalState = submitted.ApprovalState;
        TimeEntrySubmissionId = submitted.TimeEntrySubmissionId;
        Submitter = submitted.Submitter;
        SubmissionTenant = submitted.Tenant;
        SubmittedAtUtc = submitted.SubmittedAtUtc;
        SubmissionScope = submitted.SubmissionScope;
    }

    public void Apply(TimeEntryApproved approved)
    {
        ArgumentNullException.ThrowIfNull(approved);

        ApprovalState = approved.ApprovalState;
        TimeEntryApprovalDecisionId = approved.TimeEntryApprovalDecisionId;
        Approver = approved.Approver;
        ApprovalTenant = approved.Tenant;
        DecidedAtUtc = approved.DecidedAtUtc;
        ApprovalAuthoritySource = approved.AuthoritySource;
        ApprovalScope = approved.ApprovalScope;
        RejectionReason = null;
    }

    public void Apply(TimeEntryRejected rejected)
    {
        ArgumentNullException.ThrowIfNull(rejected);

        ApprovalState = rejected.ApprovalState;
        TimeEntryApprovalDecisionId = rejected.TimeEntryApprovalDecisionId;
        Approver = rejected.Approver;
        ApprovalTenant = rejected.Tenant;
        DecidedAtUtc = rejected.DecidedAtUtc;
        ApprovalAuthoritySource = rejected.AuthoritySource;
        ApprovalScope = rejected.ApprovalScope;
        RejectionReason = rejected.Reason;
        RejectionDecisionId = rejected.TimeEntryApprovalDecisionId;
    }

    public void Apply(TimeEntryCorrected corrected)
    {
        ArgumentNullException.ThrowIfNull(corrected);

        Target = corrected.CorrectedValues.Target;
        Contributor = corrected.CorrectedValues.Contributor;
        ActivityTypeId = corrected.CorrectedValues.ActivityTypeId;
        ServiceDate = corrected.CorrectedValues.ServiceDate;
        DurationMinutes = corrected.CorrectedValues.DurationMinutes;
        BillableState = corrected.CorrectedValues.BillableState;
        ContributorCategory = corrected.CorrectedValues.ContributorCategory;
        AiMetrics = corrected.CorrectedValues.AiMetrics;
        Comment = corrected.CorrectedValues.Comment;
        ApprovalState = corrected.ApprovalState;
        CorrectionState = corrected.CorrectionState;
        TimeEntryCorrectionId = corrected.TimeEntryCorrectionId;
        CorrectedBy = corrected.CorrectedBy;
        CorrectionTenant = corrected.Tenant;
        CorrectedAtUtc = corrected.CorrectedAtUtc;
        RejectionDecisionId = corrected.RejectionDecisionId;
        PreviousValues = corrected.PreviousValues;
        CorrectedValues = corrected.CorrectedValues;
    }
}
