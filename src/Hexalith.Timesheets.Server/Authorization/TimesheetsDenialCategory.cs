namespace Hexalith.Timesheets.Server.Authorization;

public enum TimesheetsDenialCategory
{
    None = 0,
    MissingTenant = 1,
    DisabledTenant = 2,
    UnknownUser = 3,
    NonMember = 4,
    InsufficientRole = 5,
    CrossTenantTarget = 6,
    StaleProjection = 7,
    AmbiguousAuthority = 8,
    UnavailableSiblingAuthority = 9,
    UnconfiguredPolicy = 10,
    InvalidReference = 11,
    CommentPolicyMissing = 12,
    RetentionPolicyMissing = 13
}
