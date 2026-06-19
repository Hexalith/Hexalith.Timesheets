using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Ui;

namespace Hexalith.Timesheets.Client;

public interface ITimesheetsClient
{
    ValueTask<IReadOnlyList<TimesheetsMetadataDescriptor>> GetMetadataDescriptorsAsync(CancellationToken cancellationToken);

    ValueTask SubmitExternalTimeEntryAsync(SubmitExternalTimeEntry command, CancellationToken cancellationToken);

    ValueTask ConfirmExternalTimeEntryAsync(ConfirmExternalTimeEntry command, CancellationToken cancellationToken);
}
