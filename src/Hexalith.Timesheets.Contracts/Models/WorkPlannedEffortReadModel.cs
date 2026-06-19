using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record WorkPlannedEffortReadModel(
    WorkPlannedEffortAvailability Availability,
    string SourceModuleName,
    decimal? Estimated,
    decimal? Done,
    decimal? Remaining,
    string? Unit,
    ActualTimeReferenceStateMetadata SourceReferenceState,
    ProjectionFreshnessMetadata SourceFreshness)
{
    public static WorkPlannedEffortReadModel Supplied(
        decimal? estimated,
        decimal? done,
        decimal? remaining,
        string unit,
        ProjectionFreshnessMetadata sourceFreshness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentNullException.ThrowIfNull(sourceFreshness);

        return new(
            WorkPlannedEffortAvailability.Supplied,
            "Works",
            estimated,
            done,
            remaining,
            unit,
            ActualTimeReferenceStateMetadata.Current,
            sourceFreshness);
    }

    public static WorkPlannedEffortReadModel NotSupplied(string? detail = "Works did not supply planned effort.")
        => WithNoValues(
            WorkPlannedEffortAvailability.NotSupplied,
            ProjectionFreshnessMetadata.Fresh,
            ActualTimeReferenceStateMetadata.Current,
            detail);

    public static WorkPlannedEffortReadModel Unavailable(string? detail = "Works planned effort is unavailable.")
        => WithNoValues(
            WorkPlannedEffortAvailability.Unavailable,
            ProjectionFreshnessMetadata.Unavailable(detail),
            ActualTimeReferenceStateMetadata.Unavailable(detail),
            detail);

    public static WorkPlannedEffortReadModel Unauthorized(string? detail = "Works planned effort is not available to this caller.")
        => WithNoValues(
            WorkPlannedEffortAvailability.Unauthorized,
            ProjectionFreshnessMetadata.Unavailable(detail),
            ActualTimeReferenceStateMetadata.Unauthorized(detail),
            detail);

    public static WorkPlannedEffortReadModel Stale(
        decimal? estimated,
        decimal? done,
        decimal? remaining,
        string unit,
        ProjectionFreshnessMetadata sourceFreshness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentNullException.ThrowIfNull(sourceFreshness);

        return new(
            WorkPlannedEffortAvailability.Stale,
            "Works",
            estimated,
            done,
            remaining,
            unit,
            ActualTimeReferenceStateMetadata.Current,
            sourceFreshness);
    }

    private static WorkPlannedEffortReadModel WithNoValues(
        WorkPlannedEffortAvailability availability,
        ProjectionFreshnessMetadata sourceFreshness,
        ActualTimeReferenceStateMetadata sourceReferenceState,
        string? detail)
        => new(
            availability,
            "Works",
            null,
            null,
            null,
            null,
            sourceReferenceState,
            sourceFreshness with { Detail = detail });
}
