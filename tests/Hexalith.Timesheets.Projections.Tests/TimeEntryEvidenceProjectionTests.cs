using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimeEntryEvidenceProjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant-2")]
    [InlineData("TENANT-1")]
    [InlineData("tenant-1 ")]
    public void EvidenceAndListRejectMismatchedCheckpointTenant(string? checkpointTenant)
    {
        TimeEntryProjectionEvent recording = Event("m1", 1, Recorded("time-entry-1", 45));
        TimeEntryProjectionEvent[][] histories = [[], [recording]];
        foreach (ProjectionFreshness freshness in Enum.GetValues<ProjectionFreshness>())
        {
            var checkpoint = new TimesheetsProjectionCheckpoint(
                checkpointTenant!, TimeEntryEvidenceProjection.ProjectionName, 1, freshness);
            foreach (TimeEntryProjectionEvent[] events in histories)
            {
                Should.Throw<InvalidOperationException>(() => Projector().Project(
                    "tenant-1", TimeEntryId(), events, checkpoint));
                Should.Throw<InvalidOperationException>(() => ListProjector().Project(
                    "tenant-1", events, checkpoint, new QueryTimeEntries()));
            }

            Should.Throw<InvalidOperationException>(() => Projector().ProjectNext(
                "tenant-1", TimeEntryId(), null, recording, checkpoint));
        }
    }

    [Fact]
    public void IncrementalEvidenceValidatesCheckpointTenantAndPreservesVerifiedPrefix()
    {
        TimeEntryEvidenceReadModel current = Projector().Project(
            "tenant-1", TimeEntryId(), [Event("m1", 1, Recorded("time-entry-1", 45))], FreshCheckpoint(1))!;
        TimeEntryProjectionEvent suffix = Event("m2", 2, Submitted("time-entry-1"));
        TimesheetsProjectionCheckpoint checkpoint = FreshCheckpoint(2);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => Projector().ProjectNext(
            "tenant-1", TimeEntryId(), current, suffix, checkpoint with { TenantId = "tenant-2" }));

        exception.Message.ShouldNotContain("tenant-1");
        exception.Message.ShouldNotContain("tenant-2");
        current.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        current.EventLineage.ShouldHaveSingleItem().EventName.ShouldBe(nameof(TimeEntryRecorded));

        TimeEntryEvidenceReadModel? next = Projector().ProjectNext("tenant-1", TimeEntryId(), current, suffix, checkpoint);

        next.ShouldNotBeNull();
        next.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        next.DurationMinutes.ShouldBe(45);
        next.EventLineage.Select(static item => item.EventName).ShouldBe(
            [nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted)]);
        next.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        next.ProjectionFreshness.Cursor.ShouldBe("2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tenant-2")]
    public void RecordingWithoutMatchingDeliveryScopeIsRejected(string? deliveryTenant)
    {
        TimeEntryProjectionEvent recording = Event("m1", 1, Recorded("time-entry-1", 45))
            with { TenantId = deliveryTenant };

        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1", TimeEntryId(), [recording], FreshCheckpoint(1)));
        Should.Throw<InvalidOperationException>(() => ListProjector().Project(
            "tenant-1", [recording], FreshCheckpoint(1), new QueryTimeEntries()));
    }

    [Fact]
    public void CrossTenantMagicLinkTerminalIsRejected()
    {
        var used = new MagicLinkConfirmationCapabilityUsed(
            new MagicLinkCapabilityId("capability-1"), new TenantReference("tenant-2"),
            Contributor(), TimeEntryId(), DateTimeOffset.UtcNow,
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1", TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45)), Event("m2", 2, new StoredMagicLinkUsed(used))],
            FreshCheckpoint(2)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossTenantRevokeOrExpirePayloadIsRejectedByEvidenceAndList(bool expire)
    {
        var capabilityId = new MagicLinkCapabilityId("capability-1");
        var otherTenant = new TenantReference("tenant-2");
        var source = new MagicLinkAuditMetadata("timesheets", "terminal");
        object terminal = expire
            ? new MagicLinkConfirmationCapabilityExpired(capabilityId, otherTenant, DateTimeOffset.UtcNow, source)
                { TimeEntryId = TimeEntryId() }
            : new MagicLinkConfirmationCapabilityRevoked(capabilityId, otherTenant, Contributor(), DateTimeOffset.UtcNow, source)
                { TimeEntryId = TimeEntryId() };
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45)),
            Event("m2", 2, terminal)
        ];

        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1", TimeEntryId(), events, FreshCheckpoint(2)));
        Should.Throw<InvalidOperationException>(() => ListProjector().Project(
            "tenant-1", events, FreshCheckpoint(2), new QueryTimeEntries()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionFoldsStoredMagicLinkEffectsAndDuplicateDelivery(bool adjust)
    {
        TimeEntryRecorded recorded = Recorded("time-entry-1", 45);
        object effect = adjust
            ? new StoredTimeEntryAdjusted(Adjusted("time-entry-1", 75))
            : new StoredTimeEntryConfirmed(Confirmed("time-entry-1"));
        TimeEntryProjectionEvent storedEffect = Event("message-2", 3, effect);

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1", TimeEntryId(),
            [Event("message-1", 1, new StoredTimeEntryRecorded(recorded)),
                storedEffect, storedEffect],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.EventLineage.Count.ShouldBe(2);
        if (adjust)
        {
            model.DurationMinutes.ShouldBe(75);
            model.EventLineage[1].EventName.ShouldBe(nameof(TimeEntryAdjustedThroughMagicLink));
        }
        else
        {
            model.ContributorConfirmation.ShouldNotBeNull();
            model.EventLineage[1].EventName.ShouldBe(nameof(TimeEntryContributorConfirmed));
        }
    }

    [Fact]
    public void ProjectionExposesRecordedDraftEvidenceWithFreshnessMetadata()
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
    public void ProjectionIsIdempotentForDuplicateDelivery()
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
    public void ProjectionPreservesProviderReportedAiMetricsThroughDuplicateReplay()
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
    public void ProjectionPreservesUnreportedTokenMetricsAsNull()
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
    public void ProjectionExposesExternalSourceAndConfirmationEvidenceIdempotently()
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
    public void ProjectionOrdersEventsBySequenceNumberAndIgnoresOtherEntries()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m3", 3, Submitted("time-entry-1")),
                Event("m2", 2, Recorded("other-entry", 15)),
                Event("m1", 1, Recorded("time-entry-1", 30))
            ],
            FreshCheckpoint(3));

        model.ShouldNotBeNull();
        model.DurationMinutes.ShouldBe(30);
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.EventLineage.Select(static item => item.Ordinal).ShouldBe([1, 3]);
    }

    [Fact]
    public void ProjectionAppliesSubmittedAfterRecordedWithoutMutatingEvidence()
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
    public void ProjectionUnwrapsStoredSubmissionAndPreservesSubmissionLineage()
    {
        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1", TimeEntryId(),
            [
                Event("m1", 1, new StoredTimeEntryRecorded(Recorded("time-entry-1", 45))),
                Event("m2", 2, new StoredTimeEntrySubmitted(Submitted("time-entry-1")))
            ], FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntrySubmitted)]);
        model.EventLineage.Select(static item => item.Ordinal).ShouldBe([1, 2]);
    }

    [Fact]
    public void ProjectionDedupesDuplicateSubmittedMessageId()
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
    public void ProjectionAppliesApprovedAfterSubmittedWithoutMutatingRecordedOrSubmittedEvidence()
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
    public void ProjectionDedupesDuplicateApprovalWithoutDuplicateLockLineage()
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
    public void ProjectionAppliesRejectedAfterSubmittedAndPreservesRejectionReason()
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
    public void ProjectionAppliesCorrectedAfterRejectedAndPreservesRejectionEvidence()
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
    public void ProjectionAppliesResubmissionOnlyAfterCorrectionAndKeepsLineage()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")),
                Event("m5", 5, Submitted("time-entry-1", "submission-2"))
            ],
            FreshCheckpoint(5)));

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
    public void ProjectionAppliesApprovedCorrectionAfterApprovalAndPreservesLockAndApprovalEvidence()
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
    public void ProjectionAndLedgerPreserveExplicitApprovedCorrectionScopeLineage()
    {
        TimeEntryApprovedCorrected corrected = ApprovedCorrected("time-entry-1", 75) with
        {
            PreviousValues = CorrectionValues(45, "Original evidence.") with
            {
                ActivityTypeScope = ActivityTypeScope.Project
            },
            CorrectedValues = CorrectionValues(75, "Approved correction evidence.") with
            {
                ActivityTypeScope = ActivityTypeScope.Tenant
            }
        };

        TimeEntryEvidenceReadModel model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45) with { ActivityTypeScope = ActivityTypeScope.Project }),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1")),
                Event("m4", 4, corrected)
            ],
            FreshCheckpoint(4)).ShouldNotBeNull();

        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(model).ActivityTypeScope
            .ShouldBe(ActivityTypeScope.Tenant);
        ApprovedTimeLedgerRowReadModel.SupersededFromApprovedCorrection(model)
            .ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
    }

    [Fact]
    public void ProjectionAndQueryRowPreserveExplicitRejectedCorrectionScopeLineage()
    {
        var projectActivityTypeId = new ActivityTypeId("project-activity-type");
        var tenantActivityTypeId = new ActivityTypeId("tenant-activity-type");
        TimeEntryCorrected corrected = Corrected("time-entry-1", 75) with
        {
            PreviousValues = CorrectionValues(45, "Original evidence.") with
            {
                ActivityTypeId = projectActivityTypeId,
                ActivityTypeScope = ActivityTypeScope.Project
            },
            CorrectedValues = CorrectionValues(75, "Corrected after rejection.") with
            {
                ActivityTypeId = tenantActivityTypeId,
                ActivityTypeScope = ActivityTypeScope.Tenant
            }
        };
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45) with
            {
                ActivityTypeId = projectActivityTypeId,
                ActivityTypeScope = ActivityTypeScope.Project
            }),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Rejected("time-entry-1")),
            Event("m4", 4, corrected)
        ];

        TimeEntryEvidenceReadModel model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            events,
            FreshCheckpoint(4)).ShouldNotBeNull();
        TimeEntryQueryRowReadModel row = ListProjector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryTimeEntries()).Items.ShouldHaveSingleItem();

        model.ActivityTypeId.ShouldBe(tenantActivityTypeId);
        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.Correction.ShouldNotBeNull().PreviousValues.ActivityTypeId.ShouldBe(projectActivityTypeId);
        model.Correction.PreviousValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        row.ActivityTypeId.ShouldBe(tenantActivityTypeId);
        row.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
    }

    [Fact]
    public void LegacyApprovedCorrectionRetainsPriorScopeInCurrentAndSupersededLedgerRows()
    {
        TimeEntryEvidenceReadModel model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45) with { ActivityTypeScope = ActivityTypeScope.Project }),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1")),
                Event("m4", 4, ApprovedCorrected("time-entry-1", 75))
            ],
            FreshCheckpoint(4)).ShouldNotBeNull();

        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        model.ApprovedCorrection.ShouldNotBeNull().CorrectedValues.ActivityTypeScope.ShouldBeNull();
        ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(model).ActivityTypeScope
            .ShouldBe(ActivityTypeScope.Project);
        ApprovedTimeLedgerRowReadModel.SupersededFromApprovedCorrection(model)
            .ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
    }

    /// <summary>Preserves project ownership when a legacy rejected correction carries no scope.</summary>
    [Fact]
    public void LegacyRejectedCorrectionRetainsPriorScopeInEvidenceAndQueryRow()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45) with { ActivityTypeScope = ActivityTypeScope.Project }),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Rejected("time-entry-1")),
            Event("m4", 4, Corrected("time-entry-1", 75))
        ];
        TimeEntryEvidenceReadModel model = Projector().Project(
            "tenant-1", TimeEntryId(), events, FreshCheckpoint(4)).ShouldNotBeNull();
        TimeEntryQueryRowReadModel row = ListProjector().Project(
            "tenant-1", events, FreshCheckpoint(4), new QueryTimeEntries()).Items.ShouldHaveSingleItem();

        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        model.DurationMinutes.ShouldBe(75);
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        model.Correction.ShouldNotBeNull().PreviousValues.ActivityTypeScope.ShouldBeNull();
        model.Correction.CorrectedValues.ActivityTypeScope.ShouldBeNull();
        row.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        row.DurationMinutes.ShouldBe(75);
    }

    [Fact]
    public void ProjectionRefusesApprovedCorrectionBeforeApprovalAndPreservesSupportedOrder()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, ApprovedCorrected("time-entry-1", 75)),
                Event("m3", 3, Submitted("time-entry-1")),
                Event("m4", 4, Approved("time-entry-1"))
            ],
            FreshCheckpoint(4)));

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
    public void ProjectionRefusesSupersededCorrectionWithoutASanctionedTransition()
    {
        TimeEntryCorrected superseded = Corrected("time-entry-1", 75) with
        {
            CorrectionState = TimeEntryCorrectionState.Superseded
        };
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            "tenant-1", TimeEntryId(),
            [Event("m1", 1, Recorded("time-entry-1", 45)), Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Rejected("time-entry-1")), Event("m4", 4, superseded)], FreshCheckpoint(4)));
    }

    [Fact]
    public void ProjectionIgnoresApprovalForUnrelatedEntries()
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

    [Fact]
    public void ProjectionAppliesMagicLinkAdjustmentToEffectiveDraftValues()
    {
        TimeEntryAdjustedThroughMagicLink adjusted = Adjusted("time-entry-1", 75);

        TimeEntryEvidenceReadModel? model = Projector().Project(
            "tenant-1",
            TimeEntryId(),
            [
                Event("m1", 1, RecordedExternal("time-entry-1", 45)),
                Event("m2", 2, adjusted),
                Event("m2", 2, adjusted)
            ],
            FreshCheckpoint(2));

        model.ShouldNotBeNull();
        model.DurationMinutes.ShouldBe(75);
        model.ServiceDate.ShouldBe(new DateOnly(2026, 6, 20));
        model.BillableState.ShouldBe(BillableState.NonBillable);
        model.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.ExternalAdjustment.ShouldNotBeNull();
        model.ExternalAdjustment.PreviousValues.DurationMinutes.ShouldBe(45);
        model.ExternalAdjustment.AdjustedValues.DurationMinutes.ShouldBe(75);
        model.ExternalAdjustment.PreviousValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.ExternalAdjustment.AdjustedValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        model.ExternalAdjustment.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));
        model.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntryAdjustedThroughMagicLink)]);
    }

    [Theory]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale)]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable)]
    public void ProjectionFreshnessMetadataDoesNotPresentUnfreshCheckpointAsFresh(
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

    [Fact]
    public void ListProjectionFiltersOperationalDimensionsAndExposesSourceType()
    {
        TimeEntryQueryReadModel page = ListProjector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, RecordedExternal("time-entry-2", 30)),
                Event("m4", 4, Recorded("time-entry-3", 90, ProviderMetrics()))
            ],
            FreshCheckpoint(4),
            new QueryTimeEntries
            {
                Contributor = Contributor(),
                Project = Project(),
                ServiceDateFrom = new DateOnly(2026, 6, 1),
                ServiceDateTo = new DateOnly(2026, 6, 30),
                ActivityTypeId = ActivityId(),
                BillableState = BillableState.Billable,
                ApprovalStates = [TimeEntryApprovalState.Submitted],
                ContributorCategories = [ContributorCategory.Employee],
                SourceTypes = [TimeEntrySourceType.Employee],
                PageSize = 50
            });

        TimeEntryQueryRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(TimeEntryId());
        row.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        row.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        row.SourceType.ShouldBe(TimeEntrySourceType.Employee);
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public void ListProjectionFiltersWorkTargetAndExternalSourceType()
    {
        TimeEntryRecorded workExternal = RecordedExternal("time-entry-2", 30) with
        {
            Target = TimeEntryTargetReference.ForWork(Work())
        };

        TimeEntryQueryReadModel page = ListProjector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, workExternal)
            ],
            FreshCheckpoint(2),
            new QueryTimeEntries
            {
                Work = Work(),
                SourceTypes = [TimeEntrySourceType.ExternalContributor],
                PageSize = 50
            });

        TimeEntryQueryRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("time-entry-2"));
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        row.SourceType.ShouldBe(TimeEntrySourceType.ExternalContributor);
    }

    [Theory]
    [InlineData("2026-06")]
    [InlineData("2026-06-15/2026-06-21")]
    public void ListProjectionFiltersByTenantLocalPeriodKey(string periodKey)
    {
        TimeEntryQueryReadModel page = ListProjector().Project(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 45)),
                Event("m2", 2, Recorded("time-entry-2", 30) with { ServiceDate = new DateOnly(2026, 7, 1) })
            ],
            FreshCheckpoint(2),
            new QueryTimeEntries
            {
                TenantLocalPeriodKey = periodKey,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            });

        page.Items.ShouldHaveSingleItem().TimeEntryId.ShouldBe(TimeEntryId());
    }

    [Fact]
    public void ListProjectionReturnsNoRowsForInvalidTenantLocalPeriodKey()
    {
        TimeEntryQueryReadModel page = ListProjector().Project(
            "tenant-1",
            [Event("m1", 1, Recorded("time-entry-1", 45))],
            FreshCheckpoint(1),
            new QueryTimeEntries
            {
                TenantLocalPeriodKey = "not-a-period"
            });

        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ListProjectionPaginatesWithStableOrderAndDuplicateReplayDeterminism()
    {
        TimeEntryProjectionEvent duplicate = Event("m2", 2, Recorded("time-entry-2", 20));
        TimeEntryProjectionEvent[] events =
        [
            Event("m3", 3, Recorded("time-entry-3", 30)),
            duplicate,
            duplicate,
            Event("m1", 1, Recorded("time-entry-1", 10))
        ];

        TimeEntryQueryReadModel firstPage = ListProjector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(3),
            new QueryTimeEntries
            {
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                SortDirection = TimeEntryQuerySortDirection.Ascending,
                PageSize = 2
            });

        firstPage.Items.Select(static row => row.TimeEntryId.Value)
            .ShouldBe(["time-entry-1", "time-entry-2"]);
        firstPage.NextCursor.ShouldNotBeNull();

        TimeEntryQueryReadModel secondPage = ListProjector().Project(
            "tenant-1",
            events,
            FreshCheckpoint(3),
            new QueryTimeEntries
            {
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                SortDirection = TimeEntryQuerySortDirection.Ascending,
                PageSize = 2,
                Cursor = firstPage.NextCursor
            });

        secondPage.Items.Select(static row => row.TimeEntryId.Value)
            .ShouldBe(["time-entry-3"]);
        secondPage.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void ListProjectionRefusesInvalidHistoryEvenWhenNoncurrentStatesAreRequested()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Rejected("time-entry-1")),
            Event("m4", 4, Corrected("time-entry-1", 75) with { CorrectionState = TimeEntryCorrectionState.Superseded })
        ];
        foreach (bool includeNoncurrent in new[] { false, true })
        {
            Should.Throw<InvalidOperationException>(() => ListProjector().Project(
                "tenant-1", events, FreshCheckpoint(4), new QueryTimeEntries
                {
                    CurrentEntriesOnly = !includeNoncurrent,
                    IncludeNonCurrentStates = includeNoncurrent
                }));
        }
    }

    [Fact]
    public void StoredNormalizerRejectsDistinctEventsAtOneStreamSequence()
    {
        TimeEntryRecorded recorded = Recorded("time-entry-1", 45);
        TimeEntryProjectionEvent first = Event("message-1", 1, recorded);
        TimeEntryStoredEventNormalizer.Normalize([first, Event("", 1, recorded)])
            .ShouldHaveSingleItem();

        Should.Throw<InvalidOperationException>(() => TimeEntryStoredEventNormalizer.Normalize(
            [first, Event("message-2", 1, Submitted("time-entry-1"))]));
        Should.Throw<InvalidOperationException>(() => TimeEntryStoredEventNormalizer.Normalize(
            [first, Event("", 1, Recorded("time-entry-1", 90))]));
    }

    [Fact]
    public void ListProjectionAcceptsIndependentOwnerStreamsWithTheSameLocalSequence()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("first", 1, Recorded("time-entry-1", 45)),
            Event("second", 1, Recorded("time-entry-2", 30))
        ];

        TimeEntryQueryReadModel page = ListProjector().Project(
            "tenant-1", events, FreshCheckpoint(1), new QueryTimeEntries());

        page.Items.Select(static row => row.TimeEntryId.Value)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ShouldBe(["time-entry-1", "time-entry-2"]);
        Should.Throw<InvalidOperationException>(() => ListProjector().Project(
            "tenant-1",
            [.. events, Event("collision", 1, Recorded("time-entry-1", 90))],
            FreshCheckpoint(1), new QueryTimeEntries()));
    }

    [Fact]
    public void MalformedSelectedOwnerLifecycleAndScopesAreRefusedByFullAndIncrementalReplay()
    {
        const string id = "time-entry-1";
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted(id, 75);
        TimeEntryCorrected correction = Corrected(id, 75);
        TimeEntryApprovedCorrected approvedCorrection = ApprovedCorrected(id, 75);
        (object[] Prefix, object Invalid)[] cases =
        [
            ([], Submitted(id)), ([], Confirmed(id)), ([], adjustment),
            ([], Approved(id)), ([], Rejected(id)), ([], correction), ([], approvedCorrection),
            ([], Recorded(id, 45) with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([], Recorded(id, 45) with { ApprovalState = (TimeEntryApprovalState)99 }),
            ([Recorded(id, 45)], Submitted(id) with { ApprovalState = TimeEntryApprovalState.Draft }),
            ([Recorded(id, 45), Submitted(id)], Approved(id) with { ApprovalState = TimeEntryApprovalState.Rejected }),
            ([Recorded(id, 45), Submitted(id)], Rejected(id) with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([Recorded(id, 45), Submitted(id), Rejected(id)], correction with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([Recorded(id, 45), Submitted(id), Rejected(id)], correction with { CorrectionState = TimeEntryCorrectionState.None }),
            ([Recorded(id, 45), Submitted(id), Approved(id)], approvedCorrection with { ApprovalState = TimeEntryApprovalState.Draft }),
            ([Recorded(id, 45), Submitted(id), Approved(id)], approvedCorrection with { CorrectionState = TimeEntryCorrectionState.None }),
            ([Recorded(id, 45)], Recorded(id, 90)),
            ([Recorded(id, 45)], Approved(id)),
            ([Recorded(id, 45)], Rejected(id)),
            ([Recorded(id, 45)], correction),
            ([Recorded(id, 45)], approvedCorrection),
            ([Recorded(id, 45), Submitted(id)], Submitted(id)),
            ([Recorded(id, 45), Submitted(id)], adjustment),
            ([Recorded(id, 45), Submitted(id), Approved(id)], Submitted(id)),
            ([Recorded(id, 45), Submitted(id), Approved(id)], Rejected(id)),
            ([Recorded(id, 45), Submitted(id), Rejected(id)], Approved(id)),
            ([Recorded(id, 45), Submitted(id), Rejected(id), correction], adjustment),
            ([Recorded(id, 45), Submitted(id), Approved(id), approvedCorrection], approvedCorrection),
            ([Recorded(id, 45), Submitted(id), Rejected(id), correction, Submitted(id), Rejected(id)], correction),
            ([Recorded(id, 45)], adjustment with { PreviousValues = adjustment.PreviousValues with { ActivityTypeScope = ActivityTypeScope.Project } }),
            ([Recorded(id, 45)], adjustment with { AdjustedValues = adjustment.AdjustedValues with { ActivityTypeScope = ActivityTypeScope.Project } }),
            ([Recorded(id, 45), Submitted(id), Rejected(id)], correction with { PreviousValues = correction.PreviousValues with { ActivityTypeScope = ActivityTypeScope.Project } }),
            ([Recorded(id, 45), Submitted(id), Approved(id)], approvedCorrection with { PreviousValues = approvedCorrection.PreviousValues with { ActivityTypeScope = ActivityTypeScope.Project } })
        ];
        foreach ((object[] prefix, object invalid) in cases)
        {
            TimeEntryProjectionEvent[] events = prefix.Select((item, i) => Event($"m{i + 1}", i + 1, item)).ToArray();
            var suffix = Event($"m{events.Length + 1}", events.Length + 1, invalid);
            TimeEntryEvidenceReadModel? current = Projector().Project("tenant-1", TimeEntryId(), events, FreshCheckpoint(events.Length));
            string before = System.Text.Json.JsonSerializer.Serialize(current);

            Should.Throw<InvalidOperationException>(() => Projector().Project(
                "tenant-1", TimeEntryId(), [.. events, suffix], FreshCheckpoint(events.Length + 1)));
            Should.Throw<InvalidOperationException>(() => Projector().ProjectNext(
                "tenant-1", TimeEntryId(), current, suffix, FreshCheckpoint(events.Length + 1)));
            System.Text.Json.JsonSerializer.Serialize(current).ShouldBe(before);
        }
    }

    [Fact]
    public void CorrectionAttributionMatchesPrecedingDecisionInFullAndIncrementalReplay()
    {
        const string id = "time-entry-1";
        (object Decision, object Invalid)[] cases =
        [
            (Approved(id), ApprovedCorrected(id, 75) with { SourceApprovalDecisionId = new("other-decision") }),
            (Approved(id), ApprovedCorrected(id, 75) with { SourceApprovalScope = TimeEntryApprovalScope.TimesheetPeriod }),
            (Rejected(id), Corrected(id, 75) with { RejectionDecisionId = new("other-decision") }),
            (Rejected(id), Corrected(id, 75) with { RejectionReason = new("Other reason") })
        ];
        foreach ((object decision, object invalid) in cases)
        {
            TimeEntryProjectionEvent[] prefix = [Event("record", 1, Recorded(id, 45)),
                Event("submit", 2, Submitted(id)), Event("decision", 3, decision)];
            TimeEntryEvidenceReadModel current = Projector().Project("tenant-1", TimeEntryId(), prefix, FreshCheckpoint(3))!;
            string before = System.Text.Json.JsonSerializer.Serialize(current);
            TimeEntryProjectionEvent suffix = Event("correction", 4, invalid);
            Should.Throw<InvalidOperationException>(() => Projector().Project(
                "tenant-1", TimeEntryId(), [.. prefix, suffix], FreshCheckpoint(4)));
            Should.Throw<InvalidOperationException>(() => Projector().ProjectNext(
                "tenant-1", TimeEntryId(), current, suffix, FreshCheckpoint(4)));
            System.Text.Json.JsonSerializer.Serialize(current).ShouldBe(before);
        }
    }

    [Fact]
    public void LegacyScopesCanonicalRedeliveryAndUnrelatedOwnersPreserveEquivalentReplay()
    {
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted("time-entry-1", 75);
        adjustment = adjustment with
        {
            PreviousValues = adjustment.PreviousValues with { ActivityTypeScope = null },
            AdjustedValues = adjustment.AdjustedValues with { ActivityTypeScope = null }
        };
        TimeEntryProjectionEvent[] events =
        [
            Event("record", 1, new StoredTimeEntryRecorded(RecordedExternal("time-entry-1", 45))),
            Event("adjust", 2, new StoredTimeEntryAdjusted(adjustment)),
            Event("submit", 3, new StoredTimeEntrySubmitted(Submitted("time-entry-1"))),
            Event("approve", 4, Approved("time-entry-1")),
            Event("confirm", 5, new StoredTimeEntryConfirmed(Confirmed("time-entry-1")))
        ];
        TimeEntryEvidenceReadModel? incremental = null;
        foreach (TimeEntryProjectionEvent item in TimeEntryStoredEventNormalizer.Normalize(events))
            incremental = Projector().ProjectNext("tenant-1", TimeEntryId(), incremental, item, FreshCheckpoint(5));
        TimeEntryEvidenceReadModel full = Projector().Project("tenant-1", TimeEntryId(),
            [.. events, events[0], events[1], Event("other", 6, Submitted("unrelated-entry"))], FreshCheckpoint(5))!;

        System.Text.Json.JsonSerializer.Serialize(full).ShouldBe(System.Text.Json.JsonSerializer.Serialize(incremental));
        full.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        full.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        full.EventLineage.Count.ShouldBe(5);
        full.ContributorConfirmation.ShouldNotBeNull();
        full.ExternalAdjustment.ShouldNotBeNull().AdjustedValues.ActivityTypeScope.ShouldBeNull();

        Should.Throw<InvalidOperationException>(() => Projector().Project("tenant-1", TimeEntryId(),
            [events[0], Event("distinct-record", 2, RecordedExternal("time-entry-1", 45))], FreshCheckpoint(2)));
    }

    private static TimeEntryEvidenceProjection Projector() => new();

    private static TimeEntryEvidenceListProjection ListProjector() => new();

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload, "tenant-1");

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

    private static AiEffortMetrics ProviderMetrics()
        => new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            2,
            100,
            50,
            150,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
            AiTokenMetricAvailability.ProviderReported);

    private static TimeEntryRecorded RecordedExternal(string id, int durationMinutes)
        => Recorded(id, durationMinutes, null) with
        {
            ContributorCategory = ContributorCategory.ExternalContributor,
            ExternalSource = new ExternalContributionSource("supplier-api", "request-1")
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

    private static TimeEntryContributorConfirmed Confirmed(string id)
        => new(
            new TimeEntryId(id),
            Contributor(),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new ExternalContributionSource("supplier-api", "confirm-1"));

    private static TimeEntryAdjustedThroughMagicLink Adjusted(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TenantReference("tenant-1"),
            Contributor(),
            new DateTimeOffset(2026, 6, 19, 12, 45, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant,
            ExternalAdjustmentValues(45, new DateOnly(2026, 6, 19), BillableState.Billable) with
            {
                ActivityTypeScope = ActivityTypeScope.Tenant
            },
            ExternalAdjustmentValues(durationMinutes, new DateOnly(2026, 6, 20), BillableState.NonBillable) with
            {
                ActivityTypeScope = ActivityTypeScope.Tenant
            },
            new ExternalContributionSource("magic-link", "capability-1"));

    private static TimeEntryCorrectionValues ExternalAdjustmentValues(
        int durationMinutes,
        DateOnly serviceDate,
        BillableState billableState)
        => new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            serviceDate,
            durationMinutes,
            billableState,
            ContributorCategory.ExternalContributor,
            null);

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

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");
}
