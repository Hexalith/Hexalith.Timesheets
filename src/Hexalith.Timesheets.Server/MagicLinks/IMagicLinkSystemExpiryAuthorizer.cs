using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Authorizes system expiry independently of Party management authority.</summary>
public interface IMagicLinkSystemExpiryAuthorizer
{
    /// <summary>Checks verified gateway workload evidence against the authoritative capability scope.</summary>
    /// <param name="verifiedWorkload">The gateway-stamped workload identity, never an envelope user or Party identity.</param>
    /// <param name="tenant">The authoritative tenant.</param>
    /// <param name="capabilityId">The authoritative capability identifier.</param>
    /// <param name="timeEntryId">The authoritative owning Time Entry identifier.</param>
    /// <param name="target">The authoritative Project or Work target.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether system expiry is authorized for this exact scope.</returns>
    ValueTask<bool> AuthorizeAsync(
        string verifiedWorkload,
        TenantReference tenant,
        MagicLinkCapabilityId capabilityId,
        TimeEntryId timeEntryId,
        TimeEntryTargetReference target,
        CancellationToken cancellationToken);
}
