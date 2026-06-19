using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

public interface IMagicLinkConfirmationCapabilityStateLoader
{
    ValueTask<ActivityTypeCatalogReadModel> LoadActivityTypeCatalogAsync(CancellationToken cancellationToken);

    ValueTask<MagicLinkCapabilityState?> LoadCapabilityAsync(
        MagicLinkCapabilityId capabilityId,
        CancellationToken cancellationToken);

    ValueTask<MagicLinkEndpointTokenState> LoadTokenStateAsync(
        string oneTimeToken,
        CancellationToken cancellationToken);
}
