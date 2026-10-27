using System.Security.Claims;

using Hexalith.Timesheets.Endpoints.MagicLinks;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Test-only server binding for one principal and exact operation/evidence pair.</summary>
internal sealed class InternalMagicLinkMigrationCallerAuthorizer(string? operationId, string? evidenceId,
    string callerId = "certified-operator", bool allowV1 = true, bool allowV6 = true)
    : IMagicLinkIndexMigrationCallerAuthorizer
{
    /// <summary>Gets how many caller bindings were evaluated.</summary>
    public int Calls { get; private set; }

    /// <summary>Gets an optional simulated provider outage.</summary>
    public Exception? Failure { get; init; }

    /// <inheritdoc />
    public Task<bool> AuthorizeV1MigrationAsync(ClaimsPrincipal caller, string operationId,
        string receiptId, CancellationToken cancellationToken) => AuthorizeAsync(caller, operationId, receiptId, allowV1);

    /// <inheritdoc />
    public Task<bool> AuthorizeV6MigrationAsync(ClaimsPrincipal caller, string operationId,
        string certifiedInventoryFingerprint, CancellationToken cancellationToken)
        => AuthorizeAsync(caller, operationId, certifiedInventoryFingerprint, allowV6);

    private Task<bool> AuthorizeAsync(ClaimsPrincipal caller, string operation, string evidence, bool versionAllowed)
    {
        Calls++;
        if (Failure is not null)
            return Task.FromException<bool>(Failure);
        return Task.FromResult(versionAllowed && caller.Identity?.IsAuthenticated == true
            && caller.FindFirstValue(ClaimTypes.NameIdentifier) == callerId
            && operation == operationId && evidence == evidenceId);
    }
}
