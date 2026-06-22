---
baseline_commit: 24c6e23f55d987578ab9765f3389d59624fd4de9
---

# Story 4.10: Add Report, Export, and Dashboard Performance Evidence

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a quality owner,
I want launch-scope performance evidence over realistic report, export, and dashboard fixtures,
so that query latency claims (NFR11) are measured where the report/export/dashboard behavior is introduced rather than only at the final release gate.

## Scope decision (READ FIRST — this is "replicate the 1.11 pattern for the read/query paths", not a greenfield build)

This story is the **NFR11 twin of Story 1.11**. Story 1.11 already built the entire performance-lane machine for
the command side (NFR10). You **extend** that machine to the report/export/dashboard read paths — you do **not**
design a new one. Concretely:

- **Measure the in-process composed query/report/export/dashboard path that is runnable today** (seeded in-memory
  readers + an allow-all access guard), exactly as 1.11 measured the in-process command-decision path. Target
  **NFR11 = `2s p95`** ([architecture.md:61], [epics.md:269]), **not** NFR10's `500 ms`.
- **Record the full EventStore-backed wire path as `waived (deferred)`** with owner/risk/revisit, because realistic
  EventStore-backed persisted fixtures still do not exist ([architecture.md:983]). Epic 5 only **aggregates** this
  verdict; it does not re-measure ([epics.md:778], [epics.md:1664-1666]).
- **No separate behavior note.** Unlike Stories 4.8/4.9 (which resolved design decisions), 1.11 recorded its verdict
  and waiver directly in `docs/performance-evidence.md`. Follow that precedent — the verdict + waiver live in
  `docs/performance-evidence.md`, not a `_bmad-output` behavior note.

## Acceptance Criteria

1. **Measured NFR11 report-query lane over tenant/project/period filters (epic AC1 — NFR11).**
   **Given** realistic in-process tenant, contributor, Project, Work, Activity Type, period, approval, ledger,
   report, export, and dashboard fixtures are seeded (no EventStore/Dapr/Aspire infrastructure)
   **When** the isolated performance lane runs the **real** `ActualTimeReportQueryService.QueryProjectAsync` and
   `QueryWorkAsync` paths over the **tenant**, **project**, and **period** (`TenantLocalPeriodKey`) filter
   combinations named in NFR11
   **Then** the measured per-scenario p95 is compared against `2 seconds p95`, **each measured iteration asserts the
   real report result** (disclosed verdict + row count) so a degenerate/no-op path cannot register as "fast", **and**
   any deviation is recorded as explicit launch-readiness evidence in `docs/performance-evidence.md`.

2. **Report, ledger, export, dashboard, and Works planned-effort paths measured with classified evidence (epic AC2).**
   **Given** the report (4.3), Approved-Time Ledger query (4.2), approved-time export generate + side-effect-free
   preview (4.5/4.9), dashboard overview (4.7), and Works planned-effort report (4.8) read paths are exercised
   **When** results are produced
   **Then** the evidence distinguishes **functional correctness** (the asserted result/row count per iteration),
   **infrastructure availability** (in-process vs EventStore-backed wire path), **data volume** (seeded row/page
   counts that force multi-page traversal), **p95 latency** (per scenario), and **launch waiver status**
   (pass / concern / fail / waived per path) — **and** skipped or unavailable lanes are visible in the evidence doc,
   never hidden in final release notes.

3. **Isolated, opt-in lane that keeps the fast baseline fast and stable (epic AC3).**
   **Given** the fast unit/architecture baseline runs
   **When** performance fixtures are unavailable or intentionally skipped
   **Then** the new report/export/dashboard lane is gated behind the existing `TIMESHEETS_PERF=1` env-var opt-in and
   dynamically `Assert.Skip`s when unset (so it never enters the fast baseline and cannot make it slow or flaky), the
   reserved EventStore placeholders stay untouched, any latency assertion is a **generous sanity bound behind the
   gate** (not a brittle hard CI gate), **and** `docs/performance-evidence.md` + `README.md` explain exactly how to
   run the lane.

4. **Verdict + waiver discipline; Epic 5 only aggregates (NFR11 launch readiness).**
   **Given** the NFR11 evidence is reviewed
   **When** report/ledger/export/dashboard/Works-planned-effort results are compared with NFR11
   **Then** each measured path carries an explicit `pass / concern / fail / waived` verdict against the `2s p95`
   target, the EventStore-backed wire path is recorded **waived (deferred)** with owner/risk/revisit so Epic 5
   aggregates this evidence instead of creating the first measurement path, **and** fitness tests guard that the
   NFR11 evidence section and the new lane class remain present and opt-in.

5. **Privacy-safe evidence emission (NFR12).**
   **Given** the lane emits per-scenario measurements
   **When** results are written via `ITestOutputHelper` and into `docs/performance-evidence.md`
   **Then** only timing aggregates (p95/min/median/max) and scenario names are emitted — **never** report rows,
   ledger rows, CSV content, comments, contributor/target identifiers, payloads, tokens, or any personal data.

## Tasks / Subtasks

- [x] **Task 1 — Add the NFR11 report/export/dashboard performance lane (AC: 1, 2, 3, 5)**
  - [x] Create `tests/Hexalith.Timesheets.IntegrationTests/ReportExportDashboardQueryPerformanceLaneTests.cs`, mirroring the **structure** of `CaptureAndGovernanceCommandPerformanceLaneTests.cs` (the 1.11 template): a sealed class taking `ITestOutputHelper`, a single `[Fact]` (e.g. `Report_export_and_dashboard_query_latency_records_nfr11_p95_evidence`).
  - [x] Reuse the **exact** opt-in gate from 1.11 — `private const string OptInVariable = "TIMESHEETS_PERF";` and, as the first lines of the `[Fact]`: `if (Environment.GetEnvironmentVariable(OptInVariable) != "1") { Assert.Skip($"Set {OptInVariable}=1 to run the report/export/dashboard performance lane."); }`. Do **not** invent a new env var — a single shared opt-in keeps both lanes out of the fast baseline.
  - [x] Set the target constant `Nfr11TargetMilliseconds = 2000.0` (NOT 500). Reuse 1.11's iteration constants `WarmupIterations = 100`, `MeasuredIterations = 500`.
  - [x] Do **not** delete, rename, or weaken the reserved static-skip placeholders `PerformanceEvidenceLaneTests.Performance_lane_is_reserved_for_launch_latency_evidence` and `InfrastructureLaneTests.Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence` — they are fitness-protected ([PerformanceEvidenceTests.cs:30-31]).

- [x] **Task 2 — Seed realistic in-process fixtures by cloning the existing integration seeds (AC: 1, 2)**
  - [x] **Do not build a parallel fixture library.** Each integration test already constructs a fully-seeded, in-process version of the exact service this lane measures. Clone its arrange block (seeded in-memory projection reader + `AllowAllAccessGuard`-style guard), then measure only the query call:
    - Report (AC1 core): clone `ActualTimeReportQueryServiceIntegrationTests.cs` → measure `ActualTimeReportQueryService.QueryProjectAsync` / `QueryWorkAsync` ([Server/OperationalReports/ActualTimeReportQueryService.cs:46-96]). The production reader is the fail-closed `UnavailableActualTimeReportProjectionReader` (returns `null`), so you **must** inject the seeded in-memory reader the integration test uses, or every scenario short-circuits to `NotFoundOrDenied`.
    - Ledger: clone `ApprovedTimeLedgerQueryServiceIntegrationTests.cs` → measure `ApprovedTimeLedgerQueryService.QueryAsync` ([Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs:30-92]).
    - Export + preview: clone `ApprovedTimeExportIntegrationTests.cs` / `ApprovedTimeExportPreviewIntegrationTests.cs` → measure `ApprovedTimeExportService.GenerateAsync` and `PreviewAsync` ([Server/Exports/ApprovedTimeExportService.cs:44-49, 156-160]). Both share `EvaluateAsync` → `LoadDisclosedLedgerAsync` ([:209-264, :337-377]) which loops the **full** ledger cursor — seed enough rows to force **multi-page** traversal so the p95 reflects real export scope, not a single page.
    - Dashboard: clone `TimesheetsDashboardOverviewIntegrationTests.cs` → measure `TimesheetsDashboardOverviewQueryService.QueryAsync` ([Server/Dashboard/TimesheetsDashboardOverviewQueryService.cs:36-39]). This composes 5 downstream reads (entry list ×2, ledger, project report, work report) — it is the most expensive fan-out path; seed all of its inputs.
    - Works planned-effort: the host registers the fail-closed `UnavailableWorkPlannedEffortProvider` ([Server/Runtime/ServiceCollectionExtensions.cs:72]); to exercise the `Supplied` per-row Works call you must compose `WorksQueryWorkPlannedEffortProvider` with a **faked `IWorksQueryChannel`** (mirror `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderTests.cs`) or inject a seeded `StaticPlannedEffortProvider` (as `ActualTimeReportQueryServiceIntegrationTests` already does), and drive the **Work** report through it.
  - [x] Build each fixture **once, outside the measured loop**, so only the query call is timed (1.11 convention). Seed by **filter combination** for the report path: (tenant only), (tenant + `Project`), (tenant + `TenantLocalPeriodKey`), (tenant + `Project` + `TenantLocalPeriodKey`) — covering the tenant/project/period dimensions NFR11 names ([epics.md:1651]). Period keys are monthly `"YYYY-MM"` buckets ([Projections/OperationalReports/ActualTimeReportProjection.cs:512-516]).

- [x] **Task 3 — Reuse the shared p95 harness and emit privacy-safe evidence (AC: 1, 3, 5)**
  - [x] Reuse `PerformanceStatistics.NearestRankPercentile(durations, 0.95)` ([tests/Hexalith.Timesheets.IntegrationTests/PerformanceStatistics.cs]) — already unit-tested by `PerformanceStatisticsTests`; do **not** add a second percentile implementation. Median = `NearestRankPercentile(durations, 0.50)`; min/max from the sorted array.
  - [x] Mirror 1.11's `MeasureAsync` harness: warm-up loop (100, discarded) → measured loop (500) timing each call with `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime(start).TotalMilliseconds` → `Array.Sort` → compute stats. **Assert the real result every iteration** (e.g. report `result.WasDisclosed` + expected row count; ledger freshness/`CanUseForExport`; export `Ready` + `Scope.RowCount`; preview `Ready` rows-free; dashboard composed sections) so a no-op can't read as fast (Epic 1 verification-depth lesson).
  - [x] Keep the only latency assertion a **generous sanity bound behind the gate**: `P95Milliseconds.ShouldBeLessThan(Nfr11TargetMilliseconds, ...)` per scenario — never a hard CI gate in the default suite.
  - [x] Emit a per-scenario `Scenario | p95 (ms) | min (ms) | median (ms) | max (ms)` table + a worst-case line via `ITestOutputHelper`. **NFR12:** only scenario names + timing aggregates — no rows, CSV, comments, identifiers, payloads, or PII.

- [x] **Task 4 — Record measured evidence, verdicts, and the waiver in docs (AC: 2, 4, 5)**
  - [x] Add a new `## NFR11 — Report, export, and dashboard query latency evidence` section to `docs/performance-evidence.md` (after the NFR10 section), mirroring the NFR10 layout: a `### Measured run` block (Test name, `TIMESHEETS_PERF=1` opt-in, `100 warm-up (discarded) + 500 measured per scenario; p95 via sorted nearest-rank`, machine/runtime), the per-scenario results table, a worst-case line, a `### NFR11 comparison and verdict` block, and a `### Scope and honesty constraint` block.
  - [x] Classify each path's verdict (`pass / concern / fail / waived`) vs `2s p95` (AC2/AC4). Record the **in-process** report/ledger/export/preview/dashboard/Works paths with their measured verdict, and record the **EventStore-backed wire path** as **`Verdict: waived (deferred)`** with owner (Story 4.10 → Epic 5 aggregates), risk, and revisit condition (needs runtime EventStore-backed persisted fixtures) — do not overstate the in-process number as proving the wire path.
  - [x] **Preserve every existing fitness literal** already asserted in the doc — `500 ms p95`, `2s p95`, `EventStore-backed write path`, `read models`, `fast unit baseline`, `NFR10`, `command acknowledgement`, `pass / concern / fail / waived`, `Verdict: pass`, `waived`, `How to run the performance lane`, `TIMESHEETS_PERF=1`, `skipped by default` ([PerformanceEvidenceTests.cs:12-16, 41-45, 82-84]). Extend the "How to run" block with the new lane's `-class "Hexalith.Timesheets.IntegrationTests.ReportExportDashboardQueryPerformanceLaneTests"` invocation.
  - [x] Extend `README.md` "Build and Test" ([README.md:29-39]) with an NFR11 paragraph mirroring the existing NFR10 `TIMESHEETS_PERF=1` block and run command; keep the line pointing readers to `docs/performance-evidence.md`.

- [x] **Task 5 — Extend the fitness guards (AC: 3, 4)**
  - [x] In `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`, **add** assertions (do not weaken existing ones): the evidence doc contains the NFR11 verdict vocabulary/section markers (e.g. `NFR11`, a report/export/dashboard `Verdict:` line, `waived (deferred)`), and the integration source contains the new lane class name `ReportExportDashboardQueryPerformanceLaneTests`. The existing `TIMESHEETS_PERF` / `Assert.Skip` source checks already cover the new lane's gate (combined-source scan), but add a focused assertion if it sharpens intent.
  - [x] Confirm the fast-baseline guard still holds: without `TIMESHEETS_PERF=1`, the new `[Fact]` skips and the fast unit/architecture baseline test counts are unchanged.

- [x] **Task 6 — Build, test, verify, report (all ACs)**
  - [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` then `... dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` (expect **0 Warning(s), 0 Error(s)**; ignore transient `Hexalith.PolymorphicSerializations` submodule pack/IDE0065 noise that self-clears — it is not in the `.slnx` and is not touched here).
  - [x] Run affected suites via the **built xUnit v3 executables** (VSTest socket is blocked in the sandbox: `SocketException (13): Permission denied`): IntegrationTests (fast baseline — the new lane must show as **skipped**) and ArchitectureTests (fitness). Then run the lane **opted-in once** to capture numbers: `TIMESHEETS_PERF=1 ... tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.ReportExportDashboardQueryPerformanceLaneTests" -xml /tmp/perf-evidence.xml` and paste the measured p95 table into `docs/performance-evidence.md`.
  - [x] Generate the File List from the actual `git diff` and report **exact** test counts from the built executables (no estimates — this is an enforced gate; Stories 1.10/1.11/4.8/4.9 were caught reporting stale counts). Baseline counts to move from: Integration **80** (77 pass + 3 perf/infra skips → the new lane adds a **4th** default skip, so expect **81**, 77 pass + 4 skips, when fixtures land), Architecture **29**. Budget for ≥1 review-found patch.

## Dev Notes

### TL;DR — extend the 1.11 lane to the read paths; measure what's runnable, waive the wire path

Story 1.11 built the performance-lane machine (`TIMESHEETS_PERF=1` opt-in, `Assert.Skip`, 100 warm-up + 500 measured,
`PerformanceStatistics.NearestRankPercentile` p95, `ITestOutputHelper` table, `docs/performance-evidence.md` verdict).
Your deliverable: **(1)** a new lane class that drives the **real** report/ledger/export/preview/dashboard/Works
query services over **seeded in-memory fixtures cloned from the existing integration tests**, **(2)** a measured
NFR11 section in `docs/performance-evidence.md` with per-path `pass/concern/fail/waived` verdicts vs `2s p95` and a
`waived (deferred)` EventStore-backed wire path, **(3)** extended fitness guards, **(4)** a README note, **(5)** exact
build/test evidence. Do **not** build a new percentile helper, a new fixture framework, a new env var, or a separate
perf project; do **not** add EventStore/Dapr/Aspire infrastructure; do **not** touch the reserved placeholders.

### What to measure — concrete service paths (verified at baseline `24c6e23`)

The IntegrationTests project is **infrastructure-free** (references only Contracts/Projections/Server) — measure
in-process, no containers/network. The five typed query contract surfaces are `QueryTimeEntries`,
`QueryApprovedTimeLedger`, `QueryProjectActualTimeReport`, `QueryWorkActualTimeReport`,
`QueryTimesheetsDashboardOverview` ([architecture.md:444]). Paths and their seeding precedents:

- **Report (Story 4.3 — the core NFR11 target).** `ActualTimeReportQueryService.QueryProjectAsync` /
  `QueryWorkAsync` ([src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs:46-96]):
  tenant-first `ProjectionRead` authorize → projection reader → per-row authorization (`CanFilterRow` only filters
  `InsufficientRole`) → Work-row planned-effort call memoized by workId. Backing projection
  `ActualTimeReportProjection` (`ProjectionName = "actual-time-report"`) folds approved evidence from the ledger
  projection + selected rows from `TimeEntryEvidenceListProjection`, rolling up by Target/period/Contributor/
  ActivityType/BillableState/ApprovalState/ContributorCategory; offset cursor paging, `PageSize=50`. Filter shape
  `QueryProjectActualTimeReport`/`QueryWorkActualTimeReport`: `Project`/`Work`, `Contributor`, `ActivityTypeId`,
  **`TenantLocalPeriodKey`**, `ServiceDateFrom`/`To`, `BillableState`, `ApprovalState`, `CurrentRowsOnly=true`,
  `SortBy`, `SortDirection`, `PageSize=50`, `Cursor`. **Production reader = fail-closed
  `UnavailableActualTimeReportProjectionReader` (returns `null`) — inject the seeded in-memory reader from
  `ActualTimeReportQueryServiceIntegrationTests`** or every measured call short-circuits to `NotFoundOrDenied`.
- **Approved-Time Ledger query (Story 4.2).** `ApprovedTimeLedgerQueryService.QueryAsync`
  ([src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs:30-92]) → projection
  `ApprovedTimeLedgerProjection` (`"approved-time-ledger"`). **Hot spot:** per-candidate full-stream re-fold
  (`foreach … CandidateIds(eventList) { _evidenceProjection.Project(...) }`), O(N×M) over N distinct entries × M
  events — seed realistic N and M so the 2s-p95 evidence is meaningful (this was an acknowledged-LOW in the 4.2
  review). Freshness via `ProjectionFreshnessMetadataMapper.ToMetadata`.
- **Export + preview (Stories 4.5 / 4.9).** `ApprovedTimeExportService.GenerateAsync` (writes CSV + audit on
  `Ready`) and `PreviewAsync` (side-effect-free: no CSV, no audit) share `EvaluateAsync` →
  **`LoadDisclosedLedgerAsync` loops the ledger cursor to accumulate the FULL disclosed scope across all pages**
  ([src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs:209-264, 337-377]). Export/preview is
  therefore heavier than a single ledger page — **seed enough rows to force multi-page traversal**. Preview measures
  the same traversal minus CSV/audit; measure both to show the cost delta.
- **Dashboard overview (Story 4.7).** `TimesheetsDashboardOverviewQueryService.QueryAsync`
  ([src/Hexalith.Timesheets.Server/Dashboard/TimesheetsDashboardOverviewQueryService.cs:36-39]) **composes 5
  sequential downstream reads** (`TimeEntryEvidenceListQueryService` ×2 — current period + approval workload,
  `ApprovedTimeLedgerQueryService`, `ActualTimeReportQueryService.QueryProjectAsync` + `QueryWorkAsync`) — the most
  expensive fan-out path; seed all inputs. Entry/ledger sub-queries use `PageSize=200`, report sub-queries `50`.
- **Works planned-effort (Story 4.8).** `WorksQueryWorkPlannedEffortProvider.GetPlannedEffortAsync`
  ([src/Hexalith.Timesheets.Works/WorksQueryWorkPlannedEffortProvider.cs:56-59]) issues the Works `get-work-item`
  query via `IWorksQueryChannel`. The host does **not** call `AddTimesheetsWorksPlannedEffortReporting()`
  ([src/Hexalith.Timesheets.Works/WorksPlannedEffortReportingServiceCollectionExtensions.cs]) — production registers
  the fail-closed `UnavailableWorkPlannedEffortProvider` ([Server/Runtime/ServiceCollectionExtensions.cs:72]). To
  exercise the `Supplied` path, compose the adapter with a **faked `IWorksQueryChannel`** (mirror
  `WorksQueryWorkPlannedEffortProviderTests`) or inject a seeded `StaticPlannedEffortProvider` (as
  `ActualTimeReportQueryServiceIntegrationTests` does) and drive the Work report through it.

Seeding templates already exist for **every** path: `ActualTimeReportQueryServiceIntegrationTests.cs`,
`ApprovedTimeLedgerQueryServiceIntegrationTests.cs`, `ApprovedTimeExportIntegrationTests.cs`,
`ApprovedTimeExportPreviewIntegrationTests.cs`, `TimesheetsDashboardOverviewIntegrationTests.cs`,
`OperationalTimeEntryQueryServiceIntegrationTests.cs` (all in `tests/Hexalith.Timesheets.IntegrationTests/`). Clone
their arrange blocks; do not re-derive seeding from scratch.

### The performance-lane pattern to replicate (from Story 1.11)

- **Opt-in + dynamic skip:** `private const string OptInVariable = "TIMESHEETS_PERF";` →
  `if (Environment.GetEnvironmentVariable(OptInVariable) != "1") { Assert.Skip(...); }` as the first lines of the
  `[Fact]`. No trait/category, no separate project, no msbuild flag, no static `[Fact(Skip=...)]` (that pattern is
  reserved for the EventStore placeholders). xUnit v3 dynamic `Assert.Skip` is available in the pinned `3.2.2`.
- **Harness:** `WarmupIterations = 100` (discarded), `MeasuredIterations = 500`; `Stopwatch.GetTimestamp()` /
  `Stopwatch.GetElapsedTime(start).TotalMilliseconds` per iteration; `Array.Sort` then
  `PerformanceStatistics.NearestRankPercentile`. Build fixtures **once outside** the measured loop.
- **Verification depth:** assert the real result every iteration (Epic 1 lesson — a no-op must not read as fast).
- **Target:** NFR11 `2s p95` = `2000.0` ms; sanity-bound assertion behind the gate only.
- **Evidence emission:** `ITestOutputHelper` table (`Scenario | p95 | min | median | max`, `:F4`) + worst-case line;
  timing aggregates + scenario names only (NFR12).
- **No hard CI gate, no flaky bound** in the default suite. The `-xml` report captures the output for the doc.

### Architecture & boundary constraints

- **EventStore stays the only authoritative boundary; this story is read-only and adds no infrastructure.** Measure
  the in-process composed read path that is runnable now ([architecture.md:983]); the EventStore-backed wire path is
  legitimately recorded `waived (deferred)` pending realistic persisted fixtures. NFR11 report/export/dashboard
  evidence ownership = Story 4.10; Epic 5 aggregates, not re-measures ([epics.md:778, 1664-1666]).
- **No new perf test project.** The lane rides inside `tests/Hexalith.Timesheets.IntegrationTests/` (the same place
  exports' golden files and the NFR10 lane live) — the architecture does not pre-authorize a separate perf project,
  so adding one would be out-of-scope churn.
- **No new packages** — `Stopwatch` and `Environment.GetEnvironmentVariable` are BCL; `PerformanceStatistics` is
  reused. Central Package Management forbids inline `<PackageReference Version>`.
- **Module boundaries:** `Projections` owns read-model handlers/freshness/ledger/operational report models; `Server`
  owns query/export orchestration; `Contracts` owns the query shapes ([architecture.md:892-899, 943-948]). Don't add
  read logic — only measure existing services.
- **No HTTP/UI:** the export service is registered-but-not-HTTP-mapped today; measure the service layer, not an HTTP
  round-trip (no `WebApplicationFactory`/`TestHost`) — consistent with 1.11.
- **Freshness honesty:** measure the **fresh/ready** path; a stale/unavailable projection short-circuits and would
  produce a misleadingly "fast" empty result — seed `Fresh` checkpoints and assert it ([architecture.md:721]).

### Fail-closed & no-disclosure (NFR8, NFR12)

- Every measured service keeps its tenant-first + per-row authorization gate; the lane uses an allow-all guard only
  to reach the real read path (mirroring 1.11's `AllowAllAccessGuard`), not to bypass correctness assertions.
- Evidence must surface **no** disclosed content — only timing + scenario names. Do not log report/ledger rows, CSV,
  comments, or identifiers in the lane, the doc, or the `-xml` capture (NFR12).

### Testing standards

- xUnit v3 (`3.2.2`) · Shouldly (`4.3.0`) · NSubstitute (`6.0.0-rc.1`, for the faked `IWorksQueryChannel`). Test
  method names PascalCase. Run projects individually via built executables (not solution-level `dotnet test`).
- Sandbox: VSTest socket blocked (`SocketException (13): Permission denied`). Build first, then run the built
  executable directly, e.g. `./tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests`.
  Set `TIMESHEETS_PERF=1` on the **same** command to capture numbers.
- Use `TestRepositoryRoot` / `RepositoryRoot.PathTo(...)` to locate `docs/performance-evidence.md`; do not hand-roll
  path discovery.
- Keep the percentile math covered by the existing `PerformanceStatisticsTests` (fast baseline) — the recorded
  verdict rests on tested math, not only the opt-in lane.

### Previous story intelligence

- **Story 1.11 (NFR10 command evidence, the direct twin)** built the lane machine and explicitly scoped
  report/export/dashboard query latency to **this story** ([1-11 Dev Notes; architecture.md:983]). It set: env-var
  opt-in + `Assert.Skip`; 100/500 iterations; nearest-rank p95 extracted to a unit-tested `PerformanceStatistics`;
  honest in-process-vs-wire-path scope with the wire path `waived/deferred`; File-List + exact-count discipline
  (its review caught an omitted helper file and stale counts). Reserved markers `PerformanceEvidenceLaneTests` and
  `InfrastructureLaneTests` are fitness-protected — keep them.
- **Story 4.9 (just merged at `24c6e23`)** confirmed the export/preview shared readiness core (`EvaluateAsync` →
  `LoadDisclosedLedgerAsync` full cursor loop) and its baseline test counts (Contracts 88, Server 420, Integration
  80·3-skip, Architecture 29, Projections 77). Its review re-affirmed the enforced count-discipline gate (stale
  78→80 fix) and the VSTest-socket workaround.
- **Stories 4.2 / 4.3 / 4.5 / 4.7 / 4.8** established the services this lane measures and their seeded integration
  tests — reuse those seeds. 4.2's per-candidate re-fold and 4.5's full-cursor export traversal are the dominant
  costs to exercise at realistic volume.
- **House gates inherited from 4.8/4.9:** exact counts from built executables (no estimates), File List from `git
  diff`, fail closed / never fabricate state, budget for ≥1 review-found patch.

### Git intelligence

- Recent cadence: `24c6e23 feat(story-4.9): Resolve and Implement Approved Export Preview Behavior`,
  `ab4a012 feat(story-4.8): Implement Works Planned-Effort Reporting Adapter`,
  `c4a9183 docs(epic-3): complete closure retrospective`. Use a `feat(story-4.10):` Conventional Commit; branch
  `feat/<desc>`. No new package families, no dependency upgrades, no submodule changes.
- Working tree at story creation has an unrelated modified file
  (`_bmad-output/story-automator/orchestration-1-20260622-124648.md`). Do not revert or bundle it.

### Latest tech information

- .NET 10 / C# 14, SDK pinned `10.0.301` (`global.json`, rollForward latestPatch), `.slnx` only, Central Package
  Management (no inline versions), nullable + implicit usings, file-scoped namespaces, Allman braces, `_camelCase`
  private fields, `Async` suffix, `ConfigureAwait(false)` on awaited production calls, `-warnaserror`. Measurement
  uses BCL `System.Diagnostics.Stopwatch` only — no BenchmarkDotNet, no new packages.
- No external/library research required: no new framework, API, or dependency selection; every consumed type and
  every seeding template already ships in the repo.

### Project context reference

Follow `Hexalith.AI.Tools/hexalith-llm-instructions.md` and the root `CLAUDE.md`. Read-only, infra-free test story:
persist nothing, add no infrastructure, reuse the existing lane machine and integration seeds rather than
duplicating. Sibling `project-context.md` files exist for EventStore/Tenants/Parties/Conversations/Projects/
FrontComposer; do **not** initialize nested submodules.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.10] (lines 1639-1662) — ACs; requirement NFR11.
- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.11] (lines 755-778) — NFR10 twin; "Epic 5 only aggregates this evidence instead of creating the first measurement path".
- [Source: _bmad-output/planning-artifacts/epics.md] (line 269) — NFR11 = "Report query <=2 s p95"; primary 4.10. (line 405) — "measured NFR10/NFR11 evidence is owned by Story 1.11 … and Story 4.10 for report/export/dashboard paths".
- [Source: _bmad-output/planning-artifacts/architecture.md] (line 61) — "common report queries target 2 seconds p95 for tenant/project/period filters". (line 983) — `TIMESHEETS_PERF=1` opt-in lane, in-process vs EventStore-backed wire path, Story 4.10 ownership, `docs/performance-evidence.md`. (line 444) — five typed query contract surfaces. (lines 892-899, 943-948) — module boundaries + reporting/export test ownership.
- [Source: docs/performance-evidence.md] — the evidence doc to extend (NFR11 target already on line 6; reserved report/query measurements on lines 58-59). Preserve all fitness literals.
- [Source: tests/Hexalith.Timesheets.IntegrationTests/CaptureAndGovernanceCommandPerformanceLaneTests.cs] — the lane template to mirror. [PerformanceStatistics.cs] / [PerformanceStatisticsTests.cs] — reuse the p95 math. [PerformanceEvidenceLaneTests.cs] / [InfrastructureLaneTests.cs] — reserved placeholders, do not touch.
- [Source: tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs] — fitness guard to extend (existing literals lines 12-16, 41-45, 82-84; lane-name + gate checks lines 30-32, 54, 71-72).
- [Source: tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs · ApprovedTimeLedgerQueryServiceIntegrationTests.cs · ApprovedTimeExportIntegrationTests.cs · ApprovedTimeExportPreviewIntegrationTests.cs · TimesheetsDashboardOverviewIntegrationTests.cs · OperationalTimeEntryQueryServiceIntegrationTests.cs] — seeding templates to clone.
- [Source: src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs · ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs · Exports/ApprovedTimeExportService.cs · Dashboard/TimesheetsDashboardOverviewQueryService.cs] · [src/Hexalith.Timesheets.Works/WorksQueryWorkPlannedEffortProvider.cs] · [Server/Runtime/ServiceCollectionExtensions.cs#L72] — services to measure + production fail-closed wiring.
- [Source: src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs · ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs · ProjectionFreshnessMetadataMapper.cs] — backing projections / cost hot spots.
- [Source: README.md#Build-and-Test] (lines 29-39) — NFR10 lane note to mirror for NFR11.

### Open Questions (recommended defaults applied; a change is a one-line switch + matching test)

- **Q-A — One lane class or per-path classes?** Default: a **single** `ReportExportDashboardQueryPerformanceLaneTests`
  with one `[Fact]` driving all paths as named scenarios (mirrors 1.11's single-class-11-scenarios shape; one gate,
  one evidence table). Alternative: split per path — rejected as gate/skip duplication and extra fitness churn.
- **Q-B — Report authorization gate in the lane.** Default: allow-all guard to reach the real read path (1.11
  precedent), with the real result asserted each iteration. Alternative: measure through a realistic role — out of
  scope; authorization correctness is owned by 4.1-4.7, this story measures latency.
- **Q-C — EventStore-backed wire path.** Default: record `waived (deferred)` with owner/risk/revisit; do not build
  EventStore/Dapr/Aspire fixtures (none exist). Alternative: build persisted fixtures now — rejected; explicitly
  reserved by 1.11/architecture for a later data-bearing story, and would slow/flaky the lane.
- **Q-D — Works planned-effort in the lane.** Default: compose `WorksQueryWorkPlannedEffortProvider` with a faked
  `IWorksQueryChannel` so the `Supplied` per-row path is exercised in the Work report scenario. Alternative: measure
  only the `Unavailable` default — rejected; it would hide the planned-effort fan-out cost NFR11 cares about.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-22: Activation completed with `bmad-dev-story`; workflow customization resolved with no prepend/append steps and persistent project-context files loaded.
- 2026-06-22: Focused red check confirmed the new NFR11 fitness guard failed before the evidence section existed.
- 2026-06-22: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` completed successfully.
- 2026-06-22: First solution build hit the known transient `Hexalith.PolymorphicSerializations` IDE0065/pack noise; immediate rerun of `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` succeeded with 0 warnings and 0 errors.
- 2026-06-22: Opt-in NFR11 lane passed: IntegrationTests total 1, passed 1, skipped 0; worst p95 6.0908 ms for dashboard overview fan-out.
- 2026-06-22: Default fast IntegrationTests executable passed: total 81, failed 0, skipped 4, passed 77 (dev-story snapshot, before the QA E2E suite landed).
- 2026-06-22: ArchitectureTests executable passed: total 31, failed 0, skipped 0, passed 31 (dev-story snapshot, before the QA fitness facts landed).
- 2026-06-22 (review re-run): After the `bmad-qa-generate-e2e-tests` suite added `ReportExportDashboardReadJourneyE2ETests` (6 facts) + 2 fitness facts, the authoritative built-executable counts are: IntegrationTests total **87**, failed 0, skipped 4, passed **83**; ArchitectureTests total **33**, failed 0, skipped 0, passed **33**. Solution build clean (0 Warning(s), 0 Error(s)). Opt-in NFR11 lane re-run green (total 1, passed 1). The earlier 81/31 snapshots above were correct at dev-story time but were not synced after QA; these review counts supersede them.

### Implementation Plan

- Reused the Story 1.11 performance-lane shape: one xUnit v3 class, shared `TIMESHEETS_PERF` opt-in, 100 warm-up iterations, 500 measured iterations, `Stopwatch`, and `PerformanceStatistics.NearestRankPercentile`.
- Built the fixtures once outside measured loops with seeded in-memory projection readers and an allow-all access guard, then measured only the real service calls.
- Used the existing static supplied Works planned-effort fixture pattern from `ActualTimeReportQueryServiceIntegrationTests` to avoid adding a new IntegrationTests project reference to `Hexalith.Timesheets.Works`.
- Recorded in-process NFR11 verdicts and kept the EventStore-backed wire path explicitly `waived (deferred)` with owner/risk/revisit.

### Completion Notes List

- Added `ReportExportDashboardQueryPerformanceLaneTests` covering Project report tenant/project/period combinations, Work report with supplied planned effort, Approved-Time Ledger, full-cursor export, full-cursor preview, and dashboard fan-out.
- Seeded 60 approved current ledger rows over project/work targets and tenant-local monthly periods; export and preview run with page size 25 to force multi-page traversal.
- Added per-iteration correctness assertions for disclosure/readiness, expected row counts, freshness, export scope, rows-free preview semantics, and dashboard section counts.
- Extended NFR11 evidence in `docs/performance-evidence.md` with measured p95/min/median/max, pass verdicts, data-volume and functional-correctness notes, and the EventStore-backed `waived (deferred)` path.
- Added README instructions for running the NFR11 lane and Architecture fitness guards for NFR11 section/verdict/waiver presence plus the new opt-in class.
- Follow-up `bmad-qa-generate-e2e-tests` run added `ReportExportDashboardReadJourneyE2ETests` (6 fast-baseline facts covering the report/ledger/export/preview/dashboard composition plus a fail-closed denied-authorization case for NFR8) and 2 extra `PerformanceEvidenceTests` fitness facts (AC5/NFR12 privacy-safe emission guard; AC3 fast-baseline functional-coverage guard). These run by default (no opt-in) and raised the suite counts to IntegrationTests 87 / ArchitectureTests 33 (see Debug Log review re-run); the summary lives in `_bmad-output/implementation-artifacts/tests/4-10-test-summary.md`.

### File List

- README.md
- _bmad-output/implementation-artifacts/4-10-add-report-export-and-dashboard-performance-evidence.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/performance-evidence.md
- tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs
- tests/Hexalith.Timesheets.IntegrationTests/ReportExportDashboardQueryPerformanceLaneTests.cs
- tests/Hexalith.Timesheets.IntegrationTests/ReportExportDashboardReadJourneyE2ETests.cs
- _bmad-output/implementation-artifacts/tests/4-10-test-summary.md

### Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-22 · **Outcome:** Approve (auto-fix applied)

**Scope verified against git reality (baseline `24c6e23`):** README.md, docs/performance-evidence.md, PerformanceEvidenceTests.cs (M); ReportExportDashboardQueryPerformanceLaneTests.cs, ReportExportDashboardReadJourneyE2ETests.cs, tests/4-10-test-summary.md (new); sprint-status.yaml + story file. The unrelated `orchestration-1-20260622-124648.md` was left untouched per the story's git intelligence note.

**Acceptance criteria:** AC1–AC5 all IMPLEMENTED and verified. The opt-in lane runs green (1/1) and asserts the real disclosed result + row/readiness/freshness counts every iteration (AC1 verification-depth); report/ledger/export/preview/dashboard/Works-planned-effort paths are all measured with classified evidence (AC2); the lane stays gated behind `TIMESHEETS_PERF=1` with `Assert.Skip` and the fast baseline keeps 4 default skips (AC3); the evidence doc carries per-path `pass` verdicts vs `2s p95` plus the EventStore-backed `waived (deferred)` wire path, and fitness guards protect the section/lane/opt-in (AC4); emission is timing-aggregates + scenario-names only, doc-guarded for NFR12 (AC5). Reserved placeholders untouched; all prior fitness literals preserved (33/33 architecture tests pass).

**Findings (auto-fixed under non-interactive review):**
- HIGH — Dev Agent Record reported stale suite counts (Integration 81 / Architecture 31) after the `bmad-qa-generate-e2e-tests` follow-up added 6 E2E facts + 2 fitness facts. Corrected to the authoritative built-executable counts: Integration **87** (83 pass + 4 skip), Architecture **33**. (Enforced count-discipline gate.)
- MEDIUM — File List omitted `ReportExportDashboardReadJourneyE2ETests.cs` and `tests/4-10-test-summary.md`; both added.
- LOW — Completion Notes did not disclose the QA-added E2E suite/fitness facts; documented.
- LOW (not changed, by convention) — Helper/fixture duplication between the two test files; the story sanctioned the clone-the-arrange-block pattern and every existing integration test in this project builds its own fixture, so a shared base would diverge from the surrounding convention.

No CRITICAL issues remain after fixes → status set to **done**.

### Change Log

| Date | Version | Description |
|------|---------|-------------|
| 2026-06-22 | 1.1 | Senior Developer Review (AI): verified AC1–AC5 against git reality and built-executable runs; auto-fixed stale suite counts (Integration 87, Architecture 33), completed the File List (E2E suite + test summary), and documented the QA-added coverage. 0 critical issues; Status → done. |
| 2026-06-22 | 1.0 | Implemented NFR11 report/export/dashboard performance lane, recorded measured evidence and waiver, extended README and fitness guards, and validated restore/build/test gates. Status → review. |
| 2026-06-22 | 0.1 | Story 4.10 drafted via create-story: NFR11 report/export/dashboard performance-evidence lane extending the Story 1.11 `TIMESHEETS_PERF=1` machine; measures in-process report/ledger/export/preview/dashboard/Works-planned-effort paths over seeded fixtures vs `2s p95`, records per-path verdicts + `waived (deferred)` EventStore wire path in `docs/performance-evidence.md`, extends fitness guards + README. Status → ready-for-dev. |
