using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections;

/// <summary>Records retryable delivery outcomes without exception details or domain identifiers.</summary>
internal static partial class ProjectionDeliveryLog
{
    /// <summary>Records a fixed projection category and platform trace identifier.</summary>
    internal static void StateUnavailable(ILogger? logger, string projection)
    {
        if (logger is not null)
        {
            try
            {
                DeliveryFailed(logger, projection, Activity.Current?.TraceId.ToHexString() ?? string.Empty);
            }
            catch (Exception)
            {
                // Diagnostic providers must not replace an already selected retryable outcome.
            }
        }
    }

    [LoggerMessage(EventId = 1302, Level = LogLevel.Warning,
        Message = "Projection delivery failed: projection={Projection} outcome=state-unavailable trace={TraceId}")]
    private static partial void DeliveryFailed(ILogger logger, string projection, string traceId);
}
