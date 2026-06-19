using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface IActivityTypeDisplayHydrationProvider
{
    ValueTask<TimeEntryHydratedDisplayLabel> HydrateActivityTypeAsync(
        TimesheetsRequestContext context,
        ActivityTypeId activityTypeId,
        ActivityTypeScope activityTypeScope,
        CancellationToken cancellationToken);
}
