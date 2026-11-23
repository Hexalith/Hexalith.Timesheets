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
    string? BlockedReason)
{
    /// <summary>Gets the SHA-256 of the ordered, redacted export rows serialized with web JSON options.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputRowsHashSha256 { get; init; }

    /// <summary>Gets the stable export request identity. When omitted, the correlation identifies the request.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; init; }

    public TenantReference? Tenant { get; init; }

    public string? OutputContentHashSha256 { get; init; }
}
