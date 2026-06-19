using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public interface IApprovedTimeLedgerProjectionReader
{
    ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger query,
        CancellationToken cancellationToken);
}
