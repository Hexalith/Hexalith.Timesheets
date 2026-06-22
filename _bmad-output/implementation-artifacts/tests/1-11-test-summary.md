# Test Automation Summary — Story 1.11 (QA generate-e2e-tests)

**Feature:** Command Performance Evidence for Capture and Governance Paths (NFR10).
**Date:** 2026-06-22 · **Engineer role:** QA automation (test generation only — no code review).
**Framework:** xUnit v3 3.2.2 + Shouldly (.NET 10), run via the built test executables
(sandbox VSTest socket fallback, per Stories 1.1–1.10).

## Context

Story 1.11 is an evidence/measurement story over **already-implemented** capture and governance
command services. Those command paths each already have dedicated E2E tests
(`SubmitTimeEntriesForApprovalE2ETests`, `ApproveOrRejectSubmittedTimeEntriesE2ETests`, …), so the
QA pass targeted the **new** behavior the story added — the measured performance lane, its
isolation gate, and the evidence document — judged against the Epic 1 *verification-depth* lesson
(no source-scan-only confidence).

## Gaps discovered and closed

| # | Gap | Risk | Fix |
|---|-----|------|-----|
| 1 | Nearest-rank **percentile math** (`Percentile`) had **zero tests** — only exercised inside the opt-in timing lane. | The recorded NFR10 `Verdict: pass` rests entirely on this math; a wrong percentile would silently falsify every p95 in `docs/performance-evidence.md`. | Extracted the math into `PerformanceStatistics.NearestRankPercentile` (the real lane now delegates to it) and added 11 fast unit tests (`PerformanceStatisticsTests`). |
| 2 | AC2 "skipped perf tests stay isolated" was only guarded by a `Skip =` source-scan, which is satisfied by the **static** reserved placeholders — it did **not** protect the new dynamic lane's `TIMESHEETS_PERF`/`Assert.Skip` opt-in gate. | Someone could remove the env-gate and timing would silently run inside the fast baseline, undetected. | Added fitness fact `Command_performance_lane_stays_opt_in_and_out_of_the_fast_baseline` asserting the integration source still contains `TIMESHEETS_PERF` and `Assert.Skip`. |
| 3 | AC2 "docs explain how to run the lane" was **unguarded** by any fitness test. | The run instructions / opt-in could regress away with no failing test. | Added fitness fact `Performance_evidence_documents_how_to_run_the_opt_in_lane` asserting the doc contains `How to run the performance lane`, `TIMESHEETS_PERF=1`, and `skipped by default`. |

No production code, command service, authorization gate, or fail-closed default was changed. The
reserved EventStore-backed placeholders and all fitness-asserted literals were preserved.

## Generated tests

### Unit tests (fast baseline, infra-free, no env gate)
- [x] `tests/Hexalith.Timesheets.IntegrationTests/PerformanceStatisticsTests.cs` — 11 cases:
  nearest-rank p95/p50/p99/p100 over 1..100, nearest-rank on a small sample, zero-percentile
  clamp to minimum (boundary), single-element sample, p95-is-an-actual-member, empty-sample
  rejection (`ArgumentException`).

### Production-under-test helper extracted to make the math testable
- [x] `tests/Hexalith.Timesheets.IntegrationTests/PerformanceStatistics.cs` — `internal static`
  nearest-rank percentile used by the lane.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/CaptureAndGovernanceCommandPerformanceLaneTests.cs`
  — now delegates to `PerformanceStatistics.NearestRankPercentile` (behavior unchanged).

### Fitness / regression guards (fast architecture baseline)
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`
  — +2 facts (env-gate isolation + run-instructions doc). Existing 3 facts kept intact.

### API tests
- N/A — Story 1.11 adds no new API endpoint; existing endpoints are covered elsewhere.

### E2E / UI tests
- N/A — Story 1.11 has no UI surface.

## Results (built xUnit v3 executables)

| Suite | Result |
|---|---|
| IntegrationTests — **default** (no `TIMESHEETS_PERF`) | **66 total, 0 failed, 3 skipped** (perf lane + 2 reserved placeholders still skip) |
| `PerformanceStatisticsTests` (new) | **11 / 11 passed** |
| ArchitectureTests — all | **26 total, 0 failed** (was 24; +2 new fitness facts) |
| `PerformanceEvidenceTests` (fitness) | **5 / 5 passed** |
| IntegrationTests — **opt-in** (`TIMESHEETS_PERF=1`) | perf lane **1 passed, 0 skipped**; worst-case command-ack p95 **0.0056 ms** (submit timesheet period), matching the recorded evidence |

Builds: `IntegrationTests` and `ArchitectureTests` both **0 warnings / 0 errors** under
`-warnaserror`.

## Coverage

- Percentile/statistics math: **0 → 11** test cases (previously untested; now the evidence verdict
  rests on tested math).
- AC2 regression guards: **+2** fitness facts (env-gate isolation + run-instructions doc), closing
  the two source-scan/doc gaps.
- Command acknowledgement scenarios measured by the lane: 11/11 (capture record; entry
  submit/approve/reject; rejected & approved correction; period submit/approve/reject; tenant &
  project Activity-Type create) — unchanged, still gated opt-in.

## Validation against checklist.md

All applicable items pass: tests use standard framework APIs, cover happy path + critical error
cases (empty-sample, zero-percentile boundary), have clear PascalCase descriptions, no
waits/sleeps, are order-independent, and all run green. API/E2E-UI items are N/A for this
non-UI evidence story (documented above).

## Next steps

- Run in CI (default lane stays fast; perf lane stays opt-in via `TIMESHEETS_PERF=1`).
- The EventStore-backed wire-path measurement remains **waived/deferred** (reserved lane kept) for
  a later data-bearing story; Epic 5 aggregates rather than re-measures.
