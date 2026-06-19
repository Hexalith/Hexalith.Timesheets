namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryId
{
    public TimeEntryId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
