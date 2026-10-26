using System.Globalization;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using NSubstitute;
using NSubstitute.Extensions;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class MagicLinkCandidateDaprBatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoOrdinaryOwnersUseActualDaprBatchSerializationAndKeyLocalEtags(bool concurrentCatchUp)
    {
        var backend = new RedisByteBackend();
        var store = new DaprReadModelStore(backend.Client);
        var batches = new ObservedBatches(store, backend);
        var coordinator = new SharedProjectionEpochCoordinator(store, batches);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        async Task<(int Completed, ProjectionDispatchStatus Status)> AttemptAsync()
        {
            try
            {
                int completed = await coordinator.CatchUpAsync(scope,
                    (delivery, generation, cancellation) => handler.FoldCatchUpAsync(delivery, generation,
                        coordinator, cancellation), TestContext.Current.CancellationToken);
                return (completed, completed > 0 ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted);
            }
            catch (SharedProjectionWriteUnavailableException)
            {
                return (0, ProjectionDispatchStatus.Retryable);
            }
            catch (InvalidOperationException exception) when (
                exception.Message == "Shared projection catch-up is incomplete: Conflict")
            {
                // Match the actual dispatcher's Indeterminate classification for
                // this precise observed SDK failure, not arbitrary exceptions.
                return (0, ProjectionDispatchStatus.Indeterminate);
            }
        }
        int recoveryAttempts = 0;
        for (int owner = 1; owner <= 2; owner++)
        {
            int successesBefore = batches.Results.Count(result => result.IsSuccess);
            SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope,
                handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
            (await coordinator.JournalAsync(scope, lease, Delivery(owner), TestContext.Current.CancellationToken))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
            (int Completed, ProjectionDispatchStatus Status)? contended = null;
            if (concurrentCatchUp && owner == 2)
            {
                // Advance one existing participant's ETag without changing its
                // bytes, after the real protocol reads it and before its CAS.
                // This guarantees an optimistic conflict, independent
                // of scheduler timing, before concurrent same-journal recovery.
                backend.ForceNextOptimisticConflict = true;
                contended = await AttemptAsync();
                contended.Value.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
                backend.ForcedOptimisticConflicts.ShouldBe(1);
                batches.Results.ShouldContain(result => result.Status == ReadModelBatchStatus.Conflict
                    && result.ConflictKind == ReadModelBatchConflictKind.Optimistic);
            }
            (int Completed, ProjectionDispatchStatus Status)[] wave = await Task.WhenAll(
                Enumerable.Range(0, concurrentCatchUp ? 4 : 1).Select(_ => AttemptAsync()));
            // Model the caller that observed nonterminal work, even if another
            // catch-up completed concurrently. Keep the original journal/input.
            var selected = contended ?? wave.FirstOrDefault(item => item.Status is ProjectionDispatchStatus.Retryable
                or ProjectionDispatchStatus.Indeterminate, wave[0]);
            int completedTotal = wave.Sum(item => item.Completed);
            for (int attempt = 1; attempt < 5 && selected.Status is ProjectionDispatchStatus.Retryable
                or ProjectionDispatchStatus.Indeterminate; attempt++)
            {
                if (contended.HasValue)
                    recoveryAttempts++;
                selected = await AttemptAsync();
                completedTotal += selected.Completed;
            }
            selected.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
            completedTotal.ShouldBeGreaterThanOrEqualTo(1);
            batches.Results.Count(result => result.IsSuccess).ShouldBeGreaterThan(successesBefore);
        }

        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        for (int owner = 1; owner <= 2; owner++)
            (await reader.FindCandidatesAsync(root.TokenRoot, Hash(owner), TestContext.Current.CancellationToken))
                .Single().CapabilityId.Value.ShouldBe(Owner(owner));
        batches.Results.ShouldNotBeEmpty();
        backend.TypedWrites.ShouldBeGreaterThan(0);
        backend.ByteWrites.ShouldBeGreaterThan(0);
        backend.Deletes.ShouldBeGreaterThan(0);
        backend.ForcedOptimisticConflicts.ShouldBe(concurrentCatchUp ? 1 : 0);
        if (concurrentCatchUp)
            recoveryAttempts.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DaprBatchIdentityConflictPreservesOriginalStoredValue()
    {
        var backend = new RedisByteBackend();
        var store = new DaprReadModelStore(backend.Client);
        var scope = new ReadModelBatchScope("fixture-readmodels", "fixture-tenant", "timesheets",
            "fixture-owner", "fixture-projection", "stable-delivery");
        var first = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("fixture-value",
            new FixtureValue("original"), ReadModelBatchConcurrency.LastWrite)]);
        (await store.ExecuteAsync(first, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var conflicting = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("fixture-value",
            new FixtureValue("different"), ReadModelBatchConcurrency.LastWrite)]);
        ReadModelBatchResult result = await store.ExecuteAsync(conflicting, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Identity);
        result.IsSuccess.ShouldBeFalse();
        (await store.GetAsync<FixtureValue>(scope.StoreName, "fixture-value", TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull().Value.ShouldBe("original");
    }

    private sealed record FixtureValue(string Value);

    private static string Owner(int value) => "live-capability-" + value.ToString("D16", CultureInfo.InvariantCulture);
    private static string Hash(int value) => new(value == 1 ? 'A' : 'B', 43);

    private static SharedProjectionDelivery Delivery(int owner)
    {
        var issued = new MagicLinkConfirmationCapabilityIssued(new MagicLinkCapabilityId(Owner(owner)),
            new TenantReference("tenant-1"), new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new ActivityTypeId("activity-1"),
            new TimeEntryId("entry-1"), MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash(Hash(owner)), DateTimeOffset.UtcNow.AddDays(1), new PartyReference("issuer-1"),
            DateTimeOffset.UtcNow, new MagicLinkAuditMetadata("timesheets", "byte-store-repro"), true);
        var item = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, JsonSerializerOptions.Web), "json", 1, DateTimeOffset.UtcNow,
            "correlation-" + owner, "message-" + owner, "issuer-1", owner);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest(
            "tenant-1", "timesheets", Owner(owner), [item]), JsonSerializerOptions.Web);
        return new("tenant-1:timesheets:" + Owner(owner), owner, payload, [], OwnerSequenceNumber: 1);
    }

    private sealed class ObservedBatches(IReadModelBatchStore inner, RedisByteBackend backend) : IReadModelBatchStore
    {
        public System.Collections.Concurrent.ConcurrentBag<ReadModelBatchResult> Results { get; } = [];

        public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        {
            backend.SelectContendedParticipant(batch);
            ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
            Results.Add(result);
            if (result.Status == ReadModelBatchStatus.Conflict)
                Console.WriteLine($"Offline batch result: status=Conflict kind={result.ConflictKind}");
            return result;
        }
    }

    private sealed class RedisByteBackend
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<(string Store, string Key), (byte[] Body, long ETag)> _values = [];
        private (string Store, string Key)? _contendedParticipant;

        public DaprClient Client { get; } = Substitute.For<DaprClient>();
        public int TypedWrites { get; private set; }
        public int ByteWrites { get; private set; }
        public int Deletes { get; private set; }
        public bool ForceNextOptimisticConflict { private get; set; }
        public int ForcedOptimisticConflicts { get; private set; }

        public void SelectContendedParticipant(ReadModelBatch batch)
        {
            lock (_gate)
            {
                if (!ForceNextOptimisticConflict)
                    return;
                foreach (ReadModelBatchOperation operation in batch.Operations)
                {
                    if (_values.ContainsKey((batch.Scope.StoreName, operation.Key)))
                    {
                        _contendedParticipant = (batch.Scope.StoreName, operation.Key);
                        return;
                    }
                }
            }
        }

        public RedisByteBackend()
        {
            Client.JsonSerializerOptions.Returns(JsonSerializerOptions.Web);
            Client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => ReadAsync(call.ArgAt<string>(0), call.ArgAt<string>(1)));
            Client.ReturnsForAll<Task<bool>>(call =>
            {
                object[] arguments = call.Args();
                if (arguments.Length == 6)
                    return DeleteAsync((string)arguments[0], (string)arguments[1], (string)arguments[2]);
                arguments.Length.ShouldBe(7);
                object value = arguments[2];
                byte[] bytes;
                if (value is ReadOnlyMemory<byte> body)
                {
                    bytes = body.ToArray();
                    ByteWrites++;
                }
                else
                {
                    bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), Client.JsonSerializerOptions);
                    TypedWrites++;
                }
                return WriteAsync((string)arguments[0], (string)arguments[1], bytes, (string)arguments[3]);
            });
        }

        private async Task<(ReadOnlyMemory<byte>, string)> ReadAsync(string store, string key)
        {
            await Task.Yield();
            lock (_gate)
                return _values.TryGetValue((store, key), out var current)
                    ? (current.Body, current.ETag.ToString(CultureInfo.InvariantCulture))
                    : (ReadOnlyMemory<byte>.Empty, string.Empty);
        }

        private async Task<bool> WriteAsync(string store, string key, byte[] body, string expected)
        {
            await Task.Yield();
            lock (_gate)
            {
                bool exists = _values.TryGetValue((store, key), out var current);
                if (ForceNextOptimisticConflict && _contendedParticipant == (store, key) && exists
                    && current.ETag.ToString(CultureInfo.InvariantCulture) == expected)
                {
                    current = (current.Body, checked(current.ETag + 1));
                    _values[(store, key)] = current;
                    ForceNextOptimisticConflict = false;
                    ForcedOptimisticConflicts++;
                }
                if (exists && expected.Length == 0)
                    throw new Dapr.DaprException("Synthetic Redis create-only contention.");
                if ((exists ? current.ETag.ToString(CultureInfo.InvariantCulture) : string.Empty) != expected)
                    return false;
                _values[(store, key)] = (body, exists ? checked(current.ETag + 1) : 1);
                return true;
            }
        }

        private async Task<bool> DeleteAsync(string store, string key, string expected)
        {
            await Task.Yield();
            lock (_gate)
            {
                if (!_values.TryGetValue((store, key), out var current)
                    || current.ETag.ToString(CultureInfo.InvariantCulture) != expected)
                    return false;
                Deletes++;
                return _values.Remove((store, key));
            }
        }
    }
}
