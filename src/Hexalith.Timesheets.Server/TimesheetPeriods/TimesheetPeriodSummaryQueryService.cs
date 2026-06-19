using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed class TimesheetPeriodSummaryQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimesheetPeriodSummaryProjectionReader _projectionReader;

    public TimesheetPeriodSummaryQueryService(
        ITimesheetsAccessGuard accessGuard,
        ITimesheetPeriodSummaryProjectionReader projectionReader)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);

        _accessGuard = accessGuard;
        _projectionReader = projectionReader;
    }

    public async ValueTask<TimesheetPeriodSummaryQueryResult> ReadAsync(
        TimesheetsRequestContext context,
        TimesheetPeriodId timesheetPeriodId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timesheetPeriodId);

        TimesheetsAuthorizationDecision tenantDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return TimesheetPeriodSummaryQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimesheetPeriodSummaryReadModel? summary = await _projectionReader
            .ReadAsync(context, timesheetPeriodId, cancellationToken)
            .ConfigureAwait(false);

        if (summary is null)
        {
            return TimesheetPeriodSummaryQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimesheetsAuthorizationDecision summaryDecision = await _accessGuard.AuthorizeAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.ProjectionRead)
            {
                Contributor = summary.Contributor
            },
            cancellationToken).ConfigureAwait(false);

        return summaryDecision.IsAuthorized
            ? TimesheetPeriodSummaryQueryResult.Disclosed(summary)
            : TimesheetPeriodSummaryQueryResult.NotFoundOrDenied(summaryDecision);
    }
}
