using Hexalith.Timesheets.Contracts;

namespace Hexalith.Timesheets.Client;

public interface ITimesheetsClient
{
    ValueTask<IReadOnlyList<TimesheetsMetadataDescriptor>> GetMetadataDescriptorsAsync(CancellationToken cancellationToken);
}
