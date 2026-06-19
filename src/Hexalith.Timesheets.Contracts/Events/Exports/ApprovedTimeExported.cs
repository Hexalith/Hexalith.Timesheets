using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.Exports;

public sealed record ApprovedTimeExported(
    PartyReference? Requester,
    TenantReference Tenant,
    QueryApprovedTimeLedger Filters,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset GeneratedAtUtc,
    string CorrelationId,
    ApprovedTimeExportScope OutputScope,
    ApprovedTimeExportFormat Format,
    string FormatVersion,
    ProjectionFreshnessState FreshnessState,
    int RowCount,
    string OutputContentHashSha256);
