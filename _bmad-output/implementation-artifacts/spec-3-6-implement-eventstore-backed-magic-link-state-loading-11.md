---
title: 'Record the Story 3.6 patch-closure evidence'
type: 'bugfix'
created: '2026-10-05'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Story 3.6 review of `15d085c` left four record patches: the patch-closure increment has no Change Log row, older evidence is still labeled current, the closure spec is missing from the File List, and the reconciliation note points only upward.

**Approach:** Correct those four records in place and check the patches. Leave production code, tests, submodule pointers, and the open acceptance and release gates unchanged. Keep the story and sprint status `in-progress`.

</frozen-after-approval>

## Implementation Notes

- Clean `main` baseline: `c9f543f0623c7cde775eac99d4f39ce70fb4c84e`. Sprint key `3-6-implement-eventstore-backed-magic-link-state-loading` is `in-progress`. Cached Epic 3 context is valid. No lower-story build spec with `status: done` exists; Story 3.5 is a completed story file, not a build spec.
- The four open patches are the only unchecked `[Review][Patch]` items, at `3-6-implement-eventstore-backed-magic-link-state-loading.md:850-853`. Older findings are already checked. Do not reopen deferred acceptance work.
- Change Log is reverse-chronological at line 538. The newest row (line 542) is this review and already ends `Status → in-progress`. Insert the missing closure row immediately after it and before the alternate-configuration review row. The row is dated 2026-10-05, names commit `15d085c`, names the eight closed patches (fail-closed `InternalSurfaceOptions` defaults, non-monotonic MessageId ordering, ledger count and resolution note, test-double sentence, deferred-from heading colon, readiness link, prior-review label), cites 0 warnings/errors and 957 total / 953 pass / 4 skips, and ends `Status → review` for the already-resolved decision. Do not edit the newer review row.
- File List evidence bullet (line 535) lists specs `-2` through `-10` and omits `_bmad-output/implementation-artifacts/spec-3-6-close-current-review-patches.md`. Add that path after the `-10` entry. Then check the four patch boxes.
- Current Review Disposition (line 28) still says story status remains `review`, while the header (line 7) and `sprint-status.yaml:79` are `in-progress` after `c9f543f`. Change only that status sentence to `in-progress`. Leave the acceptance-gap sentences.
- `docs/launch-readiness.md:119` still opens `Latest Story 3.6 historical-review reconciliation`. Relabel that paragraph `Prior`. Leave the newer 2026-10-05 paragraph at line 117.
- `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md:20` `Current reconciliation increment` and line 27 `Current runtime check (2026-10-04` become `Prior`. Append the closure increment's pre-edit Aspire result to the line-9 bullet: start succeeded, Timesheets finished with exit code 134, Aspire was stopped, and this is not a live EventStore proof. The same result is already in `spec-3-6-close-current-review-patches.md` and `docs/launch-readiness.md:117`.
- `_bmad-output/implementation-artifacts/deferred-work.md:343` ends `or the inventory entry above.` Change it to `or the inventory entry above, or the three residual annotations below.` Those annotations are the transport-diagnostics, Rebuild-helper, and test-double entries directly under the note. Do not add or rewrite ledger entries.
- No production, test, configuration, or submodule file changes. Verification is a read-back of the four citations plus `git diff --check`. Do not claim a new test run.
- Applied the four record corrections. Sprint status was already `in-progress`, so the in-progress sync left it unchanged. The closure Change Log row sits directly under the newer review row. The File List now names `spec-3-6-close-current-review-patches.md` and this increment's spec. The four patch boxes are checked. Disposition status, readiness label, test-summary labels and Aspire sentence, and the ledger below-pointer are updated. No production files changed.
- Review added a current test-summary bullet for baseline `c9f543f0623c7cde775eac99d4f39ce70fb4c84e`, labeled the closure bullet Prior, and recorded `aspire describe` Finished / exit 134 with no captured bind-failure cause. Added this increment's Change Log row and a patch-disposition paragraph. `git diff --check` passed. Finalization sets story and sprint status to `review`; the frozen in-progress sentence covered the edit, and the already-resolved lifecycle decision treats the review transition as the close-out step. Acceptance and release gates stay open. Earlier notes that describe the patches as unchecked are the pre-edit map.

## Review Triage Log

- low / patch — The current test-summary bullet still said the story remains in review. Relabeled that bullet as the prior closure increment and added a current bullet for this documentation increment.
- false — Launch readiness already says Story 3.6 is in review at the opening and the overall decision. Finalization restores that status, so those sentences match the story header.
- low / patch — The Change Log had no row for this correction, so its newest row still said four patches were open. Added a 2026-10-05 row above that review row. The `15d085c` row still ends `Status → review` because that was the historical transition the original patch required.
- low / patch — `sprint-status.yaml` still said four patches were open. The review finalization sets the story to `review` and replaces that comment.
- false — The checked Change Log patch still cites line 542. That citation is the original finding location. The closure row was inserted on the next line, and historical finding text stays verbatim.
- false — The reconciliation note uses the review's prescribed phrase, "the inventory entry above, or the three residual annotations below." Those three entries are the next ledger items.
- low / patch — The four checked patches had no disposition paragraph. Added one. The 2026-10-04 disposition heading dates that section, and the rejected finding's quote of "Story status remains review" is historical review text.
- low / patch — Current Verification did not name this increment, and the Aspire sentence omitted `Finished` and a distinction from the port-8080 failure. The new current bullet names baseline `c9f543f0623c7cde775eac99d4f39ce70fb4c84e` and states that no new test run was made. The prior bullet now includes the describe/Finished result and says no bind-failure cause was captured.
- low / rejected — The spec's early notes still describe the pre-edit map, and its context list omits three files named in the notes. Implementation Notes are append-only; the applied bullet supersedes the map. Context is optional. `git diff --check` passed and is recorded above. Rewriting the plan notes or expanding context would not change the records.
