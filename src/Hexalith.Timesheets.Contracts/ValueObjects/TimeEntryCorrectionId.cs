namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryCorrectionId
{
    public TimeEntryCorrectionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
