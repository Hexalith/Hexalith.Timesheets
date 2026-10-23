using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class EventStoreMagicLinkConfirmationCapabilityStateLoaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownAuthoritySuffixDeniesWhileKnownAliasesAndRejectionNoOpsSurvive(bool ownerSuffix)
    {
        StreamReadEvent issued = Event(1, "issued", new StoredMagicLinkIssued(Issued()));
        StreamReadEvent recorded = Event(1, "recorded", new StoredTimeEntryRecorded(Recorded())) with
        { EventTypeName = typeof(StoredTimeEntryRecorded).AssemblyQualifiedName! };
        StreamReadEvent noOp = Event(2, "rejection", new MagicLinkCommitRejected());
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued, noOp)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, recorded, noOp);
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));
        (await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken))
            .CapabilityState.ShouldNotBeNull();
        StreamReadEvent unknown = Event(3, "future", new MagicLinkCommitRejected()) with
        { EventTypeName = "FutureAuthorityEvent" };
        if (ownerSuffix)
            gateway.WithStream(Tenant().TenantId, TimeEntryId().Value, recorded, noOp, unknown);
        else
            gateway.WithStream(Tenant().TenantId, CapabilityId().Value, issued, noOp, unknown);
        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("events")]
    [InlineData("bytes")]
    [InlineData("duplicates")]
    public async Task InvocationAllowanceChargesRepeatedStreamsAtExactAndOverBoundaries(string dimension)
    {
        StreamReadEvent issued = Event(1, "issued", Issued());
        StreamReadEvent recorded = Event(1, "recorded", Recorded());
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, dimension == "duplicates" ? [issued, issued] : [issued])
            .WithStream(Tenant().TenantId, TimeEntryId().Value, recorded);
        var options = new MagicLinkStateLoadingOptions
        { MaxPages = 3, MaxEvents = dimension == "duplicates" ? 4 : 3,
            MaxPayloadBytes = (dimension == "duplicates" ? 2L : 1L) * issued.Payload.Length + 2L * recorded.Payload.Length };
        EventStoreMagicLinkConfirmationCapabilityStateLoader Loader() => new(gateway,
            new InMemoryReadModelStore(IndexWith(Hash())), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"), allowLegacyIndexRead: true,
            stateLoadingOptions: Options.Create(options));
        var loader = Loader();
        (await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)).CapabilityState.ShouldNotBeNull();
        // Each invocation starts a new allowance even on the same scoped loader.
        (await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)).TimeEntryState.ShouldNotBeNull();
        if (dimension == "pages") options.MaxPages--;
        if (dimension is "events" or "duplicates") options.MaxEvents--;
        if (dimension == "bytes") options.MaxPayloadBytes--;
        ShouldBeOpaqueFailClosed(await Loader().LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdvancingGrowingHistoryStopsAtTheSharedPageAllowance()
    {
        var gateway = new ScriptedGatewayClient().WithPagedStreamLatestSequences(Tenant().TenantId,
            CapabilityId().Value, [2, 3, 4, 5],
            [Event(1, "issued", Issued())], [Event(2, "reject-2", new MagicLinkCommitRejected())],
            [Event(3, "reject-3", new MagicLinkCommitRejected())], [Event(4, "reject-4", new MagicLinkCommitRejected())]);
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(IndexWith(Hash())), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"), allowLegacyIndexRead: true,
            stateLoadingOptions: Options.Create(new MagicLinkStateLoadingOptions { MaxPages = 3 }));
        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
        gateway.WorkloadRequests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CumulativeSuccessfulReadsShareOneInvocationDeadline()
    {
        var scripted = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "issued", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "recorded", Recorded()));
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        int completedReads = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(800), call.Arg<CancellationToken>());
                Interlocked.Increment(ref completedReads);
                return await scripted.ReadWorkloadStreamAsync(call.Arg<StreamReadRequest>(), call.Arg<CancellationToken>());
            });
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(IndexWith(Hash())), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"), allowLegacyIndexRead: true,
            stateLoadingOptions: Options.Create(new MagicLinkStateLoadingOptions { Timeout = TimeSpan.FromSeconds(2) }));

        MagicLinkEndpointTokenState result = await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(result);
        completedReads.ShouldBeLessThan(3);
    }

    [Theory]
    [InlineData("capability", false)]
    [InlineData("capability", true)]
    [InlineData("owner", false)]
    [InlineData("owner", true)]
    [InlineData("entry", false)]
    [InlineData("entry", true)]
    [InlineData("catalog", false)]
    [InlineData("catalog", true)]
    public async Task LatePublicationRefusesExpiredAllowanceAndPropagatesCallerCancellation(string boundary, bool callerCancels)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bool reachedBoundary = false;
        void ReachBoundary()
        {
            reachedBoundary = true;
            if (callerCancels) caller.Cancel();
            else Thread.Sleep(600);
        }
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "issued", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "recorded", Recorded()));
        if (boundary == "capability")
            gateway.WithPageTransform(Tenant().TenantId, CapabilityId().Value,
                page => page with { Events = new LateMaterializationEvents(page.Events, ReachBoundary) });
        int ownerReads = 0;
        gateway.WithPageTransform(Tenant().TenantId, TimeEntryId().Value, page =>
        {
            ownerReads++;
            return boundary == "owner" && ownerReads == 1 || boundary == "entry" && ownerReads == 2
                ? page with { Events = new LateMaterializationEvents(page.Events, ReachBoundary) } : page;
        });
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()),
            beforeCatalogReturn: boundary == "catalog" ? ReachBoundary : null);
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, readModels,
            new DeterministicTokenGenerator(), new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"),
            allowLegacyIndexRead: true, stateLoadingOptions: Options.Create(new MagicLinkStateLoadingOptions
            { Timeout = callerCancels ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(500) }));
        async Task<object?> ResolveAsync()
        {
            if (boundary == "capability")
            {
                var replay = (Task<Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState?>)typeof(EventStoreMagicLinkConfirmationCapabilityStateLoader)
                    .GetMethod("LoadCapabilityStreamForTenantAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(loader, [Tenant(), CapabilityId(), caller.Token])!;
                return await replay;
            }
            if (boundary == "owner") return await loader.LoadCapabilityAsync(CapabilityId(), caller.Token);
            return await loader.LoadTokenStateAsync("opaque-once", caller.Token);
        }
        if (callerCancels)
            await Should.ThrowAsync<OperationCanceledException>(() => ResolveAsync());
        else
        {
            object? result = await ResolveAsync();
            if (boundary is "capability" or "owner") result.ShouldBeNull();
            else ShouldBeOpaqueFailClosed((MagicLinkEndpointTokenState)result!);
        }
        reachedBoundary.ShouldBeTrue();
        readModels.CatalogReadCount.ShouldBe(boundary == "catalog" ? 1 : 0);
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("events")]
    [InlineData("bytes")]
    [InlineData("timeout")]
    public async Task CallerOptionsMutationCannotExpandTheValidatedSnapshot(string dimension)
    {
        StreamReadEvent issued = Event(1, "issued", Issued());
        StreamReadEvent recorded = Event(1, "recorded", Recorded());
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, recorded);
        var options = new MagicLinkStateLoadingOptions
        {
            MaxPages = dimension == "pages" ? 2 : 3,
            MaxEvents = dimension == "events" ? 2 : 3,
            MaxPayloadBytes = issued.Payload.Length + 2L * recorded.Payload.Length - (dimension == "bytes" ? 1 : 0),
            Timeout = dimension == "timeout" ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(5)
        };
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(IndexWith(Hash()), beforeCatalogReturn: dimension == "timeout" ? () => Thread.Sleep(600) : null),
            new DeterministicTokenGenerator(), new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"),
            allowLegacyIndexRead: true, stateLoadingOptions: Options.Create(options));
        options.MaxPages = int.MaxValue;
        options.MaxEvents = int.MaxValue;
        options.MaxPayloadBytes = long.MaxValue;
        options.Timeout = TimeSpan.FromDays(1);

        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonCooperativeStreamReadIsBoundedAndCallerCancellationPropagates(bool callerCancels)
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        var pending = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => { started.TrySetResult(); return pending.Task; });
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(IndexWith(Hash())), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"), allowLegacyIndexRead: true,
            stateLoadingOptions: Options.Create(new MagicLinkStateLoadingOptions
            { Timeout = callerCancels ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(75) }));
        Task<MagicLinkEndpointTokenState> resolution = loader.LoadTokenStateAsync("opaque-once", caller.Token).AsTask();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            if (callerCancels)
            {
                caller.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => resolution.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            }
            else
            {
                ShouldBeOpaqueFailClosed(await resolution.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(375));
            }
        }
        finally
        {
            pending.TrySetCanceled(TestContext.Current.CancellationToken);
            _ = await Record.ExceptionAsync(() => pending.Task);
        }

    }

    [Theory]
    [InlineData("index-late-fault", false)]
    [InlineData("index", false)]
    [InlineData("index", true)]
    [InlineData("coordinator", false)]
    [InlineData("coordinator", true)]
    [InlineData("tree", false)]
    [InlineData("tree", true)]
    [InlineData("catalog", false)]
    [InlineData("catalog", true)]
    [InlineData("standalone-catalog", false)]
    [InlineData("standalone-catalog", true)]
    public async Task NonCooperativeReadModelsShareInvocationDeadlineAndPropagateCallerCancellation(string boundary, bool callerCancels)
    {
        var inner = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        inner.SeedRaw(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, IndexWith(Hash()));
        inner.SeedRaw(MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName,
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()),
            new ActivityTypeCatalogReadModel([new ActivityTypeCatalogItem(ActivityId(), ActivityTypeScope.Tenant,
                null, "Delivery", true, BillableState.Billable)], ProjectionFreshnessMetadata.Fresh));
        var readModels = new NonCooperativeReadModelStore(inner);
        SharedProjectionEpochCoordinator? coordinator = null;
        if (boundary is "coordinator" or "tree")
        {
            coordinator = new SharedProjectionEpochCoordinator(readModels, inner);
            SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
            _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
                TestContext.Current.CancellationToken);
            long epoch = await coordinator.BeginAsync(scope, "deadline-tree", "inventory", new Dictionary<string, long>(),
                TestContext.Current.CancellationToken);
            var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(
                new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope),
                new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null), epoch);
            await builder.AddContributionAsync(new(Hash().Value, new(Tenant(), CapabilityId())), TestContext.Current.CancellationToken);
            _ = await coordinator.StageAsync(scope, "deadline-tree", [.. builder.Operations()], TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, "deadline-tree", TestContext.Current.CancellationToken);
        }
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        readModels.BeforeReadAsync = async (type, key) =>
        {
            if (boundary == "catalog" && !callerCancels
                && type == typeof(MagicLinkTokenHashCapabilityIndexReadModel))
                await Task.Delay(TimeSpan.FromMilliseconds(650), TestContext.Current.CancellationToken);
            bool stalls = boundary switch
            {
                "index" or "index-late-fault" => type == typeof(MagicLinkTokenHashCapabilityIndexReadModel),
                "coordinator" => key.StartsWith("shared-epoch:", StringComparison.Ordinal),
                "tree" => type == typeof(MagicLinkTokenHashCapabilityIndexTreeNode),
                _ => type == typeof(ActivityTypeCatalogReadModel)
            };
            if (!stalls) return;
            started.TrySetResult();
            await release.Task; // Intentionally ignores the dependency cancellation token.
            if (boundary == "index-late-fault") throw new IOException("Late dependency failure after deadline.");
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "issued", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "recorded", Recorded()));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, readModels,
            new DeterministicTokenGenerator(), new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"),
            coordinator, allowLegacyIndexRead: true,
            stateLoadingOptions: Options.Create(new MagicLinkStateLoadingOptions
            { MaxPages = 3, Timeout = callerCancels ? TimeSpan.FromSeconds(5)
                : boundary == "catalog" ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(150) }));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        async Task ResolveAsync()
        {
            if (boundary == "standalone-catalog")
            {
                var result = await loader.LoadActivityTypeCatalogAsync(caller.Token);
                result.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
                result.Items.ShouldBeEmpty();
            }
            else ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", caller.Token));
        }
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Task resolution = ResolveAsync();
        Task? abandoned = null;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            abandoned = readModels.LastRead.ShouldNotBeNull();
            if (callerCancels)
            {
                caller.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => resolution.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            }
            else
            {
                await resolution.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                // A 300 ms scheduling allowance still rejects a one-second timeout regression.
                elapsed.Elapsed.ShouldBeLessThan(boundary == "catalog" ? TimeSpan.FromMilliseconds(1_300) : TimeSpan.FromMilliseconds(450));
            }
        }
        finally
        {
            readModels.BeforeReadAsync = null;
            abandoned ??= readModels.LastRead;
            release.TrySetResult();
            if (abandoned is not null)
            {
                Exception? late = await Record.ExceptionAsync(() => abandoned.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                if (boundary == "index-late-fault") late.ShouldBeOfType<IOException>();
                else late.ShouldBeNull();
            }
        }
        if (boundary == "standalone-catalog")
            (await loader.LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)).ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        else
            (await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)).CapabilityState.ShouldNotBeNull();
    }

    /// <summary>The pure candidate helper deduplicates issuances and retains every ambiguous owner.</summary>
    [Fact]
    public void IssuanceCandidateRebuildIsDeterministicAndPreservesAmbiguity()
    {
        MagicLinkConfirmationCapabilityIssued first = Issued();
        MagicLinkConfirmationCapabilityIssued otherTenant = Issued(tenant: new TenantReference("tenant-b"));
        MagicLinkConfirmationCapabilityIssued otherCapability = Issued(capabilityId: new MagicLinkCapabilityId("capability-2"));
        MagicLinkConfirmationCapabilityIssued unique = Issued(hash: new MagicLinkTokenHash("unique-hash"));
        MagicLinkTokenHashCapabilityIndexReadModel rebuilt =
            MagicLinkTokenHashCapabilityIndexProjection.Rebuild([first, unique, otherTenant, otherCapability, first]);
        MagicLinkTokenHashCapabilityIndexReadModel reordered =
            MagicLinkTokenHashCapabilityIndexProjection.Rebuild([otherTenant, otherCapability, unique, first]);

        JsonSerializer.Serialize(rebuilt).ShouldBe(JsonSerializer.Serialize(reordered));
        rebuilt.Entries.Keys.ShouldBe(["unique-hash"]);
        rebuilt.Entries["unique-hash"].ShouldBe(new(Tenant(), CapabilityId()));
        rebuilt.Contributions[Hash().Value].ShouldBe([
            new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), CapabilityId()),
            new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), otherCapability.CapabilityId),
            new MagicLinkTokenHashCapabilityIndexEntry(otherTenant.Tenant, CapabilityId())
        ]);
        MagicLinkTokenHashCapabilityIndexProjection.Rebuild([]).Entries.ShouldBeEmpty();
        Should.Throw<ArgumentNullException>(() => MagicLinkTokenHashCapabilityIndexProjection.Rebuild(null!));
    }

    /// <summary>Emits bounded diagnostic fields while retaining the opaque failure bundle.</summary>
    /// <param name="operation">The failing logical read.</param>
    /// <param name="fault">The injected exception category.</param>
    /// <param name="category">The fixed expected outcome.</param>
    [Theory]
    [InlineData("index", "transport", "dependency-unavailable")]
    [InlineData("catalog", "transport", "dependency-unavailable")]
    [InlineData("capability", "transport", "dependency-unavailable")]
    [InlineData("entry", "transport", "dependency-unavailable")]
    [InlineData("index", "json", "invalid-content")]
    [InlineData("catalog", "json", "invalid-content")]
    [InlineData("capability", "integrity", "integrity-failure")]
    [InlineData("entry", "integrity", "integrity-failure")]
    [InlineData("index", "timeout", "dependency-timeout")]
    [InlineData("entry", "timeout", "dependency-timeout")]
    public async Task ReadFailuresLogOnlySafeOperationCategoryAndPlatformTrace(
        string operation, string fault, string category)
    {
        const string sensitive = "private-identity-payload-and-token";
        Exception failure = fault switch
        {
            "transport" => new HttpRequestException(sensitive),
            "json" => new JsonException(sensitive),
            "timeout" => new TaskCanceledException(sensitive),
            _ => new InvalidOperationException(sensitive)
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "issuance", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "recorded", Recorded()));
        if (operation == "capability")
            gateway.WithException(Tenant().TenantId, CapabilityId().Value, failure);
        if (operation == "entry")
        {
            // The capability fold first reads its owner's terminal history. Fail the subsequent
            // effective-entry replay to verify that distinct operation's diagnostics.
            int ownerReads = 0;
            gateway.WithPageTransform(Tenant().TenantId, TimeEntryId().Value, page =>
                ++ownerReads == 2 ? throw failure : page);
        }
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()),
            indexException: operation == "index" ? failure : null,
            catalogException: operation == "catalog" ? failure : null);
        ILogger logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ILoggerFactory factory = Substitute.For<ILoggerFactory>();
        factory.CreateLogger(Arg.Any<string>()).Returns(logger);
        using var trace = new Activity("read-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, readModels,
            new DeterministicTokenGenerator(), new FixedTrustedContextAccessor(Tenant(), Operator(), sensitive),
            allowLegacyIndexRead: true, loggerFactory: factory);

        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));

        object?[] arguments = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log")
            .ShouldHaveSingleItem().GetArguments();
        arguments[0].ShouldBe(LogLevel.Warning);
        ((EventId)arguments[1]!).Id.ShouldBe(1301);
        arguments[3].ShouldBeNull();
        var state = ((IEnumerable<KeyValuePair<string, object?>>)arguments[2]!).ToDictionary();
        state.Keys.Order().ShouldBe(new[] { "Operation", "Outcome", "TraceId", "{OriginalFormat}" }.Order());
        state["Operation"].ShouldBe(operation + "-read");
        state["Outcome"].ShouldBe(category);
        state["TraceId"].ShouldBe(trace.TraceId.ToHexString());
        JsonSerializer.Serialize(state).ShouldNotContain(sensitive);
        arguments[2]!.ToString()!.ShouldNotContain(sensitive);
    }

    /// <summary>Exercises wrapped production-epoch outages and failure of diagnostic emission.</summary>
    /// <param name="providerFails">Whether diagnostic emission also fails.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionEpochReadFailureStaysOpaqueEvenWhenDiagnosticProviderFails(bool providerFails)
    {
        const string sensitive = "private-epoch-transport-material";
        var readModels = new InMemoryReadModelStore(null, epochException: new IOException(sensitive));
        var coordinator = new SharedProjectionEpochCoordinator(readModels,
            new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore());
        ILogger logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(_ => providerFails
            ? throw new IOException("Diagnostic provider unavailable.") : true);
        ILoggerFactory factory = Substitute.For<ILoggerFactory>();
        factory.CreateLogger(Arg.Any<string>()).Returns(logger);
        var gateway = new ScriptedGatewayClient();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, readModels,
            new DeterministicTokenGenerator(), new FixedTrustedContextAccessor(Tenant(), Operator(), sensitive),
            coordinator, loggerFactory: factory);

        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));

        gateway.Requests.ShouldBeEmpty();
        if (!providerFails)
        {
            object?[] arguments = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log")
                .ShouldHaveSingleItem().GetArguments();
            arguments[3].ShouldBeNull();
            var state = ((IEnumerable<KeyValuePair<string, object?>>)arguments[2]!).ToDictionary();
            state.Keys.Order().ShouldBe(new[] { "Operation", "Outcome", "TraceId", "{OriginalFormat}" }.Order());
            state["Operation"].ShouldBe("index-read");
            state["Outcome"].ShouldBe("dependency-unavailable");
            JsonSerializer.Serialize(state).ShouldNotContain(sensitive);
        }
    }

    /// <summary>Keeps expected invalid-link and cancellation paths outside dependency diagnostics.</summary>
    [Fact]
    public async Task CallerCancellationAndUnknownLinksDoNotLogDependencyFailures()
    {
        ILogger logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ILoggerFactory factory = Substitute.For<ILoggerFactory>();
        factory.CreateLogger(Arg.Any<string>()).Returns(logger);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var gateway = new ScriptedGatewayClient().WithException(Tenant().TenantId, CapabilityId().Value,
            new OperationCanceledException(source.Token));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(IndexWith(Hash())), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"),
            allowLegacyIndexRead: true, loggerFactory: factory);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await loader.LoadTokenStateAsync("opaque-once", source.Token));
        ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync(string.Empty, TestContext.Current.CancellationToken));
        var unknown = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            new InMemoryReadModelStore(null), new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation"),
            allowLegacyIndexRead: true, loggerFactory: factory);
        ShouldBeOpaqueFailClosed(await unknown.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
        logger.ReceivedCalls().ShouldNotContain(call => call.GetMethodInfo().Name == "Log");
    }

    [Fact]
    public async Task NumericIndexPromotionDuringCandidateResolutionFailsClosed()
    {
        var inner = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        var hooked = new PromotionHookReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(hooked, inner);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry first = new(Hash().Value,
            new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), CapabilityId()));
        MagicLinkTokenHashCapabilityIndexDirectoryEntry second = new(Hash().Value,
            new MagicLinkTokenHashCapabilityIndexEntry(new TenantReference("tenant-2"),
                new MagicLinkCapabilityId("capability-2")));

        async Task PromoteAsync(string operation,
            MagicLinkTokenHashCapabilityIndexDirectoryEntry row)
        {
            _ = await coordinator.BeginAsync(scope, operation, "inventory-" + operation,
                new Dictionary<string, long>(), TestContext.Current.CancellationToken);
            MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
                .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                    MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                    TestContext.Current.CancellationToken)).Value
                ?? new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null);
            SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope,
                TestContext.Current.CancellationToken);
            var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(
                new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope), root,
                status.Epoch);
            await builder.AddContributionAsync(row, TestContext.Current.CancellationToken);
            _ = await coordinator.StageAsync(scope, operation, [.. builder.Operations()],
                TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, operation, TestContext.Current.CancellationToken);
        }

        await PromoteAsync("numeric-first", first);
        bool promoted = false;
        hooked.BeforeReadAsync = async (type, _) =>
        {
            if (type != typeof(MagicLinkTokenHashCapabilityIndexTreeNode) || promoted) return;
            promoted = true;
            hooked.BeforeReadAsync = null;
            await PromoteAsync("numeric-second", second);
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, hooked,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"), coordinator);

        MagicLinkEndpointTokenState state = await loader.LoadTokenStateAsync(
            "opaque-once", TestContext.Current.CancellationToken);

        promoted.ShouldBeTrue();
        state.CapabilityState.ShouldBeNull();
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedIndexPromotionDuringCandidateResolutionFailsClosed(bool directoryCandidate)
    {
        MagicLinkTokenHash hash = Hash();
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash.Value);
        var first = new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), CapabilityId());
        var second = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-2"), new MagicLinkCapabilityId("capability-2"));
        var oldPage = new MagicLinkTokenHashCapabilityIndexDirectoryPage(
            [new MagicLinkTokenHashCapabilityIndexDirectoryEntry(hash.Value, first)]);
        MagicLinkTokenHashCapabilityIndexReadModel oldRoot = directoryCandidate
            ? MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                DirectoryPages = [new MagicLinkTokenHashCapabilityIndexDirectoryReference(0,
                    hash.Value, hash.Value)],
                DirectoryPageDigests = new Dictionary<int, string>
                {
                    [0] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(oldPage)
                },
                NextCandidatePageId = 1
            }
            : MagicLinkTokenHashCapabilityIndexProjection.FromContributions([new(hash.Value, first)]);
        MagicLinkTokenHashCapabilityIndexReadModel ambiguous =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new(hash.Value, first), new(hash.Value, second)]);
        var inner = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        var hooked = new PromotionHookReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(hooked, inner);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "selected-first", "inventory-first",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        List<ReadModelBatchOperation> initial =
        [
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                oldRoot, ReadModelBatchConcurrency.LastWrite)
        ];
        if (directoryCandidate)
            initial.Add(ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 0),
                oldPage, ReadModelBatchConcurrency.LastWrite));
        _ = await coordinator.StageAsync(scope, "selected-first", initial,
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "selected-first", TestContext.Current.CancellationToken);
        int rootReads = 0;
        bool promoted = false;
        hooked.BeforeReadAsync = async (type, key) =>
        {
            bool trigger = directoryCandidate
                ? type == typeof(MagicLinkTokenHashCapabilityIndexDirectoryPage)
                : type == typeof(MagicLinkTokenHashCapabilityIndexReadModel) && ++rootReads == 2;
            if (!trigger || promoted) return;
            promoted = true;
            _ = await coordinator.BeginAsync(scope, "selected-second", "inventory-second",
                new Dictionary<string, long>(), TestContext.Current.CancellationToken);
            _ = await coordinator.StageAsync(scope, "selected-second",
                [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                    ambiguous, ReadModelBatchConcurrency.LastWrite)],
                TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, "selected-second", TestContext.Current.CancellationToken);
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, hooked,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"), coordinator);

        MagicLinkEndpointTokenState state = await loader.LoadTokenStateAsync(
            "opaque-once", TestContext.Current.CancellationToken);

        promoted.ShouldBeTrue();
        state.CapabilityState.ShouldBeNull();
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadTokenStateAsyncResolvesIndexAndFoldsCapabilityTimeEntryAndCatalog()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.CapabilityState.TokenHash.ShouldBe(Hash());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        state.ActivityTypeCatalog.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(ActivityId());
        gateway.Requests.Select(static request => request.AggregateId).ShouldBe(
            [CapabilityId().Value, TimeEntryId().Value, TimeEntryId().Value]);
    }

    [Fact]
    public async Task LoadTokenStateAsyncAcceptsExactFullyQualifiedProductionEventNames()
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued()) with
        {
            EventTypeName = typeof(MagicLinkConfirmationCapabilityIssued).FullName!
        };
        StreamReadEvent recorded = Event(1, "time-1", Recorded()) with
        {
            EventTypeName = typeof(TimeEntryRecorded).FullName!
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, recorded);

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
    }

    [Fact]
    public async Task LoadTokenStateAsyncUsesFoldedCapabilityStateAsSingleUseAuthority()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
        state.TimeEntryState.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenIndexHasNoHash()
    {
        var gateway = new ScriptedGatewayClient();
        var readModels = new InMemoryReadModelStore(new MagicLinkTokenHashCapabilityIndexReadModel(
            new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldBeNull();
        state.TimeEntryState.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForLegacyIndexWithoutOwnerContributions()
    {
        MagicLinkTokenHashCapabilityIndexReadModel canonical = IndexWith(Hash());
        var legacy = new MagicLinkTokenHashCapabilityIndexReadModel(canonical.Entries);
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(legacy));

        MagicLinkEndpointTokenState state = await loader.LoadTokenStateAsync("opaque-once",
            TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldBeNull();
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForConflictingUniqueAndPagedIndex()
    {
        MagicLinkTokenHashCapabilityIndexReadModel conflicting = IndexWith(Hash()) with
        {
            CandidatePageCounts = new Dictionary<string, int> { [Hash().Value] = 1 },
            CandidatePageIds = new Dictionary<string, IReadOnlyList<int>> { [Hash().Value] = [0] },
            NextCandidatePageId = 1
        };
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(conflicting));

        MagicLinkEndpointTokenState state = await loader.LoadTokenStateAsync("opaque-once",
            TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldBeNull();
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForHashAddressedPageManifest()
    {
        MagicLinkTokenHashCapabilityIndexReadModel legacyPaged = IndexWith(Hash()) with
        {
            CandidatePageCounts = new Dictionary<string, int> { [Hash().Value] = 1 }
        };
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(legacyPaged));

        MagicLinkEndpointTokenState state = await loader.LoadTokenStateAsync("opaque-once",
            TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldBeNull();
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void LegacyUniqueOnlyIndexRetainsUnverifiedMarkerAcrossLiveAndTenantRebuild()
    {
        var legacy = new MagicLinkTokenHashCapabilityIndexReadModel(IndexWith(Hash()).Entries);
        MagicLinkTokenHashCapabilityIndexReadModel delivered =
            MagicLinkTokenHashCapabilityIndexProjection.Apply(legacy,
                Issued(new MagicLinkCapabilityId("capability-2"), new MagicLinkTokenHash("hash-two")));
        delivered.LegacyContributionsUnverified.ShouldBeTrue();

        MagicLinkTokenHashCapabilityIndexReadModel rebuilt =
            MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(delivered, Tenant().TenantId,
                MagicLinkTokenHashCapabilityIndexProjection.Rebuild([Issued()]));
        rebuilt.LegacyContributionsUnverified.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadCapabilityAsyncUsesTrustedAmbientTenantForAdminPaths()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldNotBeNull().Tenant.ShouldBe(Tenant());
        gateway.Requests.Count.ShouldBe(2);
        gateway.Requests[0].Tenant.ShouldBe(Tenant().TenantId);
        gateway.Requests[1].AggregateId.ShouldBe(TimeEntryId().Value);
    }

    [Theory]
    [InlineData(StreamReplayReasonCodes.MissingStream, true)]
    [InlineData(StreamReplayReasonCodes.MissingEvent, false)]
    [InlineData(null, false)]
    public async Task ProposedEntryOwnerAcceptsOnlyExplicitMissingStreamReason(
        string? reasonCode, bool expectedCapability)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithException(Tenant().TenantId, TimeEntryId().Value,
                new EventStoreGatewayException(404, "Not Found", reasonCode: reasonCode));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await
            CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
                .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken);

        (state is not null).ShouldBe(expectedCapability);
    }

    [Fact]
    public async Task RepeatedPhysicalIssuanceAtDistinctSequencesFailsClosed()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value,
                Event(1, "first-issue", Issued()), Event(2, "second-issue", Issued()));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await
            CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
                .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken);

        state.ShouldBeNull();
    }

    /// <summary>Rejects a missing capability identifier before request-context access or persistence I/O.</summary>
    [Fact]
    public async Task LoadCapabilityAsyncReturnsNullWithoutIoForNullIdentifier()
    {
        var gateway = new ScriptedGatewayClient();
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        ITimesheetsTrustedContextAccessor context = Substitute.For<ITimesheetsTrustedContextAccessor>();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(
            gateway, readModels, new DeterministicTokenGenerator(), context,
            allowLegacyIndexRead: true);

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(null!, TestContext.Current.CancellationToken);

        state.ShouldBeNull();
        context.ReceivedCalls().ShouldBeEmpty();
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(0);
    }

    [Fact]
    public void TokenHashIndexRebuildsFromIssuedEventsWithoutRawTokenMaterial()
    {
        MagicLinkTokenHashCapabilityIndexReadModel index = MagicLinkTokenHashCapabilityIndexProjection.Rebuild(
        [
            Issued(),
            Issued(new MagicLinkCapabilityId("capability-2"), new MagicLinkTokenHash("hash-two"))
        ]);

        index.Entries[Hash().Value].CapabilityId.ShouldBe(CapabilityId());
        index.Entries["hash-two"].CapabilityId.ShouldBe(new MagicLinkCapabilityId("capability-2"));

        string json = JsonSerializer.Serialize(index, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.ShouldNotContain("opaque-once");
        json.ShouldNotContain("oneTimeToken", Case.Insensitive);
        json.ShouldContain("hash-only");
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenCapabilityAggregateIsMissing()
    {
        // The index resolves a candidate, but the capability stream folds to no state.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenFoldedTokenHashDoesNotMatchCandidate()
    {
        // The index points at a capability whose authoritative folded TokenHash differs from the
        // hash derived from the presented token. The candidate is non-authoritative; the fold wins.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued(hash: new MagicLinkTokenHash("rotated-hash"))))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenFoldedCapabilityTenantDiffersFromCandidate()
    {
        // Cross-tenant: the candidate tenant resolves the stream, but the folded capability carries a
        // different tenant. The loader must not bridge tenants.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued(tenant: new TenantReference("tenant-2"))))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenScopedTimeEntryIsNotRecorded()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncRejectsMismatchedActivityTypeIds()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeId: new ActivityTypeId("activity-type-2"))));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncAcceptsNewCorrectionFromProjectToTenantScope()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "time-2", Submitted(Tenant())),
                Event(3, "time-3", Rejected()),
                Event(4, "time-4", Corrected(ActivityTypeScope.Tenant)));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull();
        state.TimeEntryState.ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public async Task LoadTokenStateAsyncRejectsLegacyCorrectionThatRetainsProjectScope()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "time-2", Submitted(Tenant())),
                Event(3, "time-3", Rejected()),
                Event(4, "time-4", Corrected(null)));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncPreservesSerializedApprovedCorrectionScopeLineage()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "time-2", Submitted(Tenant())),
                Event(3, "time-3", Approved()),
                Event(4, "time-4", ApprovedCorrected()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        TimeEntryState timeEntry = state.TimeEntryState.ShouldNotBeNull();
        timeEntry.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        timeEntry.PreviousValues.ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        timeEntry.CorrectedValues.ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
    }

    [Fact]
    public async Task LoadTokenStateAsyncPreservesSerializedAdjustmentScopeAgreement()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Adjusted()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        TimeEntryState timeEntry = state.TimeEntryState.ShouldNotBeNull();
        timeEntry.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        timeEntry.ExternalAdjustment.ShouldNotBeNull().AdjustedValues.ActivityTypeScope
            .ShouldBe(timeEntry.ActivityTypeScope);
        timeEntry.ExternalAdjustment.PreviousValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);

        var legacyGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Adjusted(null, null)));

        MagicLinkEndpointTokenState legacyState = await CreateLoader(
                legacyGateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        TimeEntryState legacyTimeEntry = legacyState.TimeEntryState.ShouldNotBeNull();
        legacyTimeEntry.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        legacyTimeEntry.ExternalAdjustment.ShouldNotBeNull().PreviousValues.ActivityTypeScope.ShouldBeNull();
        legacyTimeEntry.ExternalAdjustment.AdjustedValues.ActivityTypeScope.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTokenStateAsyncRejectsSerializedAdjustmentScopeDisagreement()
    {
        (ActivityTypeScope? PreviousScope, ActivityTypeScope? AdjustedScope)[] disagreements =
        [
            (ActivityTypeScope.Project, ActivityTypeScope.Tenant),
            (ActivityTypeScope.Tenant, ActivityTypeScope.Project)
        ];

        foreach ((ActivityTypeScope? previousScope, ActivityTypeScope? adjustedScope) in disagreements)
        {
            var gateway = new ScriptedGatewayClient()
                .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
                .WithStream(
                    Tenant().TenantId,
                    TimeEntryId().Value,
                    Event(1, "time-1", Recorded()),
                    Event(2, "time-2", Adjusted(previousScope, adjustedScope)));

            MagicLinkEndpointTokenState state = await CreateLoader(
                    gateway,
                    new InMemoryReadModelStore(IndexWith(Hash())))
                .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

            ShouldBeOpaqueFailClosed(state);
        }
    }

    [Fact]
    public async Task LoadTokenStateAsyncFoldsSubmittedStateAndRejectsLaterWrongTenantEvent()
    {
        var validGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Submitted(Tenant())));
        var invalidGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Submitted(new TenantReference("tenant-2"))));

        MagicLinkEndpointTokenState valid = await CreateLoader(
                validGateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);
        MagicLinkEndpointTokenState invalid = await CreateLoader(
                invalidGateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        valid.TimeEntryState.ShouldNotBeNull().ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        ShouldBeOpaqueFailClosed(invalid);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFoldsStoredSubmittedWrapper()
    {
        TimeEntrySubmitted submitted = Submitted(Tenant());
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value,
                Event(1, "time-1", new StoredTimeEntryRecorded(Recorded())) with
                {
                    EventTypeName = typeof(StoredTimeEntryRecorded).FullName!
                },
                Event(2, "time-2", new StoredTimeEntrySubmitted(submitted)) with
                {
                    EventTypeName = typeof(StoredTimeEntrySubmitted).FullName!
                });

        MagicLinkEndpointTokenState loaded = await CreateLoader(
                gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        TimeEntryState state = loaded.TimeEntryState.ShouldNotBeNull();
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        state.TimeEntrySubmissionId.ShouldBe(submitted.TimeEntrySubmissionId);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenIndexReadThrows()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()), throwIndexRead: true);

        MagicLinkEndpointTokenState state = await CreateLoader(new ScriptedGatewayClient(), readModels)
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
        readModels.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenEventStoreReadThrows()
    {
        var gateway = new ScriptedGatewayClient()
            .WithThrow(Tenant().TenantId, CapabilityId().Value)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("foreign-suffix")]
    [InlineData("null-payload")]
    public async Task LoadTokenStateAsyncFailsClosedForUnrecognizedOrNullCapabilityPayload(string caseName)
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued());
        issued = caseName == "foreign-suffix"
            ? issued with { EventTypeName = $"Foreign.{typeof(MagicLinkConfirmationCapabilityIssued).FullName}" }
            : issued with { Payload = null! };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued);

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("time-entry")]
    public async Task LoadTokenStateAsyncFailsClosedWhenPayloadIdentityDiffersFromRequestedStream(string stage)
    {
        MagicLinkConfirmationCapabilityIssued issued = stage == "capability"
            ? Issued(new MagicLinkCapabilityId("capability-2"))
            : Issued();
        TimeEntryRecorded recorded = stage == "time-entry"
            ? Recorded(new TimeEntryId("time-entry-2"))
            : Recorded();
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", issued))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", recorded));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForTimeoutCancellationWithoutCallerCancellation()
    {
        var gateway = new ScriptedGatewayClient()
            .WithException(Tenant().TenantId, CapabilityId().Value, new OperationCanceledException("Timed out."));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncPropagatesGenuineCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var gateway = new ScriptedGatewayClient()
            .WithException(
                Tenant().TenantId,
                CapabilityId().Value,
                new OperationCanceledException(cancellation.Token));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await loader.LoadTokenStateAsync("opaque-once", cancellation.Token));
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForRecognizedEventWithUnsupportedSerialization()
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued()) with
        {
            SerializationFormat = "application/octet-stream"
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncReturnsUnavailableCatalogWhenCatalogReadThrows()
    {
        // Capability and Time Entry fold, but the catalog read fails. The loader surfaces an explicit
        // Unavailable freshness so the trust-bearing command service fails closed downstream — it never
        // returns a Fresh catalog it could not load.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash()), throwCatalogRead: true));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncReturnsFoldedRevokedStateProvingIndexIsNotRevocationAuthority()
    {
        // The index still resolves the candidate after revocation; revocation truth lives only in the
        // folded aggregate, which the downstream validators reject.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Revoked()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Revoked);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadTokenStateAsyncFoldsCapabilityDeterministicallyAcrossOrderingsAndDuplicates()
    {
        var ascending = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var shuffledWithDuplicates = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState first = await CreateLoader(ascending, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        MagicLinkEndpointTokenState second = await CreateLoader(shuffledWithDuplicates, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        first.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        second.CapabilityState.ShouldNotBeNull();
        second.CapabilityState.State.ShouldBe(first.CapabilityState.State);
        second.CapabilityState.IsTerminal.ShouldBe(first.CapabilityState.IsTerminal);
        second.CapabilityState.CapabilityId.ShouldBe(first.CapabilityState.CapabilityId);
        second.CapabilityState.TokenHash.ShouldBe(first.CapabilityState.TokenHash);
        second.CapabilityState.UsedAtUtc.ShouldBe(first.CapabilityState.UsedAtUtc);
        second.CapabilityState.ExpiresAtUtc.ShouldBe(first.CapabilityState.ExpiresAtUtc);
        second.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(first.TimeEntryState.ShouldNotBeNull().TimeEntryId);
    }

    [Fact]
    public async Task LoadTokenStateAsyncPreservesCompleteFoldedBundleAcrossOrderingAndDuplicateVariants()
    {
        var recordedComment = new TimeEntryComment(
            "Recorded confirmation evidence.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault);
        var adjustedComment = new TimeEntryComment(
            "Adjusted confirmation evidence.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault);
        var correctedComment = new TimeEntryComment(
            "Corrected confirmation evidence.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault);
        TimeEntryRecorded recorded = Recorded() with
        {
            Comment = recordedComment,
            AiMetrics = AiEffortMetrics.Unavailable
        };
        TimeEntryCorrectionValues previous = CorrectionValues(ActivityTypeScope.Tenant) with
        {
            DurationMinutes = 75,
            Comment = adjustedComment
        };
        TimeEntryAdjustedThroughMagicLink adjusted = Adjusted() with
        {
            AdjustedAtUtc = new DateTimeOffset(2026, 6, 19, 12, 10, 0, TimeSpan.Zero),
            PreviousValues = CorrectionValues(ActivityTypeScope.Tenant) with
            {
                Comment = recordedComment,
                AiMetrics = recorded.AiMetrics
            },
            AdjustedValues = previous
        };
        var confirmed = new TimeEntryContributorConfirmed(
            TimeEntryId(),
            Contributor(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 12, 20, 0, TimeSpan.Zero),
            new ExternalContributionSource("external", "confirmation-1"));
        TimeEntrySubmitted submitted = Submitted(Tenant()) with { Submitter = new PartyReference("submitter-2") };
        TimeEntryApproved approved = Approved() with { Approver = new PartyReference("approver-3") };
        TimeEntryApprovedCorrected correction = ApprovedCorrected() with
        {
            CorrectedBy = new PartyReference("corrector-4"),
            PreviousValues = previous,
            CorrectedValues = previous with
            {
                DurationMinutes = 90,
                Comment = correctedComment,
                AiMetrics = AiEffortMetrics.Unavailable
            }
        };
        MagicLinkConfirmationCapabilityIssued issued = Issued();
        MagicLinkConfirmationCapabilityUsed used = Used() with { UsedAtUtc = adjusted.AdjustedAtUtc };
        StreamReadEvent[] capabilityHistory =
        [
            Event(1, "capability-z", issued),
            Event(2, "capability-a", used)
        ];
        StreamReadEvent[] timeEntryHistory =
        [
            Event(1, "time-f", recorded),
            Event(2, "time-a", adjusted),
            Event(3, "time-e", confirmed),
            Event(4, "time-b", submitted),
            Event(5, "time-d", approved),
            Event(6, "time-c", correction)
        ];
        (string Name, StreamReadEvent[] Events)[] capabilityVariants =
        [
            ("ascending", capabilityHistory),
            ("reversed", capabilityHistory.Reverse().ToArray()),
            ("shuffled-with-duplicates", [capabilityHistory[1], capabilityHistory[0], capabilityHistory[1], capabilityHistory[0]])
        ];
        (string Name, StreamReadEvent[] Events)[] timeEntryVariants =
        [
            ("ascending", timeEntryHistory),
            ("reversed", timeEntryHistory.Reverse().ToArray()),
            ("shuffled-with-duplicates", [timeEntryHistory[5], timeEntryHistory[2], timeEntryHistory[0], timeEntryHistory[3],
                timeEntryHistory[1], timeEntryHistory[4], timeEntryHistory[2], timeEntryHistory[5]])
        ];
        var catalog = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(ActivityId(), ActivityTypeScope.Tenant, null, "Development", true, BillableState.Billable)],
            ProjectionFreshnessMetadata.Fresh with
            {
                Cursor = "catalog-head",
                AsOfUtc = new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero)
            });
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string? expectedSnapshot = null;

        foreach (var capabilityVariant in capabilityVariants)
        {
            foreach (var timeEntryVariant in timeEntryVariants)
            {
                string variantName = $"Capability: {capabilityVariant.Name}; Time Entry: {timeEntryVariant.Name}";
                var gateway = new ScriptedGatewayClient()
                    .WithStream(Tenant().TenantId, CapabilityId().Value, capabilityVariant.Events)
                    .WithStream(Tenant().TenantId, TimeEntryId().Value, timeEntryVariant.Events);
                MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash()), catalog))
                    .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

                state.CapabilityState.ShouldNotBeNull(variantName).State.ShouldBe(CapabilityState.Used, variantName);
                state.CapabilityState.UsedAtUtc.ShouldBe(used.UsedAtUtc, variantName);
                state.CapabilityState.UseMetadata.ShouldBe(used.Source, variantName);
                TimeEntryState entry = state.TimeEntryState.ShouldNotBeNull(variantName);
                entry.DurationMinutes.ShouldBe(correction.CorrectedValues.DurationMinutes, variantName);
                entry.ActivityTypeScope.ShouldBe(correction.CorrectedValues.ActivityTypeScope!.Value, variantName);
                TimeEntryExternalAdjustmentEvidence adjustment = entry.ExternalAdjustment.ShouldNotBeNull(variantName);
                adjustment.AdjustedAtUtc.ShouldBe(adjusted.AdjustedAtUtc, variantName);
                adjustment.PreviousValues.ShouldBe(adjusted.PreviousValues, variantName);
                adjustment.AdjustedValues.ShouldBe(adjusted.AdjustedValues, variantName);
                adjustment.Source.ShouldBe(adjusted.Source, variantName);
                entry.ConfirmedContributor.ShouldBe(confirmed.Contributor, variantName);
                entry.ConfirmationTenant.ShouldBe(confirmed.Tenant, variantName);
                entry.ConfirmedAtUtc.ShouldBe(confirmed.ConfirmedAtUtc, variantName);
                entry.ContributorConfirmationSource.ShouldBe(confirmed.Source, variantName);
                entry.TimeEntrySubmissionId.ShouldBe(submitted.TimeEntrySubmissionId, variantName);
                entry.Submitter.ShouldBe(submitted.Submitter, variantName);
                entry.SubmissionTenant.ShouldBe(submitted.Tenant, variantName);
                entry.SubmittedAtUtc.ShouldBe(submitted.SubmittedAtUtc, variantName);
                entry.SubmissionScope.ShouldBe(submitted.SubmissionScope, variantName);
                entry.ApprovalState.ShouldBe(approved.ApprovalState, variantName);
                entry.TimeEntryApprovalDecisionId.ShouldBe(approved.TimeEntryApprovalDecisionId, variantName);
                entry.Approver.ShouldBe(approved.Approver, variantName);
                entry.ApprovalTenant.ShouldBe(approved.Tenant, variantName);
                entry.DecidedAtUtc.ShouldBe(approved.DecidedAtUtc, variantName);
                entry.ApprovalAuthoritySource.ShouldBe(approved.AuthoritySource, variantName);
                entry.ApprovalScope.ShouldBe(approved.ApprovalScope, variantName);
                entry.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId, variantName);
                entry.CorrectedBy.ShouldBe(correction.CorrectedBy, variantName);
                entry.CorrectionTenant.ShouldBe(correction.Tenant, variantName);
                entry.CorrectedAtUtc.ShouldBe(correction.CorrectedAtUtc, variantName);
                entry.PreviousValues.ShouldBe(correction.PreviousValues, variantName);
                entry.CorrectedValues.ShouldBe(correction.CorrectedValues, variantName);
                entry.CorrectionReason.ShouldBe(correction.Reason, variantName);
                entry.SourceApprovalDecisionId.ShouldBe(correction.SourceApprovalDecisionId, variantName);
                entry.SourceApprovalScope.ShouldBe(correction.SourceApprovalScope, variantName);
                entry.CorrectionState.ShouldBe(correction.CorrectionState, variantName);
                entry.Comment.ShouldBe(correctedComment, variantName);
                entry.AiMetrics.ShouldBe(correction.CorrectedValues.AiMetrics, variantName);
                entry.IsLockedFromDirectEdit.ShouldBeTrue(variantName);
                state.ActivityTypeCatalog.ShouldBe(catalog, variantName);

                // Serialize the whole public bundle so fields beyond the transition assertions above
                // also have to survive ordering and duplicate delivery unchanged.
                string snapshot = JsonSerializer.Serialize(state, jsonOptions);
                expectedSnapshot ??= snapshot;
                snapshot.ShouldBe(expectedSnapshot, variantName);
            }
        }
    }

    [Fact]
    public async Task LoadCapabilityAsyncReturnsNullWhenCapabilityAggregateIsMissing()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldBeNull();
    }

    [Fact]
    public async Task LoadCapabilityAsyncReturnsNullWhenNoAmbientTenant()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoaderWithoutTenant(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldBeNull();
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsyncFailsClosedWhenNoAmbientTenant()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoaderWithoutTenant(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        catalog.Items.ShouldBeEmpty();
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTokenStateAsyncFailsClosedForBlankTokenWithoutAnyRead(string blankToken)
    {
        // A malformed/blank token must collapse to the identical opaque state before any index or
        // EventStore read — no resolution work, no observable difference from any other failure.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync(blankToken, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenTokenHashingRejectsMalformedToken()
    {
        // When hashing the presented token throws (malformed material), the loader fails closed without
        // touching the index or EventStore — identical opaque outcome, no disclosure of the reason.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(
            gateway,
            readModels,
            new ThrowingTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"),
            allowLegacyIndexRead: true);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("malformed-token", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task LoadTokenStateAsyncReturnsFoldedExpiredStateProvingIndexIsNotExpiryAuthority()
    {
        // Parity with the revoked/used proofs: the index still resolves the candidate after expiry, but
        // expiry truth lives only in the folded aggregate, which the downstream validators reject.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Expired()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Expired);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsyncReadsFreshTenantCatalogForAdminPaths()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        catalog.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(ActivityId());
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshnessState.Degraded)]
    [InlineData(ProjectionFreshnessState.Unknown)]
    [InlineData(ProjectionFreshnessState.Unavailable)]
    public async Task LoadActivityTypeCatalogAsyncRetainsOnlyNonFreshStatus(ProjectionFreshnessState status)
    {
        var gateway = new ScriptedGatewayClient();
        var metadata = new ProjectionFreshnessMetadata(
            status,
            "secret-cursor",
            new DateTimeOffset(2026, 6, 19, 13, 30, 0, TimeSpan.Zero),
            "secret-detail");
        var persisted = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(ActivityId(), ActivityTypeScope.Tenant, null, "secret-label", true, BillableState.Billable)],
            metadata);
        var loader = CreateLoader(
            gateway,
            new InMemoryReadModelStore(IndexWith(Hash()), persisted));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(status);
        catalog.ProjectionFreshness.Cursor.ShouldBeNull();
        catalog.ProjectionFreshness.AsOfUtc.ShouldBeNull();
        catalog.ProjectionFreshness.Detail.ShouldBeNull();
        catalog.Items.ShouldBeEmpty();

        var tokenGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        MagicLinkEndpointTokenState tokenState = await CreateLoader(
                tokenGateway,
                new InMemoryReadModelStore(IndexWith(Hash()), persisted))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        tokenState.CapabilityState.ShouldBeNull();
        tokenState.TimeEntryState.ShouldBeNull();
        tokenState.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(status);
        tokenState.ActivityTypeCatalog.ProjectionFreshness.Cursor.ShouldBeNull();
        tokenState.ActivityTypeCatalog.ProjectionFreshness.AsOfUtc.ShouldBeNull();
        tokenState.ActivityTypeCatalog.ProjectionFreshness.Detail.ShouldBeNull();
        tokenState.ActivityTypeCatalog.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshnessState.Rebuilding)]
    public async Task LoadTokenStateAsyncRejectsMalformedNonFreshCatalog(ProjectionFreshnessState status)
    {
        ActivityTypeCatalogReadModel malformed = InvalidFreshCatalog("duplicate-identifier") with
        {
            ProjectionFreshness = ProjectionFreshnessMetadata.StatusOnly(status)
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncReturnsUnavailableWhenCatalogIsMissing()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(
            gateway,
            new InMemoryReadModelStore(IndexWith(Hash()), missingCatalogRead: true));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
        state.ActivityTypeCatalog.ProjectionFreshness.Cursor.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.AsOfUtc.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTokenStateAsyncReturnsUnavailableForUnrecognizedCatalogStatus()
    {
        var malformed = new ActivityTypeCatalogReadModel(
            [],
            ProjectionFreshnessMetadata.StatusOnly((ProjectionFreshnessState)42));
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsyncRejectsFreshCatalogWithBlankLabel()
    {
        var malformed = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                null,
                " ",
                true,
                BillableState.Billable)],
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "1", null, null));
        var loader = CreateLoader(
            new ScriptedGatewayClient(),
            new InMemoryReadModelStore(IndexWith(Hash()), malformed));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        catalog.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsyncPagesAggregateStreamsWithExclusiveFromSequence()
    {
        var gateway = new ScriptedGatewayClient()
            .WithPagedStream(
                Tenant().TenantId,
                CapabilityId().Value,
                [Event(1, "capability-1", Issued())],
                [Event(2, "capability-2", Used())])
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        gateway.Requests
            .Where(static request => request.AggregateId == "capability-1")
            .Select(static request => request.FromSequence)
            .ShouldBe([0, 1]);
    }

    /// <summary>Refuses an issuance prefix when continuation leads to a page with no cursor progress.</summary>
    /// <param name="isTruncated">Whether truncation, rather than the advertised head, requires continuation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadTokenStateAsyncFailsClosedWhenContinuationStalls(bool isTruncated)
    {
        var gateway = new ScriptedGatewayClient()
            .WithPagedStream(
                Tenant().TenantId,
                CapabilityId().Value,
                [Event(1, "capability-1", Issued())],
                [Event(2, "capability-2", Revoked())])
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithPageTransform(Tenant().TenantId, CapabilityId().Value, page => page with
            {
                // Transform a real terminal-event page: the fixture skips literal empty pages.
                Events = page.Metadata.FromSequence == 0 ? page.Events : [],
                Metadata = page.Metadata with
                {
                    LatestSequence = isTruncated ? 1 : 2,
                    IsTruncated = isTruncated,
                    NextContinuationToken = isTruncated ? new ReplayContinuationToken("next") : null,
                    LastSequenceReturned = page.Metadata.FromSequence == 0 ? 1 : null,
                    EventCount = page.Metadata.FromSequence == 0 ? 1 : 0
                }
            });
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, readModels)
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.Where(request => request.AggregateId == CapabilityId().Value)
            .Select(static request => request.FromSequence).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenLaterPageRegressesLatestSequence()
    {
        var gateway = new ScriptedGatewayClient()
            .WithPagedStreamLatestSequences(
                Tenant().TenantId,
                CapabilityId().Value,
                [3, 2],
                [Event(1, "capability-1", Issued())],
                [Event(2, "capability-2", Used())]);
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests
            .Where(static request => request.AggregateId == "capability-1")
            .Select(static request => request.FromSequence)
            .ShouldBe([0, 1]);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedForAggregateSequenceGap()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(3, "capability-3", Used()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("missing-last")]
    [InlineData("unsolicited-to")]
    public async Task LoadTokenStateAsyncFailsClosedForMalformedStreamPageBounds(string caseName)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => page with
                {
                    Metadata = caseName == "missing-last"
                        ? page.Metadata with { LastSequenceReturned = null }
                        : page.Metadata with { ToSequence = 1 }
                });

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    public async Task LoadTokenStateAsyncFailsClosedWhenStreamPageScopeDiffersFromRequest(string scopePart)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => scopePart switch
                {
                    "tenant" => page with { Tenant = "tenant-2" },
                    "domain" => page with { Domain = "other-domain" },
                    "aggregate" => page with { AggregateId = "capability-2" },
                    _ => throw new InvalidOperationException($"Unknown page-scope part '{scopePart}'.")
                });

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenPageEventCountDisagreesWithEvents()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => page with { Metadata = page.Metadata with { EventCount = page.Events.Count + 1 } });

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("event-type")]
    [InlineData("payload")]
    [InlineData("serialization-format")]
    [InlineData("metadata-version")]
    [InlineData("message-id")]
    [InlineData("correlation-id")]
    [InlineData("causation-id")]
    [InlineData("timestamp")]
    [InlineData("user-id")]
    [InlineData("protection-metadata")]
    public async Task LoadTokenStateAsyncFailsClosedForSameSequenceEventsWithConflictingEnvelope(
        string conflict)
    {
        StreamReadEvent first = Event(1, "capability-1", Issued());
        if (conflict == "protection-metadata")
        {
            first = first with
            {
                ProtectionMetadata = ProtectionMetadata("first")
            };
        }

        StreamReadEvent conflicting = conflict switch
        {
            "event-type" => first with { EventTypeName = typeof(MagicLinkConfirmationCapabilityUsed).Name },
            "payload" => first with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(
                    Issued(hash: new MagicLinkTokenHash("conflicting-hash")),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            },
            "serialization-format" => first with { SerializationFormat = "xml" },
            "metadata-version" => first with { MetadataVersion = 2 },
            "message-id" => first with { MessageId = "capability-conflict" },
            "correlation-id" => first with { CorrelationId = "correlation-2" },
            "causation-id" => first with { CausationId = "causation-2" },
            "timestamp" => first with { Timestamp = first.Timestamp.AddMinutes(1) },
            "user-id" => first with { UserId = "operator-2" },
            "protection-metadata" => first with
            {
                ProtectionMetadata = ProtectionMetadata("second")
            },
            _ => throw new InvalidOperationException($"Unknown envelope conflict '{conflict}'.")
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                first,
                conflicting)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncAcceptsSameSequenceDuplicatesWithEquivalentProtectionMetadata()
    {
        StreamReadEvent first = Event(1, "capability-1", Issued()) with
        {
            ProtectionMetadata = ProtectionMetadata("same-reason")
        };
        StreamReadEvent duplicate = first with
        {
            Payload = first.Payload.ToArray(),
            ProtectionMetadata = ProtectionMetadata("same-reason")
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, first, duplicate)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task LoadTokenStateAsyncFailsClosedForBlankEventMessageId(string? messageId)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()) with { MessageId = messageId! })
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenOneMessageIdAppearsAtDifferentSequences()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "repeated-message", Issued()),
                Event(2, "repeated-message", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("project-scope")]
    [InlineData("project-reference")]
    [InlineData("duplicate-identifier")]
    public async Task LoadTokenStateAsyncFailsClosedForInvalidFreshCatalogShape(string scenario)
    {
        ActivityTypeCatalogReadModel malformed = InvalidFreshCatalog(scenario);
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);

        ActivityTypeCatalogReadModel adminCatalog = await CreateLoader(
                new ScriptedGatewayClient(),
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken);
        adminCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        adminCatalog.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenFreshTenantCatalogHasNoMatchingActivityType()
    {
        var nonMatchingCatalog = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(
                new ActivityTypeId("activity-type-2"),
                ActivityTypeScope.Tenant,
                null,
                "Research",
                true,
                BillableState.Billable)],
            ProjectionFreshnessMetadata.Fresh);
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash()), nonMatchingCatalog))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadCapabilityAsyncFoldsTerminalStateForAdminRevokeAndExpirePaths()
    {
        // The admin revoke/expire endpoints load existing capability state through LoadCapabilityAsync. Prior
        // events must fold so an already-terminal capability is observed as terminal (not as a fresh issue).
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Revoked()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldNotBeNull().State.ShouldBe(CapabilityState.Revoked);
        state.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public async Task MalformedCapabilityLifecycleHistoriesDenyOpaquely()
    {
        object[][] histories =
        [
            [Used()], [Revoked()], [Expired()], [Revoked(), Issued()],
            [Issued(), Issued()], [Issued(), Used(), Issued()],
            [Issued(), Revoked(), Issued()], [Issued(), Expired(), Issued()],
            [Issued(), Used(), Used()], [Issued(), Revoked(), Expired()]
        ];
        foreach (object[] history in histories)
        {
            var gateway = new ScriptedGatewayClient()
                .WithStream(Tenant().TenantId, CapabilityId().Value,
                    history.Select((payload, i) => Event(i + 1, $"capability-{i + 1}", payload)).ToArray())
                .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "entry-1", Recorded()));
            var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

            ShouldBeOpaqueFailClosed(await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
            (await loader.LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)).ShouldBeNull();
        }
    }

    [Fact]
    public async Task MalformedTimeEntryLifecycleAndCorrectionScopesDenyOpaquely()
    {
        TimeEntryCorrected corrected = Corrected(ActivityTypeScope.Tenant);
        TimeEntryApprovedCorrected approvedCorrected = ApprovedCorrected();
        TimeEntryCorrected tenantCorrection = corrected with { PreviousValues = CorrectionValues(ActivityTypeScope.Tenant) };
        object[][] histories =
        [
            [Submitted(Tenant()), Recorded()], [Approved(), Recorded()], [Rejected(), Recorded()],
            [Recorded(), Recorded()], [Recorded(), Approved()], [Recorded(), Rejected()],
            [Recorded(), tenantCorrection],
            [Recorded(activityTypeScope: ActivityTypeScope.Project), approvedCorrected],
            [Recorded(), Submitted(Tenant()), Submitted(Tenant())],
            [Recorded(), Submitted(Tenant()), Adjusted()],
            [Recorded(), Submitted(Tenant()), Rejected(), corrected],
            [Recorded(), Submitted(Tenant()), Approved(), approvedCorrected],
            [Recorded() with { ApprovalState = TimeEntryApprovalState.Approved }],
            [Recorded() with { ApprovalState = (TimeEntryApprovalState)99 }],
            [Recorded(), Submitted(Tenant()) with { ApprovalState = TimeEntryApprovalState.Draft }],
            [Recorded(), Submitted(Tenant()), Approved() with { ApprovalState = TimeEntryApprovalState.Rejected }],
            [Recorded(), Submitted(Tenant()), Rejected() with { ApprovalState = TimeEntryApprovalState.Approved }],
            [Recorded(), Submitted(Tenant()), Rejected(), tenantCorrection with { ApprovalState = TimeEntryApprovalState.Approved }],
            [Recorded(), Submitted(Tenant()), Rejected(), tenantCorrection with { CorrectionState = TimeEntryCorrectionState.None }],
            [Recorded(activityTypeScope: ActivityTypeScope.Project), Submitted(Tenant()), Approved(), approvedCorrected with { ApprovalState = TimeEntryApprovalState.Draft }],
            [Recorded(activityTypeScope: ActivityTypeScope.Project), Submitted(Tenant()), Approved(), approvedCorrected with { CorrectionState = TimeEntryCorrectionState.None }],
            [Recorded(), Submitted(Tenant()), Rejected(), tenantCorrection, Submitted(Tenant()), Rejected(), tenantCorrection],
            [Recorded(activityTypeScope: ActivityTypeScope.Project), Submitted(Tenant()), Approved(), approvedCorrected, approvedCorrected]
        ];
        foreach (object[] history in histories)
        {
            var gateway = new ScriptedGatewayClient()
                .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
                .WithStream(Tenant().TenantId, TimeEntryId().Value,
                    history.Select((payload, i) => Event(i + 1, $"entry-{i + 1}", payload)).ToArray());

            ShouldBeOpaqueFailClosed(await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
                .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("approvalDecision")]
    [InlineData("approvalScope")]
    [InlineData("rejectionDecision")]
    [InlineData("rejectionReason")]
    public async Task ContradictoryCorrectionAttributionDeniesOpaquely(string field)
    {
        bool approved = field.StartsWith("approval", StringComparison.Ordinal);
        object correction = field switch
        {
            "approvalDecision" => ApprovedCorrected() with { SourceApprovalDecisionId = new("other-decision") },
            "approvalScope" => ApprovedCorrected() with { SourceApprovalScope = TimeEntryApprovalScope.TimesheetPeriod },
            "rejectionDecision" => Corrected(ActivityTypeScope.Tenant) with { RejectionDecisionId = new("other-decision") },
            "rejectionReason" => Corrected(ActivityTypeScope.Tenant) with { RejectionReason = new("Other reason") },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value,
                Event(1, "record", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "submit", Submitted(Tenant())), Event(3, "decision", approved ? Approved() : Rejected()),
                Event(4, "correction", correction));
        ShouldBeOpaqueFailClosed(await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken));
    }

    private static void ShouldBeOpaqueFailClosed(MagicLinkEndpointTokenState state)
    {
        // Every loader failure stage collapses to the identical opaque token state: no capability, no
        // Time Entry, and an Unavailable catalog. No channel reveals which stage failed.
        state.CapabilityState.ShouldBeNull();
        state.TimeEntryState.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        state.ActivityTypeCatalog.Items.ShouldBeEmpty();
    }

    private static EventStoreMagicLinkConfirmationCapabilityStateLoader CreateLoader(
        IEventStoreGatewayClient gateway,
        IReadModelStore readModels)
        => new(
            gateway,
            readModels,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"),
            allowLegacyIndexRead: true);

    private static EventStoreMagicLinkConfirmationCapabilityStateLoader CreateLoaderWithoutTenant(
        IEventStoreGatewayClient gateway,
        IReadModelStore readModels)
        => new(
            gateway,
            readModels,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(null, null, "correlation-1"),
            allowLegacyIndexRead: true);

    private static MagicLinkTokenHashCapabilityIndexReadModel IndexWith(MagicLinkTokenHash hash)
        => MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
            [new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(hash.Value,
                new(Tenant(), CapabilityId()))]);

    private static StreamReadEvent Event(long sequence, string messageId, object payload)
        => new(
            sequence,
            payload.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "json",
            1,
            messageId,
            "correlation-1",
            null,
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            "operator-1");

    private static EventStorePayloadProtectionMetadata ProtectionMetadata(string compatibilityValue)
        => new(
            PayloadProtectionState.Unprotected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            null,
            null,
            "application/json",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["compatibility"] = compatibilityValue
            });

    private static MagicLinkConfirmationCapabilityIssued Issued(
        MagicLinkCapabilityId? capabilityId = null,
        MagicLinkTokenHash? hash = null,
        TenantReference? tenant = null)
        => new(
            capabilityId ?? CapabilityId(),
            tenant ?? Tenant(),
            Contributor(),
            TimeEntryTargetReference.ForProject(Project()),
            ActivityId(),
            TimeEntryId(),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.ConfirmOrAdjust,
            hash ?? Hash(),
            new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true);

    private static MagicLinkConfirmationCapabilityUsed Used()
        => new(
            CapabilityId(),
            Tenant(),
            Contributor(),
            TimeEntryId(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static MagicLinkConfirmationCapabilityRevoked Revoked()
        => new(
            CapabilityId(),
            Tenant(),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static MagicLinkConfirmationCapabilityExpired Expired()
        => new(
            CapabilityId(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static TimeEntryRecorded Recorded(
        TimeEntryId? timeEntryId = null,
        ActivityTypeId? activityTypeId = null,
        ActivityTypeScope activityTypeScope = ActivityTypeScope.Tenant)
        => new(
            timeEntryId ?? TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            activityTypeId ?? ActivityId(),
            activityTypeScope,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            ExternalSource = new ExternalContributionSource("external", "source-1")
        };

    private static TimeEntrySubmitted Submitted(TenantReference tenant)
        => new(
            TimeEntryId(),
            Operator(),
            tenant,
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved()
        => new(
            TimeEntryId(),
            Operator(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 12, 45, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            new ApprovalAuthoritySourceAttribution(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey,
                "v2",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryRejected Rejected()
        => new(TimeEntryId(), Operator(), Tenant(),
            new DateTimeOffset(2026, 6, 19, 12, 45, 0, TimeSpan.Zero), new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Rejected, Approved().AuthoritySource with { Action = ApprovalAuthorityAction.EntryRejection },
            TimeEntryApprovalScope.IndividualEntry, new TimeEntryRejectionReason("Rejected."));

    private static TimeEntryCorrected Corrected(ActivityTypeScope? correctedScope)
    {
        TimeEntryCorrectionValues previous = new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.ExternalContributor,
            null)
        {
            ActivityTypeScope = correctedScope is null ? null : ActivityTypeScope.Project
        };
        TimeEntryCorrectionValues corrected = previous with
        {
            ActivityTypeScope = correctedScope,
            DurationMinutes = 75
        };
        return new(
            TimeEntryId(),
            new TimeEntryCorrectionId("correction-1"),
            Tenant(),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            previous,
            corrected,
            new TimeEntryRejectionReason("Rejected."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Corrected);
    }

    private static TimeEntryApprovedCorrected ApprovedCorrected()
    {
        TimeEntryCorrectionValues previous = CorrectionValues(ActivityTypeScope.Project);
        TimeEntryCorrectionValues corrected = previous with
        {
            ActivityTypeScope = ActivityTypeScope.Tenant,
            DurationMinutes = 75
        };
        return new(
            TimeEntryId(),
            new TimeEntryCorrectionId("correction-1"),
            Tenant(),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            previous,
            corrected,
            new TimeEntryCorrectionReason("Approved correction."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);
    }

    private static TimeEntryAdjustedThroughMagicLink Adjusted(
        ActivityTypeScope? previousScope = ActivityTypeScope.Tenant,
        ActivityTypeScope? adjustedScope = ActivityTypeScope.Tenant)
    {
        TimeEntryCorrectionValues previous = CorrectionValues(ActivityTypeScope.Tenant) with
        {
            ActivityTypeScope = previousScope
        };
        TimeEntryCorrectionValues adjusted = previous with
        {
            ActivityTypeScope = adjustedScope,
            DurationMinutes = 75
        };
        return new(
            TimeEntryId(),
            Tenant(),
            Contributor(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant,
            previous,
            adjusted,
            new ExternalContributionSource("magic-link", "capability-1"));
    }

    private static TimeEntryCorrectionValues CorrectionValues(ActivityTypeScope activityTypeScope)
        => new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.ExternalContributor,
            null)
        {
            ActivityTypeScope = activityTypeScope
        };

    private static ActivityTypeCreated ActivityCreated()
        => new(
            ActivityId(),
            ActivityTypeScope.Tenant,
            null,
            "Delivery",
            BillableState.Billable);

    private static ActivityTypeCreated SecondActivityCreated()
        => new(
            new ActivityTypeId("activity-type-2"),
            ActivityTypeScope.Tenant,
            null,
            "Research",
            BillableState.Billable);

    private static ActivityTypeCreated ProjectScopedActivityCreated()
        => new(
            new ActivityTypeId("project-activity-type"),
            ActivityTypeScope.Project,
            Project(),
            "Project Work",
            BillableState.Billable);

    private static ActivityTypeCatalogReadModel InvalidFreshCatalog(string scenario)
    {
        ActivityTypeCatalogItem first = scenario switch
        {
            "project-scope" => new ActivityTypeCatalogItem(
                ProjectScopedActivityCreated().ActivityTypeId,
                ActivityTypeScope.Project,
                null,
                "Project Work",
                true,
                BillableState.Billable),
            "project-reference" => new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                Project(),
                "Delivery",
                true,
                BillableState.Billable),
            "duplicate-identifier" => new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                null,
                "Delivery",
                true,
                BillableState.Billable),
            _ => throw new InvalidOperationException($"Unknown invalid-catalog scenario '{scenario}'.")
        };
        ActivityTypeCatalogItem[] items = scenario == "duplicate-identifier"
            ? [
                first,
                new ActivityTypeCatalogItem(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    SecondActivityCreated().Label,
                    true,
                    BillableState.Billable)
            ]
            : [first];
        return new ActivityTypeCatalogReadModel(
            items,
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "2", null, null));
    }

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("party-1");

    private static PartyReference Operator() => new("operator-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static MagicLinkTokenHash Hash() => new("hash-only");

    private sealed class DeterministicTokenGenerator : IMagicLinkTokenGenerator
    {
        public MagicLinkTokenMaterial Generate() => new("opaque-once", Hash());

        public MagicLinkTokenHash DeriveHash(string oneTimeToken)
            => string.Equals(oneTimeToken, "opaque-once", StringComparison.Ordinal)
                ? Hash()
                : new MagicLinkTokenHash("different-hash");
    }

    private sealed class ThrowingTokenGenerator : IMagicLinkTokenGenerator
    {
        public MagicLinkTokenMaterial Generate() => new("opaque-once", Hash());

        public MagicLinkTokenHash DeriveHash(string oneTimeToken)
            => throw new ArgumentException("Malformed token material.", nameof(oneTimeToken));
    }

    private sealed class FixedTrustedContextAccessor(
        TenantReference? tenant,
        PartyReference? actor,
        string correlationId) : ITimesheetsTrustedContextAccessor
    {
        public TenantReference? CurrentTenant => tenant;

        public PartyReference? CurrentActor => actor;

        public string? CurrentCorrelationId => correlationId;
    }

    private sealed class InMemoryReadModelStore(
        MagicLinkTokenHashCapabilityIndexReadModel? index,
        ActivityTypeCatalogReadModel? catalog = null,
        bool throwCatalogRead = false,
        bool throwIndexRead = false,
        bool missingCatalogRead = false,
        Exception? indexException = null,
        Exception? catalogException = null,
        Exception? epochException = null,
        Action? beforeCatalogReturn = null) : IReadModelStore
    {
        public int ReadCount { get; private set; }

        public int CatalogReadCount { get; private set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ReadCount++;

            if (epochException is not null && key.StartsWith("shared-epoch:", StringComparison.Ordinal))
                throw epochException;

            if (typeof(TValue) == typeof(ActivityTypeCatalogReadModel))
            {
                CatalogReadCount++;
                if (catalogException is not null) throw catalogException;
                // Verify the addressing, not just the type. Dispatching on typeof(TValue) alone let
                // the loader read any store and any key and still pass, so the tenant-scoped catalog
                // key and its state-store component were never actually asserted by these tests.
                storeName.ShouldBe(MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName);
                key.ShouldBe(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()));

                if (throwCatalogRead)
                {
                    throw new InvalidOperationException("Catalog read failed.");
                }

                if (missingCatalogRead)
                {
                    return Task.FromResult(new ReadModelEntry<TValue>(null, "etag-catalog"));
                }

                ActivityTypeCatalogReadModel value = catalog ?? new ActivityTypeCatalogReadModel(
                    [new ActivityTypeCatalogItem(
                        ActivityId(),
                        ActivityTypeScope.Tenant,
                        null,
                        "Delivery",
                        true,
                        BillableState.Billable)],
                    new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "1", null, null));
                beforeCatalogReturn?.Invoke();
                return Task.FromResult(new ReadModelEntry<TValue>(value as TValue, "etag-catalog"));
            }

            storeName.ShouldBe(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName);
            key.ShouldBe(MagicLinkTokenHashCapabilityIndexProjection.StateKey);
            if (indexException is not null) throw indexException;
            if (throwIndexRead)
            {
                throw new InvalidOperationException("Index read failed.");
            }

            return Task.FromResult(new ReadModelEntry<TValue>(index as TValue, "etag-index"));
        }

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.CompletedTask;

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(true);
    }

    private sealed class ScriptedGatewayClient : IEventStoreGatewayClient
    {

        // The magic-link loader never queries command status; this member exists only to satisfy
        // IEventStoreGatewayClient.
        public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[]> _streams = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[][]> _pagedStreams = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), long[]> _pagedLatestSequences = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), Exception> _exceptions = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), Func<StreamReadPage, StreamReadPage>> _pageTransforms = [];

        public List<StreamReadRequest> Requests { get; } = [];

        public List<StreamReadRequest> WorkloadRequests { get; } = [];

        public ScriptedGatewayClient WithStream(string tenant, string? aggregateId, params StreamReadEvent[] events)
        {
            _streams[(tenant, aggregateId)] = events;
            return this;
        }

        public ScriptedGatewayClient WithPagedStream(string tenant, string? aggregateId, params StreamReadEvent[][] pages)
        {
            _pagedStreams[(tenant, aggregateId)] = pages;
            return this;
        }

        public ScriptedGatewayClient WithPagedStreamLatestSequences(
            string tenant,
            string? aggregateId,
            long[] latestSequences,
            params StreamReadEvent[][] pages)
        {
            if (latestSequences.Length != pages.Length)
            {
                throw new ArgumentException("Each page must have one LatestSequence value.", nameof(latestSequences));
            }

            _pagedStreams[(tenant, aggregateId)] = pages;
            _pagedLatestSequences[(tenant, aggregateId)] = latestSequences;
            return this;
        }

        public ScriptedGatewayClient WithThrow(string tenant, string? aggregateId)
        {
            _exceptions[(tenant, aggregateId)] = new InvalidOperationException("EventStore stream read failed.");
            return this;
        }

        public ScriptedGatewayClient WithException(string tenant, string? aggregateId, Exception exception)
        {
            _exceptions[(tenant, aggregateId)] = exception;
            return this;
        }

        public ScriptedGatewayClient WithPageTransform(
            string tenant,
            string? aggregateId,
            Func<StreamReadPage, StreamReadPage> transform)
        {
            _pageTransforms[(tenant, aggregateId)] = transform;
            return this;
        }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult> SubmitQueryAsync(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamReadPage> ReadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The loader must use the workload stream route.");

        private Task<StreamReadPage> ReadScriptedStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_exceptions.TryGetValue((request.Tenant, request.AggregateId), out Exception? exception))
            {
                throw exception;
            }

            if (_pagedStreams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[][]? pages))
            {
                int pageIndex = pages.TakeWhile(page => page.Length == 0
                    || page.Max(static item => item.SequenceNumber) <= request.FromSequence).Count();
                StreamReadEvent[] pageEvents = pageIndex < pages.Length ? pages[pageIndex] : [];
                bool hasMore = pageIndex + 1 < pages.Length;
                long latestSequence = _pagedLatestSequences.TryGetValue(
                        (request.Tenant, request.AggregateId),
                        out long[]? latestSequences)
                    && pageIndex < latestSequences.Length
                        ? latestSequences[pageIndex]
                        : pageEvents.Length == 0
                            ? 0
                            : pageEvents.Max(static item => item.SequenceNumber);
                StreamReadPage result = new(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    pageEvents,
                    new StreamReadMetadata(
                        request.FromSequence,
                        request.ToSequence,
                        pageEvents.Length == 0 ? null : pageEvents.Max(static @event => @event.SequenceNumber),
                        latestSequence,
                        pageEvents.Length,
                        hasMore,
                        hasMore
                            ? new ReplayContinuationToken((pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                            : null));
                return Task.FromResult(Transform(request, result));
            }

            StreamReadEvent[] events = _streams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[]? value)
                ? value
                : [];
            StreamReadPage page = new(
                request.Tenant,
                request.Domain,
                request.AggregateId,
                events,
                new StreamReadMetadata(
                    request.FromSequence,
                    request.ToSequence,
                    events.Length == 0 ? null : events.Max(static @event => @event.SequenceNumber),
                    events.Length == 0 ? 0 : events.Max(static @event => @event.SequenceNumber),
                    events.Length,
                    false,
                    null));
            return Task.FromResult(Transform(request, page));
        }

        public Task<StreamReadPage> ReadWorkloadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            WorkloadRequests.Add(request);
            return ReadScriptedStreamAsync(request, cancellationToken);
        }

        private StreamReadPage Transform(StreamReadRequest request, StreamReadPage page)
            => _pageTransforms.TryGetValue(
                (request.Tenant, request.AggregateId),
                out Func<StreamReadPage, StreamReadPage>? transform)
                    ? transform(page)
                    : page;
    }
}
