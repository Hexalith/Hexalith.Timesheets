using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class EvidencePolicyContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Evidence_policy_enums_expose_unknown_zero_sentinels_and_string_json()
    {
        Enum.GetName((TimesheetsEvidenceRetentionCategory)0).ShouldBe("Unknown");
        Enum.GetName((TimesheetsRetentionPosture)0).ShouldBe("Unknown");
        Enum.GetName((TimesheetsCommentVisibilityScope)0).ShouldBe("Unknown");
        Enum.GetName((TimesheetsCommentPolicyDecision)0).ShouldBe("Unknown");
        Enum.GetName((TimesheetsCommentRedactionRequirement)0).ShouldBe("Unknown");

        string json = JsonSerializer.Serialize(
            TimesheetsEvidenceRetentionCategory.MagicLinkConfirmationAuditMetadata,
            JsonOptions);

        json.ShouldBe("\"MagicLinkConfirmationAuditMetadata\"");
    }

    [Fact]
    public void Default_policy_descriptor_declares_retention_categories_and_launch_readiness_gaps()
    {
        TimesheetsEvidencePolicyDescriptor descriptor = TimesheetsEvidencePolicyDescriptor.FailClosedDefault;

        descriptor.RetentionRules.Select(static rule => rule.Category).ShouldBe([
            TimesheetsEvidenceRetentionCategory.TimeEntryEvidence,
            TimesheetsEvidenceRetentionCategory.CommentText,
            TimesheetsEvidenceRetentionCategory.ExportRecord,
            TimesheetsEvidenceRetentionCategory.MagicLinkConfirmationAuditMetadata
        ]);
        descriptor.RetentionRules.All(static rule => rule.Posture != TimesheetsRetentionPosture.Unknown)
            .ShouldBeTrue();
        descriptor.LaunchReadinessGaps.ShouldContain("Legal-hold policy is unresolved.");
        descriptor.LaunchReadinessGaps.ShouldContain("Tenant-specific retention overrides are unresolved.");
    }

    [Fact]
    public void Comment_sensitivity_policy_covers_visibility_export_diagnostics_redaction_and_retention()
    {
        TimeEntryCommentPolicy policy = TimeEntryCommentPolicy.SensitiveDefault;

        policy.InternalDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        policy.ProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        policy.ExportInclusion.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        policy.SupportDiagnostics.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        policy.RedactionRequirement.ShouldBe(TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure);
        policy.RetentionCategory.ShouldBe(TimesheetsEvidenceRetentionCategory.CommentText);
    }

    [Fact]
    public void Default_policy_descriptor_declares_comment_scope_rules_and_export_redaction()
    {
        TimesheetsEvidencePolicyDescriptor descriptor = TimesheetsEvidencePolicyDescriptor.FailClosedDefault;

        descriptor.CommentRules.Select(static rule => rule.Scope).ShouldBe([
            TimesheetsCommentVisibilityScope.InternalDisplay,
            TimesheetsCommentVisibilityScope.ExternalConfirmationDisplay,
            TimesheetsCommentVisibilityScope.ProjectionReadModel,
            TimesheetsCommentVisibilityScope.ExportOutput,
            TimesheetsCommentVisibilityScope.SupportDiagnostics
        ]);

        CommentSensitivityRule exportRule = descriptor.CommentRules
            .Single(static rule => rule.Scope == TimesheetsCommentVisibilityScope.ExportOutput);
        exportRule.Decision.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        exportRule.RedactionRequirement.ShouldBe(TimesheetsCommentRedactionRequirement.RequiredBeforeExport);
        exportRule.RetentionCategory.ShouldBe(TimesheetsEvidenceRetentionCategory.CommentText);
        exportRule.SafeGuidance.ShouldBe("Export comments only when policy allows it.");

        descriptor.CommentRules
            .Single(static rule => rule.Scope == TimesheetsCommentVisibilityScope.SupportDiagnostics)
            .Decision.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    [Fact]
    public void Time_entry_comment_requires_text_and_keeps_policy_metadata_safe()
    {
        Should.Throw<ArgumentException>(static () => new TimeEntryComment(" ", TimeEntryCommentPolicy.SensitiveDefault));

        TimeEntryComment comment = new("Customer asked for evidence review.", TimeEntryCommentPolicy.SensitiveDefault);

        string json = JsonSerializer.Serialize(comment, JsonOptions);

        json.ShouldContain("\"text\":\"Customer asked for evidence review.\"");
        json.ShouldContain("\"supportDiagnostics\":\"Excluded\"");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("PartyDisplayName");
        json.ShouldNotContain("ProjectDisplayName");
        json.ShouldNotContain("WorkDisplayName");
    }

    [Fact]
    public void Time_entry_comment_enforces_upper_length_boundary()
    {
        string atLimit = new('x', TimeEntryComment.MaxLength);
        TimeEntryComment comment = new(atLimit, TimeEntryCommentPolicy.SensitiveDefault);
        comment.Text.Length.ShouldBe(TimeEntryComment.MaxLength);

        string tooLong = new('x', TimeEntryComment.MaxLength + 1);
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TimeEntryComment(tooLong, TimeEntryCommentPolicy.SensitiveDefault));
    }

    [Fact]
    public void Record_time_entry_comment_is_additive_and_round_trips_without_constructor_breaking()
    {
        RecordTimeEntry command = CreateRecordTimeEntry() with
        {
            Comment = new("Internal context only.", TimeEntryCommentPolicy.SensitiveDefault)
        };

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"comment\"");
        json.ShouldContain("\"exportInclusion\":\"Excluded\"");
        AssertJsonOmitsCallerAuthority(json);

        RecordTimeEntry? roundTripped = JsonSerializer.Deserialize<RecordTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Comment.ShouldNotBeNull();
        roundTripped.Comment.Text.ShouldBe("Internal context only.");
        roundTripped.Comment.Policy.SupportDiagnostics.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    [Fact]
    public void Time_entry_recorded_comment_is_additive_and_round_trips_without_constructor_breaking()
    {
        TimeEntryRecorded recorded = new(
            new TimeEntryId("time-entry-234"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-234")),
            new PartyReference("party-234"),
            new ActivityTypeId("activity-type-234"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 17),
            90,
            BillableState.NonBillable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Policy-aware event comment.", TimeEntryCommentPolicy.SensitiveDefault)
        };

        string json = JsonSerializer.Serialize(recorded, JsonOptions);

        json.ShouldContain("\"comment\"");
        json.ShouldContain("\"externalConfirmationDisplay\":\"Excluded\"");
        AssertJsonOmitsCallerAuthority(json);

        TimeEntryRecorded? roundTripped = JsonSerializer.Deserialize<TimeEntryRecorded>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Comment.ShouldNotBeNull();
        roundTripped.Comment.Text.ShouldBe("Policy-aware event comment.");
        roundTripped.Comment.Policy.ExportInclusion.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    [Fact]
    public void Evidence_read_model_comment_policy_is_additive_and_excludes_diagnostics_by_default()
    {
        TimeEntryEvidenceReadModel model = CreateEvidenceReadModel() with
        {
            Comment = new("Visible only where policy allows.", TimeEntryCommentPolicy.SensitiveDefault)
        };

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"comment\"");
        json.ShouldContain("\"supportDiagnostics\":\"Excluded\"");
        AssertJsonOmitsCallerAuthority(json);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Comment.ShouldNotBeNull();
        roundTripped.Comment.Policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    [Fact]
    public void Metadata_catalog_exposes_policy_help_without_runtime_or_finance_ownership_language()
    {
        string metadata = JsonSerializer.Serialize(TimesheetsMetadataCatalog.Descriptors, JsonOptions);

        metadata.ShouldContain("Comments may be excluded by policy.");
        metadata.ShouldContain("Export comments only when policy allows it.");
        metadata.ShouldContain(nameof(TimesheetsEvidenceRetentionCategory));
        metadata.ShouldNotContain("Microsoft.FluentUI");
        metadata.ShouldNotContain("Microsoft.AspNetCore");
        metadata.ShouldNotContain("Dapr");
        metadata.ShouldNotContain("EventStore");
        AssertNoFinanceOwnershipLanguage(metadata);
    }

    [Fact]
    public void Openapi_policy_guidance_documents_comment_and_retention_defaults_safely()
    {
        string artifactPath = RepositoryPath(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json");
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(artifactPath))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        JsonObject policy = artifact["x-hexalith-evidence-policy"]?.AsObject()
            ?? throw new InvalidOperationException("Policy guidance metadata is missing.");

        string policyJson = policy.ToJsonString();
        policyJson.ShouldContain("TimeEntryEvidence");
        policyJson.ShouldContain("CommentText");
        policyJson.ShouldContain("ExportRecord");
        policyJson.ShouldContain("MagicLinkConfirmationAuditMetadata");
        policyJson.ShouldContain("Comment export is disabled until policy allows it.");
        policyJson.ShouldContain("Legal-hold policy is unresolved.");
        policyJson.ShouldContain("Tenant-specific retention overrides are unresolved.");
        policyJson.ShouldNotContain("EventStore");
        policyJson.ShouldNotContain("Party display", Case.Insensitive);
        policyJson.ShouldNotContain("Project", Case.Insensitive);
        policyJson.ShouldNotContain("Work", Case.Insensitive);
        AssertNoFinanceOwnershipLanguage(policyJson);
    }

    private static RecordTimeEntry CreateRecordTimeEntry()
    {
        return new(
            new TimeEntryId("time-entry-123"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-123")),
            new PartyReference("party-123"),
            new ActivityTypeId("activity-type-123"),
            new DateOnly(2026, 6, 19),
            45,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);
    }

    private static TimeEntryEvidenceReadModel CreateEvidenceReadModel()
    {
        return new(
            new TimeEntryId("time-entry-456"),
            TimeEntryTargetReference.ForWork(new WorkReference("work-456")),
            new PartyReference("party-456"),
            new ActivityTypeId("activity-type-456"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 18),
            30,
            BillableState.NonBillable,
            TimeEntryApprovalState.Submitted,
            ContributorCategory.ExternalContributor,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh);
    }

    private static void AssertJsonOmitsCallerAuthority(string json)
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

    private static void AssertNoFinanceOwnershipLanguage(string content)
    {
        foreach (string forbiddenWord in new[] { "invoice", "payroll", "rate", "tax", "revenue" })
        {
            Regex.IsMatch(content, $@"\b{forbiddenWord}\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
                .ShouldBeFalse(forbiddenWord);
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
