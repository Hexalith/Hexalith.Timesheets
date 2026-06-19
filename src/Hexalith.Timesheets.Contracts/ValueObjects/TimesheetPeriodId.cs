namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimesheetPeriodId
{
    public TimesheetPeriodId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
