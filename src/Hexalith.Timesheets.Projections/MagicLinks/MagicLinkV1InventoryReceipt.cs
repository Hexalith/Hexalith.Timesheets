namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Server-issued evidence for an immutable, replayable, complete cross-tenant issuance inventory.
/// Coverage includes terminal capabilities and omitted collisions, or independently verified erasure
/// exclusions. The installation and quiescence receipts must come from independent server verification;
/// legacy rows, checkpoint enumeration, observed maxima and caller assertions cannot establish them.
/// </summary>
/// <param name="ReceiptId">The immutable server-side receipt identity.</param>
/// <param name="SourceCutRevision">The exact immutable source cut to replay.</param>
/// <param name="InstallationCoverageReceiptId">Independent global installation/coverage proof.</param>
/// <param name="QuiescenceReceiptId">Enforced source-writer and delivery quiescence through promotion.</param>
/// <param name="ExpectedContributionCount">All retained issuance contributions at the certified cut.</param>
/// <param name="ExpectedContributionDigest">Uppercase SHA-256 of the ordered, length-prefixed contributions.</param>
public sealed record MagicLinkV1InventoryReceipt(
    string ReceiptId,
    string SourceCutRevision,
    string InstallationCoverageReceiptId,
    string QuiescenceReceiptId,
    long ExpectedContributionCount,
    string ExpectedContributionDigest);
