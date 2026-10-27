using System.Security.Claims;

namespace Hexalith.Timesheets.Endpoints.MagicLinks;

/// <summary>
/// Authorizes an authenticated caller for the exact migration operation and evidence before
/// migration I/O. Implementations must resolve permission server-side; caller claims and submitted
/// evidence alone do not grant access. Inventory certification remains a separate requirement.
/// </summary>
public interface IMagicLinkIndexMigrationCallerAuthorizer
{
    /// <summary>Authorizes the caller for this exact v1 operation and server-resolved receipt.</summary>
    Task<bool> AuthorizeV1MigrationAsync(ClaimsPrincipal caller, string operationId,
        string receiptId, CancellationToken cancellationToken);

    /// <summary>Authorizes the caller for this exact v6 operation and inventory fingerprint.</summary>
    Task<bool> AuthorizeV6MigrationAsync(ClaimsPrincipal caller, string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken);
}
