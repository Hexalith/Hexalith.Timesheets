# Test Automation Summary — Story 4.9 (Approved Export Preview Behavior)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer · **Date:** 2026-06-22
**Feature under test:** `PreviewApprovedTimeExport` — dedicated, side-effect-free export preview handler
(`ApprovedTimeExportService.PreviewAsync`) sharing one readiness core with `GenerateApprovedTimeExport`.

> Note: this rolling summary file previously held the Story 4.7 dashboard run; it was replaced with the 4.9 run.

**Framework detected:** xUnit v3 · Shouldly · NSubstitute (.NET 10 / C# 14). No JS/Playwright UI in scope — the
preview surface is contract + service + FrontComposer metadata only (no Blazor), so "E2E" maps to the project's
integration suite and "API tests" map to the service/contract suites. Tests run via the **built xUnit v3
executables** because the VSTest socket is blocked in the sandbox (`SocketException (13): Permission denied`).

## Coverage assessment

Story 4.9 shipped with broad coverage already. Mapping the existing tests against the acceptance criteria:

| AC | Existing coverage | Status |
|----|-------------------|--------|
| AC2 — dedicated, side-effect-free handler | `ApprovedTimeExportPreviewServiceTests` (no CSV, no audit, GeneratedAtUtc/hash null) | Covered |
| AC3 — single readiness core; preview matches generation | `Preview_and_generate_share_one_readiness_core_*` (fake reader) | **Gap: real-projection consistency** |
| AC4 — fail closed, no disclosure | denial / missing-tenant / cross-tenant / stale / empty / non-billable / insufficient-role / comment-redacted | Covered |
| AC5 — deterministic, scenario-tested readiness | unit determinism (fake), integration ready/blocked/stale | **Gap: corrected/superseded determinism with default `CurrentRowsOnly=true`, across repeated runs, over a real projection** |
| AC1 — metadata consistency | `ApprovedTimeLedgerContractTests` (projection + new preview descriptor, `Timesheets.ReviewExportReadiness` bound) | Covered |

## Discovered gaps — auto-applied

Both pre-existing AC3/AC5 proofs relied on a **fake** `TrackingLedgerReader` that returns an identical page to
both code paths, so they cannot catch divergence introduced by real projection behavior (current-rows filtering,
correction supersession). Two integration tests were added to close this.

### Integration tests (`tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportPreviewIntegrationTests.cs`)

- [x] `Preview_and_generate_agree_over_seeded_projection_while_only_generate_produces_file_and_audit`
  — **AC3 end-to-end.** Seeds real time-entry events (incl. an approved correction), runs `PreviewAsync` and
  `GenerateAsync` over the same projection, asserts identical `Readiness` / `Scope.RowCount` (=2, default
  current-rows-only) / freshness / blocked-reason, and that **only generate** produces CSV + emits
  `ApprovedTimeExported` while preview emits neither (audit recorder empty, `GeneratedAtUtc`/hash null).
- [x] `Preview_current_rows_only_corrected_scope_is_deterministic_and_excludes_superseded_rows`
  — **AC5.** Over seeded corrected/superseded rows: two repeated current-rows-only previews agree
  (`Ready`, RowCount=2, Fresh); an include-superseded preview deterministically widens the scope to 3
  (original + correction); no run emits audit evidence.

## Test execution

Affected project only (no production code changed → other suites unaffected; green at story baseline):

| Suite | Total | Failed | Skipped | Notes |
|-------|------:|-------:|--------:|-------|
| `Hexalith.Timesheets.IntegrationTests` | **80** | 0 | 3 | was 78; +2 added. 3 skips are pre-existing perf/infra lanes (`TIMESHEETS_PERF` / runtime fixtures). |

Build: `dotnet build tests/Hexalith.Timesheets.IntegrationTests/...csproj -warnaserror -m:1 /nr:false`
→ **Build succeeded, 0 Warning(s), 0 Error(s)**.

## Coverage metrics

- AC3 (preview ↔ generate consistency): fake-reader **+ real-projection** end-to-end — closed.
- AC5 (deterministic readiness scenarios): no-rows, stale, denied, insufficient-role, comment-redacted, billable
  states, **+ corrected/superseded with default current-rows-only across repeated runs over a real projection** —
  closed.
- No-file / no-audit side-effect-free guarantee: asserted on every preview test, now including the real-projection
  consistency test.

## Next steps

- Run in CI (the affected suite is green locally via the built executable).
- No further gaps identified against the 4.9 ACs; HTTP/REST wiring and performance evidence (NFR11) are explicitly
  out of scope (owned by platform host / Story 4.10).
