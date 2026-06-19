using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
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
}
