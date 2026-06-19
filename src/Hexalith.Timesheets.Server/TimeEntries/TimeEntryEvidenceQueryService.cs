using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntryEvidenceQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimeEntryDisplayHydrator _displayHydrator;
    private readonly ITimeEntryEvidenceProjectionReader _projectionReader;

    public TimeEntryEvidenceQueryService(
        ITimesheetsAccessGuard accessGuard,
        ITimeEntryEvidenceProjectionReader projectionReader,
        ITimeEntryDisplayHydrator displayHydrator)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);
        ArgumentNullException.ThrowIfNull(displayHydrator);

        _accessGuard = accessGuard;
        _projectionReader = projectionReader;
        _displayHydrator = displayHydrator;
    }

    public async ValueTask<TimeEntryEvidenceQueryResult> ReadAsync(
        TimesheetsRequestContext context,
        TimeEntryId timeEntryId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeEntryId);

        TimesheetsAuthorizationDecision tenantDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return TimeEntryEvidenceQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimeEntryEvidenceReadModel? evidence = await _projectionReader
            .ReadAsync(context, timeEntryId, cancellationToken)
            .ConfigureAwait(false);

        if (evidence is null)
        {
            return TimeEntryEvidenceQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimesheetsAuthorizationDecision evidenceDecision = await _accessGuard.AuthorizeAsync(
            CreateEvidenceAuthorizationRequest(context, evidence),
            cancellationToken).ConfigureAwait(false);

        if (!evidenceDecision.IsAuthorized)
        {
            return TimeEntryEvidenceQueryResult.NotFoundOrDenied(evidenceDecision);
        }

        TimeEntryDisplayHydration hydration = await _displayHydrator
            .HydrateAsync(context, evidence, cancellationToken)
            .ConfigureAwait(false);

        return TimeEntryEvidenceQueryResult.Disclosed(evidence with
        {
            DisplayHydration = hydration
        });
    }

    private static TimesheetsAuthorizationRequest CreateEvidenceAuthorizationRequest(
        TimesheetsRequestContext context,
        TimeEntryEvidenceReadModel evidence)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.ProjectionRead)
        {
            Contributor = evidence.Contributor
        };

        if (evidence.Target.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new(evidence.Target.TargetId) };
        }

        if (evidence.Target.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new(evidence.Target.TargetId) };
        }

        return request;
    }
}
