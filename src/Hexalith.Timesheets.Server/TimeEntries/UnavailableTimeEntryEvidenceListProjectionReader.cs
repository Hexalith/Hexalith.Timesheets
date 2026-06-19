using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class UnavailableTimeEntryEvidenceListProjectionReader : ITimeEntryEvidenceListProjectionReader
{
    public ValueTask<TimeEntryQueryReadModel?> QueryAsync(
        TimesheetsRequestContext context,
        QueryTimeEntries query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult<TimeEntryQueryReadModel?>(null);
    }
}
