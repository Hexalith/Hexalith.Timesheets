using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts;

public static class TimesheetsMetadataCatalog
{
    public static IReadOnlyList<TimesheetsMetadataDescriptor> Descriptors { get; } =
    [
        new(
            "timesheets.command.record-time",
            "Record time",
            "capture",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("serviceDate", "Service date", "DateOnly", true),
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("contributorCategory", "Contributor category", nameof(ContributorCategory), true),
                new("aiMetrics", "AI effort metrics", nameof(AiEffortMetrics), false, "AI metrics keep runtime, effort, and token units explicit."),
                new("aiWallClockDurationMilliseconds", "AI wall-clock execution", "Milliseconds", false, "AI wall-clock execution time is recorded in milliseconds and kept separate from Duration minutes."),
                new("aiModelRuntimeMilliseconds", "AI model/tool runtime", "Milliseconds", false, "AI model or tool runtime is recorded in milliseconds."),
                new("aiBillableEffortMinutes", "AI billable effort", "WholeMinutes", false, "AI billable effort is recorded in minutes without converting token counts or runtime."),
                new("aiTokenAvailability", "AI token availability", nameof(AiTokenMetricAvailability), false, "Provider token counts use explicit text availability; unavailable counts stay null."),
                new("aiMetricSource", "AI metric source", nameof(AiEffortMetricSourceMetadata), false, "Compact audit source metadata without prompts, responses, secrets, request bodies, or personal data."),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Comments may be excluded by policy.")
            ],
            [
                new("record-time", "Record time", "Timesheets.RecordTime"),
                new("record-project-time", "Record project time", "Timesheets.RecordProjectTime"),
                new("record-work-time", "Record work time", "Timesheets.RecordWorkTime")
            ],
            [
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory)),
                new("ai-metrics", "AI metric availability", nameof(AiMetricAvailability)),
                new("ai-tokens", "AI token availability", nameof(AiTokenMetricAvailability)),
                new("ai-source", "AI metric source category", nameof(AiEffortMetricSourceCategory)),
                new("comment-retention", "Comment retention", nameof(TimesheetsEvidenceRetentionCategory))
            ]),
        new(
            "timesheets.command.submit-time-entries",
            "Submit entries",
            "submission",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("timeEntryIds", "Time Entries", "TimeEntryId[]", true, "Selected Draft entries remain visible before submission."),
                new("timeEntrySubmissionId", "Submission ID", nameof(TimeEntrySubmissionId), true),
                new("submissionScope", "Submission scope", nameof(TimeEntrySubmissionScope), true),
                new("approvalState", "Approval state", nameof(TimeEntryApprovalState), true, "Draft entries become Submitted when accepted."),
                new("blockingFields", "Blocking fields", "TimesheetsFieldError[]", false, "Invalid entries show correction fields without hiding selected entries."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true),
                new("partialSubmission", "Partial submission", "Boolean", false, "Valid entries and blocked entries are reported distinctly."),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Blocking policy and freshness messages persist across interrupted commands.")
            ],
            [
                new("submit-time-entries", "Submit entries", "Timesheets.SubmitTimeEntriesForApproval"),
                new("submit-for-approval", "Submit for approval", "Timesheets.SubmitForApproval")
            ],
            [
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.command.external-contribution",
            "External contributor",
            "external-contribution",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("source", "External source", nameof(ExternalContributionSource), true, "Safe source system and idempotency reference only."),
                new("timeEntry", "Time Entry", nameof(TimeEntryId), true),
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("serviceDate", "Service date", "DateOnly", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("confirmation", "Confirmation recorded", nameof(TimeEntryContributorConfirmationEvidence), false, "Contributor confirmation is evidence, not approval."),
                new("approvalState", "Approval state", nameof(TimeEntryApprovalState), true, "Approval still follows the configured Timesheets workflow."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true)
            ],
            [
                new("submit-external-time", "Submit external time", "Timesheets.SubmitExternalTimeEntry"),
                new("confirm-time", "Confirm time", "Timesheets.ConfirmExternalTimeEntry")
            ],
            [
                new("external-contributor", "External contributor", nameof(ContributorCategory)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.command.submit-period",
            "Submit period",
            "submission",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("timesheetPeriodId", "Timesheet Period", nameof(TimesheetPeriodId), true),
                new("contributor", "Contributor", "PartyReference", true),
                new("periodKind", "Period kind", nameof(TimesheetPeriodKind), true),
                new("localAnchorDate", "Local period date", "DateOnly", true, "Tenant-local period key is resolved from tenant policy."),
                new("timeEntryIds", "Time Entries", "TimeEntryId[]", true, "Draft and already Submitted entries remain visible before submission."),
                new("tenantTimeZoneId", "Tenant time-zone", "String", true, "Displayed from tenant policy; not accepted as caller authority."),
                new("periodState", "Period state", nameof(TimesheetPeriodApprovalState), true, "Period state stays separate from entry Approval State."),
                new("blockingEntryGuidance", "Blocking-entry guidance", nameof(TimesheetPeriodBlockingEntryGuidance), false, "Entry needs correction."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Blocking policy and freshness messages persist across interrupted commands.")
            ],
            [
                new("submit-period", "Submit period", "Timesheets.SubmitTimesheetPeriod"),
                new("submit-time-entries", "Submit entries", "Timesheets.SubmitTimeEntriesForApproval")
            ],
            [
                new("period", "Period", nameof(TimesheetPeriodApprovalState)),
                new("period-kind", "Period kind", nameof(TimesheetPeriodKind)),
                new("approval", "Entry approval", nameof(TimeEntryApprovalState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.command.correct-rejected-time-entry",
            "Correct entry",
            "correction",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("timeEntry", "Time Entry", "TimeEntryId", true),
                new("timeEntryCorrectionId", "Correction ID", nameof(TimeEntryCorrectionId), true),
                new("priorValues", "Prior values", nameof(TimeEntryCorrectionValues), true, "Prior rejected facts remain visible where authorized."),
                new("correctedValues", "Corrected values", nameof(TimeEntryCorrectionValues), true, "Corrected facts are submitted as a correction command."),
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("serviceDate", "Service date", "DateOnly", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true, "Corrected duration supersedes the rejected value after acceptance."),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("contributorCategory", "Contributor category", nameof(ContributorCategory), true),
                new("aiMetrics", "AI effort metrics", nameof(AiEffortMetrics), false),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Correction comments are additive evidence."),
                new("rejectionReason", "Rejection reason", nameof(TimeEntryRejectionReason), true, "Rejection reason remains visible where authorized."),
                new("correctionState", "Correction state", nameof(TimeEntryCorrectionState), true),
                new("fieldValidation", "Field validation", "TimesheetsFieldError[]", false),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Policy, freshness, and permission messages persist across interrupted correction attempts.")
            ],
            [
                new("correct-entry", "Correct entry", "Timesheets.CorrectRejectedTimeEntry"),
                new("submit-corrected-entry", "Submit corrected entry", "Timesheets.SubmitTimeEntriesForApproval")
            ],
            [
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory))
            ]),
        new(
            "timesheets.command.correct-approved-time-entry",
            "Add correction",
            "correction",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("timeEntry", "Time Entry", "TimeEntryId", true),
                new("timeEntryCorrectionId", "Correction ID", nameof(TimeEntryCorrectionId), true),
                new("priorValues", "Prior values", nameof(TimeEntryCorrectionValues), true, "Prior approved facts remain visible where authorized."),
                new("correctedValues", "Corrected values", nameof(TimeEntryCorrectionValues), true, "Corrected facts are appended as correction evidence."),
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("serviceDate", "Service date", "DateOnly", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true, "Corrected duration becomes the effective value without editing approved history."),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("contributorCategory", "Contributor category", nameof(ContributorCategory), true),
                new("aiMetrics", "AI effort metrics", nameof(AiEffortMetrics), false),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Correction comments are additive evidence."),
                new("reason", "Correction reason", nameof(TimeEntryCorrectionReason), true, "Correction reason is audit evidence and must remain policy-safe."),
                new("sourceApprovalDecisionId", "Source approval decision", nameof(TimeEntryApprovalDecisionId), true),
                new("approvalState", "Approval state", nameof(TimeEntryApprovalState), true),
                new("correctionState", "Correction state", nameof(TimeEntryCorrectionState), true),
                new("lockState", "Lock state", nameof(TimeEntryLockState), true, "Corrected approved entries remain locked from direct edits."),
                new("fieldValidation", "Field validation", "TimesheetsFieldError[]", false),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Authority, freshness, and correction policy messages persist across interrupted correction attempts.")
            ],
            [
                new("add-correction", "Add correction", "Timesheets.CorrectApprovedTimeEntry")
            ],
            [
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("lock", "Lock", nameof(TimeEntryLockState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory))
            ]),
        new(
            "timesheets.command.activity-type-catalog",
            "Activity Type Catalog",
            "catalog",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("project", "Project reference", "ProjectReference", false),
                new("label", "Label", "String", true),
                new("scope", "Scope", nameof(ActivityTypeScope), true),
                new("billableDefault", "Billable default", nameof(BillableState), false)
            ],
            [
                new("create-tenant-activity-type", "Create tenant Activity Type", "Timesheets.CreateTenantActivityType"),
                new("create-project-activity-type", "Create project Activity Type", "Timesheets.CreateProjectActivityType"),
                new("rename-activity-type", "Rename Activity Type", "Timesheets.RenameActivityType"),
                new("update-billable-default", "Update billable default", "Timesheets.UpdateActivityTypeMetadata"),
                new("deactivate-activity-type", "Deactivate Activity Type", "Timesheets.DeactivateActivityType"),
                new("reactivate-activity-type", "Reactivate Activity Type", "Timesheets.ReactivateActivityType"),
                new("configure-project-catalog-restriction", "Restrict project Activity Type selection", "Timesheets.ConfigureProjectActivityTypeCatalogRestriction")
            ],
            [
                new("activity-type-scope", "Scope", nameof(ActivityTypeScope)),
                new("active-state", "Active state", nameof(ActivityTypeActiveState))
            ]),
        new(
            "timesheets.projection.activity-type-catalog",
            "Activity Type Catalog",
            "catalog",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("projectFilter", "Project filter", "ProjectReference", false, "Preserved when drilling into project-scoped catalog entries."),
                new("project", "Project reference", "ProjectReference", false),
                new("label", "Label", "String", true),
                new("scope", "Scope", nameof(ActivityTypeScope), true),
                new("activeState", "Active state", nameof(ActivityTypeActiveState), true),
                new("statusText", "Status text", "String", true, "Active and inactive state is shown as text."),
                new("billableDefault", "Billable default", nameof(BillableState), false),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true)
            ],
            [
                new("create-tenant-activity-type", "Create tenant Activity Type", "Timesheets.CreateTenantActivityType"),
                new("create-project-activity-type", "Create project Activity Type", "Timesheets.CreateProjectActivityType"),
                new("rename-activity-type", "Rename Activity Type", "Timesheets.RenameActivityType"),
                new("update-billable-default", "Update billable default", "Timesheets.UpdateActivityTypeMetadata"),
                new("deactivate-activity-type", "Deactivate Activity Type", "Timesheets.DeactivateActivityType"),
                new("reactivate-activity-type", "Reactivate Activity Type", "Timesheets.ReactivateActivityType"),
                new("configure-project-catalog-restriction", "Restrict project Activity Type selection", "Timesheets.ConfigureProjectActivityTypeCatalogRestriction")
            ],
            [
                new("active-state", "Active state", nameof(ActivityTypeActiveState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.projection.time-entry-evidence",
            "Time Entry Evidence",
            "evidence",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("timeEntry", "Time Entry", "TimeEntryId", true),
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("serviceDate", "Service date", "DateOnly", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("approvalState", "Approval state", nameof(TimeEntryApprovalState), true),
                new("contributorCategory", "Contributor category", nameof(ContributorCategory), true),
                new("sourceAuthority", "Source authority", nameof(TimeEntryEvidenceSourceAuthority), true, "Timesheets domain events are the evidence source."),
                new("eventLineage", "Event lineage", nameof(TimeEntryEventLineageItem), true, "Safe event summaries for audit metadata."),
                new("approvalDecision", "Approval decision", nameof(TimeEntryApprovalDecisionEvidence), false, "Stable approval/rejection evidence for later locking, correction, and ledger projections."),
                new("approvalDecisionId", "Approval decision ID", nameof(TimeEntryApprovalDecisionId), false),
                new("approvalScope", "Approval scope", nameof(TimeEntryApprovalScope), false),
                new("approver", "Approver", "PartyReference", false),
                new("decidedAtUtc", "Decision time", "DateTimeOffset", false),
                new("lockEvidence", "Lock evidence", nameof(TimeEntryLockEvidence), true, "Direct-edit lock state derived from Time Entry domain events."),
                new("lockState", "Lock state", nameof(TimeEntryLockState), true, "Approved entries are locked from direct edits."),
                new("rejectionReason", "Rejection reason", nameof(TimeEntryRejectionReason), false, "Required rejection reason preserved for correction workflow."),
                new("authoritySource", "Authority source", nameof(ApprovalAuthoritySource), false, "Stable source attribution for the approval decision."),
                new("authorityFreshness", "Authority freshness", nameof(ProjectionFreshnessState), false),
                new("displayHydration", "Display hydration", nameof(TimeEntryDisplayHydration), true, "Read-time labels keep explicit hydration state."),
                new("correction", "Correction evidence", nameof(TimeEntryCorrectionEvidence), false, "Original and corrected values are shown as additive lineage."),
                new("approvedCorrection", "Approved correction evidence", nameof(TimeEntryApprovedCorrectionEvidence), false, "Approved correction lineage is shown separately from rejected correction lineage."),
                new("correctionReason", "Correction reason", nameof(TimeEntryCorrectionReason), false, "Approved correction reason is visible only on authorized evidence surfaces."),
                new("aiMetrics", "AI effort metrics", nameof(AiEffortMetrics), false),
                new("aiWallClockDurationMilliseconds", "AI wall-clock execution", "Milliseconds", false, "AI wall-clock execution time is displayed separately from human/external duration."),
                new("aiModelRuntimeMilliseconds", "AI model/tool runtime", "Milliseconds", false, "AI model or tool runtime is displayed in milliseconds."),
                new("aiBillableEffortMinutes", "AI billable effort", "WholeMinutes", false, "AI billable effort is displayed in minutes without token-to-hours conversion."),
                new("aiTokenAvailability", "AI token availability", nameof(AiTokenMetricAvailability), false, "Unavailable provider token metrics are shown with explicit text, not zero or silence."),
                new("aiMetricSource", "AI metric source", nameof(AiEffortMetricSourceMetadata), false, "Compact audit source metadata for provider, tool, or work execution context."),
                new("correctionState", "Correction state", nameof(TimeEntryCorrectionState), true),
                new("priorValues", "Prior values", nameof(TimeEntryCorrectionValues), false),
                new("correctedValues", "Corrected values", nameof(TimeEntryCorrectionValues), false),
                new("fieldValidation", "Field validation", "TimesheetsFieldError[]", false),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Freshness and correction policy messages persist."),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Comments may contain sensitive information."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true)
            ],
            [
                new("correct-entry", "Correct entry", "Timesheets.CorrectRejectedTimeEntry"),
                new("add-correction", "Add correction", "Timesheets.CorrectApprovedTimeEntry"),
                new("review-export-policy", "Review export policy", "Timesheets.ReviewExportPolicy")
            ],
            [
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState)),
                new("source-authority", "Source authority", nameof(TimeEntryEvidenceSourceAuthority)),
                new("hydration-state", "Hydration state", nameof(DisplayHydrationState)),
                new("authority-decision", "Authority decision", nameof(ApprovalAuthorityDecisionState)),
                new("authority-source", "Authority source", nameof(ApprovalAuthoritySource)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("lock", "Lock", nameof(TimeEntryLockState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("ai-metrics", "AI metric availability", nameof(AiMetricAvailability)),
                new("ai-tokens", "AI token availability", nameof(AiTokenMetricAvailability)),
                new("ai-source", "AI metric source category", nameof(AiEffortMetricSourceCategory)),
                new("comment-export", "Comment export", nameof(TimesheetsCommentPolicyDecision))
            ]),
        new(
            "timesheets.projection.my-timesheet-period",
            "My Timesheet Period",
            "submission",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("timesheetPeriodId", "Timesheet Period", nameof(TimesheetPeriodId), true),
                new("periodKind", "Period kind", nameof(TimesheetPeriodKind), true),
                new("periodState", "Period state", nameof(TimesheetPeriodApprovalState), true, "Period state stays separate from entry Approval State."),
                new("tenantTimeZoneId", "Tenant time-zone", "String", true),
                new("localBoundary", "Local boundary", nameof(TenantLocalPeriodBoundary), true),
                new("includedEntryCount", "Included entries", "Integer", true),
                new("timeEntries", "TimeEntry evidence", "TimeEntryEvidenceReadModel[]", true),
                new("entrySummaries", "Entry summaries", nameof(TimesheetPeriodEntrySummary), true, "Entry badges stay separate from period state."),
                new("blockingEntryGuidance", "Blocking-entry guidance", nameof(TimesheetPeriodBlockingEntryGuidance), false, "Entry needs correction."),
                new("rejectionReason", "Rejection reason", nameof(TimeEntryRejectionReason), false),
                new("approvedCorrection", "Approved correction evidence", nameof(TimeEntryApprovedCorrectionEvidence), false),
                new("correctionReason", "Correction reason", nameof(TimeEntryCorrectionReason), false),
                new("priorValues", "Prior values", nameof(TimeEntryCorrectionValues), false),
                new("correctedValues", "Corrected values", nameof(TimeEntryCorrectionValues), false),
                new("correctionState", "Correction state", nameof(TimeEntryCorrectionState), true),
                new("lockEvidence", "Lock evidence", nameof(TimeEntryLockEvidence), true, "Entry lock state is display evidence only; write authority remains the aggregate state."),
                new("lockState", "Lock state", nameof(TimeEntryLockState), true),
                new("fieldValidation", "Field validation", "TimesheetsFieldError[]", false),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true),
                new("persistentMessageBarState", "Persistent message-bar state", "String", false, "Period correction and freshness blockers remain visible.")
            ],
            [
                new("submit-period", "Submit period", "Timesheets.SubmitTimesheetPeriod"),
                new("correct-entry", "Correct entry", "Timesheets.CorrectRejectedTimeEntry"),
                new("add-correction", "Add correction", "Timesheets.CorrectApprovedTimeEntry"),
                new("submit-time-entries", "Submit entries", "Timesheets.SubmitTimeEntriesForApproval")
            ],
            [
                new("period", "Period", nameof(TimesheetPeriodApprovalState)),
                new("period-kind", "Period kind", nameof(TimesheetPeriodKind)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("lock", "Lock", nameof(TimeEntryLockState)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.projection.period-approval-detail",
            "Period Approval Detail",
            "approval",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("timesheetPeriodId", "Timesheet Period", nameof(TimesheetPeriodId), true),
                new("periodState", "Period state", nameof(TimesheetPeriodApprovalState), true, "Period state stays separate from entry Approval State."),
                new("entrySummaries", "Entry summaries", nameof(TimesheetPeriodEntrySummary), true, "Entry rows show entry states separately from the period decision."),
                new("affectedEntryIds", "Affected entries", "TimeEntryId[]", false, "Affected entry ids remain grouped decision evidence."),
                new("periodDecision", "Period decision evidence", nameof(TimesheetPeriodApprovalDecisionEvidence), false, "Grouped approval or rejection decision evidence."),
                new("rejectedEntries", "Rejected entries", nameof(TimesheetPeriodSelectedEntryRejectionEvidence), false, "Selected rejected entries carry required reasons."),
                new("rejectionReason", "Rejection reason", nameof(TimesheetPeriodRejectionReason), false, "Period rejection reason is separate from entry rejection reasons."),
                new("authorityDecision", "Authority decision", nameof(ApprovalAuthorityDecisionState), true, "Authority cannot be resolved."),
                new("authoritySource", "Authority source", nameof(ApprovalAuthoritySource), false),
                new("authorityFreshness", "Authority freshness", nameof(ProjectionFreshnessState), true),
                new("lockEvidence", "Lock evidence", nameof(TimeEntryLockEvidence), true, "Entry lock state is derived from Time Entry approval events."),
                new("lockState", "Lock state", nameof(TimeEntryLockState), true),
                new("blockingEntryGuidance", "Blocking-entry guidance", nameof(TimesheetPeriodBlockingEntryGuidance), false, "Entry needs correction."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true, "Projection is rebuilding."),
                new("persistentMessageBarState", "Persistent message-bar state", "String", true, "Authority cannot be resolved.")
            ],
            [
                new("approve-period", "Approve period", "Timesheets.ApprovePeriod"),
                new("reject-period", "Reject period", "Timesheets.RejectPeriod"),
                new("approve-entry", "Approve entry", "Timesheets.ApproveEntry"),
                new("reject-entry", "Reject entry", "Timesheets.RejectEntry"),
                new("correct-entry", "Correct entry", "Timesheets.CorrectRejectedTimeEntry")
            ],
            [
                new("period", "Period", nameof(TimesheetPeriodApprovalState)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("lock", "Lock", nameof(TimeEntryLockState)),
                new("authority-decision", "Authority decision", nameof(ApprovalAuthorityDecisionState)),
                new("authority-freshness", "Authority freshness", nameof(ProjectionFreshnessState)),
                new("authority-source", "Authority source", nameof(ApprovalAuthoritySource)),
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState))
            ]),
        new(
            "timesheets.approvals.queue",
            "Approvals Queue",
            "approval",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("timeEntry", "Time Entry", "TimeEntryId", false),
                new("period", "Period", "String", false),
                new("approvalState", "Approval state", nameof(TimeEntryApprovalState), true),
                new("lockEvidence", "Lock evidence", nameof(TimeEntryLockEvidence), false, "Approved entries show direct-edit lock evidence."),
                new("lockState", "Lock state", nameof(TimeEntryLockState), false),
                new("approvedCorrection", "Approved correction evidence", nameof(TimeEntryApprovedCorrectionEvidence), false),
                new("correctionReason", "Correction reason", nameof(TimeEntryCorrectionReason), false),
                new("authorityDecision", "Authority decision", nameof(ApprovalAuthorityDecisionState), true),
                new("authoritySource", "Authority source", nameof(ApprovalAuthoritySource), true, "Stable source attribution for the policy decision."),
                new("authorityFreshness", "Authority freshness", nameof(ProjectionFreshnessState), true),
                new("blockingState", "Blocking state", "String", true, "Authority cannot be resolved."),
                new("persistentMessageBarState", "Persistent message-bar state", "String", true, "Authority cannot be resolved.")
            ],
            [
                new("approve-entry", "Approve entry", "Timesheets.ApproveEntry"),
                new("reject-entry", "Reject entry", "Timesheets.RejectEntry"),
                new("add-correction", "Add correction", "Timesheets.CorrectApprovedTimeEntry"),
                new("approve-period", "Approve period", "Timesheets.ApprovePeriod"),
                new("reject-period", "Reject period", "Timesheets.RejectPeriod")
            ],
            [
                new("authority-decision", "Authority decision", nameof(ApprovalAuthorityDecisionState)),
                new("authority-freshness", "Authority freshness", nameof(ProjectionFreshnessState)),
                new("authority-source", "Authority source", nameof(ApprovalAuthoritySource)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("lock", "Lock", nameof(TimeEntryLockState))
            ]),
        new(
            "timesheets.command.time-entry-approval",
            "Time Entry Approval",
            "approval",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("timeEntry", "Time Entry", "TimeEntryId", true),
                new("approvalAction", "Approval action", nameof(ApprovalAuthorityAction), true),
                new("authorityDecision", "Authority decision", nameof(ApprovalAuthorityDecisionState), true),
                new("authoritySource", "Authority source", nameof(ApprovalAuthoritySource), false),
                new("authorityFreshness", "Authority freshness", nameof(ProjectionFreshnessState), true),
                new("blockingState", "Blocking state", "String", false, "Authority cannot be resolved.")
            ],
            [
                new("approve-entry", "Approve entry", "Timesheets.ApproveEntry"),
                new("reject-entry", "Reject entry", "Timesheets.RejectEntry")
            ],
            [
                new("authority-decision", "Authority decision", nameof(ApprovalAuthorityDecisionState)),
                new("authority-freshness", "Authority freshness", nameof(ProjectionFreshnessState)),
                new("authority-source", "Authority source", nameof(ApprovalAuthoritySource))
            ]),
        new(
            "timesheets.command.period-approval",
            "Period Approval",
            "approval",
            TimesheetsSurfaceKind.Command,
            TimesheetsCompositionPattern.FrontComposerGeneratedForm,
            [
                new("period", "Period", "String", true),
                new("timesheetPeriodId", "Timesheet Period", nameof(TimesheetPeriodId), true),
                new("periodState", "Period state", nameof(TimesheetPeriodApprovalState), true, "Period state stays separate from entry Approval State."),
                new("affectedEntryIds", "Affected entries", "TimeEntryId[]", false),
                new("rejectedEntries", "Rejected entries", nameof(TimesheetPeriodSelectedEntryRejectionEvidence), false, "Entry needs correction."),
                new("rejectionReason", "Rejection reason", nameof(TimesheetPeriodRejectionReason), false),
                new("lockState", "Lock state", nameof(TimeEntryLockState), false),
                new("approvalAction", "Approval action", nameof(ApprovalAuthorityAction), true),
                new("authorityDecision", "Authority decision", nameof(ApprovalAuthorityDecisionState), true),
                new("authoritySource", "Authority source", nameof(ApprovalAuthoritySource), false),
                new("authorityFreshness", "Authority freshness", nameof(ProjectionFreshnessState), true),
                new("blockingState", "Blocking state", "String", false, "Authority cannot be resolved.")
            ],
            [
                new("approve-period", "Approve period", "Timesheets.ApprovePeriod"),
                new("reject-period", "Reject period", "Timesheets.RejectPeriod")
            ],
            [
                new("authority-decision", "Authority decision", nameof(ApprovalAuthorityDecisionState)),
                new("authority-freshness", "Authority freshness", nameof(ProjectionFreshnessState)),
                new("authority-source", "Authority source", nameof(ApprovalAuthoritySource))
            ]),
        new(
            "timesheets.review.export-policy",
            "Export policy review",
            "evidence",
            TimesheetsSurfaceKind.Projection,
            TimesheetsCompositionPattern.FrontComposerProjectionView,
            [
                new("retentionCategory", "Retention category", nameof(TimesheetsEvidenceRetentionCategory), true),
                new("commentExport", "Comment export", nameof(TimesheetsCommentPolicyDecision), true, "Export comments only when policy allows it."),
                new("diagnostics", "Diagnostics", nameof(TimesheetsCommentPolicyDecision), true, "Comments are excluded from diagnostics."),
                new("lockState", "Lock state", nameof(TimeEntryLockState), false, "Future ledger and report surfaces display approved-entry lock state as read evidence.")
            ],
            [],
            [
                new("retention-posture", "Retention posture", nameof(TimesheetsRetentionPosture)),
                new("comment-policy", "Comment policy", nameof(TimesheetsCommentPolicyDecision)),
                new("lock", "Lock", nameof(TimeEntryLockState))
            ])
    ];
}
