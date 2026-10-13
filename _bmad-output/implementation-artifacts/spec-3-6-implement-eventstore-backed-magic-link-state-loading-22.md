---
title: 'Recover committed external recordings after a lost submit response'
type: 'bugfix'
created: '2026-10-13'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Story 3.6's recording path can persist an external Time Entry but return an
opaque denial when the submission transport response fails. Recover that
outcome through the existing bounded command-status polling and exact stored
recording verification, without another submission. Preserve trusted actor
and source identity, preflight retry semantics, comment decisions, rejection,
receipt mismatch denial, and caller cancellation. This increment covers a
failed submit response; independent observation of a still-pending response
and a separate readback latency policy remain outside it.

Use the existing helper in `MagicLinkDurableSubmissionService`; add recording
service and mapped HTTP regressions, including persisted recording contents
and optional submission evidence. Keep default-deny production authority,
legacy data, AppHost, ServiceDefaults, dependencies and gitlinks unchanged.
Story 3.6 and release remain in-progress and FAIL until the certified legacy
and deployed acceptance gates close. Leave changes uncommitted under the
repository's explicit Git safeguard.

</frozen-after-approval>

## Implementation Notes

- Investigation found the concrete follow-up at `deferred-work.md` under
  final materialization review. `TimeEntryRecordSubmissionService.SubmitAsync`
  already verifies Completed status and exact stored events; only the
  awaited response fault currently prevents those checks. Catch that fault
  in `SubmitAndReadStatusWithPreflightAsync`, retaining its deadline and
  mismatched accepted-message refusal. Its transition caller shares the
  same recovery behavior and must retain existing regression coverage.
- Initial repository was clean on main at
  `a37f4d42defa89bab630d40b74540bdea926043a`. The epic context cache is valid;
  the latest lower completed story is 3.5. The materialization, v1 migration
  and fixture-hardening increments are already done. The stale spec-21 draft
  is preserved; it predates those increments.
- Baseline command:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-record-response-baseline`
  exited 1: `Matching Dapr app ID exists; no AppHost started`. Sanitized
  evidence: `/tmp/timesheets-36-record-response-baseline/run-oonsusu5`.
  No fixture resources were started or foreign runtime stopped.
- Added six lost-response service cases and two HTTP cases before changing
  production code. The pre-fix source-reference build passed in 42.92s with
  zero warnings/errors; all six service cases and both HTTP cases failed,
  reproducing the response-first gap. Logs:
  `/tmp/timesheets-36-record-response-red-server.log` and
  `/tmp/timesheets-36-record-response-red-http.log`.
- The shared helper now catches a failed submit response while its original
  linked deadline is active, then polls the same MessageId. Caller/deadline
  cancellation and a mismatched accepted identity still stop recovery.
  `WasPresentAtPreflight` and exact recording/submission receipt checks are
  unchanged. No new public API or persistence path was added.
- Focused post-fix service tests passed 33/33, including recording and shared
  durable-submission coverage; HTTP recovery, uncertainty and concurrent
  retry selections passed 5/5. Final source-reference warning-as-error Debug
  solution build passed in 7.63s with zero warnings/errors. Additional
  service cases protect caller cancellation, wrong accepted identity and
  missing terminal status within a 100-ms polling deadline. Exact commands
  and broad results will be recorded in the story test summary.
- The first full Integration run exposed a pre-existing regression in the
  preceding GET privacy-header patch: invalid POST responses lacked the
  headers now present on GET. The existing all-route no-disclosure matrix
  failed on `confirm-submit:malformed`. Added the existing header helper at
  the start of both public POST handlers to preserve Story 3.6's uniform
  denial contract; no route, response body or authorization behavior changed.
  The completed run and final corrected-tree run will both be retained.
- Independent review exposed transient status-query faults after a lost
  submit response. The same helper now retries classified observation
  failures inside the unchanged deadline; nonretryable status failures stay
  opaque. Tests additionally cover missing then Processing then Completed,
  transport cancellation while the caller remains active, and revoke/expire
  response loss with exact versus corrupted stored terminal events. The
  deadline regression now consumes 800 ms of a one-second budget before
  the fault, checks the original budget with 500 ms scheduling tolerance,
  and verifies the original submission token is cancelled.
- Final reviewed solution build passed in 8.17s with zero warnings/errors.
  Six individual default suites passed 1,899 cases with five declared skips,
  zero failures, 1,904 total; focused reviewed HTTP passed 11/11. Source
  corrections were independently rechecked with no remaining issue. Exact
  commands and failed/passing provenance are in `tests/3-6-test-summary.md`.
- A fresh private Dapr run failed at metadata HTTP 500 before domain commands;
  its initial shutdown left three captured processes past the runner window.
  All 14 captured identities later exited, and guarded follow-up removed only
  its three containers/component copies. Original journey preservation
  matched; the later cleanup interval had a changed shared DB0 digest, with
  no cause attributed or repair made. A diagnostic retry refused a foreign
  Works eventstore sidecar. No passing new live proof is claimed.
- Parent Story 3.6 and Epic 3 remain in progress, with their required deployed
  acceptance still unchecked; this completed scoped follow-up does not advance
  the parent to review. No staging, commit, push, branch, dependency/gitlink or
  AppHost/ServiceDefaults change was made.

## Review Triage Log

- **medium, patch:** A status 503 after the lost submission response aborted
  recovery despite available polling time. Reuse the existing retryable
  gateway-failure classifier during post-submit status observation; the
  503-to-Completed case recovers with one submission and exact readback,
  while a status 403 still denies without readback.
- **low, patch:** Successful cases initially observed Completed immediately.
  Add missing and Processing status observations before completion and
  assert a single submission with exact stored recording verification.
- **low, patch:** The initial deadline regression proved eventual completion
  but not the original remaining budget. Consume 800 ms before the failed
  response, assert completion below 1.5 seconds for a one-second budget,
  and verify cancellation of the submission's original deadline token.
- **medium, patch:** The shared helper also serves revoke/expire, so the
  original focused use-only tests did not cover its transition caller.
  Extend the mapped HTTP transition matrix with both lost-response actions,
  exact stored terminal fields, and corrupted audit-source denial.
- **low, patch:** Caller cancellation alone did not cover a transport timeout
  with an active caller. Add an independently cancelled submission Task;
  recovery still requires Completed and exact readback. Retain the separate
  caller-cancellation propagation regression.
- **low, patch (verification follow-up):** An elapsed-budget exit could
  dispose the deadline source before its timer callback fired, leaving the
  captured transport token uncancelled. Explicitly cancel on each of the
  helper's three budget-exhaustion exits; the deadline assertion no longer
  depends on callback scheduling.
