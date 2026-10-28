namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Server-certified original v6 layout, never inferred from index membership.</summary>
/// <param name="Witness">The exact previously authorized abort owner.</param>
/// <param name="CertificateId">The immutable verifier receipt identity.</param>
/// <param name="SourceDigest">The canonical complete v6 guard inventory digest, including keys, ETags, content and absent shards.</param>
/// <param name="OriginallyAbsentMetadataKeys">The complete metadata keys proven absent before migration.</param>
public sealed record MagicLinkV6AbortRepairCertificate(MagicLinkV6MigrationAbortWitness Witness,
    string CertificateId, string SourceDigest, string[] OriginallyAbsentMetadataKeys);
