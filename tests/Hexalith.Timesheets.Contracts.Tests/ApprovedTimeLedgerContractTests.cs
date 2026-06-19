using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ApprovedTimeLedgerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Ledger_query_contract_round_trips_without_server_controlled_authority_fields()
    {
        QueryApprovedTimeLedger query = new()
        {
            Project = new ProjectReference("project-1"),
            Contributor = new PartyReference("party-1"),
            ActivityTypeId = new ActivityTypeId("activity-type-1"),
            TenantLocalPeriodKey = "2026-06",
            BillableState = BillableState.Billable,
            IncludeSupersededRows = true,
            SortBy = TimeEntryQuerySortBy.TimeEntryId,
            PageSize = 25,
            Cursor = "opaque"
        };

        string json = JsonSerializer.Serialize(query, JsonOptions);

        AssertJsonOmitsCallerAuthority(json);
        json.ShouldContain("\"includeSupersededRows\":true");

        QueryApprovedTimeLedger? roundTripped = JsonSerializer.Deserialize<QueryApprovedTimeLedger>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Project.ShouldBe(new ProjectReference("project-1"));
        roundTripped.Contributor.ShouldBe(new PartyReference("party-1"));
        roundTripped.IncludeSupersededRows.ShouldBeTrue();
    }

    [Fact]
    public void Ledger_read_model_round_trips_approval_lineage_comment_policy_cursor_and_degraded_freshness()
    {
        ApprovedTimeLedgerReadModel model = new(
            [
                ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(EvidenceWithApprovedCorrection()),
                ApprovedTimeLedgerRowReadModel.SupersededFromApprovedCorrection(EvidenceWithApprovedCorrection())
                    ?? throw new InvalidOperationException("Superseded ledger row was not created.")
            ],
            "cursor-2",
            ProjectionFreshnessMetadata.Degraded(),
            false,
            "Projection freshness does not allow export preview.");

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"rowState\":\"Superseded\"");
        json.ShouldContain("\"state\":\"Degraded\"");
        json.ShouldContain("\"commentProjectionState\":\"PolicyRequired\"");
        json.ShouldContain("\"nextCursor\":\"cursor-2\"");
        json.ShouldNotContain("Comment that must be omitted");
        ApprovedTimeLedgerReadModel? roundTripped = JsonSerializer.Deserialize<ApprovedTimeLedgerReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Items.Count.ShouldBe(2);
        roundTripped.Items.Select(static row => row.RowState)
            .ShouldBe([ApprovedTimeLedgerRowState.Current, ApprovedTimeLedgerRowState.Superseded]);
        foreach (ApprovedTimeLedgerRowReadModel row in roundTripped.Items)
        {
            row.ApprovedCorrection.ShouldNotBeNull();
            row.CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
            row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Degraded);
        }
    }

    [Fact]
    public void Approved_time_ledger_metadata_declares_filters_fields_actions_and_status_vocabularies()
    {
        TimesheetsMetadataDescriptor descriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.projection.approved-time-ledger");

        descriptor.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);
        descriptor.Fields.Select(static field => field.Name).ShouldContain("projectFilter");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("workFilter");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("includeSupersededRows");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("approvalDecision");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("approvedCorrection");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("commentPolicy");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("exportReadiness");
        descriptor.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.ReviewExportReadiness");
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(BillableState));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ApprovedTimeLedgerRowState));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryLockState));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(DisplayHydrationState));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ApprovalAuthoritySource));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimesheetsCommentPolicyDecision));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));

        string metadata = JsonSerializer.Serialize(descriptor, JsonOptions);
        metadata.ShouldNotContain("EventStore");
        AssertNoFinanceOwnershipLanguage(metadata);
    }

    private static TimeEntryApprovalDecisionEvidence Approval()
        => new(
            new TimeEntryId("time-entry-1"),
            new TimeEntryApprovalDecisionId("decision-1"),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            TimeEntryApprovalState.Approved,
            TimeEntryApprovalScope.IndividualEntry,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            null);

    private static TimeEntryEvidenceReadModel EvidenceWithApprovedCorrection()
        => new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            75,
            BillableState.Billable,
            TimeEntryApprovalState.Approved,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.Corrected,
            ProjectionFreshnessMetadata.Degraded())
        {
            ApprovalDecision = Approval(),
            ApprovedCorrection = ApprovedCorrection(),
            LockEvidence = TimeEntryLockEvidence.Approved(
                new TimeEntryApprovalDecisionId("decision-1"),
                TimeEntryApprovalScope.IndividualEntry,
                new PartyReference("approver-1"),
                new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero)),
            Comment = new("Comment that must be omitted", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static TimeEntryApprovedCorrectionEvidence ApprovedCorrection()
        => new(
            new TimeEntryId("time-entry-1"),
            new TimeEntryCorrectionId("correction-1"),
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            new TimeEntryCorrectionReason("Correct approved duration."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalScope.IndividualEntry,
            Values(60, "Comment that must be omitted"),
            Values(75, "Corrected comment that must be omitted"),
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(int durationMinutes, string comment)
        => new(
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new(comment, TimeEntryCommentPolicy.SensitiveDefault)
        };

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
            Regex.IsMatch(
                content,
                $@"\b{forbiddenWord}\b",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)).ShouldBeFalse(forbiddenWord);
        }
    }
}
