---
title: 'Close Story 3.6 terminal review follow-up patches'
type: 'chore'
created: '2026-09-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - '_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-4.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's terminal review decision is only partially applied: the story and sprint are `in-progress`, while launch readiness and its fitness tests still say `in review`; four evidence defects also remain in historical smoke wording, deferred-ledger references, assertion coverage, and the recorded build baseline.

**Approach:** Close exactly those five accepted review patches without changing production behavior or deferred ownership: synchronize every live lifecycle surface to `in-progress` until presentation, correct and pin the historical/current runtime distinction, repair the ledger evidence and citations, record the canonical build-base commit, and reconcile both review checklists with focused ArchitectureTests verification.

</frozen-after-approval>

## Implementation Notes

- Kept Story 3.6 at `in-progress` across the canonical story, sprint tracker, launch-readiness record, and fitness assertions for the active review run; the workflow will advance the sprint only after review completes.
- Corrected the historical endpoint-split sentence and replaced the fabricated build-base SHA with canonical commit `16219dee5162420e976b65f755e0eca2cf43715a`.
- Pinned the InternalSurfaceGuard paragraph's historical dependency-graph scope and current Aspire/Keycloak compatibility disclaimer in `LaunchReadinessTests`.
- Reworded the duplicate VG-01 ledger evidence to match the current fitness assertions, repaired the two moved-line citations, and reconciled both Story 3.6 review checklists without changing production code, topology, package ownership, or deferred risks.
- The first focused run exposed two assertion-shape mistakes in this increment: combined lifecycle prose hid the standalone Story 5.2 statement, and the new section reader used boundaries in document-reverse order. The corrected wording and control-section boundaries retain the intended evidence without broadening scope.
- Final focused verification passed: `git diff --check`; `git cat-file -e '16219dee5162420e976b65f755e0eca2cf43715a^{commit}'`; warnings-as-errors ArchitectureTests build with 0 warnings/errors; and the direct xUnit v3 `-class Hexalith.Timesheets.ArchitectureTests.FitnessTests.LaunchReadinessTests` lane with 12/12 passing.
- The first post-review rerun caught the Markdown line wrap inside the pinned "The split was therefore verified" sentence; the assertion now pins the exact wrapped sentence and still falsifies the original "is was" wording.
- After the independent review and patch rerun passed, the presentation transition advanced the canonical story, sprint tracker, launch-readiness record, and fitness assertions together from `in-progress` to `review`.

## Review Triage Log

- **medium / patch:** The sprint header still described five open patches after all five were closed; it now records closure pending presentation and is superseded by the synchronized review transition.
- **medium / patch:** The canonical changelog ended with a `Status → review` entry that contradicted the active `in-progress` header; a dated 2026-09-18 row now records this follow-up and its presentation transition.
- **medium / patch:** The deferred ledger represented the same AppHost runtime-smoke risk as two structured items; the later occurrence is now a plain cross-reference to the single canonical item.
- **low / patch:** This increment lacked reproducible verification evidence; Implementation Notes now record the exact Git check, warnings-as-errors build, focused executable command, and 12/12 result.
- **low / rejected:** The 2026-09-17 Build row cannot name an immutable endpoint for an intentionally uncommitted tested worktree. It already records the canonical base, date, commands, and outcome; manufacturing a self-referential commit endpoint would add process without improving the historical claim.
- **medium / patch:** Lifecycle assertions used broad document-wide substring checks; they now isolate the opening and final decision statements and reject the conflicting lifecycle phrase.
- **medium / patch:** Historical-smoke assertions searched independent fragments across the full control section; they now isolate the dated smoke paragraph through its residual-risk boundary and pin the corrected sentence plus current-runtime disclaimer.
