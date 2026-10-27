namespace Hexalith.Timesheets.Endpoints.MagicLinks;

/// <summary>Operator request; authority and inventory verification remain server-side.</summary>
/// <param name="OperationId">The immutable operation to begin or resume.</param>
/// <param name="CertifiedInventoryFingerprint">The exact inventory fingerprint requiring server certification.</param>
public sealed record MagicLinkV6MigrationRequest(string OperationId,
    string CertifiedInventoryFingerprint);
