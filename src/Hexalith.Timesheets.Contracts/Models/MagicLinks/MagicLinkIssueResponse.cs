using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkIssueResponse(
    MagicLinkCapabilityId CapabilityId,
    string OneTimeToken,
    DateTimeOffset ExpiresAtUtc);
