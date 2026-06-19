using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class DiagnosticsPrivacyTests
{
    private static readonly string[] SensitiveLoggingTerms =
    [
        "command body",
        "commandBody",
        "event payload",
        "eventPayload",
        "comment",
        "comments",
        "full request body",
        "requestBody",
        "response body",
        "responseBody",
        "raw command",
        "raw provider",
        "provider request",
        "provider response",
        "prompt",
        "prompts",
        "response",
        "responses",
        "personal data",
        "personalData",
        "PartyDisplayName",
        "ProjectDisplayName",
        "WorkDisplayName",
        "sibling raw problem",
        "token value",
        "token count",
        "tokens",
        "token",
        "bearer",
        "secret",
        "api key",
        "magic-link",
        "PartyReference.Value",
        "ProjectReference.Value",
        "WorkReference.Value"
    ];

    [Fact]
    public void Source_logging_does_not_include_sensitive_payload_or_identifier_material()
    {
        string[] sourceFiles = Directory.GetFiles(RepositoryRoot.PathTo("src"), "*.cs", SearchOption.AllDirectories);
        sourceFiles.ShouldNotBeEmpty();

        foreach (string sourceFile in sourceFiles)
        {
            string[] loggingLines = File.ReadAllLines(sourceFile)
                .Where(static line => line.Contains("Log", StringComparison.Ordinal)
                    || line.Contains("logger", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string loggingLine in loggingLines)
            {
                foreach (string sensitiveTerm in SensitiveLoggingTerms)
                {
                    loggingLine.ShouldNotContain(sensitiveTerm, Case.Insensitive, sourceFile);
                }
            }
        }
    }

    [Fact]
    public void Host_metadata_endpoint_exposes_correlation_safe_module_metadata_only()
    {
        string hostProgram = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));

        hostProgram.ShouldContain("MapGet");
        hostProgram.ShouldContain("/metadata/timesheets");
        hostProgram.ShouldContain("Hexalith.Timesheets");
        hostProgram.ShouldContain("timesheets");
        hostProgram.ShouldContain("Hexalith.Timesheets.Server");
        hostProgram.ShouldNotContain("TenantReference");
        hostProgram.ShouldNotContain("PartyReference");
        hostProgram.ShouldNotContain("ProjectReference");
        hostProgram.ShouldNotContain("WorkReference");
        hostProgram.ShouldNotContain("token", Case.Insensitive);
        hostProgram.ShouldNotContain("secret", Case.Insensitive);
    }

    [Fact]
    public void Policy_guidance_omits_protected_payloads_sibling_state_and_finance_ownership_language()
    {
        string[] artifactPaths =
        [
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts", "openapi", "timesheets-capture-contracts.v1.json"),
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts", "openapi", "timesheets-evidence-policy.v1.md")
        ];

        foreach (string artifactPath in artifactPaths)
        {
            string content = File.ReadAllText(artifactPath);
            if (Path.GetExtension(artifactPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                System.Text.Json.Nodes.JsonNode artifact =
                    System.Text.Json.Nodes.JsonNode.Parse(content)
                    ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");
                content = artifact["x-hexalith-evidence-policy"]?.ToJsonString()
                    ?? throw new InvalidOperationException("Policy guidance metadata is missing.");
            }

            content.ShouldContain("Comments are excluded from diagnostics.");
            content.ShouldNotContain("EventStore envelope", Case.Insensitive);
            content.ShouldNotContain("command body", Case.Insensitive);
            content.ShouldNotContain("event payload", Case.Insensitive);
            content.ShouldNotContain("Party display", Case.Insensitive);
            content.ShouldNotContain("Project name", Case.Insensitive);
            content.ShouldNotContain("Work name", Case.Insensitive);
            content.ShouldNotContain("token", Case.Insensitive);
            content.ShouldNotContain("secret", Case.Insensitive);
            AssertNoFinanceOwnershipLanguage(content);
        }
    }

    [Fact]
    public void Evidence_detail_contract_schemas_expose_no_envelope_or_identifier_fields()
    {
        string openApiPath = RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json");

        System.Text.Json.Nodes.JsonNode artifact =
            System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(openApiPath))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        System.Text.Json.Nodes.JsonNode schemas =
            artifact["components"]?["schemas"]
            ?? throw new InvalidOperationException("OpenAPI schema components are missing.");

        AssertClosedSchema(schemas, "TimeEntryEventLineageItem", ["eventName", "ordinal", "sourceAuthority"]);
        AssertClosedSchema(schemas, "TimeEntryHydratedDisplayLabel", ["state", "label", "asOfUtc", "detail"]);
        AssertClosedSchema(schemas, "TimeEntryDisplayHydration", ["contributor", "target", "activityType"]);
    }

    [Fact]
    public void Approval_decision_schema_exposes_only_stable_evidence_without_display_or_raw_authority_material()
    {
        string openApiPath = RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json");

        System.Text.Json.Nodes.JsonNode artifact =
            System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(openApiPath))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        System.Text.Json.Nodes.JsonNode schemas =
            artifact["components"]?["schemas"]
            ?? throw new InvalidOperationException("OpenAPI schema components are missing.");

        System.Text.Json.Nodes.JsonNode schema = schemas["TimeEntryApprovalDecisionEvidence"]
            ?? throw new InvalidOperationException("Approval decision evidence schema is missing.");

        bool additionalProperties = schema["additionalProperties"]?.GetValue<bool>()
            ?? throw new InvalidOperationException("Approval decision evidence schema must declare additionalProperties:false.");
        additionalProperties.ShouldBeFalse();

        System.Text.Json.Nodes.JsonObject properties = schema["properties"]?.AsObject()
            ?? throw new InvalidOperationException("Approval decision evidence schema has no properties.");

        properties.Select(static property => property.Key).ShouldBe([
            "timeEntryId",
            "timeEntryApprovalDecisionId",
            "approver",
            "tenant",
            "decidedAtUtc",
            "approvalState",
            "approvalScope",
            "authoritySource",
            "reason"
        ]);

        string schemaJson = schema.ToJsonString();
        schemaJson.ShouldNotContain("displayName", Case.Insensitive);
        schemaJson.ShouldNotContain("role", Case.Insensitive);
        schemaJson.ShouldNotContain("claim", Case.Insensitive);
        schemaJson.ShouldNotContain("token", Case.Insensitive);
        schemaJson.ShouldNotContain("envelope", Case.Insensitive);
        schemaJson.ShouldNotContain("messageId", Case.Insensitive);
        schemaJson.ShouldNotContain("correlationId", Case.Insensitive);
        schemaJson.ShouldNotContain("payload body", Case.Insensitive);
    }

    private static void AssertClosedSchema(
        System.Text.Json.Nodes.JsonNode schemas,
        string schemaName,
        string[] allowedProperties)
    {
        System.Text.Json.Nodes.JsonNode schema = schemas[schemaName]
            ?? throw new InvalidOperationException($"Schema '{schemaName}' is missing.");

        bool additionalProperties = schema["additionalProperties"]?.GetValue<bool>()
            ?? throw new InvalidOperationException($"Schema '{schemaName}' must declare additionalProperties:false.");
        additionalProperties.ShouldBeFalse(schemaName);

        System.Text.Json.Nodes.JsonObject properties = schema["properties"]?.AsObject()
            ?? throw new InvalidOperationException($"Schema '{schemaName}' has no properties.");

        foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> property in properties)
        {
            allowedProperties.ShouldContain(property.Key, $"{schemaName}.{property.Key}");

            foreach (string forbidden in ForbiddenContractPropertyFragments)
            {
                property.Key.ShouldNotContain(forbidden, Case.Insensitive, schemaName);
            }
        }
    }

    private static readonly string[] ForbiddenContractPropertyFragments =
    [
        "messageId",
        "sequenceNumber",
        "stream",
        "envelope",
        "tenant",
        "correlation",
        "causation",
        "payload",
        "token",
        "secret",
        "jwt"
    ];

    private static void AssertNoFinanceOwnershipLanguage(string content)
    {
        foreach (string forbiddenWord in new[] { "invoice", "payroll", "rate", "tax", "revenue" })
        {
            System.Text.RegularExpressions.Regex.IsMatch(
                content,
                $@"\b{forbiddenWord}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)).ShouldBeFalse(forbiddenWord);
        }
    }
}
