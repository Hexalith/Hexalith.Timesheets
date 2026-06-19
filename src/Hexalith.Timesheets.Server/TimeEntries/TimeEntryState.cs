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
    }
}
