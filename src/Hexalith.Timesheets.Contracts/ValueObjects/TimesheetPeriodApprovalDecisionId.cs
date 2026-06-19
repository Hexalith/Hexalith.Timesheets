namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimesheetPeriodApprovalDecisionId
{
    public TimesheetPeriodApprovalDecisionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
