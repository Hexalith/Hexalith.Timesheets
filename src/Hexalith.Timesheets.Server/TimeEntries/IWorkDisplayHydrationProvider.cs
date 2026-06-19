using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface IWorkDisplayHydrationProvider
{
    ValueTask<TimeEntryHydratedDisplayLabel> HydrateWorkAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken);
}
