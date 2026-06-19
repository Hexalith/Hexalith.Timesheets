using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ActivityTypeAuthorizationTests
{
    [Theory]
    [InlineData(TimesheetsTenantAccessState.MissingTenant)]
    [InlineData(TimesheetsTenantAccessState.NonMember)]
    [InlineData(TimesheetsTenantAccessState.StaleProjection)]
    [InlineData(TimesheetsTenantAccessState.AmbiguousAuthority)]
    [InlineData(TimesheetsTenantAccessState.UnavailableSiblingAuthority)]
    public async Task Tenant_activity_type_write_fails_closed_before_domain_decision(
        TimesheetsTenantAccessState accessState)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(accessState, "Authority cannot be resolved."));

        TenantActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateTenantActivityType(new ActivityTypeId("activity-type-1"), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        projectValidator.ReceivedCalls().ShouldBeEmpty();
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Tenant_activity_type_write_uses_tenant_then_policy_without_project_or_work_validation()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        TenantActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateTenantActivityType(new ActivityTypeId("activity-type-1"), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<ActivityTypeCreated>();
        projectValidator.ReceivedCalls().ShouldBeEmpty();
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Tenant_activity_type_write_policy_denial_fails_before_domain_dispatch()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.InsufficientRole,
                "Operator lacks Activity Type catalog permission."));

        TenantActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateTenantActivityType(new ActivityTypeId("activity-type-1"), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
    }

    [Fact]
    public async Task Tenant_activity_type_catalog_read_requires_projection_read_authority()
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.ProjectionRead, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        TenantActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(),
            Substitute.For<IContributorPartyValidator>(),
            policyEvaluator));

        TimesheetsAuthorizationDecision decision = await service.AuthorizeCatalogReadAsync(
            Context(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeTrue();
        await tenantValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.ProjectionRead, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.DisabledOrArchived, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Project_activity_type_write_fails_closed_before_domain_dispatch_when_project_authority_is_not_valid(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
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
            .Returns(ReferenceValidationResult.Denied(referenceState, "Project authority cannot be resolved."));

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateProjectActivityType(new ActivityTypeId("activity-type-1"), Project(), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await projectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TimesheetsTenantAccessState.MissingTenant, TimesheetsDenialCategory.MissingTenant)]
    [InlineData(TimesheetsTenantAccessState.NonMember, TimesheetsDenialCategory.NonMember)]
    [InlineData(TimesheetsTenantAccessState.StaleProjection, TimesheetsDenialCategory.StaleProjection)]
    public async Task Project_activity_type_write_fails_closed_on_tenant_authority_before_project_validation(
        TimesheetsTenantAccessState accessState,
        TimesheetsDenialCategory expectedCategory)
    {
        ITimesheetsTenantAccessValidator tenantValidator = Substitute.For<ITimesheetsTenantAccessValidator>();
        IProjectReferenceValidator projectValidator = Substitute.For<IProjectReferenceValidator>();
        IWorkReferenceValidator workValidator = Substitute.For<IWorkReferenceValidator>();
        IContributorPartyValidator partyValidator = Substitute.For<IContributorPartyValidator>();
        ITimesheetsPolicyEvaluator policyEvaluator = Substitute.For<ITimesheetsPolicyEvaluator>();

        tenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(accessState, "Authority cannot be resolved."));

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateProjectActivityType(new ActivityTypeId("activity-type-1"), Project(), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        projectValidator.ReceivedCalls().ShouldBeEmpty();
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_activity_type_write_uses_tenant_then_project_then_policy_without_work_or_contributor_validation()
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
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await service.CreateAsync(
            Context(),
            new CreateProjectActivityType(new ActivityTypeId("activity-type-1"), Project(), "Discovery", BillableState.Billable),
            null,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<ActivityTypeCreated>();
        await projectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        await policyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request => request != null && Project().Equals(request.Project)),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("create")]
    [InlineData("rename")]
    [InlineData("update-metadata")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    [InlineData("configure-restriction")]
    public async Task Project_activity_type_catalog_commands_validate_project_before_domain_dispatch(
        string command)
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
            .Returns(ReferenceValidationResult.Denied(ReferenceValidationState.InvalidReference, "Project reference is invalid."));

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await ExecuteProjectCommandAsync(
            service,
            command,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        await projectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        policyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("rename")]
    [InlineData("update-metadata")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    [InlineData("configure-restriction")]
    public async Task Project_activity_type_catalog_commands_dispatch_only_after_tenant_project_and_policy_allow(
        string command)
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
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await ExecuteProjectCommandAsync(
            service,
            command,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsSuccess.ShouldBeTrue();
        await projectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        await policyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request => request != null && Project().Equals(request.Project)),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("create")]
    [InlineData("rename")]
    [InlineData("update-metadata")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    [InlineData("configure-restriction")]
    public async Task Project_activity_type_catalog_commands_stop_on_policy_denial_before_domain_dispatch(
        string command)
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
        policyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.InsufficientRole,
                "Operator lacks project Activity Type catalog permission."));

        ProjectActivityTypeCommandService service = new(new TimesheetsAccessGuard(
            tenantValidator,
            projectValidator,
            workValidator,
            partyValidator,
            policyEvaluator));

        ActivityTypeCommandResult result = await ExecuteProjectCommandAsync(
            service,
            command,
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        await projectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workValidator.ReceivedCalls().ShouldBeEmpty();
        partyValidator.ReceivedCalls().ShouldBeEmpty();
        await policyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request => request != null && Project().Equals(request.Project)),
                Arg.Any<CancellationToken>());
    }

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static async ValueTask<ActivityTypeCommandResult> ExecuteProjectCommandAsync(
        ProjectActivityTypeCommandService service,
        string command,
        CancellationToken cancellationToken)
        => command switch
        {
            "create" => await service.CreateAsync(
                Context(),
                new CreateProjectActivityType(ActivityId(), Project(), "Discovery", BillableState.Billable),
                null,
                cancellationToken).ConfigureAwait(false),
            "rename" => await service.RenameAsync(
                Context(),
                new RenameProjectActivityType(ActivityId(), Project(), "Renamed discovery"),
                ProjectState(),
                cancellationToken).ConfigureAwait(false),
            "update-metadata" => await service.UpdateMetadataAsync(
                Context(),
                new UpdateProjectActivityTypeMetadata(ActivityId(), Project(), BillableState.NonBillable),
                ProjectState(),
                cancellationToken).ConfigureAwait(false),
            "deactivate" => await service.DeactivateAsync(
                Context(),
                new DeactivateProjectActivityType(ActivityId(), Project()),
                ProjectState(),
                cancellationToken).ConfigureAwait(false),
            "reactivate" => await service.ReactivateAsync(
                Context(),
                new ReactivateProjectActivityType(ActivityId(), Project()),
                DeactivatedProjectState(),
                cancellationToken).ConfigureAwait(false),
            "configure-restriction" => await service.ConfigureRestrictionAsync(
                Context(),
                new ConfigureProjectActivityTypeCatalogRestriction(
                    Project(),
                    true,
                    [new ActivityTypeId("tenant-activity-type-1")],
                    [ActivityId()]),
                cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported project Activity Type command.")
        };

    private static ActivityTypeCatalogState ProjectState()
    {
        ActivityTypeCatalogState state = new();
        state.Apply(new ActivityTypeCreated(
            ActivityId(),
            ActivityTypeScope.Project,
            Project(),
            "Discovery",
            BillableState.Billable));
        return state;
    }

    private static ActivityTypeCatalogState DeactivatedProjectState()
    {
        ActivityTypeCatalogState state = ProjectState();
        state.Apply(new ActivityTypeDeactivated(ActivityId()));
        return state;
    }
}
