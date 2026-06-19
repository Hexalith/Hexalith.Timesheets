namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryRejectionReason
{
    public const int MaxLength = 1000;

    public TimeEntryRejectionReason(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength);
        Value = value;
    }

    public string Value { get; }
}
