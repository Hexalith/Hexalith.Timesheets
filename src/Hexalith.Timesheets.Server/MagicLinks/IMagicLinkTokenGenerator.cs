using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public interface IMagicLinkTokenGenerator
{
    MagicLinkTokenMaterial Generate();

    MagicLinkTokenHash DeriveHash(string oneTimeToken);
}
