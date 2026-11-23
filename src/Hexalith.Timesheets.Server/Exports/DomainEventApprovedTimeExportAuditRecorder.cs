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
        // Direct construction without a durable recorder withholds delivery.
        return ValueTask.FromResult(TimesheetsDomainResult.NoOp());
    }
}
