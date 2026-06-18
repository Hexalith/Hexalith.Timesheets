using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsRequestContext(
    TenantReference? Tenant,
    PartyReference? Actor,
    string CorrelationId);
