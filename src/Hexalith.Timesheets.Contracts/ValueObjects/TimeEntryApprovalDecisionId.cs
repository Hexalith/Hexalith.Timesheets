namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryApprovalDecisionId
{
    public TimeEntryApprovalDecisionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
