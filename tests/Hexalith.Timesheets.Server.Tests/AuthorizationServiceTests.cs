using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class AuthorizationServiceTests
{
    [Fact]
    public void Operation_vocabulary_covers_trust_boundaries_and_ui_visibility()
    {
        Enum.GetNames<TimesheetsOperation>().ShouldBe([
            "Unknown",
            "Command",
            "Query",
            "ProjectionRead",
            "Export",
            "Confirmation",
            "UiActionVisibility"
        ]);
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.MissingTenant)]
    [InlineData(TimesheetsDenialCategory.DisabledTenant)]
    [InlineData(TimesheetsDenialCategory.UnknownUser)]
    [InlineData(TimesheetsDenialCategory.NonMember)]
    [InlineData(TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(TimesheetsDenialCategory.UnconfiguredPolicy)]
    public void Denial_vocabulary_contains_safe_categories(TimesheetsDenialCategory category)
    {
        TimesheetsAuthorizationDecision decision = TimesheetsAuthorizationDecision.Denied(
            category,
            "Access denied for this action.");

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(category);
        decision.Reason.ShouldBe("Access denied for this action.");
    }

    [Fact]
    public async Task Access_guard_short_circuits_when_tenant_authority_fails()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.NonMember,
                "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.NonMember);
        projectValidator.ReceivedCalls().ShouldBeEmpty();
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TimesheetsTenantAccessState.MissingTenant, TimesheetsDenialCategory.MissingTenant)]
    [InlineData(TimesheetsTenantAccessState.DisabledTenant, TimesheetsDenialCategory.DisabledTenant)]
    [InlineData(TimesheetsTenantAccessState.UnknownUser, TimesheetsDenialCategory.UnknownUser)]
    [InlineData(TimesheetsTenantAccessState.NonMember, TimesheetsDenialCategory.NonMember)]
    [InlineData(TimesheetsTenantAccessState.InsufficientRole, TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(TimesheetsTenantAccessState.StaleProjection, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsTenantAccessState.AmbiguousAuthority, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsTenantAccessState.UnavailableSiblingAuthority, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(TimesheetsTenantAccessState.UnconfiguredPolicy, TimesheetsDenialCategory.UnconfiguredPolicy)]
    public async Task Access_guard_maps_tenant_failures_to_safe_denials(
        TimesheetsTenantAccessState state,
        TimesheetsDenialCategory expectedCategory)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(state, "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            Substitute.For<ITimesheetsPolicyEvaluator>());

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(expectedCategory);
        decision.Reason.ShouldBe("Authority cannot be resolved.");
    }

    [Fact]
    public async Task Access_guard_denies_cross_tenant_reference_before_policy_check()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.Unauthorized, TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.DisabledOrArchived, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Access_guard_maps_reference_failures_to_safe_denials(
        ReferenceValidationState state,
        TimesheetsDenialCategory expectedCategory)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(state, "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest() with { Work = null, Contributor = null },
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(expectedCategory);
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Access_guard_reaches_policy_only_after_tenant_and_references_are_valid()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        workValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        partyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.UnconfiguredPolicy,
                "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        policyEvaluator.ReceivedCalls().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(TimesheetsOperation.Command)]
    [InlineData(TimesheetsOperation.Query)]
    [InlineData(TimesheetsOperation.ProjectionRead)]
    [InlineData(TimesheetsOperation.Export)]
    [InlineData(TimesheetsOperation.Confirmation)]
    [InlineData(TimesheetsOperation.UiActionVisibility)]
    public async Task Access_guard_routes_each_host_operation_through_tenant_authority_and_policy(
        TimesheetsOperation operation)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), operation, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationRequest request = BasicRequest(operation) with
        {
            UiAction = operation == TimesheetsOperation.UiActionVisibility
                ? TimesheetsUiAction.Export
                : null
        };

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            request,
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeTrue();
        await tenantValidator
            .Received(1)
            .ValidateAsync(request.Context, operation, Arg.Any<CancellationToken>());
        await policyEvaluator
            .Received(1)
            .EvaluateAsync(request, Arg.Any<CancellationToken>());
        projectValidator.ReceivedCalls().ShouldBeEmpty();
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("project")]
    [InlineData("work")]
    [InlineData("party")]
    public async Task Access_guard_denies_cross_tenant_targets_for_each_resource_before_policy(
        string target)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(target == "project"
                ? ReferenceValidationResult.Denied(ReferenceValidationState.TenantMismatch, "Authority cannot be resolved.")
                : ReferenceValidationResult.Valid());
        workValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(target == "work"
                ? ReferenceValidationResult.Denied(ReferenceValidationState.TenantMismatch, "Authority cannot be resolved.")
                : ReferenceValidationResult.Valid());
        partyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(target == "party"
                ? ReferenceValidationResult.Denied(ReferenceValidationState.TenantMismatch, "Authority cannot be resolved.")
                : ReferenceValidationResult.Valid());

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();

        if (target == "project")
        {
            workValidator.ReceivedCalls().ShouldBeEmpty();
            partyValidator.ReceivedCalls().ShouldBeEmpty();
        }
        else if (target == "work")
        {
            partyValidator.ReceivedCalls().ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Access_guard_allows_command_after_tenant_references_and_policy_allow()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        workValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        partyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            CommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeTrue();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.None);
    }

    [Fact]
    public async Task Access_guard_does_not_run_trusted_work_when_authorization_fails()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.DisabledTenant,
                "Authority cannot be resolved."));

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            Substitute.For<ITimesheetsPolicyEvaluator>());

        bool trustedWorkWasInvoked = false;
        TimesheetsAuthorizationDecision decision = await guard.ExecuteIfAuthorizedAsync(
            CommandRequest(),
            _ =>
            {
                trustedWorkWasInvoked = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        trustedWorkWasInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Access_guard_runs_trusted_work_when_authorization_succeeds()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        projectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        workValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        partyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsAccessGuard guard = new(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator);

        bool trustedWorkWasInvoked = false;
        TimesheetsAuthorizationDecision decision = await guard.ExecuteIfAuthorizedAsync(
            CommandRequest(),
            _ =>
            {
                trustedWorkWasInvoked = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeTrue();
        trustedWorkWasInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Evaluate_ui_action_returns_allowed_outcome_when_policy_allows()
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Authorized(),
            TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsUiActionPolicyOutcome outcome = await guard.EvaluateUiActionAsync(
            UiActionRequest(),
            TimesheetsUiActionVisibility.Hidden,
            TestContext.Current.CancellationToken);

        outcome.Action.ShouldBe(TimesheetsUiAction.Export);
        outcome.Visibility.ShouldBe(TimesheetsUiActionVisibility.Allowed);
    }

    [Fact]
    public async Task Evaluate_ui_action_maps_unresolved_authority_to_authority_unresolved_copy()
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.UnconfiguredPolicy,
                "Authority cannot be resolved."),
            TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsUiActionPolicyOutcome outcome = await guard.EvaluateUiActionAsync(
            UiActionRequest(),
            TimesheetsUiActionVisibility.Hidden,
            TestContext.Current.CancellationToken);

        outcome.Visibility.ShouldBe(TimesheetsUiActionVisibility.Hidden);
        outcome.SafeMessage.ShouldBe("Authority cannot be resolved.");
    }

    [Fact]
    public async Task Evaluate_ui_action_maps_access_denial_to_denied_copy()
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.NonMember,
                "Authority cannot be resolved."),
            TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsUiActionPolicyOutcome outcome = await guard.EvaluateUiActionAsync(
            UiActionRequest(),
            TimesheetsUiActionVisibility.Disabled,
            TestContext.Current.CancellationToken);

        outcome.Visibility.ShouldBe(TimesheetsUiActionVisibility.Disabled);
        outcome.SafeMessage.ShouldBe("Access denied for this action.");
    }

    [Theory]
    [InlineData(TimesheetsOperation.Command)]
    [InlineData(TimesheetsOperation.Export)]
    public async Task Evaluate_ui_action_requires_the_ui_action_visibility_operation(TimesheetsOperation operation)
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Authorized(),
            TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsAuthorizationRequest request = UiActionRequest() with { Operation = operation };

        await Should.ThrowAsync<ArgumentException>(async () => await guard.EvaluateUiActionAsync(
            request,
            TimesheetsUiActionVisibility.Hidden,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Evaluate_ui_action_requires_a_ui_action()
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Authorized(),
            TimesheetsPolicyEvaluationResult.Allowed());

        TimesheetsAuthorizationRequest request = UiActionRequest() with { UiAction = null };

        await Should.ThrowAsync<ArgumentException>(async () => await guard.EvaluateUiActionAsync(
            request,
            TimesheetsUiActionVisibility.Hidden,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Evaluate_ui_action_rejects_allowed_denied_visibility()
    {
        TimesheetsAccessGuard guard = UiActionGuard(
            TimesheetsTenantAccessResult.Authorized(),
            TimesheetsPolicyEvaluationResult.Allowed());

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await guard.EvaluateUiActionAsync(
            UiActionRequest(),
            TimesheetsUiActionVisibility.Allowed,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Ui_action_policy_outcomes_use_safe_copy_and_hide_disable_semantics()
    {
        TimesheetsUiActionPolicyOutcome denied = TimesheetsUiActionPolicyOutcome.Denied(
            TimesheetsUiAction.Export,
            TimesheetsUiActionVisibility.Disabled);

        denied.Action.ShouldBe(TimesheetsUiAction.Export);
        denied.Visibility.ShouldBe(TimesheetsUiActionVisibility.Disabled);
        denied.SafeMessage.ShouldBe("Access denied for this action.");
        denied.SafeMessage.ShouldNotContain("project", Case.Insensitive);
        denied.SafeMessage.ShouldNotContain("party", Case.Insensitive);
        denied.SafeMessage.ShouldNotContain("tenant", Case.Insensitive);
    }

    private static TimesheetsAuthorizationRequest CommandRequest()
    {
        return new TimesheetsAuthorizationRequest(
            new TimesheetsRequestContext(
                new TenantReference("tenant_01"),
                new PartyReference("party_01"),
                "correlation_01"),
            TimesheetsOperation.Command)
        {
            Project = new ProjectReference("project_01"),
            Work = new WorkReference("work_01"),
            Contributor = new PartyReference("party_02")
        };
    }

    private static TimesheetsAuthorizationRequest BasicRequest(TimesheetsOperation operation)
    {
        return new TimesheetsAuthorizationRequest(
            new TimesheetsRequestContext(
                new TenantReference("tenant_01"),
                new PartyReference("party_01"),
                "correlation_01"),
            operation);
    }

    private static TimesheetsAuthorizationRequest UiActionRequest()
    {
        return BasicRequest(TimesheetsOperation.UiActionVisibility) with
        {
            UiAction = TimesheetsUiAction.Export
        };
    }

    private static TimesheetsAccessGuard UiActionGuard(
        TimesheetsTenantAccessResult tenantAccess,
        TimesheetsPolicyEvaluationResult policyResult)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.UiActionVisibility, Arg.Any<CancellationToken>())
            .Returns(tenantAccess);

        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(policyResult);

        return new TimesheetsAccessGuard(
            tenantValidator,
            Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            policyEvaluator);
    }
}
