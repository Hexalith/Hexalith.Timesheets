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

    public static ProjectionFreshnessMetadata Stale(string? cursor = null, DateTimeOffset? asOfUtc = null, string? detail = "Projection is stale.") => new(
        ProjectionFreshnessState.Stale,
        cursor,
        asOfUtc,
        detail);

    public static ProjectionFreshnessMetadata Unavailable(string? detail = "Projection is unavailable.") => new(
        ProjectionFreshnessState.Unavailable,
        null,
        null,
        detail);
}
