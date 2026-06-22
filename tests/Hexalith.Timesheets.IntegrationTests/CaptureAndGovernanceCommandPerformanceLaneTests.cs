using System.Diagnostics;

using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimesheetPeriods;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Opt-in, infrastructure-free command-acknowledgement performance lane for NFR10.
/// Drives the real in-process capture and governance command services (authorization gate
/// → aggregate decision → acknowledgement result) and records the measured p95 latency.
/// <para>
/// The lane is skipped by default and only runs when <c>TIMESHEETS_PERF=1</c> so it never
/// slows or destabilizes the fast unit/architecture baseline. It measures the warmed
/// in-process composed command-decision path, NOT the full EventStore-backed persistence /
/// Dapr wire path, which remains reserved in
/// <see cref="PerformanceEvidenceLaneTests"/>.
/// </para>
/// </summary>
public sealed class CaptureAndGovernanceCommandPerformanceLaneTests
{
    private const string OptInVariable = "TIMESHEETS_PERF";
    private const int WarmupIterations = 100;
    private const int MeasuredIterations = 500;

    // NFR10 acknowledgement target. Used here only as a generous sanity bound inside the
    // opt-in branch: an in-process command decision runs in microseconds, so this trips only
    // on a catastrophic regression. It is intentionally NOT a brittle default-suite CI gate.
    private const double Nfr10TargetMilliseconds = 500.0;

    private readonly ITestOutputHelper _output;

    public CaptureAndGovernanceCommandPerformanceLaneTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Capture_and_governance_command_acknowledgements_record_nfr10_p95_evidence()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the command performance lane.");
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        List<ScenarioMeasurement> measurements =
        [
            await MeasureCaptureRecordAsync(cancellationToken),
            await MeasureEntrySubmissionAsync(cancellationToken),
            await MeasureEntryApprovalAsync(cancellationToken),
            await MeasureEntryRejectionAsync(cancellationToken),
            await MeasureRejectedCorrectionAsync(cancellationToken),
            await MeasureApprovedCorrectionAsync(cancellationToken),
            await MeasurePeriodSubmissionAsync(cancellationToken),
            await MeasurePeriodApprovalAsync(cancellationToken),
            await MeasurePeriodRejectionAsync(cancellationToken),
            await MeasureTenantActivityTypeAsync(cancellationToken),
            await MeasureProjectActivityTypeAsync(cancellationToken),
        ];

        ReportEvidence(measurements);

        foreach (ScenarioMeasurement measurement in measurements)
        {
            measurement.P95Milliseconds.ShouldBeLessThan(
                Nfr10TargetMilliseconds,
                $"{measurement.Scenario} p95 exceeded the NFR10 {Nfr10TargetMilliseconds} ms acknowledgement sanity bound.");
        }
    }

    // ---- Measurement engine -------------------------------------------------------------

    private async Task<ScenarioMeasurement> MeasureAsync(
        string scenario,
        Func<CancellationToken, ValueTask<bool>> acknowledge,
        CancellationToken cancellationToken)
    {
        // Warm the JIT/path so the measured pass reflects steady-state command cost.
        for (int iteration = 0; iteration < WarmupIterations; iteration++)
        {
            (await acknowledge(cancellationToken)).ShouldBeTrue(
                $"{scenario} warm-up acknowledgement was not dispatched/accepted.");
        }

        double[] durations = new double[MeasuredIterations];
        for (int iteration = 0; iteration < MeasuredIterations; iteration++)
        {
            long start = Stopwatch.GetTimestamp();
            bool acknowledged = await acknowledge(cancellationToken);
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            // A degenerate/no-op path must NOT register as "fast": assert the real
            // acknowledgement outcome every measured iteration.
            acknowledged.ShouldBeTrue(
                $"{scenario} acknowledgement was not dispatched/accepted.");
            durations[iteration] = elapsedMilliseconds;
        }

        Array.Sort(durations);

        // Nearest-rank percentile math lives in PerformanceStatistics so it is covered by fast
        // unit tests (PerformanceStatisticsTests), independent of this opt-in timing lane.
        return new ScenarioMeasurement(
            scenario,
            PerformanceStatistics.NearestRankPercentile(durations, 0.95),
            durations[0],
            PerformanceStatistics.NearestRankPercentile(durations, 0.50),
            durations[^1]);
    }

    private void ReportEvidence(IReadOnlyList<ScenarioMeasurement> measurements)
    {
        // NFR12: emit only timing aggregates and scenario names - no command bodies,
        // event payloads, comments, personal data, tokens, or secrets.
        _output.WriteLine(
            $"Command acknowledgement performance lane (warm-up {WarmupIterations}, measured {MeasuredIterations} iterations per scenario).");
        _output.WriteLine("NFR10 target: 500 ms p95 in a warmed in-process command pipeline.");
        _output.WriteLine("Scenario | p95 (ms) | min (ms) | median (ms) | max (ms)");

        foreach (ScenarioMeasurement measurement in measurements)
        {
            _output.WriteLine(
                $"{measurement.Scenario} | {measurement.P95Milliseconds:F4} | {measurement.MinMilliseconds:F4} | {measurement.MedianMilliseconds:F4} | {measurement.MaxMilliseconds:F4}");
        }

        ScenarioMeasurement worst = measurements.MaxBy(static measurement => measurement.P95Milliseconds)!;
        _output.WriteLine(
            $"Worst-case capture/governance command p95: {worst.Scenario} at {worst.P95Milliseconds:F4} ms (NFR10 target 500 ms p95).");
    }

    // ---- Capture scenario ---------------------------------------------------------------

    private async Task<ScenarioMeasurement> MeasureCaptureRecordAsync(CancellationToken cancellationToken)
    {
        TimeEntryCommandService recordService = new(new AllowAllAccessGuard());
        RecordTimeEntry command = new(
            EntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);
        ActivityTypeCatalogReadModel catalog = FreshCatalog();
        TimesheetsRequestContext context = ContributorContext();

        return await MeasureAsync(
            "capture: record draft time entry",
            async ct => (await recordService.RecordAsync(context, command, null, catalog, ct)).WasDispatched,
            cancellationToken);
    }

    // ---- Governance: time entry scenarios -----------------------------------------------

    private async Task<ScenarioMeasurement> MeasureEntrySubmissionAsync(CancellationToken cancellationToken)
    {
        TimeEntrySubmissionCommandService submissionService = new(new AllowAllAccessGuard());
        SubmitTimeEntriesForApproval command = new(
            new TimeEntrySubmissionId("submission-1"),
            [EntryId()],
            TimeEntrySubmissionScope.SelectedEntries);
        Dictionary<TimeEntryId, TimeEntryState?> states = new() { [EntryId()] = RecordedState() };
        ActivityTypeCatalogReadModel catalog = FreshCatalog();
        DateTimeOffset submittedAtUtc = new(2026, 6, 19, 8, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = ContributorContext();

        return await MeasureAsync(
            "governance: submit time entries for approval",
            async ct => (await submissionService.SubmitAsync(context, command, states, catalog, submittedAtUtc, ct)).HasAcceptedEvents,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasureEntryApprovalAsync(CancellationToken cancellationToken)
    {
        TimeEntryApprovalCommandService approvalService = new(new AllowAllAccessGuard(), EntryAuthorityResolver());
        ApproveTimeEntry command = new(EntryId(), new TimeEntryApprovalDecisionId("approval-decision-1"));
        TimeEntryState submittedState = SubmittedState();
        DateTimeOffset decidedAtUtc = new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = ApproverContext();

        return await MeasureAsync(
            "governance: approve submitted time entry",
            async ct => (await approvalService.ApproveAsync(context, command, submittedState, decidedAtUtc, ct)).WasDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasureEntryRejectionAsync(CancellationToken cancellationToken)
    {
        TimeEntryApprovalCommandService approvalService = new(new AllowAllAccessGuard(), EntryAuthorityResolver());
        RejectTimeEntry command = new(
            EntryId(),
            new TimeEntryApprovalDecisionId("rejection-decision-1"),
            new TimeEntryRejectionReason("Needs customer PO evidence."));
        TimeEntryState submittedState = SubmittedState();
        DateTimeOffset decidedAtUtc = new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = ApproverContext();

        return await MeasureAsync(
            "governance: reject submitted time entry",
            async ct => (await approvalService.RejectAsync(context, command, submittedState, decidedAtUtc, ct)).WasDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasureRejectedCorrectionAsync(CancellationToken cancellationToken)
    {
        TimeEntryCorrectionCommandService correctionService = new(new AllowAllAccessGuard(), EntryAuthorityResolver());
        CorrectRejectedTimeEntry command = new(
            EntryId(),
            new TimeEntryCorrectionId("correction-1"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);
        TimeEntryState rejectedState = RejectedState();
        ActivityTypeCatalogReadModel catalog = FreshCatalog();
        DateTimeOffset correctedAtUtc = new(2026, 6, 19, 11, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = ContributorContext();

        return await MeasureAsync(
            "governance: correct rejected time entry",
            async ct => (await correctionService.CorrectAsync(context, command, rejectedState, catalog, correctedAtUtc, ct)).WasDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasureApprovedCorrectionAsync(CancellationToken cancellationToken)
    {
        TimeEntryCorrectionCommandService correctionService = new(new AllowAllAccessGuard(), EntryAuthorityResolver());
        CorrectApprovedTimeEntry command = new(
            EntryId(),
            new TimeEntryCorrectionId("approved-correction-1"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            new TimeEntryCorrectionReason("Correct approved duration after audit review."));
        TimeEntryState approvedState = ApprovedState();
        ActivityTypeCatalogReadModel catalog = FreshCatalog();
        DateTimeOffset correctedAtUtc = new(2026, 6, 19, 11, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = ApproverContext();

        return await MeasureAsync(
            "governance: correct approved time entry (locking add-correction)",
            async ct => (await correctionService.CorrectAsync(context, command, approvedState, catalog, correctedAtUtc, ct)).WasDispatched,
            cancellationToken);
    }

    // ---- Governance: timesheet period scenarios -----------------------------------------

    private async Task<ScenarioMeasurement> MeasurePeriodSubmissionAsync(CancellationToken cancellationToken)
    {
        TimesheetPeriodSubmissionCommandService service = new(new AllowAllAccessGuard());
        TimeEntryId first = new("time-entry-1");
        TimeEntryId second = new("time-entry-2");
        SubmitTimesheetPeriod command = new(
            new TimesheetPeriodId("period-1"),
            Contributor(),
            new TimesheetPeriodRequest(TimesheetPeriodKind.Monthly, new DateOnly(2026, 6, 19)),
            [first, second]);
        Dictionary<TimeEntryId, TimeEntryState?> states = new()
        {
            [first] = PeriodDraftState(first),
            [second] = PeriodDraftState(second, new DateOnly(2026, 6, 20)),
        };
        ActivityTypeCatalogReadModel catalog = FreshCatalog();
        TenantTimesheetPeriodPolicy policy = new("UTC", DayOfWeek.Monday);
        DateTimeOffset submittedAtUtc = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = new(Tenant(), Contributor(), "perf-period-submit");

        return await MeasureAsync(
            "governance: submit timesheet period",
            async ct => (await service.SubmitAsync(context, command, null, states, catalog, policy, submittedAtUtc, ct)).WasPeriodDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasurePeriodApprovalAsync(CancellationToken cancellationToken)
    {
        TimesheetPeriodApprovalCommandService service = new(new AllowAllAccessGuard(), new StaticAllowAuthorityResolver());
        TimeEntryId first = new("time-entry-1");
        TimeEntryId second = new("time-entry-2");
        ApproveTimesheetPeriod command = new(PeriodId(), PeriodDecisionId());
        TimesheetPeriodState periodState = SubmittedPeriodState(first, second);
        Dictionary<TimeEntryId, TimeEntryState?> states = new()
        {
            [first] = PeriodSubmittedState(first),
            [second] = PeriodSubmittedState(second),
        };
        TimesheetPeriodSummaryReadModel projection = PeriodProjection(first, second);
        DateTimeOffset decidedAtUtc = new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = new(Tenant(), Approver(), "perf-period-approve");

        return await MeasureAsync(
            "governance: approve timesheet period",
            async ct => (await service.ApproveAsync(context, command, periodState, states, projection, decidedAtUtc, ct)).WasPeriodDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasurePeriodRejectionAsync(CancellationToken cancellationToken)
    {
        TimesheetPeriodApprovalCommandService service = new(new AllowAllAccessGuard(), new StaticAllowAuthorityResolver());
        TimeEntryId rejected = new("time-entry-1");
        TimeEntryId unaffected = new("time-entry-2");
        RejectTimesheetPeriod command = new(
            PeriodId(),
            PeriodDecisionId(),
            [new(rejected, new TimeEntryRejectionReason("Missing customer evidence."))],
            new TimesheetPeriodRejectionReason("Period contains entries needing correction."));
        TimesheetPeriodState periodState = SubmittedPeriodState(rejected, unaffected);
        Dictionary<TimeEntryId, TimeEntryState?> states = new()
        {
            [rejected] = PeriodSubmittedState(rejected),
            [unaffected] = PeriodSubmittedState(unaffected),
        };
        TimesheetPeriodSummaryReadModel projection = PeriodProjection(rejected, unaffected);
        DateTimeOffset decidedAtUtc = new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);
        TimesheetsRequestContext context = new(Tenant(), Approver(), "perf-period-reject");

        return await MeasureAsync(
            "governance: reject timesheet period",
            async ct => (await service.RejectAsync(context, command, periodState, states, projection, decidedAtUtc, ct)).WasPeriodDispatched,
            cancellationToken);
    }

    // ---- Governance: Activity-Type catalog scenarios ------------------------------------

    private async Task<ScenarioMeasurement> MeasureTenantActivityTypeAsync(CancellationToken cancellationToken)
    {
        TenantActivityTypeCommandService service = new(new AllowAllAccessGuard());
        CreateTenantActivityType command = new(
            new ActivityTypeId("tenant-activity-type-1"),
            "Discovery",
            BillableState.Billable);
        TimesheetsRequestContext context = new(Tenant(), new PartyReference("operator-1"), "perf-tenant-activity");

        return await MeasureAsync(
            "governance: create tenant activity type",
            async ct => (await service.CreateAsync(context, command, null, ct)).WasDispatched,
            cancellationToken);
    }

    private async Task<ScenarioMeasurement> MeasureProjectActivityTypeAsync(CancellationToken cancellationToken)
    {
        ProjectActivityTypeCommandService service = new(new AllowAllAccessGuard());
        CreateProjectActivityType command = new(
            new ActivityTypeId("project-activity-type-1"),
            Project(),
            "Discovery",
            BillableState.Billable);
        TimesheetsRequestContext context = new(Tenant(), new PartyReference("operator-1"), "perf-project-activity");

        return await MeasureAsync(
            "governance: create project activity type",
            async ct => (await service.CreateAsync(context, command, null, ct)).WasDispatched,
            cancellationToken);
    }

    // ---- Shared in-process fixtures (warmed once, outside the measured loop) -------------

    private static TimesheetsApprovalAuthorityResolver EntryAuthorityResolver()
        => new(
            new TimesheetsApprovalAuthorityPolicyOptions { PolicyVersion = "v2" },
            [
                new FixedAuthorityProvider(static request => ApprovalAuthoritySourceResult.Allowed(
                    request.Action == ApprovalAuthorityAction.CorrectionAuthorization
                        ? ApprovalAuthoritySource.TenantAdministrator
                        : ApprovalAuthoritySource.ProjectApprover,
                    ProjectionFreshnessMetadata.Fresh)),
            ]);

    private static ActivityTypeCatalogReadModel FreshCatalog()
        => new([ActiveCatalogItem()], ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogItem ActiveCatalogItem()
        => new(ActivityId(), ActivityTypeScope.Tenant, null, "Delivery", true, BillableState.Billable);

    private static TimeEntryState RecordedState()
    {
        TimeEntryState state = new();
        state.Apply(Recorded());
        return state;
    }

    private static TimeEntryState SubmittedState()
    {
        TimeEntryState state = RecordedState();
        state.Apply(Submitted());
        return state;
    }

    private static TimeEntryState RejectedState()
    {
        TimeEntryState state = SubmittedState();
        state.Apply(Rejected());
        return state;
    }

    private static TimeEntryState ApprovedState()
    {
        TimeEntryState state = SubmittedState();
        state.Apply(Approved());
        return state;
    }

    private static TimeEntryState PeriodDraftState(TimeEntryId timeEntryId, DateOnly? serviceDate = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            serviceDate ?? new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable));
        return state;
    }

    private static TimeEntryState PeriodSubmittedState(TimeEntryId timeEntryId)
    {
        TimeEntryState state = PeriodDraftState(timeEntryId);
        state.Apply(new TimeEntrySubmitted(
            timeEntryId,
            Contributor(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-existing"),
            TimeEntrySubmissionScope.TimesheetPeriod,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimesheetPeriodState SubmittedPeriodState(params TimeEntryId[] ids)
    {
        TimesheetPeriodState state = new();
        state.Apply(PeriodSubmitted(ids));
        return state;
    }

    private static TimesheetPeriodSubmitted PeriodSubmitted(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Contributor(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "UTC",
            ids,
            TimesheetPeriodApprovalState.Submitted);

    private static TimesheetPeriodSummaryReadModel PeriodProjection(params TimeEntryId[] ids)
        => new(
            PeriodId(),
            Tenant(),
            Contributor(),
            Contributor(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            TimesheetPeriodKind.Monthly,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            "UTC",
            ids,
            TimesheetPeriodApprovalState.Submitted,
            ProjectionFreshnessMetadata.Fresh)
        {
            EntrySummaries = ids.Select(static id => new TimesheetPeriodEntrySummary(
                id,
                TimeEntryApprovalState.Submitted,
                TimeEntryCorrectionState.None,
                TimeEntryLockState.Unlocked,
                ProjectionFreshnessMetadata.Fresh)).ToArray(),
        };

    private static TimeEntryRecorded Recorded()
        => new(
            EntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted Submitted()
        => new(
            EntryId(),
            Contributor(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 8, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryRejected Rejected()
        => new(
            EntryId(),
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("rejection-decision-1"),
            TimeEntryApprovalState.Rejected,
            new(
                ApprovalAuthorityAction.EntryRejection,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey,
                "v2",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence."));

    private static TimeEntryApproved Approved()
        => new(
            EntryId(),
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("approval-decision-1"),
            TimeEntryApprovalState.Approved,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey,
                "v2",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimesheetsRequestContext ContributorContext() => new(Tenant(), Contributor(), "perf-contributor");

    private static TimesheetsRequestContext ApproverContext() => new(Tenant(), Approver(), "perf-approver");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("party-contributor");

    private static PartyReference Approver() => new("party-approver");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId EntryId() => new("time-entry-1");

    private static TimesheetPeriodId PeriodId() => new("period-1");

    private static TimesheetPeriodApprovalDecisionId PeriodDecisionId() => new("period-decision-1");

    private sealed record ScenarioMeasurement(
        string Scenario,
        double P95Milliseconds,
        double MinMilliseconds,
        double MedianMilliseconds,
        double MaxMilliseconds);

    // Allow-all guard so the lane measures the command-decision path itself, not an
    // authorization denial. Does not record requests (avoids unbounded growth across iterations).
    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());

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
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(
                request.UiAction ?? TimesheetsUiAction.Capture));
    }

    // Allowing approval-authority provider for entry approval/rejection/correction,
    // mirroring the in-process construction of the governance E2E tests.
    private sealed class FixedAuthorityProvider(
        Func<ApprovalAuthorityResolutionRequest, ApprovalAuthoritySourceResult> evaluate)
        : IApprovalAuthoritySourceProvider
    {
        public ApprovalAuthoritySource Source => ApprovalAuthoritySource.ProjectApprover;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(evaluate(request));
    }

    // Static allowing resolver for period approval/rejection, mirroring the period E2E tests.
    private sealed class StaticAllowAuthorityResolver : ITimesheetsApprovalAuthorityResolver
    {
        public ValueTask<ApprovalAuthorityResolutionResult> ResolveAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(ApprovalAuthorityResolutionResult.Allowed(
                new ApprovalAuthoritySourceAttribution(
                    request.Action,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Allowed,
                    "timesheets.approval-authority.v1",
                    "v1",
                    ProjectionFreshnessMetadata.Fresh)));
    }
}
