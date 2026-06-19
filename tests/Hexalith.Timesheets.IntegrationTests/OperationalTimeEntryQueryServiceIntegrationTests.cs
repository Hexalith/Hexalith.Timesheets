using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class OperationalTimeEntryQueryServiceIntegrationTests
{
    [Fact]
    public async Task Seeded_entries_can_be_queried_paged_and_drilled_into_evidence_projection()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Recorded("time-entry-2", 45)),
            Event("m4", 4, Submitted("time-entry-2"))
        ];
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            TimeEntryEvidenceListProjection.ProjectionName,
            4,
            ProjectionFreshness.Fresh);
        TimeEntryEvidenceListProjection listProjection = new();
        TimeEntryEvidenceListQueryService service = new(
            new AllowAllAccessGuard(),
            new SeededListReader(listProjection, events, checkpoint),
            new StaticHydrator());

        TimeEntryEvidenceListQueryResult firstPage = await service.QueryAsync(
            Context(),
            new QueryTimeEntries
            {
                Project = Project(),
                ApprovalStates = [TimeEntryApprovalState.Submitted],
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1
            },
            TestContext.Current.CancellationToken);

        firstPage.WasDisclosed.ShouldBeTrue();
        TimeEntryQueryReadModel first = firstPage.Page.ShouldNotBeNull();
        TimeEntryQueryRowReadModel firstRow = first.Items.ShouldHaveSingleItem();
        firstRow.TimeEntryId.ShouldBe(new TimeEntryId("time-entry-1"));
        firstRow.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        firstRow.DisplayHydration.Target.Label.ShouldBe("Project");
        first.NextCursor.ShouldNotBeNull();

        TimeEntryEvidenceListQueryResult secondPage = await service.QueryAsync(
            Context(),
            new QueryTimeEntries
            {
                Project = Project(),
                ApprovalStates = [TimeEntryApprovalState.Submitted],
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1,
                Cursor = first.NextCursor
            },
            TestContext.Current.CancellationToken);

        secondPage.WasDisclosed.ShouldBeTrue();
        TimeEntryQueryReadModel second = secondPage.Page.ShouldNotBeNull();
        second.Items.ShouldHaveSingleItem().TimeEntryId.ShouldBe(new TimeEntryId("time-entry-2"));
        second.NextCursor.ShouldBeNull();

        TimeEntryEvidenceReadModel? drillIn = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            firstRow.TimeEntryId,
            events,
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 4, ProjectionFreshness.Fresh));

        drillIn.ShouldNotBeNull();
        drillIn.TimeEntryId.ShouldBe(firstRow.TimeEntryId);
        drillIn.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
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
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

    private sealed class SeededListReader(
        TimeEntryEvidenceListProjection projection,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint) : ITimeEntryEvidenceListProjectionReader
    {
        public ValueTask<TimeEntryQueryReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryTimeEntries query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<TimeEntryQueryReadModel?>(
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
