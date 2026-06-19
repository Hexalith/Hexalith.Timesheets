using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.TimeEntries;

public static class TimeEntry
{
    public static TimesheetsDomainResult Handle(
        RecordTimeEntry command,
        TimeEntryState? state,
        ActivityTypeScope activityTypeScope)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<TimesheetsFieldError> errors = [];
        ValidateRequiredFields(command, activityTypeScope, errors);
        ValidateAiMetrics(command.AiMetrics, command.ContributorCategory, errors);
        ValidateComment(command.Comment, errors);

        if (state?.IsRecorded == true)
        {
            errors.Add(new("timeEntryId", "duplicate", "Time Entry ID already exists."));
        }

        if (errors.Count > 0)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Time Entry capture failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new TimeEntryRecorded(
                command.TimeEntryId,
                command.Target,
                command.Contributor,
                command.ActivityTypeId,
                activityTypeScope,
                command.ServiceDate,
                command.DurationMinutes,
                command.BillableState,
                TimeEntryApprovalState.Draft,
                command.ContributorCategory,
                command.AiMetrics)
            {
                Comment = command.Comment
            }
        ]);
    }

    public static TimesheetsDomainResult Handle(
        SubmitTimeEntriesForApproval command,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        PartyReference? submitter,
        TenantReference? tenant,
        DateTimeOffset submittedAtUtc,
        ActivityTypeScope activityTypeScope)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(timeEntryId);

        List<TimesheetsFieldError> errors = [];
        ValidateSubmission(command, timeEntryId, state, submitter, tenant, submittedAtUtc, activityTypeScope, errors);

        if (state?.ApprovalState == TimeEntryApprovalState.Submitted
            && state.TimeEntrySubmissionId == command.TimeEntrySubmissionId
            && errors.Count == 0)
        {
            return TimesheetsDomainResult.NoOp();
        }

        if (errors.Count > 0)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Time Entry submission failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new TimeEntrySubmitted(
                timeEntryId,
                submitter!,
                tenant!,
                submittedAtUtc.ToUniversalTime(),
                command.TimeEntrySubmissionId,
                command.SubmissionScope,
                TimeEntryApprovalState.Submitted)
        ]);
    }

    private static void ValidateRequiredFields(
        RecordTimeEntry command,
        ActivityTypeScope activityTypeScope,
        List<TimesheetsFieldError> errors)
    {
        if (command.TimeEntryId is null || string.IsNullOrWhiteSpace(command.TimeEntryId.Value))
        {
            errors.Add(new("timeEntryId", "required", "Time Entry ID is required."));
        }

        if (command.Target is null)
        {
            errors.Add(new("target", "required", "Target reference is required."));
        }
        else
        {
            if (command.Target.TargetKind is not (TimeEntryTargetKind.Project or TimeEntryTargetKind.Work))
            {
                errors.Add(new("target.targetKind", "invalid", "Target kind must be Project or Work."));
            }

            if (string.IsNullOrWhiteSpace(command.Target.TargetId))
            {
                errors.Add(new("target.targetId", "required", "Target ID is required."));
            }
        }

        if (command.Contributor is null || string.IsNullOrWhiteSpace(command.Contributor.PartyId))
        {
            errors.Add(new("contributor", "required", "Contributor Party reference is required."));
        }

        if (command.ActivityTypeId is null || string.IsNullOrWhiteSpace(command.ActivityTypeId.Value))
        {
            errors.Add(new("activityTypeId", "required", "Activity Type ID is required."));
        }

        if (activityTypeScope == ActivityTypeScope.Unknown)
        {
            errors.Add(new("activityTypeScope", "unknown", "Activity Type scope is required."));
        }

        if (command.DurationMinutes <= 0)
        {
            errors.Add(new("durationMinutes", "positive", "Duration must be a positive whole-minute value."));
        }

        if (command.BillableState == BillableState.Unknown)
        {
            errors.Add(new("billableState", "unknown", "Billable state is required."));
        }

        if (command.ContributorCategory == ContributorCategory.Unknown)
        {
            errors.Add(new("contributorCategory", "unknown", "Contributor category is required."));
        }
    }

    private static void ValidateAiMetrics(
        AiEffortMetrics? metrics,
        ContributorCategory contributorCategory,
        List<TimesheetsFieldError> errors)
    {
        if (metrics is null)
        {
            return;
        }

        if (metrics.Availability == AiMetricAvailability.Unknown)
        {
            errors.Add(new("aiMetrics.availability", "unknown", "AI metric availability is required when metrics are supplied."));
        }

        ValidateAiMetricSource(metrics, errors);
        ValidateAiTokenAvailability(metrics, errors);
        ValidateUnavailableMetricsCarryNoValues(metrics, errors);
        ValidateAiContributorCategory(metrics, contributorCategory, errors);

        AddNonNegativeError(metrics.WallClockDurationMilliseconds, "aiMetrics.wallClockDurationMilliseconds", errors);
        AddNonNegativeError(metrics.ModelRuntimeMilliseconds, "aiMetrics.modelRuntimeMilliseconds", errors);
        AddNonNegativeError(metrics.BillableEffortMinutes, "aiMetrics.billableEffortMinutes", errors);
        AddNonNegativeError(metrics.ProviderInputTokenCount, "aiMetrics.providerInputTokenCount", errors);
        AddNonNegativeError(metrics.ProviderOutputTokenCount, "aiMetrics.providerOutputTokenCount", errors);
        AddNonNegativeError(metrics.ProviderTotalTokenCount, "aiMetrics.providerTotalTokenCount", errors);
    }

    private static void ValidateAiMetricSource(AiEffortMetrics metrics, List<TimesheetsFieldError> errors)
    {
        AiEffortMetricSourceMetadata? source = metrics.Source;
        if (source is null)
        {
            errors.Add(new("aiMetrics.source", "required", "AI metric source metadata is required when metrics are supplied."));
            return;
        }

        if (source.SourceCategory == AiEffortMetricSourceCategory.Unknown)
        {
            errors.Add(new("aiMetrics.source.sourceCategory", "unknown", "AI metric source category is required when metrics are supplied."));
        }

        if (metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
            && source.SourceCategory == AiEffortMetricSourceCategory.Unavailable)
        {
            errors.Add(new("aiMetrics.source.sourceCategory", "required", "Reported or estimated AI metrics require source metadata."));
        }

        if (source.SourceCategory == AiEffortMetricSourceCategory.Provider
            && string.IsNullOrWhiteSpace(source.ProviderName))
        {
            errors.Add(new("aiMetrics.source.providerName", "required", "Provider source metadata requires a provider name."));
        }

        if (source.SourceCategory == AiEffortMetricSourceCategory.Tool
            && string.IsNullOrWhiteSpace(source.ToolName))
        {
            errors.Add(new("aiMetrics.source.toolName", "required", "Tool source metadata requires a tool name."));
        }

        if (source.SourceCategory == AiEffortMetricSourceCategory.WorkExecution
            && string.IsNullOrWhiteSpace(source.WorkExecutionId))
        {
            errors.Add(new("aiMetrics.source.workExecutionId", "required", "Work execution source metadata requires a work execution ID."));
        }
    }

    private static void ValidateAiTokenAvailability(AiEffortMetrics metrics, List<TimesheetsFieldError> errors)
    {
        if (metrics.TokenAvailability == AiTokenMetricAvailability.Unknown)
        {
            errors.Add(new("aiMetrics.tokenAvailability", "unknown", "AI token metric availability is required when metrics are supplied."));
            return;
        }

        bool anyTokenCountSupplied = metrics.ProviderInputTokenCount is not null
            || metrics.ProviderOutputTokenCount is not null
            || metrics.ProviderTotalTokenCount is not null;

        if (metrics.TokenAvailability is AiTokenMetricAvailability.NotReported or AiTokenMetricAvailability.Unavailable
            && anyTokenCountSupplied)
        {
            errors.Add(new("aiMetrics.providerTokenCounts", "must-be-null", "Provider token counts must be null when tokens were not reported."));
        }

        if (metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported
            && (metrics.ProviderInputTokenCount is null
                || metrics.ProviderOutputTokenCount is null
                || metrics.ProviderTotalTokenCount is null))
        {
            errors.Add(new("aiMetrics.providerTokenCounts", "required", "Provider-reported token metrics require input, output, and total counts."));
        }
    }

    private static void ValidateUnavailableMetricsCarryNoValues(AiEffortMetrics metrics, List<TimesheetsFieldError> errors)
    {
        if (metrics.Availability != AiMetricAvailability.Unavailable)
        {
            return;
        }

        bool anyValueSupplied = metrics.WallClockDurationMilliseconds is not null
            || metrics.ModelRuntimeMilliseconds is not null
            || metrics.BillableEffortMinutes is not null
            || metrics.ProviderInputTokenCount is not null
            || metrics.ProviderOutputTokenCount is not null
            || metrics.ProviderTotalTokenCount is not null;

        if (anyValueSupplied)
        {
            errors.Add(new("aiMetrics", "unavailable-values-must-be-null", "Unavailable AI metrics must not carry numeric values."));
        }
    }

    private static void ValidateAiContributorCategory(
        AiEffortMetrics metrics,
        ContributorCategory contributorCategory,
        List<TimesheetsFieldError> errors)
    {
        if (contributorCategory == ContributorCategory.AutomatedAgent)
        {
            return;
        }

        if (metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
            || metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported)
        {
            errors.Add(new("aiMetrics", "automated-agent-required", "Provider-reported or estimated AI metrics require an automated-agent contributor."));
        }
    }

    private static void AddNonNegativeError(long? value, string field, List<TimesheetsFieldError> errors)
    {
        if (value < 0)
        {
            errors.Add(new(field, "non-negative", "AI metric values must be non-negative when provided."));
        }
    }

    private static void ValidateComment(TimeEntryComment? comment, List<TimesheetsFieldError> errors)
    {
        if (comment is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(comment.Text))
        {
            errors.Add(new("comment.text", "blank", "Comment text cannot be blank when supplied."));
        }

        if (comment.Text.Length > TimeEntryComment.MaxLength)
        {
            errors.Add(new("comment.text", "too-long", "Comment text exceeds the maximum supported length."));
        }

        TimeEntryCommentPolicy? policy = comment.Policy;
        if (policy is null)
        {
            errors.Add(new("comment.policy", "required", "Comment policy is required when a comment is supplied."));
            return;
        }

        if (policy.InternalDisplay == TimesheetsCommentPolicyDecision.Unknown
            || policy.ExternalConfirmationDisplay == TimesheetsCommentPolicyDecision.Unknown
            || policy.ProjectionInclusion == TimesheetsCommentPolicyDecision.Unknown
            || policy.ExportInclusion == TimesheetsCommentPolicyDecision.Unknown
            || policy.SupportDiagnostics == TimesheetsCommentPolicyDecision.Unknown)
        {
            errors.Add(new("comment.policy", "unknown", "Comment policy decisions must be explicit."));
        }

        if (policy.RedactionRequirement == TimesheetsCommentRedactionRequirement.Unknown)
        {
            errors.Add(new("comment.policy.redactionRequirement", "unknown", "Comment redaction requirement must be explicit."));
        }

        if (policy.RetentionCategory == TimesheetsEvidenceRetentionCategory.Unknown)
        {
            errors.Add(new("comment.policy.retentionCategory", "unknown", "Comment retention category must be explicit."));
        }
    }

    private static void ValidateSubmission(
        SubmitTimeEntriesForApproval command,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        PartyReference? submitter,
        TenantReference? tenant,
        DateTimeOffset submittedAtUtc,
        ActivityTypeScope activityTypeScope,
        List<TimesheetsFieldError> errors)
    {
        string prefix = EntryFieldPrefix(timeEntryId);

        if (command.TimeEntrySubmissionId is null || string.IsNullOrWhiteSpace(command.TimeEntrySubmissionId.Value))
        {
            errors.Add(new("timeEntrySubmissionId", "required", "Submission ID is required."));
        }

        if (command.TimeEntryIds is null || command.TimeEntryIds.Count == 0)
        {
            errors.Add(new("timeEntryIds", "required", "At least one Time Entry ID is required."));
        }
        else if (!command.TimeEntryIds.Contains(timeEntryId))
        {
            errors.Add(new($"{prefix}.timeEntryId", "not-in-submission", "Time Entry is not included in the submission command."));
        }

        if (command.SubmissionScope == TimeEntrySubmissionScope.Unknown)
        {
            errors.Add(new("submissionScope", "unknown", "Submission scope is required."));
        }

        if (submitter is null || string.IsNullOrWhiteSpace(submitter.PartyId))
        {
            errors.Add(new("submitter", "required", "Submitter Party reference is required."));
        }

        if (tenant is null || string.IsNullOrWhiteSpace(tenant.TenantId))
        {
            errors.Add(new("tenant", "required", "Tenant reference is required."));
        }

        if (submittedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new("submittedAtUtc", "utc-required", "Submission timestamp must be a UTC instant."));
        }

        if (state?.IsRecorded != true)
        {
            errors.Add(new($"{prefix}.timeEntryId", "not-recorded", "Time Entry must be recorded before submission."));
            return;
        }

        if (state.ApprovalState != TimeEntryApprovalState.Draft)
        {
            if (state.ApprovalState == TimeEntryApprovalState.Submitted
                && state.TimeEntrySubmissionId == command.TimeEntrySubmissionId)
            {
                return;
            }

            errors.Add(new($"{prefix}.approvalState", "invalid-transition", "Only Draft Time Entries can be submitted."));
        }

        ValidateRecordedFacts(state, activityTypeScope, prefix, errors);
        ValidateComment(state.Comment, errors);
    }

    private static void ValidateRecordedFacts(
        TimeEntryState state,
        ActivityTypeScope activityTypeScope,
        string prefix,
        List<TimesheetsFieldError> errors)
    {
        if (state.TimeEntryId is null || string.IsNullOrWhiteSpace(state.TimeEntryId.Value))
        {
            errors.Add(new($"{prefix}.timeEntryId", "required", "Time Entry ID is required."));
        }

        if (state.Target is null)
        {
            errors.Add(new($"{prefix}.target", "required", "Target reference is required."));
        }

        if (state.Contributor is null || string.IsNullOrWhiteSpace(state.Contributor.PartyId))
        {
            errors.Add(new($"{prefix}.contributor", "required", "Contributor Party reference is required."));
        }

        if (state.ActivityTypeId is null || string.IsNullOrWhiteSpace(state.ActivityTypeId.Value))
        {
            errors.Add(new($"{prefix}.activityTypeId", "required", "Activity Type ID is required."));
        }

        if (activityTypeScope == ActivityTypeScope.Unknown)
        {
            errors.Add(new($"{prefix}.activityTypeScope", "unknown", "Activity Type scope is required."));
        }

        if (state.DurationMinutes <= 0)
        {
            errors.Add(new($"{prefix}.durationMinutes", "positive", "Duration must be a positive whole-minute value."));
        }

        if (state.BillableState == BillableState.Unknown)
        {
            errors.Add(new($"{prefix}.billableState", "unknown", "Billable state is required."));
        }

        if (state.ContributorCategory == ContributorCategory.Unknown)
        {
            errors.Add(new($"{prefix}.contributorCategory", "unknown", "Contributor category is required."));
        }
    }

    private static string EntryFieldPrefix(TimeEntryId timeEntryId)
        => $"entries[{timeEntryId.Value}]";

    private static TimesheetsDomainResult Reject(
        TimesheetsRejectionCode code,
        string message,
        IReadOnlyList<TimesheetsFieldError> errors)
        => TimesheetsDomainResult.Rejection([
            new(code, message, errors)
        ]);
}
