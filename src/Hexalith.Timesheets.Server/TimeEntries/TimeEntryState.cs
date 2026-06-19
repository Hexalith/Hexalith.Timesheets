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
}
