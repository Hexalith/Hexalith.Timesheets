using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ActivityTypeAggregateTests
{
    [Fact]
    public void CreateTenantActivityTypeEmitsTenantScopedCreatedEvent()
    {
        TimesheetsDomainResult result = TenantActivityTypeAggregate.Handle(
            new CreateTenantActivityType(ActivityId(), " Discovery ", BillableState.Billable),
            null);

        ActivityTypeCreated created = SingleSuccess<ActivityTypeCreated>(result);
        created.ActivityTypeId.Value.ShouldBe("activity-type-1");
        created.Scope.ShouldBe(ActivityTypeScope.Tenant);
        created.Project.ShouldBeNull();
        created.Label.ShouldBe("Discovery");
        created.DefaultBillableState.ShouldBe(BillableState.Billable);
    }

    [Fact]
    public void CreateRejectsDuplicateActivityTypeId()
    {
        ActivityTypeCatalogState state = CreatedState();

        TimesheetsDomainResult result = TenantActivityTypeAggregate.Handle(
            new CreateTenantActivityType(ActivityId(), "Build", BillableState.NonBillable),
            state);

        Rejection(result).Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeAlreadyExists);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateAndRenameRejectBlankLabels(string label)
    {
        TenantActivityTypeAggregate.Handle(
                new CreateTenantActivityType(new ActivityTypeId("new-activity"), label, BillableState.Billable),
                null)
            .IsRejection.ShouldBeTrue();

        TenantActivityTypeAggregate.Handle(
                new RenameActivityType(ActivityId(), label),
                CreatedState())
            .IsRejection.ShouldBeTrue();
    }

    [Fact]
    public void CreateAndMetadataUpdateRejectUnknownBillableMetadata()
    {
        TenantActivityTypeAggregate.Handle(
                new CreateTenantActivityType(new ActivityTypeId("new-activity"), "Build", BillableState.Unknown),
                null)
            .IsRejection.ShouldBeTrue();

        TimesheetsRejection rejection = Rejection(TenantActivityTypeAggregate.Handle(
            new UpdateActivityTypeMetadata(ActivityId(), BillableState.Unknown),
            CreatedState()));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
    }

    [Fact]
    public void RenameMetadataDeactivateAndReactivatePreserveStableActivityTypeId()
    {
        ActivityTypeCatalogState state = CreatedState();

        SingleSuccess<ActivityTypeRenamed>(TenantActivityTypeAggregate.Handle(
                new RenameActivityType(ActivityId(), "Delivery"),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        SingleSuccess<ActivityTypeMetadataUpdated>(TenantActivityTypeAggregate.Handle(
                new UpdateActivityTypeMetadata(ActivityId(), BillableState.NonBillable),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        SingleSuccess<ActivityTypeDeactivated>(TenantActivityTypeAggregate.Handle(
                new DeactivateActivityType(ActivityId()),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        state.Apply(new ActivityTypeDeactivated(ActivityId()));

        SingleSuccess<ActivityTypeReactivated>(TenantActivityTypeAggregate.Handle(
                new ReactivateActivityType(ActivityId()),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());
    }

    [Fact]
    public void SameLabelSameMetadataInactiveAndActiveTransitionsAreNoops()
    {
        ActivityTypeCatalogState state = CreatedState();

        TenantActivityTypeAggregate.Handle(
                new RenameActivityType(ActivityId(), "Discovery"),
                state)
            .IsNoOp.ShouldBeTrue();

        TenantActivityTypeAggregate.Handle(
                new UpdateActivityTypeMetadata(ActivityId(), BillableState.Billable),
                state)
            .IsNoOp.ShouldBeTrue();

        state.Apply(new ActivityTypeDeactivated(ActivityId()));

        TenantActivityTypeAggregate.Handle(
                new DeactivateActivityType(ActivityId()),
                state)
            .IsNoOp.ShouldBeTrue();

        state.Apply(new ActivityTypeReactivated(ActivityId()));

        TenantActivityTypeAggregate.Handle(
                new ReactivateActivityType(ActivityId()),
                state)
            .IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void UnknownOrProjectScopedActivityTypeRejectsTenantMutations()
    {
        TenantActivityTypeAggregate.Handle(
                new DeactivateActivityType(ActivityId()),
                null)
            .IsRejection.ShouldBeTrue();

        ActivityTypeCatalogState state = new();
        state.Apply(new ActivityTypeCreated(
            ActivityId(),
            ActivityTypeScope.Project,
            new ProjectReference("project-1"),
            "Project work",
            BillableState.Billable));

        Rejection(TenantActivityTypeAggregate.Handle(
                new RenameActivityType(ActivityId(), "Tenant work"),
                state))
            .Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);
    }

    [Fact]
    public void CreateProjectActivityTypeEmitsProjectScopedCreatedEvent()
    {
        TimesheetsDomainResult result = ProjectActivityTypeAggregate.Handle(
            new CreateProjectActivityType(ActivityId(), Project(), " Delivery ", BillableState.Billable),
            null);

        ActivityTypeCreated created = SingleSuccess<ActivityTypeCreated>(result);
        created.ActivityTypeId.ShouldBe(ActivityId());
        created.Scope.ShouldBe(ActivityTypeScope.Project);
        created.Project.ShouldBe(Project());
        created.Label.ShouldBe("Delivery");
        created.DefaultBillableState.ShouldBe(BillableState.Billable);
    }

    [Fact]
    public void ProjectActivityTypeRejectsDuplicateScopeMismatchAndMissingProjectReference()
    {
        ActivityTypeCatalogState state = CreatedState();

        Rejection(ProjectActivityTypeAggregate.Handle(
                new CreateProjectActivityType(ActivityId(), Project(), "Project work", BillableState.Billable),
                state))
            .Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);

        ActivityTypeCatalogState projectState = ProjectCreatedState();

        Rejection(ProjectActivityTypeAggregate.Handle(
                new CreateProjectActivityType(ActivityId(), Project(), "Project work", BillableState.Billable),
                projectState))
            .Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeAlreadyExists);

        Rejection(ProjectActivityTypeAggregate.Handle(
                new RenameProjectActivityType(ActivityId(), new ProjectReference("other-project"), "Renamed"),
                projectState))
            .Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);
    }

    [Fact]
    public void ProjectCommandsRejectMissingProjectReferenceWithTypedValidationOutcome()
    {
        Rejection(ProjectActivityTypeAggregate.Handle(
                new CreateProjectActivityType(ActivityId(), null!, "Delivery", BillableState.Billable),
                null))
            .Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);

        Rejection(ProjectActivityTypeAggregate.Handle(
                new RenameProjectActivityType(ActivityId(), null!, "Delivery"),
                ProjectCreatedState()))
            .Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);

        Rejection(ProjectActivityTypeAggregate.Handle(
                new DeactivateProjectActivityType(ActivityId(), null!),
                ProjectCreatedState()))
            .Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);

        Rejection(ProjectActivityTypeAggregate.Handle(
                new ConfigureProjectActivityTypeCatalogRestriction(null!, true, [], [])))
            .Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
    }

    [Fact]
    public void ProjectMutationsPreserveStableActivityTypeId()
    {
        ActivityTypeCatalogState state = ProjectCreatedState();

        SingleSuccess<ActivityTypeRenamed>(ProjectActivityTypeAggregate.Handle(
                new RenameProjectActivityType(ActivityId(), Project(), "Delivery"),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        SingleSuccess<ActivityTypeMetadataUpdated>(ProjectActivityTypeAggregate.Handle(
                new UpdateProjectActivityTypeMetadata(ActivityId(), Project(), BillableState.NonBillable),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        SingleSuccess<ActivityTypeDeactivated>(ProjectActivityTypeAggregate.Handle(
                new DeactivateProjectActivityType(ActivityId(), Project()),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());

        state.Apply(new ActivityTypeDeactivated(ActivityId()));

        SingleSuccess<ActivityTypeReactivated>(ProjectActivityTypeAggregate.Handle(
                new ReactivateProjectActivityType(ActivityId(), Project()),
                state))
            .ActivityTypeId.ShouldBe(ActivityId());
    }

    [Fact]
    public void ProjectRestrictionConfigurationEmitsReplayablePolicyEvent()
    {
        TimesheetsDomainResult result = ProjectActivityTypeAggregate.Handle(
            new ConfigureProjectActivityTypeCatalogRestriction(
                Project(),
                true,
                [new ActivityTypeId("tenant-allowed")],
                [ActivityId()]));

        ProjectActivityTypeCatalogRestrictionConfigured configured =
            SingleSuccess<ProjectActivityTypeCatalogRestrictionConfigured>(result);
        configured.Project.ShouldBe(Project());
        configured.IsRestricted.ShouldBeTrue();
        configured.AllowedTenantActivityTypeIds.ShouldBe([new ActivityTypeId("tenant-allowed")]);
        configured.AllowedProjectActivityTypeIds.ShouldBe([ActivityId()]);
    }

    private static ActivityTypeCatalogState CreatedState()
    {
        ActivityTypeCatalogState state = new();
        state.Apply(new ActivityTypeCreated(
            ActivityId(),
            ActivityTypeScope.Tenant,
            null,
            "Discovery",
            BillableState.Billable));
        return state;
    }

    private static ActivityTypeCatalogState ProjectCreatedState()
    {
        ActivityTypeCatalogState state = new();
        state.Apply(new ActivityTypeCreated(
            ActivityId(),
            ActivityTypeScope.Project,
            Project(),
            "Project discovery",
            BillableState.Billable));
        return state;
    }

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static ProjectReference Project() => new("project-1");

    private static TEvent SingleSuccess<TEvent>(TimesheetsDomainResult result)
    {
        result.IsSuccess.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TEvent>();
    }

    private static TimesheetsRejection Rejection(TimesheetsDomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }
}
