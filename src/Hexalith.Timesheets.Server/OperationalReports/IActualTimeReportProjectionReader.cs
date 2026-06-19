using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.OperationalReports;

public interface IActualTimeReportProjectionReader
{
    ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
        TimesheetsRequestContext context,
        QueryProjectActualTimeReport query,
        CancellationToken cancellationToken);

    ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
        TimesheetsRequestContext context,
        QueryWorkActualTimeReport query,
        CancellationToken cancellationToken);
}
