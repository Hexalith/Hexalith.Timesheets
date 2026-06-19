using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimeEntries;

namespace Hexalith.Timesheets.Projections.TimesheetPeriods;

public sealed class TimesheetPeriodSummaryProjection
{
    public const string ProjectionName = "timesheet-period-summary";

    private readonly TimeEntryEvidenceProjection _entryProjection = new();

    public TimesheetPeriodSummaryReadModel? Project(
        string tenantId,
        TimesheetPeriodId timesheetPeriodId,
        IEnumerable<TimesheetPeriodProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(timesheetPeriodId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        List<TimesheetPeriodProjectionEvent> ordered = Deduplicate(events);
        TimesheetPeriodSubmitted? submitted = ordered
            .Select(static item => item.Payload)
            .OfType<TimesheetPeriodSubmitted>()
            .LastOrDefault(item => item.TimesheetPeriodId == timesheetPeriodId);

        if (submitted is null)
        {
            return null;
        }

        List<TimeEntryProjectionEvent> entryEvents = ordered
            .Select(static item => new TimeEntryProjectionEvent(
                item.MessageId,
                item.SequenceNumber,
                item.Payload))
            .ToList();
        List<TimesheetPeriodEntrySummary> entrySummaries = [];
        List<TimeEntryId> incomplete = [];

        foreach (TimeEntryId entryId in submitted.IncludedTimeEntryIds)
        {
            TimeEntryEvidenceReadModel? evidence = _entryProjection.Project(
                tenantId,
                entryId,
                entryEvents,
                checkpoint);

            if (evidence is null)
            {
                incomplete.Add(entryId);
                continue;
            }

            entrySummaries.Add(new(
                entryId,
                evidence.ApprovalState,
                evidence.CorrectionState,
                evidence.LockEvidence.LockState,
                evidence.ProjectionFreshness));
        }

        ProjectionFreshnessMetadata freshness = incomplete.Count == 0
            ? ToFreshnessMetadata(checkpoint)
            : ProjectionFreshnessMetadata.Rebuilding("Projection is rebuilding.");

        return new(
            submitted.TimesheetPeriodId,
            submitted.Tenant,
            submitted.Contributor,
            submitted.Submitter,
            submitted.SubmittedAtUtc,
            submitted.PeriodKind,
            submitted.PeriodKey,
            submitted.LocalStartDate,
            submitted.LocalEndDate,
            submitted.TenantTimeZoneId,
            submitted.IncludedTimeEntryIds,
            submitted.PeriodState,
            freshness)
        {
            EntrySummaries = entrySummaries,
            IncompleteEntryEvidenceIds = incomplete
        };
    }

    private static List<TimesheetPeriodProjectionEvent> Deduplicate(
        IEnumerable<TimesheetPeriodProjectionEvent> events)
    {
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);
        List<TimesheetPeriodProjectionEvent> ordered = [];

        foreach (TimesheetPeriodProjectionEvent projectionEvent in events
            .OrderBy(static item => item.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            ordered.Add(projectionEvent);
        }

        return ordered;
    }

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
