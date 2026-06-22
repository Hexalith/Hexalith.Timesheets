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
    public void Magic_link_invalid_outcome_category_stays_server_internal_and_off_the_external_contract()
    {
        string serverOutcomeCategory = File.ReadAllText(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Server",
            "MagicLinks",
            "MagicLinkInvalidLinkOutcomeCategory.cs"));
        string denialContract = File.ReadAllText(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "Models",
            "MagicLinks",
            "MagicLinkInvalidLinkDenial.cs"));
        string endpoint = File.ReadAllText(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));
        string[] contractFiles = Directory.GetFiles(
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts"),
            "*.cs",
            SearchOption.AllDirectories);

        serverOutcomeCategory.ShouldContain("MagicLinkInvalidLinkOutcomeCategory");
        denialContract.ShouldNotContain("MagicLinkInvalidLinkOutcomeCategory");
        endpoint.ShouldContain("LogExternalLinkDenial");
        endpoint.ShouldContain("MagicLinkInvalidLinkOutcomeCategory");

        foreach (string contractFile in contractFiles)
        {
            File.ReadAllText(contractFile).ShouldNotContain("MagicLinkInvalidLinkOutcomeCategory", Case.Sensitive, contractFile);
        }
    }

    [Fact]
    public void Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state()
    {
        string adapterRoot = RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Works");
        string[] adapterFiles = Directory.GetFiles(adapterRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        adapterFiles.ShouldNotBeEmpty();

        // No adapter in this project may log, may serialize Works state (or the raw QueryResult payload)
        // outward, or may copy Works internal roll-up structure (OwnEffort) or parent lineage (Parent).
        // Both adapters only deserialize the consumer view inbound and return a typed read model. The Works
        // effort fields (.Estimated/.Done/.Remaining/.Unit) are NOT forbidden project-wide because the
        // planned-effort provider's legitimate payload IS the planned effort; those tokens are forbidden in
        // the authority-gate validator file only (asserted below), and the provider is verified to surface
        // effort exclusively through its typed read-model return.
        string[] projectWideForbiddenTokens =
        [
            "ILogger",
            "logger",
            "LogInformation",
            "LogError",
            "LogWarning",
            "LogDebug",
            "LogTrace",
            "JsonSerializer.Serialize",
            "SerializeToElement",
            "SerializeToUtf8Bytes",
            ".OwnEffort",
            ".Parent"
        ];

        foreach (string adapterFile in adapterFiles)
        {
            string source = File.ReadAllText(adapterFile);

            foreach (string forbidden in projectWideForbiddenTokens)
            {
                source.ShouldNotContain(forbidden, Case.Sensitive, adapterFile);
            }
        }

        // The reference validator is an authority gate, not a data source: it must never read or copy any
        // Works effort field. These effort tokens stay forbidden in the validator file only — the
        // planned-effort provider must read them to do its job.
        string validatorFile = Path.Combine(adapterRoot, "WorksQueryWorkReferenceValidator.cs");
        File.Exists(validatorFile).ShouldBeTrue(validatorFile);
        string validatorSource = File.ReadAllText(validatorFile);
        foreach (string effortToken in new[] { ".Estimated", ".Done", ".Remaining", ".Unit" })
        {
            validatorSource.ShouldNotContain(effortToken, Case.Sensitive, validatorFile);
        }

        // Positive assertion: the planned-effort provider exposes effort only through the typed,
        // source-attributed WorkPlannedEffortReadModel return. It reads the consumer view's effort fields but
        // (proven by the project-wide scan above) never logs them and never serializes Works state outward.
        string providerFile = Path.Combine(adapterRoot, "WorksQueryWorkPlannedEffortProvider.cs");
        File.Exists(providerFile).ShouldBeTrue(providerFile);
        string providerSource = File.ReadAllText(providerFile);
        providerSource.ShouldContain("IWorkPlannedEffortProvider", Case.Sensitive, providerFile);
        providerSource.ShouldContain("ValueTask<WorkPlannedEffortReadModel>", Case.Sensitive, providerFile);
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
    public void Export_metadata_and_golden_fixtures_omit_raw_payloads_and_finance_ownership_language()
    {
        string[] exportMetadataLines = File.ReadAllLines(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "TimesheetsMetadataCatalog.cs"))
            .Where(static line => line.Contains("export", StringComparison.OrdinalIgnoreCase)
                || line.Contains("ApprovedTimeExport", StringComparison.Ordinal))
            .ToArray();
        string[] goldenFiles = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests", "Exports", "Golden"),
            "*.csv",
            SearchOption.AllDirectories);

        goldenFiles.ShouldNotBeEmpty();

        foreach (string content in goldenFiles.Select(File.ReadAllText).Append(string.Join('\n', exportMetadataLines)))
        {
            content.ShouldNotContain("raw EventStore", Case.Insensitive);
            content.ShouldNotContain("EventStore envelope", Case.Insensitive);
            content.ShouldNotContain("command body", Case.Insensitive);
            content.ShouldNotContain("claims", Case.Insensitive);
            content.ShouldNotContain("bearer", Case.Insensitive);
            content.ShouldNotContain("secret", Case.Insensitive);
            content.ShouldNotContain("displayName", Case.Insensitive);
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
        AssertClosedSchema(schemas, "TimeEntryLockEvidence", [
            "lockState",
            "sourceApprovalDecisionId",
            "sourceApprovalScope",
            "lockedBy",
            "lockedAtUtc",
            "explanation"
        ]);
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

    [Fact]
    public void Magic_link_read_schema_exposes_operator_state_without_raw_capability_material()
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

        System.Text.Json.Nodes.JsonNode schema = schemas["MagicLinkConfirmationCapabilityReadModel"]
            ?? throw new InvalidOperationException("Magic-link read model schema is missing.");

        bool additionalProperties = schema["additionalProperties"]?.GetValue<bool>()
            ?? throw new InvalidOperationException("Magic-link read model schema must declare additionalProperties:false.");
        additionalProperties.ShouldBeFalse();

        string schemaJson = schema.ToJsonString();
        schemaJson.ShouldContain("stateBadgeText");
        schemaJson.ShouldContain("expiryBadgeText");
        schemaJson.ShouldNotContain("oneTimeToken");
        schemaJson.ShouldNotContain("rawToken");
        schemaJson.ShouldNotContain("tokenHash");
        schemaJson.ShouldNotContain("decoded");
        schemaJson.ShouldNotContain("comment", Case.Insensitive);
        schemaJson.ShouldNotContain("displayName", Case.Insensitive);
        schemaJson.ShouldNotContain("messageId");
        schemaJson.ShouldNotContain("correlationId");
        schemaJson.ShouldNotContain("envelope", Case.Insensitive);
    }

    [Fact]
    public void Correction_evidence_schema_exposes_only_stable_lineage_without_display_or_raw_authority_material()
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

        System.Text.Json.Nodes.JsonNode schema = schemas["TimeEntryCorrectionEvidence"]
            ?? throw new InvalidOperationException("Correction evidence schema is missing.");

        bool additionalProperties = schema["additionalProperties"]?.GetValue<bool>()
            ?? throw new InvalidOperationException("Correction evidence schema must declare additionalProperties:false.");
        additionalProperties.ShouldBeFalse();

        System.Text.Json.Nodes.JsonObject properties = schema["properties"]?.AsObject()
            ?? throw new InvalidOperationException("Correction evidence schema has no properties.");

        properties.Select(static property => property.Key).ShouldBe([
            "timeEntryId",
            "timeEntryCorrectionId",
            "correctedBy",
            "tenant",
            "correctedAtUtc",
            "rejectionReason",
            "rejectionDecisionId",
            "previousValues",
            "correctedValues",
            "correctionState"
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

    [Fact]
    public void Magic_link_state_loader_and_token_hash_index_persist_no_raw_token_or_decoded_material()
    {
        string indexReadModel = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Server", "MagicLinks", "MagicLinkTokenHashCapabilityIndexReadModel.cs"));
        string indexProjection = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Server", "MagicLinks", "MagicLinkTokenHashCapabilityIndexProjection.cs"));
        string loader = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Server", "MagicLinks", "EventStoreMagicLinkConfirmationCapabilityStateLoader.cs"));

        // The rebuildable token-hash index is keyed on the persisted token hash only. It is built from
        // Issued events and must never reference, derive from, store, or decode the raw one-time token.
        foreach (string indexSource in new[] { indexReadModel, indexProjection })
        {
            indexSource.ShouldNotContain("oneTimeToken", Case.Insensitive);
            indexSource.ShouldNotContain("rawToken", Case.Insensitive);
            indexSource.ShouldNotContain("DeriveHash");
            indexSource.ShouldNotContain("decoded", Case.Insensitive);
        }

        indexProjection.ShouldContain("TokenHash");

        // The loader is a read-only candidate resolver: it never writes to the read-model store (so it
        // cannot persist token material into the index) and never logs.
        loader.ShouldNotContain("SaveAsync");
        loader.ShouldNotContain("TrySaveAsync");
        loader.ShouldNotContain("ILogger");
        loader.ShouldNotContain("LogInformation");
        loader.ShouldNotContain("LogWarning");
        loader.ShouldNotContain("LogError");
        loader.ShouldNotContain("decoded", Case.Insensitive);
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
