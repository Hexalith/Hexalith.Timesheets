using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface ITimeEntryDisplayHydrator
{
    ValueTask<TimeEntryDisplayHydration> HydrateAsync(
        TimesheetsRequestContext context,
        TimeEntryEvidenceReadModel evidence,
        CancellationToken cancellationToken);
}
