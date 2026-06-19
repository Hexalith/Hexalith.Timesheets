using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.OperationalReports;

public interface IWorkPlannedEffortProvider
{
    ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken);
}
