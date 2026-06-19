namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActualTimeReportReadModel(
    IReadOnlyList<ActualTimeReportRowReadModel> Items,
    string? NextCursor,
    ProjectionFreshnessMetadata ProjectionFreshness);
