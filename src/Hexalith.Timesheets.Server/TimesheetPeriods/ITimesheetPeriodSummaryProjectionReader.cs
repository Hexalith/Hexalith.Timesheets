using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public interface ITimesheetPeriodSummaryProjectionReader
{
    ValueTask<TimesheetPeriodSummaryReadModel?> ReadAsync(
        TimesheetsRequestContext context,
        TimesheetPeriodId timesheetPeriodId,
        CancellationToken cancellationToken);
}
