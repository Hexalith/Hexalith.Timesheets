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
                new("displayHydration", "Display hydration", nameof(TimeEntryDisplayHydration), true, "Read-time labels keep explicit hydration state."),
                new("aiMetrics", "AI effort metrics", nameof(AiEffortMetrics), false),
                new("aiWallClockDurationMilliseconds", "AI wall-clock execution", "Milliseconds", false, "AI wall-clock execution time is displayed separately from human/external duration."),
                new("aiModelRuntimeMilliseconds", "AI model/tool runtime", "Milliseconds", false, "AI model or tool runtime is displayed in milliseconds."),
                new("aiBillableEffortMinutes", "AI billable effort", "WholeMinutes", false, "AI billable effort is displayed in minutes without token-to-hours conversion."),
                new("aiTokenAvailability", "AI token availability", nameof(AiTokenMetricAvailability), false, "Unavailable provider token metrics are shown with explicit text, not zero or silence."),
                new("aiMetricSource", "AI metric source", nameof(AiEffortMetricSourceMetadata), false, "Compact audit source metadata for provider, tool, or work execution context."),
                new("correctionState", "Correction state", nameof(TimeEntryCorrectionState), true),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Comments may contain sensitive information."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true)
            ],
            [
                new("review-export-policy", "Review export policy", "Timesheets.ReviewExportPolicy")
            ],
            [
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState)),
                new("source-authority", "Source authority", nameof(TimeEntryEvidenceSourceAuthority)),
                new("hydration-state", "Hydration state", nameof(DisplayHydrationState)),
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("ai-metrics", "AI metric availability", nameof(AiMetricAvailability)),
                new("ai-tokens", "AI token availability", nameof(AiTokenMetricAvailability)),
                new("ai-source", "AI metric source category", nameof(AiEffortMetricSourceCategory)),
                new("comment-export", "Comment export", nameof(TimesheetsCommentPolicyDecision))
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
                new("diagnostics", "Diagnostics", nameof(TimesheetsCommentPolicyDecision), true, "Comments are excluded from diagnostics.")
            ],
            [],
            [
                new("retention-posture", "Retention posture", nameof(TimesheetsRetentionPosture)),
                new("comment-policy", "Comment policy", nameof(TimesheetsCommentPolicyDecision))
            ])
    ];
}
