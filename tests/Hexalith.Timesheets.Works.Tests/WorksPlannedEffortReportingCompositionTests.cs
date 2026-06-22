using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

/// <summary>
/// Composed-service proof for the planned-effort adapter: a DI-resolved <see cref="ActualTimeReportQueryService"/>
/// discloses Works-attributed planned effort only when <c>AddTimesheetsWorksPlannedEffortReporting()</c> is
/// composed, and stays fail-closed on the kernel default otherwise. Plus registration-wins coverage in both
/// call orders relative to <c>AddTimesheetsServerKernel</c>.
/// </summary>
public sealed class WorksPlannedEffortReportingCompositionTests
{
    [Fact]
    public async Task ComposedReportServiceDisclosesWorksPlannedEffortThroughTheAdapter()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));
        using ServiceProvider provider = BuildReportProvider(channel, registerAdapter: true);

        ActualTimeReportQueryService service = provider.GetRequiredService<ActualTimeReportQueryService>();
        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            WorksQueryTestData.Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        WorkPlannedEffortReadModel plannedEffort = row.WorkPlannedEffort.ShouldNotBeNull();
        plannedEffort.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        plannedEffort.SourceModuleName.ShouldBe("Works");
        plannedEffort.Estimated.ShouldBe(160);
        plannedEffort.Unit.ShouldBe("minutes");

        await channel.Received(1).InvokeAsync(
            Arg.Is<QueryEnvelope>(envelope =>
                envelope != null
                && envelope.Domain == "work"
                && envelope.QueryType == "get-work-item"
                && envelope.AggregateId == WorksQueryTestData.WorkIdValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposedReportServiceKeepsPlannedEffortUnavailableWhenAdapterIsNotRegistered()
    {
        using ServiceProvider provider = BuildReportProvider(channel: null, registerAdapter: false);

        ActualTimeReportQueryService service = provider.GetRequiredService<ActualTimeReportQueryService>();
        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            WorksQueryTestData.Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        WorkPlannedEffortReadModel plannedEffort = row.WorkPlannedEffort.ShouldNotBeNull();
        plannedEffort.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
        plannedEffort.SourceModuleName.ShouldBe("Works");

        provider.GetRequiredService<IWorkPlannedEffortProvider>()
            .ShouldBeOfType<UnavailableWorkPlannedEffortProvider>();
    }

    [Fact]
    public void AdapterRegistrationWinsWhenCalledAfterKernel()
    {
        ServiceCollection services = new();
        services.AddTimesheetsServerKernel();
        services.AddTimesheetsWorksPlannedEffortReporting();
        services.AddSingleton(Substitute.For<IWorksQueryChannel>());

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkPlannedEffortProvider>()
            .ShouldBeOfType<WorksQueryWorkPlannedEffortProvider>();
    }

    [Fact]
    public void AdapterRegistrationWinsWhenCalledBeforeKernel()
    {
        ServiceCollection services = new();
        services.AddTimesheetsWorksPlannedEffortReporting();
        services.AddTimesheetsServerKernel();
        services.AddSingleton(Substitute.For<IWorksQueryChannel>());

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkPlannedEffortProvider>()
            .ShouldBeOfType<WorksQueryWorkPlannedEffortProvider>();
    }

    private static IWorksQueryChannel ChannelReturning(QueryResult result)
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return channel;
    }

    private static ServiceProvider BuildReportProvider(IWorksQueryChannel? channel, bool registerAdapter)
    {
        ServiceCollection services = new();
        services.AddTimesheetsServerKernel();

        // Allow disclosure to reach the planned-effort step: authorize the projection read and every row.
        services.Replace(ServiceDescriptor.Singleton<ITimesheetsAccessGuard>(new AllowAllAccessGuard()));

        // Seed one already-authorized Work row carrying the projection's NotSupplied placeholder, which the
        // planned-effort provider overwrites during disclosure.
        services.Replace(ServiceDescriptor.Singleton<IActualTimeReportProjectionReader>(
            new SingleWorkRowReportReader()));

        if (channel is not null)
        {
            services.AddSingleton(channel);
        }

        if (registerAdapter)
        {
            services.AddTimesheetsWorksPlannedEffortReporting();
        }

        return services.BuildServiceProvider();
    }

    private static ActualTimeReportRowReadModel WorkRow()
        => new(
            TimeEntryTargetReference.ForWork(new WorkReference(WorksQueryTestData.WorkIdValue)),
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            TimeEntryApprovalState.Approved,
            ContributorCategory.Employee,
            60,
            1,
            0,
            0,
            ActualTimeReportRowState.Current,
            ActualTimeReferenceStateMetadata.Current,
            ProjectionFreshnessMetadata.Fresh)
        {
            WorkPlannedEffort = WorkPlannedEffortReadModel.NotSupplied()
        };

    private sealed class SingleWorkRowReportReader : IActualTimeReportProjectionReader
    {
        public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
            TimesheetsRequestContext context,
            QueryProjectActualTimeReport query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ActualTimeReportReadModel?>(
                new([WorkRow()], null, ProjectionFreshnessMetadata.Fresh));

        public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
            TimesheetsRequestContext context,
            QueryWorkActualTimeReport query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ActualTimeReportReadModel?>(
                new([WorkRow()], null, ProjectionFreshnessMetadata.Fresh));
    }

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
    }
}
