using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeExportAuditMetadata(
    PartyReference? Requester,
    QueryApprovedTimeLedger Filters,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? GeneratedAtUtc,
    string CorrelationId,
    ApprovedTimeExportScope OutputScope,
    ApprovedTimeExportFormat Format,
    string FormatVersion,
    ProjectionFreshnessState FreshnessState,
    int RowCount,
    string? BlockedReason);
