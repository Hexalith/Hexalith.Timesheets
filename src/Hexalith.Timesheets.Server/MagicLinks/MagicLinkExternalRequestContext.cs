using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Builds external-link request authority only from resolved capability state.</summary>
public static class MagicLinkExternalRequestContext
{
    /// <summary>Creates a fail-closed context from a resolved capability candidate.</summary>
    /// <param name="state">The authoritatively folded capability state, if available.</param>
    /// <param name="correlationId">The server-issued correlation identifier.</param>
    /// <returns>The server authority context for the external action.</returns>
    public static TimesheetsRequestContext FromResolvedCapability(
        MagicLinkCapabilityState? state,
        string correlationId)
        => TimesheetsServerRequestContext.FromTrustedSources(
            state?.Tenant?.TenantId,
            state?.Contributor?.PartyId,
            correlationId);
}
