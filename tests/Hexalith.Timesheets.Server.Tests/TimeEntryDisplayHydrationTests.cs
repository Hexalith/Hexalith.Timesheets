using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryDisplayHydrationTests
{
    [Fact]
    public async Task Default_display_hydration_provider_returns_unavailable_states_without_guessing_labels()
    {
        UnavailableDisplayHydrationProvider provider = new();
        TimesheetsRequestContext context = Context();

        TimeEntryHydratedDisplayLabel contributor = await provider.HydrateContributorAsync(
            context,
            Contributor(),
            TestContext.Current.CancellationToken);
        TimeEntryHydratedDisplayLabel project = await provider.HydrateProjectAsync(
            context,
            Project(),
            TestContext.Current.CancellationToken);
        TimeEntryHydratedDisplayLabel work = await provider.HydrateWorkAsync(
            context,
            Work(),
            TestContext.Current.CancellationToken);
        TimeEntryHydratedDisplayLabel activityType = await provider.HydrateActivityTypeAsync(
            context,
            ActivityId(),
            ActivityTypeScope.Tenant,
            TestContext.Current.CancellationToken);

        foreach (TimeEntryHydratedDisplayLabel label in new[] { contributor, project, work, activityType })
        {
            label.State.ShouldBe(DisplayHydrationState.Unavailable);
            label.Label.ShouldBeNull();
            label.AsOfUtc.ShouldBeNull();
            label.Detail.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Display_hydrator_routes_project_target_to_project_provider_and_activity_provider()
    {
        IPartyDisplayHydrationProvider partyProvider = Substitute.For<IPartyDisplayHydrationProvider>();
        IProjectDisplayHydrationProvider projectProvider = Substitute.For<IProjectDisplayHydrationProvider>();
        IWorkDisplayHydrationProvider workProvider = Substitute.For<IWorkDisplayHydrationProvider>();
        IActivityTypeDisplayHydrationProvider activityTypeProvider = Substitute.For<IActivityTypeDisplayHydrationProvider>();
        partyProvider
            .HydrateContributorAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Contributor")));
        projectProvider
            .HydrateProjectAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Stale("Project", detail: "Project label is stale.")));
        activityTypeProvider
            .HydrateActivityTypeAsync(
                Arg.Any<TimesheetsRequestContext>(),
                Arg.Any<ActivityTypeId>(),
                Arg.Any<ActivityTypeScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Unavailable("Activity Type display label is unavailable.")));

        UnavailableTimeEntryDisplayHydrator hydrator = new(
            partyProvider,
            projectProvider,
            workProvider,
            activityTypeProvider);

        TimeEntryDisplayHydration hydration = await hydrator.HydrateAsync(
            Context(),
            EvidenceModel(TimeEntryTargetReference.ForProject(Project())),
            TestContext.Current.CancellationToken);

        hydration.Contributor.Label.ShouldBe("Contributor");
        hydration.Target.State.ShouldBe(DisplayHydrationState.Stale);
        hydration.Target.Label.ShouldBe("Project");
        hydration.ActivityType.State.ShouldBe(DisplayHydrationState.Unavailable);
        await projectProvider.Received(1)
            .HydrateProjectAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        workProvider.ReceivedCalls().ShouldBeEmpty();
        await activityTypeProvider.Received(1)
            .HydrateActivityTypeAsync(
                Arg.Any<TimesheetsRequestContext>(),
                ActivityId(),
                ActivityTypeScope.Tenant,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Display_hydrator_routes_work_target_to_work_provider_without_project_lookup()
    {
        IPartyDisplayHydrationProvider partyProvider = Substitute.For<IPartyDisplayHydrationProvider>();
        IProjectDisplayHydrationProvider projectProvider = Substitute.For<IProjectDisplayHydrationProvider>();
        IWorkDisplayHydrationProvider workProvider = Substitute.For<IWorkDisplayHydrationProvider>();
        IActivityTypeDisplayHydrationProvider activityTypeProvider = Substitute.For<IActivityTypeDisplayHydrationProvider>();
        partyProvider
            .HydrateContributorAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Contributor")));
        workProvider
            .HydrateWorkAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Work")));
        activityTypeProvider
            .HydrateActivityTypeAsync(
                Arg.Any<TimesheetsRequestContext>(),
                Arg.Any<ActivityTypeId>(),
                Arg.Any<ActivityTypeScope>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Denied()));

        UnavailableTimeEntryDisplayHydrator hydrator = new(
            partyProvider,
            projectProvider,
            workProvider,
            activityTypeProvider);

        TimeEntryDisplayHydration hydration = await hydrator.HydrateAsync(
            Context(),
            EvidenceModel(TimeEntryTargetReference.ForWork(Work())),
            TestContext.Current.CancellationToken);

        hydration.Target.Label.ShouldBe("Work");
        hydration.ActivityType.State.ShouldBe(DisplayHydrationState.Denied);
        projectProvider.ReceivedCalls().ShouldBeEmpty();
        await workProvider.Received(1)
            .HydrateWorkAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
    }

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static TimeEntryEvidenceReadModel EvidenceModel(TimeEntryTargetReference target)
        => new(
            TimeEntryId(),
            target,
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh);

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");
}
