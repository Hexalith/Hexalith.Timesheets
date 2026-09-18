---
title: 'Close Story 3.6 terminal review patches'
type: 'chore'
created: '2026-09-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - '_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 remains in progress even though its accepted loader implementation is complete: the terminal independent review left six evidence and tracking patches, including stale AppHost smoke wording, duplicate or obsolete deferred entries, and incomplete ownership/checklist records.

**Approach:** Close only those six terminal patches without changing production behavior or deferred policy: explicitly own the accepted submodule-pointer history, preserve the intentional in-progress lifecycle until presentation, make runtime-smoke evidence honest for the current dependency graph, deduplicate or retire resolved ledger entries, and synchronize both Story 3.6 review records before rerunning proportionate verification.

</frozen-after-approval>

## Implementation Notes

- Confirmed the accepted Story 3.6 loader, projection discovery, authoritative folds, and focused tests are complete; no production-code change was needed.
- Recorded the human-approved submodule-pointer exception through current HEAD without moving any gitlink, and reconciled the two already-completed checklist items.
- Retired the resolved Test SDK deferral, replaced the duplicate agent-context entry with a pointer to its original open item, and retained every independent deferred product or infrastructure risk.
- Relabeled the 2026-09-15 AppHost run as historical because it predates the current Builds catalog, then changed the fitness assertion to require the current-graph evidence gap instead of stale success wording.
- Verified the changed worktree based on `16219dee5162420e976b65f755e0eca2cf43715a` with restore, a warnings-as-errors solution build (0 warnings/errors), and all six direct xUnit v3 executables: 945 total, 941 passed, 4 declared integration/performance skips, 0 failures. Current direct-package, vulnerable, and deprecated audits also passed for all 15 Timesheets projects.
- Patched every verified Blind Hunter finding, then rebuilt and reran ArchitectureTests (55/55) after strengthening the structured Build-gate assertion.

## Review Triage Log

- **medium / patch:** The accepted Works pointer decision stopped at `3c042f9` although current history advances to `28724f2`; the decision now records the intermediate and final pointers.
- **medium / patch:** Older agent-context findings remained duplicate open entries; they now cross-reference one canonical 2026-09-16 deferred item.
- **medium / patch:** The sprint header still described six open patches; it now records that the terminal patches are resolved pending presentation.
- **low / patch:** The canonical cumulative File List omitted this terminal spec; it is now included.
- **low / patch:** “Verified current HEAD” overstated what an uncommitted worktree run proves; verification now names the full base commit and changed worktree.
- **low / patch:** The resolved Test SDK note said `18.10.0` survived only in planning although historical review findings also retain it; the note now names both record classes.
- **low / patch:** Historical smoke prose used the ungrammatical “therefore was verified”; corrected to “was therefore verified.”
- **medium / patch:** The package fitness test did not inspect the structured Build row, allowing current/historical smoke wording to drift; it now asserts current compile evidence and the deferred current-graph runtime gap.
- **medium / patch:** The Build gate cited only older build/runtime evidence despite the current worktree run; it now records the dated base commit, current restore/build result, and historical-only runtime smoke scope.

### Review Findings

Independent review of `16219dee...26d53c0` (2026-09-17). Layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor.

**Decision needed**

- [x] [Review][Decision] Story and sprint lifecycle advanced to `review` while frozen intent still requires `in-progress` until presentation — spec-4 Approach and spec-3 Decision A require keeping story and sprint at `in-progress` until the presentation step. This increment checks Decision A as done, sets story Status, `sprint-status.yaml` 3-6, `docs/launch-readiness.md`, and `LaunchReadinessTests` to `review`, and leaves the 2026-09-17 verification paragraph plus spec-3 Implementation Notes saying `in-progress` pending review. Fitness tests now lock `Story 3.6 is in review`, so the next correction cannot proceed without choosing which lifecycle is authoritative. **Resolved 2026-09-17 — option 1.** Restore `in-progress` until this review's presentation: revert story Status, sprint 3-6, launch-readiness, and `LaunchReadinessTests` pins.

**Patch**

- [x] [Review][Patch] [From decision 1] Restore Story 3.6 and sprint to `in-progress` until presentation, and retarget launch-readiness plus `LaunchReadinessTests` off `Story 3.6 is in review` [_bmad-output/implementation-artifacts/sprint-status.yaml:79] — story, sprint, readiness, and fitness evidence now agree for the active review run.
- [x] [Review][Patch] Historical endpoint-split prose reads "The split is was therefore verified" [docs/launch-readiness.md:96] — corrected to "The split was therefore verified."
- [x] [Review][Patch] VG-01 ledger evidence still says ArchitectureTests require the retired 2026-09-15 `aspire start` / `security` Healthy sentences, and closed-patch citations still point at `deferred-work.md:243` / `:230` after those rows moved [_bmad-output/implementation-artifacts/deferred-work.md:243] — corrected the VG-01 evidence and retargeted the closed citations to `:233` / `:223`.
- [x] [Review][Patch] InternalSurfaceGuard live-smoke rewrite is unpinned: `Launch_readiness_record_captures_package_currency_verdict_dimensions` never reads that paragraph, so restoring current-tense verification there stays green [docs/launch-readiness.md:89] — the fitness test now asserts the historical dependency-graph scope and current-runtime disclaimer directly.
- [x] [Review][Patch] Build-gate evidence cites `16219de4d26e6c191fa1c016d476eb3eb624e1eb`, which is not a git object; HEAD's parent is `16219dee5162420e976b65f755e0eca2cf43715a` [docs/launch-readiness.md:113] — readiness and its structured test now pin the valid canonical commit.

**Deferred**

- [x] [Review][Defer] Completion Notes and the 2026-06-22 senior-review carry-forward still say the token-hash index has no live `IDomainProjectionHandler` wiring [_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md:514] — deferred: pre-existing; the 2026-09-16 supersession already records live handlers, and this increment did not rewrite historical completion notes.

**Rejected**

- `false` — no dedicated AppHost runtime-smoke row in the release-gate table: Build notes and the package-currency paragraph already state current-graph smoke is deferred, overall FAIL still names live-topology, and spec-4 did not require a new gate.
- rejected per rule — spec-4 frozen `context` omits the files this oneshot changes: the fix would edit the spec under review.
- `low` — `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` Current Verification omits the historical-vs-current smoke reclassification: everyday maintainers use launch-readiness and the story changelog; the summary already records the 945-test inventory, and expanding it is extra documentation.
- `false` — spec-3 Implementation Notes skip intermediate Works pointer `3c042f9`: the story Decision line records `06d64b0`→`3c042f9`→`28724f2`; start→HEAD in the notes is not a contradictory ownership claim.
- `false` — `review` overstates closure because earlier-round `[ ]` patches remain: `review` is the sprint workflow state for this increment, not a claim that historical ledger items are this increment's work.
- `false` — Dev Agent Record Debug Log still lists ArchitectureTests 27 / Server.Tests 395: that block is the original implementation record; later 2026-09-17 verification sections carry the current 945-test evidence.
- `false` — a prior-round rejected note still describes tests pinning “Story 3.6 remains in progress”: that paragraph documents a previous rejection; it is not the live fitness contract.
