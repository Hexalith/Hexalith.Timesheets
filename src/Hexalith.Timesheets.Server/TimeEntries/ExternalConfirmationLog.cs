using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Records low-cardinality external confirmation outcomes without protected identifiers or payloads.</summary>
internal static partial class ExternalConfirmationLog
{
    /// <summary>Outcome recorded when authorization or domain validation refuses confirmation.</summary>
    internal const string PolicyRefusal = "policy-refusal";

    /// <summary>Outcome recorded when confirmation commit or readback cannot be verified.</summary>
    internal const string IndeterminateCommit = "indeterminate-commit";

    /// <summary>Outcome recorded when confirmation is committed and verified on the owner stream.</summary>
    internal const string Committed = "committed";

    /// <summary>Outcome recorded when an existing matching confirmation is verified on the owner stream.</summary>
    internal const string AlreadyConfirmed = "already-confirmed";

    /// <summary>Logs a fixed confirmation outcome with the active trace identifier.</summary>
    /// <param name="logger">The optional target logger.</param>
    /// <param name="outcome">The low-cardinality outcome constant.</param>
    internal static void RecordOutcome(ILogger? logger, string outcome)
    {
        if (logger is null)
        {
            return;
        }

        string traceId = Activity.Current?.TraceId.ToHexString() ?? string.Empty;
        try
        {
            if (outcome is Committed or AlreadyConfirmed)
            {
                ConfirmationAccepted(logger, outcome, traceId);
            }
            else
            {
                ConfirmationNotAccepted(logger, outcome, traceId);
            }
        }
        catch (Exception)
        {
            // Diagnostic providers must not replace a selected confirmation outcome.
        }
    }

    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Information,
        Message = "External confirmation completed: outcome={Outcome} trace={TraceId}")]
    private static partial void ConfirmationAccepted(ILogger logger, string outcome, string traceId);

    [LoggerMessage(
        EventId = 1402,
        Level = LogLevel.Warning,
        Message = "External confirmation withheld: outcome={Outcome} trace={TraceId}")]
    private static partial void ConfirmationNotAccepted(ILogger logger, string outcome, string traceId);
}
