namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A numeric roster page and its short sortable first-row prefix.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexTenantRosterPageReference(
    MagicLinkTokenHashCapabilityIndexNodeReference Page,
    string FirstPathPrefix);
