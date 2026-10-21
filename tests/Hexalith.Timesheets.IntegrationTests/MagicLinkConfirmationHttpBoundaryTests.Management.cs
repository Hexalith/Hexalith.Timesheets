using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

using NSubstitute;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Checks the opt-in local host supplies the tenant gate with the same isolated authority rules.</summary>
    /// <param name="tenant">The proposed tenant identifier.</param>
    /// <param name="actor">The optional actor identifier.</param>
    /// <param name="allowed">Whether the fixture should admit the request.</param>
    [Theory]
    [InlineData("live-dapr-management", "operator", true)]
    [InlineData("ordinary-tenant", "operator", false)]
    [InlineData("live-dapr-management", null, false)]
    public async Task LocalFixtureManagementAdmissionPreservesIsolatedAuthority(string tenant, string? actor, bool allowed)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useLiveFixtureAdmission: true);
        ITimesheetsTenantAccessValidator admission = factory.Services.GetRequiredService<ITimesheetsTenantAccessValidator>();
        admission.ShouldBeSameAs(factory.Services.GetRequiredService<ITimesheetsAccessGuard>());
        var context = TimesheetsServerRequestContext.FromTrustedSources(tenant, actor, "fixture-management-admission");

        TimesheetsTenantAccessResult result = await admission.ValidateAsync(
            context, TimesheetsOperation.MagicLinkIssuance, TestContext.Current.CancellationToken);

        result.IsAuthorized.ShouldBe(allowed);
    }

    /// <summary>Checks missing transition identifiers return opaque refusals before protected work.</summary>
    /// <param name="action">The management transition.</param>
    /// <param name="omit">Whether the identifier is omitted instead of explicitly null.</param>
    [Theory]
    [InlineData("revoke", false)]
    [InlineData("revoke", true)]
    [InlineData("expire", false)]
    [InlineData("expire", true)]
    public async Task ManagementTransitionsRejectMissingIdsBeforeProtectedWork(string action, bool omit)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        var generator = new CountingMagicLinkTokenGenerator();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader, tokenGenerator: generator);
        using HttpClient client = factory.CreateClient();
        JsonObject body = JsonSerializer.SerializeToNode(ManagementBody(action), JsonOptions)!.AsObject();
        if (omit) body.Remove("capabilityId");
        else body["capabilityId"] = null;

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute(action), body);

        await CaptureFailureAsync(response, "missing-management-id", string.Empty);
        loader.ReceivedCalls().ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(0);
        factory.TenantAccessValidator.ReceivedCalls().ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, "management-capability").ShouldBeEmpty();
        generator.GenerationCount.ShouldBe(0);
    }

    /// <summary>Checks incomplete issuance cannot load state or generate a token.</summary>
    /// <param name="field">The required field to remove.</param>
    /// <param name="omit">Whether the field is omitted instead of explicitly null.</param>
    [Theory]
    [InlineData("contributor", false)]
    [InlineData("contributor", true)]
    [InlineData("timeEntryId", false)]
    [InlineData("timeEntryId", true)]
    [InlineData("source", false)]
    [InlineData("source", true)]
    [InlineData("scope", false)]
    [InlineData("scope", true)]
    public async Task IssuanceRejectsIncompleteBodiesWithoutReadingOrGeneratingTokens(string field, bool omit)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        var generator = new CountingMagicLinkTokenGenerator();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader, tokenGenerator: generator);
        using HttpClient client = factory.CreateClient();
        JsonObject body = JsonSerializer.SerializeToNode(ManagementBody("issue"), JsonOptions)!.AsObject();
        JsonObject owner = field is "contributor" or "timeEntryId" ? body["scope"]!.AsObject() : body;
        if (omit) owner.Remove(field);
        else owner[field] = null;

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute("issue"), body);

        await CaptureFailureAsync(response, "missing-issuance-field", string.Empty);
        loader.ReceivedCalls().ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(0);
        factory.TenantAccessValidator.ReceivedCalls().ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, "management-capability").ShouldBeEmpty();
        generator.GenerationCount.ShouldBe(0);
    }

    /// <summary>Checks the management gate refuses access before catalog or capability reads.</summary>
    /// <param name="action">The management action.</param>
    [Theory]
    [InlineData("issue")]
    [InlineData("revoke")]
    [InlineData("expire")]
    public async Task ManagementDenialPrecedesAllStateReads(string action)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        var generator = new CountingMagicLinkTokenGenerator();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader, tokenGenerator: generator);
        using HttpClient client = factory.CreateClient();
        factory.AccessGuard.DecisionOverride = request =>
        {
            request.Operation.ShouldBe(TimesheetsOperation.MagicLinkIssuance);
            request.Context.Tenant.ShouldBe(Tenant());
            return TimesheetsAuthorizationDecision.Denied("management-denied");
        };
        factory.TenantAccessValidator.ValidateAsync(Arg.Is<TimesheetsRequestContext>(context => context.Tenant == Tenant()),
            TimesheetsOperation.MagicLinkIssuance, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.InsufficientRole, "management-denied"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute(action), ManagementBody(action));

        await CaptureFailureAsync(response, "management-denied", string.Empty);
        loader.ReceivedCalls().ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(action == "issue" ? 1 : 0);
        factory.TenantAccessValidator.ReceivedCalls().Count().ShouldBe(action == "issue" ? 0 : 1);
        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, "management-capability").ShouldBeEmpty();
        generator.GenerationCount.ShouldBe(0);
    }

    /// <summary>Checks correctly signed assertions need the management operation permission.</summary>
    /// <param name="action">The management action.</param>
    /// <param name="operation">The unrelated signed operation.</param>
    [Theory]
    [InlineData("issue", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("revoke", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("expire", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("issue", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("revoke", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("expire", EventStoreWorkloadOperations.DomainServiceProject)]
    public async Task ManagementRejectsSignedAssertionsForUnrelatedOperations(string action, string operation)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute(action), ManagementBody(action), operation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        loader.ReceivedCalls().ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(0);
        factory.TenantAccessValidator.ReceivedCalls().ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    /// <summary>Checks coarse admission never replaces resource-level transition authorization.</summary>
    /// <param name="action">The management transition.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("expire")]
    public async Task ManagementStillRequiresResourceAuthorizationAfterCoarseAdmission(string action)
    {
        var capabilityId = new MagicLinkCapabilityId("management-capability");
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, new TimeEntryId("management-entry"));
        if (action == "expire")
        {
            StreamReadEvent seeded = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
            MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seeded);
            factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seeded with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
            });
        }
        int submissions = factory.Gateway.SubmissionCount;
        int authorizations = factory.AccessGuard.AuthorizationCount;
        factory.Gateway.Requests.Clear();
        factory.AccessGuard.DecisionOverride = request =>
        {
            request.Contributor.ShouldBe(Contributor());
            request.Project.ShouldBe(Project());
            return TimesheetsAuthorizationDecision.Denied("resource-denied");
        };

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute(action), ManagementBody(action));

        await CaptureFailureAsync(response, "resource-denied", string.Empty);
        factory.Gateway.Requests.ShouldNotBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(authorizations + 1);
        factory.Gateway.SubmissionCount.ShouldBe(submissions);
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).Length.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, "management-entry").Length.ShouldBe(1);
    }

    /// <summary>Checks issuance admission authorizes the request's known contributor and target.</summary>
    /// <param name="workTarget">Whether the target belongs to Works rather than Projects.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuanceReferenceDenialPrecedesStateReads(bool workTarget)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        factory.AccessGuard.DecisionOverride = request =>
        {
            request.Contributor.ShouldBe(Contributor());
            request.Project.ShouldBe(workTarget ? null : Project());
            request.Work.ShouldBe(workTarget ? new("management-work") : null);
            return TimesheetsAuthorizationDecision.Denied("reference-denied");
        };
        IssueMagicLinkConfirmationCapability command = (IssueMagicLinkConfirmationCapability)ManagementBody("issue");
        if (workTarget)
        {
            command = command with { Scope = command.Scope with { Target = TimeEntryTargetReference.ForWork(new("management-work")) } };
        }

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, ManagementRoute("issue"), command);

        await CaptureFailureAsync(response, "reference-denied", string.Empty);
        loader.ReceivedCalls().ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    private static string ManagementRoute(string action)
        => "/api/timesheets/magic-links/confirmation-capabilities"
            + (action == "issue" ? string.Empty : $"/management-capability/{action}");

    private static object ManagementBody(string action)
    {
        var id = new MagicLinkCapabilityId("management-capability");
        var source = new MagicLinkAuditMetadata("timesheets", "management-test");
        return action switch
        {
            "issue" => new IssueMagicLinkConfirmationCapability(id,
                new(Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                    new TimeEntryId("management-entry"), MagicLinkTargetKind.ProposedTimeEntry),
                MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1), source),
            "revoke" => new RevokeMagicLinkConfirmationCapability(id, source),
            "expire" => new ExpireMagicLinkConfirmationCapability(id, source),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }
}
