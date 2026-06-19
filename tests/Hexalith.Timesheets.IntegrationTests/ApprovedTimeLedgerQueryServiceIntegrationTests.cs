using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApprovedTimeLedgerQueryServiceIntegrationTests
{
    [Fact]
    public async Task Seeded_approved_entries_can_be_queried_paged_authorized_hydrated_and_drilled_into_evidence()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", 45)),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2")),
            Event("m7", 7, ApprovedCorrected("time-entry-2", 60))
        ];
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ApprovedTimeLedgerProjection.ProjectionName,
            7,
            ProjectionFreshness.Fresh);
        ApprovedTimeLedgerProjection ledgerProjection = new();
        ApprovedTimeLedgerQueryService service = new(
            new AllowAllAccessGuard(),
            new SeededLedgerReader(ledgerProjection, events, checkpoint),
            new StaticHydrator());

        ApprovedTimeLedgerQueryResult firstPage = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger
            {
                Project = Project(),
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1
            },
            TestContext.Current.CancellationToken);

        firstPage.WasDisclosed.ShouldBeTrue();
        ApprovedTimeLedgerReadModel first = firstPage.Page.ShouldNotBeNull();
        ApprovedTimeLedgerRowReadModel firstRow = first.Items.ShouldHaveSingleItem();
        firstRow.TimeEntryId.ShouldBe(new TimeEntryId("time-entry-1"));
        firstRow.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        firstRow.DisplayHydration.Target.Label.ShouldBe("Project");
        first.CanUseForExport.ShouldBeTrue();
        first.NextCursor.ShouldNotBeNull();

        ApprovedTimeLedgerQueryResult secondPage = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger
            {
                Project = Project(),
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1,
                Cursor = first.NextCursor
            },
            TestContext.Current.CancellationToken);

        secondPage.WasDisclosed.ShouldBeTrue();
        ApprovedTimeLedgerReadModel second = secondPage.Page.ShouldNotBeNull();
        ApprovedTimeLedgerRowReadModel correctedRow = second.Items.ShouldHaveSingleItem();
        correctedRow.TimeEntryId.ShouldBe(new TimeEntryId("time-entry-2"));
        correctedRow.DurationMinutes.ShouldBe(60);
        correctedRow.ApprovedCorrection.ShouldNotBeNull();
        second.NextCursor.ShouldBeNull();

        TimeEntryEvidenceReadModel? drillIn = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            correctedRow.TimeEntryId,
            events,
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 7, ProjectionFreshness.Fresh));

        drillIn.ShouldNotBeNull();
        drillIn.TimeEntryId.ShouldBe(correctedRow.TimeEntryId);
        drillIn.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        drillIn.ApprovedCorrection.ShouldNotBeNull();
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityType(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted Submitted(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-" + id),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved(string id, string decisionId)
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId(decisionId),
            TimeEntryApprovalState.Approved,
            Authority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryApprovedCorrected ApprovedCorrected(string id, int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            Values(45),
            Values(durationMinutes),
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(int durationMinutes)
        => new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityType(),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static ApprovalAuthoritySourceAttribution Authority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

    private sealed class SeededLedgerReader(
        ApprovedTimeLedgerProjection projection,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint) : IApprovedTimeLedgerProjectionReader
    {
        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(
                projection.Project(context.Tenant.TenantId, events, checkpoint, query));
        }
    }

    private sealed class StaticHydrator : ITimeEntryDisplayHydrator
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Fresh("Activity Type")));
        }
    }

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
        }
    }
}
