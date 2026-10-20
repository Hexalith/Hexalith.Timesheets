---
baseline_commit: cff081cdfe6e0970edac5c960bf5b41f86aa090b
---

# Story 5.1: Final Launch-Readiness Gate and Documentation Sync

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a release owner,
I want final readiness evidence to distinguish completed stories from launch-scope integrations,
so that Timesheets does not ship with hidden unavailable defaults or overstated product claims.

## Current Scope and Acceptance — 2026-10-20

Story 5.1 verifies, classifies, and synchronizes evidence; it implements no feature behavior, hosting, dependency, submodule, or UI change. The approved 2026-09-12 course correction §3/§4.4 supersedes the original completed/CONCERNS framing. Epic 5 also contains Stories 5.2 and 5.3. The spec `spec-5-1-final-launch-readiness-gate-and-documentation-sync.md` controls this increment; the historical task/evidence record below retains its original dates.

1. Given current code and approved decisions, when artifacts are reconciled, then each concern is `implemented`, `unfinished`, `waived`, or `post-v1`, and every waiver names approval evidence, owner, risk, and revisit condition. FR14 deployed acceptance remains unfinished; release remains **FAIL**.
2. Given executable configuration and sprint tracking, when guidance and fitness checks run, then SDK/package claims and current story states agree. Q6 governance, Q7 CSV v1, Q8 sensitive comments, and consumer-host rendering/accessibility ownership are resolved. No Timesheets UI project may be added.
3. Given release checks, when results are published, then all six executables have exact pass/fail/skip counts and dated source scope. Historical local proof, current-graph runtime qualification, skipped performance/runtime lanes, and deployed acceptance stay distinct.

Reconciliation implementation and checks: `tests/5-1-test-summary.md`. The user accepted and signed off the completed Story 5.1 reconciliation on 2026-10-20: “accept and sign-off”. [Acceptance record](evidence/5-1/20261020-final-gate/story-acceptance.json) closes this story independently of the remaining release prerequisites. Stories 3.6/3.7 deployed acceptance, Story 5.2 review, tenant/legal approval, policy configuration, and product stakeholder UAT remain open; release remains **FAIL**. Old checked tasks below retain their historical evidence scope.

## Historical Tasks / Subtasks (original execution)

- [x] **Task 1 — Author `docs/launch-readiness.md` classification record (AC: 1)**
  - [x] Create `docs/launch-readiness.md` (durable, code-adjacent, alongside `performance-evidence.md` and
    `boundary-decision-record.md`). Open with a one-paragraph "story-complete vs launch-complete" framing and the
    baseline commit + date.
  - [x] Add a **Launch-scope classification table** with columns: `Item | Verified state | Classification
    (implemented/waived/post-v1) | Owner | Risk | Revisit condition | Evidence`. Populate it from the **verified
    state inventory** in Dev Notes below — one row per launch-scope item. Every `waived`/`post-v1` row MUST fill
    Owner + Risk + Revisit (AC1 hard requirement).
  - [x] Cover, at minimum, the eight epic-named items (unavailable defaults; skipped lanes; deferred integrations;
    legal-hold policy; comment sensitivity; export format; secondary magic-link identity verification; performance
    evidence) **plus** the additional verified items in the inventory (live Works host activation, live magic-link
    end-to-end resolution, export-preview HTTP endpoint, NFR13 UI, production-deployment evidence, external
    stakeholder acceptance, Works staleness detection, and the doc/version divergences).

- [x] **Task 2 — Documentation sync against committed reality (AC: 2)**
  - [x] Reconcile `architecture.md` **status notes** that predate Stories 4.8/4.9/4.10: update any text that still
    calls planned-effort (4.8), export preview (4.9), or report/export/dashboard performance (4.10) "backlog"/"remains
    reserved" to reflect that they are committed (`ab4a012`, `24c6e23`, `cff081c`) — while keeping the honest residual
    caveats (host not wired for live Works; preview has no HTTP route; perf wire path waived).
  - [x] Audit `README.md` against code (the recurring "README lagged after every epic" lesson): confirm it still
    correctly states the magic-link host "does not resolve end-to-end" pending projection-host wiring, the perf lanes
    are opt-in with a waived wire path, and there is intentionally no Timesheets UI project. Fix anything stale; do
    **not** add claims the code does not back.
  - [x] Confirm `docs/performance-evidence.md` (NFR10 + NFR11 measured, wire path waived) and
    `docs/boundary-decision-record.md` (owns-vs-references decision) are consistent with the launch-readiness record;
    cross-link them from `docs/launch-readiness.md`.
  - [x] Record the **doc-vs-dependency divergences** you cannot fix here (they live in the `Hexalith.Builds`
    submodule — do not edit it): base `Dapr` package pinned `1.17.9` while `architecture.md` documents `1.18.4`;
    `Microsoft.FluentUI.Components 4.11.6` (V4) pinned while the architecture states "Fluent UI V5 only". And the
    **doc-vs-test-project** divergence: `architecture.md` lists `UnitTests`, `Security.Tests`, `PropertyTests`,
    `UI.Tests`, `UI` that do not exist on disk, and omits the `Works.Tests` project that does. Capture these as
    `concern` rows; correct the `architecture.md` test-map prose where it is purely a Timesheets-owned claim.

- [x] **Task 3 — Run the final release gates and record per-gate verdicts (AC: 3)**
  - [x] Run each gate and record `PASS/CONCERNS/FAIL/WAIVED` + traceable evidence (test class / file / commit) in a
    **Release-gate decision table** in `docs/launch-readiness.md`:
    - **Build** — clean restore + `-warnaserror` build of `Hexalith.Timesheets.slnx`.
    - **Tests (full suite)** — exact pass/skip counts per project from the **built xUnit v3 executables**.
    - **Privacy/logging scans** — `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`.
    - **Projection rebuild/idempotency** — `tests/Hexalith.Timesheets.Projections.Tests/*ProjectionTests.cs`.
    - **Export golden files** — `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs` +
      `Exports/Golden/approved-time-export-v1-project-work-boundaries.csv`.
    - **Magic-link HTTP no-disclosure** — `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`.
    - **Performance evidence** — `docs/performance-evidence.md` (NFR10/NFR11 measured in-process pass; wire path waived).
  - [x] Record the **overall** verdict. Given the explicit waivers, the honest overall is **CONCERNS** (or `WAIVED`
    if you frame the waivers as formally accepted) — **not PASS**. Justify the verdict from the evidence; list the
    open waivers that block a clean PASS.
  - [x] Reconcile sprint artifacts: this story moves `epic-5: in-progress` (already set by create-story) toward done
    via `code-review`; the launch-readiness record is the durable closure evidence.

- [x] **Task 4 — Fitness guard for the launch-readiness record (AC: 3)**
  - [x] Add `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` mirroring the structure
    of `PerformanceEvidenceTests.cs` (locate the doc via the same `RepositoryRoot.PathTo(...)` / `TestRepositoryRoot`
    helper those fitness tests already use — do **not** hand-roll path discovery). Assert `docs/launch-readiness.md`
    exists and contains: the classification vocabulary (`implemented`, `waived`, `post-v1`), the verdict vocabulary
    (`PASS`, `CONCERNS`, `FAIL`, `WAIVED`), waiver markers (`Owner`, `Risk`, `Revisit`), and the eight epic-named
    launch-scope item labels. Keep assertions about **presence of required sections/vocabulary**, not brittle exact
    prose, so the doc can evolve.
  - [x] Do **not** weaken any existing `PerformanceEvidenceTests` / `DiagnosticsPrivacyTests` / `ScaffoldGovernanceTests`
    literal; only add. Confirm the two reserved `[Fact(Skip=…)]` placeholders (`InfrastructureLaneTests`,
    `PerformanceEvidenceLaneTests`) are untouched.

- [x] **Task 5 — Build, test, verify, report (all ACs)**
  - [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` then
    `… dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` (expect **0 Warning(s),
    0 Error(s)**; ignore the known transient `Hexalith.PolymorphicSerializations` submodule pack/IDE0065 noise that
    self-clears — it is not in the `.slnx` and is not touched here).
  - [x] Run suites via the **built xUnit v3 executables** (VSTest socket is blocked in the sandbox:
    `SocketException (13): Permission denied`). The new fitness test lands in **ArchitectureTests**; only that count
    should move. Re-run the perf lane opted-in once (`TIMESHEETS_PERF=1`) only if you need to re-confirm the perf
    evidence rows — otherwise cite the existing `docs/performance-evidence.md` numbers.
  - [x] Generate the File List from the actual `git diff` and report **exact** test counts from the built executables
    (no estimates — this is an enforced gate; Stories 1.10/1.11/3.7/4.8/4.9 were caught reporting stale counts and
    File-List omissions; this is the #1 recurring failure the retros flagged). Counts to move from (post-4.10):
    Architecture **33** (→ 33 + new fitness facts), Integration **87** (83 pass + 4 skip — **unchanged**, no new
    integration tests), Contracts ~**88**, Server ~**420**, Projections ~**77** (re-measure all exactly). Budget for
    ≥1 review-found patch.

## Historical Dev Notes (original context; superseded by current scope)

### TL;DR — aggregate, classify, sync, decide; do not implement

Epics 1–4 are story-complete and their launch-scope prerequisites were rehomed to and completed in their owning
stories. Your deliverable is **(1)** `docs/launch-readiness.md` classifying every launch-scope item
`implemented/waived/post-v1` with owner/risk/revisit on each waiver; **(2)** a doc-sync pass that makes `README.md`,
`architecture.md` status notes, `docs/performance-evidence.md`, the PRD notes, and sprint artifacts tell the same
story-complete-vs-launch-complete truth with no overstated claims; **(3)** a release-gate run recording per-gate +
overall `PASS/CONCERNS/FAIL/WAIVED` with traceable evidence and exact built-executable counts; **(4)** a fitness test
guarding the record. Build nothing, host-wire nothing, scaffold no UI, edit no submodule, skip/weaken no test.

### Verified launch-scope state inventory (ground truth at baseline `cff081c` — use this to author the record)

Each row was verified against the committed source/tests/docs. Use it to populate the Task-1 classification table.

| Item | Verified state | Suggested class | Owner | Risk | Revisit condition |
|---|---|---|---|---|---|
| **Live Works reference validation in host** | `WorksQueryWorkReferenceValidator` is **real** (Story 1.10), but `IWorksQueryChannel` is **never bound** in `src/` and `AddTimesheetsWorksReferenceValidation()` is **never called by the host**; the kernel registers `DenyAllWorkReferenceValidator` ([Server/Runtime/ServiceCollectionExtensions.cs:66]). In the running host, Work writes are denied. | waived | Story 1.10 / platform | Live Work capture denied until channel bound | Host binds `IWorksQueryChannel` (e.g. `DomainQueryInvokerWorksQueryChannel`) + calls the opt-in |
| **Live Works planned-effort reporting** | `WorksQueryWorkPlannedEffortProvider` **real** (Story 4.8), but host keeps `UnavailableWorkPlannedEffortProvider` ([…ServiceCollectionExtensions.cs:72]); `AddTimesheetsWorksPlannedEffortReporting()` only called in `Works.Tests`. | waived | Story 4.8 / platform | Planned-vs-actual shows "unavailable" in host | Host wires the provider over a live Works channel |
| **Magic-link live end-to-end resolution** | `EventStoreMagicLinkConfirmationCapabilityStateLoader` **real + registered** (Story 3.6), but `MagicLinkTokenHashCapabilityIndexProjection` has **no projection-host wiring**, so a valid link resolves **empty** in the running topology. Invalid-link no-disclosure is fully proven. | waived | Story 3.6 follow-up / platform | Valid magic links do not resolve in host (invalid-link safety intact) | Projection host wired to populate the token-hash index |
| **Magic-link HTTP no-disclosure** | `MagicLinkConfirmationHttpBoundaryTests` **real** (`WebApplicationFactory<Program>`, 11 invalid cases × 4 routes, byte-for-byte opaque 403 `application/problem+json`) — Story 3.7. | implemented | Story 3.7 | — | — |
| **Export preview** | `ApprovedTimeExportService.PreviewAsync` **real**, side-effect-free (Story 4.9), covered by service + integration tests. **No HTTP route maps it** (host maps only default/external-contribution/magic-link/metadata endpoints); metadata advertises `timesheets.query.approved-ledger-export-preview` with no route. | implemented (service) / post-v1 (HTTP endpoint) | Story 4.9 | Metadata advertises a capability with no HTTP route | A story maps a preview endpoint, or the capability is removed from metadata |
| **Finance export format** | Deterministic CSV v1, evidence-only, golden-file tested (Stories 4.5/4.6); no rates/invoices/payroll/taxes/revenue. PRD Q7 (CSV vs structured API/webhook) **open**. | implemented (CSV v1) / post-v1 (structured contract) | Story 4.5 / PM | Downstream billing may need a structured contract | PRD §14 Q7 decision |
| **Performance evidence (NFR10/NFR11)** | Measured **in-process pass** in `docs/performance-evidence.md` (NFR10 11 scenarios worst 0.0056 ms vs 500 ms; NFR11 9 scenarios worst 6.09 ms vs 2 s). EventStore-backed **wire path waived (deferred)** for both. | implemented (in-process) / waived (wire path) | Stories 1.11, 4.10 | Wire-path latency unmeasured | Realistic EventStore/Dapr/Aspire persisted fixtures exist |
| **Skipped lanes** | Two static `[Fact(Skip=…)]` placeholders: `InfrastructureLaneTests`, `PerformanceEvidenceLaneTests` (empty bodies, reserved). | waived | data-bearing story | Reserved lanes, no runtime fixtures | Runtime EventStore/Dapr/Aspire fixtures land |
| **Legal-hold retention policy (NFR7)** | Default retention implemented + documented (Story 1.4); **legal-hold override is an explicit launch-readiness gate** requiring tenant/legal sign-off ([epics.md:489], [prd.md §9:369]). | waived (gate) | Story 1.4 / tenant + legal | Ships without legal-hold configured | Tenant/legal sign-off |
| **Comment sensitivity policy** | Policy vocabulary + fail-closed default implemented (Story 1.4); required-comment policy not modeled; PRD Q8 (classification/redaction rules) **open**. | implemented (vocabulary) / post-v1 (classification rules) | Story 1.4 / PM | Comment-classification rules unresolved | PRD §14 Q8 decision |
| **Secondary magic-link identity verification (Q4)** | **Deferred to post-v1** — settled (v1 = single-use scoped expiring links). | post-v1 | Story 3.2 / PM | High-value/billable links rely on scoped single-use only | Post-v1 review |
| **Time-zone / period policy (NFR15)** | Tenant time zone canonical; UTC instants + tenant-local period keys; DST/boundary golden-file tests (Stories 2.7/4.6). `carry-existing-approved-entry` period-approval behavior **open** (side-stepped, not export authority). | implemented / post-v1 (carry-forward policy) | Stories 2.7/4.6 / PM | Period carry-forward behavior unresolved | Period-approval policy decision |
| **NFR13 WCAG / UI behavior** | **No Timesheets UI project** (intentional, README:61); accessibility represented by FrontComposer metadata only, no component tests. | post-v1 (deferred) | UI-bearing story (none yet) | Accessibility unproven at component level | A UI-bearing story scaffolds `UI`/`UI.Tests` |
| **Production deployment evidence** | None present in any epic. | post-v1 / accept | Release owner | No production deploy proof | At deployment |
| **External stakeholder acceptance** | Story-record acceptance only; no external acceptance artifact. | post-v1 / accept | PM/PO | No external acceptance evidence | At UAT |
| **Works staleness detection** | Adapter cannot fail-closed on **stale** Works state (`WorkItemView` exposes only `LatestAcceptedSourceSequence`) — documented limitation (Q3); no Epic 1–5 plan depends on a change. | waived (documented limitation) | platform / Works owner | Stale Works state not detectable | Works-side view extension or Timesheets EventStore projection |
| **Doc-vs-dependency version divergence** | Base `Dapr` pinned `1.17.9` vs `architecture.md` `1.18.4`; `Microsoft.FluentUI.Components 4.11.6` (V4) vs "V5 only". Both live in the `Hexalith.Builds` submodule (do not edit). | concern (doc-sync) | platform / Hexalith.Builds | Docs claim versions that differ from pinned | Reconcile architecture prose vs build props |
| **Doc-vs-test-project divergence** | `architecture.md` lists `UnitTests`/`Security.Tests`/`PropertyTests`/`UI.Tests`/`UI` (absent on disk) and omits `Works.Tests` (present). Security/tenant-isolation tests live in `Server.Tests`/`IntegrationTests`, not a `Security.Tests` project. | concern (doc-sync) | Story 5.1 | architecture.md test map inaccurate | Fix the prose in this story |

### Release-gate evidence map (use for the Task-3 decision table)

| Gate (epic AC3) | Concrete evidence artifact | Expected verdict |
|---|---|---|
| Build | `Hexalith.Timesheets.slnx` clean `-warnaserror` build | PASS |
| Tests (full) | Built executables: ArchitectureTests, Contracts.Tests, IntegrationTests, Projections.Tests, Server.Tests, Works.Tests | PASS (exact counts) |
| Privacy/logging scans | `ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` (~13 facts) | PASS |
| Projection rebuild/idempotency | `Projections.Tests/*ProjectionTests.cs` (ledger, report, magic-link, evidence, period-summary, catalog) | PASS |
| Export golden files | `IntegrationTests/ApprovedTimeExportIntegrationTests.cs` + `Exports/Golden/approved-time-export-v1-project-work-boundaries.csv` | PASS |
| Magic-link HTTP no-disclosure | `IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` | PASS (invalid-link safety) / note live end-to-end waived |
| Performance evidence | `docs/performance-evidence.md` (NFR10/NFR11) | PASS in-process / WAIVED wire path |
| Tenant-isolation/security | `Server.Tests/AuthorizationServiceTests.cs`, `AuthorityBoundaryTests.cs`, `FailClosedDefaultsTests.cs`, `TimeEntryAuthorizationTests.cs` | PASS |

### Where things live (module map verified at `cff081c`)

`src/`: `Hexalith.Timesheets` (host: `Program.cs`, `Endpoints/`), `…Contracts` (commands/events/queries/models,
`openapi/`, `TimesheetsMetadataCatalog.cs`), `…Server` (`MagicLinks/`, `Exports/`, `ApprovedTimeLedger/`,
`OperationalReports/`, `Dashboard/`, `Runtime/ServiceCollectionExtensions.cs`), `…Projections`, `…Works`
(validator + planned-effort provider + DI extensions), `…Client`, `…ServiceDefaults`, `…Testing`, `…AppHost`.
**No UI project** (intentional). `tests/`: `ArchitectureTests`, `Contracts.Tests`, `IntegrationTests`,
`Projections.Tests`, `Server.Tests`, `Works.Tests`. `docs/`: `performance-evidence.md`, `boundary-decision-record.md`
(add `launch-readiness.md`). Solution: `Hexalith.Timesheets.slnx` at repo root (the 8 sibling `Hexalith.*.slnx` are
submodules — out of scope).

### Architecture & boundary constraints

- **Epic 5 verifies, never first-implements** ([epics.md:345-349], [prd.md §13:410-418]). Any temptation to "just
  wire it up" (Works channel, magic-link projection host, preview endpoint, UI) is **out of scope** — those are
  rows in your waiver table, owned by their feature stories.
- **EventStore stays the only authoritative boundary**; this story is docs + one fitness test + verification. Add no
  infrastructure, no persistence, no new packages (`File.ReadAllText`-style fitness assertions use BCL only).
- **Do not edit sibling submodules** (`Hexalith.Builds`, `Hexalith.EventStore`, `Hexalith.Tenants`, …). The Dapr/
  FluentUI version divergences are recorded, not fixed here. (Story 3.7 accidentally bumped `Hexalith.Tenants` and
  had to revert it — do not repeat.)
- **Fitness tests own the durability guarantee.** Mirror `PerformanceEvidenceTests` (which guards
  `performance-evidence.md`) so `launch-readiness.md` cannot silently drift. Locate the file via the existing
  repository-root test helper.

### Honesty discipline (the project's #1 recurring failure — internalize it)

The retros are unanimous: the dominant defect across every epic is **overstatement** — checked-off tasks that were
no-op stubs (Story 3.3 CRITICAL: a `GET …/confirm` always-denied while OpenAPI advertised a 200), overstated
checkboxes (Story 3.5 marked the loader `[x]` while shipping the `Unavailable` stub — "literally why Story 3.7
exists"), source-scan "tests" that asserted text instead of exercising routes, README/architecture lagging code, and
**File-List omissions + stale test counts in nearly every story including the repair stories** (1.10 reported "25"
when it was 39; 1.11/3.7/4.8/4.9 all corrected at review). This story's entire purpose is to **stop** that pattern:

- Classify honestly — a thing that exists in code but is not wired into the host is **waived**, not "implemented".
- Sync docs to **committed reality**, not to intent.
- Report **exact** built-executable counts and a `git diff`-derived File List.
- Render **CONCERNS**, not a vanity PASS.

### Testing standards

- xUnit v3 (`3.2.2`) · Shouldly (`4.3.0`) · NSubstitute (`6.0.0-rc.1`). Test method names PascalCase. Run test
  projects individually via built executables, not solution-level `dotnet test`.
- Sandbox: VSTest socket blocked (`SocketException (13): Permission denied`). Build first, then run the built
  executable directly, e.g. `./tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests`.
- The new `LaunchReadinessTests` is a fast-baseline fitness test (no opt-in gate); it must run by default and keep
  the ArchitectureTests suite green.
- All existing and new tests must pass before the story is complete; do not skip or weaken any.

### Previous story intelligence

- **Story 4.10 (NFR11 perf evidence, just merged at `cff081c`)** is the closest precedent: it extended the perf lane,
  recorded measured verdicts + a `waived (deferred)` wire path in `docs/performance-evidence.md`, guarded it with
  `PerformanceEvidenceTests`, and was caught at review reporting stale counts (Integration 81→87, Architecture 31→33)
  and an incomplete File List. **Mirror its evidence-doc + fitness-guard pattern; avoid its count/File-List misses.**
- **Stories 1.10, 1.11, 3.6, 3.7, 4.8, 4.9** are the rehomed launch-readiness implementations you are aggregating —
  read their Dev Agent Records and Senior Developer Reviews for the exact `implemented`/`waived` boundary of each
  (especially 3.6's "concrete loader but index not host-wired" and 1.10/4.8's "adapter real but host keeps the
  fail-closed default").
- **Epic retros**: `epic-1-retro-2026-06-22.md` and `epic-3-retro-2026-06-22.md` (post-repair, authoritative) define
  Epic 5 as "verification-only" and enumerate the waiver list and the File-List honesty control. **`epic-4-retro-
  2026-06-19.md` is stale** (predates the repair; still calls 4.8/4.9/4.10 open) — verify against committed code, not
  that retro's "deferred" framing. There is no Epic 4 re-run retro.

### Git intelligence

- Recent cadence: `cff081c feat(story-4.10)…`, `24c6e23 feat(story-4.9)…`, `ab4a012 feat(story-4.8)…`,
  `c4a9183 docs(epic-3): complete closure retrospective`. This is a verification/docs story with no feature behavior:
  use **`docs(story-5.1): final launch-readiness gate and documentation sync`** (Conventional Commits → no version
  bump, which is correct for Epic 5); branch `docs/story-5-1-launch-readiness`. The added fitness test rides in the
  same commit. No new packages, no dependency upgrades, no submodule changes.
- The working tree carries an unrelated modified file (`_bmad-output/story-automator/orchestration-1-20260622-124648.md`).
  Do **not** revert or bundle it.

### Latest tech information

- No external/library research required: no new framework, API, or dependency selection. Documentation uses Markdown
  only; the fitness test uses BCL (`File.ReadAllText` + `ShouldContain`) consistent with `PerformanceEvidenceTests`.
- **Version ground truth for the doc-sync (do not "correct" the submodule — record the divergence):** `.NET` SDK
  `10.0.301` (`global.json`); `Dapr.AspNetCore/Actors/Workflow = 1.18.4` but base `Dapr = 1.17.9`;
  `Aspire.Hosting = 13.4.6` (architecture says `13.4.5`); `Microsoft.FluentUI.Components = 4.11.6` (V4, vs "V5 only");
  `xunit.v3 = 3.2.2`. Architecture documents intended versions; the pinned versions live in
  `Hexalith.Builds/Props/Directory.Packages.props` and the root `Directory.Packages.props`.

### Project context reference

Follow `Hexalith.AI.Tools/hexalith-llm-instructions.md` and the root `CLAUDE.md`. Docs + verification story: persist
nothing, add no infrastructure, edit no submodule, initialize no nested submodules. Sibling `project-context.md`
files exist for EventStore/Tenants/Parties/Conversations/Projects/FrontComposer — they are reference context, not
edit targets.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-5.1] (lines 1668-1689) — ACs; Requirements FR21/FR22/FR23,
  NFR7/NFR12/NFR13/NFR15; verdict vocabulary `PASS/CONCERNS/FAIL/WAIVED`; the eight launch-scope classification items.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-5] (lines 345-349) — "verifies evidence … after feature-
  completion work … FRs covered: none directly". (line 489) — ratified retention policy; legal-hold = launch gate.
  (lines 265, 273) — NFR7/NFR15 launch-gate map; NFR7 primary 1.4 also-in 5.1.
- [Source: _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md] §9 (line 369) retention/legal-hold;
  §10 (lines 376-379) NFR12/NFR13/NFR15; §13 (lines 410-418) Phase-5 launch-readiness criteria + "must not be the
  first implementation location"; §14 Q4 (425) secondary identity post-v1, Q5 (426) retention, Q7 (428) export
  format, Q8 (429) comment sensitivity.
- [Source: _bmad-output/planning-artifacts/architecture.md] (line 61) NFR10/NFR11 targets; (line 403) magic-link
  Status-update 2026-06-22 — loader concrete, token-hash index not host-wired; (line 983) Story 1.11 perf status,
  `TIMESHEETS_PERF=1` lane, wire path waived; (lines 739-743) fitness-test enforcement list; (lines 217-219,
  1011-1017) testing strategy; (lines 569-579) CI gates; (line 583) NFR12 logging; (line 948, 876-877) export golden;
  (line 741, 874) security tests (`Security.Tests` documented but absent). **Status notes for 4.8/4.9/4.10 are stale
  → fix in Task 2.**
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-readiness-repair.md] — Epic 5 reframed
  verification-only; rehome map (5.1→3.6, 5.2→3.7, 5.3→1.10+4.8, 5.4→4.9, 5.5→1.11+4.10, 5.6→Story 5.1);
  "story-complete vs launch-ready" success criterion.
- [Source: _bmad-output/implementation-artifacts/epic-1-retro-2026-06-22.md · epic-3-retro-2026-06-22.md] — post-
  repair authoritative retros: Epic 5 verification-only; waiver list; File-List honesty as the #1 recurring control.
- [Source: README.md] (lines 6-47) build/test; (line 51) magic-link host "does not resolve end-to-end"; (lines 37,
  47-49) perf lanes opt-in + wire-path waiver; (line 61) intentionally no UI project.
- [Source: docs/performance-evidence.md] — NFR10 (line ~40) + NFR11 (line ~77) measured verdicts; wire path
  `waived (deferred)`. [Source: docs/boundary-decision-record.md] — owns-vs-references decision; Works opt-in vs
  `DenyAll` default.
- [Source: src/Hexalith.Timesheets/Program.cs · src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs
  (:47 loader, :66 DenyAllWorkReferenceValidator, :72 UnavailableWorkPlannedEffortProvider)] — host wiring proving
  fail-closed defaults are active and the live adapters are not.
- [Source: src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs ·
  Exports/ApprovedTimeExportService.cs · src/Hexalith.Timesheets.Works/WorksQueryWorkReferenceValidator.cs ·
  IWorksQueryChannel.cs] — the real-but-not-host-activated adapters/loaders.
- [Source: tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs] — fitness pattern to
  mirror for `LaunchReadinessTests`. [DiagnosticsPrivacyTests.cs] — privacy gate. [tests/…IntegrationTests/
  MagicLinkConfirmationHttpBoundaryTests.cs · ApprovedTimeExportIntegrationTests.cs · Exports/Golden/…csv] —
  no-disclosure + export golden gates. [tests/…Projections.Tests/*ProjectionTests.cs] — projection-rebuild gate.

### Open Questions (recommended defaults applied; a change is a one-line switch)

- **Q-A — Where does the launch-readiness record live?** Default: **`docs/launch-readiness.md`** (durable, code-
  adjacent, fitness-guardable, alongside `performance-evidence.md`/`boundary-decision-record.md`). Alternative: a
  `_bmad-output/…` artifact — rejected; it would not be fitness-guarded and would drift from code.
- **Q-B — Overall verdict.** Default: **CONCERNS** (story-complete with explicit owner-named waivers; not launch-
  complete). Alternative: `WAIVED` if the PM/PO formally accepts the waiver set in this pass; `PASS` is an
  overstatement and rejected. Let the evidence justify the final wording.
- **Q-C — Fix vs only-report the architecture.md/version divergences.** Default: **fix** stale Timesheets-owned prose
  (4.8/4.9/4.10 status; the inaccurate test-project map) and **record** the submodule-pinned version divergences
  (Dapr/FluentUI) without editing the submodule. Alternative: report-only — rejected; AC2 requires docs not to make
  claims the code does not back.
- **Q-D — New fitness file vs extend `PerformanceEvidenceTests`.** Default: **new `LaunchReadinessTests.cs`** (single
  responsibility, clean fitness name). Alternative: fold into an existing fitness class — rejected; muddies intent
  and the guarded artifact.

## Dev Agent Record

### Agent Model Used

Codex (GPT-5)

### Debug Log References

- Red phase: `LaunchReadinessTests` failed with 3 `FileNotFoundException` failures while `docs/launch-readiness.md` was absent.
- Green phase: targeted `LaunchReadinessTests` passed: 3 total, 3 passed, 0 skipped.
- Restore/build: `dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` succeeded; first build hit the known transient `Hexalith.PolymorphicSerializations` submodule pack/IDE0065 issue; immediate rerun `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- Full executable suites: ArchitectureTests 36/36 pass; Contracts.Tests 88/88 pass; IntegrationTests 87 total, 83 pass, 4 skipped; Projections.Tests 77/77 pass; Server.Tests 420/420 pass; Works.Tests 76/76 pass.

### Completion Notes List

- Added `docs/launch-readiness.md` with launch-scope classifications, owner/risk/revisit details on waivers/post-v1 rows, per-gate verdicts, exact test counts, and an overall `CONCERNS` release decision.
- Synced README, architecture, boundary decision, and PRD launch-readiness notes so they distinguish story-complete from launch-complete and avoid claiming live Works host activation, valid magic-link end-to-end resolution, export-preview HTTP route, wire-path performance evidence, or a Timesheets UI.
- Added `LaunchReadinessTests` fitness coverage for the readiness record vocabulary, waiver markers, and eight epic-named launch-scope items.
- Confirmed reserved `InfrastructureLaneTests` and `PerformanceEvidenceLaneTests` static `[Fact(Skip=...)]` placeholders are untouched.

### File List

- README.md
- _bmad-output/implementation-artifacts/5-1-final-launch-readiness-gate-and-documentation-sync.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/5-1-test-summary.md
- _bmad-output/planning-artifacts/architecture.md
- _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
- docs/boundary-decision-record.md
- docs/launch-readiness.md
- tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs

### Change Log

- 2026-06-22: Implemented final launch-readiness classification, documentation sync, release-gate evidence table, and fitness guard for Story 5.1.
- 2026-06-23: QA E2E automation pass (`bmad-qa-generate-e2e-tests`) hardened the launch-readiness fitness guard — added six fitness facts to `LaunchReadinessTests.cs` covering coverage gaps the original three facts left open; synced the recorded test counts in `docs/launch-readiness.md`.
- 2026-06-23: Senior Developer Review (AI) — adversarial code review, auto-fix mode. Re-verified build (0/0) and exact built-executable counts (790/786/4/0, all six suites match the record). Fixed one MEDIUM (AC2 doc-sync gap: stale Story 4.9 export-preview status note in `architecture.md`) and one LOW (AC3 traceability: corrected the nonexistent `Contracts/Metadata/TimesheetsMetadataCatalog.cs` evidence path in `docs/launch-readiness.md`). 0 Critical / 0 High. Status → done; sprint status synced.

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-06-23 · **Outcome:** Approve (auto-fix applied) · **Mode:** non-interactive, fix-all

### Verified honest (the controls this story exists to enforce)

- **Exact test counts re-measured from the built xUnit v3 executables** (not estimates): ArchitectureTests 42/42, Contracts.Tests 88/88, IntegrationTests 87 total / 83 pass / 4 skip, Projections.Tests 77/77, Server.Tests 420/420, Works.Tests 76/76 → **790 total, 786 pass, 4 skip, 0 fail**. Matches the story and `docs/launch-readiness.md` exactly.
- **Build** `Hexalith.Timesheets.slnx --no-restore -warnaserror` → 0 Warning(s), 0 Error(s).
- **File List** matches `git status` reality exactly; the unrelated `orchestration-1-20260622-124648.md` is correctly excluded, not bundled.
- **Host-wiring waivers accurate against source:** `DenyAllWorkReferenceValidator` (`ServiceCollectionExtensions.cs:66`), `UnavailableWorkPlannedEffortProvider` (`:72`), concrete `EventStoreMagicLinkConfirmationCapabilityStateLoader` registered (`:47`); `Program.cs` maps only default/external-contribution/magic-link endpoints + `/metadata/timesheets` (no export-preview route).
- **Reserved `[Fact(Skip=…)]` placeholders** (`InfrastructureLaneTests`, `PerformanceEvidenceLaneTests`) untouched; AC1 table fills Owner/Risk/Revisit on every waived/post-v1 row; `LaunchReadinessTests` (9 facts) mirrors `PerformanceEvidenceTests`, uses the `RepositoryRoot.PathTo(...)` helper, and passes.

### Findings fixed (auto-fix mode)

1. **[MEDIUM][AC2/Task 2]** `architecture.md:462-464` still framed Story 4.9 (export preview, committed `24c6e23`) as an undecided future decision while 4.8/4.10 were synced. Reconciled the readiness-repair note to committed reality: `ApprovedTimeExportService.PreviewAsync` is concrete + tested, resolved decision is a ledger/service-driven preview with **no dedicated HTTP route** (metadata-only capability), HTTP endpoint recorded post-v1.
2. **[LOW][AC3]** `docs/launch-readiness.md` Export-preview row cited a nonexistent evidence path `…Contracts/Metadata/TimesheetsMetadataCatalog.cs`; corrected to the real `…Contracts/TimesheetsMetadataCatalog.cs`.

Post-fix: ArchitectureTests re-run 42/42 (launch-readiness fitness guard still green); build still 0/0. No Critical/High issues; overall release posture remains the honest **CONCERNS** the story records.

### QA E2E Automation Addendum (2026-06-23)

QA automation gap-fill over the existing launch-readiness feature (no behavior change; ArchitectureTests-only, per the story budget). Six fitness facts added to `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs`, closing these previously-unguarded gaps in `docs/launch-readiness.md`:

1. `Launch_readiness_record_distinguishes_story_complete_from_launch_complete` — AC1/AC2 core framing.
2. `Launch_readiness_record_anchors_evidence_to_a_baseline_commit_and_date` — AC3 traceable-evidence anchor (40-char SHA + ISO date).
3. `Launch_readiness_record_publishes_a_per_gate_release_decision_table` — AC3 per-gate table + gate names.
4. `Launch_readiness_overall_decision_is_an_honest_verdict_not_a_vanity_pass` — honesty discipline / Q-B: overall must be CONCERNS/WAIVED, never a silent vanity PASS.
5. `Launch_readiness_record_cross_links_related_evidence_documents` — Task 2 cross-links to performance-evidence/boundary-decision.
6. `Launch_readiness_record_keeps_deferred_integrations_marked_not_launch_active` — AC2 no-overstatement (live Works / magic-link e2e / preview HTTP route stay "not launch-active").

Exact built-executable counts after the pass: ArchitectureTests **42** total / 42 pass (was 36); Contracts.Tests 88/88; IntegrationTests 87 total / 83 pass / 4 skipped; Projections.Tests 77/77; Server.Tests 420/420; Works.Tests 76/76. **Total 790 tests, 786 pass, 4 intentional skips, 0 failures.** Only ArchitectureTests moved (+6). The two reserved `[Fact(Skip=…)]` placeholders remain untouched (IntegrationTests still 4 skips). QA test summary: `_bmad-output/implementation-artifacts/tests/5-1-test-summary.md`.
