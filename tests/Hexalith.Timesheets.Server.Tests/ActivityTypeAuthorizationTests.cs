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

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");
}
