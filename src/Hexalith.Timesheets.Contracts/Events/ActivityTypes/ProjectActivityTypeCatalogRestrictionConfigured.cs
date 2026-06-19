using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.ActivityTypes;

public sealed record ProjectActivityTypeCatalogRestrictionConfigured(
    ProjectReference Project,
    bool IsRestricted,
    IReadOnlyList<ActivityTypeId> AllowedTenantActivityTypeIds,
    IReadOnlyList<ActivityTypeId> AllowedProjectActivityTypeIds);
