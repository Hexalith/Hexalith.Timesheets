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

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntrySubmissionScope>))]
public enum TimeEntrySubmissionScope
{
    Unknown = 0,
    SelectedEntries = 1,
    TimesheetPeriod = 2
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

[JsonConverter(typeof(JsonStringEnumConverter<ActivityTypeActiveState>))]
public enum ActivityTypeActiveState
{
    Unknown = 0,
    Active = 1,
    Inactive = 2
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

[JsonConverter(typeof(JsonStringEnumConverter<AiEffortMetricSourceCategory>))]
public enum AiEffortMetricSourceCategory
{
    Unknown = 0,
    Unavailable = 1,
    Provider = 2,
    Tool = 3,
    WorkExecution = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<AiTokenMetricAvailability>))]
public enum AiTokenMetricAvailability
{
    Unknown = 0,
    NotReported = 1,
    Unavailable = 2,
    ProviderReported = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryCorrectionState>))]
public enum TimeEntryCorrectionState
{
    Unknown = 0,
    None = 1,
    Corrected = 2,
    Superseded = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryEvidenceSourceAuthority>))]
public enum TimeEntryEvidenceSourceAuthority
{
    Unknown = 0,
    TimesheetsDomainEvents = 1
}

[JsonConverter(typeof(JsonStringEnumConverter<DisplayHydrationState>))]
public enum DisplayHydrationState
{
    Unknown = 0,
    Fresh = 1,
    Stale = 2,
    Unavailable = 3,
    Denied = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<ApprovalAuthorityAction>))]
public enum ApprovalAuthorityAction
{
    Unknown = 0,
    EntryApproval = 1,
    EntryRejection = 2,
    PeriodApproval = 3,
    PeriodRejection = 4,
    CorrectionAuthorization = 5,
    ApprovedTimeExportEligibility = 6
}

[JsonConverter(typeof(JsonStringEnumConverter<ApprovalAuthoritySource>))]
public enum ApprovalAuthoritySource
{
    Unknown = 0,
    SelfApprovalPolicy = 1,
    ProjectApprover = 2,
    WorkOwner = 3,
    TenantAdministrator = 4,
    FinanceReviewer = 5,
    DefaultDeny = 6
}

[JsonConverter(typeof(JsonStringEnumConverter<ApprovalAuthorityDecisionState>))]
public enum ApprovalAuthorityDecisionState
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    Stale = 3,
    Unavailable = 4,
    Ambiguous = 5,
    MissingActor = 6,
    DisabledTenant = 7,
    InvalidReference = 8,
    CrossTenantTarget = 9
}
