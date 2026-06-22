using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

/// <summary>
/// State-matrix unit tests for <see cref="WorksQueryWorkPlannedEffortProvider"/>: every Works consumer-view
/// condition mapped to the expected <see cref="WorkPlannedEffortReadModel"/> outcome, source attribution,
/// freshness cursor, unit pass-through, and replay determinism.
/// </summary>
public sealed class WorksQueryWorkPlannedEffortProviderTests
{
    [Fact]
    public async Task SuppliesPlannedEffortWhenFoundWorkHasEstimateAndUnit()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.SourceModuleName.ShouldBe("Works");
        result.Estimated.ShouldBe(160);
        result.Done.ShouldBe(40);
        result.Remaining.ShouldBe(120);
        result.Unit.ShouldBe("minutes");
    }

    [Fact]
    public async Task SuppliedResultRecordsWorksSourceSequenceAsFreshnessCursor()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.Completed,
                sourceSequence: 42,
                estimated: 8,
                done: 8,
                remaining: 0,
                unit: "hours")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        result.SourceFreshness.Cursor.ShouldBe("42");
        result.SourceFreshness.AsOfUtc.ShouldBeNull();
    }

    [Fact]
    public async Task PassesWorksUnitStringThroughUnchangedWithoutConversion()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 13,
                done: 5,
                remaining: 8,
                unit: "story-points")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        // The Works unit is surfaced verbatim — never converted into minutes/hours or any Timesheets unit.
        result.Unit.ShouldBe("story-points");
        result.Estimated.ShouldBe(13);
    }

    [Fact]
    public async Task ReturnsNotSuppliedWhenFoundWorkHasNoEstimate()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(WorkItemStatus.InProgress)));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.NotSupplied);
        result.Estimated.ShouldBeNull();
        result.Unit.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNotSuppliedWhenFoundWorkHasEstimateButNoUnit()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: null)));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.NotSupplied);
    }

    [Fact]
    public async Task ReturnsNotSuppliedWhenWorkNotFound()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.NotFoundView()));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        // A row reaching the provider is already authorized; an absent Works estimate is "not supplied",
        // not an authority failure.
        result.Availability.ShouldBe(WorkPlannedEffortAvailability.NotSupplied);
    }

    [Theory]
    [InlineData(WorkItemStatus.Created)]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Cancelled)]
    [InlineData(WorkItemStatus.Expired)]
    public async Task SuppliesPlannedEffortRegardlessOfLifecycleStatus(WorkItemStatus status)
    {
        // Reporting reads over already-authorized actuals: a completed or cancelled Work can still carry a
        // planned estimate worth comparing against logged time. There is no lifecycle write-gate here.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                status,
                estimated: 100,
                done: 10,
                remaining: 90,
                unit: "minutes")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.Estimated.ShouldBe(100);
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenViewTenantDiffersFromRequestTenant()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                tenant: "tenant-b",
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unauthorized);
        result.Estimated.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsUnavailableWhenQueryResultIsUnsuccessful()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            QueryResult.Failure("works-domain-unreachable"));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
        result.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
    }

    [Fact]
    public async Task ProducesEqualResultsAcrossRepeatedInvocationsForSameWork()
    {
        // Determinism / replay: two reads of the same Work at the same source sequence yield value-equal
        // read models with no side effects, so the consumer can memoize one call per distinct Work.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                sourceSequence: 11,
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));

        WorkPlannedEffortReadModel first = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);
        WorkPlannedEffortReadModel second = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        second.ShouldBe(first);
    }

    [Fact]
    public async Task SuppliedResultCarriesCurrentReferenceStateMetadata()
    {
        // AC1 requires source-attributed planned effort with BOTH freshness AND reference-state metadata.
        // A live, in-tenant supply is "Current" reference state — not rebuilding/unavailable/unauthorized.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.SourceReferenceState.State.ShouldBe(ActualTimeReferenceState.Current);
        result.SourceReferenceState.Detail.ShouldBeNull();
    }

    [Fact]
    public async Task SuppliesPlannedEffortWhenEstimateAndUnitPresentButDoneAndRemainingAbsent()
    {
        // The supply gate keys only on Estimated + Unit; Done/Remaining are optional and pass through as null.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 40,
                unit: "hours")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.Estimated.ShouldBe(40);
        result.Unit.ShouldBe("hours");
        result.Done.ShouldBeNull();
        result.Remaining.ShouldBeNull();
    }

    [Fact]
    public async Task SuppliesZeroEstimateAsAConcretePlannedValue()
    {
        // Zero is a real estimate, not "absent": the null-guard must not collapse 0 into NotSupplied.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                estimated: 0,
                done: 0,
                remaining: 0,
                unit: "hours")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.Estimated.ShouldBe(0);
        result.Unit.ShouldBe("hours");
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(7L, "7")]
    [InlineData(4096L, "4096")]
    [InlineData(9007199254740993L, "9007199254740993")]
    public async Task RecordsActualWorksSourceSequenceAsFreshnessCursor(long sourceSequence, string expectedCursor)
    {
        // AC3: across duplicate/replayed/rebuilt Works projection data the cursor stays freshness-aware,
        // reflecting the actual monotonic Works source sequence (including a rebuilt, higher value), and the
        // state stays Fresh — never a fabricated Stale (the consumer view exposes no degraded flag).
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                sourceSequence: sourceSequence,
                estimated: 8,
                done: 2,
                remaining: 6,
                unit: "hours")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        result.SourceFreshness.Cursor.ShouldBe(expectedCursor);
    }

    [Fact]
    public async Task UnauthorizedResultCarriesUnauthorizedReferenceStateMetadata()
    {
        // AC2 fail-closed: the cross-tenant path must mark reference state Unauthorized (never leave it
        // Current) and surface no planned values.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                tenant: "tenant-b",
                estimated: 160,
                done: 40,
                remaining: 120,
                unit: "minutes")));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unauthorized);
        result.SourceReferenceState.State.ShouldBe(ActualTimeReferenceState.Unauthorized);
        result.Estimated.ShouldBeNull();
        result.Unit.ShouldBeNull();
    }

    private static WorksQueryWorkPlannedEffortProvider ProviderReturning(QueryResult result)
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return new WorksQueryWorkPlannedEffortProvider(channel);
    }
}
