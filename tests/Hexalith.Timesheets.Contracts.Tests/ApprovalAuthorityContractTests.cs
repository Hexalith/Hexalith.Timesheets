using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ApprovalAuthorityContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Approval_authority_enums_expose_unknown_zero_sentinels_and_json_strings()
    {
        Enum.GetName((ApprovalAuthorityAction)0).ShouldBe("Unknown");
        Enum.GetName((ApprovalAuthoritySource)0).ShouldBe("Unknown");
        Enum.GetName((ApprovalAuthorityDecisionState)0).ShouldBe("Unknown");

        ApprovalAuthoritySourceAttribution evidence = new(
            ApprovalAuthorityAction.EntryApproval,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"action\":\"EntryApproval\"");
        json.ShouldContain("\"source\":\"ProjectApprover\"");
        json.ShouldContain("\"decisionState\":\"Allowed\"");
        AssertJsonOmitsCallerAuthority(json);

        ApprovalAuthoritySourceAttribution? roundTripped =
            JsonSerializer.Deserialize<ApprovalAuthoritySourceAttribution>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        roundTripped.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        roundTripped.DecisionState.ShouldBe(ApprovalAuthorityDecisionState.Allowed);
        roundTripped.Freshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public void Approval_authority_action_vocabulary_covers_entry_period_correction_and_export()
    {
        Enum.GetNames<ApprovalAuthorityAction>().ShouldBe(
        [
            "Unknown",
            "EntryApproval",
            "EntryRejection",
            "PeriodApproval",
            "PeriodRejection",
            "CorrectionAuthorization",
            "ApprovedTimeExportEligibility"
        ]);
    }

    [Fact]
    public void Metadata_catalog_declares_approval_queue_and_approval_command_surfaces()
    {
        TimesheetsMetadataDescriptor queue = Descriptor("timesheets.approvals.queue");
        TimesheetsMetadataDescriptor entryCommand = Descriptor("timesheets.command.time-entry-approval");
        TimesheetsMetadataDescriptor periodCommand = Descriptor("timesheets.command.period-approval");

        queue.Fields.Select(static field => field.Name).ShouldContain("authorityFreshness");
        queue.Fields.Select(static field => field.Name).ShouldContain("authorityDecision");
        queue.Fields.Select(static field => field.Name).ShouldContain("authoritySource");
        queue.Fields.Select(static field => field.Name).ShouldContain("blockingState");
        queue.StateBadges.Select(static badge => badge.StateVocabulary)
            .ShouldContain(nameof(ApprovalAuthorityDecisionState));
        queue.StateBadges.Select(static badge => badge.StateVocabulary)
            .ShouldContain(nameof(ProjectionFreshnessState));

        entryCommand.Actions.Select(static action => action.Label).ShouldBe(["Approve entry", "Reject entry"]);
        periodCommand.Actions.Select(static action => action.Label).ShouldBe(["Approve period", "Reject period"]);

        string serialized = JsonSerializer.Serialize(
            new[] { queue, entryCommand, periodCommand },
            JsonOptions);

        string displayText = string.Join(
            " ",
            new[] { queue, entryCommand, periodCommand }.Select(static descriptor => descriptor.ToString()));

        serialized.ShouldContain("Authority cannot be resolved.");
        displayText.ShouldNotContain("invoice", Case.Insensitive);
        displayText.ShouldNotContain("payroll", Case.Insensitive);
        displayText.ShouldNotContain("role", Case.Insensitive);
        AssertJsonOmitsCallerAuthority(JsonSerializer.Serialize(queue, JsonOptions));
    }

    [Fact]
    public void Openapi_artifact_documents_additive_approval_authority_schema()
    {
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(RepositoryPath(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json")))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        JsonObject schemas = artifact["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI schemas node is missing.");

        schemas.ContainsKey("ApprovalAuthorityAction").ShouldBeTrue();
        schemas.ContainsKey("ApprovalAuthoritySource").ShouldBeTrue();
        schemas.ContainsKey("ApprovalAuthorityDecisionState").ShouldBeTrue();
        schemas.ContainsKey("ApprovalAuthoritySourceAttribution").ShouldBeTrue();

        string schemaJson = schemas.ToJsonString();
        schemaJson.ShouldContain("EntryApproval");
        schemaJson.ShouldContain("ApprovedTimeExportEligibility");
        schemaJson.ShouldContain("policyVersion");
        AssertJsonOmitsCallerAuthority(schemaJson, allowTenantId: true);
    }

    private static TimesheetsMetadataDescriptor Descriptor(string name)
        => TimesheetsMetadataCatalog.Descriptors.Single(descriptor => descriptor.Name == name);

    private static void AssertJsonOmitsCallerAuthority(string json, bool allowTenantId = false)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "tenantId",
            "userId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "roles"
        ];

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            if (allowTenantId && forbiddenPropertyName == "tenantId")
            {
                continue;
            }

            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }

    private static string RepositoryPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Timesheets.slnx")))
            {
                return Path.Combine([directory.FullName, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Timesheets repository root.");
    }
}
