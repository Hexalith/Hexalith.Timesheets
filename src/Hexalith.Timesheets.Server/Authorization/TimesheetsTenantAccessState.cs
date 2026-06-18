namespace Hexalith.Timesheets.Server.Authorization;

public enum TimesheetsTenantAccessState
{
    Authorized = 0,
    MissingTenant = 1,
    DisabledTenant = 2,
    UnknownUser = 3,
    NonMember = 4,
    InsufficientRole = 5,
    StaleProjection = 6,
    AmbiguousAuthority = 7,
    UnavailableSiblingAuthority = 8,
    UnconfiguredPolicy = 9
}
