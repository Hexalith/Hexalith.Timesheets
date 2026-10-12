using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Maps one test token to a selected candidate hash.</summary>
internal sealed class FixedIndexTokenGenerator(string hash) : IMagicLinkTokenGenerator
{
    public MagicLinkTokenMaterial Generate() => new("opaque-interior", new MagicLinkTokenHash(hash));

    public MagicLinkTokenHash DeriveHash(string oneTimeToken)
        => oneTimeToken == "opaque-interior" ? new MagicLinkTokenHash(hash)
            : new MagicLinkTokenHash("unknown-hash");
}
