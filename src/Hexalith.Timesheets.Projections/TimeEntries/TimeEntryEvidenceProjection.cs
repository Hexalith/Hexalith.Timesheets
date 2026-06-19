using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.TimeEntries;

public sealed class TimeEntryEvidenceProjection
{
    public const string ProjectionName = "time-entry-evidence";

    public TimeEntryEvidenceReadModel? Project(
        string tenantId,
        TimeEntryId timeEntryId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(timeEntryId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        TimeEntryEvidenceReadModel? model = null;
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

        foreach (TimeEntryProjectionEvent projectionEvent in events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            if (projectionEvent.Payload is TimeEntryRecorded recorded
                && recorded.TimeEntryId == timeEntryId)
            {
                model = Apply(recorded, checkpoint);
            }
        }

        return model;
    }

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryRecorded recorded,
        TimesheetsProjectionCheckpoint checkpoint)
        => new(
            recorded.TimeEntryId,
            recorded.Target,
            recorded.Contributor,
            recorded.ActivityTypeId,
            recorded.ActivityTypeScope,
            recorded.ServiceDate,
            recorded.DurationMinutes,
            recorded.BillableState,
            recorded.ApprovalState,
            recorded.ContributorCategory,
            recorded.AiMetrics,
            TimeEntryCorrectionState.None,
            ToFreshnessMetadata(checkpoint))
        {
            Comment = recorded.Comment
        };

    private static ProjectionFreshnessMetadata ToFreshnessMetadata(TimesheetsProjectionCheckpoint checkpoint)
        => checkpoint.Freshness switch
        {
            ProjectionFreshness.Fresh => new(
                ProjectionFreshnessState.Fresh,
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null,
                null),
            ProjectionFreshness.Rebuilding => ProjectionFreshnessMetadata.Rebuilding(),
            ProjectionFreshness.Stale => ProjectionFreshnessMetadata.Stale(
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ProjectionFreshness.Unavailable => ProjectionFreshnessMetadata.Unavailable(),
            _ => new(ProjectionFreshnessState.Unknown, null, null, "Projection freshness is unknown.")
        };
}
