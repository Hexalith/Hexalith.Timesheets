using System.Security.Claims;

namespace Hexalith.Timesheets.Endpoints.MagicLinks;

/// <summary>Refuses migration callers until a server-side caller-binding authority is installed.</summary>
public sealed class DenyAllMagicLinkIndexMigrationCallerAuthorizer : IMagicLinkIndexMigrationCallerAuthorizer
{
    /// <inheritdoc />
    public Task<bool> AuthorizeV1MigrationAsync(ClaimsPrincipal caller, string operationId,
        string receiptId, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> AuthorizeV6MigrationAsync(ClaimsPrincipal caller, string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken) => Task.FromResult(false);
}
