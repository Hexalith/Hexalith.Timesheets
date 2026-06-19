using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed class TimesheetPeriodState
{
    public bool IsSubmitted { get; private set; }

    public TimesheetPeriodId? TimesheetPeriodId { get; private set; }

    public TenantReference? Tenant { get; private set; }

    public PartyReference? Contributor { get; private set; }

    public PartyReference? Submitter { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public TenantLocalPeriodBoundary? Boundary { get; private set; }

    public IReadOnlyList<TimeEntryId> IncludedTimeEntryIds { get; private set; } = [];

    public TimesheetPeriodApprovalState PeriodState { get; private set; }

    public TimesheetPeriodApprovalDecisionId? TimesheetPeriodApprovalDecisionId { get; private set; }

    public PartyReference? Approver { get; private set; }

    public TenantReference? DecisionTenant { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public ApprovalAuthoritySourceAttribution? ApprovalAuthoritySource { get; private set; }

    public TimesheetPeriodRejectionReason? RejectionReason { get; private set; }

    public IReadOnlyList<TimeEntryId> AffectedTimeEntryIds { get; private set; } = [];

    public IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> RejectedEntries { get; private set; } = [];

    public void Apply(TimesheetPeriodSubmitted submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        IsSubmitted = true;
        TimesheetPeriodId = submitted.TimesheetPeriodId;
        Tenant = submitted.Tenant;
        Contributor = submitted.Contributor;
        Submitter = submitted.Submitter;
        SubmittedAtUtc = submitted.SubmittedAtUtc;
        Boundary = new(
            submitted.PeriodKind,
            submitted.PeriodKey,
            submitted.LocalStartDate,
            submitted.LocalEndDate,
            submitted.TenantTimeZoneId);
        IncludedTimeEntryIds = [.. submitted.IncludedTimeEntryIds];
        PeriodState = submitted.PeriodState;
    }

    public void Apply(TimesheetPeriodApproved approved)
    {
        ArgumentNullException.ThrowIfNull(approved);

        PeriodState = approved.PeriodState;
        TimesheetPeriodApprovalDecisionId = approved.TimesheetPeriodApprovalDecisionId;
        DecisionTenant = approved.Tenant;
        Approver = approved.Approver;
        DecidedAtUtc = approved.DecidedAtUtc;
        ApprovalAuthoritySource = approved.AuthoritySource;
        AffectedTimeEntryIds = [.. approved.IncludedTimeEntryIds];
        RejectionReason = null;
        RejectedEntries = [];
    }

    public void Apply(TimesheetPeriodRejected rejected)
    {
        ArgumentNullException.ThrowIfNull(rejected);

        PeriodState = rejected.PeriodState;
        TimesheetPeriodApprovalDecisionId = rejected.TimesheetPeriodApprovalDecisionId;
        DecisionTenant = rejected.Tenant;
        Approver = rejected.Approver;
        DecidedAtUtc = rejected.DecidedAtUtc;
        ApprovalAuthoritySource = rejected.AuthoritySource;
        AffectedTimeEntryIds = [.. rejected.AffectedTimeEntryIds];
        RejectionReason = rejected.Reason;
        RejectedEntries = [.. rejected.RejectedEntries];
    }
}
