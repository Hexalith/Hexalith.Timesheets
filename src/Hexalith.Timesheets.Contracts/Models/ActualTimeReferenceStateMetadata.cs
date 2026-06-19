using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActualTimeReferenceStateMetadata(
    ActualTimeReferenceState State,
    ProjectionFreshnessMetadata Freshness,
    string? Detail)
{
    public static ActualTimeReferenceStateMetadata Current { get; } = new(
        ActualTimeReferenceState.Current,
        ProjectionFreshnessMetadata.Fresh,
        null);

    public static ActualTimeReferenceStateMetadata Rebuilding(string? detail = "Reference state is rebuilding.")
        => new(
            ActualTimeReferenceState.Rebuilding,
            ProjectionFreshnessMetadata.Rebuilding(),
            detail);

    public static ActualTimeReferenceStateMetadata Unavailable(string? detail = "Reference state is unavailable.")
        => new(
            ActualTimeReferenceState.Unavailable,
            ProjectionFreshnessMetadata.Unavailable(),
            detail);

    public static ActualTimeReferenceStateMetadata Unauthorized(string? detail = "Reference state is not available to this caller.")
        => new(
            ActualTimeReferenceState.Unauthorized,
            ProjectionFreshnessMetadata.Unavailable(detail),
            detail);
}
