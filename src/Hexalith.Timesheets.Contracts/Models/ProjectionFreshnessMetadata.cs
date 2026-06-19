using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ProjectionFreshnessMetadata(
    ProjectionFreshnessState State,
    string? Cursor,
    DateTimeOffset? AsOfUtc,
    string? Detail)
{
    public static ProjectionFreshnessMetadata Fresh { get; } = new(
        ProjectionFreshnessState.Fresh,
        null,
        null,
        null);

    public static ProjectionFreshnessMetadata Rebuilding(string? detail = "Projection is rebuilding.") => new(
        ProjectionFreshnessState.Rebuilding,
        null,
        null,
        detail);
}
