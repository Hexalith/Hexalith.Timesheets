using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ActivityTypes;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class ActivityTypeCatalogProjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant-2")]
    [InlineData("TENANT-1")]
    [InlineData("tenant-1 ")]
    public void CatalogViewsRejectMismatchedCheckpointTenant(string? checkpointTenant)
    {
        ActivityTypeProjectionEvent[][] histories = [[], [Event("m1", 1, Created("delivery", "Delivery"))]];
        foreach (ProjectionFreshness freshness in Enum.GetValues<ProjectionFreshness>())
        {
            var checkpoint = new TimesheetsProjectionCheckpoint(
                checkpointTenant!, TenantActivityTypeCatalogProjection.ProjectionName, 1, freshness);
            foreach (ActivityTypeProjectionEvent[] events in histories)
            {
                Should.Throw<InvalidOperationException>(() => Projector().Project("tenant-1", events, checkpoint));
                Should.Throw<InvalidOperationException>(() => Projector().ProjectForProject(
                    "tenant-1", new ProjectReference("project-1"), events, checkpoint));
            }
        }
    }

    [Fact]
    public void ProjectionReturnsFreshCatalogWithActiveAndInactiveStatusText()
    {
        ActivityTypeCatalogReadModel model = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Created("discovery", "Discovery")),
                Event("m2", 2, Created("delivery", "Delivery")),
                Event("m3", 3, new ActivityTypeDeactivated(new("delivery")))
            ],
            FreshCheckpoint(3));

        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        model.Items.Count.ShouldBe(2);

        ActivityTypeCatalogItem inactive = model.Items.Single(static item => item.ActivityTypeId.Value == "delivery");
        inactive.IsActive.ShouldBeFalse();
        inactive.ActiveState.ShouldBe(ActivityTypeActiveState.Inactive);
        inactive.StatusText.ShouldBe("Inactive");
        inactive.IsAvailableForCapture.ShouldBeFalse();
    }

    [Fact]
    public void ProjectionAppliesRenameMetadataUpdateDeactivateAndReactivateByStableId()
    {
        ActivityTypeCatalogReadModel model = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Created("discovery", "Discovery")),
                Event("m2", 2, new ActivityTypeRenamed(new("discovery"), "Customer discovery")),
                Event("m3", 3, new ActivityTypeMetadataUpdated(new("discovery"), BillableState.NonBillable)),
                Event("m4", 4, new ActivityTypeDeactivated(new("discovery"))),
                Event("m5", 5, new ActivityTypeReactivated(new("discovery")))
            ],
            FreshCheckpoint(5));

        ActivityTypeCatalogItem item = model.Items.ShouldHaveSingleItem();
        item.ActivityTypeId.Value.ShouldBe("discovery");
        item.Label.ShouldBe("Customer discovery");
        item.DefaultBillableState.ShouldBe(BillableState.NonBillable);
        item.IsActive.ShouldBeTrue();
        item.StatusText.ShouldBe("Active");
        item.IsAvailableForCapture.ShouldBeTrue();
    }

    [Fact]
    public void ProjectionIsIdempotentForDuplicateDeliveryAndReplayEquivalent()
    {
        ActivityTypeProjectionEvent[] once =
        [
            Event("m1", 1, Created("discovery", "Discovery")),
            Event("m2", 2, new ActivityTypeRenamed(new("discovery"), "Customer discovery"))
        ];
        ActivityTypeProjectionEvent[] duplicates = [.. once, once[0], once[1]];

        ActivityTypeCatalogReadModel replayedOnce = Projector().Project("tenant-1", once, FreshCheckpoint(2));
        ActivityTypeCatalogReadModel replayedDuplicates = Projector().Project("tenant-1", duplicates, FreshCheckpoint(2));

        replayedDuplicates.Items.ShouldBe(replayedOnce.Items);
    }

    [Fact]
    public void ProjectionIgnoresProjectScopedEventsForTenantCatalog()
    {
        ActivityTypeCatalogReadModel model = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Created("tenant", "Tenant work")),
                Event("m2", 2, new ActivityTypeCreated(
                    new("project"),
                    ActivityTypeScope.Project,
                    new ProjectReference("project-1"),
                    "Project work",
                    BillableState.Billable))
            ],
            FreshCheckpoint(2));

        model.Items.ShouldHaveSingleItem().ActivityTypeId.Value.ShouldBe("tenant");
    }

    [Fact]
    public void ProjectCatalogIncludesActiveTenantRowsAndRequestedProjectRows()
    {
        ActivityTypeCatalogReadModel model = Projector().ProjectForProject(
            "tenant-1",
            Project(),
            [
                Event("m1", 1, Created("tenant", "Tenant work")),
                Event("m2", 2, new ActivityTypeCreated(
                    new("project"),
                    ActivityTypeScope.Project,
                    Project(),
                    "Project work",
                    BillableState.Billable)),
                Event("m3", 3, new ActivityTypeCreated(
                    new("other-project"),
                    ActivityTypeScope.Project,
                    new ProjectReference("other-project"),
                    "Other project work",
                    BillableState.Billable))
            ],
            FreshCheckpoint(3));

        model.Items.Select(static item => item.ActivityTypeId.Value).ShouldBe(["project", "tenant"]);
        model.Items.All(static item => item.IsAvailableForCapture).ShouldBeTrue();
        model.Items.Single(static item => item.ActivityTypeId.Value == "project").Project.ShouldBe(Project());
    }

    [Fact]
    public void RestrictedProjectCatalogMarksExcludedAndInactiveRowsUnavailableForCapture()
    {
        ActivityTypeCatalogReadModel model = Projector().ProjectForProject(
            "tenant-1",
            Project(),
            [
                Event("m1", 1, Created("tenant-allowed", "Allowed tenant")),
                Event("m2", 2, Created("tenant-restricted", "Restricted tenant")),
                Event("m3", 3, new ActivityTypeCreated(
                    new("project-allowed"),
                    ActivityTypeScope.Project,
                    Project(),
                    "Allowed project",
                    BillableState.Billable)),
                Event("m4", 4, new ActivityTypeCreated(
                    new("project-restricted"),
                    ActivityTypeScope.Project,
                    Project(),
                    "Restricted project",
                    BillableState.Billable)),
                Event("m5", 5, new ActivityTypeDeactivated(new("project-allowed"))),
                Event("m6", 6, new ProjectActivityTypeCatalogRestrictionConfigured(
                    Project(),
                    true,
                    [new ActivityTypeId("tenant-allowed")],
                    [new ActivityTypeId("project-allowed")]))
            ],
            FreshCheckpoint(6));

        model.Items.Single(static item => item.ActivityTypeId.Value == "tenant-allowed")
            .IsAvailableForCapture.ShouldBeTrue();
        model.Items.Single(static item => item.ActivityTypeId.Value == "tenant-restricted")
            .IsAvailableForCapture.ShouldBeFalse();
        model.Items.Single(static item => item.ActivityTypeId.Value == "project-allowed")
            .IsAvailableForCapture.ShouldBeFalse();
        model.Items.Single(static item => item.ActivityTypeId.Value == "project-restricted")
            .IsAvailableForCapture.ShouldBeFalse();
    }

    [Fact]
    public void ProjectCatalogRestrictionProjectionIsIdempotentForDuplicateDelivery()
    {
        ActivityTypeProjectionEvent[] once =
        [
            Event("m1", 1, Created("tenant", "Tenant work")),
            Event("m2", 2, new ProjectActivityTypeCatalogRestrictionConfigured(
                Project(),
                true,
                [new ActivityTypeId("tenant")],
                []))
        ];

        ActivityTypeCatalogReadModel replayedOnce = Projector().ProjectForProject("tenant-1", Project(), once, FreshCheckpoint(2));
        ActivityTypeCatalogReadModel replayedDuplicates = Projector().ProjectForProject(
            "tenant-1",
            Project(),
            [.. once, once[0], once[1]],
            FreshCheckpoint(2));

        replayedDuplicates.Items.ShouldBe(replayedOnce.Items);
    }

    [Fact]
    public void ProjectCatalogKeepsTenantAndProjectRowsDistinctWhenDisplayLabelsCollide()
    {
        ActivityTypeCatalogReadModel model = Projector().ProjectForProject(
            "tenant-1",
            Project(),
            [
                Event("m1", 1, Created("tenant-discovery", "Discovery")),
                Event("m2", 2, new ActivityTypeCreated(
                    new("project-discovery"),
                    ActivityTypeScope.Project,
                    Project(),
                    "Discovery",
                    BillableState.Billable))
            ],
            FreshCheckpoint(2));

        model.Items.Count.ShouldBe(2);
        model.Items.ShouldAllBe(static item => item.Label == "Discovery");
        model.Items.Count(static item => item.Scope == ActivityTypeScope.Tenant).ShouldBe(1);
        model.Items.Count(static item => item.Scope == ActivityTypeScope.Project).ShouldBe(1);
        model.Items.Select(static item => item.ActivityTypeId.Value)
            .ShouldBe(["project-discovery", "tenant-discovery"]);
    }

    [Fact]
    public void ProjectionOrdersEventsBySequenceNumberRegardlessOfDeliveryOrder()
    {
        ActivityTypeCatalogReadModel model = Projector().Project(
            "tenant-1",
            [
                Event("m3", 3, new ActivityTypeReactivated(new("discovery"))),
                Event("m1", 1, Created("discovery", "Discovery")),
                Event("m2", 2, new ActivityTypeDeactivated(new("discovery")))
            ],
            FreshCheckpoint(3));

        ActivityTypeCatalogItem item = model.Items.ShouldHaveSingleItem();
        item.ActivityTypeId.Value.ShouldBe("discovery");
        item.IsActive.ShouldBeTrue();
        item.StatusText.ShouldBe("Active");
        item.IsAvailableForCapture.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable)]
    public void ProjectionFreshnessMetadataPreventsUnfreshCatalogAuthority(
        ProjectionFreshness freshness,
        ProjectionFreshnessState expectedState)
    {
        ActivityTypeCatalogReadModel model = Projector().Project(
            "tenant-1",
            [Event("m1", 1, Created("tenant", "Tenant work"))],
            new TimesheetsProjectionCheckpoint("tenant-1", TenantActivityTypeCatalogProjection.ProjectionName, 1, freshness));

        model.ProjectionFreshness.State.ShouldBe(expectedState);
        new TimesheetsProjectionCheckpoint("tenant-1", TenantActivityTypeCatalogProjection.ProjectionName, 1, freshness)
            .CanServeReads.ShouldBeFalse();
    }

    [Fact]
    public void CanonicalTenantCatalogCarriesProjectRestrictionsWithoutProjectOwnedActivityTypes()
    {
        var otherProject = new ProjectReference("project-2");
        ActivityTypeCatalogReadModel model = Projector().Project("tenant-1",
        [
            Event("tenant", 1, Created("tenant", "Tenant work")),
            Event("project", 2, new ActivityTypeCreated(new("project-type"), ActivityTypeScope.Project,
                Project(), "Project work", BillableState.Billable)),
            Event("restricted", 3, new ProjectActivityTypeCatalogRestrictionConfigured(Project(), true,
                [new("tenant")], [new("project-type")])),
            Event("other", 4, new ProjectActivityTypeCatalogRestrictionConfigured(otherProject, true, [], [])),
            Event("unrestricted", 5, new ProjectActivityTypeCatalogRestrictionConfigured(Project(), false, [], []))
        ], FreshCheckpoint(5));
        model.Items.ShouldHaveSingleItem().Scope.ShouldBe(ActivityTypeScope.Tenant);
        model.ProjectPolicy.ShouldNotBeNull().Tenant.ShouldBe(new TenantReference("tenant-1"));
        model.ProjectPolicy.IsComplete.ShouldBeFalse();
        model.ProjectPolicy.Restrictions.Count.ShouldBe(2);
        model.ProjectPolicy.Restrictions.Single(row => row.Project == Project()).IsRestricted.ShouldBeFalse();
        model.ProjectPolicy.Restrictions.Single(row => row.Project == otherProject).IsRestricted.ShouldBeTrue();
    }

    private static TenantActivityTypeCatalogProjection Projector() => new();

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequenceNumber)
        => new("tenant-1", TenantActivityTypeCatalogProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static ActivityTypeCreated Created(string id, string label)
        => new(new(id), ActivityTypeScope.Tenant, null, label, BillableState.Billable);

    private static ActivityTypeProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static ProjectReference Project() => new("project-1");
}
