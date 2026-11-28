using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Records low-cardinality external recording outcomes.</summary>
internal static partial class ExternalRecordingLog
{
    /// <summary>Outcome for a verified refusal.</summary>
    internal const string PolicyRefusal = "policy-refusal";

    /// <summary>Outcome when a durable recording cannot be verified.</summary>
    internal const string IndeterminateCommit = "indeterminate-commit";

    /// <summary>Outcome for a committed and verified recording.</summary>
    internal const string Committed = "committed";

    /// <summary>Outcome for a verified recording already present at preflight.</summary>
    internal const string AlreadyRecorded = "already-recorded";

    /// <summary>Records a fixed outcome with the active trace identifier.</summary>
    internal static void RecordOutcome(ILogger? logger, string outcome)
    {
        if (logger is null)
        {
            return;
        }

        string traceId = Activity.Current?.TraceId.ToHexString() ?? string.Empty;
        try
        {
            if (outcome is Committed or AlreadyRecorded)
            {
                RecordingAccepted(logger, outcome, traceId);
            }
            else
            {
                RecordingWithheld(logger, outcome, traceId);
            }
        }
        catch (Exception)
        {
            // Diagnostic providers cannot replace the selected outcome.
        }
    }

    [LoggerMessage(
        EventId = 1403,
        Level = LogLevel.Information,
        Message = "External recording completed: outcome={Outcome} trace={TraceId}")]
    private static partial void RecordingAccepted(ILogger logger, string outcome, string traceId);

    [LoggerMessage(
        EventId = 1404,
        Level = LogLevel.Warning,
        Message = "External recording withheld: outcome={Outcome} trace={TraceId}")]
    private static partial void RecordingWithheld(ILogger logger, string outcome, string traceId);
}
