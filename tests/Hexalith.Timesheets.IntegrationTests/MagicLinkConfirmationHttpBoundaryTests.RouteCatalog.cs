#if HEXALITH_SOURCE_REFERENCES
using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Proves supported fixture activation preserves receiver admission and unchanged projection state on refusal.</summary>
    /// <param name="scenario">The absent or conflicting generation or projection evidence.</param>
    [Theory]
    [InlineData("unactivated")]
    [InlineData("missing-root")]
    [InlineData("wrong-root")]
    [InlineData("multiple-roots")]
    [InlineData("missing-epoch")]
    [InlineData("wrong-epoch")]
    [InlineData("multiple-epochs")]
    [InlineData("wrong-fingerprint")]
    [InlineData("unknown-projection")]
    [InlineData("foreign-domain")]
    [InlineData("noncanonical-method")]
    public async Task FixtureCatalogRefusesUnboundProjectionWithoutWritingState(string scenario)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        var capability = new MagicLinkCapabilityId("catalog-admission-capability");
        ProjectionDispatchRequest valid = DeadlineDelivery(factory, capability,
            MagicLinkHttpBoundaryFactory.ProjectionEvent(1, new MagicLinkConfirmationCapabilityIssued(
                capability, Tenant(), Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(), new("catalog-entry"),
                MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm, new("catalog-token-hash"),
                ObservedAtUtc.AddDays(1), Operator(), ObservedAtUtc, new("timesheets", "catalog-admission"), true)));
        ProjectionDispatchRequest invalid = scenario switch
        {
            "wrong-fingerprint" => valid with { CatalogFingerprint = new string('f', 64) },
            "unknown-projection" => valid with { ProjectionTypes = ["unknown-projection"] },
            "foreign-domain" => valid with { Request = valid.Request with { Domain = "foreign" } },
            _ => valid
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            scenario == "noncanonical-method" ? "/PROJECT/v2" : "/project/v2")
        {
            Content = JsonContent.Create(invalid, options: JsonOptions)
        };
        request.Headers.Add(DaprAppChannelToken.HeaderName, "timesheets-test-app-channel-token");
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            await factory.IssueAssertionAsync(EventStoreWorkloadOperations.DomainServiceProject));
        if (scenario != "unactivated")
        {
            await factory.AddCatalogHeadersAsync(request);
        }

        string header = scenario.Contains("epoch", StringComparison.Ordinal)
            ? RouteCatalogInvocationHandler.EpochHeader : RouteCatalogInvocationHandler.RootHeader;
        if (scenario.StartsWith("missing-", StringComparison.Ordinal))
        {
            request.Headers.Remove(header);
        }
        else if (scenario is "wrong-root" or "wrong-epoch")
        {
            request.Headers.Remove(header);
            request.Headers.Add(header, scenario == "wrong-root" ? new string('f', 64) : "999");
        }
        else if (scenario.StartsWith("multiple-", StringComparison.Ordinal))
        {
            request.Headers.Add(header, request.Headers.GetValues(header).Single());
        }

        using HttpResponseMessage refused = await client.SendAsync(request, TestContext.Current.CancellationToken);

        refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        store.SnapshotRawEntries(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName).ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);

        using HttpResponseMessage accepted = await factory.PostProjectionAsync(client, "/project/v2", valid);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var epoch = new SharedProjectionEpochCoordinator(store, store);
        var scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        var root = (await epoch.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(epoch, scope);
        (await reader.FindCandidatesAsync(root.TokenRoot, "catalog-token-hash", TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().ShouldBe(new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), capability));
    }
}
#endif
