using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class UnavailableTimeEntryEvidenceProjectionReader : ITimeEntryEvidenceProjectionReader
{
    public ValueTask<TimeEntryEvidenceReadModel?> ReadAsync(
        TimesheetsRequestContext context,
        TimeEntryId timeEntryId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeEntryId);

        return ValueTask.FromResult<TimeEntryEvidenceReadModel?>(null);
    }
}
