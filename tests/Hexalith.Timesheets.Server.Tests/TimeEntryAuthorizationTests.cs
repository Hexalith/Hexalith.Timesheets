using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryAuthorizationTests
{
    [Theory]
    [InlineData(TimesheetsTenantAccessState.MissingTenant, TimesheetsDenialCategory.MissingTenant)]
    [InlineData(TimesheetsTenantAccessState.NonMember, TimesheetsDenialCategory.NonMember)]
    [InlineData(TimesheetsTenantAccessState.StaleProjection, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsTenantAccessState.AmbiguousAuthority, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsTenantAccessState.UnavailableSiblingAuthority, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    public async Task Capture_fails_closed_on_tenant_authority_before_target_contributor_policy_or_domain_dispatch(
        TimesheetsTenantAccessState accessState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(accessState, "Authority cannot be resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.DisabledOrArchived, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Project_targeted_capture_fails_closed_before_domain_dispatch_when_project_authority_is_invalid(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Project authority cannot be resolved."));

        TimeEntryCommandService service = fixture.CreateService();

        TimeEntryCommandResult result = await service.RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.PartyValidator.Received(0)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.Unauthorized, TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Capture_fails_closed_on_contributor_authority_before_policy_or_domain_dispatch(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Contributor authority cannot be resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_targeted_capture_validates_tenant_project_contributor_policy_then_dispatches()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimeEntryRecorded>();
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.PolicyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request =>
                    request != null
                    && request.Project == Project()
                    && request.Work == null
                    && request.Contributor == Contributor()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_policy_denial_fails_before_activity_type_selection_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.CommentPolicyMissing,
                "Comment policy is not resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CommentPolicyMissing);
    }

    [Fact]
    public async Task Work_targeted_capture_validates_only_work_and_contributor_before_policy()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            WorkCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_rejects_stale_activity_type_catalog_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            new([], ProjectionFreshnessMetadata.Stale("42")),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Capture_rejects_missing_activity_type_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeNotFound);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "not-found");
    }

    [Fact]
    public async Task Capture_rejects_inactive_activity_type_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Tenant, null, false)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeInactive);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "unavailable");
    }

    [Fact]
    public async Task Project_capture_rejects_project_activity_type_from_different_project_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, new ProjectReference("other-project"), true)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "scope-mismatch");
    }

    [Fact]
    public async Task Work_capture_rejects_project_activity_type_until_governing_project_adapter_exists()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            WorkCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, Project(), true)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.AuthorityCannotBeResolved);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "target" && error.Code == "work-project-unresolved");
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Project_capture_dispatches_with_matching_project_activity_type_scope()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, Project(), true)),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryRecorded recorded = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        recorded.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
    }

    private static ActivityTypeCatalogReadModel FreshCatalog(ActivityTypeScope scope)
        => new(
            [
                new(
                    ActivityId(),
                    scope,
                    scope == ActivityTypeScope.Project ? Project() : null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogReadModel Catalog(params ActivityTypeCatalogItem[] items)
        => new(items, ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogItem Item(
        ActivityTypeId activityTypeId,
        ActivityTypeScope scope,
        ProjectReference? project,
        bool isActive)
        => new(
            activityTypeId,
            scope,
            project,
            "Delivery",
            isActive,
            BillableState.Billable);

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static RecordTimeEntry ProjectCommand()
        => Command(TimeEntryTargetReference.ForProject(Project()));

    private static RecordTimeEntry WorkCommand()
        => Command(TimeEntryTargetReference.ForWork(Work()));

    private static RecordTimeEntry Command(TimeEntryTargetReference target)
        => new(
            new TimeEntryId("time-entry-1"),
            target,
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            null);

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static Fixture AuthorizedFixture()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());
        return fixture;
    }

    private static Fixture AuthorizedProjectFixture()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        return fixture;
    }

    private static TimesheetsRejection Rejection(TimeEntryCommandResult result)
    {
        result.Authorization.IsAuthorized.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
        return result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }

    private sealed class Fixture
    {
        public ITimesheetsTenantAccessValidator TenantValidator { get; } = Substitute.For<ITimesheetsTenantAccessValidator>();

        public IProjectReferenceValidator ProjectValidator { get; } = Substitute.For<IProjectReferenceValidator>();

        public IWorkReferenceValidator WorkValidator { get; } = Substitute.For<IWorkReferenceValidator>();

        public IContributorPartyValidator PartyValidator { get; } = Substitute.For<IContributorPartyValidator>();

        public ITimesheetsPolicyEvaluator PolicyEvaluator { get; } = Substitute.For<ITimesheetsPolicyEvaluator>();

        public TimeEntryCommandService CreateService()
            => new(new TimesheetsAccessGuard(
                TenantValidator,
                ProjectValidator,
                WorkValidator,
                PartyValidator,
                PolicyEvaluator));
    }
}
