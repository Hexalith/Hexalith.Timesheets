namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record ActivityTypeId
{
    public ActivityTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
