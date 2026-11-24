using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class ApprovedTimeExportIntegrationTests
{
    [Fact]
    public async Task RootExportServiceCanPreviewWithoutConstructingAWorkloadGateway()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IEventStoreGatewayClient>(_ => throw new InvalidOperationException("Gateway unavailable."))
            .AddSingleton<ITimesheetsAccessGuard>(new AllowAllAccessGuard())
            .AddSingleton<IApprovedTimeLedgerProjectionReader>(ReceiptLedger())
            .AddSingleton<ITimeEntryDisplayHydrator>(new StaticHydrator())
            .AddSingleton(FinanceExportTestPolicy.Configured)
            .AddTimesheetsServerKernel().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var service = provider.GetRequiredService<ApprovedTimeExportService>();
        var token = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        (await service.PreviewAsync(Context(), new(), now, token)).Preview!.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        AssertWithheld(await service.GenerateAsync(Context(), new(), now, token));
    }

    [Fact]
    public async Task ConcurrentFirstAttemptsWithDifferentClocksAppendExactlyOneFinanceReceipt()
    {
        var token = TestContext.Current.CancellationToken;
        var context = Context();
        var now = DateTimeOffset.UtcNow;
        TaskCompletionSource bothReadMissing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int missing = 0;
        using var gateway = new FinanceReceiptActorGateway(new(), context.Actor!.PartyId)
        {
            OnMissingStream = async () =>
            {
                if (Interlocked.Increment(ref missing) == 2) bothReadMissing.SetResult();
                await bothReadMissing.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            }
        };
        using var provider = ReceiptServices(ReceiptLedger(), gateway, now.AddMinutes(2));
        using var scope = provider.CreateScope();
        gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        var service = provider.GetRequiredService<ApprovedTimeExportService>();
        var command = new GenerateApprovedTimeExport();
        var results = await Task.WhenAll(service.GenerateAsync(context, command, now, token).AsTask(),
            service.GenerateAsync(context, command, now.AddMinutes(1), token).AsTask());
        results.ShouldContain(result => result.WasGenerated);
        (await service.GenerateAsync(context, command, now.AddMinutes(2), token)).WasGenerated.ShouldBeTrue();
        var owner = gateway.Submitted!.AggregateId;
        (await gateway.Actor(owner).GetEventsAsync(0)).ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(StoredApprovedTimeExported).FullName);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task FinanceReceiptRecoversWithLegacyScopeFlags(bool currentOnly, bool includeSuperseded)
    {
        var token = TestContext.Current.CancellationToken;
        var context = Context();
        var now = DateTimeOffset.UtcNow;
        var command = new GenerateApprovedTimeExport
        {
            RequestId = "legacy-scope",
            LedgerQuery = new()
            {
                BillableState = BillableState.Billable,
                CurrentRowsOnly = currentOnly,
                IncludeSupersededRows = includeSuperseded
            }
        };
        using var gateway = new FinanceReceiptActorGateway(new(), context.Actor!.PartyId);
        using var provider = ReceiptServices(ReceiptLedger(), gateway, now);
        using var scope = provider.CreateScope();
        gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        var service = provider.GetRequiredService<ApprovedTimeExportService>();
        var generated = await service.GenerateAsync(context, command, now, token);
        generated.WasGenerated.ShouldBeTrue();
        var original = generated.Export.ShouldNotBeNull();
        original.Scope.CurrentRowsOnly.ShouldBeFalse();
        original.Scope.IncludesSupersededRows.ShouldBeTrue();

        // Earlier v1 receipts copied the input flags. Their command identity did not hash those derived flags.
        gateway.Readback = page =>
        {
            var item = page.Events.ShouldHaveSingleItem();
            var stored = JsonSerializer.Deserialize<StoredApprovedTimeExported>(item.Payload, JsonSerializerOptions.Web)!;
            var legacy = stored with
            {
                Event = stored.Event with
                {
                    OutputScope = stored.Event.OutputScope with
                    {
                        CurrentRowsOnly = currentOnly,
                        IncludesSupersededRows = includeSuperseded
                    }
                }
            };
            return page with { Events = [item with { Payload = JsonSerializer.SerializeToUtf8Bytes(legacy) }] };
        };
        var recovered = await service.GenerateAsync(context, command, now.AddMinutes(1), token);
        recovered.WasGenerated.ShouldBeTrue();
        var retried = recovered.Export.ShouldNotBeNull();
        retried.CsvContent.ShouldBe(original.CsvContent);
        retried.Scope.ShouldBe(original.Scope);
        retried.Audit.OutputScope.ShouldBe(original.Scope);
        (await gateway.Actor(gateway.Submitted!.AggregateId).GetEventsAsync(0)).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FinanceReceiptSurvivesLostAcknowledgementAndActorRecreation(bool loseAcknowledgement, bool retryClockBeforeRequest)
    {
        var token = TestContext.Current.CancellationToken;
        var context = Context();
        var original = new DateTimeOffset(2026, 6, 19, 17, 0, 0, TimeSpan.FromHours(2));
        DateTimeOffset firstGeneration = original.ToUniversalTime().AddSeconds(2);
        var command = new GenerateApprovedTimeExport { RequestId = "finance-request" };
        var storage = new ConcurrentDictionary<(string, string), InMemoryStateManager>();
        var reader = ReceiptLedger();
        string? originalCsv;
        CommandEnvelope firstCommand;
        EventEnvelope envelope;
        using (var gateway = new FinanceReceiptActorGateway(storage, context.Actor!.PartyId) { LoseAcknowledgement = loseAcknowledgement })
        using (var provider = ReceiptServices(reader, gateway, firstGeneration))
        using (var scope = provider.CreateScope())
        {
            gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
            var service = scope.ServiceProvider.GetRequiredService<ApprovedTimeExportService>();
            var preview = (await service.PreviewAsync(context, new(), original, token)).Preview.ShouldNotBeNull();
            preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
            preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
            JsonSerializer.Serialize(preview).ShouldNotContain("private finance note");
            gateway.Client.ReceivedCalls().ShouldBeEmpty();

            var first = await service.GenerateAsync(context, command, original, token);
            if (loseAcknowledgement) AssertWithheld(first);
            else first.WasGenerated.ShouldBeTrue();
            originalCsv = first.Export!.CsvContent;
            firstCommand = gateway.Submitted.ShouldNotBeNull();
            envelope = (await gateway.Actor(firstCommand.AggregateId).GetEventsAsync(0)).ShouldHaveSingleItem();
            envelope.CausationId.ShouldBe(firstCommand.MessageId);
            envelope.CorrelationId.ShouldBe(firstCommand.CorrelationId);
            envelope.MessageId.ShouldNotBe(firstCommand.MessageId);
            envelope.EventTypeName.ShouldBe(typeof(StoredApprovedTimeExported).FullName);
            envelope.UserId.ShouldBe(context.Actor.PartyId);
            string payload = Encoding.UTF8.GetString(envelope.Payload);
            payload.ShouldNotContain("private finance note");
            payload.ShouldNotContain("finance-entry-123");
            payload.ShouldNotContain("durationMinutes", Case.Insensitive);
            payload.ShouldNotContain("csvContent", Case.Insensitive);
            payload.ShouldNotContain("eventLineage", Case.Insensitive);
        }

        // Fresh gateway, status store, export service, receiver, and actor over the original actor state.
        using var fresh = new FinanceReceiptActorGateway(storage, context.Actor!.PartyId);
        using var restarted = ReceiptServices(reader, fresh, retryClockBeforeRequest ? firstGeneration : firstGeneration.AddDays(1));
        using var newScope = restarted.CreateScope();
        var receiver = newScope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        fresh.Connect(receiver);
        var serviceAfterRestart = newScope.ServiceProvider.GetRequiredService<ApprovedTimeExportService>();
        var later = original.AddHours(3);
        var retryContext = context with { CorrelationId = "later-trace" };
        var retries = await Task.WhenAll(Enumerable.Range(0, 3).Select(attempt =>
            serviceAfterRestart.GenerateAsync(retryContext, command, later.AddMinutes(attempt), token).AsTask()));
        foreach (var retry in retries)
        {
            retry.WasGenerated.ShouldBeTrue();
            var file = retry.Export!;
            var csv = file.CsvContent.ShouldNotBeNull();
            csv.ShouldContain("finance-entry-123");
            csv.ShouldContain("private finance note");
            file.Rows.ShouldHaveSingleItem().Comment.ShouldNotBeNull().Text.ShouldBe("private finance note");
            if (originalCsv is not null) csv.ShouldBe(originalCsv);
            var stored = JsonSerializer.Deserialize<StoredApprovedTimeExported>(envelope.Payload, JsonSerializerOptions.Web)!.Event;
            stored.ShouldBe(retry.AuditResult!.Events.ShouldHaveSingleItem().ShouldBeOfType<ApprovedTimeExported>());
            stored.RequestId.ShouldBe(file.Audit.RequestId);
            stored.Requester.ShouldBe(file.Audit.Requester);
            stored.Tenant.ShouldBe(file.Audit.Tenant);
            stored.Filters.ShouldBe(file.Audit.Filters);
            stored.OutputScope.ShouldBe(file.Scope);
            stored.OutputScope.LedgerCursor.ShouldBe(file.ProjectionFreshness.Cursor);
            stored.OutputScope.LedgerAsOfUtc.ShouldBe(file.ProjectionFreshness.AsOfUtc);
            stored.FreshnessState.ShouldBe(file.ProjectionFreshness.State);
            stored.RowCount.ShouldBe(file.Rows.Count);
            stored.OutputRowsHashSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(file.Rows, JsonSerializerOptions.Web))));
            file.Audit.OutputRowsHashSha256.ShouldBe(stored.OutputRowsHashSha256);
            stored.Format.ShouldBe(file.Format);
            stored.FormatVersion.ShouldBe(file.FormatVersion);
            stored.RequestedAtUtc.ShouldBe(original.ToUniversalTime());
            stored.GeneratedAtUtc.ShouldBe(firstGeneration);
            stored.CorrelationId.ShouldBe(context.CorrelationId);
            file.Audit.CorrelationId.ShouldBe(stored.CorrelationId);
            file.Audit.RequestedAtUtc.ShouldBe(stored.RequestedAtUtc);
            file.Audit.GeneratedAtUtc.ShouldBe(stored.GeneratedAtUtc);
            stored.OutputContentHashSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(csv))));
        }

        AssertWithheld(await serviceAfterRestart.GenerateAsync(context with { Actor = new("another-finance-operator") }, command, later, token));
        AssertWithheld(await serviceAfterRestart.GenerateAsync(context, command with
            { LedgerQuery = command.LedgerQuery with { Project = Project() } }, later, token));
        if (retryClockBeforeRequest)
        {
            AssertWithheld(await serviceAfterRestart.GenerateAsync(context, command with { RequestId = "new-request" }, later, token));
        }
        var changedContent = Substitute.For<IApprovedTimeLedgerProjectionReader>();
        var samePage = (await reader.QueryAsync(context, command.LedgerQuery, token))!;
        changedContent.QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryApprovedTimeLedger>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(samePage with
            {
                Items = samePage.Items.Select(row => row with { DurationMinutes = row.DurationMinutes + 10 }).ToArray()
            }));
        using var contentChange = ReceiptServices(changedContent, fresh, later);
        AssertWithheld(await contentChange.GetRequiredService<ApprovedTimeExportService>().GenerateAsync(context, command, later, token));
        fresh.Submitted.ShouldBeNull();
        (await fresh.Actor(firstCommand.AggregateId).GetEventsAsync(0)).ShouldHaveSingleItem().MessageId.ShouldBe(envelope.MessageId);

        // The actor also refuses a second append if two independent submissions saw an empty owner.
        var occupied = new DomainServiceCurrentState(null, [], 0, 1);
        (await receiver.ProcessAsync(firstCommand, occupied, token)).IsNoOp.ShouldBeTrue();
        var emptyOwner = new DomainServiceCurrentState(null, [], 0, 0);
        (await receiver.ProcessAsync(firstCommand, emptyOwner, token)).Events.ShouldHaveSingleItem();
        (await receiver.ProcessAsync(firstCommand, JsonSerializer.SerializeToElement(emptyOwner, JsonSerializerOptions.Web), token))
            .Events.ShouldHaveSingleItem();
        (await receiver.ProcessAsync(firstCommand, JsonSerializer.SerializeToElement(new { events = Array.Empty<object>() }), token))
            .IsNoOp.ShouldBeTrue();
        (await receiver.ProcessAsync(firstCommand with { CommandType = "unrelated-command" }, null, token)).IsNoOp.ShouldBeTrue();
        foreach (var untrusted in new[]
        {
            firstCommand with { Extensions = null },
            firstCommand with { Extensions = new Dictionary<string, string> { [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets" } },
            firstCommand with { UserId = "timesheets" },
            firstCommand with { TenantId = "another-tenant" },
            firstCommand with { AggregateId = "another-owner" }
        })
        {
            (await receiver.ProcessAsync(untrusted, null, token)).IsNoOp.ShouldBeTrue();
        }

        foreach (var fault in new Func<StreamReadPage, StreamReadPage>[]
        {
            page => page with { Tenant = "another-tenant" },
            page => page with { AggregateId = "another-owner" },
            page => page with { Metadata = page.Metadata with { LatestSequence = 2 } },
            page => page with { Events = [page.Events[0] with { CorrelationId = "another-command" }] },
            page => page with { Events = [page.Events[0] with { MessageId = "invalid-event-id" }] },
            page =>
            {
                var stored = JsonSerializer.Deserialize<StoredApprovedTimeExported>(page.Events[0].Payload, JsonSerializerOptions.Web)!;
                var changed = stored with { Event = stored.Event with { GeneratedAtUtc = stored.Event.GeneratedAtUtc.AddSeconds(1) } };
                return page with { Events = [page.Events[0] with { Payload = JsonSerializer.SerializeToUtf8Bytes(changed) }] };
            },
            page => page with { Events = [page.Events[0] with { UserId = "timesheets" }] },
            page => page with { Events = [page.Events[0] with { Payload = "{}"u8.ToArray() }] },
            page => page with { Events = [page.Events[0] with { EventTypeName = typeof(ApprovedTimeExported).FullName! }] },
            page => throw new HttpRequestException("Workload gateway unavailable.")
        })
        {
            fresh.Readback = fault;
            AssertWithheld(await serviceAfterRestart.GenerateAsync(context, command, later, token));
        }
        using var cancelled = new CancellationTokenSource();
        fresh.Readback = page => { cancelled.Cancel(); return page; };
        await Should.ThrowAsync<OperationCanceledException>(() =>
            serviceAfterRestart.GenerateAsync(context, command, later, cancelled.Token).AsTask());
    }

    [Theory]
    [InlineData("workload")]
    [InlineData("scope")]
    public async Task RefusedFinanceAttemptLeavesOwnerEmptyAndAnAuthorizedRetryCommitsAtTheSameClock(string denial)
    {
        var now = DateTimeOffset.UtcNow;
        var token = TestContext.Current.CancellationToken;
        var context = Context();
        var command = new GenerateApprovedTimeExport { LedgerQuery = new() { Project = Project(), Contributor = Contributor(), BillableState = BillableState.Billable } };
        bool receiverDenied = true;
        bool rechecking = false;
        bool revoked = false;
        var guard = Substitute.For<ITimesheetsAccessGuard>();
        var scopeChecks = new List<TimesheetsAuthorizationRequest>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<TimesheetsAuthorizationRequest>();
            if (request.Operation == TimesheetsOperation.Export) scopeChecks.Add(request);
            if (rechecking && request.Operation == TimesheetsOperation.ProjectionRead) revoked = true;
            var refused = (denial == "scope" && receiverDenied && scopeChecks.Count == 2 || revoked)
                && request.Operation == TimesheetsOperation.Export && request.Project == Project();
            return ValueTask.FromResult(refused ? TimesheetsAuthorizationDecision.Denied("Scoped permission missing.") : TimesheetsAuthorizationDecision.Allowed());
        });
        using var gateway = new FinanceReceiptActorGateway(new(), context.Actor!.PartyId)
        {
            ReturnEmptyStreamPage = denial == "scope",
            BeforeProcess = e => denial == "workload" && receiverDenied ? e with { Extensions = null } : e
        };
        using var provider = ReceiptServices(ReceiptLedger(), gateway, now, guard);
        using var scope = provider.CreateScope();
        gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        var service = provider.GetRequiredService<ApprovedTimeExportService>();
        AssertWithheld(await service.GenerateAsync(context, command, now, token));
        string owner = gateway.Submitted!.AggregateId;
        (await gateway.Actor(owner).GetEventsAsync(0)).ShouldBeEmpty();
        receiverDenied = false;
        (await service.GenerateAsync(context, command, now, token)).WasGenerated.ShouldBeTrue();
        (await gateway.Actor(owner).GetEventsAsync(0)).ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(StoredApprovedTimeExported).FullName);

        // Revoke the scoped export grant while the recovered rows are being reauthorized.
        gateway.Readback = page => { rechecking = true; return page; };
        var recovery = await service.GenerateAsync(context, command, now, token);
        recovery.Authorization.IsAuthorized.ShouldBeFalse();
        recovery.Export.ShouldBeNull();
        scopeChecks.ShouldAllBe(request => request.Project == command.LedgerQuery.Project && request.Work == command.LedgerQuery.Work
            && request.Contributor == command.LedgerQuery.Contributor);
    }

    [Fact]
    public async Task FinanceOwnersIsolateTenantsIndependentRequestsAndFallbackCorrelations()
    {
        var now = DateTimeOffset.UtcNow;
        var ct = TestContext.Current.CancellationToken;
        var storage = new ConcurrentDictionary<(string, string), InMemoryStateManager>();
        async Task<(string Owner, bool Committed)> Generate(string tenant, string correlation, string? requestId,
            IApprovedTimeLedgerProjectionReader ledger)
        {
            using var gateway = new FinanceReceiptActorGateway(storage, Context().Actor!.PartyId);
            using var provider = ReceiptServices(ledger, gateway, now, tenant: tenant);
            using var scope = provider.CreateScope();
            gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
            var result = await provider.GetRequiredService<ApprovedTimeExportService>().GenerateAsync(
                Context() with { Tenant = new(tenant), CorrelationId = correlation },
                new() { RequestId = requestId }, now, ct);
            if (result.WasGenerated)
            {
                result.Export!.Audit.Tenant!.TenantId.ShouldBe(tenant);
            }
            return (gateway.Submitted?.AggregateId ?? "", result.WasGenerated);
        }
        var first = ReceiptLedger();
        var second = ReceiptLedger("Tenant-B");
        var (a, okA) = await Generate("tenant-1", "trace", "request-A", first);
        var (b, okB) = await Generate("tenant-1", "trace", "request-B", first);
        var (c, okC) = await Generate("Tenant-B", "trace", "request-A", second);
        var (d, okD) = await Generate("tenant-1", "fallback-1", null, first);
        var (e, okE) = await Generate("tenant-1", "fallback-2", null, first);
        new[] { okA, okB, okC, okD, okE }.ShouldAllBe(ok => ok);
        new[] { a, b, c, d, e }.Distinct().Count().ShouldBe(5);
        (await Generate("tenant-1", "fallback-1", null, first)).ShouldBe(("", true));
        (await Generate("tenant-1", "different-trace", "fallback-1", first)).ShouldBe(("", false));
        (await Generate("Tenant-B", "later", "request-A", second)).ShouldBe(("", true));
        // The storage alias reaches the same owner. It cannot inherit the originally authorized exact Timesheets tenant.
        (await Generate("tenant-b", "later", "request-A", ReceiptLedger("tenant-b"))).ShouldBe(("", false));
        storage.Keys.ShouldContain(("tenant-b", c));
        storage.Count.ShouldBe(5);
    }

    [Fact]
    public async Task RecoveredReceiptBindsRedactedCorrectionRowsAndSnapshotIndependentlyOfCsv()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var context = Context();
        var command = new GenerateApprovedTimeExport { RequestId = "row-digest", LedgerQuery = new()
            { BillableState = BillableState.Billable, TenantLocalPeriodKey = " 2026-06 ", PageSize = 0 } };
        var reader = Substitute.For<IApprovedTimeLedgerProjectionReader>();
        var originalPage = (await ReceiptLedger().QueryAsync(context, command.LedgerQuery, ct))!;
        var row = originalPage.Items.Single() with { ApprovedCorrection = GoldenApprovedCorrection("finance-entry-123") };
        var page = originalPage with { Items = [row], ExportSnapshotId = null };
        reader.QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryApprovedTimeLedger>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page));
        using var gateway = new FinanceReceiptActorGateway(new(), context.Actor!.PartyId);
        using var provider = ReceiptServices(reader, gateway, now);
        using var scope = provider.CreateScope();
        gateway.Connect(scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        var service = provider.GetRequiredService<ApprovedTimeExportService>();
        var originalFile = (await service.GenerateAsync(context, command, now, ct)).Export!;
        originalFile.HasOutput.ShouldBeTrue();
        originalFile.CsvContent.ShouldNotBeNull().ShouldContain("private finance note");
        var normalized = command with { LedgerQuery = command.LedgerQuery with { TenantLocalPeriodKey = "2026-06", PageSize = 50 } };
        (await service.GenerateAsync(context, normalized, now, ct)).WasGenerated.ShouldBeTrue();
        var published = row.Comment.ShouldNotBeNull();
        page = page with { Items = [row with { Comment = new(published.Text, published.Policy with
        {
            ServerPublicationVersion = 2,
            ExportInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerExportInclusion = null
        }) }] };
        AssertWithheld(await service.GenerateAsync(context, normalized, now, ct));
        (await gateway.Actor(gateway.Submitted!.AggregateId).GetEventsAsync(0)).ShouldHaveSingleItem();
        page = page with { Items = [row] };
        page = page with { ExportSnapshotId = "new-snapshot" };
        AssertWithheld(await service.GenerateAsync(context, normalized, now, ct));
        page = page with { ExportSnapshotId = null, ProjectionFreshness = originalPage.ProjectionFreshness with { Cursor = "changed-cursor" } };
        AssertWithheld(await service.GenerateAsync(context, normalized, now, ct));
        page = page with { ProjectionFreshness = originalPage.ProjectionFreshness with { AsOfUtc = now.AddDays(-1) } };
        AssertWithheld(await service.GenerateAsync(context, normalized, now, ct));
        page = page with { ProjectionFreshness = originalPage.ProjectionFreshness, Items = [row with
            { ApprovedCorrection = row.ApprovedCorrection! with { Reason = new("different historical reason") } }] };
        AssertWithheld(await service.GenerateAsync(context, normalized, now, ct));
        var independentlyRequested = await service.GenerateAsync(context, normalized with { RequestId = "different-request" }, now, ct);
        independentlyRequested.WasGenerated.ShouldBeTrue();
        independentlyRequested.Export!.CsvContent.ShouldBe(originalFile.CsvContent);
        independentlyRequested.Export.Audit.OutputRowsHashSha256.ShouldNotBe(originalFile.Audit.OutputRowsHashSha256);
    }

    private static FrozenLedgerTestReader ReceiptLedger(string tenant = "tenant-1") => new(new ApprovedTimeLedgerProjection(FinanceExportTestPolicy.Configured),
        [new TimeEntryProjectionEvent("r1", 1, Recorded("finance-entry-123", TimeEntryTargetReference.ForProject(Project()), 30) with
            {
                Comment = new("private finance note", TimeEntryCommentPolicy.SensitiveDefault with
                {
                    ServerPublicationVersion = 3,
                    ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
                    ServerExportInclusion = TimesheetsCommentPolicyDecision.Allowed
                })
            }, tenant),
         new("r2", 2, Submitted("finance-entry-123") with { Tenant = new(tenant) }, tenant),
         new("r3", 3, Approved("finance-entry-123", "finance-decision") with { Tenant = new(tenant) }, tenant)],
        new(tenant, ApprovedTimeLedgerProjection.ProjectionName, 3, ProjectionFreshness.Fresh));

    private static ServiceProvider ReceiptServices(IApprovedTimeLedgerProjectionReader reader, FinanceReceiptActorGateway gateway,
        DateTimeOffset now, ITimesheetsAccessGuard? access = null, string tenant = "tenant-1")
    {
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var store = new InMemoryReadModelStore();
        return new ServiceCollection()
            .AddSingleton<IReadModelStore>(store).AddSingleton<IReadModelBatchStore>(store)
            .AddSingleton(clock).AddSingleton(gateway.Client).AddSingleton(FinanceExportTestPolicy.Configured with
            {
                ExportCommentsAllowedByTenant = new Dictionary<string, bool>(StringComparer.Ordinal) { [tenant] = true }
            })
            .AddSingleton<ITimesheetsAccessGuard>(access ?? new AllowAllAccessGuard())
            .AddSingleton(reader).AddSingleton<ITimeEntryDisplayHydrator>(new StaticHydrator())
            .AddTimesheetsServerKernel().AddTimesheetsMagicLinkEventStoreProcessing()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void AssertWithheld(ApprovedTimeExportResult result)
    {
        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        var blocked = result.Export.ShouldNotBeNull();
        blocked.Rows.ShouldBeEmpty();
        blocked.CsvContent.ShouldBeNull();
        blocked.Audit.RowCount.ShouldBe(0);
        blocked.Audit.OutputContentHashSha256.ShouldBeNull();
    }
}
