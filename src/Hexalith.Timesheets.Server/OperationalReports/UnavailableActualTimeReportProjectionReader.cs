using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.OperationalReports;

public sealed class UnavailableActualTimeReportProjectionReader : IActualTimeReportProjectionReader
{
    public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
        TimesheetsRequestContext context,
        QueryProjectActualTimeReport query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult<ActualTimeReportReadModel?>(null);
    }

    public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
        TimesheetsRequestContext context,
        QueryWorkActualTimeReport query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult<ActualTimeReportReadModel?>(null);
    }
}
