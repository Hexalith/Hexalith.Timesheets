using Hexalith.Timesheets.Projections.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Fixture authority bound to the exact simulated receipt; never registered by production.</summary>
internal sealed class SimulatedMagicLinkV1Authority(SimulatedMagicLinkV1Inventory inventory) : IMagicLinkIndexMigrationAuthority
{
    public bool Permitted { get; set; } = true;

    public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken)
        => Task.FromResult(Permitted && certifiedInventoryFingerprint == MagicLinkV1InventoryDigest.ReceiptFingerprint(inventory.Receipt));
}
