using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.Exports;

public sealed record GenerateApprovedTimeExport
{
    public QueryApprovedTimeLedger LedgerQuery { get; init; } = new()
    {
        BillableState = BillableState.Billable
    };

    public ApprovedTimeExportFormat Format { get; init; } = ApprovedTimeExportFormat.Csv;

    public string FormatVersion { get; init; } = "approved-time-export.csv.v1";
}
