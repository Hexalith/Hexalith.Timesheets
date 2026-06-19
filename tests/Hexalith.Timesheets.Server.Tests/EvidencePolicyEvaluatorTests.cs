using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class EvidencePolicyEvaluatorTests
{
    [Theory]
    [InlineData(TimesheetsOperation.Command)]
    [InlineData(TimesheetsOperation.Export)]
    [InlineData(TimesheetsOperation.Confirmation)]
    [InlineData(TimesheetsOperation.UiActionVisibility)]
    public async Task Missing_retention_policy_blocks_trust_bearing_operations(TimesheetsOperation operation)
    {
        TimesheetsEvidencePolicyEvaluator evaluator = new(TimesheetsEvidencePolicyOptions.FailClosedDefault);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(operation),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.RetentionPolicyMissing);
        result.Reason.ShouldBe("Retention policy is unresolved for this action.");
        result.Reason.ShouldNotContain("tenant", Case.Insensitive);
        result.Reason.ShouldNotContain("project", Case.Insensitive);
        result.Reason.ShouldNotContain("party", Case.Insensitive);
        result.Reason.ShouldNotContain("token", Case.Insensitive);
    }

    [Fact]
    public async Task Missing_comment_policy_blocks_export_after_retention_is_configured()
    {
        TimesheetsEvidencePolicyOptions options = TimesheetsEvidencePolicyOptions.FailClosedDefault with
        {
            LegalHoldPolicyConfigured = true,
            TenantRetentionOverridePolicyConfigured = true
        };
        TimesheetsEvidencePolicyEvaluator evaluator = new(options);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.Export),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.CommentPolicyMissing);
        result.Reason.ShouldBe("Comment policy is unresolved for this action.");
    }

    [Theory]
    [InlineData(TimesheetsUiAction.Approval)]
    [InlineData(TimesheetsUiAction.Correction)]
    [InlineData(TimesheetsUiAction.Confirmation)]
    [InlineData(TimesheetsUiAction.Export)]
    public async Task Missing_retention_policy_blocks_trust_bearing_ui_actions(TimesheetsUiAction uiAction)
    {
        TimesheetsEvidencePolicyEvaluator evaluator = new(TimesheetsEvidencePolicyOptions.FailClosedDefault);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.UiActionVisibility, uiAction),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.RetentionPolicyMissing);
        AssertSafePolicyReason(result.Reason);
    }

    [Fact]
    public async Task Configured_policy_allows_projection_reads_and_launch_ready_trust_operations()
    {
        TimesheetsEvidencePolicyOptions options = new()
        {
            LegalHoldPolicyConfigured = true,
            TenantRetentionOverridePolicyConfigured = true,
            CommentSensitivityPolicyConfigured = true,
            ExportCommentsAllowed = false
        };
        TimesheetsEvidencePolicyEvaluator evaluator = new(options);

        TimesheetsPolicyEvaluationResult command = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.Command),
            TestContext.Current.CancellationToken);
        TimesheetsPolicyEvaluationResult projection = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.ProjectionRead),
            TestContext.Current.CancellationToken);

        command.IsAllowed.ShouldBeTrue();
        projection.IsAllowed.ShouldBeTrue();
        options.LaunchReadinessGaps.ShouldBeEmpty();
        options.EffectivePolicy.CommentRules
            .Single(static rule => rule.Scope == TimesheetsCommentVisibilityScope.ExportOutput)
            .Decision.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    [Fact]
    public async Task Explicit_export_comment_policy_marks_export_comments_allowed_without_launch_readiness_gaps()
    {
        TimesheetsEvidencePolicyOptions options = new()
        {
            LegalHoldPolicyConfigured = true,
            TenantRetentionOverridePolicyConfigured = true,
            CommentSensitivityPolicyConfigured = true,
            ExportCommentsAllowed = true
        };
        TimesheetsEvidencePolicyEvaluator evaluator = new(options);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.Export),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        options.LaunchReadinessGaps.ShouldBeEmpty();
        CommentSensitivityRule exportRule = options.EffectivePolicy.CommentRules
            .Single(static rule => rule.Scope == TimesheetsCommentVisibilityScope.ExportOutput);
        exportRule.Decision.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        exportRule.RedactionRequirement.ShouldBe(TimesheetsCommentRedactionRequirement.RequiredBeforeExport);
    }

    [Theory]
    [InlineData(TimesheetsOperation.Query)]
    [InlineData(TimesheetsOperation.ProjectionRead)]
    public async Task Missing_policy_allows_non_trust_bearing_read_operations(TimesheetsOperation operation)
    {
        TimesheetsEvidencePolicyEvaluator evaluator = new(TimesheetsEvidencePolicyOptions.FailClosedDefault);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(operation),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.None);
    }

    [Fact]
    public async Task Unknown_operation_fails_closed_with_opaque_copy()
    {
        TimesheetsEvidencePolicyEvaluator evaluator = new(TimesheetsEvidencePolicyOptions.FailClosedDefault);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            Request(TimesheetsOperation.Unknown),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        result.Reason.ShouldBe("Authority cannot be resolved.");
        AssertSafePolicyReason(result.Reason);
    }

    [Fact]
    public void Policy_options_report_unresolved_legal_hold_and_tenant_overrides_as_launch_readiness_gaps()
    {
        TimesheetsEvidencePolicyOptions options = TimesheetsEvidencePolicyOptions.FailClosedDefault;

        options.LaunchReadinessGaps.ShouldBe([
            "Legal-hold policy is unresolved.",
            "Tenant-specific retention overrides are unresolved.",
            "Comment sensitivity policy is unresolved."
        ]);
        options.EffectivePolicy.RetentionRules
            .Select(static rule => rule.Category)
            .ShouldContain(TimesheetsEvidenceRetentionCategory.ExportRecord);
    }

    private static TimesheetsAuthorizationRequest Request(
        TimesheetsOperation operation,
        TimesheetsUiAction? uiAction = null)
    {
        return new(
            new TimesheetsRequestContext(
                new TenantReference("tenant_01"),
                new PartyReference("party_01"),
                "correlation_01"),
            operation)
        {
            UiAction = operation == TimesheetsOperation.UiActionVisibility
                ? uiAction ?? TimesheetsUiAction.Export
                : null
        };
    }

    private static void AssertSafePolicyReason(string reason)
    {
        foreach (string protectedTerm in new[] { "tenant", "user", "party", "project", "work", "entry", "token", "correlation" })
        {
            reason.ShouldNotContain(protectedTerm, Case.Insensitive);
        }
    }
}
