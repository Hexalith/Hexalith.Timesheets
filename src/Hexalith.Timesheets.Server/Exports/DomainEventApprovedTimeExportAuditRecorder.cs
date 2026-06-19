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

        return ValueTask.FromResult(TimesheetsDomainResult.Success([evidence]));
    }
}
