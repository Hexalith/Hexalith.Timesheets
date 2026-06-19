namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeLedgerReadModel(
    IReadOnlyList<ApprovedTimeLedgerRowReadModel> Items,
    string? NextCursor,
    ProjectionFreshnessMetadata ProjectionFreshness,
    bool CanUseForExport,
    string ExportReadinessDetail);
