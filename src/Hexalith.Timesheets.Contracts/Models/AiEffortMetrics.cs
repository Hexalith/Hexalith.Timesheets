using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record AiEffortMetrics(
    AiMetricAvailability Availability,
    int? WallClockDurationMilliseconds,
    int? ModelRuntimeMilliseconds,
    int? BillableEffortMinutes,
    long? ProviderInputTokenCount,
    long? ProviderOutputTokenCount,
    long? ProviderTotalTokenCount)
{
    public static AiEffortMetrics Unavailable { get; } = new(
        AiMetricAvailability.Unavailable,
        null,
        null,
        null,
        null,
        null,
        null);
}
