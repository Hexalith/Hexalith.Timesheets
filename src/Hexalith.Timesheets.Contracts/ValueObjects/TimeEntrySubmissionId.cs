namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntrySubmissionId
{
    public TimeEntrySubmissionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
