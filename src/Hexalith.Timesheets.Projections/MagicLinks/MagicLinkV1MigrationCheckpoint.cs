using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Private numeric progress binding a complete tenant boundary to its receipt and legacy source.</summary>
/// <param name="Ordinal">The monotonically increasing tenant checkpoint ordinal.</param>
/// <param name="OperationId">The exact operator operation.</param>
/// <param name="Receipt">All independently verified inventory and quiescence evidence.</param>
/// <param name="SourceGeneration">The unchanged selected legacy generation.</param>
/// <param name="SourceDigest">The direct/selected legacy root snapshot binding.</param>
/// <param name="LastTenantId">Last complete tenant, or null before any tenant.</param>
/// <param name="CompletedContributionCount">Number of retained rows through this tenant.</param>
/// <param name="Root">Private v7 tree snapshot.</param>
/// <param name="NextRetirementOrdinal">Next numeric retirement record.</param>
/// <param name="NextPageOrdinal">Next durable SDK append ordinal.</param>
public sealed record MagicLinkV1MigrationCheckpoint(long Ordinal, string OperationId,
    MagicLinkV1InventoryReceipt Receipt, long SourceGeneration, string SourceDigest,
    string? LastTenantId, long CompletedContributionCount,
    MagicLinkTokenHashCapabilityIndexTreeRoot Root, long NextRetirementOrdinal, int NextPageOrdinal);
