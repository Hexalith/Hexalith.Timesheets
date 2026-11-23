using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public interface IApprovedTimeLedgerProjectionReader
{
    /// <summary>Reads one authorized tenant's ledger page. NextCursor is a continuation within the query.</summary>
    /// <remarks>For export, CanUseForExport certifies the page even when it is empty. Set it to false
    /// if the reader withholds the page. A continuation must advance within the result and use the
    /// same token for the same position. A null continuation indicates the end of the result.
    /// Set ExportSnapshotId only when it identifies immutable membership and values across the
    /// entire filtered scope, including every participating entry stream. Every page must certify
    /// the same version. A per-owner sequence or a page offset cannot provide that guarantee.
    /// Readers without this capability omit the ID and may serve a single atomic export page.</remarks>
    ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger query,
        CancellationToken cancellationToken);
}
