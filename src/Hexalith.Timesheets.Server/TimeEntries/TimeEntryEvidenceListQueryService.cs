using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntryEvidenceListQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimeEntryDisplayHydrator _displayHydrator;
    private readonly ITimeEntryEvidenceListProjectionReader _projectionReader;

    public TimeEntryEvidenceListQueryService(
        ITimesheetsAccessGuard accessGuard,
        ITimeEntryEvidenceListProjectionReader projectionReader,
        ITimeEntryDisplayHydrator displayHydrator)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);
        ArgumentNullException.ThrowIfNull(displayHydrator);

        _accessGuard = accessGuard;
        _projectionReader = projectionReader;
        _displayHydrator = displayHydrator;
    }

    public async ValueTask<TimeEntryEvidenceListQueryResult> QueryAsync(
        TimesheetsRequestContext context,
        QueryTimeEntries query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        TimesheetsAuthorizationDecision tenantDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return TimeEntryEvidenceListQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimeEntryQueryReadModel? page = await _projectionReader
            .QueryAsync(context, query, cancellationToken)
            .ConfigureAwait(false);

        if (page is null)
        {
            return TimeEntryEvidenceListQueryResult.NotFoundOrDenied(tenantDecision);
        }

        List<TimeEntryQueryRowReadModel> disclosedRows = [];
        foreach (TimeEntryQueryRowReadModel row in page.Items)
        {
            TimesheetsAuthorizationDecision rowDecision = await _accessGuard.AuthorizeAsync(
                CreateRowAuthorizationRequest(context, row),
                cancellationToken).ConfigureAwait(false);

            if (!rowDecision.IsAuthorized)
            {
                if (CanFilterRow(rowDecision))
                {
                    continue;
                }

                return TimeEntryEvidenceListQueryResult.NotFoundOrDenied(rowDecision);
            }

            TimeEntryDisplayHydration hydration = await _displayHydrator
                .HydrateAsync(context, ToEvidence(row), cancellationToken)
                .ConfigureAwait(false);

            disclosedRows.Add(row with
            {
                DisplayHydration = hydration
            });
        }

        return TimeEntryEvidenceListQueryResult.Disclosed(page with
        {
            Items = disclosedRows
        });
    }

    private static bool CanFilterRow(TimesheetsAuthorizationDecision decision)
        => decision.DenialCategory == TimesheetsDenialCategory.InsufficientRole;

    private static TimesheetsAuthorizationRequest CreateRowAuthorizationRequest(
        TimesheetsRequestContext context,
        TimeEntryQueryRowReadModel row)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.ProjectionRead)
        {
            Contributor = row.Contributor
        };

        if (row.Target.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(row.Target.TargetId) };
        }

        if (row.Target.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(row.Target.TargetId) };
        }

        return request;
    }

    private static TimeEntryEvidenceReadModel ToEvidence(TimeEntryQueryRowReadModel row)
        => new(
            row.TimeEntryId,
            row.Target,
            row.Contributor,
            row.ActivityTypeId,
            ActivityTypeScope.Unknown,
            row.ServiceDate,
            row.DurationMinutes,
            row.BillableState,
            row.ApprovalState,
            row.ContributorCategory,
            null,
            row.CorrectionState,
            row.ProjectionFreshness)
        {
            DisplayHydration = row.DisplayHydration
        };
}
