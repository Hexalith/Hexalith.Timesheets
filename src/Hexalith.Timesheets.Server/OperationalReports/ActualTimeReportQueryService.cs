using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.OperationalReports;

public sealed class ActualTimeReportQueryService
{
    private readonly IActivityTypeDisplayHydrationProvider _activityHydrationProvider;
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly IPartyDisplayHydrationProvider _partyHydrationProvider;
    private readonly IActualTimeReportProjectionReader _projectionReader;
    private readonly IProjectDisplayHydrationProvider _projectHydrationProvider;
    private readonly IWorkDisplayHydrationProvider _workHydrationProvider;
    private readonly IWorkPlannedEffortProvider _workPlannedEffortProvider;

    public ActualTimeReportQueryService(
        ITimesheetsAccessGuard accessGuard,
        IActualTimeReportProjectionReader projectionReader,
        IPartyDisplayHydrationProvider partyHydrationProvider,
        IProjectDisplayHydrationProvider projectHydrationProvider,
        IWorkDisplayHydrationProvider workHydrationProvider,
        IActivityTypeDisplayHydrationProvider activityHydrationProvider,
        IWorkPlannedEffortProvider workPlannedEffortProvider)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);
        ArgumentNullException.ThrowIfNull(partyHydrationProvider);
        ArgumentNullException.ThrowIfNull(projectHydrationProvider);
        ArgumentNullException.ThrowIfNull(workHydrationProvider);
        ArgumentNullException.ThrowIfNull(activityHydrationProvider);
        ArgumentNullException.ThrowIfNull(workPlannedEffortProvider);

        _accessGuard = accessGuard;
        _projectionReader = projectionReader;
        _partyHydrationProvider = partyHydrationProvider;
        _projectHydrationProvider = projectHydrationProvider;
        _workHydrationProvider = workHydrationProvider;
        _activityHydrationProvider = activityHydrationProvider;
        _workPlannedEffortProvider = workPlannedEffortProvider;
    }

    public async ValueTask<ActualTimeReportQueryResult> QueryProjectAsync(
        TimesheetsRequestContext context,
        QueryProjectActualTimeReport query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        TimesheetsAuthorizationDecision tenantDecision = await AuthorizeProjectionReadAsync(
            context,
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return ActualTimeReportQueryResult.NotFoundOrDenied(tenantDecision);
        }

        ActualTimeReportReadModel? page = await _projectionReader
            .QueryProjectAsync(context, query, cancellationToken)
            .ConfigureAwait(false);

        return page is null
            ? ActualTimeReportQueryResult.NotFoundOrDenied(tenantDecision)
            : await DiscloseRowsAsync(context, page, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ActualTimeReportQueryResult> QueryWorkAsync(
        TimesheetsRequestContext context,
        QueryWorkActualTimeReport query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        TimesheetsAuthorizationDecision tenantDecision = await AuthorizeProjectionReadAsync(
            context,
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return ActualTimeReportQueryResult.NotFoundOrDenied(tenantDecision);
        }

        ActualTimeReportReadModel? page = await _projectionReader
            .QueryWorkAsync(context, query, cancellationToken)
            .ConfigureAwait(false);

        return page is null
            ? ActualTimeReportQueryResult.NotFoundOrDenied(tenantDecision)
            : await DiscloseRowsAsync(context, page, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TimesheetsAuthorizationDecision> AuthorizeProjectionReadAsync(
        TimesheetsRequestContext context,
        CancellationToken cancellationToken)
        => await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

    private async ValueTask<ActualTimeReportQueryResult> DiscloseRowsAsync(
        TimesheetsRequestContext context,
        ActualTimeReportReadModel page,
        CancellationToken cancellationToken)
    {
        // Report rows are grouped by target/period/contributor/activity, so the same Project, Work,
        // Party, Activity Type, and Works planned-effort reference repeats across many rows. Memoize
        // each lookup per reference within this disclosure pass so authorized rows reuse one hydration
        // and one Works planned-effort call instead of issuing redundant cross-module requests. Denied
        // rows still short-circuit before any lookup, so nothing is hydrated or queried for them.
        Dictionary<string, TimeEntryHydratedDisplayLabel> targetLabels = new(StringComparer.Ordinal);
        Dictionary<string, TimeEntryHydratedDisplayLabel> contributorLabels = new(StringComparer.Ordinal);
        Dictionary<string, TimeEntryHydratedDisplayLabel> activityLabels = new(StringComparer.Ordinal);
        Dictionary<string, WorkPlannedEffortReadModel> plannedEffortByWork = new(StringComparer.Ordinal);

        List<ActualTimeReportRowReadModel> disclosedRows = [];
        foreach (ActualTimeReportRowReadModel row in page.Items)
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

                return ActualTimeReportQueryResult.NotFoundOrDenied(rowDecision);
            }

            TimeEntryDisplayHydration hydration = await HydrateAsync(
                context,
                row,
                targetLabels,
                contributorLabels,
                activityLabels,
                cancellationToken).ConfigureAwait(false);

            disclosedRows.Add(row with
            {
                DisplayHydration = hydration,
                WorkPlannedEffort = row.Target.TargetKind == TimeEntryTargetKind.Work
                    ? await GetPlannedEffortAsync(context, row.Target.TargetId, plannedEffortByWork, cancellationToken)
                        .ConfigureAwait(false)
                    : row.WorkPlannedEffort
            });
        }

        return ActualTimeReportQueryResult.Disclosed(page with
        {
            Items = disclosedRows
        });
    }

    private async ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
        TimesheetsRequestContext context,
        string workId,
        Dictionary<string, WorkPlannedEffortReadModel> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(workId, out WorkPlannedEffortReadModel? cached))
        {
            return cached;
        }

        WorkPlannedEffortReadModel plannedEffort = await _workPlannedEffortProvider
            .GetPlannedEffortAsync(context, new WorkReference(workId), cancellationToken)
            .ConfigureAwait(false);
        cache[workId] = plannedEffort;
        return plannedEffort;
    }

    private async ValueTask<TimeEntryDisplayHydration> HydrateAsync(
        TimesheetsRequestContext context,
        ActualTimeReportRowReadModel row,
        Dictionary<string, TimeEntryHydratedDisplayLabel> targetLabels,
        Dictionary<string, TimeEntryHydratedDisplayLabel> contributorLabels,
        Dictionary<string, TimeEntryHydratedDisplayLabel> activityLabels,
        CancellationToken cancellationToken)
    {
        string targetKey = $"{row.Target.TargetKind}|{row.Target.TargetId}";
        if (!targetLabels.TryGetValue(targetKey, out TimeEntryHydratedDisplayLabel? target))
        {
            target = row.Target.TargetKind == TimeEntryTargetKind.Project
                ? await _projectHydrationProvider
                    .HydrateProjectAsync(context, new ProjectReference(row.Target.TargetId), cancellationToken)
                    .ConfigureAwait(false)
                : await _workHydrationProvider
                    .HydrateWorkAsync(context, new WorkReference(row.Target.TargetId), cancellationToken)
                    .ConfigureAwait(false);
            targetLabels[targetKey] = target;
        }

        if (!contributorLabels.TryGetValue(row.Contributor.PartyId, out TimeEntryHydratedDisplayLabel? contributor))
        {
            contributor = await _partyHydrationProvider
                .HydrateContributorAsync(context, row.Contributor, cancellationToken)
                .ConfigureAwait(false);
            contributorLabels[row.Contributor.PartyId] = contributor;
        }

        string activityKey = $"{row.ActivityTypeId.Value}|{row.ActivityTypeScope}";
        if (!activityLabels.TryGetValue(activityKey, out TimeEntryHydratedDisplayLabel? activityType))
        {
            activityType = await _activityHydrationProvider
                .HydrateActivityTypeAsync(context, row.ActivityTypeId, row.ActivityTypeScope, cancellationToken)
                .ConfigureAwait(false);
            activityLabels[activityKey] = activityType;
        }

        return new(contributor, target, activityType);
    }

    private static bool CanFilterRow(TimesheetsAuthorizationDecision decision)
        => decision.DenialCategory == TimesheetsDenialCategory.InsufficientRole;

    private static TimesheetsAuthorizationRequest CreateRowAuthorizationRequest(
        TimesheetsRequestContext context,
        ActualTimeReportRowReadModel row)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.ProjectionRead)
        {
            Contributor = row.Contributor
        };

        return row.Target.TargetKind switch
        {
            TimeEntryTargetKind.Project => request with { Project = new ProjectReference(row.Target.TargetId) },
            TimeEntryTargetKind.Work => request with { Work = new WorkReference(row.Target.TargetId) },
            _ => request
        };
    }
}
