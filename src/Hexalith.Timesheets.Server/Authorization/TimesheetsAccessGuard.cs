using Hexalith.Timesheets.Server.References;

namespace Hexalith.Timesheets.Server.Authorization;

public sealed class TimesheetsAccessGuard : ITimesheetsAccessGuard
{
    private readonly IContributorPartyValidator _partyValidator;
    private readonly ITimesheetsPolicyEvaluator _policyEvaluator;
    private readonly IProjectReferenceValidator _projectValidator;
    private readonly ITimesheetsTenantAccessValidator _tenantAccessValidator;
    private readonly IWorkReferenceValidator _workValidator;

    public TimesheetsAccessGuard(
        ITimesheetsTenantAccessValidator tenantAccessValidator,
        IProjectReferenceValidator projectValidator,
        IWorkReferenceValidator workValidator,
        IContributorPartyValidator partyValidator,
        ITimesheetsPolicyEvaluator policyEvaluator)
    {
        ArgumentNullException.ThrowIfNull(tenantAccessValidator);
        ArgumentNullException.ThrowIfNull(projectValidator);
        ArgumentNullException.ThrowIfNull(workValidator);
        ArgumentNullException.ThrowIfNull(partyValidator);
        ArgumentNullException.ThrowIfNull(policyEvaluator);

        _tenantAccessValidator = tenantAccessValidator;
        _projectValidator = projectValidator;
        _workValidator = workValidator;
        _partyValidator = partyValidator;
        _policyEvaluator = policyEvaluator;
    }

    public async ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);

        TimesheetsTenantAccessResult tenantAccess = await _tenantAccessValidator
            .ValidateAsync(request.Context, request.Operation, cancellationToken)
            .ConfigureAwait(false);

        if (!tenantAccess.IsAuthorized)
        {
            return TimesheetsAuthorizationDecision.Denied(
                MapTenantState(tenantAccess.State),
                tenantAccess.Reason);
        }

        TimesheetsAuthorizationDecision? referenceDecision = await ValidateReferencesAsync(
            request,
            cancellationToken).ConfigureAwait(false);

        if (referenceDecision is not null)
        {
            return referenceDecision;
        }

        TimesheetsPolicyEvaluationResult policyResult = await _policyEvaluator
            .EvaluateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return policyResult.IsAllowed
            ? TimesheetsAuthorizationDecision.Allowed()
            : TimesheetsAuthorizationDecision.Denied(policyResult.DenialCategory, policyResult.Reason);
    }

    public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
        TimesheetsAuthorizationRequest request,
        Func<CancellationToken, ValueTask> trustedWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedWork);

        TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!decision.IsAuthorized)
        {
            return decision;
        }

        await trustedWork(cancellationToken).ConfigureAwait(false);

        return decision;
    }

    public async ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
        TimesheetsAuthorizationRequest request,
        TimesheetsUiActionVisibility deniedVisibility,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Operation != TimesheetsOperation.UiActionVisibility)
        {
            throw new ArgumentException(
                "UI action evaluation requires the UiActionVisibility operation.",
                nameof(request));
        }

        if (request.UiAction is null)
        {
            throw new ArgumentException(
                "UI action evaluation requires a UI action.",
                nameof(request));
        }

        if (deniedVisibility == TimesheetsUiActionVisibility.Allowed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deniedVisibility),
                deniedVisibility,
                "Denied UI actions must be hidden or disabled.");
        }

        TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return TimesheetsUiActionPolicyOutcome.FromDecision(
            request.UiAction.Value,
            decision,
            deniedVisibility);
    }

    private async ValueTask<TimesheetsAuthorizationDecision?> ValidateReferencesAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Project is not null)
        {
            ReferenceValidationResult projectResult = await _projectValidator
                .ValidateAsync(request.Context, request.Project, cancellationToken)
                .ConfigureAwait(false);

            if (!projectResult.IsValid)
            {
                return TimesheetsAuthorizationDecision.Denied(
                    MapReferenceState(projectResult.State),
                    projectResult.Reason);
            }
        }

        if (request.Work is not null)
        {
            ReferenceValidationResult workResult = await _workValidator
                .ValidateAsync(request.Context, request.Work, cancellationToken)
                .ConfigureAwait(false);

            if (!workResult.IsValid)
            {
                return TimesheetsAuthorizationDecision.Denied(
                    MapReferenceState(workResult.State),
                    workResult.Reason);
            }
        }

        if (request.Contributor is not null)
        {
            ReferenceValidationResult partyResult = await _partyValidator
                .ValidateAsync(request.Context, request.Contributor, cancellationToken)
                .ConfigureAwait(false);

            if (!partyResult.IsValid)
            {
                return TimesheetsAuthorizationDecision.Denied(
                    MapReferenceState(partyResult.State),
                    partyResult.Reason);
            }
        }

        return null;
    }

    private static TimesheetsDenialCategory MapTenantState(TimesheetsTenantAccessState state)
    {
        return state switch
        {
            TimesheetsTenantAccessState.MissingTenant => TimesheetsDenialCategory.MissingTenant,
            TimesheetsTenantAccessState.DisabledTenant => TimesheetsDenialCategory.DisabledTenant,
            TimesheetsTenantAccessState.UnknownUser => TimesheetsDenialCategory.UnknownUser,
            TimesheetsTenantAccessState.NonMember => TimesheetsDenialCategory.NonMember,
            TimesheetsTenantAccessState.InsufficientRole => TimesheetsDenialCategory.InsufficientRole,
            TimesheetsTenantAccessState.StaleProjection => TimesheetsDenialCategory.StaleProjection,
            TimesheetsTenantAccessState.AmbiguousAuthority => TimesheetsDenialCategory.AmbiguousAuthority,
            TimesheetsTenantAccessState.UnavailableSiblingAuthority => TimesheetsDenialCategory.UnavailableSiblingAuthority,
            TimesheetsTenantAccessState.UnconfiguredPolicy => TimesheetsDenialCategory.UnconfiguredPolicy,
            TimesheetsTenantAccessState.Authorized => TimesheetsDenialCategory.None,
            _ => TimesheetsDenialCategory.UnconfiguredPolicy
        };
    }

    private static TimesheetsDenialCategory MapReferenceState(ReferenceValidationState state)
    {
        return state switch
        {
            ReferenceValidationState.Unauthorized => TimesheetsDenialCategory.InsufficientRole,
            ReferenceValidationState.TenantMismatch => TimesheetsDenialCategory.CrossTenantTarget,
            ReferenceValidationState.Stale => TimesheetsDenialCategory.StaleProjection,
            ReferenceValidationState.Ambiguous => TimesheetsDenialCategory.AmbiguousAuthority,
            ReferenceValidationState.Unavailable => TimesheetsDenialCategory.UnavailableSiblingAuthority,
            ReferenceValidationState.DisabledOrArchived => TimesheetsDenialCategory.UnavailableSiblingAuthority,
            ReferenceValidationState.InvalidReference => TimesheetsDenialCategory.InvalidReference,
            ReferenceValidationState.Valid => TimesheetsDenialCategory.None,
            _ => TimesheetsDenialCategory.UnconfiguredPolicy
        };
    }
}
