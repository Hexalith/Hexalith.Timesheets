namespace Hexalith.Timesheets.Contracts.References;

public sealed record ProjectReference
{
    public ProjectReference(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ProjectId = projectId;
    }

    public string ProjectId { get; }
}
