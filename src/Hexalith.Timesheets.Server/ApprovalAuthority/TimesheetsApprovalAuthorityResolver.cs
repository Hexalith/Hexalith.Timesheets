using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed class TimesheetsApprovalAuthorityResolver : ITimesheetsApprovalAuthorityResolver
{
    private const string AccessDenied = "Access denied for this action.";
    private const string AuthorityUnresolved = "Authority cannot be resolved.";

    private readonly ITimesheetsAccessGuard? _accessGuard;
    private readonly TimesheetsApprovalAuthorityPolicyOptions _options;
    private readonly IReadOnlyList<IApprovalAuthoritySourceProvider> _providers;

    public TimesheetsApprovalAuthorityResolver(
        TimesheetsApprovalAuthorityPolicyOptions options,
        IEnumerable<IApprovalAuthoritySourceProvider> providers)
        : this(options, providers, null)
    {
    }

    public TimesheetsApprovalAuthorityResolver(
        TimesheetsApprovalAuthorityPolicyOptions options,
        IEnumerable<IApprovalAuthoritySourceProvider> providers,
        ITimesheetsAccessGuard? accessGuard)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providers);

        _options = options;
        _providers = providers.OrderBy(static provider => provider.Precedence).ToArray();
        _accessGuard = accessGuard;
    }

    public async ValueTask<ApprovalAuthorityResolutionResult> ResolveAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Action == ApprovalAuthorityAction.Unknown)
        {
            return Denied(
                request.Action,
                ApprovalAuthoritySource.DefaultDeny,
                ApprovalAuthorityDecisionState.Unavailable,
                TimesheetsDenialCategory.UnconfiguredPolicy,
                AuthorityUnresolved,
                ProjectionFreshnessMetadata.Unavailable());
        }

        if (_accessGuard is not null)
        {
            TimesheetsAuthorizationDecision baseDecision = await _accessGuard
                .AuthorizeAsync(
                    request.AuthorizationRequest with { ApprovalAction = request.Action },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!baseDecision.IsAuthorized)
            {
                return Denied(
                    request.Action,
                    ApprovalAuthoritySource.DefaultDeny,
                    MapDecisionState(baseDecision.DenialCategory),
                    baseDecision.DenialCategory,
                    SafeReason(baseDecision.DenialCategory),
                    ProjectionFreshnessMetadata.Unavailable());
            }
        }

        PartyReference? actor = request.AuthorizationRequest.Context.Actor;
        if (actor is null)
        {
            return Denied(
                request.Action,
                ApprovalAuthoritySource.DefaultDeny,
                ApprovalAuthorityDecisionState.MissingActor,
                TimesheetsDenialCategory.UnknownUser,
                AuthorityUnresolved,
                ProjectionFreshnessMetadata.Unavailable());
        }

        if (IsSelfApprovalCheckRequired(request.Action, actor, request.Contributor))
        {
            if (_options.SelfApprovalAllowedActions.Contains(request.Action))
            {
                return ApprovalAuthorityResolutionResult.Allowed(
                    Attribution(
                        request.Action,
                        ApprovalAuthoritySource.SelfApprovalPolicy,
                        ApprovalAuthorityDecisionState.Allowed,
                        ProjectionFreshnessMetadata.Fresh));
            }

            return Denied(
                request.Action,
                ApprovalAuthoritySource.SelfApprovalPolicy,
                ApprovalAuthorityDecisionState.Denied,
                TimesheetsDenialCategory.InsufficientRole,
                AccessDenied,
                ProjectionFreshnessMetadata.Fresh);
        }

        if (_providers.Count == 0)
        {
            return Denied(
                request.Action,
                ApprovalAuthoritySource.DefaultDeny,
                ApprovalAuthorityDecisionState.Unavailable,
                TimesheetsDenialCategory.UnavailableSiblingAuthority,
                AuthorityUnresolved,
                ProjectionFreshnessMetadata.Unavailable());
        }

        foreach (IGrouping<int, IApprovalAuthoritySourceProvider> providerGroup in _providers.GroupBy(static provider => provider.Precedence))
        {
            List<ApprovalAuthoritySourceResult> results = [];

            foreach (IApprovalAuthoritySourceProvider provider in providerGroup)
            {
                ApprovalAuthoritySourceResult result = await provider
                    .EvaluateAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(result);
            }

            if (HasSamePrecedenceContradiction(results))
            {
                return Denied(
                    request.Action,
                    ApprovalAuthoritySource.DefaultDeny,
                    ApprovalAuthorityDecisionState.Ambiguous,
                    TimesheetsDenialCategory.AmbiguousAuthority,
                    AuthorityUnresolved,
                    ProjectionFreshnessMetadata.Stale());
            }

            ApprovalAuthoritySourceResult selected = results[0];
            return selected.IsAllowed
                ? ApprovalAuthorityResolutionResult.Allowed(
                    Attribution(
                        request.Action,
                        selected.Source,
                        ApprovalAuthorityDecisionState.Allowed,
                        selected.Freshness))
                : Denied(
                    request.Action,
                    selected.Source,
                    selected.DecisionState,
                    selected.DenialCategory,
                    SafeReason(selected.DenialCategory),
                    selected.Freshness);
        }

        return Denied(
            request.Action,
            ApprovalAuthoritySource.DefaultDeny,
            ApprovalAuthorityDecisionState.Unavailable,
            TimesheetsDenialCategory.UnavailableSiblingAuthority,
            AuthorityUnresolved,
            ProjectionFreshnessMetadata.Unavailable());
    }

    private static bool HasSamePrecedenceContradiction(IReadOnlyList<ApprovalAuthoritySourceResult> results)
    {
        return results.Count > 1
            && results.Any(static result => result.IsAllowed)
            && results.Any(static result => !result.IsAllowed);
    }

    private static bool IsSelfApprovalCheckRequired(
        ApprovalAuthorityAction action,
        PartyReference actor,
        PartyReference? contributor)
    {
        return contributor is not null
            && action is ApprovalAuthorityAction.EntryApproval or ApprovalAuthorityAction.PeriodApproval
            && string.Equals(actor.PartyId, contributor.PartyId, StringComparison.Ordinal);
    }

    private static ApprovalAuthorityDecisionState MapDecisionState(TimesheetsDenialCategory category)
    {
        return category switch
        {
            TimesheetsDenialCategory.DisabledTenant => ApprovalAuthorityDecisionState.DisabledTenant,
            TimesheetsDenialCategory.UnknownUser => ApprovalAuthorityDecisionState.MissingActor,
            TimesheetsDenialCategory.CrossTenantTarget => ApprovalAuthorityDecisionState.CrossTenantTarget,
            TimesheetsDenialCategory.StaleProjection => ApprovalAuthorityDecisionState.Stale,
            TimesheetsDenialCategory.AmbiguousAuthority => ApprovalAuthorityDecisionState.Ambiguous,
            TimesheetsDenialCategory.InvalidReference => ApprovalAuthorityDecisionState.InvalidReference,
            TimesheetsDenialCategory.UnavailableSiblingAuthority => ApprovalAuthorityDecisionState.Unavailable,
            _ => ApprovalAuthorityDecisionState.Denied
        };
    }

    private static string SafeReason(TimesheetsDenialCategory category)
    {
        return category is TimesheetsDenialCategory.NonMember
            or TimesheetsDenialCategory.InsufficientRole
            ? AccessDenied
            : AuthorityUnresolved;
    }

    private ApprovalAuthorityResolutionResult Denied(
        ApprovalAuthorityAction action,
        ApprovalAuthoritySource source,
        ApprovalAuthorityDecisionState state,
        TimesheetsDenialCategory denialCategory,
        string reason,
        ProjectionFreshnessMetadata freshness)
    {
        return ApprovalAuthorityResolutionResult.Denied(
            denialCategory,
            reason,
            Attribution(action, source, state, freshness));
    }

    private ApprovalAuthoritySourceAttribution Attribution(
        ApprovalAuthorityAction action,
        ApprovalAuthoritySource source,
        ApprovalAuthorityDecisionState state,
        ProjectionFreshnessMetadata freshness)
    {
        return new(
            action,
            source,
            state,
            _options.PolicyKey,
            _options.PolicyVersion,
            freshness);
    }
}
