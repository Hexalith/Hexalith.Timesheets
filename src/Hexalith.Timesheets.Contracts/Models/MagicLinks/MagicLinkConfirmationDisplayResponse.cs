using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkConfirmationDisplayResponse(
    DateOnly ProposedDate,
    int DurationMinutes,
    string DurationUnit,
    ActivityTypeId ActivityTypeId,
    string ActivityTypeLabel,
    BillableState BillableState,
    string TargetContext)
{
    public string? Comment { get; init; }
}
