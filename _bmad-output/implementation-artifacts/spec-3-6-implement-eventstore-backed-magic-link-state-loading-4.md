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
- Verified the changed worktree based on `16219de4d26e6c191fa1c016d476eb3eb624e1eb` with restore, a warnings-as-errors solution build (0 warnings/errors), and all six direct xUnit v3 executables: 945 total, 941 passed, 4 declared integration/performance skips, 0 failures. Current direct-package, vulnerable, and deprecated audits also passed for all 15 Timesheets projects.
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
