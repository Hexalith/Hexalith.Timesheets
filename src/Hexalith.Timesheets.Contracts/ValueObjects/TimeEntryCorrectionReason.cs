namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryCorrectionReason
{
    public const int MaxLength = 1024;

    public TimeEntryCorrectionReason(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength);
        Value = value;
    }

    public string Value { get; }
}
