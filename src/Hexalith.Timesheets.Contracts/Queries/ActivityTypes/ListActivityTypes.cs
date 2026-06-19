using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.ActivityTypes;

public sealed record ListActivityTypes(
    ActivityTypeScope Scope,
    ProjectReference? Project);
