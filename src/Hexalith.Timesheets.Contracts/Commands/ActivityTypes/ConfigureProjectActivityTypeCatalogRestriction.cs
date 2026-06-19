using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record ConfigureProjectActivityTypeCatalogRestriction(
    ProjectReference Project,
    bool IsRestricted,
    IReadOnlyList<ActivityTypeId> AllowedTenantActivityTypeIds,
    IReadOnlyList<ActivityTypeId> AllowedProjectActivityTypeIds);
