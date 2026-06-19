namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryQueryReadModel(
    IReadOnlyList<TimeEntryQueryRowReadModel> Items,
    string? NextCursor,
    ProjectionFreshnessMetadata ProjectionFreshness);
