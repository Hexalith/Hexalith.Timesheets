namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Denies repair until a server-side original-layout certificate verifier is supplied.</summary>
public sealed class DenyAllMagicLinkV6AbortRepairCertification : IMagicLinkV6AbortRepairCertification
{
    /// <inheritdoc />
    public Task<MagicLinkV6AbortRepairCertificate?> CertifyAsync(MagicLinkV6MigrationAbortWitness witness,
        CancellationToken cancellationToken) => Task.FromResult<MagicLinkV6AbortRepairCertificate?>(null);
}
