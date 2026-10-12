namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Bounded manifest of one tenant's paged index contributions.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexTenantRoster(
    string TenantId,
    int ContributionCount,
    IReadOnlyList<MagicLinkTokenHashCapabilityIndexTenantRosterPageReference> Pages);
