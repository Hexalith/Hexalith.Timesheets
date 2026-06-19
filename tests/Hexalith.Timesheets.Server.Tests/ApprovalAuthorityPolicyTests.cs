using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ApprovalAuthorityPolicyTests
{
    [Theory]
    [InlineData(ApprovalAuthoritySource.ProjectApprover)]
    [InlineData(ApprovalAuthoritySource.WorkOwner)]
    [InlineData(ApprovalAuthoritySource.TenantAdministrator)]
    [InlineData(ApprovalAuthoritySource.FinanceReviewer)]
    public async Task Resolver_allows_when_configured_source_authorizes_action(ApprovalAuthoritySource source)
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(source, ApprovalAuthoritySourceResult.Allowed(source, ProjectionFreshnessMetadata.Fresh)));

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ActionForSource(source)),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.SourceAttribution.Source.ShouldBe(source);
        result.SourceAttribution.DecisionState.ShouldBe(ApprovalAuthorityDecisionState.Allowed);
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.None);
    }

    [Fact]
    public async Task Resolver_applies_source_precedence_before_tenant_governance()
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(
                ApprovalAuthoritySource.TenantAdministrator,
                ApprovalAuthoritySourceResult.Allowed(
                    ApprovalAuthoritySource.TenantAdministrator,
                    ProjectionFreshnessMetadata.Fresh)),
            Provider(
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthoritySourceResult.Denied(
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Denied,
                    TimesheetsDenialCategory.InsufficientRole,
                    "Access denied for this action.",
                    ProjectionFreshnessMetadata.Fresh)));

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
    }

    [Fact]
    public async Task Resolver_denies_same_precedence_contradiction_as_ambiguous_authority()
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthoritySourceResult.Allowed(
                    ApprovalAuthoritySource.ProjectApprover,
                    ProjectionFreshnessMetadata.Fresh),
                precedence: 20),
            Provider(
                ApprovalAuthoritySource.WorkOwner,
                ApprovalAuthoritySourceResult.Denied(
                    ApprovalAuthoritySource.WorkOwner,
                    ApprovalAuthorityDecisionState.Denied,
                    TimesheetsDenialCategory.InsufficientRole,
                    "Access denied for this action.",
                    ProjectionFreshnessMetadata.Fresh),
                precedence: 20));

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.AmbiguousAuthority);
        result.Reason.ShouldBe("Authority cannot be resolved.");
        result.SourceAttribution.DecisionState.ShouldBe(ApprovalAuthorityDecisionState.Ambiguous);
    }

    [Fact]
    public async Task Resolver_denies_self_approval_by_default_even_when_governance_source_allows()
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(
                ApprovalAuthoritySource.TenantAdministrator,
                ApprovalAuthoritySourceResult.Allowed(
                    ApprovalAuthoritySource.TenantAdministrator,
                    ProjectionFreshnessMetadata.Fresh)));

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(
                ApprovalAuthorityAction.EntryApproval,
                actor: new PartyReference("party_01"),
                contributor: new PartyReference("party_01")),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.SelfApprovalPolicy);
    }

    [Fact]
    public async Task Resolver_allows_self_approval_only_for_explicit_policy_action()
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(
                ApprovalAuthoritySource.TenantAdministrator,
                ApprovalAuthoritySourceResult.Allowed(
                    ApprovalAuthoritySource.TenantAdministrator,
                    ProjectionFreshnessMetadata.Fresh)),
            options: new TimesheetsApprovalAuthorityPolicyOptions
            {
                SelfApprovalAllowedActions = new HashSet<ApprovalAuthorityAction>
                {
                    ApprovalAuthorityAction.PeriodApproval
                }
            });

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(
                ApprovalAuthorityAction.PeriodApproval,
                actor: new PartyReference("party_01"),
                contributor: new PartyReference("party_01")),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.SelfApprovalPolicy);
        result.SourceAttribution.Action.ShouldBe(ApprovalAuthorityAction.PeriodApproval);
    }

    [Theory]
    [InlineData(ApprovalAuthorityDecisionState.MissingActor, TimesheetsDenialCategory.UnknownUser)]
    [InlineData(ApprovalAuthorityDecisionState.DisabledTenant, TimesheetsDenialCategory.DisabledTenant)]
    [InlineData(ApprovalAuthorityDecisionState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ApprovalAuthorityDecisionState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ApprovalAuthorityDecisionState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    [InlineData(ApprovalAuthorityDecisionState.CrossTenantTarget, TimesheetsDenialCategory.CrossTenantTarget)]
    public async Task Resolver_fails_closed_for_untrusted_source_states(
        ApprovalAuthorityDecisionState state,
        TimesheetsDenialCategory denialCategory)
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver(
            Provider(
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthoritySourceResult.Denied(
                    ApprovalAuthoritySource.ProjectApprover,
                    state,
                    denialCategory,
                    "Authority cannot be resolved.",
                    ProjectionFreshnessMetadata.Unavailable())));

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(denialCategory);
        result.Reason.ShouldBe("Authority cannot be resolved.");
        result.Reason.ShouldNotContain("party", Case.Insensitive);
        result.Reason.ShouldNotContain("project", Case.Insensitive);
        result.Reason.ShouldNotContain("tenant", Case.Insensitive);
    }

    [Fact]
    public async Task Resolver_fails_closed_when_no_source_can_resolve_authority()
    {
        TimesheetsApprovalAuthorityResolver resolver = Resolver();

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.DefaultDeny);
        result.Reason.ShouldBe("Authority cannot be resolved.");
    }

    [Fact]
    public async Task Resolver_runs_base_access_guard_before_authority_sources()
    {
        TrackingAuthorityProvider provider = new(
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthoritySourceResult.Allowed(
                ApprovalAuthoritySource.ProjectApprover,
                ProjectionFreshnessMetadata.Fresh));
        DenyingAccessGuard accessGuard = new();
        TimesheetsApprovalAuthorityResolver resolver = new(
            TimesheetsApprovalAuthorityPolicyOptions.Default,
            [provider],
            accessGuard);

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.StaleProjection);
        provider.WasEvaluated.ShouldBeFalse();
    }

    [Fact]
    public void Result_copy_does_not_expose_protected_authority_material()
    {
        ApprovalAuthorityResolutionResult result = ApprovalAuthorityResolutionResult.Denied(
            TimesheetsDenialCategory.AmbiguousAuthority,
            "Authority cannot be resolved.",
            new ApprovalAuthoritySourceAttribution(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Ambiguous,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Stale()));

        result.Reason.ShouldNotContain("role", Case.Insensitive);
        result.Reason.ShouldNotContain("token", Case.Insensitive);
        result.Reason.ShouldNotContain("party", Case.Insensitive);
        result.SourceAttribution.PolicyKey.ShouldBe("timesheets.approval-authority.v1");
    }

    private static ApprovalAuthorityAction ActionForSource(ApprovalAuthoritySource source)
    {
        return source == ApprovalAuthoritySource.FinanceReviewer
            ? ApprovalAuthorityAction.ApprovedTimeExportEligibility
            : ApprovalAuthorityAction.EntryApproval;
    }

    private static TimesheetsApprovalAuthorityResolver Resolver(
        params IApprovalAuthoritySourceProvider[] providers)
        => Resolver(providers, TimesheetsApprovalAuthorityPolicyOptions.Default);

    private static TimesheetsApprovalAuthorityResolver Resolver(
        IApprovalAuthoritySourceProvider[] providers,
        TimesheetsApprovalAuthorityPolicyOptions options)
        => new(options, providers);

    private static TimesheetsApprovalAuthorityResolver Resolver(
        IApprovalAuthoritySourceProvider provider,
        TimesheetsApprovalAuthorityPolicyOptions? options = null)
        => new(options ?? TimesheetsApprovalAuthorityPolicyOptions.Default, [provider]);

    private static ApprovalAuthorityResolutionRequest Request(
        ApprovalAuthorityAction action,
        PartyReference? actor = null,
        PartyReference? contributor = null)
    {
        actor ??= new PartyReference("party_approver");
        contributor ??= new PartyReference("party_contributor");

        return new(
            new TimesheetsAuthorizationRequest(
                new TimesheetsRequestContext(
                    new TenantReference("tenant_01"),
                    actor,
                    "correlation_01"),
                TimesheetsOperation.Command)
            {
                Contributor = contributor
            },
            action,
            contributor);
    }

    private static TestAuthorityProvider Provider(
        ApprovalAuthoritySource source,
        ApprovalAuthoritySourceResult result,
        int? precedence = null)
        => new(source, precedence ?? TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(source), result);

    private sealed class TestAuthorityProvider(
        ApprovalAuthoritySource source,
        int precedence,
        ApprovalAuthoritySourceResult result)
        : IApprovalAuthoritySourceProvider
    {
        public ApprovalAuthoritySource Source { get; } = source;

        public int Precedence { get; } = precedence;

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TrackingAuthorityProvider(
        ApprovalAuthoritySource source,
        ApprovalAuthoritySourceResult result)
        : IApprovalAuthoritySourceProvider
    {
        public ApprovalAuthoritySource Source { get; } = source;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public bool WasEvaluated { get; private set; }

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            WasEvaluated = true;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DenyingAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(
                TimesheetsDenialCategory.StaleProjection,
                "Authority cannot be resolved."));
        }

        public ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            return AuthorizeAsync(request, cancellationToken);
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.AuthorityUnresolved(
                request.UiAction ?? TimesheetsUiAction.Approval,
                deniedVisibility));
        }
    }
}
