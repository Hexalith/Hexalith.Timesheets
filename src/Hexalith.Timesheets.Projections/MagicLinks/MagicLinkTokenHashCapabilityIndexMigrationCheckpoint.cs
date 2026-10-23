using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Private staged migration progress, addressed only by epoch and numeric ordinal.</summary>
/// <param name="Ordinal">The completed tenant checkpoint ordinal, with zero denoting the initial checkpoint.</param>
/// <param name="SourceGeneration">The selected v6 generation retained until promotion.</param>
/// <param name="SourceDigest">The digest of the complete certified source contribution sequence.</param>
/// <param name="LastTenantId">The last completed tenant, or null for the initial checkpoint.</param>
/// <param name="Root">The private v7 root after the completed tenants.</param>
/// <param name="NextRetirementOrdinal">The next private retirement record ordinal.</param>
/// <param name="NextPageOrdinal">The next streaming stage page ordinal.</param>
public sealed record MagicLinkTokenHashCapabilityIndexMigrationCheckpoint(
    long Ordinal, long SourceGeneration, string SourceDigest, string? LastTenantId,
    MagicLinkTokenHashCapabilityIndexTreeRoot Root, long NextRetirementOrdinal, int NextPageOrdinal);
