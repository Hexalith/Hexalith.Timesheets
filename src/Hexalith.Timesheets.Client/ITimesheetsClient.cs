using Hexalith.Timesheets.Contracts.Ui;

namespace Hexalith.Timesheets.Client;

public interface ITimesheetsClient
{
    ValueTask<IReadOnlyList<TimesheetsMetadataDescriptor>> GetMetadataDescriptorsAsync(CancellationToken cancellationToken);
}
