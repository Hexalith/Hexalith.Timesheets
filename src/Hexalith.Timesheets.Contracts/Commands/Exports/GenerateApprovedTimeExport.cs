using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.Exports;

public sealed record GenerateApprovedTimeExport
{
    /// <summary>Gets the stable export request identity. When omitted, the correlation identifies the request.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; init; }

    public QueryApprovedTimeLedger LedgerQuery { get; init; } = new()
    {
        BillableState = BillableState.Billable
    };

    public ApprovedTimeExportFormat Format { get; init; } = ApprovedTimeExportFormat.Csv;

    public string FormatVersion { get; init; } = "approved-time-export.csv.v1";
}
