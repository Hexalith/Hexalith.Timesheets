using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.OperationalReports;

public sealed class UnavailableWorkPlannedEffortProvider : IWorkPlannedEffortProvider
{
    public ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        return ValueTask.FromResult(WorkPlannedEffortReadModel.Unavailable());
    }
}
