---
title: 'Reject duplicate scheduler JSON names before preservation evidence'
type: 'bugfix'
created: '2026-11-04'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized deferred-work maintenance">

## Intent

**Problem:** The remaining scheduler parser deferral is reproducible: JSON with
duplicate object names can overwrite a nonempty range with an empty range before
preservation fingerprinting.

**Approach:** Reject duplicate names at every scheduler JSON object level before
fingerprinting, retain the existing sanitized refusal receipt, close the response
and restore deadline state. Verify valid empty/nonempty responses and duplicate
header, range and row names through transport doubles and owned loopback HTTP.
Keep transport, byte/time bounds, shared-state gates and launch FAIL. Complete
locally actionable work even if fewer than five entries close, as the user
explicitly selected during this run. Record the one verified closure separately
from the remaining external qualification obligations. Do not count already
resolved entries again. Commit and push the reviewed work to main as requested.

</frozen-after-approval>

## Implementation Notes

- Baseline: clean main at `d91d22e3409858d7c156428c55fdb05a5b8f15a0`.
- The ledger's final duplicate-name entry is open. Other apparent candidates
  already contain resolutions with inconsistent capitalization/indentation.
- Reuse `read_only_shared_snapshot`, `scheduler_fingerprint`, the existing six-key
  refusal helper, deadline context and owned HTTP transport. Add only a local
  object-pairs hook at the scheduler decode; unrelated JSON decoders are unchanged.
- No intent gaps or implementation irreversibles. Footprint: two Python files,
  this spec, ledger and a verification report with compact command receipts.
- No deployed or shared service access is needed for the parser fix. Remaining
  five-item selection is pending clarification; no external acceptance is inferred.

- Proceeded with source audits of four genuinely open obligations while fixing
  the parser: manifest capacity, manifest construction cost, persistent native
  signal API failure, and complete large/active Redis preservation. None has
  sufficient evidence for closure; do not count prior resolved entries again.
- The first full fixture run executes 266 tests and reports two failures because
  an existing JSON deadline test double did not forward keyword arguments.
  Forwarding the new parser hook preserves its actual parse/fingerprint deadline
  coverage; all three deadline phases pass the focused recheck.
- Final complete offline fixture command passes 266/266 without failures,
  errors or skips in 59.960 test seconds. Source-bound command receipts retain
  both the initial failed run and final pass separately. Four selected audit
  obligations remain open with concrete closure requirements; no deployed or
  shared-service access, .NET build or broader runtime qualification is claimed.
- The user answered the scope clarification: complete locally actionable work
  even if fewer than five entries can close. Updated the frozen intent only to
  record this explicit human scope decision. One parser fix satisfies the local
  scope; the four investigated qualification obligations remain open.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Escaped aliases also duplicate count | medium: ordinary duplicate count could mask alias-specific coverage | Patched both transport-double and HTTP inputs to contain a single count. |
| No duplicate-count-only response | low: root-name validation is generic, but the field can independently mask range evidence | Added a count-only duplicate whose final count agrees with the rows. |
| Reordered positive input bypasses decoder | low: prior expected fingerprint alone did not exercise decoding reordered wire bytes | Send reordered valid JSON through all three owned HTTP framings. |
| No positive unique escaped name | low: duplicate rejection must retain legal escaped-name input | Added a valid unique escaped-name HTTP response in all three framings. |
| Nested duplicate coverage over HTTP incomplete | low: the intent calls for header/range/row checks across doubles and HTTP | Added nested header and row duplicates through real owned HTTP. |
| Command metadata lacks output digests | low: adjacent logs were not directly bound by the JSON receipt | Add compressed/plain log SHA-256 values and a packet manifest. |
| No before/after source equality | medium: concurrent edits could invalidate source-at-start evidence | Final command captures both maps and requires equality; historical after hashes are not invented. |

- No new code defect or deferred review finding was identified. The parser
  implementation is unchanged by review; the final test matrix has seven
  direct duplicate cases and 21 owned HTTP cases across three framings.
- Post-review complete fixture verification passes 266/266 in 63.035 test
  seconds, with identical source hashes before/after and compressed/plain
  output digests. The exact baseline runner fails all seven final direct
  duplicate-name cases. The earlier 59.960-second pass remains pre-review
  evidence; final receipts bind the expanded review matrix.
- Independent final recheck passes all seven original findings, current
  before/after source hashes, both output digests, all eleven packet manifest
  entries and all six source-audit hashes. No test or service ran during the
  recheck. Conventional Commit validation passes before publication.
