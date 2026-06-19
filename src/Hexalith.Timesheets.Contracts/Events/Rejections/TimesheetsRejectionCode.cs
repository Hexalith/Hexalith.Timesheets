using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.Events.Rejections;

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsRejectionCode>))]
public enum TimesheetsRejectionCode
{
    Unknown = 0,
    ValidationFailed = 1,
    AuthorityCannotBeResolved = 2,
    PolicyDenied = 3,
    TargetNotFound = 4,
    ActivityTypeInactive = 5,
    ProjectionUnavailable = 6
}
