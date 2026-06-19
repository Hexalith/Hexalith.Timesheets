using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed record MagicLinkTokenMaterial(string OneTimeToken, MagicLinkTokenHash TokenHash);
