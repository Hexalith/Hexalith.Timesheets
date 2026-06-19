using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class TimeCaptureContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Record_time_entry_models_exactly_one_target_reference()
    {
        TimeEntryTargetReference projectTarget = TimeEntryTargetReference.ForProject(new ProjectReference("project-42"));
        TimeEntryTargetReference workTarget = TimeEntryTargetReference.ForWork(new WorkReference("work-42"));

        projectTarget.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        projectTarget.TargetId.ShouldBe("project-42");
        workTarget.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        workTarget.TargetId.ShouldBe("work-42");

        Type commandType = typeof(RecordTimeEntry);
        commandType.GetProperty("Target").ShouldNotBeNull();
        commandType.GetProperty("TargetKind").ShouldBeNull();
        commandType.GetProperty("TargetId").ShouldBeNull();
        commandType.GetProperty("ProjectReference").ShouldBeNull();
        commandType.GetProperty("WorkReference").ShouldBeNull();
    }

    [Fact]
    public void Record_time_entry_contract_round_trips_through_web_json_without_authority_fields()
    {
        RecordTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-123")),
            new PartyReference("party-123"),
            new ActivityTypeId("activity-type-123"),
            new DateOnly(2026, 6, 19),
            45,
            BillableState.Billable,
            ContributorCategory.AutomatedAgent,
            new(
                AiMetricAvailability.ProviderReported,
                90000,
                75000,
                2,
                1000,
                250,
                1250,
                AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-123"),
                AiTokenMetricAvailability.ProviderReported));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryId\"");
        json.ShouldContain("\"targetKind\":\"Project\"");
        json.ShouldContain("\"durationMinutes\":45");
        json.ShouldContain("\"providerTotalTokenCount\":1250");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        RecordTimeEntry? roundTripped = JsonSerializer.Deserialize<RecordTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        roundTripped.Target.TargetId.ShouldBe("project-123");
        roundTripped.Contributor.PartyId.ShouldBe("party-123");
        roundTripped.AiMetrics.ShouldNotBeNull();
        roundTripped.AiMetrics.ProviderTotalTokenCount.ShouldBe(1250);
        roundTripped.AiMetrics.Source.ShouldNotBeNull();
        roundTripped.AiMetrics.Source.SourceCategory.ShouldBe(AiEffortMetricSourceCategory.Provider);
        roundTripped.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
    }

    [Fact]
    public void Time_entry_evidence_read_model_round_trips_projection_and_state_metadata()
    {
        TimeEntryEvidenceReadModel readModel = new(
            new TimeEntryId("time-entry-456"),
            TimeEntryTargetReference.ForWork(new WorkReference("work-456")),
            new PartyReference("party-456"),
            new ActivityTypeId("activity-type-456"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 18),
            30,
            BillableState.NonBillable,
            TimeEntryApprovalState.Submitted,
            ContributorCategory.AutomatedAgent,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Rebuilding());

        string json = JsonSerializer.Serialize(readModel, JsonOptions);

        json.ShouldContain("\"targetKind\":\"Work\"");
        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"projectionFreshness\"");
        json.ShouldContain("Projection is rebuilding.");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        roundTripped.Target.TargetId.ShouldBe("work-456");
        roundTripped.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Rebuilding);
        roundTripped.AiMetrics.ShouldNotBeNull();
        roundTripped.AiMetrics.ProviderInputTokenCount.ShouldBeNull();
    }

    [Fact]
    public void Query_time_entries_contract_round_trips_without_authority_fields()
    {
        QueryTimeEntries query = new()
        {
            Contributor = new PartyReference("party-123"),
            Project = new ProjectReference("project-123"),
            ServiceDateFrom = new DateOnly(2026, 6, 1),
            ServiceDateTo = new DateOnly(2026, 6, 30),
            ActivityTypeId = new ActivityTypeId("activity-type-123"),
            BillableState = BillableState.Billable,
            ApprovalStates = [TimeEntryApprovalState.Submitted, TimeEntryApprovalState.Approved],
            CorrectionStates = [TimeEntryCorrectionState.None, TimeEntryCorrectionState.Corrected],
            ContributorCategories = [ContributorCategory.Employee],
            SourceTypes = [TimeEntrySourceType.Employee],
            CurrentEntriesOnly = true,
            SortBy = TimeEntryQuerySortBy.ServiceDate,
            SortDirection = TimeEntryQuerySortDirection.Descending,
            PageSize = 25,
            Cursor = "opaque-cursor"
        };

        string json = JsonSerializer.Serialize(query, JsonOptions);

        json.ShouldContain("\"approvalStates\":[\"Submitted\",\"Approved\"]");
        json.ShouldContain("\"sourceTypes\":[\"Employee\"]");
        json.ShouldContain("\"pageSize\":25");
        AssertJsonOmitsCallerAuthority(json);

        QueryTimeEntries? roundTripped = JsonSerializer.Deserialize<QueryTimeEntries>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Contributor.ShouldBe(new PartyReference("party-123"));
        roundTripped.Project.ShouldBe(new ProjectReference("project-123"));
        roundTripped.ApprovalStates.ShouldBe([TimeEntryApprovalState.Submitted, TimeEntryApprovalState.Approved]);
        roundTripped.SourceTypes.ShouldBe([TimeEntrySourceType.Employee]);
        roundTripped.CurrentEntriesOnly.ShouldBeTrue();
    }

    [Fact]
    public void Time_entry_query_read_model_round_trips_state_names_source_type_and_degraded_freshness()
    {
        TimeEntryQueryReadModel page = new(
            [
                new(
                    new TimeEntryId("time-entry-789"),
                    TimeEntryTargetReference.ForProject(new ProjectReference("project-789")),
                    new PartyReference("party-789"),
                    new ActivityTypeId("activity-type-789"),
                    ActivityTypeScope.Tenant,
                    new DateOnly(2026, 6, 20),
                    60,
                    BillableState.Billable,
                    TimeEntryApprovalState.Approved,
                    TimeEntryCorrectionState.Corrected,
                    ContributorCategory.AutomatedAgent,
                    TimeEntrySourceType.AutomatedAgent,
                    ProjectionFreshnessMetadata.Degraded("Projection shard is delayed."))
            ],
            "next-cursor",
            ProjectionFreshnessMetadata.Degraded("Projection shard is delayed."));

        string json = JsonSerializer.Serialize(page, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Approved\"");
        json.ShouldContain("\"correctionState\":\"Corrected\"");
        json.ShouldContain("\"sourceType\":\"AutomatedAgent\"");
        json.ShouldContain("\"state\":\"Degraded\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryQueryReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryQueryReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        TimeEntryQueryRowReadModel row = roundTripped.Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("time-entry-789"));
        row.SourceType.ShouldBe(TimeEntrySourceType.AutomatedAgent);
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Degraded);
    }

    [Fact]
    public void Submit_time_entries_contract_round_trips_without_authority_fields()
    {
        SubmitTimeEntriesForApproval command = new(
            new TimeEntrySubmissionId("submission-123"),
            [
                new("time-entry-123"),
                new("time-entry-456")
            ],
            TimeEntrySubmissionScope.SelectedEntries);

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntrySubmissionId\"");
        json.ShouldContain("\"timeEntryIds\"");
        json.ShouldContain("\"submissionScope\":\"SelectedEntries\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        SubmitTimeEntriesForApproval? roundTripped = JsonSerializer.Deserialize<SubmitTimeEntriesForApproval>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntrySubmissionId.Value.ShouldBe("submission-123");
        roundTripped.TimeEntryIds.Select(static id => id.Value).ShouldBe(["time-entry-123", "time-entry-456"]);
        roundTripped.SubmissionScope.ShouldBe(TimeEntrySubmissionScope.SelectedEntries);
    }

    [Fact]
    public void Submit_timesheet_period_contract_round_trips_without_authority_or_policy_fields()
    {
        SubmitTimesheetPeriod command = new(
            new TimesheetPeriodId("period-2026-w25"),
            new PartyReference("party-contributor"),
            new TimesheetPeriodRequest(TimesheetPeriodKind.Weekly, new DateOnly(2026, 6, 19)),
            [
                new("time-entry-123"),
                new("time-entry-456")
            ]);

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timesheetPeriodId\"");
        json.ShouldContain("\"contributor\"");
        json.ShouldContain("\"periodKind\":\"Weekly\"");
        json.ShouldContain("\"timeEntryIds\"");
        json.ShouldNotContain("tenantTimeZoneId");
        json.ShouldNotContain("submittedAtUtc");
        AssertJsonOmitsCallerAuthority(json);

        SubmitTimesheetPeriod? roundTripped = JsonSerializer.Deserialize<SubmitTimesheetPeriod>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimesheetPeriodId.Value.ShouldBe("period-2026-w25");
        roundTripped.Contributor.PartyId.ShouldBe("party-contributor");
        roundTripped.Period.PeriodKind.ShouldBe(TimesheetPeriodKind.Weekly);
        roundTripped.TimeEntryIds.Select(static id => id.Value).ShouldBe(["time-entry-123", "time-entry-456"]);
    }

    [Fact]
    public void Approve_timesheet_period_contract_round_trips_without_authority_fields()
    {
        ApproveTimesheetPeriod command = new(
            new TimesheetPeriodId("period-2026-w25"),
            new TimesheetPeriodApprovalDecisionId("period-decision-123"));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timesheetPeriodId\"");
        json.ShouldContain("\"timesheetPeriodApprovalDecisionId\"");
        AssertJsonOmitsCallerAuthority(json);

        ApproveTimesheetPeriod? roundTripped = JsonSerializer.Deserialize<ApproveTimesheetPeriod>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimesheetPeriodId.Value.ShouldBe("period-2026-w25");
        roundTripped.TimesheetPeriodApprovalDecisionId.Value.ShouldBe("period-decision-123");
    }

    [Fact]
    public void Reject_timesheet_period_contract_round_trips_selected_entry_reasons_without_authority_fields()
    {
        RejectTimesheetPeriod command = new(
            new TimesheetPeriodId("period-2026-w25"),
            new TimesheetPeriodApprovalDecisionId("period-decision-456"),
            [
                new(new TimeEntryId("time-entry-123"), new TimeEntryRejectionReason("Missing customer evidence."))
            ],
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timesheetPeriodId\"");
        json.ShouldContain("\"timesheetPeriodApprovalDecisionId\"");
        json.ShouldContain("\"rejectedEntries\"");
        json.ShouldContain("Missing customer evidence.");
        json.ShouldContain("Period contains entries needing correction.");
        AssertJsonOmitsCallerAuthority(json);

        RejectTimesheetPeriod? roundTripped = JsonSerializer.Deserialize<RejectTimesheetPeriod>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimesheetPeriodId.Value.ShouldBe("period-2026-w25");
        roundTripped.TimesheetPeriodApprovalDecisionId.Value.ShouldBe("period-decision-456");
        roundTripped.RejectedEntries.Single().TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.RejectedEntries.Single().Reason.Value.ShouldBe("Missing customer evidence.");
        roundTripped.Reason.Value.ShouldBe("Period contains entries needing correction.");
    }

    [Fact]
    public void Timesheet_period_submitted_event_records_boundary_and_keeps_utc_audit_instant_separate()
    {
        TimesheetPeriodSubmitted submitted = new(
            new TimesheetPeriodId("period-2026-06"),
            new TenantReference("tenant-123"),
            new PartyReference("party-contributor"),
            new PartyReference("party-submitter"),
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "Europe/Paris",
            [new("time-entry-123")],
            TimesheetPeriodApprovalState.Submitted);

        string json = JsonSerializer.Serialize(submitted, JsonOptions);

        json.ShouldContain("\"periodState\":\"Submitted\"");
        json.ShouldContain("\"periodKind\":\"Monthly\"");
        json.ShouldContain("\"tenantTimeZoneId\":\"Europe/Paris\"");
        json.ShouldContain("\"submittedAtUtc\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimesheetPeriodSubmitted? roundTripped = JsonSerializer.Deserialize<TimesheetPeriodSubmitted>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.PeriodKey.ShouldBe("2026-06");
        roundTripped.LocalStartDate.ShouldBe(new DateOnly(2026, 6, 1));
        roundTripped.SubmittedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Timesheet_period_approved_event_records_grouped_decision_evidence()
    {
        TimesheetPeriodApproved approved = new(
            new TimesheetPeriodId("period-2026-w25"),
            new TenantReference("tenant-123"),
            new PartyReference("party-contributor"),
            new PartyReference("party-approver"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimesheetPeriodApprovalDecisionId("period-decision-123"),
            TimesheetPeriodApprovalState.Approved,
            AuthoritySource(ApprovalAuthorityAction.PeriodApproval),
            [new("time-entry-123"), new("time-entry-456")]);

        string json = JsonSerializer.Serialize(approved, JsonOptions);

        json.ShouldContain("\"periodState\":\"Approved\"");
        json.ShouldContain("\"includedTimeEntryIds\"");
        json.ShouldContain("\"authoritySource\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimesheetPeriodApproved? roundTripped = JsonSerializer.Deserialize<TimesheetPeriodApproved>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimesheetPeriodApprovalDecisionId.Value.ShouldBe("period-decision-123");
        roundTripped.PeriodState.ShouldBe(TimesheetPeriodApprovalState.Approved);
        roundTripped.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.PeriodApproval);
        roundTripped.IncludedTimeEntryIds.Select(static id => id.Value).ShouldBe(["time-entry-123", "time-entry-456"]);
    }

    [Fact]
    public void Timesheet_period_rejected_event_records_grouped_and_selected_entry_reasons()
    {
        TimesheetPeriodRejected rejected = new(
            new TimesheetPeriodId("period-2026-w25"),
            new TenantReference("tenant-123"),
            new PartyReference("party-contributor"),
            new PartyReference("party-approver"),
            new DateTimeOffset(2026, 6, 19, 13, 15, 0, TimeSpan.Zero),
            new TimesheetPeriodApprovalDecisionId("period-decision-456"),
            TimesheetPeriodApprovalState.Rejected,
            AuthoritySource(ApprovalAuthorityAction.PeriodRejection),
            [new("time-entry-123")],
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."),
            [
                new(new TimeEntryId("time-entry-123"), new TimeEntryRejectionReason("Missing customer evidence."))
            ]);

        string json = JsonSerializer.Serialize(rejected, JsonOptions);

        json.ShouldContain("\"periodState\":\"Rejected\"");
        json.ShouldContain("\"affectedTimeEntryIds\"");
        json.ShouldContain("\"rejectedEntries\"");
        json.ShouldContain("Missing customer evidence.");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimesheetPeriodRejected? roundTripped = JsonSerializer.Deserialize<TimesheetPeriodRejected>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimesheetPeriodApprovalDecisionId.Value.ShouldBe("period-decision-456");
        roundTripped.AffectedTimeEntryIds.Single().Value.ShouldBe("time-entry-123");
        roundTripped.RejectedEntries.Single().Reason.Value.ShouldBe("Missing customer evidence.");
        roundTripped.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.PeriodRejection);
    }

    [Fact]
    public void Approve_time_entry_contract_round_trips_without_caller_authority_fields()
    {
        ApproveTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryApprovalDecisionId("approval-decision-123"));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryId\"");
        json.ShouldContain("\"timeEntryApprovalDecisionId\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        ApproveTimeEntry? roundTripped = JsonSerializer.Deserialize<ApproveTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.TimeEntryApprovalDecisionId.Value.ShouldBe("approval-decision-123");
    }

    [Fact]
    public void Reject_time_entry_contract_round_trips_reason_without_caller_authority_fields()
    {
        RejectTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryApprovalDecisionId("approval-decision-456"),
            new TimeEntryRejectionReason("Needs customer PO evidence."));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryId\"");
        json.ShouldContain("\"timeEntryApprovalDecisionId\"");
        json.ShouldContain("\"reason\"");
        json.ShouldContain("Needs customer PO evidence.");
        AssertJsonOmitsCallerAuthority(json);

        RejectTimeEntry? roundTripped = JsonSerializer.Deserialize<RejectTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.TimeEntryApprovalDecisionId.Value.ShouldBe("approval-decision-456");
        roundTripped.Reason.Value.ShouldBe("Needs customer PO evidence.");
    }

    [Fact]
    public void Time_entry_submitted_event_records_submission_evidence_and_resulting_state()
    {
        TimeEntrySubmitted submitted = new(
            new TimeEntryId("time-entry-123"),
            new PartyReference("party-submitter"),
            new TenantReference("tenant-123"),
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-123"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

        string json = JsonSerializer.Serialize(submitted, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"submissionScope\":\"SelectedEntries\"");
        json.ShouldContain("\"submittedAtUtc\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntrySubmitted? roundTripped = JsonSerializer.Deserialize<TimeEntrySubmitted>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Submitter.PartyId.ShouldBe("party-submitter");
        roundTripped.Tenant.TenantId.ShouldBe("tenant-123");
        roundTripped.TimeEntrySubmissionId.Value.ShouldBe("submission-123");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    [Fact]
    public void Time_entry_approved_event_records_decision_evidence_and_resulting_state()
    {
        TimeEntryApproved approved = new(
            new TimeEntryId("time-entry-123"),
            new PartyReference("party-approver"),
            new TenantReference("tenant-123"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("approval-decision-123"),
            TimeEntryApprovalState.Approved,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "project-approval",
                "2026-06",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry);

        string json = JsonSerializer.Serialize(approved, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Approved\"");
        json.ShouldContain("\"approvalScope\":\"IndividualEntry\"");
        json.ShouldContain("\"authoritySource\"");
        json.ShouldContain("\"decidedAtUtc\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryApproved? roundTripped = JsonSerializer.Deserialize<TimeEntryApproved>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Approver.PartyId.ShouldBe("party-approver");
        roundTripped.Tenant.TenantId.ShouldBe("tenant-123");
        roundTripped.TimeEntryApprovalDecisionId.Value.ShouldBe("approval-decision-123");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        roundTripped.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        roundTripped.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
    }

    [Fact]
    public void Time_entry_rejected_event_records_reason_and_decision_evidence()
    {
        TimeEntryRejected rejected = new(
            new TimeEntryId("time-entry-123"),
            new PartyReference("party-approver"),
            new TenantReference("tenant-123"),
            new DateTimeOffset(2026, 6, 19, 13, 15, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("approval-decision-456"),
            TimeEntryApprovalState.Rejected,
            new(
                ApprovalAuthorityAction.EntryRejection,
                ApprovalAuthoritySource.WorkOwner,
                ApprovalAuthorityDecisionState.Allowed,
                "work-approval",
                "2026-06",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence."));

        string json = JsonSerializer.Serialize(rejected, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Rejected\"");
        json.ShouldContain("\"approvalScope\":\"IndividualEntry\"");
        json.ShouldContain("\"reason\"");
        json.ShouldContain("Needs customer PO evidence.");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryRejected? roundTripped = JsonSerializer.Deserialize<TimeEntryRejected>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Approver.PartyId.ShouldBe("party-approver");
        roundTripped.Tenant.TenantId.ShouldBe("tenant-123");
        roundTripped.TimeEntryApprovalDecisionId.Value.ShouldBe("approval-decision-456");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        roundTripped.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.WorkOwner);
        roundTripped.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        roundTripped.Reason.Value.ShouldBe("Needs customer PO evidence.");
    }

    [Fact]
    public void Correct_rejected_time_entry_contract_round_trips_without_caller_authority_fields()
    {
        CorrectRejectedTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryCorrectionId("correction-123"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-456")),
            new PartyReference("party-456"),
            new ActivityTypeId("activity-type-456"),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.NonBillable,
            ContributorCategory.AutomatedAgent,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Corrected after rejection.", TimeEntryCommentPolicy.SensitiveDefault)
        };

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryCorrectionId\"");
        json.ShouldContain("\"targetKind\":\"Project\"");
        json.ShouldContain("\"durationMinutes\":75");
        json.ShouldContain("\"comment\"");
        AssertJsonOmitsCallerAuthority(json);

        CorrectRejectedTimeEntry? roundTripped = JsonSerializer.Deserialize<CorrectRejectedTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.TimeEntryCorrectionId.Value.ShouldBe("correction-123");
        roundTripped.Target.TargetId.ShouldBe("project-456");
        roundTripped.Contributor.PartyId.ShouldBe("party-456");
        roundTripped.DurationMinutes.ShouldBe(75);
        roundTripped.Comment.ShouldNotBeNull();
        roundTripped.Comment.Text.ShouldBe("Corrected after rejection.");
    }

    [Fact]
    public void Correct_approved_time_entry_contract_round_trips_reason_without_caller_authority_fields()
    {
        CorrectApprovedTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryCorrectionId("approved-correction-123"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-456")),
            new PartyReference("party-456"),
            new ActivityTypeId("activity-type-456"),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.NonBillable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            new TimeEntryCorrectionReason("Correct approved duration after audit review."))
        {
            Comment = new("Approved correction evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryCorrectionId\"");
        json.ShouldContain("\"reason\"");
        json.ShouldContain("Correct approved duration after audit review.");
        AssertJsonOmitsCallerAuthority(json);

        CorrectApprovedTimeEntry? roundTripped = JsonSerializer.Deserialize<CorrectApprovedTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.TimeEntryCorrectionId.Value.ShouldBe("approved-correction-123");
        roundTripped.Reason.Value.ShouldBe("Correct approved duration after audit review.");
        roundTripped.Comment.ShouldNotBeNull();
        roundTripped.Comment.Text.ShouldBe("Approved correction evidence.");
    }

    [Fact]
    public void Time_entry_corrected_event_links_previous_corrected_values_and_rejection_lineage()
    {
        TimeEntryCorrectionValues previous = CorrectionValues(60);
        TimeEntryCorrectionValues corrected = CorrectionValues(75) with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference("project-456"))
        };
        TimeEntryCorrected correctedEvent = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryCorrectionId("correction-123"),
            new TenantReference("tenant-123"),
            new PartyReference("party-operator"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            previous,
            corrected,
            new TimeEntryRejectionReason("Needs customer PO evidence."),
            new TimeEntryApprovalDecisionId("approval-decision-456"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Corrected);

        string json = JsonSerializer.Serialize(correctedEvent, JsonOptions);

        json.ShouldContain("\"timeEntryCorrectionId\"");
        json.ShouldContain("\"previousValues\"");
        json.ShouldContain("\"correctedValues\"");
        json.ShouldContain("\"rejectionReason\"");
        json.ShouldContain("\"rejectionDecisionId\"");
        json.ShouldContain("\"correctionState\":\"Corrected\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryCorrected? roundTripped = JsonSerializer.Deserialize<TimeEntryCorrected>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryCorrectionId.Value.ShouldBe("correction-123");
        roundTripped.PreviousValues.DurationMinutes.ShouldBe(60);
        roundTripped.CorrectedValues.DurationMinutes.ShouldBe(75);
        roundTripped.RejectionReason.Value.ShouldBe("Needs customer PO evidence.");
        roundTripped.RejectionDecisionId.Value.ShouldBe("approval-decision-456");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        roundTripped.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
    }

    [Fact]
    public void Time_entry_approved_corrected_event_links_previous_corrected_values_reason_and_approval_lineage()
    {
        TimeEntryCorrectionValues previous = CorrectionValues(60);
        TimeEntryCorrectionValues corrected = CorrectionValues(75);
        TimeEntryApprovedCorrected correctedEvent = new(
            new TimeEntryId("time-entry-123"),
            new TimeEntryCorrectionId("approved-correction-123"),
            new TenantReference("tenant-123"),
            new PartyReference("party-operator"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            previous,
            corrected,
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("approval-decision-123"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

        string json = JsonSerializer.Serialize(correctedEvent, JsonOptions);

        json.ShouldContain("\"timeEntryCorrectionId\"");
        json.ShouldContain("\"previousValues\"");
        json.ShouldContain("\"correctedValues\"");
        json.ShouldContain("\"reason\"");
        json.ShouldContain("\"sourceApprovalDecisionId\"");
        json.ShouldContain("\"approvalState\":\"Approved\"");
        json.ShouldContain("\"correctionState\":\"Corrected\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryApprovedCorrected? roundTripped = JsonSerializer.Deserialize<TimeEntryApprovedCorrected>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryCorrectionId.Value.ShouldBe("approved-correction-123");
        roundTripped.PreviousValues.DurationMinutes.ShouldBe(60);
        roundTripped.CorrectedValues.DurationMinutes.ShouldBe(75);
        roundTripped.Reason.Value.ShouldBe("Correct approved duration after audit review.");
        roundTripped.SourceApprovalDecisionId.Value.ShouldBe("approval-decision-123");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        roundTripped.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
    }

    [Fact]
    public void Time_entry_evidence_read_model_round_trips_source_lineage_and_display_hydration_without_raw_envelope_fields()
    {
        TimeEntryEvidenceReadModel readModel = new(
            new TimeEntryId("time-entry-789"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-789")),
            new PartyReference("party-789"),
            new ActivityTypeId("activity-type-789"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 19),
            75,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh)
        {
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage =
            [
                new("TimeEntryRecorded", 7, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            ],
            ApprovalDecision = new(
                new TimeEntryId("time-entry-789"),
                new TimeEntryApprovalDecisionId("decision-789"),
                new PartyReference("approver-789"),
                new TenantReference("tenant-789"),
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
                null),
            DisplayHydration = new(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor label"),
                TimeEntryHydratedDisplayLabel.Stale("Project label"),
                TimeEntryHydratedDisplayLabel.Unavailable())
        };

        string json = JsonSerializer.Serialize(readModel, JsonOptions);

        json.ShouldContain("\"sourceAuthority\":\"TimesheetsDomainEvents\"");
        json.ShouldContain("\"eventLineage\"");
        json.ShouldContain("\"eventName\":\"TimeEntryRecorded\"");
        json.ShouldContain("\"ordinal\":7");
        json.ShouldContain("\"approvalDecision\"");
        json.ShouldContain("\"approvalScope\":\"IndividualEntry\"");
        json.ShouldContain("\"displayHydration\"");
        json.ShouldContain("\"state\":\"Stale\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);
        json.ShouldNotContain("\"payload\"", Case.Insensitive);
        json.ShouldNotContain("\"envelope\"", Case.Insensitive);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.SourceAuthority.ShouldBe(TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        roundTripped.EventLineage.ShouldHaveSingleItem().Ordinal.ShouldBe(7);
        roundTripped.ApprovalDecision.ShouldNotBeNull();
        roundTripped.ApprovalDecision.TimeEntryApprovalDecisionId.Value.ShouldBe("decision-789");
        roundTripped.DisplayHydration.Target.State.ShouldBe(DisplayHydrationState.Stale);
    }

    [Fact]
    public void Rejection_contract_round_trips_policy_failures_without_disclosing_protected_context()
    {
        TimesheetsRejection rejection = new(
            TimesheetsRejectionCode.AuthorityCannotBeResolved,
            "Authority cannot be resolved.",
            [
                new("target", "authority-unresolved", "Authority cannot be resolved.")
            ]);

        string json = JsonSerializer.Serialize(rejection, JsonOptions);

        json.ShouldContain("\"code\":\"AuthorityCannotBeResolved\"");
        json.ShouldContain("\"fieldErrors\"");
        json.ShouldContain("\"field\":\"target\"");
        AssertJsonOmitsCallerAuthority(json);

        TimesheetsRejection? roundTripped = JsonSerializer.Deserialize<TimesheetsRejection>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Code.ShouldBe(TimesheetsRejectionCode.AuthorityCannotBeResolved);
        roundTripped.FieldErrors.Single().Code.ShouldBe("authority-unresolved");
    }

    [Fact]
    public void Public_commands_do_not_accept_server_controlled_authority_or_envelope_fields()
    {
        Type[] commandTypes =
        [
            typeof(RecordTimeEntry),
            typeof(SubmitTimeEntriesForApproval),
            typeof(ApproveTimesheetPeriod),
            typeof(RejectTimesheetPeriod),
            typeof(ApproveTimeEntry),
            typeof(RejectTimeEntry),
            typeof(CorrectRejectedTimeEntry),
            typeof(CorrectApprovedTimeEntry),
            typeof(CreateTenantActivityType),
            typeof(CreateProjectActivityType),
            typeof(RenameProjectActivityType),
            typeof(UpdateProjectActivityTypeMetadata),
            typeof(DeactivateProjectActivityType),
            typeof(ReactivateProjectActivityType),
            typeof(ConfigureProjectActivityTypeCatalogRestriction),
            typeof(RenameActivityType),
            typeof(UpdateActivityTypeMetadata),
            typeof(DeactivateActivityType),
            typeof(ReactivateActivityType),
            typeof(ReadTimeEntryEvidence)
        ];

        string[] forbiddenNames =
        [
            "TenantId",
            "UserId",
            "CorrelationId",
            "MessageId",
            "CausationId",
            "Sequence",
            "Stream",
            "ClaimsPrincipal",
            "Authorization",
            "Jwt",
            "Token",
            "Roles"
        ];

        foreach (Type commandType in commandTypes)
        {
            string[] propertyNames = commandType.GetProperties().Select(static property => property.Name).ToArray();

            foreach (string forbiddenName in forbiddenNames)
            {
                propertyNames.ShouldNotContain(forbiddenName, commandType.Name);
            }
        }
    }

    [Fact]
    public void Value_enums_expose_unknown_zero_sentinels()
    {
        Enum.GetName((TimeEntryTargetKind)0).ShouldBe("Unknown");
        Enum.GetName((ContributorCategory)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryApprovalState)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntrySubmissionScope)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryApprovalScope)0).ShouldBe("Unknown");
        Enum.GetName((BillableState)0).ShouldBe("Unknown");
        Enum.GetName((ActivityTypeScope)0).ShouldBe("Unknown");
        Enum.GetName((ActivityTypeActiveState)0).ShouldBe("Unknown");
        Enum.GetName((ProjectionFreshnessState)0).ShouldBe("Unknown");
        Enum.GetName((AiMetricAvailability)0).ShouldBe("Unknown");
        Enum.GetName((AiEffortMetricSourceCategory)0).ShouldBe("Unknown");
        Enum.GetName((AiTokenMetricAvailability)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryEvidenceSourceAuthority)0).ShouldBe("Unknown");
        Enum.GetName((DisplayHydrationState)0).ShouldBe("Unknown");
        Enum.GetName((ApprovalAuthorityAction)0).ShouldBe("Unknown");
        Enum.GetName((ApprovalAuthoritySource)0).ShouldBe("Unknown");
        Enum.GetName((ApprovalAuthorityDecisionState)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryCorrectionState)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryLockState)0).ShouldBe("Unknown");
    }

    [Fact]
    public void Time_entry_lock_evidence_round_trips_without_authority_or_envelope_fields()
    {
        TimeEntryLockEvidence evidence = new(
            TimeEntryLockState.LockedFromDirectEdit,
            new TimeEntryApprovalDecisionId("approval-decision-123"),
            TimeEntryApprovalScope.IndividualEntry,
            new PartyReference("approver-123"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            "Approved entries are locked from direct edits.");

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"lockState\":\"LockedFromDirectEdit\"");
        json.ShouldContain("\"sourceApprovalDecisionId\"");
        json.ShouldContain("\"sourceApprovalScope\":\"IndividualEntry\"");
        json.ShouldContain("\"lockedBy\"");
        json.ShouldContain("\"lockedAtUtc\"");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("claim", Case.Insensitive);
        json.ShouldNotContain("role", Case.Insensitive);
        json.ShouldNotContain("envelope", Case.Insensitive);
        json.ShouldNotContain("command", Case.Insensitive);

        TimeEntryLockEvidence? roundTripped = JsonSerializer.Deserialize<TimeEntryLockEvidence>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        roundTripped.SourceApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("approval-decision-123"));
        roundTripped.SourceApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        roundTripped.LockedBy.ShouldBe(new PartyReference("approver-123"));
    }

    [Fact]
    public void Time_entry_evidence_read_model_carries_lock_state_and_evidence()
    {
        TimeEntryEvidenceReadModel model = new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Approved,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh)
        {
            LockEvidence = new(
                TimeEntryLockState.LockedFromDirectEdit,
                new TimeEntryApprovalDecisionId("approval-decision-1"),
                TimeEntryApprovalScope.IndividualEntry,
                new PartyReference("approver-1"),
                new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
                "Approved entries are locked from direct edits.")
        };

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"lockEvidence\"");
        json.ShouldContain("\"lockState\":\"LockedFromDirectEdit\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.LockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
    }

    [Fact]
    public void Ai_metrics_keep_units_source_metadata_and_unavailable_provider_counts_explicit()
    {
        AiEffortMetrics unavailable = AiEffortMetrics.Unavailable;

        unavailable.Availability.ShouldBe(AiMetricAvailability.Unavailable);
        unavailable.Source.ShouldBe(AiEffortMetricSourceMetadata.Unavailable);
        unavailable.TokenAvailability.ShouldBe(AiTokenMetricAvailability.Unavailable);
        unavailable.WallClockDurationMilliseconds.ShouldBeNull();
        unavailable.ModelRuntimeMilliseconds.ShouldBeNull();
        unavailable.BillableEffortMinutes.ShouldBeNull();
        unavailable.ProviderInputTokenCount.ShouldBeNull();
        unavailable.ProviderOutputTokenCount.ShouldBeNull();
        unavailable.ProviderTotalTokenCount.ShouldBeNull();

        typeof(AiEffortMetrics).GetProperty("Duration").ShouldBeNull();
        typeof(AiEffortMetrics).GetProperty("Tokens").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Prompt").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Response").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Secret").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("BearerToken").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("RequestBody").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("ResponseBody").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("PartyDisplayName").ShouldBeNull();
    }

    [Fact]
    public void Ai_metrics_distinguish_provider_reported_zero_tokens_from_unreported_tokens()
    {
        AiEffortMetrics reportedZeroTokens = new(
            AiMetricAvailability.ProviderReported,
            100,
            90,
            1,
            0,
            0,
            0,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-456"),
            AiTokenMetricAvailability.ProviderReported);
        AiEffortMetrics notReportedTokens = new(
            AiMetricAvailability.ProviderReported,
            100,
            90,
            1,
            null,
            null,
            null,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-789"),
            AiTokenMetricAvailability.NotReported);

        reportedZeroTokens.ProviderInputTokenCount.ShouldBe(0);
        reportedZeroTokens.ProviderOutputTokenCount.ShouldBe(0);
        reportedZeroTokens.ProviderTotalTokenCount.ShouldBe(0);
        reportedZeroTokens.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);

        notReportedTokens.ProviderInputTokenCount.ShouldBeNull();
        notReportedTokens.ProviderOutputTokenCount.ShouldBeNull();
        notReportedTokens.ProviderTotalTokenCount.ShouldBeNull();
        notReportedTokens.TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
    }

    [Fact]
    public void Time_entry_evidence_read_model_keeps_sibling_references_stable_and_projection_freshness_explicit()
    {
        TimeEntryEvidenceReadModel model = new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh);

        model.Target.TargetId.ShouldBe("project-1");
        model.Contributor.PartyId.ShouldBe("party-1");
        model.DurationMinutes.ShouldBe(60);
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public void Metadata_catalog_declares_foundational_frontcomposer_surfaces_without_runtime_dependencies()
    {
        TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.Name)
            .ShouldBe(
            [
                "timesheets.command.record-time",
                "timesheets.command.submit-time-entries",
                "timesheets.command.external-contribution",
                "timesheets.command.magic-link-adjustment",
                "timesheets.command.submit-period",
                "timesheets.command.correct-rejected-time-entry",
                "timesheets.command.correct-approved-time-entry",
                "timesheets.command.activity-type-catalog",
                "timesheets.projection.activity-type-catalog",
                "timesheets.dashboard.overview",
                "timesheets.projection.time-entry-evidence",
                "timesheets.projection.time-entry-query",
                "timesheets.projection.approved-time-ledger",
                "timesheets.command.approved-ledger-export",
                "timesheets.projection.project-actual-time-report",
                "timesheets.projection.work-actual-time-report",
                "timesheets.projection.my-timesheet-period",
                "timesheets.projection.period-approval-detail",
                "timesheets.approvals.queue",
                "timesheets.command.time-entry-approval",
                "timesheets.command.period-approval",
                "timesheets.review.export-policy",
                "timesheets.projection.magic-link-confirmation-capabilities"
            ]);

        TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Pattern)
            .ShouldContain(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Pattern)
            .ShouldContain(TimesheetsCompositionPattern.FrontComposerProjectionView);

        string serializedMetadata = string.Join(
            " ",
            TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.ToString()));

        serializedMetadata.ShouldNotContain("Microsoft.FluentUI");
        serializedMetadata.ShouldNotContain("Microsoft.AspNetCore");
        serializedMetadata.ShouldNotContain("Dapr");
        serializedMetadata.ShouldNotContain("EventStore");
    }

    [Fact]
    public void Metadata_catalog_declares_required_status_badge_vocabularies()
    {
        string[] badgeVocabularies = TimesheetsMetadataCatalog.Descriptors
            .SelectMany(static descriptor => descriptor.StateBadges)
            .Select(static badge => badge.StateVocabulary)
            .ToArray();

        badgeVocabularies.ShouldContain(nameof(TimeEntryApprovalState));
        badgeVocabularies.ShouldContain(nameof(BillableState));
        badgeVocabularies.ShouldContain(nameof(ContributorCategory));
        badgeVocabularies.ShouldContain(nameof(TimeEntrySourceType));
        badgeVocabularies.ShouldContain(nameof(ApprovedTimeLedgerRowState));
        badgeVocabularies.ShouldContain(nameof(ActualTimeReportRowState));
        badgeVocabularies.ShouldContain(nameof(ActualTimeReferenceState));
        badgeVocabularies.ShouldContain(nameof(WorkPlannedEffortAvailability));
        badgeVocabularies.ShouldContain(nameof(ActivityTypeActiveState));
        badgeVocabularies.ShouldContain(nameof(TimeEntryCorrectionState));
        badgeVocabularies.ShouldContain(nameof(ProjectionFreshnessState));
        badgeVocabularies.ShouldContain(nameof(TimeEntryEvidenceSourceAuthority));
        badgeVocabularies.ShouldContain(nameof(DisplayHydrationState));
        badgeVocabularies.ShouldContain(nameof(ApprovalAuthorityDecisionState));
        badgeVocabularies.ShouldContain(nameof(ApprovalAuthoritySource));
        badgeVocabularies.ShouldContain(nameof(TimeEntryLockState));
        badgeVocabularies.ShouldContain(nameof(TimesheetPeriodApprovalState));
        badgeVocabularies.ShouldContain(nameof(TimesheetPeriodKind));
        badgeVocabularies.ShouldContain(nameof(MagicLinkCapabilityState));
        badgeVocabularies.ShouldContain(nameof(MagicLinkExpiryState));
        badgeVocabularies.ShouldContain(nameof(MagicLinkAllowedAction));
        badgeVocabularies.ShouldContain(nameof(ApprovedTimeExportReadinessState));
        badgeVocabularies.ShouldContain(nameof(ApprovedTimeExportFormat));
    }

    [Fact]
    public void Record_time_entry_e2e_metadata_exposes_expected_capture_workflow()
    {
        TimesheetsMetadataDescriptor command = Descriptor("timesheets.command.record-time");
        TimesheetsMetadataDescriptor submission = Descriptor("timesheets.command.submit-time-entries");
        TimesheetsMetadataDescriptor periodSubmission = Descriptor("timesheets.command.submit-period");
        TimesheetsMetadataDescriptor evidence = Descriptor("timesheets.projection.time-entry-evidence");
        TimesheetsMetadataDescriptor myPeriod = Descriptor("timesheets.projection.my-timesheet-period");
        TimesheetsMetadataDescriptor periodApprovalDetail = Descriptor("timesheets.projection.period-approval-detail");

        command.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        command.Fields.Select(static field => field.Name).ShouldBe(
        [
            "serviceDate",
            "target",
            "contributor",
            "activityType",
            "durationMinutes",
            "billableState",
            "contributorCategory",
            "aiMetrics",
            "aiWallClockDurationMilliseconds",
            "aiModelRuntimeMilliseconds",
            "aiBillableEffortMinutes",
            "aiTokenAvailability",
            "aiMetricSource",
            "comment"
        ]);
        command.Fields.Single(static field => field.Name == "durationMinutes")
            .ContractType.ShouldBe("WholeMinutes");
        command.Fields.Single(static field => field.Name == "aiMetrics")
            .HelpText.ShouldBe("AI metrics keep runtime, effort, and token units explicit.");
        command.Actions.Select(static action => action.Name).ShouldBe(
        [
            "record-time",
            "record-project-time",
            "record-work-time"
        ]);
        submission.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        submission.Fields.Select(static field => field.Name).ShouldBe(
        [
            "timeEntryIds",
            "timeEntrySubmissionId",
            "submissionScope",
            "approvalState",
            "blockingFields",
            "projectionFreshness",
            "partialSubmission",
            "persistentMessageBarState"
        ]);
        submission.Actions.Select(static action => action.Label).ShouldContain("Submit entries");
        periodSubmission.Actions.Select(static action => action.Label).ShouldContain("Submit period");
        periodSubmission.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.SubmitTimesheetPeriod");
        periodSubmission.Fields.Select(static field => field.Name).ShouldContain("blockingEntryGuidance");
        periodSubmission.Fields.Select(static field => field.Name).ShouldContain("tenantTimeZoneId");
        submission.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryApprovalState));
        submission.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
        command.StateBadges.Select(static badge => badge.StateVocabulary).ShouldBe(
        [
            nameof(TimeEntryApprovalState),
            nameof(BillableState),
            nameof(ContributorCategory),
            nameof(AiMetricAvailability),
            nameof(AiTokenMetricAvailability),
            nameof(AiEffortMetricSourceCategory),
            nameof(TimesheetsEvidenceRetentionCategory)
        ]);

        command.Fields.Select(static field => field.Name).ShouldContain("aiMetricSource");
        command.Fields.Select(static field => field.Name).ShouldContain("aiTokenAvailability");
        evidence.Fields.Select(static field => field.Name).ShouldContain("projectionFreshness");
        evidence.Fields.Select(static field => field.Name).ShouldContain("correctionState");
        evidence.Fields.Select(static field => field.Name).ShouldContain("correction");
        evidence.Fields.Select(static field => field.Name).ShouldContain("approvedCorrection");
        evidence.Fields.Select(static field => field.Name).ShouldContain("correctionReason");
        evidence.Fields.Select(static field => field.Name).ShouldContain("sourceAuthority");
        evidence.Fields.Select(static field => field.Name).ShouldContain("eventLineage");
        evidence.Fields.Select(static field => field.Name).ShouldContain("approvalDecision");
        evidence.Fields.Select(static field => field.Name).ShouldContain("rejectionReason");
        evidence.Fields.Select(static field => field.Name).ShouldContain("authoritySource");
        evidence.Fields.Select(static field => field.Name).ShouldContain("authorityFreshness");
        evidence.Fields.Select(static field => field.Name).ShouldContain("displayHydration");
        evidence.Fields.Select(static field => field.Name).ShouldContain("lockEvidence");
        evidence.Fields.Select(static field => field.Name).ShouldContain("lockState");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiWallClockDurationMilliseconds");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiModelRuntimeMilliseconds");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiBillableEffortMinutes");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiTokenAvailability");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiMetricSource");
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryCorrectionState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryEvidenceSourceAuthority));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(DisplayHydrationState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ApprovalAuthorityDecisionState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ApprovalAuthoritySource));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryLockState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(AiTokenMetricAvailability));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(AiEffortMetricSourceCategory));
        myPeriod.Actions.Select(static action => action.Name).ShouldContain("submit-period");
        myPeriod.Fields.Select(static field => field.Name).ShouldContain("periodState");
        myPeriod.Fields.Select(static field => field.Name).ShouldContain("entrySummaries");
        myPeriod.Fields.Select(static field => field.Name).ShouldContain("blockingEntryGuidance");
        myPeriod.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimesheetPeriodApprovalState));
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("periodState");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("entrySummaries");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("affectedEntryIds");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("periodDecision");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("rejectedEntries");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("authorityFreshness");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("lockState");
        periodApprovalDetail.Fields.Select(static field => field.Name).ShouldContain("projectionFreshness");
        periodApprovalDetail.Actions.Select(static action => action.Label).ShouldContain("Approve period");
        periodApprovalDetail.Actions.Select(static action => action.Label).ShouldContain("Reject period");
        periodApprovalDetail.Actions.Select(static action => action.Label).ShouldContain("Correct entry");
        periodApprovalDetail.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimesheetPeriodApprovalState));
        periodApprovalDetail.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryApprovalState));
    }

    [Fact]
    public void Operational_time_entry_query_metadata_declares_filters_grid_fields_and_badges()
    {
        TimesheetsMetadataDescriptor query = Descriptor("timesheets.projection.time-entry-query");

        query.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);
        query.Fields.Select(static field => field.Name).ShouldBe(
        [
            "contributorFilter",
            "projectFilter",
            "workFilter",
            "periodFilter",
            "activityTypeFilter",
            "billableFilter",
            "approvalStateFilter",
            "correctionStateFilter",
            "sourceTypeFilter",
            "timeEntry",
            "target",
            "contributor",
            "activityType",
            "serviceDate",
            "durationMinutes",
            "billableState",
            "approvalState",
            "correctionState",
            "sourceType",
            "displayHydration",
            "projectionFreshness"
        ]);
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryApprovalState));
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryCorrectionState));
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(BillableState));
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntrySourceType));
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(DisplayHydrationState));
        query.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
    }

    [Fact]
    public void Activity_type_catalog_read_model_exposes_text_status_and_selection_metadata()
    {
        ActivityTypeCatalogReadModel model = new(
            [
                new(
                    new ActivityTypeId("activity-type-1"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Discovery",
                    false,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Stale("42"));

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"statusText\":\"Inactive\"");
        json.ShouldContain("\"activeState\":\"Inactive\"");
        json.ShouldContain("\"isAvailableForCapture\":false");
        json.ShouldContain("\"state\":\"Stale\"");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public void Openapi_ready_artifact_documents_safe_contract_surface_without_product_endpoints()
    {
        string artifactPath = RepositoryPath(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json");
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(artifactPath))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        artifact["openapi"]?.GetValue<string>().ShouldBe("3.1.0");
        artifact["info"]?["title"]?.GetValue<string>().ShouldBe("Hexalith Timesheets Capture Contracts");

        JsonObject paths = artifact["paths"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI paths node is missing.");
        paths.Select(static path => path.Key).Order(StringComparer.Ordinal).ShouldBe(
        [
            "/api/timesheets/magic-links/adjust",
            "/api/timesheets/magic-links/adjust/submit",
            "/api/timesheets/magic-links/confirm",
            "/api/timesheets/magic-links/confirm/submit"
        ]);

        JsonArray serverDerivedContext = artifact["x-hexalith-boundaries"]?["serverDerivedContext"]?.AsArray()
            ?? throw new InvalidOperationException("serverDerivedContext boundary metadata is missing.");
        serverDerivedContext.Select(static value => value?.GetValue<string>()).ShouldBe(
        [
            "tenant",
            "user",
            "correlation",
            "authorization"
        ]);

        JsonObject schemas = artifact["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI schemas node is missing.");

        schemas.ContainsKey("RecordTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("SubmitTimeEntriesForApproval").ShouldBeTrue();
        schemas.ContainsKey("SubmitTimesheetPeriod").ShouldBeTrue();
        schemas.ContainsKey("ApproveTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("RejectTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("CorrectRejectedTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("CorrectApprovedTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmitted").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodSubmitted").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApproved").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryRejected").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryCorrected").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovedCorrected").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryCorrectionId").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryCorrectionReason").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryCorrectionValues").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryCorrectionEvidence").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovedCorrectionEvidence").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryLockEvidence").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryLockState").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmissionId").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodId").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodRequest").ShouldBeTrue();
        schemas.ContainsKey("TenantLocalPeriodBoundary").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodBlockingEntryGuidance").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodSummaryReadModel").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovalDecisionId").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodApprovalDecisionId").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodRejectionReason").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodSelectedEntryRejectionEvidence").ShouldBeTrue();
        schemas.ContainsKey("TimesheetPeriodApprovalDecisionEvidence").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmissionScope").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovalScope").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryRejectionReason").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryTargetReference").ShouldBeTrue();
        schemas.ContainsKey("AiEffortMetrics").ShouldBeTrue();
        schemas.ContainsKey("AiEffortMetricSourceMetadata").ShouldBeTrue();
        schemas.ContainsKey("ActivityTypeCatalogCommand").ShouldBeTrue();
        schemas.ContainsKey("CreateProjectActivityType").ShouldBeTrue();
        schemas.ContainsKey("RenameProjectActivityType").ShouldBeTrue();
        schemas.ContainsKey("UpdateProjectActivityTypeMetadata").ShouldBeTrue();
        schemas.ContainsKey("ConfigureProjectActivityTypeCatalogRestriction").ShouldBeTrue();
        schemas.ContainsKey("ActivityTypeCatalogReadModel").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryEvidenceReadModel").ShouldBeTrue();
        schemas.ContainsKey("TimesheetsMetadataDescriptor").ShouldBeTrue();

        string schemaJson = schemas.ToJsonString();
        AssertJsonOmitsCallerAuthority(schemaJson, allowTenantId: true);
        schemaJson.ShouldContain("wall-clock execution time in milliseconds");
        schemaJson.ShouldContain("model or tool runtime in milliseconds");
        schemaJson.ShouldContain("Provider token counts are nullable when not reported");
        schemaJson.ShouldContain("AiTokenMetricAvailability");
        schemaJson.ShouldContain("AiEffortMetricSourceMetadata");
        schemaJson.ShouldContain("SubmitTimeEntriesForApproval");
        schemaJson.ShouldContain("SubmitTimesheetPeriod");
        schemaJson.ShouldContain("ApproveTimesheetPeriod");
        schemaJson.ShouldContain("RejectTimesheetPeriod");
        schemaJson.ShouldContain("TimeEntrySubmitted");
        schemaJson.ShouldContain("TimesheetPeriodSubmitted");
        schemaJson.ShouldContain("TimesheetPeriodApproved");
        schemaJson.ShouldContain("TimesheetPeriodRejected");
        schemaJson.ShouldContain("ApproveTimeEntry");
        schemaJson.ShouldContain("RejectTimeEntry");
        schemaJson.ShouldContain("CorrectRejectedTimeEntry");
        schemaJson.ShouldContain("CorrectApprovedTimeEntry");
        schemaJson.ShouldContain("TimeEntryApproved");
        schemaJson.ShouldContain("TimeEntryRejected");
        schemaJson.ShouldContain("TimeEntryCorrected");
        schemaJson.ShouldContain("TimeEntryApprovedCorrected");
        schemaJson.ShouldContain("LockedFromDirectEdit");
        schemaJson.ShouldContain("Approved entries are locked from direct edits");
        schemaJson.ShouldContain("selected-entry rejection evidence");
        schemaJson.ShouldContain("period state separately from entry states");
        schemaJson.ShouldNotContain("EventStore");
        schemaJson.ShouldNotContain("invoice", Case.Insensitive);
        schemaJson.ShouldNotContain("payroll", Case.Insensitive);
        schemaJson.ShouldNotContain("revenue", Case.Insensitive);
    }

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
            "stream",
            "sequence"
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

    private static TimesheetsMetadataDescriptor Descriptor(string name)
        => TimesheetsMetadataCatalog.Descriptors.Single(descriptor => descriptor.Name == name);

    private static TimeEntryCorrectionValues CorrectionValues(int durationMinutes)
        => new(
            TimeEntryTargetReference.ForProject(new ProjectReference("project-123")),
            new PartyReference("party-123"),
            new ActivityTypeId("activity-type-123"),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Prior evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static ApprovalAuthoritySourceAttribution AuthoritySource(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

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
