using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodSummaryReadModel(
    TimesheetPeriodId TimesheetPeriodId,
    TenantReference Tenant,
    PartyReference Contributor,
    PartyReference Submitter,
    DateTimeOffset SubmittedAtUtc,
    TimesheetPeriodKind PeriodKind,
    string PeriodKey,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TenantTimeZoneId,
    IReadOnlyList<TimeEntryId> IncludedTimeEntryIds,
    TimesheetPeriodApprovalState PeriodState,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> BlockingGuidance { get; init; } = [];

    public IReadOnlyList<TimesheetPeriodEntrySummary> EntrySummaries { get; init; } = [];

    public IReadOnlyList<TimeEntryId> IncompleteEntryEvidenceIds { get; init; } = [];

    public TimesheetPeriodApprovalDecisionEvidence? PeriodDecision { get; init; }

    public IReadOnlyList<TimeEntryId> AffectedEntryIds { get; init; } = [];
}
