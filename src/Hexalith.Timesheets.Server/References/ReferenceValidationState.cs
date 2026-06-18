namespace Hexalith.Timesheets.Server.References;

public enum ReferenceValidationState
{
    Valid = 0,
    Unauthorized = 1,
    TenantMismatch = 2,
    Stale = 3,
    Ambiguous = 4,
    Unavailable = 5,
    DisabledOrArchived = 6,
    InvalidReference = 7
}
