using System.Text.Json.Serialization;

using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryCorrectionValues(
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    DateOnly ServiceDate,
    int DurationMinutes,
    BillableState BillableState,
    ContributorCategory ContributorCategory,
    AiEffortMetrics? AiMetrics)
{
    /// <summary>
    /// Gets the server-resolved Activity Type scope captured with this snapshot, or <see langword="null"/>
    /// when replaying a legacy correction that predates scope capture.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActivityTypeScope? ActivityTypeScope { get; init; }

    public TimeEntryComment? Comment { get; init; }
}
