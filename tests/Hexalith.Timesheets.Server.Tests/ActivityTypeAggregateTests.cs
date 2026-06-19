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
    public void Create_tenant_activity_type_emits_tenant_scoped_created_event()
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
    public void Create_rejects_duplicate_activity_type_id()
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
    public void Create_and_rename_reject_blank_labels(string label)
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
    public void Create_and_metadata_update_reject_unknown_billable_metadata()
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
    public void Rename_metadata_deactivate_and_reactivate_preserve_stable_activity_type_id()
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
    public void Same_label_same_metadata_inactive_and_active_transitions_are_noops()
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
    public void Unknown_or_project_scoped_activity_type_rejects_tenant_mutations()
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

    private static ActivityTypeId ActivityId() => new("activity-type-1");

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
