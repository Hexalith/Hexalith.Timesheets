using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.MagicLinks;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Verifies projection failure diagnostics and realistic read-model store semantics.</summary>
public sealed partial class MagicLinkStateProjectionHandlerTests
{
    /// <summary>Limits failure records to fixed categories and a platform trace identifier.</summary>
    /// <param name="handlerName">The canonical live handler.</param>
    /// <param name="operation">The failing store operation.</param>
    [Theory]
    [InlineData("index", "get")]
    [InlineData("index", "save")]
    [InlineData("catalog", "get")]
    [InlineData("catalog", "save")]
    public async Task StoreFailureDiagnosticsContainOnlyFixedOutcomeAndPlatformTrace(string handlerName, string operation)
    {
        const string sensitive = "private-payload-token-and-owner";
        var failure = new HttpRequestException(sensitive);
        var store = new ScriptedReadModelStore();
        string before = SeedExistingState(store, handlerName);
        if (operation == "get") store.GetException = failure;
        else store.TrySaveException = failure;
        using var log = new DeliveryOutcomeLogger();
        using var trace = new Activity("projection-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        ProjectionRequest request = ValidRequest(handlerName) with
        {
            Events = ValidRequest(handlerName).Events.Select(item => item with
            {
                CorrelationId = sensitive,
                MessageId = sensitive,
                UserId = sensitive
            }).ToArray()
        };

        DomainProjectionHandlerResult result = handlerName == "index"
            ? await new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, log)
                .ProjectAsync(request, sensitive, TestContext.Current.CancellationToken)
            : await new TenantActivityTypeCatalogProjectionHandler(store, log)
                .ProjectAsync(request, sensitive, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        ReadExistingStateSnapshot(store, handlerName).ShouldBe(before);
        var record = log.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Id.Id.ShouldBe(1302);
        record.Exception.ShouldBeNull();
        record.Message.ShouldContain("outcome=state-unavailable");
        record.Message.ShouldContain(trace.TraceId.ToHexString());
        record.Message.ShouldNotContain(sensitive);
        record.State.ShouldNotContain(sensitive);
        using var state = JsonDocument.Parse(record.State);
        Dictionary<string, string?> fields = state.RootElement.EnumerateArray().ToDictionary(
            item => item.GetProperty("Key").GetString()!, item => item.GetProperty("Value").GetString());
        fields.Keys.Order().ShouldBe(new[] { "Projection", "TraceId", "{OriginalFormat}" }.Order());
        fields["Projection"].ShouldBe(handlerName == "index" ? "candidate-index" : "activity-catalog");
        fields["TraceId"].ShouldBe(trace.TraceId.ToHexString());
    }

    /// <summary>Preserves caller cancellation without emitting a storage-outage record.</summary>
    /// <param name="handlerName">The canonical live handler.</param>
    [Theory]
    [InlineData("index")]
    [InlineData("catalog")]
    public async Task CallerCancellationDoesNotProduceStorageFailureDiagnostics(string handlerName)
    {
        using var source = new CancellationTokenSource();
        using var log = new DeliveryOutcomeLogger();
        var store = new ScriptedReadModelStore { CancelBeforeTrySave = source };

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            if (handlerName == "index")
                await new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, log)
                    .ProjectAsync(ValidRequest(handlerName), "dispatch", source.Token);
            else
                await new TenantActivityTypeCatalogProjectionHandler(store, log)
                    .ProjectAsync(ValidRequest(handlerName), "dispatch", source.Token);
        });

        log.Records.ShouldBeEmpty();
    }

    /// <summary>Refuses invalid conditional writes independently for each component and key.</summary>
    [Fact]
    public async Task ScriptedStoreIsolatesComponentsAndRejectsStaleOrForeignETags()
    {
        const string key = "same-key";
        var store = new ScriptedReadModelStore();
        var first = new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.StatusOnly(ProjectionFreshnessState.Fresh));
        var second = first with { ProjectionFreshness = ProjectionFreshnessMetadata.StatusOnly(ProjectionFreshnessState.Stale) };
        CancellationToken ct = TestContext.Current.CancellationToken;

        (await store.TrySaveAsync("one", key, first, string.Empty, ct)).ShouldBeTrue();
        string firstETag = store.ETagFor(key, "one");
        (await store.GetAsync<ActivityTypeCatalogReadModel>("two", key, ct)).Value.ShouldBeNull();
        (await store.TrySaveAsync("two", key, second, firstETag, ct)).ShouldBeFalse();
        (await store.TrySaveAsync("two", key, second, string.Empty, ct)).ShouldBeTrue();
        string secondETag = store.ETagFor(key, "two");
        (await store.TrySaveAsync("one", key, second, secondETag, ct)).ShouldBeFalse();
        (await store.TrySaveAsync("one", key, second, string.Empty, ct)).ShouldBeFalse();
        store.ETagFor(key, "one").ShouldBe(firstETag);
        store.Get<ActivityTypeCatalogReadModel>(key, "one").ShouldBeSameAs(first);
        (await store.TrySaveAsync("one", key, second, firstETag, ct)).ShouldBeTrue();
        string updatedETag = store.ETagFor(key, "one");
        (await store.TrySaveAsync("one", key, first, firstETag, ct)).ShouldBeFalse();
        store.ETagFor(key, "one").ShouldBe(updatedETag);
        store.Get<ActivityTypeCatalogReadModel>(key, "one").ShouldBeSameAs(second);
        store.ETagFor(key, "two").ShouldBe(secondETag);
        store.Get<ActivityTypeCatalogReadModel>(key, "two").ShouldBeSameAs(second);
        await store.SaveAsync("two", key, first, ct);
        store.Get<ActivityTypeCatalogReadModel>(key, "two").ShouldBeSameAs(first);
        store.Get<ActivityTypeCatalogReadModel>(key, "one").ShouldBeSameAs(second);
    }

    /// <summary>Keeps storage failure retryable when diagnostic emission itself fails.</summary>
    /// <param name="handlerName">The canonical live handler.</param>
    [Theory]
    [InlineData("index")]
    [InlineData("catalog")]
    public async Task BrokenDiagnosticProviderDoesNotChangeRetryableOutcome(string handlerName)
    {
        var store = new ScriptedReadModelStore { GetException = new IOException("State unavailable.") };
        using var log = new DeliveryOutcomeLogger { Failure = new IOException("Logger unavailable.") };

        DomainProjectionHandlerResult result = handlerName == "index"
            ? await new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, log)
                .ProjectAsync(ValidRequest(handlerName), "dispatch", TestContext.Current.CancellationToken)
            : await new TenantActivityTypeCatalogProjectionHandler(store, log)
                .ProjectAsync(ValidRequest(handlerName), "dispatch", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Rejects a foreign ETag even when component and key contain the same delimiters.</summary>
    [Fact]
    public async Task ScriptedStoreETagsHaveUnambiguousComponentBoundaries()
    {
        var store = new ScriptedReadModelStore();
        var value = new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.StatusOnly(ProjectionFreshnessState.Fresh));
        CancellationToken ct = TestContext.Current.CancellationToken;
        await store.SaveAsync("one/two", "key", value, ct);
        await store.SaveAsync("one", "two/key", value, ct);
        string before = store.ETagFor("two/key", "one");

        (await store.TrySaveAsync("one", "two/key", value, store.ETagFor("key", "one/two"), ct)).ShouldBeFalse();

        store.ETagFor("two/key", "one").ShouldBe(before);
    }
}
