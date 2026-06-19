using System.Text.Json;

using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApprovalAuthorityPolicyE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Configured_project_approval_workflow_records_policy_source_evidence_and_safe_json()
    {
        AllowAllAccessGuard accessGuard = new();
        TimesheetsApprovalAuthorityResolver resolver = new(
            new TimesheetsApprovalAuthorityPolicyOptions
            {
                PolicyVersion = "v2"
            },
            [
                new FixedAuthorityProvider(
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthoritySourceResult.Allowed(
                        ApprovalAuthoritySource.ProjectApprover,
                        ProjectionFreshnessMetadata.Fresh))
            ],
            accessGuard);

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.None);
        result.SourceAttribution.Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        result.SourceAttribution.PolicyKey.ShouldBe(TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey);
        result.SourceAttribution.PolicyVersion.ShouldBe("v2");
        result.SourceAttribution.Freshness.State.ShouldBe(ProjectionFreshnessState.Fresh);

        TimesheetsAuthorizationRequest authorizationRequest = accessGuard.Requests.ShouldHaveSingleItem();
        authorizationRequest.Operation.ShouldBe(TimesheetsOperation.Command);
        authorizationRequest.ApprovalAction.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        authorizationRequest.Project.ShouldBe(Project());
        authorizationRequest.Contributor.ShouldBe(Contributor());

        string json = JsonSerializer.Serialize(result, JsonOptions);

        json.ShouldContain("\"isAllowed\":true");
        json.ShouldContain("\"source\":\"ProjectApprover\"");
        json.ShouldContain("\"decisionState\":\"Allowed\"");
        json.ShouldContain("\"policyVersion\":\"v2\"");
        AssertJsonOmitsProtectedAuthorityMaterial(json);
    }

    [Fact]
    public async Task Default_kernel_authority_workflow_fails_closed_when_authority_sources_are_unavailable()
    {
        AllowAllAccessGuard accessGuard = new();
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<ITimesheetsAccessGuard>(accessGuard);
        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITimesheetsApprovalAuthorityResolver resolver = provider.GetRequiredService<ITimesheetsApprovalAuthorityResolver>();

        ApprovalAuthorityResolutionResult result = await resolver.ResolveAsync(
            Request(ApprovalAuthorityAction.EntryApproval),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        result.Reason.ShouldBe("Authority cannot be resolved.");
        result.SourceAttribution.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        result.SourceAttribution.DecisionState.ShouldBe(ApprovalAuthorityDecisionState.Unavailable);
        result.SourceAttribution.Freshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        provider.GetServices<IApprovalAuthoritySourceProvider>()
            .Select(static sourceProvider => sourceProvider.Source)
            .ShouldBe(
            [
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthoritySource.WorkOwner,
                ApprovalAuthoritySource.TenantAdministrator,
                ApprovalAuthoritySource.FinanceReviewer
            ]);
        accessGuard.Requests.ShouldHaveSingleItem().ApprovalAction.ShouldBe(ApprovalAuthorityAction.EntryApproval);

        string json = JsonSerializer.Serialize(result, JsonOptions);

        json.ShouldContain("\"isAllowed\":false");
        json.ShouldContain("\"source\":\"ProjectApprover\"");
        json.ShouldContain("\"decisionState\":\"Unavailable\"");
        AssertJsonOmitsProtectedAuthorityMaterial(json);
    }

    [Fact]
    public void Metadata_endpoint_payload_exposes_approval_surfaces_and_blocking_copy_without_protected_identifiers()
    {
        var response = new
        {
            Module = "Hexalith.Timesheets",
            ContractVersion = "1.0",
            Capabilities = TimesheetsMetadataCatalog.Descriptors
                .Select(static descriptor => descriptor.Capability)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            MetadataDescriptors = TimesheetsMetadataCatalog.Descriptors
                .Select(static descriptor => descriptor.Name)
                .ToArray()
        };

        string endpointJson = JsonSerializer.Serialize(response, JsonOptions);

        endpointJson.ShouldContain("\"approval\"");
        endpointJson.ShouldContain("timesheets.approvals.queue");
        endpointJson.ShouldContain("timesheets.command.time-entry-approval");
        endpointJson.ShouldContain("timesheets.command.period-approval");
        AssertJsonOmitsProtectedAuthorityMaterial(endpointJson);

        string approvalMetadataJson = JsonSerializer.Serialize(
            TimesheetsMetadataCatalog.Descriptors
                .Where(static descriptor => descriptor.Capability == "approval")
                .ToArray(),
            JsonOptions);

        approvalMetadataJson.ShouldContain("Approve entry");
        approvalMetadataJson.ShouldContain("Reject entry");
        approvalMetadataJson.ShouldContain("Approve period");
        approvalMetadataJson.ShouldContain("Reject period");
        approvalMetadataJson.ShouldContain("Authority cannot be resolved.");
        approvalMetadataJson.ShouldContain("authorityFreshness");
        approvalMetadataJson.ShouldContain("authorityDecision");
        AssertJsonOmitsProtectedAuthorityMaterial(approvalMetadataJson);
    }

    private static ApprovalAuthorityResolutionRequest Request(ApprovalAuthorityAction action)
    {
        return new(
            new TimesheetsAuthorizationRequest(
                new TimesheetsRequestContext(
                    new TenantReference("tenant-1"),
                    Actor(),
                    "correlation-1"),
                TimesheetsOperation.Command)
            {
                Project = Project(),
                Contributor = Contributor()
            },
            action,
            Contributor());
    }

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Actor() => new("party-approver");

    private static PartyReference Contributor() => new("party-contributor");

    private static void AssertJsonOmitsProtectedAuthorityMaterial(string json)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "tenantId",
            "userId",
            "partyId",
            "projectId",
            "workId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "roles",
            "stream",
            "sequence"
        ];

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }

    private sealed class FixedAuthorityProvider(
        ApprovalAuthoritySource source,
        ApprovalAuthoritySourceResult result)
        : IApprovalAuthoritySourceProvider
    {
        public ApprovalAuthoritySource Source { get; } = source;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);

            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(
                request.UiAction ?? TimesheetsUiAction.Approval));
        }
    }
}
