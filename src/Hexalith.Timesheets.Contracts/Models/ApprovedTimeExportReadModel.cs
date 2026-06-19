using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeExportReadModel(
    ApprovedTimeExportReadinessState Readiness,
    string ReadinessDetail,
    ApprovedTimeExportScope Scope,
    ProjectionFreshnessMetadata ProjectionFreshness,
    ApprovedTimeExportAuditMetadata Audit,
    ApprovedTimeExportFormat Format,
    string FormatVersion,
    IReadOnlyList<ApprovedTimeExportRowReadModel> Rows)
{
    public string? CsvContent { get; init; }

    public bool HasOutput => Readiness == ApprovedTimeExportReadinessState.Ready
        && Rows.Count > 0
        && !string.IsNullOrWhiteSpace(CsvContent);
}
