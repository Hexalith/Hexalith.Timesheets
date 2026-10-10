using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks.Commands;

/// <summary>Server-resolved issuance intent for the capability stream.</summary>
/// <param name="Command">The requested capability scope.</param>
/// <param name="Tenant">The server-resolved tenant.</param>
/// <param name="Issuer">The server-resolved issuer.</param>
/// <param name="TokenHash">The hash of the transient token returned only after commit proof.</param>
/// <param name="IssuedAtUtc">The UTC issuance instant.</param>
public sealed record CommitMagicLinkIssue(
    IssueMagicLinkConfirmationCapability Command,
    TenantReference Tenant,
    PartyReference Issuer,
    MagicLinkTokenHash TokenHash,
    DateTimeOffset IssuedAtUtc);
