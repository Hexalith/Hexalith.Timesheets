using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class ApprovedTimeLedgerProjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant-2")]
    [InlineData("TENANT-1")]
    [InlineData("tenant-1 ")]
    public void LedgerRejectsMismatchedCheckpointTenantBeforeExportReadiness(string? checkpointTenant)
    {
        TimeEntryProjectionEvent[][] histories =
        [
            [],
            [Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1"))]
        ];
        foreach (ProjectionFreshness freshness in Enum.GetValues<ProjectionFreshness>())
        {
            var checkpoint = new TimesheetsProjectionCheckpoint(
                checkpointTenant!, ApprovedTimeLedgerProjection.ProjectionName, 3, freshness);
            foreach (TimeEntryProjectionEvent[] events in histories)
            {
                Should.Throw<InvalidOperationException>(() => Projector().Project(
                    "tenant-1", events, checkpoint, new QueryApprovedTimeLedger()));
            }
        }
    }

    [Fact]
    public void LedgerRejectsUnscopedRecording()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1",
            [Event("m1", 1, Recorded("time-entry-1", 45)) with { TenantId = null }],
            FreshCheckpoint(1), new QueryApprovedTimeLedger()));
    }

    [Fact]
    public void LedgerAcceptsIndependentOwnerStreamsWithTheSameLocalSequences()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("first-recorded", 1, Recorded("time-entry-1", 45)),
            Event("second-recorded", 1, Recorded("time-entry-2", 30)),
            Event("first-submitted", 2, Submitted("time-entry-1")),
            Event("second-submitted", 2, Submitted("time-entry-2")),
            Event("first-approved", 3, Approved("time-entry-1", "decision-1")),
            Event("second-approved", 3, Approved("time-entry-2", "decision-2"))
        ];

        ApprovedTimeLedgerReadModel page = Projector().Project(
            "tenant-1", events, FreshCheckpoint(3), new QueryApprovedTimeLedger());

        page.Items.Select(static row => row.TimeEntryId.Value)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ShouldBe(["time-entry-1", "time-entry-2"]);
    }

    [Fact]
    public void LedgerIncludesOnlyApprovedEntriesByDefault()
    {
        ApprovedTimeLedgerReadModel page = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("draft-entry", 15)),
                Event("m2", 2, Recorded("submitted-entry", 30)),
                Event("m3", 3, Submitted("submitted-entry")),
                Event("m4", 4, Recorded("approved-entry", 45)),
                Event("m5", 5, Submitted("approved-entry")),
                Event("m6", 6, Approved("approved-entry"))
            ],
            FreshCheckpoint(6),
            new QueryApprovedTimeLedger());

        ApprovedTimeLedgerRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("approved-entry"));
        row.ApprovalDecision.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        row.RowState.ShouldBe(ApprovedTimeLedgerRowState.Current);
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        page.CanUseForExport.ShouldBeTrue();
    }

    [Fact]
    public void LedgerPreservesApprovedCorrectionLineageAndIncludesSupersededRowsOnlyWhenRequested()
    {
        TimeEntryProjectionEvent duplicateCorrection = Event("m4", 4, ApprovedCorrected("time-entry-1", 75));
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1")),
            duplicateCorrection,
            duplicateCorrection
        ];

        ApprovedTimeLedgerReadModel currentOnly = Projector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryApprovedTimeLedger
            {
                SortBy = TimeEntryQuerySortBy.TimeEntryId
            });

        ApprovedTimeLedgerRowReadModel current = currentOnly.Items.ShouldHaveSingleItem();
        current.DurationMinutes.ShouldBe(75);
        current.RowState.ShouldBe(ApprovedTimeLedgerRowState.Current);
        current.ApprovedCorrection.ShouldNotBeNull();
        current.Comment.ShouldBeNull();
        current.CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        current.EventLineage.Select(static item => item.EventName)
            .ShouldBe([
                nameof(TimeEntryRecorded),
                nameof(TimeEntrySubmitted),
                nameof(TimeEntryApproved),
                nameof(TimeEntryApprovedCorrected)
            ]);

        ApprovedTimeLedgerReadModel includingSuperseded = Projector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryApprovedTimeLedger
            {
                CurrentRowsOnly = false,
                IncludeSupersededRows = true,
                SortBy = TimeEntryQuerySortBy.TimeEntryId
            });

        includingSuperseded.Items.Count.ShouldBe(2);
        includingSuperseded.Items.Select(static row => row.RowState)
            .ShouldBe([ApprovedTimeLedgerRowState.Current, ApprovedTimeLedgerRowState.Superseded]);
        ApprovedTimeLedgerRowReadModel superseded = includingSuperseded.Items
            .Single(static row => row.RowState == ApprovedTimeLedgerRowState.Superseded);
        superseded.DurationMinutes.ShouldBe(45);
        superseded.LockEvidence.LockState.ShouldBe(TimeEntryLockState.SupersededLocked);
    }

    [Fact]
    public void LedgerPreservesRejectedCorrectionLineageOnlyAfterLaterApproval()
    {
        TimeEntryProjectionEvent[] beforeApproval =
        [
            Event("m1", 1, Recorded("time-entry-1", 45)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Rejected("time-entry-1")),
            Event("m4", 4, Corrected("time-entry-1", 75)),
            Event("m5", 5, Submitted("time-entry-1", "submission-2"))
        ];

        Projector().Project(
            "tenant-1",
            beforeApproval,
            FreshCheckpoint(5),
            new QueryApprovedTimeLedger()).Items.ShouldBeEmpty();

        ApprovedTimeLedgerReadModel afterApproval = Projector().Project(
            "tenant-1",
            [.. beforeApproval, Event("m6", 6, Approved("time-entry-1", "decision-3"))],
            FreshCheckpoint(6),
            new QueryApprovedTimeLedger());

        ApprovedTimeLedgerRowReadModel row = afterApproval.Items.ShouldHaveSingleItem();
        row.DurationMinutes.ShouldBe(75);
        row.Correction.ShouldNotBeNull();
        row.Correction.RejectionDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-2"));
        row.ApprovalDecision.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-3"));
    }

    [Fact]
    public void LedgerFiltersSortsPagesAndDoesNotDuplicateReplayedEvents()
    {
        TimeEntryProjectionEvent duplicate = Event("m6", 6, Approved("time-entry-2", "decision-2"));
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", 30)),
            Event("m5", 5, Submitted("time-entry-2")),
            duplicate,
            duplicate
        ];

        ApprovedTimeLedgerReadModel firstPage = Projector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryApprovedTimeLedger
            {
                Project = Project(),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1
            });

        firstPage.Items.ShouldHaveSingleItem().TimeEntryId.ShouldBe(new TimeEntryId("time-entry-1"));
        firstPage.NextCursor.ShouldNotBeNull();

        ApprovedTimeLedgerReadModel secondPage = Projector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryApprovedTimeLedger
            {
                Project = Project(),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1,
                Cursor = firstPage.NextCursor
            });

        secondPage.Items.ShouldHaveSingleItem().TimeEntryId.ShouldBe(new TimeEntryId("time-entry-2"));
        secondPage.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void LedgerAppliesWorkContributorActivityPeriodDateAndBillableFilters()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("project-entry", 45)),
            Event("m2", 2, Submitted("project-entry")),
            Event("m3", 3, Approved("project-entry", "decision-1")),
            Event(
                "m4",
                4,
                Recorded(
                    "work-entry",
                    30,
                    TimeEntryTargetReference.ForWork(new WorkReference("work-1")),
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 1),
                    BillableState.NonBillable)),
            Event("m5", 5, Submitted("work-entry")),
            Event("m6", 6, Approved("work-entry", "decision-2"))
        ];

        ApprovedTimeLedgerReadModel page = Projector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryApprovedTimeLedger
            {
                Work = new WorkReference("work-1"),
                Contributor = new PartyReference("party-2"),
                ActivityTypeId = new ActivityTypeId("activity-type-2"),
                TenantLocalPeriodKey = "2026-07",
                ServiceDateFrom = new DateOnly(2026, 7, 1),
                ServiceDateTo = new DateOnly(2026, 7, 31),
                BillableState = BillableState.NonBillable
            });

        ApprovedTimeLedgerRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("work-entry"));
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        row.Target.TargetId.ShouldBe("work-1");
        row.Contributor.ShouldBe(new PartyReference("party-2"));
        row.ActivityTypeId.ShouldBe(new ActivityTypeId("activity-type-2"));
        row.ServiceDate.ShouldBe(new DateOnly(2026, 7, 1));
        row.BillableState.ShouldBe(BillableState.NonBillable);
    }

    [Fact]
    public void LedgerMapsDegradedFreshnessAndBlocksExportReadiness()
    {
        ApprovedTimeLedgerReadModel page = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1"))
            ],
            new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 3, ProjectionFreshness.Degraded),
            new QueryApprovedTimeLedger());

        page.Items.ShouldHaveSingleItem().ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Degraded);
        page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Degraded);
        page.CanUseForExport.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable)]
    public void LedgerMapsNonFreshProjectionStatesAndBlocksExportReadiness(
        ProjectionFreshness freshness,
        ProjectionFreshnessState expectedState)
    {
        ApprovedTimeLedgerReadModel page = Projector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1"))
            ],
            new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 3, freshness),
            new QueryApprovedTimeLedger());

        page.Items.ShouldHaveSingleItem().ProjectionFreshness.State.ShouldBe(expectedState);
        page.ProjectionFreshness.State.ShouldBe(expectedState);
        page.CanUseForExport.ShouldBeFalse();
        page.ExportReadinessDetail.ShouldBe("Projection freshness does not allow export preview.");
    }

    [Theory]
    [InlineData(1, true, true, true, false)]
    [InlineData(2, true, true, false, true)]
    [InlineData(0, true, true, false, false)]
    [InlineData(3, true, true, false, false)]
    [InlineData(4, true, true, false, false)]
    [InlineData(2, false, true, false, false)]
    [InlineData(2, true, false, false, false)]
    public void LedgerRebuildPublishesOnlyServerAuthorizedValues(
        int version, bool configured, bool hostAllows, bool publishOld, bool publishNew)
    {
        const string Id = "time-entry-1";
        const string Original = "original-marker";
        const string Corrected = "corrected-marker";
        TimeEntryComment Comment(string text, bool allowed) => new(text, TimeEntryCommentPolicy.SensitiveDefault with
        {
            // The caller-facing decision deliberately disagrees with issued authority.
            ProjectionInclusion = allowed ? TimesheetsCommentPolicyDecision.Excluded : TimesheetsCommentPolicyDecision.Allowed,
            ServerPublicationVersion = version,
            ServerProjectionInclusion = allowed ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded
        });
        bool allowOld = version == 1;
        TimeEntryApprovedCorrected correction = ApprovedCorrected(Id, 75);
        correction = correction with
        {
            PreviousValues = correction.PreviousValues with { Comment = Comment(Original, allowOld) },
            CorrectedValues = correction.CorrectedValues with { Comment = Comment(Corrected, !allowOld) }
        };
        TimeEntryProjectionEvent[] history =
        [
            Event("m1", 1, Recorded(Id, 45) with { Comment = correction.PreviousValues.Comment }),
            Event("m2", 2, Submitted(Id)),
            Event("m3", 3, Approved(Id)),
            Event("m4", 4, correction)
        ];
        QueryApprovedTimeLedger query = new() { IncludeSupersededRows = true };
        ApprovedTimeLedgerProjection projector = new(new()
        {
            CommentSensitivityPolicyConfigured = configured,
            ProjectionCommentsAllowed = hostAllows
        });
        string direct = JsonSerializer.Serialize(projector.Project("tenant-1", history, FreshCheckpoint(4), query));
        ApprovedTimeLedgerReadModel rebuilt = projector.Project("tenant-1", history.Reverse(), FreshCheckpoint(4), query);
        JsonSerializer.Serialize(rebuilt).ShouldBe(direct);
        direct.Contains(Original, StringComparison.Ordinal).ShouldBe(publishOld);
        direct.Contains(Corrected, StringComparison.Ordinal).ShouldBe(publishNew);
        rebuilt.Items.Count.ShouldBe(2);
        foreach (ApprovedTimeLedgerRowReadModel row in rebuilt.Items)
        {
            (row.Comment?.Text).ShouldBe(row.RowState == ApprovedTimeLedgerRowState.Current
                ? (publishNew ? Corrected : null) : (publishOld ? Original : null));
            TimeEntryApprovedCorrectionEvidence evidence = row.ApprovedCorrection.ShouldNotBeNull();
            (evidence.PreviousValues.Comment?.Text).ShouldBe(publishOld ? Original : null);
            (evidence.CorrectedValues.Comment?.Text).ShouldBe(publishNew ? Corrected : null);
            evidence.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId);
            row.EventLineage.Select(item => item.EventName).ShouldContain(nameof(TimeEntryApprovedCorrected));
        }
    }

    [Fact]
    public void MaximumTenantLocalMonthIncludesTheLastRepresentableDay()
    {
        var page = Projector().Project("tenant-1",
            [
                Event("m1", 1, Recorded("entry", 45, serviceDate: DateOnly.MaxValue)),
                Event("m2", 2, Submitted("entry")),
                Event("m3", 3, Approved("entry"))
            ],
            FreshCheckpoint(3), new QueryApprovedTimeLedger { TenantLocalPeriodKey = "9999-12" });
        page.Items.ShouldHaveSingleItem().ServiceDate.ShouldBe(DateOnly.MaxValue);
    }

    private static ApprovedTimeLedgerProjection Projector() => new();

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload, "tenant-1");

    private static TimeEntryRecorded Recorded(
        string id,
        int durationMinutes,
        TimeEntryTargetReference? target = null,
        PartyReference? contributor = null,
        ActivityTypeId? activityTypeId = null,
        DateOnly? serviceDate = null,
        BillableState billableState = BillableState.Billable)
        => new(
            new TimeEntryId(id),
            target ?? TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
            activityTypeId ?? ActivityId(),
            ActivityTypeScope.Tenant,
            serviceDate ?? new DateOnly(2026, 6, 19),
            durationMinutes,
            billableState,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Internal context.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static TimeEntrySubmitted Submitted(string id, string submissionId = "submission-1")
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId(submissionId),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved(string id, string decisionId = "decision-1")
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId(decisionId),
            TimeEntryApprovalState.Approved,
            Authority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryRejected Rejected(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 15, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalState.Rejected,
            Authority(ApprovalAuthorityAction.EntryRejection),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence."));

    private static TimeEntryCorrected Corrected(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            CorrectionValues(45, "Original evidence."),
            CorrectionValues(durationMinutes, "Corrected after rejection."),
            new TimeEntryRejectionReason("Needs customer PO evidence."),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryApprovedCorrected ApprovedCorrected(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            CorrectionValues(45, "Original evidence."),
            CorrectionValues(durationMinutes, "Approved correction evidence."),
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues CorrectionValues(int durationMinutes, string comment)
        => new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new(comment, TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static ApprovalAuthoritySourceAttribution Authority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequenceNumber)
        => new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");
}
