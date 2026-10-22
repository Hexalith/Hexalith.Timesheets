using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Records internal read outcomes without exception details or domain identifiers.</summary>
internal static partial class MagicLinkStateReadLog
{
    /// <summary>Records a fixed operation and failure category with the platform trace identifier.</summary>
    internal static void Failure(ILogger? logger, string operation, Exception exception)
    {
        if (logger is null)
        {
            return;
        }

        string category = exception switch
        {
            HttpRequestException or IOException or EventStoreGatewayException
                or Dapr.DaprException or SharedProjectionReadUnavailableException => "dependency-unavailable",
            TimeoutException or OperationCanceledException => "dependency-timeout",
            JsonException => "invalid-content",
            InvalidOperationException or ArgumentException or NullReferenceException => "integrity-failure",
            _ => "internal-failure"
        };
        try
        {
            ReadFailed(logger, operation, category, Activity.Current?.TraceId.ToHexString() ?? string.Empty);
        }
        catch (Exception)
        {
            // Diagnostic providers must not replace an already selected fail-closed outcome.
        }
    }

    [LoggerMessage(EventId = 1301, Level = LogLevel.Warning,
        Message = "State read failed: operation={Operation} outcome={Outcome} trace={TraceId}")]
    private static partial void ReadFailed(ILogger logger, string operation, string outcome, string traceId);
}
