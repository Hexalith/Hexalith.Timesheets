using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsRequestContext(
    TenantReference? Tenant,
    PartyReference? Actor,
    string CorrelationId)
{
    /// <summary>Gets whether the context was established by a Project workflow entry point.</summary>
    public bool IsProjectWorkflow { get; private init; }

    /// <summary>Creates a context from the tenant and actor established at a Project workflow boundary.</summary>
    public static TimesheetsRequestContext ForProjectWorkflow(TenantReference tenant, PartyReference actor, string correlationId)
        => new(tenant, actor, correlationId) { IsProjectWorkflow = true };
}
