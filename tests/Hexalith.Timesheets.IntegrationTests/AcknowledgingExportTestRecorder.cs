using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Exports;

namespace Hexalith.Timesheets.IntegrationTests;

internal sealed class AcknowledgingExportTestRecorder : IApprovedTimeExportAuditRecorder
{
    public ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
        ApprovedTimeExported evidence, CancellationToken cancellationToken)
        => ValueTask.FromResult(TimesheetsDomainResult.Success([evidence]));
}
