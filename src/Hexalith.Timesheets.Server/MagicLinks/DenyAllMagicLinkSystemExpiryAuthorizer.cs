using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Fails closed until a host supplies authoritative system expiry policy.</summary>
public sealed class DenyAllMagicLinkSystemExpiryAuthorizer : IMagicLinkSystemExpiryAuthorizer
{
    /// <inheritdoc/>
    public ValueTask<bool> AuthorizeAsync(
        string verifiedWorkload,
        TenantReference tenant,
        MagicLinkCapabilityId capabilityId,
        TimeEntryId timeEntryId,
        TimeEntryTargetReference target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}
