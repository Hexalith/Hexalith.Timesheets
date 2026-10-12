using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Independently verifies a global source inventory and enforced maintenance window. An implementation
/// must verify installed cross-tenant coverage, immutable source-cut and erasure evidence, and externally
/// prevent source writes and deliveries until promotion/abort. Tenant snapshots and delivery checkpoints
/// are insufficient. Receipt lookup and revalidation must fail closed on missing or revoked evidence.
/// </summary>
public interface IMagicLinkV1VerifiedInventoryProvider
{
    /// <summary>Resolves server-verified evidence; a caller supplies only a receipt identifier.</summary>
    Task<MagicLinkV1InventoryReceipt?> GetVerifiedReceiptAsync(string receiptId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replays every retained issuance, including terminal owners and collisions, ordered by tenant then
    /// capability identifier using ordinal comparison. Owners appear exactly once, with at most 10,000
    /// retained contributions per tenant. Tenant identifiers are bounded to 256 UTF-8 bytes and hashes
    /// use the domain's 128-character bound. Each enumeration must replay the same immutable source cut.
    /// </summary>
    IAsyncEnumerable<MagicLinkTokenHashCapabilityIndexDirectoryEntry> ReadContributionsAsync(
        MagicLinkV1InventoryReceipt receipt, CancellationToken cancellationToken);

    /// <summary>Rechecks independent coverage, exact source cut, receipt validity and enforced quiescence.</summary>
    Task<bool> ValidateReceiptAsync(MagicLinkV1InventoryReceipt receipt,
        CancellationToken cancellationToken);
}
