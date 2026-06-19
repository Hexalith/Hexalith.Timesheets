using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record AiEffortMetricSourceMetadata(
    AiEffortMetricSourceCategory SourceCategory,
    string? ProviderName,
    string? ToolName,
    string? WorkExecutionId)
{
    public static AiEffortMetricSourceMetadata Unavailable { get; } = new(
        AiEffortMetricSourceCategory.Unavailable,
        null,
        null,
        null);

    public static AiEffortMetricSourceMetadata Provider(
        string providerName,
        string? toolName,
        string? workExecutionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return new(
            AiEffortMetricSourceCategory.Provider,
            providerName,
            toolName,
            workExecutionId);
    }
}
