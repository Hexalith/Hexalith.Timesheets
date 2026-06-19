using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActivityTypeCatalogItem(
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope Scope,
    ProjectReference? Project,
    string Label,
    bool IsActive,
    BillableState DefaultBillableState);
