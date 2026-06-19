using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public sealed class UnavailableApprovedTimeLedgerProjectionReader : IApprovedTimeLedgerProjectionReader
{
    public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(null);
    }
}
