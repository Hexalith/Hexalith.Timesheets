using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Authorization;

public static class TimesheetsServerRequestContext
{
    public static TimesheetsRequestContext FromTrustedSources(
        string? tenantId,
        string? actorPartyId,
        string correlationId)
        => new(
            string.IsNullOrWhiteSpace(tenantId) ? null : new TenantReference(tenantId),
            string.IsNullOrWhiteSpace(actorPartyId) ? null : new PartyReference(actorPartyId),
            correlationId);
}
