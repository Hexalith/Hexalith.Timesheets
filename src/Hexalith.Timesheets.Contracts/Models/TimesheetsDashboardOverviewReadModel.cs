using System.Text.Json.Serialization;

using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetsDashboardOverviewReadModel(
    TimesheetsDashboardContextReadModel Context,
    TimesheetsDashboardCurrentPeriodSummary CurrentPeriod,
    TimesheetsDashboardPendingActionsSummary PendingActions,
    TimesheetsDashboardApprovalWorkloadSummary ApprovalWorkload,
    IReadOnlyList<TimesheetsDashboardShortcutReadModel> ReportShortcuts,
    TimesheetsDashboardLedgerReadinessSummary LedgerReadiness,
    IReadOnlyList<TimesheetsDashboardProjectionStatusReadModel> ProjectionStatuses,
    TimesheetsDashboardShortcutReadModel EmptyStateAction);

public sealed record TimesheetsDashboardContextReadModel(
    string? TenantLocalPeriodKey,
    DateOnly? ServiceDateFrom,
    DateOnly? ServiceDateTo,
    ProjectReference? Project,
    WorkReference? Work,
    ActivityTypeId? ActivityTypeId,
    BillableState? BillableState);

public sealed record TimesheetsDashboardCurrentPeriodSummary(
    TimesheetsDashboardCurrentPeriodState State,
    int EntryCount,
    int DraftEntryCount,
    int SubmittedEntryCount,
    int ApprovedEntryCount,
    int RejectedEntryCount,
    ProjectionFreshnessMetadata ProjectionFreshness,
    string Message);

public sealed record TimesheetsDashboardPendingActionsSummary(
    int PendingSubmissionCount,
    int PendingCorrectionCount,
    TimesheetsDashboardShortcutReadModel PrimaryAction,
    ProjectionFreshnessMetadata ProjectionFreshness,
    string Message);

public sealed record TimesheetsDashboardApprovalWorkloadSummary(
    TimesheetsDashboardAuthorityState AuthorityState,
    TimesheetsDashboardShortcutVisibility Visibility,
    int SubmittedEntryCount,
    ProjectionFreshnessMetadata ProjectionFreshness,
    string Message,
    TimesheetsDashboardShortcutReadModel? Shortcut);

public sealed record TimesheetsDashboardLedgerReadinessSummary(
    TimesheetsDashboardShortcutVisibility LedgerVisibility,
    TimesheetsDashboardShortcutVisibility ExportVisibility,
    ApprovedTimeExportReadinessState ExportReadiness,
    int DisclosedApprovedRowCount,
    ProjectionFreshnessMetadata ProjectionFreshness,
    string Message,
    TimesheetsDashboardShortcutReadModel? LedgerShortcut,
    TimesheetsDashboardShortcutReadModel? ExportShortcut);

public sealed record TimesheetsDashboardShortcutReadModel(
    string Name,
    string Label,
    string Intent,
    TimesheetsDashboardShortcutVisibility Visibility,
    TimesheetsDashboardShortcutState State,
    string Message,
    TimesheetsDashboardContextReadModel PreservedContext);

public sealed record TimesheetsDashboardProjectionStatusReadModel(
    string Name,
    ProjectionFreshnessMetadata Freshness,
    TimesheetsDashboardProjectionDecisionAuthority DecisionAuthority,
    string Message);

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsDashboardCurrentPeriodState>))]
public enum TimesheetsDashboardCurrentPeriodState
{
    Unknown = 0,
    NoEntries = 1,
    Draft = 2,
    Submitted = 3,
    Approved = 4,
    NeedsCorrection = 5,
    Mixed = 6
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsDashboardAuthorityState>))]
public enum TimesheetsDashboardAuthorityState
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    Unresolved = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsDashboardShortcutVisibility>))]
public enum TimesheetsDashboardShortcutVisibility
{
    Unknown = 0,
    Visible = 1,
    Disabled = 2,
    Hidden = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsDashboardShortcutState>))]
public enum TimesheetsDashboardShortcutState
{
    Unknown = 0,
    Ready = 1,
    DisabledByPolicy = 2,
    BlockedByFreshness = 3,
    Empty = 4,
    Unavailable = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsDashboardProjectionDecisionAuthority>))]
public enum TimesheetsDashboardProjectionDecisionAuthority
{
    Unknown = 0,
    FreshDecisionAuthority = 1,
    StatusOnly = 2,
    Unavailable = 3
}
