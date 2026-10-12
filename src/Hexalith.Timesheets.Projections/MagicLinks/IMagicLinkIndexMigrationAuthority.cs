namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Server-side operator authority and certified full-inventory receipt check for the explicit
/// v6-to-v7 migration and verified-inventory v1-to-v7 migration. A submitted fingerprint alone never
/// grants this operation; v1 calls bind all server-resolved receipt fields into the fingerprint.
/// </summary>
public interface IMagicLinkIndexMigrationAuthority
{
    /// <summary>Confirms the operator and complete source-inventory receipt.</summary>
    Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken);
}

/// <summary>Default launch posture until an operator authority and receipt verifier are bound.</summary>
public sealed class DenyAllMagicLinkIndexMigrationAuthority : IMagicLinkIndexMigrationAuthority
{
    /// <inheritdoc />
    public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
