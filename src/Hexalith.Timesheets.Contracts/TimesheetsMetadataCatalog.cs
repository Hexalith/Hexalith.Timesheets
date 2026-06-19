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
                new("target", "Target reference", "TimeEntryTargetReference", true),
                new("contributor", "Contributor", "PartyReference", true),
                new("activityType", "Activity Type", "ActivityTypeId", true),
                new("durationMinutes", "Duration minutes", "WholeMinutes", true),
                new("billableState", "Billable state", nameof(BillableState), true),
                new("comment", "Comment", nameof(TimeEntryComment), false, "Comments may be excluded by policy.")
            ],
            [
                new("record-time", "Record time", "Timesheets.RecordTime")
            ],
            [
                new("approval", "Approval", nameof(TimeEntryApprovalState)),
                new("billable", "Billable", nameof(BillableState)),
                new("contributor", "Contributor", nameof(ContributorCategory)),
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
                new("label", "Label", "String", true),
                new("scope", "Scope", nameof(ActivityTypeScope), true),
                new("billableDefault", "Billable default", nameof(BillableState), false)
            ],
            [
                new("create-tenant-activity-type", "Create tenant Activity Type", "Timesheets.CreateTenantActivityType"),
                new("create-project-activity-type", "Create project Activity Type", "Timesheets.CreateProjectActivityType"),
                new("rename-activity-type", "Rename Activity Type", "Timesheets.RenameActivityType"),
                new("deactivate-activity-type", "Deactivate Activity Type", "Timesheets.DeactivateActivityType"),
                new("reactivate-activity-type", "Reactivate Activity Type", "Timesheets.ReactivateActivityType")
            ],
            [
                new("activity-type-scope", "Scope", nameof(ActivityTypeScope)),
                new("active-state", "Active state", "ActivityTypeActiveState")
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
                new("comment", "Comment", nameof(TimeEntryComment), false, "Comments may contain sensitive information."),
                new("projectionFreshness", "Projection freshness", nameof(ProjectionFreshnessState), true)
            ],
            [
                new("review-export-policy", "Review export policy", "Timesheets.ReviewExportPolicy")
            ],
            [
                new("projection-freshness", "Projection freshness", nameof(ProjectionFreshnessState)),
                new("correction", "Correction", nameof(TimeEntryCorrectionState)),
                new("ai-metrics", "AI metric availability", nameof(AiMetricAvailability)),
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
