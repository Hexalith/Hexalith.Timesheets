using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>
/// Rows-free readiness result returned by a dedicated, side-effect-free approved-time export preview.
/// </summary>
/// <remarks>
/// A preview is a dry run: it shares one readiness/disclosure evaluation path with export generation but
/// produces no CSV file and emits no <c>ApprovedTimeExported</c> audit evidence. The model deliberately carries
/// neither export evidence rows nor CSV content, so a preview can never be mistaken for, substituted for, or used
/// to leak a generated export.
/// </remarks>
/// <param name="Readiness">Whether the disclosed scope is ready for export or blocked.</param>
/// <param name="ReadinessDetail">Human-readable explanation of the readiness verdict, reusing export-generation readiness vocabulary.</param>
/// <param name="Scope">Deterministic output scope: selected filters, row count, and row-lineage options.</param>
/// <param name="CommentExportPolicy">Effective export comment-policy decision applied to the previewed scope.</param>
/// <param name="ProjectionFreshness">Projection freshness backing the readiness verdict.</param>
/// <param name="Audit">Audit metadata describing the preview request; <see cref="ApprovedTimeExportAuditMetadata.GeneratedAtUtc"/> and content hash are always null because nothing is generated.</param>
/// <param name="Format">Export format the preview was evaluated against.</param>
/// <param name="FormatVersion">Export format version the preview was evaluated against.</param>
public sealed record ApprovedTimeExportPreviewReadModel(
    ApprovedTimeExportReadinessState Readiness,
    string ReadinessDetail,
    ApprovedTimeExportScope Scope,
    TimesheetsCommentPolicyDecision CommentExportPolicy,
    ProjectionFreshnessMetadata ProjectionFreshness,
    ApprovedTimeExportAuditMetadata Audit,
    ApprovedTimeExportFormat Format,
    string FormatVersion)
{
    /// <summary>
    /// Gets a value indicating whether the previewed scope is ready for export generation.
    /// </summary>
    public bool IsReady => Readiness == ApprovedTimeExportReadinessState.Ready;
}
