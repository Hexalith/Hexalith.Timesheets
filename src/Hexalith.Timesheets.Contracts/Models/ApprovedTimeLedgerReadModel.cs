using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeLedgerReadModel(
    IReadOnlyList<ApprovedTimeLedgerRowReadModel> Items,
    string? NextCursor,
    ProjectionFreshnessMetadata ProjectionFreshness,
    bool CanUseForExport,
    string ExportReadinessDetail)
{
    /// <summary>Gets the reader-certified immutable version of the entire tenant ledger scope.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExportSnapshotId { get; init; }
}
