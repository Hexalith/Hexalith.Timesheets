using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Provides the original-layout certificate from independently captured fixture evidence.</summary>
/// <param name="sourceDigest">The independently captured complete source-layout fingerprint.</param>
internal sealed class CertifiedAbortLayout(string sourceDigest) : IMagicLinkV6AbortRepairCertification
{
    /// <summary>Gets or sets a deliberate non-null certificate mismatch for refusal verification.</summary>
    public Func<MagicLinkV6AbortRepairCertificate, MagicLinkV6AbortRepairCertificate>? Transform { get; init; }

    /// <inheritdoc />
    public Task<MagicLinkV6AbortRepairCertificate?> CertifyAsync(MagicLinkV6MigrationAbortWitness witness, CancellationToken cancellationToken)
    {
        var certificate = new MagicLinkV6AbortRepairCertificate(witness, "original-layout-certificate", sourceDigest,
            [MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey]);
        return Task.FromResult<MagicLinkV6AbortRepairCertificate?>(Transform?.Invoke(certificate) ?? certificate);
    }
}
