using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryAggregateTests
{
    [Fact]
    public void Record_draft_time_entry_emits_stable_draft_event()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = Comment() };

        TimesheetsDomainResult result = TimeEntry.Handle(command, null, ActivityTypeScope.Tenant);

        TimeEntryRecorded recorded = SingleSuccess<TimeEntryRecorded>(result);
        recorded.TimeEntryId.ShouldBe(command.TimeEntryId);
        recorded.Target.ShouldBe(command.Target);
        recorded.Contributor.ShouldBe(command.Contributor);
        recorded.ActivityTypeId.ShouldBe(command.ActivityTypeId);
        recorded.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        recorded.ServiceDate.ShouldBe(command.ServiceDate);
        recorded.DurationMinutes.ShouldBe(60);
        recorded.BillableState.ShouldBe(BillableState.Billable);
        recorded.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        recorded.ContributorCategory.ShouldBe(ContributorCategory.Employee);
        recorded.AiMetrics.ShouldBe(AiEffortMetrics.Unavailable);
        recorded.Comment.ShouldBe(command.Comment);
    }

    [Fact]
    public void Record_rejects_duplicate_time_entry_id_without_duplicate_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            command.TimeEntryId,
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            ActivityTypeScope.Tenant,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            TimeEntryApprovalState.Draft,
            command.ContributorCategory,
            command.AiMetrics));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, state, ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "timeEntryId" && error.Code == "duplicate");
    }

    [Theory]
    [InlineData(0, "durationMinutes", "positive")]
    [InlineData(-5, "durationMinutes", "positive")]
    public void Record_rejects_non_positive_duration_with_field_error(
        int durationMinutes,
        string expectedField,
        string expectedCode)
    {
        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            ValidCommand() with { DurationMinutes = durationMinutes },
            null,
            ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(error => error.Field == expectedField && error.Code == expectedCode);
    }

    [Fact]
    public void Record_rejects_missing_required_capture_fields()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            Target = null!,
            Contributor = null!,
            ActivityTypeId = null!,
            BillableState = BillableState.Unknown,
            ContributorCategory = ContributorCategory.Unknown
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Unknown));

        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("target");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("contributor");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("activityTypeId");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("billableState");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("contributorCategory");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("activityTypeScope");
    }

    [Fact]
    public void Record_rejects_invalid_ai_metric_units()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            AiMetrics = new(
                AiMetricAvailability.ProviderReported,
                -1,
                -2,
                -3,
                -4,
                -5,
                -6,
                AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
                AiTokenMetricAvailability.ProviderReported),
            ContributorCategory = ContributorCategory.AutomatedAgent,
            Comment = null
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.wallClockDurationMilliseconds");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.modelRuntimeMilliseconds");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.billableEffortMinutes");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerInputTokenCount");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerOutputTokenCount");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerTotalTokenCount");
    }

    [Fact]
    public void Record_accepts_automated_agent_provider_metrics_and_keeps_duration_authoritative()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            ContributorCategory = ContributorCategory.AutomatedAgent,
            AiMetrics = ProviderReportedMetrics(billableEffortMinutes: 5),
            DurationMinutes = 60
        };

        TimeEntryRecorded recorded = SingleSuccess<TimeEntryRecorded>(
            TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        recorded.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        recorded.DurationMinutes.ShouldBe(60);
        recorded.AiMetrics.ShouldNotBeNull();
        recorded.AiMetrics.BillableEffortMinutes.ShouldBe(5);
        recorded.AiMetrics.Source.ShouldNotBeNull();
        recorded.AiMetrics.Source.SourceCategory.ShouldBe(AiEffortMetricSourceCategory.Provider);
        recorded.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
    }

    [Theory]
    [InlineData(ContributorCategory.Employee)]
    [InlineData(ContributorCategory.ExternalContributor)]
    public void Record_rejects_human_or_external_provider_reported_ai_metrics(
        ContributorCategory category)
    {
        RecordTimeEntry command = ValidCommand() with
        {
            ContributorCategory = category,
            AiMetrics = ProviderReportedMetrics()
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "aiMetrics" && error.Code == "automated-agent-required");
    }

    [Fact]
    public void Record_rejects_unknown_ai_source_and_token_availability()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            ContributorCategory = ContributorCategory.AutomatedAgent,
            AiMetrics = new(
                AiMetricAvailability.ProviderReported,
                1,
                1,
                1,
                null,
                null,
                null,
                new(AiEffortMetricSourceCategory.Unknown, null, null, null),
                AiTokenMetricAvailability.Unknown)
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "aiMetrics.source.sourceCategory" && error.Code == "unknown");
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "aiMetrics.tokenAvailability" && error.Code == "unknown");
    }

    [Fact]
    public void Record_rejects_unavailable_token_metrics_when_counts_are_supplied()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            ContributorCategory = ContributorCategory.AutomatedAgent,
            AiMetrics = new(
                AiMetricAvailability.ProviderReported,
                1,
                1,
                1,
                10,
                null,
                null,
                AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-2"),
                AiTokenMetricAvailability.NotReported)
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "aiMetrics.providerTokenCounts" && error.Code == "must-be-null");
    }

    [Fact]
    public void Record_accepts_unavailable_ai_placeholder_for_human_entries()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            ContributorCategory = ContributorCategory.Employee,
            AiMetrics = AiEffortMetrics.Unavailable
        };

        TimeEntryRecorded recorded = SingleSuccess<TimeEntryRecorded>(
            TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        recorded.AiMetrics.ShouldBe(AiEffortMetrics.Unavailable);
    }

    [Fact]
    public void Record_rejects_invalid_comment_policy_decisions()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = InvalidComment() };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "comment.policy" && error.Code == "unknown");
    }

    [Fact]
    public void Submit_draft_time_entry_emits_submitted_event_and_preserves_recorded_state()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = Comment() };
        TimeEntryState state = RecordedState(command);
        SubmitTimeEntriesForApproval submit = SubmitCommand(command.TimeEntryId);
        DateTimeOffset submittedAtUtc = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

        TimeEntrySubmitted submitted = SingleSuccess<TimeEntrySubmitted>(
            TimeEntry.Handle(
                submit,
                command.TimeEntryId,
                state,
                new PartyReference("submitter-1"),
                new TenantReference("tenant-1"),
                submittedAtUtc,
                ActivityTypeScope.Tenant));

        submitted.TimeEntryId.ShouldBe(command.TimeEntryId);
        submitted.Submitter.ShouldBe(new PartyReference("submitter-1"));
        submitted.Tenant.ShouldBe(new TenantReference("tenant-1"));
        submitted.SubmittedAtUtc.ShouldBe(submittedAtUtc);
        submitted.TimeEntrySubmissionId.ShouldBe(submit.TimeEntrySubmissionId);
        submitted.SubmissionScope.ShouldBe(TimeEntrySubmissionScope.SelectedEntries);
        submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        state.DurationMinutes.ShouldBe(command.DurationMinutes);
        state.Comment.ShouldBe(command.Comment);
    }

    [Fact]
    public void Submit_duplicate_same_submission_id_is_noop()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RecordedState(command);
        SubmitTimeEntriesForApproval submit = SubmitCommand(command.TimeEntryId);
        TimeEntrySubmitted submitted = new(
            command.TimeEntryId,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            submit.TimeEntrySubmissionId,
            submit.SubmissionScope,
            TimeEntryApprovalState.Submitted);
        state.Apply(submitted);

        TimesheetsDomainResult result = TimeEntry.Handle(
            submit,
            command.TimeEntryId,
            state,
            submitted.Submitter,
            submitted.Tenant,
            submitted.SubmittedAtUtc,
            ActivityTypeScope.Tenant);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Submit_rejects_non_draft_state_with_different_submission_id()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RecordedState(command);
        state.Apply(new TimeEntrySubmitted(
            command.TimeEntryId,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-original"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            SubmitCommand(command.TimeEntryId, "submission-new"),
            command.TimeEntryId,
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 5, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].approvalState" && error.Code == "invalid-transition");
    }

    [Fact]
    public void Submit_rejects_missing_submitter_tenant_and_unrecorded_state()
    {
        SubmitTimeEntriesForApproval submit = SubmitCommand(new TimeEntryId("time-entry-1"));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            submit,
            new TimeEntryId("time-entry-1"),
            null,
            null,
            null,
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "submitter" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "tenant" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].timeEntryId" && error.Code == "not-recorded");
    }

    [Fact]
    public void Submit_revalidates_required_recorded_facts_with_entry_field_paths()
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            new TimeEntryId("time-entry-1"),
            null!,
            null!,
            null!,
            ActivityTypeScope.Unknown,
            new DateOnly(2026, 6, 19),
            0,
            BillableState.Unknown,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Unknown,
            null));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            SubmitCommand(new TimeEntryId("time-entry-1")),
            new TimeEntryId("time-entry-1"),
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Unknown));

        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].target");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].contributor");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].activityTypeId");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].durationMinutes");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].billableState");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("entries[time-entry-1].contributorCategory");
    }

    [Fact]
    public void Approve_submitted_time_entry_emits_approved_event_and_preserves_prior_evidence()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = Comment() };
        TimeEntryState state = SubmittedState(command);
        DateTimeOffset decidedAtUtc = new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

        TimeEntryApproved approved = SingleSuccess<TimeEntryApproved>(
            TimeEntry.Handle(
                ApproveCommand(command.TimeEntryId),
                command.TimeEntryId,
                state,
                new PartyReference("approver-1"),
                new TenantReference("tenant-1"),
                decidedAtUtc,
                AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
                TimeEntryApprovalScope.IndividualEntry));

        approved.TimeEntryId.ShouldBe(command.TimeEntryId);
        approved.Approver.ShouldBe(new PartyReference("approver-1"));
        approved.Tenant.ShouldBe(new TenantReference("tenant-1"));
        approved.DecidedAtUtc.ShouldBe(decidedAtUtc);
        approved.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        approved.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        approved.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        approved.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        state.DurationMinutes.ShouldBe(command.DurationMinutes);
        state.Target.ShouldBe(command.Target);
        state.Contributor.ShouldBe(command.Contributor);
        state.TimeEntrySubmissionId.ShouldBe(new TimeEntrySubmissionId("submission-1"));
        state.Comment.ShouldBe(command.Comment);
    }

    [Fact]
    public void Reject_submitted_time_entry_emits_rejected_event_with_required_reason()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SubmittedState(command);
        RejectTimeEntry reject = RejectCommand(command.TimeEntryId);

        TimeEntryRejected rejected = SingleSuccess<TimeEntryRejected>(
            TimeEntry.Handle(
                reject,
                command.TimeEntryId,
                state,
                new PartyReference("approver-1"),
                new TenantReference("tenant-1"),
                new DateTimeOffset(2026, 6, 19, 13, 15, 0, TimeSpan.Zero),
                AllowedAuthority(ApprovalAuthorityAction.EntryRejection),
                TimeEntryApprovalScope.IndividualEntry));

        rejected.TimeEntryId.ShouldBe(command.TimeEntryId);
        rejected.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        rejected.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryRejection);
        rejected.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        rejected.Reason.ShouldBe(reject.Reason);
    }

    [Fact]
    public void Approve_duplicate_same_decision_id_and_resulting_state_is_noop()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryApproved(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));

        TimesheetsDomainResult result = TimeEntry.Handle(
            ApproveCommand(command.TimeEntryId),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Approved_state_derives_locked_from_direct_edit_from_replayed_events()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SubmittedState(command);

        state.IsLockedFromDirectEdit.ShouldBeFalse();
        state.LockState.ShouldBe(TimeEntryLockState.Unlocked);

        state.Apply(new TimeEntryApproved(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));

        state.IsLockedFromDirectEdit.ShouldBeTrue();
        state.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
    }

    [Fact]
    public void Rejects_different_decision_id_after_terminal_approval_state()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryApproved(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-original"),
            TimeEntryApprovalState.Approved,
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            ApproveCommand(command.TimeEntryId, "decision-new"),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 5, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "locked-from-direct-edit");
    }

    [Fact]
    public void Submit_after_approved_rejects_with_typed_lock_rejection()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = ApprovedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            SubmitCommand(command.TimeEntryId, "submission-after-approval"),
            command.TimeEntryId,
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "locked-from-direct-edit");
    }

    [Fact]
    public void Reject_after_approved_rejects_with_typed_lock_rejection_without_new_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = ApprovedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            RejectCommand(command.TimeEntryId, "decision-new"),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 5, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryRejection),
            TimeEntryApprovalScope.IndividualEntry));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "locked-from-direct-edit");
    }

    [Fact]
    public void Approve_rejects_unrecorded_draft_missing_context_and_non_utc_timestamp()
    {
        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            ApproveCommand(new TimeEntryId("time-entry-1")),
            new TimeEntryId("time-entry-1"),
            null,
            null,
            null,
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.FromHours(2)),
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval) with
            {
                DecisionState = ApprovalAuthorityDecisionState.Unknown
            },
            TimeEntryApprovalScope.Unknown));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "approver" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "tenant" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "decidedAtUtc" && error.Code == "utc-required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "authoritySource.decisionState" && error.Code == "unknown");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "approvalScope" && error.Code == "unknown");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].timeEntryId" && error.Code == "not-recorded");
    }

    [Fact]
    public void Reject_revalidates_missing_reason_and_submitted_state()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RecordedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            new RejectTimeEntry(
                command.TimeEntryId,
                new TimeEntryApprovalDecisionId("decision-1"),
                null!),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryRejection),
            TimeEntryApprovalScope.IndividualEntry));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "reason" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].approvalState" && error.Code == "invalid-transition");
    }

    [Fact]
    public void Correct_rejected_time_entry_emits_additive_correction_and_preserves_rejection_evidence()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = Comment() };
        TimeEntryState state = RejectedState(command);
        CorrectRejectedTimeEntry correction = CorrectCommand(command.TimeEntryId) with
        {
            DurationMinutes = 75,
            Comment = new("Corrected after rejection.", Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault)
        };
        DateTimeOffset correctedAtUtc = new(2026, 6, 20, 9, 30, 0, TimeSpan.Zero);

        TimeEntryCorrected corrected = SingleSuccess<TimeEntryCorrected>(
            TimeEntry.Handle(
                correction,
                command.TimeEntryId,
                state,
                new PartyReference("operator-1"),
                new TenantReference("tenant-1"),
                correctedAtUtc,
                ActivityTypeScope.Tenant));

        corrected.TimeEntryId.ShouldBe(command.TimeEntryId);
        corrected.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId);
        corrected.Tenant.ShouldBe(new TenantReference("tenant-1"));
        corrected.CorrectedBy.ShouldBe(new PartyReference("operator-1"));
        corrected.CorrectedAtUtc.ShouldBe(correctedAtUtc);
        corrected.PreviousValues.DurationMinutes.ShouldBe(60);
        corrected.PreviousValues.Comment.ShouldBe(command.Comment);
        corrected.CorrectedValues.DurationMinutes.ShouldBe(75);
        corrected.CorrectedValues.Comment.ShouldBe(correction.Comment);
        corrected.RejectionDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        corrected.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);

        state.RejectionReason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        state.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
    }

    [Fact]
    public void Correct_duplicate_same_correction_id_after_apply_is_noop()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RejectedState(command);
        CorrectRejectedTimeEntry correction = CorrectCommand(command.TimeEntryId);
        TimeEntryCorrected corrected = SingleSuccess<TimeEntryCorrected>(
            TimeEntry.Handle(
                correction,
                command.TimeEntryId,
                state,
                new PartyReference("operator-1"),
                new TenantReference("tenant-1"),
                new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
                ActivityTypeScope.Tenant));
        state.Apply(corrected);

        TimesheetsDomainResult result = TimeEntry.Handle(
            correction,
            command.TimeEntryId,
            state,
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 9, 35, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Correct_rejects_non_rejected_state_and_different_correction_after_correction()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState draft = RecordedState(command);

        TimesheetsRejection draftRejection = Rejection(TimeEntry.Handle(
            CorrectCommand(command.TimeEntryId),
            command.TimeEntryId,
            draft,
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        draftRejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].approvalState" && error.Code == "invalid-transition");

        TimeEntryState correctedState = RejectedState(command);
        correctedState.Apply(SingleSuccess<TimeEntryCorrected>(
            TimeEntry.Handle(
                CorrectCommand(command.TimeEntryId),
                command.TimeEntryId,
                correctedState,
                new PartyReference("operator-1"),
                new TenantReference("tenant-1"),
                new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
                ActivityTypeScope.Tenant)));

        TimesheetsRejection repeatedDifferent = Rejection(TimeEntry.Handle(
            CorrectCommand(command.TimeEntryId, "correction-2"),
            command.TimeEntryId,
            correctedState,
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 9, 35, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        repeatedDifferent.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].correctionState" && error.Code == "already-corrected");
    }

    [Fact]
    public void Correct_rejects_missing_context_non_utc_timestamp_invalid_comment_and_ai_metrics()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RejectedState(command);
        CorrectRejectedTimeEntry correction = CorrectCommand(command.TimeEntryId) with
        {
            TimeEntryCorrectionId = null!,
            DurationMinutes = 0,
            ContributorCategory = ContributorCategory.Employee,
            AiMetrics = ProviderReportedMetrics(),
            Comment = null
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            correction,
            command.TimeEntryId,
            state,
            null,
            null,
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.FromHours(2)),
            ActivityTypeScope.Unknown));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "timeEntryCorrectionId" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "correctedBy" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "tenant" && error.Code == "required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "correctedAtUtc" && error.Code == "utc-required");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].durationMinutes" && error.Code == "positive");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "entries[time-entry-1].activityTypeScope" && error.Code == "unknown");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics" && error.Code == "automated-agent-required");
    }

    [Fact]
    public void Submit_allows_corrected_rejected_entry_and_preserves_rejection_lineage()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RejectedState(command);
        state.Apply(SingleSuccess<TimeEntryCorrected>(
            TimeEntry.Handle(
                CorrectCommand(command.TimeEntryId),
                command.TimeEntryId,
                state,
                new PartyReference("operator-1"),
                new TenantReference("tenant-1"),
                new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
                ActivityTypeScope.Tenant)));

        TimeEntrySubmitted submitted = SingleSuccess<TimeEntrySubmitted>(TimeEntry.Handle(
            SubmitCommand(command.TimeEntryId, "submission-2"),
            command.TimeEntryId,
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        submitted.TimeEntrySubmissionId.ShouldBe(new TimeEntrySubmissionId("submission-2"));
        state.RejectionReason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        state.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
    }

    [Fact]
    public void Submit_rejects_rejected_state_without_intervening_correction()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RejectedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            SubmitCommand(command.TimeEntryId, "submission-2"),
            command.TimeEntryId,
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].approvalState" && error.Code == "invalid-transition");
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        state.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
    }

    [Fact]
    public void Correct_approved_entry_rejects_with_typed_lock_rejection_without_correction_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = ApprovedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            CorrectCommand(command.TimeEntryId),
            command.TimeEntryId,
            state,
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "locked-from-direct-edit");
        state.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
    }

    [Fact]
    public void Submit_after_superseded_entry_rejects_with_typed_superseded_lock_rejection()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = RejectedState(command);
        state.Apply(new TimeEntryCorrected(
            command.TimeEntryId,
            new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            CorrectionValues(command),
            CorrectionValues(command with { DurationMinutes = 75 }),
            new TimeEntryRejectionReason("Needs customer PO evidence."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Superseded));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            SubmitCommand(command.TimeEntryId, "submission-after-superseded"),
            command.TimeEntryId,
            state,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "superseded-locked");
        state.LockState.ShouldBe(TimeEntryLockState.SupersededLocked);
        state.IsLockedFromDirectEdit.ShouldBeTrue();
    }

    [Fact]
    public void Approve_after_superseded_submitted_rejects_with_typed_superseded_lock_without_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SupersededSubmittedState(command);

        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        state.LockState.ShouldBe(TimeEntryLockState.SupersededLocked);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            ApproveCommand(command.TimeEntryId, "decision-after-superseded"),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 11, 0, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "superseded-locked");
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        state.LockState.ShouldBe(TimeEntryLockState.SupersededLocked);
    }

    [Fact]
    public void Reject_after_superseded_submitted_rejects_with_typed_superseded_lock_without_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = SupersededSubmittedState(command);

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            RejectCommand(command.TimeEntryId, "decision-after-superseded"),
            command.TimeEntryId,
            state,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 11, 5, 0, TimeSpan.Zero),
            AllowedAuthority(ApprovalAuthorityAction.EntryRejection),
            TimeEntryApprovalScope.IndividualEntry));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].lockState" && error.Code == "superseded-locked");
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    private static RecordTimeEntry ValidCommand()
        => new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static ApproveTimeEntry ApproveCommand(TimeEntryId timeEntryId, string decisionId = "decision-1")
        => new(
            timeEntryId,
            new TimeEntryApprovalDecisionId(decisionId));

    private static RejectTimeEntry RejectCommand(TimeEntryId timeEntryId, string decisionId = "decision-1")
        => new(
            timeEntryId,
            new TimeEntryApprovalDecisionId(decisionId),
            new TimeEntryRejectionReason("Needs customer PO evidence."));

    private static CorrectRejectedTimeEntry CorrectCommand(TimeEntryId timeEntryId, string correctionId = "correction-1")
        => new(
            timeEntryId,
            new TimeEntryCorrectionId(correctionId),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static SubmitTimeEntriesForApproval SubmitCommand(TimeEntryId timeEntryId, string submissionId = "submission-1")
        => new(
            new TimeEntrySubmissionId(submissionId),
            [timeEntryId],
            TimeEntrySubmissionScope.SelectedEntries);

    private static TimeEntryState RecordedState(RecordTimeEntry command)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            command.TimeEntryId,
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            ActivityTypeScope.Tenant,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            TimeEntryApprovalState.Draft,
            command.ContributorCategory,
            command.AiMetrics)
        {
            Comment = command.Comment
        });
        return state;
    }

    private static TimeEntryState ApprovedState(RecordTimeEntry command)
    {
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryApproved(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            AllowedAuthority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry));
        return state;
    }

    private static TimeEntryState SubmittedState(RecordTimeEntry command)
    {
        TimeEntryState state = RecordedState(command);
        state.Apply(new TimeEntrySubmitted(
            command.TimeEntryId,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimeEntryState RejectedState(RecordTimeEntry command)
    {
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryRejected(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 15, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Rejected,
            AllowedAuthority(ApprovalAuthorityAction.EntryRejection),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence.")));
        return state;
    }

    private static TimeEntryState SupersededSubmittedState(RecordTimeEntry command)
    {
        TimeEntryState state = RejectedState(command);
        state.Apply(new TimeEntryCorrected(
            command.TimeEntryId,
            new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            CorrectionValues(command),
            CorrectionValues(command with { DurationMinutes = 75 }),
            new TimeEntryRejectionReason("Needs customer PO evidence."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Superseded));
        state.Apply(new TimeEntrySubmitted(
            command.TimeEntryId,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-2"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimeEntryCorrectionValues CorrectionValues(RecordTimeEntry command)
        => new(
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            command.ContributorCategory,
            command.AiMetrics)
        {
            Comment = command.Comment
        };

    private static ApprovalAuthoritySourceAttribution AllowedAuthority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "project-approval",
            "2026-06",
            ProjectionFreshnessMetadata.Fresh);

    private static AiEffortMetrics ProviderReportedMetrics(int billableEffortMinutes = 2)
        => new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            billableEffortMinutes,
            1000,
            250,
            1250,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
            AiTokenMetricAvailability.ProviderReported);

    private static TimeEntryComment Comment()
        => new(
            "Worked on project delivery.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault);

    private static TimeEntryComment InvalidComment()
        => new(
            "Worked on project delivery.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault with
            {
                InternalDisplay = Hexalith.Timesheets.Contracts.Policies.TimesheetsCommentPolicyDecision.Unknown
            });

    private static TEvent SingleSuccess<TEvent>(TimesheetsDomainResult result)
    {
        result.IsSuccess.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TEvent>();
    }

    private static TimesheetsRejection Rejection(TimesheetsDomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }
}
