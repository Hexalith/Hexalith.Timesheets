using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record ExternalContributionPolicyOptions
{
    public static ExternalContributionPolicyOptions Default { get; } = new();

    public TimeEntryApprovalState InitialApprovalState { get; init; } = TimeEntryApprovalState.Draft;
}
