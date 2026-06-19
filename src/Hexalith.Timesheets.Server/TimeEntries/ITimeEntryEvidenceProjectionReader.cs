using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface ITimeEntryEvidenceProjectionReader
{
    ValueTask<TimeEntryEvidenceReadModel?> ReadAsync(
        TimesheetsRequestContext context,
        TimeEntryId timeEntryId,
        CancellationToken cancellationToken);
}
