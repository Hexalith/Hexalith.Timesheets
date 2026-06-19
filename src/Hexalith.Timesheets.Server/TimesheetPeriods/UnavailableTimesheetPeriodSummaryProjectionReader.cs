using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed class UnavailableTimesheetPeriodSummaryProjectionReader : ITimesheetPeriodSummaryProjectionReader
{
    public ValueTask<TimesheetPeriodSummaryReadModel?> ReadAsync(
        TimesheetsRequestContext context,
        TimesheetPeriodId timesheetPeriodId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timesheetPeriodId);

        return ValueTask.FromResult<TimesheetPeriodSummaryReadModel?>(null);
    }
}
