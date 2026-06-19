using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.ValueObjects;

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryTargetKind>))]
public enum TimeEntryTargetKind
{
    Unknown = 0,
    Project = 1,
    Work = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ContributorCategory>))]
public enum ContributorCategory
{
    Unknown = 0,
    Employee = 1,
    ExternalContributor = 2,
    AutomatedAgent = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryApprovalState>))]
public enum TimeEntryApprovalState
{
    Unknown = 0,
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<BillableState>))]
public enum BillableState
{
    Unknown = 0,
    NonBillable = 1,
    Billable = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ActivityTypeScope>))]
public enum ActivityTypeScope
{
    Unknown = 0,
    Tenant = 1,
    Project = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectionFreshnessState>))]
public enum ProjectionFreshnessState
{
    Unknown = 0,
    Fresh = 1,
    Rebuilding = 2,
    Stale = 3,
    Unavailable = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<AiMetricAvailability>))]
public enum AiMetricAvailability
{
    Unknown = 0,
    Unavailable = 1,
    ProviderReported = 2,
    Estimated = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryCorrectionState>))]
public enum TimeEntryCorrectionState
{
    Unknown = 0,
    None = 1,
    Corrected = 2,
    Superseded = 3
}
