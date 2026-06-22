using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Exports;

/// <summary>
/// Outcome of a side-effect-free approved-time export preview.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ApprovedTimeExportResult"/>: terminal authorization denials return
/// <see cref="NotFoundOrDenied"/> with a null preview, while contract/readiness blocks return an
/// <see cref="Evaluated"/> <see cref="ApprovedTimeExportReadinessState.Blocked"/> preview — parity with how
/// generation returns blocked read models rather than denials.
/// </remarks>
/// <param name="Authorization">Authorization decision for the preview request.</param>
/// <param name="Preview">The disclosed preview readiness, or null when the request was not found or denied.</param>
public sealed record ApprovedTimePreviewResult(
    TimesheetsAuthorizationDecision Authorization,
    ApprovedTimeExportPreviewReadModel? Preview)
{
    /// <summary>
    /// Gets a value indicating whether a preview readiness was disclosed to the caller.
    /// </summary>
    public bool WasDisclosed => Authorization.IsAuthorized && Preview is not null;

    /// <summary>
    /// Creates a disclosed preview result for an authorized caller, whether the verdict is ready or blocked.
    /// </summary>
    /// <param name="preview">The evaluated preview readiness.</param>
    /// <returns>A disclosed preview result.</returns>
    public static ApprovedTimePreviewResult Evaluated(ApprovedTimeExportPreviewReadModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        return new(TimesheetsAuthorizationDecision.Allowed(), preview);
    }

    /// <summary>
    /// Creates a fail-closed result that discloses no preview readiness.
    /// </summary>
    /// <param name="authorization">The terminal authorization denial.</param>
    /// <returns>A not-found-or-denied preview result.</returns>
    public static ApprovedTimePreviewResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
