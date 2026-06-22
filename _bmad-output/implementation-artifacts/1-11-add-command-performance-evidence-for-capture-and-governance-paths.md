---
baseline_commit: 3731140
---

# Story 1.11: Add Command Performance Evidence for Capture and Governance Paths

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a quality owner,
I want command acknowledgement evidence for capture and governance paths,
so that NFR10 is measured where hot command behavior is introduced rather than only at the final release gate.

## Acceptance Criteria

1. Given realistic tenant, Party, Project, Work, Activity Type, and Time Entry command fixtures exist, when the isolated performance lane runs capture and governance command scenarios, then common command acknowledgements target `500 ms p95` in a warmed local service, and deviations are recorded as explicit launch-readiness evidence.
2. Given the fast unit and architecture baseline runs, when performance fixtures are unavailable or intentionally skipped, then skipped performance tests remain isolated and do not slow or destabilize the fast baseline, and docs explain how to run the performance lane.
3. Given performance evidence is reviewed, when capture/governance command results are compared with NFR10, then the result is marked pass, concern, fail, or waived, and Epic 5 only aggregates this evidence instead of creating the first measurement path.

## Tasks / Subtasks

- [x] Add an isolated, opt-in command performance lane that measures capture + governance command-acknowledgement latency (AC: 1, 2)
  - [x] Add a new test class in `tests/Hexalith.Timesheets.IntegrationTests/` (suggested name `CaptureAndGovernanceCommandPerformanceLaneTests.cs`) that drives the **existing in-process command services** (no new production code) and measures acknowledgement latency. Reuse the established E2E construction pattern: a local `AllowAllAccessGuard : ITimesheetsAccessGuard`, direct `new TimeEntryCommandService(accessGuard)` etc., and the in-memory `TimeEntryState`/catalog builders already used by the E2E tests in this project. Do NOT spin up EventStore/Dapr/Aspire/containers/network.
  - [x] Cover **capture**: `RecordTimeEntry` via `TimeEntryCommandService.RecordAsync(...)` (acknowledgement = `TimeEntryCommandResult.WasDispatched`).
  - [x] Cover **governance**: entry submission (`TimeEntrySubmissionCommandService.SubmitAsync` → `TimeEntrySubmissionCommandResult.HasAcceptedEvents`), approval/rejection (`TimeEntryApprovalCommandService.ApproveAsync`/`RejectAsync` → `TimeEntryApprovalCommandResult`), rejected-entry correction and approved-entry correction (`TimeEntryCorrectionCommandService` → `TimeEntryCorrectionCommandResult`), period submission/approval (`TimesheetPeriodSubmissionCommandService`, `TimesheetPeriodApprovalCommandService`), and Activity-Type catalog governance (`TenantActivityTypeCommandService`, `ProjectActivityTypeCommandService` → `ActivityTypeCommandResult`). Copy each service's exact constructor wiring (some take extra collaborators such as an approval-authority resolver) from the matching E2E test listed in "In-Process Command Harness Map" below — do not invent new dependencies.
  - [x] Implement a warm-up + measurement loop: run a discard warm-up pass (e.g. 50–100 iterations) so the service path is JIT-warmed, then time a measured pass (e.g. 200–1000 iterations) per scenario with `System.Diagnostics.Stopwatch.GetTimestamp()`/`Stopwatch.GetElapsedTime(...)`, and compute the **p95** from the sorted per-iteration durations. Assert each acknowledgement result is the expected dispatched/accepted outcome so a regression that turns a command into a no-op cannot pass as "fast".
  - [x] Keep timing **out of the fast baseline and non-flaky**: gate the measured lane on an explicit opt-in (recommended: `Environment.GetEnvironmentVariable("TIMESHEETS_PERF")`), and when it is unset call xUnit v3 `Assert.Skip("Set TIMESHEETS_PERF=1 to run the command performance lane.")` so the test dynamically skips in the normal `dotnet test` run. Do NOT add a hard p95 latency `ShouldBeLessThan` gate to the default suite — surface measured p95 as recorded evidence, not a brittle CI failure. If any latency assertion is included at all, make it a generous sanity bound only and only inside the opt-in branch.
  - [x] Print/emit the per-scenario p95 (and min/median/max) via `ITestOutputHelper` so a manual run produces copy-pasteable evidence numbers for `docs/performance-evidence.md`.
- [x] Preserve the reserved EventStore-backed performance placeholder and the fitness contract that guards it (AC: 2)
  - [x] Keep `tests/Hexalith.Timesheets.IntegrationTests/PerformanceEvidenceLaneTests.cs` (the statically-skipped `Performance_lane_is_reserved_for_launch_latency_evidence`) as the reserved marker for the **full EventStore-backed wire/persistence** measurement, which is still blocked on runtime fixtures. Do NOT delete or rename it without also updating the fitness test below — `PerformanceEvidenceTests.Integration_tests_reserve_runtime_and_performance_lanes_without_entering_fast_baseline` asserts the integration source still contains the literal strings `Performance_lane_is_reserved_for_launch_latency_evidence`, `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence`, and `Skip =`.
  - [x] Do NOT touch `InfrastructureLaneTests.Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence` (reserved runtime lane, owned by a later data-bearing story).
- [x] Update `docs/performance-evidence.md` with measured capture/governance command evidence and a pass/concern/fail/waived verdict (AC: 1, 3)
  - [x] Record the measured in-process command-acknowledgement p95 numbers (per scenario, plus the worst-case across capture/governance) from a `TIMESHEETS_PERF=1` run, the iteration/warm-up counts, the machine/runtime note, and the NFR10 `500 ms p95` comparison. Mark each result **pass / concern / fail / waived** and state the verdict for the capture/governance command surface as a whole.
  - [x] Be explicit and honest about scope: this lane measures the **in-process composed command-decision + authorization + result** path, not the full EventStore-backed persistence/Dapr wire path. State that the EventStore-backed end-to-end command-ack measurement remains reserved (still owned here, deferred until runtime fixtures exist) and give its current verdict (e.g. **waived/deferred** with rationale) so Epic 5 aggregates rather than re-measures.
  - [x] Add a short "How to run the performance lane" section: the `TIMESHEETS_PERF=1` opt-in, the exact `dotnet test`/direct-executable command for `Hexalith.Timesheets.IntegrationTests`, and that the lane is skipped by default.
  - [x] **Preserve every substring the fitness test already asserts** in `docs/performance-evidence.md`: `500 ms p95`, `2s p95`, `EventStore-backed write path`, `read models`, and `fast unit baseline`. Re-run `PerformanceEvidenceTests` after editing the doc; if you must change wording, update the fitness assertions in the same change.
- [x] Extend the performance fitness test to assert the new evidence exists (AC: 2, 3)
  - [x] In `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`, add assertions that `docs/performance-evidence.md` now contains the capture/governance command verdict vocabulary (e.g. a pass/concern/fail/waived marker and a "command acknowledgement" / NFR10 reference) and that the new performance lane test name is present in the integration source — so the evidence cannot silently regress to a pure placeholder. Keep the existing assertions intact.
- [x] Document the lane in the README and keep the fast baseline clean (AC: 2)
  - [x] Update `README.md` (Build and Test section) to mention the opt-in `TIMESHEETS_PERF=1` command performance lane and that it is skipped by default, mirroring the existing fallback-command style.
- [x] Verify build and focused test lanes (AC: 1-3)
  - [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`
  - [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` (expect 0 warnings, 0 errors)
  - [x] Run the **default** (no `TIMESHEETS_PERF`) lanes and confirm the perf test dynamically skips and the fast baseline is unaffected: `Hexalith.Timesheets.IntegrationTests` and `Hexalith.Timesheets.ArchitectureTests` (run others if touched).
  - [x] Run the **opt-in** lane once (`TIMESHEETS_PERF=1`) to produce the evidence numbers, then paste them into `docs/performance-evidence.md`.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox (`SocketException (13): Permission denied`), run the built xUnit v3 test executables directly, as documented in Stories 1.1–1.10, and record the reason. The env var must be set on the same invocation (e.g. `TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/.../Hexalith.Timesheets.IntegrationTests`).

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md` (Story 1.11, Story 1.1 performance-scaffold AC, NFR inventory NFR10/NFR11, FR/NFR coverage maps).
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md` (Integration Points "Readiness repair 2026-06-20" performance-ownership note, Test Organization, performance-target decisions, build/test workflow).
- Loaded previous-story intelligence from `1-10-implement-work-reference-validation-adapter.md` and `epic-1-retro-2026-06-19.md` (verification-depth lesson, sandbox test-execution constraints).
- Read the live source for the existing reserved performance/infrastructure lanes, the performance fitness test, the performance-evidence doc, the in-process command E2E harness, and the capture/governance command services + result types (paths cited in References).

### Epic And Story Context

- Epic 1 ("Trusted Time Capture & Activity Governance") shipped the executable capture and governance command surface (record, submit, approve/reject, correct, lock, period submit/approve, Activity-Type catalog governance). Story 1.1 deliberately left an **isolated place for future performance evidence** and assigned NFR10 measurement to **this story** and NFR11 (report/query latency) to Story 4.10. [Source: `epics.md#Story-1-1` perf AC; `epics.md#NFR-Coverage-Map` — NFR10 Primary = 1.11, NFR11 Primary = 4.10]
- Story 1.11 is a readiness-repair follow-up (approved 2026-06-20). It makes NFR10 a measured lane that produces launch-readiness evidence now, so **Epic 5 only aggregates** the evidence/waivers instead of creating the first measurement path. [Source: `architecture.md#Integration-Points` "Readiness repair (approved 2026-06-20)"]
- This story realizes **NFR10** only: "Common command acknowledgements should complete within 500 ms p95 in a warmed local service, subject to architecture sizing." [Source: `epics.md#NonFunctional-Requirements` NFR10]

### Scope Boundaries (read before writing code)

- **In scope:** acknowledgement-latency evidence for **command** paths — capture (record) + governance (submit, approve, reject, correct rejected, correct approved/locking, submit period, approve/reject period, Activity-Type catalog governance). One isolated, opt-in, infra-free performance lane that measures p95, plus evidence doc + fitness assertion + README note.
- **Out of scope (do NOT measure here):** report/export/dashboard **query** latency (NFR11) — that is **Story 4.10**. Do not add report/ledger/export/dashboard timing here; do not duplicate or pre-empt 4.10. Magic-link confirmation and external-contribution commands (Epic 3) are optional/secondary and may be omitted — include only if trivially cheap; the core proof is capture + entry/period/activity-type governance.
- **Do NOT** add production code, change command services, change authorization gates, or weaken any fail-closed default. This is a measurement/evidence story over existing behavior.
- **Do NOT** introduce a flaky hard latency gate into the default test suite, and do NOT let any timing test run in the fast baseline.

### Current Code State To Extend (verified against source)

Existing reserved/placeholder assets — extend, do not replace:

- `tests/Hexalith.Timesheets.IntegrationTests/PerformanceEvidenceLaneTests.cs` — single statically-skipped `[Fact(Skip = "Performance evidence requires realistic EventStore-backed command/report fixtures in a later data-bearing story.")] Performance_lane_is_reserved_for_launch_latency_evidence()`. Keep it as the reserved **EventStore-backed wire-path** marker.
- `tests/Hexalith.Timesheets.IntegrationTests/InfrastructureLaneTests.cs` — reserved runtime lane `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence` (statically skipped). Do not touch.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs` — two fast fitness tests:
  - `Performance_evidence_lane_documents_launch_latency_targets()` reads `docs/performance-evidence.md` and asserts it contains `500 ms p95`, `2s p95`, `EventStore-backed write path`, `read models`, `fast unit baseline`.
  - `Integration_tests_reserve_runtime_and_performance_lanes_without_entering_fast_baseline()` reads the IntegrationTests `*.cs` source and asserts it contains `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence`, `Performance_lane_is_reserved_for_launch_latency_evidence`, and `Skip =`. **Your changes must keep all three literals present in the integration source** (the kept static-skip placeholders satisfy this) or update this fitness test in the same change.
- `docs/performance-evidence.md` — the evidence document (currently states the lane is reserved). This story turns it into recorded measured evidence + verdict while preserving the asserted substrings.
- File-locating helpers to reuse (do not hand-roll path discovery): `RepositoryRoot.PathTo(...)` in `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/RepositoryRoot.cs` and `TestRepositoryRoot.PathTo(...)` in `tests/Hexalith.Timesheets.IntegrationTests/TestRepositoryRoot.cs` (walks up to the dir containing `Hexalith.Timesheets.slnx`).

The IntegrationTests project (`Hexalith.Timesheets.IntegrationTests.csproj`) references only `Contracts`, `Projections`, and `Server` (infra-free) and uses xUnit v3 + Shouldly + `Microsoft.Extensions.DependencyInjection`. The performance lane belongs here and must stay infra-free.

### In-Process Command Harness Map (reuse — do not rebuild)

There is **no shared fixture/builder**; each E2E test constructs services directly with a local `AllowAllAccessGuard : ITimesheetsAccessGuard` (returns `TimesheetsAuthorizationDecision.Allowed()`), builds in-memory `TimeEntryState` by applying recorded events, and supplies a `FreshCatalog(...)` read model. Mirror the closest existing E2E test for each command's exact constructor and call shape:

| Command path (kind) | Command record (`Hexalith.Timesheets.Contracts.Commands.*`) | Service (`Hexalith.Timesheets.Server.*`) → method | Result / acknowledgement | Copy construction from |
|---|---|---|---|---|
| Record draft (capture) | `TimeEntries.RecordTimeEntry` | `TimeEntries.TimeEntryCommandService.RecordAsync` | `TimeEntryCommandResult.WasDispatched` | `SubmitTimeEntriesForApprovalE2ETests.cs`, `AiAssistedTimeCaptureMetricsE2ETests.cs` |
| Submit entries (gov) | `TimeEntries.SubmitTimeEntriesForApproval` | `TimeEntries.TimeEntrySubmissionCommandService.SubmitAsync` | `TimeEntrySubmissionCommandResult.HasAcceptedEvents` | `SubmitTimeEntriesForApprovalE2ETests.cs` |
| Approve / reject (gov) | `TimeEntries.ApproveTimeEntry` / `RejectTimeEntry` | `TimeEntries.TimeEntryApprovalCommandService.ApproveAsync`/`RejectAsync` | `TimeEntryApprovalCommandResult` | `ApproveOrRejectSubmittedTimeEntriesE2ETests.cs`, `ApprovalAuthorityPolicyE2ETests.cs` (has an approval-authority resolver collaborator — copy it) |
| Correct rejected (gov) | `TimeEntries.CorrectRejectedTimeEntry` | `TimeEntries.TimeEntryCorrectionCommandService` | `TimeEntryCorrectionCommandResult` | `CorrectRejectedTimeEntriesForResubmissionE2ETests.cs` |
| Correct approved / locking (gov) | `TimeEntries.CorrectApprovedTimeEntry` | `TimeEntries.TimeEntryCorrectionCommandService` | `TimeEntryCorrectionCommandResult` | `ApprovedEntryCorrectionsE2ETests.cs` |
| Submit period (gov) | `TimesheetPeriods.SubmitTimesheetPeriod` | `TimesheetPeriods.TimesheetPeriodSubmissionCommandService` | `TimesheetPeriodSubmissionCommandResult` | `SubmitTimesheetPeriodE2ETests.cs` |
| Approve / reject period (gov) | `TimesheetPeriods.ApproveTimesheetPeriod` / `RejectTimesheetPeriod` | `TimesheetPeriods.TimesheetPeriodApprovalCommandService` | `TimesheetPeriodApprovalCommandResult` | `ApproveOrRejectTimesheetPeriodE2ETests.cs` |
| Tenant Activity-Type governance | `ActivityTypes.CreateTenantActivityType` / `Rename` / `UpdateMetadata` / `Deactivate` / `Reactivate` | `ActivityTypes.TenantActivityTypeCommandService.CreateAsync`/… | `ActivityTypeCommandResult` | Story 1.5 tests (`Server.Tests`/E2E for tenant activity types) |
| Project Activity-Type governance | `ActivityTypes.CreateProjectActivityType` / … | `ActivityTypes.ProjectActivityTypeCommandService` | `ActivityTypeCommandResult` | Story 1.6 tests (project activity types) |

Verified constructors + method signatures (acknowledgement property in **bold**):

```csharp
// CAPTURE
TimeEntryCommandService recordService = new(accessGuard); // ITimesheetsAccessGuard
await recordService.RecordAsync(context, recordTimeEntry, state: null, freshCatalog, ct);
//   -> TimeEntryCommandResult.WasDispatched

// GOVERNANCE — entries
TimeEntrySubmissionCommandService submissionService = new(accessGuard);
await submissionService.SubmitAsync(context, submit, statesById, freshCatalog, submittedAtUtc, ct);
//   -> TimeEntrySubmissionCommandResult.HasAcceptedEvents

// Approval/rejection/correction/period-approval need an APPROVAL-AUTHORITY RESOLVER.
// Build it once and reuse (copy FixedAuthorityProvider from ApproveOrRejectSubmittedTimeEntriesE2ETests.cs):
var authorityResolver = new TimesheetsApprovalAuthorityResolver(
    new TimesheetsApprovalAuthorityPolicyOptions { PolicyVersion = "v2" },
    [authorityProvider]); // authorityProvider : IApprovalAuthoritySourceProvider returning an authorized source

TimeEntryApprovalCommandService approvalService = new(accessGuard, authorityResolver);
await approvalService.ApproveAsync(context, approve, state, decidedAtUtc, ct);   // -> TimeEntryApprovalCommandResult.WasDispatched
await approvalService.RejectAsync(context, reject, state, decidedAtUtc, ct);

TimeEntryCorrectionCommandService correctionService = new(accessGuard, authorityResolver);
await correctionService.CorrectAsync(context, correct, state, freshCatalog, correctionAtUtc, ct); // -> TimeEntryCorrectionCommandResult.WasDispatched

// GOVERNANCE — periods
TimesheetPeriodSubmissionCommandService periodSubmit = new(accessGuard);
await periodSubmit.SubmitAsync(context, submitPeriod, periodState, timeEntryStates, freshCatalog, policy, submittedAtUtc, ct);
//   -> TimesheetPeriodSubmissionCommandResult.WasPeriodDispatched

TimesheetPeriodApprovalCommandService periodApproval = new(accessGuard, authorityResolver);
await periodApproval.ApproveAsync(context, approvePeriod, periodState, timeEntryStates, periodProjection, decidedAtUtc, ct);
//   -> TimesheetPeriodApprovalCommandResult.WasPeriodDispatched

// GOVERNANCE — Activity-Type catalog
TenantActivityTypeCommandService tenantCatalog = new(accessGuard);
await tenantCatalog.CreateAsync(...); // RenameAsync/UpdateMetadataAsync/DeactivateAsync/ReactivateAsync -> ActivityTypeCommandResult
ProjectActivityTypeCommandService projectCatalog = new(accessGuard);
```

Fixtures to copy verbatim into the perf lane (no centralized fakes — each E2E test inlines its own):
- `AllowAllAccessGuard : ITimesheetsAccessGuard` — from `SubmitTimeEntriesForApprovalE2ETests.cs` (~line 275). Returns `TimesheetsAuthorizationDecision.Allowed()` for `AuthorizeAsync`/`ExecuteIfAuthorizedAsync`/`EvaluateUiActionAsync`. Use allow-all so the lane measures the **command path**, not authorization denial.
- `FixedAuthorityProvider : IApprovalAuthoritySourceProvider` — from `ApproveOrRejectSubmittedTimeEntriesE2ETests.cs` (~line 432). Ctor takes `Func<ApprovalAuthorityResolutionRequest, ApprovalAuthoritySourceResult>`; return an authorized source so approval/correction/period-approval dispatch.
- State builders: build `TimeEntryState`/`TimesheetPeriodState` by applying events (`state.Apply(recorded); state.Apply(submitted); ...`) and `ActivityTypeCatalogReadModel` via `FreshCatalog(ActiveCatalogItem(...))` — all already shown in the E2E tests. Build the warmed state **once outside** the measured loop so you time only the command call.

Notes:
- The acknowledgement result is a pure record (no I/O); measuring it captures the authorization-gate + aggregate-decision cost. Assert the acknowledgement is the expected dispatched/accepted value each iteration.
- `Hexalith.Timesheets.Testing` (which holds `TimesheetsTestIds`) is **not** referenced by `Hexalith.Timesheets.IntegrationTests`. Define local id/context constants exactly as the E2E tests do (`Context()`, `Project()`, `ActivityId()`, …); do **not** add the Testing reference just for ids.
- `TimesheetsDomainResult` exposes `IsSuccess`/`IsRejection`/`IsNoOp` and `Events` if you want to additionally assert events were produced.

### What "Warmed Local Service" Means Here (honesty constraint)

- The infra-free IntegrationTests project cannot start a real EventStore/Dapr service. "Warmed local service" in this lane = a **warmed in-process composed command pipeline** (authorization gate → aggregate decision → result), with warm-up iterations discarded before measurement. This is a legitimate, reproducible NFR10 lower-bound for the command-decision path and is the part Story 1.1 reserved as runnable now.
- The **full EventStore-backed wire path** (persistence + Dapr round-trip) is the more realistic NFR10 target but needs runtime fixtures that do not exist yet; it stays reserved via `PerformanceEvidenceLaneTests`. Record its verdict as deferred/waived with rationale in the evidence doc. Do not pretend the in-process number covers the wire path — state the limitation plainly (this mirrors the Story 1.10 "documented limitation" discipline and the Epic 1 "no source-scan-only / no false-confidence" lesson).

### Previous Story Intelligence

- **Epic 1 retro (mandatory lessons):** the recurring gap was **verification depth** — "tests sometimes proved a shape existed but not that the real composed service used it." Apply it here: the perf lane must drive the **real** command services and assert each acknowledgement result is genuinely dispatched/accepted, so a degenerate/no-op path cannot register as fast. Avoid source-scan-only "evidence."
- **File-List honesty (every Epic 1 story, and the 1.10 review MEDIUM-1):** list every added/modified test, doc, and csproj file in the File List before claiming done. The 1.10 review caught an omitted test file and a stale "25 vs 39 passed" count — report exact, real numbers.
- **Sandbox test execution (Stories 1.1–1.10):** `dotnet test` may fail with `System.Net.Sockets.SocketException (13): Permission denied` (VSTest listener); fall back to running the built xUnit v3 executables directly and document the reason. Set `TIMESHEETS_PERF=1` on the same command for the opt-in run.
- **Doc discipline:** BMAD artifacts live under `_bmad-output/`; `docs/` is the module's real documentation tree (`performance-evidence.md`, `boundary-decision-record.md`) — editing `docs/performance-evidence.md` here is correct and expected, but do not dump BMAD scratch into `docs/`.

### Architecture & Testing Constraints

- Keep fast tests infrastructure-free (no Dapr/Aspire/EventStore server/containers/network); infrastructure setup cost must never be counted as fast-baseline time. [Source: `architecture.md#Test-Organization`, `docs/performance-evidence.md`]
- Test framework: **xUnit v3 + Shouldly** (NSubstitute available if a collaborator fake is needed). Test method names use **PascalCase**. Run test projects individually; use `.slnx` for restore/build only. [Source: `Hexalith.AI.Tools/CLAUDE.md` Testing Standards]
- C#: .NET 10, nullable + implicit usings, file-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix, `.ConfigureAwait(false)` on awaited production calls, warnings-as-errors. No inline `<Version>`; no new `PackageReference` is expected (Stopwatch/env-var are BCL). If a package is genuinely needed, add it via `Directory.Packages.props` only. [Source: `Hexalith.AI.Tools/CLAUDE.md` C# Coding Standards]
- NFR12 still applies: do not log command bodies, event payloads, comments, personal data, tokens, or secrets in the lane's output. Emit only timing aggregates and scenario names. [Source: `epics.md` NFR12]
- xUnit v3 dynamic skip: `Assert.Skip("reason")` skips at runtime — use it for the env-var gate so the test self-skips in the default run without code edits. Confirm the API is available in the pinned xUnit v3 version; if not, fall back to a `[Fact(Skip=...)]`-by-default plus a separately-invoked harness, but then ensure the env-gated measured numbers are still produced for the doc.

### Anti-Patterns To Prevent

- Do NOT add a hard p95 timing assertion to the default `dotnet test` suite — it will flake across machines/CI. Surface measured p95 as recorded evidence; gate any latency assertion behind the opt-in and keep it a generous sanity bound at most.
- Do NOT let the performance lane run in the fast baseline (must dynamically skip without `TIMESHEETS_PERF`).
- Do NOT delete/rename the reserved `Performance_lane_is_reserved_for_launch_latency_evidence` or `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence` methods, or remove the `Skip =` literal, without updating `PerformanceEvidenceTests` in the same change.
- Do NOT remove any substring `PerformanceEvidenceTests` asserts from `docs/performance-evidence.md` (`500 ms p95`, `2s p95`, `EventStore-backed write path`, `read models`, `fast unit baseline`).
- Do NOT measure report/export/dashboard query latency (that is Story 4.10) or change any production command/authorization code.
- Do NOT claim the in-process number proves the EventStore-backed wire path; record that path's verdict honestly as deferred/waived.
- Do NOT use a denying/real authorization path for the measurement — use `AllowAllAccessGuard` so the command path itself is what is measured.

## Project Structure Notes

- New performance lane test: `tests/Hexalith.Timesheets.IntegrationTests/CaptureAndGovernanceCommandPerformanceLaneTests.cs` (infra-free, opt-in, in-process). No new `.csproj`; the IntegrationTests project already references `Contracts`/`Projections`/`Server`, which is sufficient for the capture/governance command services.
- Reserved markers stay in place: `PerformanceEvidenceLaneTests.cs`, `InfrastructureLaneTests.cs`.
- Evidence doc: `docs/performance-evidence.md` (updated with measured numbers + verdict + run instructions).
- Fitness extension: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`.
- README: `README.md` Build and Test section (opt-in lane note).
- No `.slnx` change is expected (no new project). If you do add a project, register it in `Hexalith.Timesheets.slnx` and honor CPM/warnings-as-errors.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-11-Add-Command-Performance-Evidence-for-Capture-and-Governance-Paths`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-1` (performance-evidence scaffold AC: "isolated place for future performance evidence"; NFR10/NFR11 ownership split 1.11 vs 4.10)]
- [Source: `_bmad-output/planning-artifacts/epics.md#NonFunctional-Requirements` (NFR10 command ack ≤500 ms p95; NFR11 report query ≤2 s p95)]
- [Source: `_bmad-output/planning-artifacts/epics.md#NFR-Coverage-Map` (NFR10 Primary = 1.11, also 1.1; NFR11 Primary = 4.10)]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Integration-Points` ("Readiness repair (approved 2026-06-20)" — 1.11 owns capture/governance command evidence; Epic 5 aggregates)]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Test-Organization` and `#Development-Workflow-Integration` (infra-free fast lanes; build/test through `.slnx`)]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-19.md` (verification-depth lesson; File-List honesty; sandbox xUnit fallback; `docs/performance-evidence.md` still-reserved status at end of Epic 1)]
- [Source: `_bmad-output/implementation-artifacts/1-10-implement-work-reference-validation-adapter.md` (sandbox build/test commands; documented-limitation discipline; honest test-count reporting)]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/PerformanceEvidenceLaneTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/InfrastructureLaneTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/SubmitTimeEntriesForApprovalE2ETests.cs` (AllowAllAccessGuard + in-process command construction pattern)]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/TestRepositoryRoot.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/RepositoryRoot.cs`]
- [Source: `docs/performance-evidence.md`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`, `TimeEntryCommandResult.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`, `TimeEntrySubmissionCommandResult.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryApprovalCommandService.cs`, `TimeEntryApprovalCommandResult.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs`, `TimeEntryCorrectionCommandResult.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSubmissionCommandService.cs`, `TimesheetPeriodApprovalCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeCommandService.cs`, `ProjectActivityTypeCommandService.cs`, `ActivityTypeCommandResult.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/*`, `Commands/TimesheetPeriods/*`, `Commands/ActivityTypes/*`]
- [Source: `README.md` (Build and Test section; sandbox xUnit fallback)]

## Open Questions

These carry sensible defaults so implementation is not blocked.

1. **Measurement realism (in-process vs EventStore-backed):** default — measure the in-process composed command path now and record the EventStore-backed wire-path verdict as deferred/waived (reserved lane kept). Confirm the architect accepts the in-process number as the NFR10 evidence for this milestone.
2. **Iteration/warm-up counts and percentile method:** default — ~50–100 warm-up + ~200–1000 measured iterations per scenario, p95 via sorted nearest-rank. Tune if the run is too slow or too noisy; record the chosen counts in the evidence doc.
3. **Skip mechanism:** default — env-var opt-in (`TIMESHEETS_PERF=1`) with xUnit v3 `Assert.Skip`. If the pinned xUnit v3 lacks dynamic skip, fall back to a default `[Fact(Skip=...)]` plus a documented manual-run path. Record which was used.
4. **Magic-link / external-contribution / export-generation commands:** default — out of this story's core scope (Epic 3/Epic 4 paths; export/report latency is 4.10). Include only if trivially cheap to add to the lane; otherwise note them as future coverage.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context)

### Debug Log References

- Build (in-scope, warnings-as-errors): each Timesheets project built standalone with `dotnet build <proj> --no-restore -warnaserror -m:1` → **0 warnings, 0 errors** for Contracts, Projections, Server, ArchitectureTests, Server.Tests, Contracts.Tests, Projections.Tests, IntegrationTests.
- Full-solution build deviation: `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` surfaces **18 pre-existing errors** in the transitively-referenced `Hexalith.PolymorphicSerializations` submodule (8× IDE0065 style + NuGet pack NU5118/NU5128 from its `GeneratePackageOnBuild`). That submodule is **not a member of `Hexalith.Timesheets.slnx`** and is not touched by this story; `git status` confirms none of the erroring files are in this change set. The errors are deterministic from unchanged submodule source → pre-existing and independent of Story 1.11, not a regression.
- Test execution used the built xUnit v3 executables directly (the documented Stories 1.1–1.10 sandbox pattern) rather than `dotnet test`, to avoid the known VSTest `SocketException (13): Permission denied` listener failure.
- Performance lane default run (no `TIMESHEETS_PERF`): `Capture_and_governance_command_acknowledgements_record_nfr10_p95_evidence` **dynamically skips** via `Assert.Skip`.
- Performance lane opt-in run (`TIMESHEETS_PERF=1`): passes; per-scenario p95 captured via the `-xml` report (`ITestOutputHelper`).

### Completion Notes List

- Added one isolated, opt-in, infrastructure-free command performance lane (`CaptureAndGovernanceCommandPerformanceLaneTests`) that drives the **real in-process** capture + governance command services (no new production code, no EventStore/Dapr/Aspire/containers/network). It warms (100 discarded iterations) then measures (500 iterations) per scenario, computes p95 via sorted nearest-rank, and asserts the real acknowledgement (`WasDispatched`/`HasAcceptedEvents`/`WasPeriodDispatched`) every iteration so a no-op path cannot register as fast (addresses the Epic 1 "verification depth" lesson).
- Scenarios covered (11): capture record; entry submit/approve/reject; rejected-entry correction; approved-entry correction (locking add-correction); period submit/approve/reject; tenant Activity-Type create; project Activity-Type create. Constructor wiring copied from the matching E2E tests; Activity-Type services accept `ITimesheetsAccessGuard`, so the local `AllowAllAccessGuard` is reused (no NSubstitute reference added).
- Skip mechanism: env-var opt-in `TIMESHEETS_PERF=1` with xUnit v3 `Assert.Skip` (confirmed available in pinned xunit.v3 3.2.2). No hard p95 gate in the default suite; a single generous sanity bound (`< 500 ms` p95, the NFR10 ceiling) exists **only inside the opt-in branch**.
- Measured evidence (AMD Ryzen 9 9950X3D, Ubuntu 26.04 WSL2, .NET SDK 10.0.301 / runtime 10.0.9, Debug): worst-case command-acknowledgement p95 = **0.0056 ms** (submit timesheet period); all scenarios ≈0.0015–0.0056 ms p95 — roughly five orders of magnitude inside NFR10 `500 ms p95`. **NFR10 in-process verdict: pass.** EventStore-backed wire-path verdict: **waived (deferred)** — reserved lane kept, blocked on runtime fixtures; recorded so Epic 5 aggregates rather than re-measures.
- Reserved markers untouched: `PerformanceEvidenceLaneTests.cs` and `InfrastructureLaneTests.cs` kept verbatim; all fitness literals (`Performance_lane_is_reserved_for_launch_latency_evidence`, `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence`, `Skip =`, `500 ms p95`, `2s p95`, `EventStore-backed write path`, `read models`, `fast unit baseline`) preserved.
- Fitness test extended (existing assertions kept intact) with **three** new facts: `Performance_evidence_documents_capture_and_governance_command_verdict` (NFR10 verdict vocabulary), `Command_performance_lane_stays_opt_in_and_out_of_the_fast_baseline` (asserts the integration source keeps the `TIMESHEETS_PERF`/`Assert.Skip` dynamic gate — the static `Skip =` placeholders do not protect it), and `Performance_evidence_documents_how_to_run_the_opt_in_lane` (asserts the doc keeps the run instructions).
- Nearest-rank percentile math was extracted from the timing lane into `PerformanceStatistics.NearestRankPercentile` and locked down with 11 fast, infra-free unit tests (`PerformanceStatisticsTests`) that run in the default baseline, so the recorded `Verdict: pass` rests on tested math rather than only being exercised inside the opt-in lane (Epic 1 verification-depth lesson).
- Out of scope honored: no report/export/dashboard query latency (NFR11 / Story 4.10); no production command/authorization changes; no fail-closed default weakened. Magic-link/external-contribution commands omitted (Open Question #4 default).
- Test results (built executables, default mode unless noted): IntegrationTests **66 total, 0 failed, 3 skipped** (perf lane + 2 reserved placeholders; the 11 new `PerformanceStatisticsTests` cases run in the default baseline); ArchitectureTests **26 total, 0 failed** (incl. 5 `PerformanceEvidenceTests` facts); Server.Tests **379/379**; Contracts.Tests **86/86**; Projections.Tests **77/77**. Opt-in IntegrationTests: perf lane **1 passed, 0 skipped**.

### File List

- `tests/Hexalith.Timesheets.IntegrationTests/CaptureAndGovernanceCommandPerformanceLaneTests.cs` (added)
- `tests/Hexalith.Timesheets.IntegrationTests/PerformanceStatistics.cs` (added — infra-free nearest-rank percentile helper the lane delegates to)
- `tests/Hexalith.Timesheets.IntegrationTests/PerformanceStatisticsTests.cs` (added — 11 fast unit tests for the percentile math, run in the default baseline)
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs` (modified — +3 fitness facts)
- `docs/performance-evidence.md` (modified)
- `README.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status tracking)
- `_bmad-output/implementation-artifacts/tests/1-11-test-summary.md` (added — QA test-generation summary)

## Change Log

| Date | Change |
|---|---|
| 2026-06-22 | Implemented Story 1.11: added opt-in `TIMESHEETS_PERF` capture/governance command-acknowledgement performance lane, recorded measured NFR10 p95 evidence + pass/waived verdict in `docs/performance-evidence.md`, extended `PerformanceEvidenceTests` fitness assertions, documented the lane in `README.md`. All in-scope builds 0/0; full Timesheets test suite green (perf lane skipped by default). Status → review. |
| 2026-06-22 | Adversarial review (auto-fix): verified ACs/tasks by building (`-warnaserror` 0/0) and running the default lanes, the architecture fitness lane, and the opt-in `TIMESHEETS_PERF=1` perf lane (worst-case p95 ≈ 0.0059 ms vs NFR10 500 ms — verdict pass holds). Fixed 3 MEDIUM doc-honesty findings: File List omitted `PerformanceStatistics.cs` + `PerformanceStatisticsTests.cs`; Completion Notes carried stale counts (IntegrationTests 55→66, ArchitectureTests 24→26, fitness facts 3→5) and undercounted the fitness extension (1→3 facts) plus the percentile-math extraction. No CRITICAL/HIGH; no source/production changes. Status → done. |

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-22 · **Outcome:** Approve (auto-fix applied)

### Verification performed (not source-scan-only)

- Built `Hexalith.Timesheets.IntegrationTests` and `Hexalith.Timesheets.ArchitectureTests` with `-warnaserror` → **0 warnings, 0 errors**.
- Ran the **default** IntegrationTests executable → **66 total, 0 failed, 3 skipped**; confirmed `Capture_and_governance_command_acknowledgements_record_nfr10_p95_evidence` dynamically skips with `Set TIMESHEETS_PERF=1 to run the command performance lane.` and the two reserved EventStore placeholders stay statically skipped.
- Ran **ArchitectureTests** → **26 total, 0 failed**; all 5 `PerformanceEvidenceTests` facts pass (verdict vocabulary, run-instructions, env-gate isolation, reserved-lane literals, launch targets).
- Ran the **opt-in** lane (`TIMESHEETS_PERF=1`) → **1 passed, 0 skipped**; captured the `ITestOutputHelper` evidence and confirmed the recorded p95 numbers are genuine (worst-case ≈ 0.0059 ms, scenario "submit timesheet period"), consistent with the doc's 0.0056 ms (sub-microsecond run-to-run variation). **NFR10 in-process verdict: pass** is sound; EventStore-backed wire path correctly recorded **waived/deferred**.

### AC coverage

- **AC1** (measured capture+governance lane, 500 ms p95 target, deviations recorded): IMPLEMENTED — 11 scenarios drive the real in-process command services and assert the real acknowledgement each iteration.
- **AC2** (skipped perf tests isolated, fast baseline unaffected, docs explain how to run): IMPLEMENTED — `Assert.Skip` env-gate verified to skip by default; `PerformanceStatistics` math runs in the fast baseline; two new fitness facts guard the gate and the run instructions.
- **AC3** (pass/concern/fail/waived verdict; Epic 5 aggregates): IMPLEMENTED — verdict recorded in `docs/performance-evidence.md`; reserved EventStore lane kept so Epic 5 aggregates rather than re-measures.

### Findings (all MEDIUM, auto-fixed in this review)

1. File List omitted the added `PerformanceStatistics.cs` and `PerformanceStatisticsTests.cs` → added.
2. Stale test counts in Completion Notes (IntegrationTests 55, ArchitectureTests 24/24, 3 fitness facts) → corrected to 66 / 26 / 5 against the actual runs.
3. Completion Notes undercounted the fitness extension (1 fact) and omitted the percentile-math extraction → documented all three facts and the `PerformanceStatistics` extraction.

No CRITICAL or HIGH issues. No production/source/authorization code was changed by this story or this review; reserved markers and all fitness-asserted literals are intact.
