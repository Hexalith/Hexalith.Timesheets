namespace Hexalith.Timesheets.Contracts.References;

public sealed record WorkReference
{
    public WorkReference(string workId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workId);
        WorkId = workId;
    }

    public string WorkId { get; }
}
