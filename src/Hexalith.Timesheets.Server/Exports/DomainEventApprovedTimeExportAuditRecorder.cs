using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.Exports;

public sealed class DomainEventApprovedTimeExportAuditRecorder : IApprovedTimeExportAuditRecorder
{
    public ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
        ApprovedTimeExported evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        cancellationToken.ThrowIfCancellationRequested();
        // No durable store is installed by the kernel.
        return ValueTask.FromResult(TimesheetsDomainResult.NoOp());
    }
}
