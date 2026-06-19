using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ApprovedTimeLedgerAuthorizationTests
{
    [Fact]
    public async Task Ledger_query_denies_tenant_before_projection_lookup()
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingHydrator hydrator = new();
        ApprovedTimeLedgerQueryService service = new(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.MissingTenant, "Tenant is missing.")
            ]),
            reader,
            hydrator);

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        reader.Calls.ShouldBe(0);
        hydrator.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Unavailable_ledger_reader_discloses_nothing_after_tenant_authorization()
    {
        ApprovedTimeLedgerQueryService service = new(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
            new UnavailableApprovedTimeLedgerProjectionReader(),
            new TrackingHydrator());

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Page.ShouldBeNull();
    }

    [Fact]
    public async Task Ledger_query_filters_insufficient_role_rows_without_hydrating_denied_rows()
    {
        TrackingLedgerReader reader = new(Page([
            Row("time-entry-1", ProjectTarget()),
            Row("time-entry-2", ProjectTarget("project-2"))
        ]));
        TrackingHydrator hydrator = new();
        ApprovedTimeLedgerQueryService service = new(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No row role.")
            ]),
            reader,
            hydrator);

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ApprovedTimeLedgerReadModel page = result.Page.ShouldNotBeNull();
        page.Items.ShouldHaveSingleItem().TimeEntryId.ShouldBe(new TimeEntryId("time-entry-1"));
        hydrator.Calls.ShouldBe(1);
        hydrator.HydratedEntryIds.ShouldBe(["time-entry-1"]);
        page.CanUseForExport.ShouldBeTrue();
    }

    [Fact]
    public async Task Ledger_query_blocks_export_readiness_when_all_rows_are_filtered()
    {
        TrackingHydrator hydrator = new();
        ApprovedTimeLedgerQueryService service = new(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No row role.")
            ]),
            new TrackingLedgerReader(Page([Row("time-entry-1", ProjectTarget())])),
            hydrator);

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ApprovedTimeLedgerReadModel page = result.Page.ShouldNotBeNull();
        page.Items.ShouldBeEmpty();
        page.CanUseForExport.ShouldBeFalse();
        page.ExportReadinessDetail.ShouldBe("No approved ledger rows are available for export preview.");
        hydrator.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(TimesheetsDenialCategory.InvalidReference)]
    [InlineData(TimesheetsDenialCategory.UnconfiguredPolicy)]
    public async Task Ledger_query_fails_closed_for_non_filterable_row_denials(
        TimesheetsDenialCategory category)
    {
        TrackingHydrator hydrator = new();
        ApprovedTimeLedgerQueryService service = new(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(category, "Unsafe row disclosure.")
            ]),
            new TrackingLedgerReader(Page([Row("time-entry-1", WorkTarget())])),
            hydrator);

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(category);
        hydrator.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Ledger_row_authorization_uses_project_for_project_rows_and_work_for_work_rows()
    {
        RecordingAccessGuard guard = new();
        ApprovedTimeLedgerQueryService service = new(
            guard,
            new TrackingLedgerReader(Page([
                Row("time-entry-1", ProjectTarget()),
                Row("time-entry-2", WorkTarget())
            ])),
            new TrackingHydrator());

        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(
            Context(),
            new QueryApprovedTimeLedger(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        guard.Requests.Count.ShouldBe(3);
        guard.Requests[0].Project.ShouldBeNull();
        guard.Requests[0].Work.ShouldBeNull();
        guard.Requests[1].Project.ShouldBe(new ProjectReference("project-1"));
        guard.Requests[1].Work.ShouldBeNull();
        guard.Requests[2].Project.ShouldBeNull();
        guard.Requests[2].Work.ShouldBe(new WorkReference("work-1"));
    }

    private static ApprovedTimeLedgerReadModel Page(IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows)
        => new(rows, null, ProjectionFreshnessMetadata.Fresh, true, "Approved ledger rows are fresh enough for export preview.");

    private static ApprovedTimeLedgerRowReadModel Row(string id, TimeEntryTargetReference target)
        => new(
            new TimeEntryId(id),
            new PartyReference("party-1"),
            target,
            new DateOnly(2026, 6, 19),
            60,
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            Approval(id),
            TimeEntryLockEvidence.Approved(
                new TimeEntryApprovalDecisionId("decision-" + id),
                TimeEntryApprovalScope.IndividualEntry,
                new PartyReference("approver-1"),
                new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero)),
            ApprovedTimeLedgerRowState.Current,
            ProjectionFreshnessMetadata.Fresh);

    private static TimeEntryApprovalDecisionEvidence Approval(string id)
        => new(
            new TimeEntryId(id),
            new TimeEntryApprovalDecisionId("decision-" + id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            TimeEntryApprovalState.Approved,
            TimeEntryApprovalScope.IndividualEntry,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            null);

    private static TimeEntryTargetReference ProjectTarget(string projectId = "project-1")
        => TimeEntryTargetReference.ForProject(new ProjectReference(projectId));

    private static TimeEntryTargetReference WorkTarget() => TimeEntryTargetReference.ForWork(new WorkReference("work-1"));

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private sealed class TrackingLedgerReader(ApprovedTimeLedgerReadModel page) : IApprovedTimeLedgerProjectionReader
    {
        public int Calls { get; private set; }

        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page);
        }
    }

    private sealed class TrackingHydrator : ITimeEntryDisplayHydrator
    {
        public int Calls { get; private set; }

        public List<string> HydratedEntryIds { get; } = [];

        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
        {
            Calls++;
            HydratedEntryIds.Add(evidence.TimeEntryId.Value);
            return ValueTask.FromResult(TimeEntryDisplayHydration.Unavailable());
        }
    }

    private sealed class SequencedAccessGuard(IReadOnlyList<TimesheetsAuthorizationDecision> decisions) : ITimesheetsAccessGuard
    {
        private int _index;

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            TimesheetsAuthorizationDecision decision = _index < decisions.Count
                ? decisions[_index]
                : TimesheetsAuthorizationDecision.Allowed();
            _index++;
            return ValueTask.FromResult(decision);
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (decision.IsAuthorized)
            {
                await trustedWork(cancellationToken).ConfigureAwait(false);
            }

            return decision;
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
    }

    private sealed class RecordingAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
        }
    }
}
