using Hexalith.Timesheets.Contracts.Queries.Reporting;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeExportScope(
    QueryApprovedTimeLedger Filters,
    int RowCount,
    bool IncludesSupersededRows,
    bool CurrentRowsOnly);
