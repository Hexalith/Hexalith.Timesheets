using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.Exports;

public sealed record PreviewApprovedTimeExport
{
    public QueryApprovedTimeLedger LedgerQuery { get; init; } = new()
    {
        BillableState = BillableState.Billable
    };

    public ApprovedTimeExportFormat Format { get; init; } = ApprovedTimeExportFormat.Csv;

    public string FormatVersion { get; init; } = "approved-time-export.csv.v1";
}
