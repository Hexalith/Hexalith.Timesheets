using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Allows isolated local Dapr acceptance data in Development only.</summary>
internal sealed class LiveDaprFixtureAccessGuard : ITimesheetsAccessGuard, ITimesheetsTenantAccessValidator
{
    private const string TenantPrefix = "live-dapr-";

    /// <inheritdoc/>
    public async ValueTask<TimesheetsTenantAccessResult> ValidateAsync(
        TimesheetsRequestContext context,
        TimesheetsOperation operation,
        CancellationToken cancellationToken)
    {
        TimesheetsAuthorizationDecision decision = await AuthorizeAsync(new(context, operation), cancellationToken)
            .ConfigureAwait(false);
        return decision.IsAuthorized
            ? TimesheetsTenantAccessResult.Authorized()
            : TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.NonMember, decision.Reason);
    }

    /// <inheritdoc/>
    public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        bool isolated = request.Context.Tenant?.TenantId.StartsWith(TenantPrefix, StringComparison.Ordinal) == true
            && !string.IsNullOrWhiteSpace(request.Context.Actor?.PartyId)
            && request.Operation is TimesheetsOperation.Command
                or TimesheetsOperation.MagicLinkIssuance
                or TimesheetsOperation.MagicLinkDisclosure
                or TimesheetsOperation.Confirmation;
        return ValueTask.FromResult(isolated
            ? TimesheetsAuthorizationDecision.Allowed()
            : TimesheetsAuthorizationDecision.Denied("Local fixture identity is unavailable."));
    }

    /// <inheritdoc/>
    public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
        TimesheetsAuthorizationRequest request,
        Func<CancellationToken, ValueTask> trustedWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedWork);
        TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision.IsAuthorized)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
        }

        return decision;
    }

    /// <inheritdoc/>
    public async ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
        TimesheetsAuthorizationRequest request,
        TimesheetsUiActionVisibility deniedVisibility,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Operation != TimesheetsOperation.UiActionVisibility)
        {
            throw new ArgumentException("UI action evaluation requires the UiActionVisibility operation.", nameof(request));
        }

        if (request.UiAction is null)
        {
            throw new ArgumentException("UI action evaluation requires a UI action.", nameof(request));
        }

        if (deniedVisibility == TimesheetsUiActionVisibility.Allowed)
        {
            throw new ArgumentOutOfRangeException(nameof(deniedVisibility), deniedVisibility,
                "Denied UI actions must be hidden or disabled.");
        }

        TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        return TimesheetsUiActionPolicyOutcome.FromDecision(
            request.UiAction.Value,
            decision,
            deniedVisibility);
    }
}
