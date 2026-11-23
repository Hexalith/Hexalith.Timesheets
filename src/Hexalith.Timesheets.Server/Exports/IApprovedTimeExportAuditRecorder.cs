using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.Exports;

public interface IApprovedTimeExportAuditRecorder
{
    /// <summary>Returns the single matching export event only after verifying durable completion.
    /// Rejection, unavailable storage and uncertain completion must never acknowledge delivery.</summary>
    ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
        ApprovedTimeExported evidence,
        CancellationToken cancellationToken);
}
