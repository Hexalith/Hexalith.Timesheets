---
title: '5.1 Final launch-readiness gate and documentation sync'
type: 'chore'
created: '2026-10-20'
status: 'done'
baseline_commit: 'd72899b4489aaca0b9a9b96d0ac267165920caf8'
route: 'dispatch'
story_key: '5-1-final-launch-readiness-gate-and-documentation-sync'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Reopened Story 5.1 still presents obsolete completion, classification, UI ownership, and release evidence. The approved September course correction has not reached all artifacts.

**Approach:** Reconcile documentation and fitness guards against current code, tracking, and dated evidence; rerun available release checks and publish an honest decision.

## Boundaries & Constraints

**Always:** Apply the approved 2026-09-12 decisions, retaining subsequent SDK/package approvals. Distinguish `implemented`, `unfinished`, `waived`, and `post-v1`; waivers require approval evidence, owner, risk, and revisit condition. Preserve dated receipts. Keep release `FAIL` while deployed acceptance remains unfinished; distinguish successful historical local proof from current-graph runtime qualification. Keep story acceptance open when prerequisites remain incomplete.

**Never:** Implement features, alter hosting/dependencies/submodules, create UI projects, touch foreign infrastructure, invent approvals, weaken unrelated tests, or commit/push.

</frozen-after-approval>

## Code Map

- Baseline: `d72899b4489aaca0b9a9b96d0ac267165920caf8`; clean `main`. SDK `10.0.402`, AppHost `13.6.1`.
- `src/Hexalith.Timesheets/Program.cs` registers projection handlers and EventStore processing. Server defaults still deny Works validation and provide unavailable planned effort; preview has no HTTP route.
- `docs/launch-readiness.md` owns current classifications/gates; package section records round-8 Release evidence (1,954 passed, five skipped). Latest local journey passed on October 18 using the earlier AppHost graph; deployed acceptance and current runtime smoke remain open.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` has 15 facts, structured table readers, and catalog helpers; reuse these. Replace obsolete historical/status literals while preserving package, privacy, and persistence guarantees.

## Tasks & Acceptance

**Execution:**

- [x] `docs/launch-readiness.md` — introduce `unfinished`, correct magic-link and UI rows, resolve Q6–Q8, expose waiver approval sources, and separate current assessments from history; refresh gates with exact commands, full revisions, dates, and measured counts.
- [x] `README.md`, `docs/performance-evidence.md`, `docs/boundary-decision-record.md` — reconcile actual integration boundaries and local/deployed evidence; preserve historical performance measurements and wire-path waiver.
- [x] `_bmad-output/planning-artifacts/epics.md`, `_bmad-output/planning-artifacts/architecture.md` — apply current classifications and consumer-host UI ownership; remove forbidden UI project targets and obsolete unwired-index claims.
- [x] `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` — resolve Q6 governance, Q7 CSV, and Q8 sensitive comments; align FR22, NFR13, journeys, assumptions, and decision history.
- [x] `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` — state consumer rendering/accessibility ownership and policy-conditional comment disclosure; resolve obsolete assumptions.
- [x] `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` — synchronize managed repository guidance for SDK, workflows, tests, projection wiring, and `FAIL`; preserve the shared baseline.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` — guard structured classifications, populated waiver fields/approval sources, unfinished FR14, consumer UI ownership, and tracking consistency without hard-coding historical rounds.
- [x] `_bmad-output/implementation-artifacts/` — reconcile current portions of `5-1-final-launch-readiness-gate-and-documentation-sync.md`, `5-2-reconcile-package-currency-and-platform-dependency-versions.md`, `3-3-confirm-time-through-magic-link.md`, `4-7-surface-timesheets-dashboard-overview.md`, and `sprint-status.yaml`; record fresh checks in `tests/5-1-test-summary.md`. Preserve other stories' statuses and historical evidence.

**Acceptance Criteria:**

- Given current code and approved decisions, when artifacts are reconciled, then unfinished v1 acceptance remains visible, every waiver has traceable approval and ownership, and no artifact requires Timesheets UI projects or unconditional external comment disclosure.
- Given executable configuration and sprint tracking, when guidance and fitness checks run, then SDK/package claims and current story states agree without claiming prerequisites complete.
- Given release checks, when results are published, then all six suites have exact pass/fail/skip counts and evidence scopes; skipped, blocked, and historical checks remain explicit, and the overall verdict reflects unresolved deployed acceptance.

## Implementation Notes

- 2026-10-20: All reconciliation tasks implemented. Local Debug/source restore and warnings-as-errors build pass; latest complete suites total 1,952 passed, zero failed, five declared skips. Initial Projection scratch-cleanup failure is retained alongside the focused and complete passing retries. Readiness 13/13 and Architecture 62/62 pass.
- Parent verification checked compressed/raw receipt hashes, selected XML counts, synchronized entry points, unchanged shared baseline, and the full diff. A historical gate-row overwrite was found and corrected; separate correction receipts prove preservation. See [verification summary](tests/5-1-test-summary.md) and [current file inventory](evidence/5-1/20261020-final-gate/artifact-checks.json).
- Build implementation is in review. Story 5.1 and release acceptance stay open under the approved intent; Stories 3.6/3.7 remain in progress, Story 5.2 remains review, and release remains FAIL. No runtime, dependency, submodule, or Git publication change was made.

- 2026-10-20 parent final verification: matching solution build passed with 0 warnings/errors (6.03 s); full Architecture passed 65/65 (22.919 s), including 16 readiness cases. Latest selected six-suite totals: 1,955 passed, zero failed, five skipped (1,960 cases). Captured sources, dependency identities, binary hashes, and input-stability evidence are retained. Implementation spec is done; Story 5.1 is review with acceptance open. The frozen no-commit/no-push boundary overrides the workflow commit default.

- Final publication check: readiness 16/16 (0.372 s) against the updated counts and review statuses; no failures/skips.

## Spec Change Log

- 2026-10-20 — Human acceptance amendment: the user instructed “accept and sign-off” after reviewing the completed increment and its FAIL assessment. [Scoped acceptance](evidence/5-1/20261020-final-gate/story-acceptance.json) accepts the completed Story 5.1 reconciliation and authorizes its status to move from review to done independently of remaining release prerequisites. This supersedes only the frozen clause “Keep story acceptance open when prerequisites remain incomplete.” Release remains FAIL; Stories 3.6/3.7, Story 5.2 review, tenant/legal sign-off, policy configuration, and product UAT remain open. No release, deployment, commit, or push is authorized. The earlier implementation/verification notes retain their original checkpoint status.

- Post-sign-off verification: Architecture project build 0 warnings/errors; readiness 16/16 and full Architecture 65/65 pass. Six-suite totals remain 1,955 passed, zero failed, five skips. Exact source, acceptance, execution, and output receipts are retained in the evidence packet.

## Review Triage Log

Review pass 1 — all three layers completed; verification-gap reported no findings. Each finding was verified before grouping.

| ID | Verdict | Evidence | Route |
|---|---|---|---|
| B1 | medium | The new suite guard parses handwritten summary arithmetic without comparing selected retained XML or counts.json; internally consistent invented counts pass. | patch |
| B2 | medium | The guard only checks suite names and a numeric shape in the readiness table, and ignores the summary total row; those published counts can diverge. | patch |
| B3 | medium | Waiver validation accepts any linked file containing approved status, without requiring the actual approved course-correction source and section for the concern. | patch |
| B4 | medium | The final status loop rejects done for Story 5.2 independently of its own acceptance; the approved intent requires open dependencies to remain visible, not an unconditional permanent prohibition on package-story completion. | patch |
| B5 | medium | The disclosure guard finds policy words anywhere and rejects one exact sentence in active sections; it does not require the policy condition in each changed active disclosure criterion. | patch |
| B6 | medium | Both new status regexes end their restricted status capture directly at multiline $, leaving carriage returns unmatched on otherwise valid CRLF files. | patch |
| B7 | medium | Output hashes bind receipts, but the packet lacks a durable code/config snapshot or hash binding for the changed tested source; a base revision alone cannot identify the changed fitness code. | patch |
| B8 | low | Story 3.3 guidance and two Story 5.1 task lines were reconciled under labels claiming original historical context/execution. The actual dated Dev Agent Record receipts are preserved, but those labels misattribute edited guidance. | patch |
| B9 | low | Active Story 3.3 testing guidance still permits component conformance if UI is added, contradicting its newly explicit consumer-host ownership rule. | patch |
| B10 | low | Legal-hold sign-off and tenant-policy configuration are explicit unfinished classification rows but absent as named gate rows; direct addition makes their independent gate decisions visible. | patch |
| E1 | medium | Independently confirms B6: CRLF leaves a carriage return before the anchored line end in story and sprint parsing. | patch |
| E2 | medium | Independently confirms B2: release-gate totals and summary rows have no value comparison to each other or selected report data. | patch |
| E3 | medium | Active UX-DR31 in epics.md still lists comment disclosure unconditionally, despite the approved policy-conditional scope and other corrected journeys. | patch |

Groups: count/report correlation (B1/B2/E2); scoped approval source (B3); independent story completion (B4); active disclosure criteria (B5/E3); newline parsing (B6/E1); source binding (B7); historical attribution (B8); remaining UI test ownership wording (B9); independent policy gates (B10). All fixes are bounded corrections to the existing documentation, evidence, or fitness checks, with no public surface or runtime behavior added. No intent or specification loopback is required.


All 13 findings (B1–B10, E1–E3) are resolved by the bounded patch groups above. Parent matching solution build and unfiltered Architecture execution pass; no review finding is deferred. Retained failure receipts and earlier source-provenance limits remain explicit.

## Verification

- Restore `Hexalith.Timesheets.slnx` with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`, `-p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`; build with matching Debug/source-reference mode, `--no-restore -warnaserror`. If Works/catalog EventStore versions diverge, use the catalog version explicitly and record the existing workaround.
- Run the built Architecture executable with `-class '*LaunchReadinessTests'`, then each of the six built test executables individually with separate `-xml` reports. Require zero failures; retain declared skips and existing performance opt-in behavior.
- Compare normalized entry-point text, scan current artifact sections for contradictions, verify evidence links and actual changed-file list, and run `git diff --check`. Report exact blockers; final launch closure awaits owning-story acceptance.
