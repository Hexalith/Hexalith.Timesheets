using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Endpoints;

/// <summary>Reads missing or null scalar fields as invalid recording values at the policy boundary.</summary>
/// <param name="TimeEntryId">The entry to record.</param>
/// <param name="Target">The target to which time is attributed.</param>
/// <param name="Contributor">The Party who performed the work.</param>
/// <param name="ActivityTypeId">The tenant's Activity Type for the recording.</param>
/// <param name="ServiceDate">The date on which the work was performed, if supplied.</param>
/// <param name="DurationMinutes">The duration in whole minutes, if supplied.</param>
/// <param name="BillableState">The requested billability of the recording, if supplied.</param>
/// <param name="Source">The external system and request that originated the recording.</param>
internal sealed record ExternalRecordingBody(
    TimeEntryId TimeEntryId,
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    DateOnly? ServiceDate,
    int? DurationMinutes,
    BillableState? BillableState,
    ExternalContributionSource Source)
{
    /// <summary>Gets optional supporting context for the recording.</summary>
    public TimeEntryComment? Comment { get; init; }

    /// <summary>Creates the command for recording policy validation.</summary>
    internal SubmitExternalTimeEntry ToCommand()
        => new(TimeEntryId, Target, Contributor, ActivityTypeId, ServiceDate.GetValueOrDefault(),
            DurationMinutes.GetValueOrDefault(), BillableState.GetValueOrDefault(), Source) { Comment = Comment };
}
