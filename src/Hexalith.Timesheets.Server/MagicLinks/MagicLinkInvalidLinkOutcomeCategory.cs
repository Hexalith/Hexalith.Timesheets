namespace Hexalith.Timesheets.Server.MagicLinks;

public enum MagicLinkInvalidLinkOutcomeCategory
{
    Unknown = 0,
    Malformed = 1,
    UnknownCapability = 2,
    HashMismatch = 3,
    Expired = 4,
    Revoked = 5,
    Used = 6,
    TenantMismatch = 7,
    WrongRecipient = 8,
    WrongAction = 9,
    WrongScope = 10,
    StaleCatalog = 11,
    Unauthorized = 12,
    RateLimited = 13,
    RepeatedAttempt = 14
}
