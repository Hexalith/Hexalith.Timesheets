using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryTargetReference
{
    public TimeEntryTargetReference(TimeEntryTargetKind targetKind, string targetId)
    {
        if (targetKind is not (TimeEntryTargetKind.Project or TimeEntryTargetKind.Work))
        {
            throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Time entry target must be Project or Work.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        TargetKind = targetKind;
        TargetId = targetId;
    }

    public TimeEntryTargetKind TargetKind { get; }

    public string TargetId { get; }

    public static TimeEntryTargetReference ForProject(ProjectReference project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new(TimeEntryTargetKind.Project, project.ProjectId);
    }

    public static TimeEntryTargetReference ForWork(WorkReference work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return new(TimeEntryTargetKind.Work, work.WorkId);
    }
}
