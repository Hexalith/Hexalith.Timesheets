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

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntrySourceType>))]
public enum TimeEntrySourceType
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

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryApprovalScope>))]
public enum TimeEntryApprovalScope
{
    Unknown = 0,
    IndividualEntry = 1,
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
    Unavailable = 4,
    Degraded = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryQuerySortBy>))]
public enum TimeEntryQuerySortBy
{
    Unknown = 0,
    ServiceDate = 1,
    TimeEntryId = 2,
    DurationMinutes = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryQuerySortDirection>))]
public enum TimeEntryQuerySortDirection
{
    Unknown = 0,
    Ascending = 1,
    Descending = 2
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

[JsonConverter(typeof(JsonStringEnumConverter<ApprovedTimeLedgerRowState>))]
public enum ApprovedTimeLedgerRowState
{
    Unknown = 0,
    Current = 1,
    Superseded = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ActualTimeReportRowState>))]
public enum ActualTimeReportRowState
{
    Unknown = 0,
    Current = 1,
    IncludesSuperseded = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ActualTimeReportSortBy>))]
public enum ActualTimeReportSortBy
{
    Unknown = 0,
    TargetReference = 1,
    Period = 2,
    Contributor = 3,
    ActivityType = 4,
    ActualMinutes = 5,
    SourceRowCount = 6,
    AiWallClockDurationMilliseconds = 7,
    AiModelRuntimeMilliseconds = 8,
    AiBillableEffortMinutes = 9,
    AiProviderTotalTokenCount = 10
}

[JsonConverter(typeof(JsonStringEnumConverter<ActualTimeReferenceState>))]
public enum ActualTimeReferenceState
{
    Unknown = 0,
    Current = 1,
    Stale = 2,
    Unavailable = 3,
    Unauthorized = 4,
    Invalid = 5,
    Rebuilding = 6
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkPlannedEffortAvailability>))]
public enum WorkPlannedEffortAvailability
{
    Unknown = 0,
    Supplied = 1,
    NotSupplied = 2,
    Unavailable = 3,
    Unauthorized = 4,
    Stale = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<TimeEntryLockState>))]
public enum TimeEntryLockState
{
    Unknown = 0,
    Unlocked = 1,
    LockedFromDirectEdit = 2,
    SupersededLocked = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetPeriodKind>))]
public enum TimesheetPeriodKind
{
    Unknown = 0,
    Weekly = 1,
    Monthly = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetPeriodApprovalState>))]
public enum TimesheetPeriodApprovalState
{
    Unknown = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
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
