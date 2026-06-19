using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
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
        ValidateAiMetrics(command.AiMetrics, errors);
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

    private static void ValidateAiMetrics(AiEffortMetrics? metrics, List<TimesheetsFieldError> errors)
    {
        if (metrics is null)
        {
            return;
        }

        if (metrics.Availability == AiMetricAvailability.Unknown)
        {
            errors.Add(new("aiMetrics.availability", "unknown", "AI metric availability is required when metrics are supplied."));
        }

        AddNonNegativeError(metrics.WallClockDurationMilliseconds, "aiMetrics.wallClockDurationMilliseconds", errors);
        AddNonNegativeError(metrics.ModelRuntimeMilliseconds, "aiMetrics.modelRuntimeMilliseconds", errors);
        AddNonNegativeError(metrics.BillableEffortMinutes, "aiMetrics.billableEffortMinutes", errors);
        AddNonNegativeError(metrics.ProviderInputTokenCount, "aiMetrics.providerInputTokenCount", errors);
        AddNonNegativeError(metrics.ProviderOutputTokenCount, "aiMetrics.providerOutputTokenCount", errors);
        AddNonNegativeError(metrics.ProviderTotalTokenCount, "aiMetrics.providerTotalTokenCount", errors);
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

    private static TimesheetsDomainResult Reject(
        TimesheetsRejectionCode code,
        string message,
        IReadOnlyList<TimesheetsFieldError> errors)
        => TimesheetsDomainResult.Rejection([
            new(code, message, errors)
        ]);
}
