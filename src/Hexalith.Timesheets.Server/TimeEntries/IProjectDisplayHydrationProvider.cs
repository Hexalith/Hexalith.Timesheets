using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface IProjectDisplayHydrationProvider
{
    ValueTask<TimeEntryHydratedDisplayLabel> HydrateProjectAsync(
        TimesheetsRequestContext context,
        ProjectReference project,
        CancellationToken cancellationToken);
}
