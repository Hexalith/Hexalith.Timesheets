---
title: 'Correct Story 3.6 review line references'
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

**Problem:** The Story 3.6 review of `bb3ec0e..07a7411` left two record patches: three citations still use line numbers that the same commit shifted, and the `09e11a7` title patch says "27 of the 29 added lines" where the review section and Change Log row are 25.

**Approach:** Replace those line numbers with the named sections and change 27 to 25. Leave production code, tests, submodule pointers, and the open acceptance and release gates unchanged. Keep the story and sprint status in-progress until finalization.

</frozen-after-approval>

## Implementation Notes

- Clean `main` baseline: `a6cc4605f565cf616aa18b87e21dec59300df498`. Sprint key `3-6-implement-eventstore-backed-magic-link-state-loading` is `in-progress`. Cached Epic 3 context is valid. No lower-story build spec with `status: done` exists; Story 3.5 is a completed story file, not a build spec.
- The two open patches are the only unchecked `[Review][Patch]` items, in `### Review Findings — review-record commit (2026-10-05)`. Do not reopen deferred acceptance work: live EventStore topology, non-Fresh catalog distinctions, durable atomic confirm/adjust writes, the deployed Activity Type inventory, and the release `FAIL`.
- In the Change Log row for the review-patch commit (`aab64cf..09e11a7`), replace `at line 886` with `in the record-correction review's second patch`. That row is the one that says the Server project was named in the EventStore breakdown. Do not edit the newer review-record row except to leave it as history.
- In `**Patch disposition (2026-10-05, applied in review).**` under `### Review Findings — review-patch commit (2026-10-05)`, replace `Line 886 now names` with `The record-correction review's second patch now names`.
- In `_bmad-output/implementation-artifacts/deferred-work.md`, in the 2026-10-05 frozen-intent entry, replace `story line 893` with `the record-correction review's first rejected bullet`. That bullet is the first `**Rejected**` item under `### Review Findings — record-correction increment (2026-10-05)`.
- In the checked title patch under the review-patch commit section, change `27 of the 29 added lines` to `25 of the 29 added lines`. Leave the rest of that finding verbatim.
- Insert one Change Log row, dated 2026-10-05, directly under the table header and above the review-record row. It names this closure, the section-reference replacements, the 27-to-25 correction, and that no production code changed and no new test run is claimed. End it `Status → review` only as the historical close-out of these two patches; do not use that sentence to set the story done.
- Add this spec path to the File List evidence bullet, after `spec-3-6-implement-eventstore-backed-magic-link-state-loading-11.md`. Then check the two patch boxes.
- Do not insert new line numbers anywhere. Do not edit launch readiness, the test summary, or sprint status beyond the workflow status sync. Verification is a read-back that the three old line citations are gone from those three targets, that the title patch says 25, that both boxes are checked, plus `git diff --check`.
- Applied the three section-reference replacements, the 27-to-25 correction, the closure Change Log row, the File List spec path, and the two checked boxes. Sprint status was already `in-progress`, so the in-progress sync left it unchanged. `git diff --check` passed. The checked finding text still quotes the old line numbers and the old count; those sentences are the review record, not the three targets. Acceptance and release gaps stay open.
- Blind Hunter review (edge-case, verification-gap, and acceptance-auditor layers are not in this oneshot step). Restored `in the EventStore breakdown` so the Change Log replaces only `at line 886`. Added a patch-disposition paragraph for the two checked patches. Named the record-correction increment heading in the ledger pointer. Finalization sets the story header and sprint key to `review` and rewrites the sprint comment that still listed the two patches as open. No production code changed.

## Review Triage Log

- low / patch — The closure Change Log row ended `Status → review` while the story header was still `in-progress`. Finalization sets the story header and sprint key to `review`, which is the close-out that row records. Acceptance and release gaps stay open, so the story is not `done`.
- low / patch — The sprint comment still said the `bb3ec0e..07a7411` review left two patches as action items. The review sync rewrites that comment with the closure.
- low / patch — The checked review-record patches had no disposition paragraph, so the bullets still read as open instructions. Added a paragraph naming spec-12, the section references, and the 25 count. Finding text stays verbatim.
- low / patch — The review-patch Change Log edit dropped `in the EventStore breakdown` along with `at line 886`. Restored that phrase so the row still matches the spec's locator.
- false — The closure row treats the second patch and the first rejected bullet as one pair, and it does not mention the deferred bullet's `line 893` locator. The row names the two section references the patch required. The Change Log and disposition use the second patch; the ledger uses the first rejected bullet. The deferred bullet's line citation is the reviewed revision, which the prior review already accepted.
- low / patch — The ledger phrase is not text inside the target bullet, so a string sweep cannot land on it. Added the `### Review Findings — record-correction increment (2026-10-05)` heading beside the prescribed phrase.
- false — Spec context omits `deferred-work.md`. Context is optional, and Implementation Notes name the ledger. A prior review rejected the same context-list finding.
- false — The test summary and launch readiness still present the prior increment as current. This change has no new test or release fact, and the spec forbids editing those records. The prior six-lane run remains the current evidence.
