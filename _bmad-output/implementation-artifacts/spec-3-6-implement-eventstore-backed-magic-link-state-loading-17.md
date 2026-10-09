---
title: 'Close Story 3.6 privacy scope and evidence review patches'
type: 'bugfix'
created: '2026-10-09'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'c75de3c2f6328e63e77682b6d3cac6c54a7ded87'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The latest Story 3.6 review found that privacy assertions reject tenant identifiers in Timesheets diagnostics despite the accepted response-only policy. Four evidence records also misstate the scope, provenance, or current status of verification.

**Approach:** Keep tenant identifiers forbidden in external responses while allowing them in diagnostics, prove both scopes in the passing privacy-helper lane, and close the five accepted review patches in the existing story and evidence records. Preserve the deferred EventStore authorization fix and the overall FAIL release decision.

</frozen-after-approval>

## Implementation Notes

- Existing 2026-10-09 review at the end of the Story 3.6 record identifies five unchecked patches. The code patch is confined to the test helper and theory in `MagicLinkConfirmationHttpBoundaryTests.cs`; the other patches are corrections to the story, launch record, deferred-work entry, test summary, and README.
- No intent gap or irreversible action is needed. This increment adds no production API, dependency, or persistence change. The prior SDK security regression remains owned by the deferred EventStore domain-service contract work.
- The pre-edit explicit AppHost start exited 0, `aspire wait timesheets --timeout 30` exited 18 because `APP_API_TOKEN` and JWT Authority/SigningKey are missing, and explicit stop exited 0.
- Moved fixture tenant exclusions from the shared Timesheets diagnostic helper to a response-only helper. Added twelve acceptance cases for tenant identifiers in Timesheets and framework category, message and structured state. The response cases still reject both identifiers.
- Corrected AC4 regression wording, spec-16 scratch-run provenance, superseded helper and HTTP claims, and the EventStore versus Builds attribution. The story keeps the five original review findings and adds individual closure evidence; no production source, dependency pointer, or configuration changed.
- SDK `10.0.401` source-reference Debug restore/build passed with zero warnings/errors. The focused privacy theory passed 432/432. All six direct test executables ran: Architecture 55/56; Contracts 90/90; Server 482/482; Projections 150/150; Integration 508 pass / 42 fail / four skips; Works 76/76. Overall 1408 total / 1361 pass / 43 fail / four skips. The 42 integration failures are unchanged unexpected 401s and the one Architecture failure is the pre-existing package-verdict mismatch. The four scratch-root HTTP cases reran against this build and all failed at 401 before privacy assertions. Logs use `/tmp/timesheets-story-3-6-spec-17-*.log`.
- Review found a stale sprint comment and file inventory, a stale export-gate pass count, and missing mixed-case response coverage; these received small corrections. Two pre-existing HTTP assertion gaps (headers and early 401 bodies) are recorded as deferred work because they require separate response-surface coverage and do not make the blocked 401 route acceptable.
- After those review patches, the final source-reference Debug build passed with zero warnings/errors; the focused helper passed 432/432. All six final direct lanes retained the same totals and 43 existing failures. The four scratch-root cases reran against the final build and failed at 401. `git diff --check` passed.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Sprint tracking still says five patches open and omits AC4 | low | patch | The five boxes are checked in the story, so the tracking comment now says they closed and records AC1/AC3/AC4. |
| Current cumulative File List omits spec-17 | low | patch | Added the new spec to the story inventory. |
| Export gate retains 496 Integration passes | low | patch | Current Integration lane has 508 passes; aligned the gate with the full-suite row. |
| Response headers are compared but not scanned for protected material | medium | defer | `HeaderSet` supplies equality only; identical protected headers would pass. This predates this increment and is in the spec-17 deferred-work section. |
| Status assertion prevents body privacy checks on current 401s | medium | defer | `CaptureFailureAsync` asserts 403 before its body scan. The old test gap is in deferred work; the 401 route remains a failing gate. |
| Lowercase-only tenant response theory misses a case-sensitive regression | medium | patch | One response case now uses uppercase `TENANT-2` while the other retains lowercase; 432/432 pass. |
| Scope note omits README | low | patch | Added README to the spec's agent-owned implementation note. |
