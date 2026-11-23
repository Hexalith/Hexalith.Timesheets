using System.Text.Json.Serialization;

using Hexalith.Timesheets.Contracts.Queries.Reporting;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeExportScope(
    QueryApprovedTimeLedger Filters,
    int RowCount,
    bool IncludesSupersededRows,
    bool CurrentRowsOnly)
{
    /// <summary>Gets the ledger version verified during traversal. A single atomic page may omit it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LedgerCheckpoint { get; init; }

    /// <summary>Gets the projection freshness cursor observed across the export scope.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LedgerCursor { get; init; }

    /// <summary>Gets the snapshot instant when supplied by the ledger reader.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LedgerAsOfUtc { get; init; }
}
