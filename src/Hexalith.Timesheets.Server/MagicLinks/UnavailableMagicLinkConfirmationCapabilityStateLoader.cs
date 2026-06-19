using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class UnavailableMagicLinkConfirmationCapabilityStateLoader : IMagicLinkConfirmationCapabilityStateLoader
{
    public ValueTask<ActivityTypeCatalogReadModel> LoadActivityTypeCatalogAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(UnavailableCatalog());

    public ValueTask<MagicLinkCapabilityState?> LoadCapabilityAsync(
        MagicLinkCapabilityId capabilityId,
        CancellationToken cancellationToken)
        => ValueTask.FromResult<MagicLinkCapabilityState?>(null);

    public ValueTask<MagicLinkEndpointTokenState> LoadTokenStateAsync(
        string oneTimeToken,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(new MagicLinkEndpointTokenState(null, null, UnavailableCatalog()));

    private static ActivityTypeCatalogReadModel UnavailableCatalog()
        => new([], ProjectionFreshnessMetadata.Unavailable());
}
