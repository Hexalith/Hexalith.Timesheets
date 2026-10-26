using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>Preserves a restriction contribution without guessing an authoritative policy stream.</summary>
/// <param name="SourceAggregateId">The actual source aggregate, absent in an unaddressed pure fold.</param>
/// <param name="Project">The Project identifier governed by this contribution.</param>
/// <param name="IsRestricted">Whether only the configured identifiers may be selected.</param>
/// <param name="AllowedTenantActivityTypeIds">Allowed tenant-owned Activity Type identifiers.</param>
/// <param name="AllowedProjectActivityTypeIds">Allowed Project-owned identifiers; magic links still require tenant ownership.</param>
public sealed record ProjectActivityTypeCatalogRestrictionSnapshot(
    string? SourceAggregateId,
    ProjectReference Project,
    bool IsRestricted,
    IReadOnlyList<ActivityTypeId> AllowedTenantActivityTypeIds,
    IReadOnlyList<ActivityTypeId> AllowedProjectActivityTypeIds);
