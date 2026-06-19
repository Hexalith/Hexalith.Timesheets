using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimeEntryEvidenceProjectionTests
{
    [Fact]
    public void Projection_exposes_recorded_draft_evidence_with_freshness_metadata()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45))],
            FreshCheckpoint(1));

        model.ShouldNotBeNull();
        model.TimeEntryId.ShouldBe(TimeEntryId());
        model.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        model.Contributor.ShouldBe(Contributor());
        model.ActivityTypeId.ShouldBe(ActivityId());
        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.DurationMinutes.ShouldBe(45);
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        model.ProjectionFreshness.Cursor.ShouldBe("1");
        model.SourceAuthority.ShouldBe(TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        model.EventLineage.ShouldHaveSingleItem().EventName.ShouldBe(nameof(TimeEntryRecorded));
        model.DisplayHydration.Target.State.ShouldBe(DisplayHydrationState.Unavailable);
    }

    [Fact]
    public void Projection_is_idempotent_for_duplicate_delivery()
    {
        TimeEntryProjectionEvent[] once = [Event("m1", 1, Recorded("time-entry-1", 45))];

        TimeEntryEvidenceReadModel? replayedOnce = Projector().Project("tenant-1", TimeEntryId(), once, FreshCheckpoint(1));
        TimeEntryEvidenceReadModel? replayedDuplicates = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [.. once, once[0]],
            FreshCheckpoint(1));

        replayedOnce.ShouldNotBeNull();
        replayedDuplicates.ShouldNotBeNull();
        replayedDuplicates.DurationMinutes.ShouldBe(replayedOnce.DurationMinutes);
        replayedDuplicates.EventLineage.Select(static item => item.EventName)
            .ShouldBe(replayedOnce.EventLineage.Select(static item => item.EventName));
        replayedDuplicates.EventLineage.Count.ShouldBe(1);
    }

    [Fact]
    public void Projection_preserves_provider_reported_ai_metrics_through_duplicate_replay()
    {
        AiEffortMetrics metrics = new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            2,
            0,
            0,
            0,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
            AiTokenMetricAvailability.ProviderReported);
        TimeEntryProjectionEvent[] once = [Event("m1", 1, Recorded("time-entry-1", 45, metrics))];

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [.. once, once[0]],
            FreshCheckpoint(1));

        model.ShouldNotBeNull();
        model.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        model.AiMetrics.ShouldBe(metrics);
        AiEffortMetrics modelMetrics = model.AiMetrics.ShouldNotBeNull();
        modelMetrics.ProviderInputTokenCount.ShouldBe(0);
        modelMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
        AiEffortMetricSourceMetadata source = modelMetrics.Source.ShouldNotBeNull();
        source.ProviderName.ShouldBe("generic-provider");
        model.EventLineage.Count.ShouldBe(1);
    }

    [Fact]
    public void Projection_preserves_unreported_token_metrics_as_null()
    {
        AiEffortMetrics metrics = new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            2,
            null,
            null,
            null,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-2"),
            AiTokenMetricAvailability.NotReported);

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45, metrics))],
            FreshCheckpoint(1));

        model.ShouldNotBeNull();
        model.AiMetrics.ShouldNotBeNull();
        model.AiMetrics.ProviderInputTokenCount.ShouldBeNull();
        model.AiMetrics.ProviderOutputTokenCount.ShouldBeNull();
        model.AiMetrics.ProviderTotalTokenCount.ShouldBeNull();
        model.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
    }

    [Fact]
    public void Projection_exposes_external_source_and_confirmation_evidence_idempotently()
    {
        TimeEntryRecorded recorded = Recorded("time-entry-1", 45) with
        {
            ContributorCategory = ContributorCategory.ExternalContributor,
            ExternalSource = new ExternalContributionSource("supplier-api", "request-1")
        };
        TimeEntryContributorConfirmed confirmed = Confirmed("time-entry-1");

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, recorded),
                Event("m2", 2, confirmed),
                Event("m2", 2, confirmed)
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.ContributorCategory.ShouldBe(ContributorCategory.ExternalContributor);
        model.ExternalSource.ShouldBe(new ExternalContributionSource("supplier-api", "request-1"));
        model.ContributorConfirmation.ShouldNotBeNull();
        model.ContributorConfirmation.Source.ShouldBe(new ExternalContributionSource("supplier-api", "confirm-1"));
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.ApprovalDecision.ShouldBeNull();
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.Unlocked);
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntryContributorConfirmed)]);
    }

    [Fact]
    public void Projection_orders_events_by_sequence_number_and_ignores_other_entries()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m3", 3, Recorded("time-entry-1", 90)),
                Event("m2", 2, Recorded("other-entry", 15)),
                Event("m1", 1, Recorded("time-entry-1", 30))
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.DurationMinutes.ShouldBe(90);
        model.EventLineage.Select(static item => item.Ordinal).ShouldBe([1, 3]);
    }

    [Fact]
    public void Projection_applies_submitted_after_recorded_without_mutating_evidence()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m1", 1, Recorded("time-entry-1", 45))
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.DurationMinutes.ShouldBe(45);
        model.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        model.Contributor.ShouldBe(Contributor());
        model.ActivityTypeId.ShouldBe(ActivityId());
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        model.EventLineage.Select(static item => item.EventName).ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted)]);
    }

    [Fact]
    public void Projection_dedupes_duplicate_submitted_message_id()
    {
        TimeEntryProjectionEvent submitted = Event("m2", 2, Submitted("time-entry-1"));

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                submitted,
                submitted
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.EventLineage.Select(static item => item.EventName).ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted)]);
    }

    [Fact]
    public void Projection_applies_approved_after_submitted_without_mutating_recorded_or_submitted_evidence()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m3", 3, Approved("time-entry-1")),
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1"))
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        model.DurationMinutes.ShouldBe(45);
        model.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        model.Contributor.ShouldBe(Contributor());
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryApproved)]);
        model.ApprovalDecision.ShouldNotBeNull();
        model.ApprovalDecision.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        model.ApprovalDecision.Approver.ShouldBe(new PartyReference("approver-1"));
        model.ApprovalDecision.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        model.ApprovalDecision.Reason.ShouldBeNull();
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        model.LockEvidence.SourceApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        model.LockEvidence.SourceApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        model.LockEvidence.LockedBy.ShouldBe(new PartyReference("approver-1"));
        model.LockEvidence.LockedAtUtc.ShouldBe(new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Projection_dedupes_duplicate_approval_without_duplicate_lock_lineage()
    {
        TimeEntryProjectionEvent approved = Event("m3", 3, Approved("time-entry-1"));

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                approved,
                approved
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryApproved)]);
    }

    [Fact]
    public void Projection_applies_rejected_after_submitted_and_preserves_rejection_reason()
    {
        TimeEntryProjectionEvent rejected = Event("m3", 3, Rejected("time-entry-1"));

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                rejected,
                rejected
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        model.ApprovalDecision.ShouldNotBeNull();
        model.ApprovalDecision.Reason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        model.ApprovalDecision.TimeEntryId.ShouldBe(TimeEntryId());
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryRejected)]);
    }

    [Fact]
    public void Projection_applies_corrected_after_rejected_and_preserves_rejection_evidence()
    {
        TimeEntryProjectionEvent corrected = Event("m4", 4, Corrected("time-entry-1", 75));

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")),
                corrected,
                corrected
            ],
            FreshCheckpoint(4));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        model.DurationMinutes.ShouldBe(75);
        model.Comment.ShouldNotBeNull();
        model.Comment.Text.ShouldBe("Corrected after rejection.");
        model.ApprovalDecision.ShouldNotBeNull();
        model.ApprovalDecision.Reason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        model.Correction.ShouldNotBeNull();
        model.Correction.TimeEntryCorrectionId.ShouldBe(new TimeEntryCorrectionId("correction-1"));
        model.Correction.PreviousValues.DurationMinutes.ShouldBe(45);
        model.Correction.CorrectedValues.DurationMinutes.ShouldBe(75);
        model.Correction.RejectionReason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        model.Correction.RejectionDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-2"));
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryRejected), nameof(TimeEntryCorrected)]);
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.Unlocked);
    }

    [Fact]
    public void Projection_applies_resubmission_only_after_correction_and_keeps_lineage()
    {
        TimeEntryEvidenceReadModel? blocked = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")),
                Event("m5", 5, Submitted("time-entry-1", "submission-2"))
            ],
            FreshCheckpoint(5));

        blocked.ShouldNotBeNull();
        blocked.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        blocked.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryRejected)]);

        TimeEntryEvidenceReadModel? corrected = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")),
                Event("m4", 4, Corrected("time-entry-1", 75)),
                Event("m5", 5, Submitted("time-entry-1", "submission-2"))
            ],
            FreshCheckpoint(5));

        corrected.ShouldNotBeNull();
        corrected.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        corrected.LockEvidence.LockState.ShouldBe(TimeEntryLockState.Unlocked);
        corrected.DurationMinutes.ShouldBe(75);
        corrected.ApprovalDecision.ShouldNotBeNull();
        corrected.ApprovalDecision.Reason.ShouldBe(new TimeEntryRejectionReason("Needs customer PO evidence."));
        corrected.EventLineage.Select(static item => item.EventName)
            .ShouldBe([
                nameof(TimeEntryRecorded),
                nameof(TimeEntrySubmitted),
                nameof(TimeEntryRejected),
                nameof(TimeEntryCorrected),
                nameof(TimeEntrySubmitted)
            ]);
    }

    [Fact]
    public void Projection_applies_approved_correction_after_approval_and_preserves_lock_and_approval_evidence()
    {
        TimeEntryProjectionEvent corrected = Event("m4", 4, ApprovedCorrected("time-entry-1", 75));

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1")),
                corrected,
                corrected
            ],
            FreshCheckpoint(4));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        model.DurationMinutes.ShouldBe(75);
        model.Comment.ShouldNotBeNull();
        model.Comment.Text.ShouldBe("Approved correction evidence.");
        model.ApprovalDecision.ShouldNotBeNull();
        model.ApprovalDecision.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        model.ApprovedCorrection.ShouldNotBeNull();
        model.ApprovedCorrection.TimeEntryCorrectionId.ShouldBe(new TimeEntryCorrectionId("approved-correction-1"));
        model.ApprovedCorrection.PreviousValues.DurationMinutes.ShouldBe(45);
        model.ApprovedCorrection.CorrectedValues.DurationMinutes.ShouldBe(75);
        model.ApprovedCorrection.Reason.ShouldBe(new TimeEntryCorrectionReason("Correct approved duration after audit review."));
        model.ApprovedCorrection.SourceApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-1"));
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted), nameof(TimeEntryApproved), nameof(TimeEntryApprovedCorrected)]);
    }

    [Fact]
    public void Projection_ignores_approved_correction_before_approval_until_replayed_in_supported_order()
    {
        TimeEntryEvidenceReadModel? blocked = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, ApprovedCorrected("time-entry-1", 75)),
                Event("m3", 3, Submitted("time-entry-1")),
                Event("m4", 4, Approved("time-entry-1"))
            ],
            FreshCheckpoint(4));

        blocked.ShouldNotBeNull();
        blocked.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        blocked.DurationMinutes.ShouldBe(45);
        blocked.ApprovedCorrection.ShouldBeNull();

        TimeEntryEvidenceReadModel? supported = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1")),
                Event("m4", 4, ApprovedCorrected("time-entry-1", 75))
            ],
            FreshCheckpoint(4));

        supported.ShouldNotBeNull();
        supported.DurationMinutes.ShouldBe(75);
        supported.ApprovedCorrection.ShouldNotBeNull();
    }

    [Fact]
    public void Projection_marks_superseded_correction_as_superseded_locked_evidence()
    {
        TimeEntryCorrected superseded = new(
            TimeEntryId(),
            new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            CorrectionValues(45, "Original evidence."),
            CorrectionValues(75, "Corrected after rejection."),
            new TimeEntryRejectionReason("Needs customer PO evidence."),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Superseded);

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")),
                Event("m4", 4, superseded)
            ],
            FreshCheckpoint(4));

        model.ShouldNotBeNull();
        model.CorrectionState.ShouldBe(TimeEntryCorrectionState.Superseded);
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.SupersededLocked);
        model.LockEvidence.SourceApprovalDecisionId.ShouldBeNull();
    }

    [Fact]
    public void Projection_ignores_approval_for_unrelated_entries()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("other-entry"))
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.ApprovalDecision.ShouldBeNull();
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted)]);
    }

    [Theory]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable)]
    public void Projection_freshness_metadata_does_not_present_unfresh_checkpoint_as_fresh(
        ProjectionFreshness freshness,
        ProjectionFreshnessState expectedState)
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45))],
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 1, freshness));

        model.ShouldNotBeNull();
        model.ProjectionFreshness.State.ShouldBe(expectedState);
    }

    private static TimeEntryEvidenceProjection Projector() => new();

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(string id, int durationMinutes)
        => Recorded(id, durationMinutes, AiEffortMetrics.Unavailable);

    private static TimeEntryRecorded Recorded(string id, int durationMinutes, AiEffortMetrics? metrics)
        => new(
            new TimeEntryId(id),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            metrics == AiEffortMetrics.Unavailable ? ContributorCategory.Employee : ContributorCategory.AutomatedAgent,
            metrics);

    private static TimeEntrySubmitted Submitted(string id, string submissionId = "submission-1")
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId(submissionId),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryContributorConfirmed Confirmed(string id)
        => new(
            new TimeEntryId(id),
            Contributor(),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new ExternalContributionSource("supplier-api", "confirm-1"));

    private static TimeEntryApproved Approved(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
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
            Comment = new(
                comment,
                Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault)
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
        => new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");
}
