namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Verifies external original-layout and complete unchanged-source evidence for abort repair.</summary>
public interface IMagicLinkV6AbortRepairCertification
{
    /// <summary>Returns an authoritative certificate for this exact witness, or denies repair.</summary>
    Task<MagicLinkV6AbortRepairCertificate?> CertifyAsync(MagicLinkV6MigrationAbortWitness witness,
        CancellationToken cancellationToken);
}
