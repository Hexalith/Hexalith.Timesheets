using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkAdjustmentDisplayResponse(
    DateOnly ProposedDate,
    int DurationMinutes,
    string DurationUnit,
    ActivityTypeId ActivityTypeId,
    string ActivityTypeLabel,
    BillableState BillableState,
    string TargetContext,
    IReadOnlyList<string> EditableFields,
    IReadOnlyList<string> ReadOnlyFields)
{
    public string? Comment { get; init; }
}
