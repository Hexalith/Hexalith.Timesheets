using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.MagicLinks;

/// <summary>
/// Capability-scoped adjustment request. The request carries only editable proposed-entry values; tenant,
/// contributor, target, approval state, token, authority, and audit metadata are server-derived.
/// </summary>
public sealed record AdjustTimeThroughMagicLink(
    DateOnly ServiceDate,
    int DurationMinutes,
    ActivityTypeId ActivityTypeId,
    BillableState BillableState)
{
    public TimeEntryComment? Comment { get; init; }
}
