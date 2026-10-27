---
title: 'Retain local dashboard ownership failure evidence'
type: 'bugfix'
created: '2026-10-27'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Dashboard ownership refusals discard the partial socket observation needed to diagnose local sharing or transfer.

**Approach:** Retain allowlisted expected PID/start, observed listener inodes and holders, failure stage and visibility limits in initial and post-journey failure evidence. Preserve opaque refusals and fail-closed behavior. Extend controlled real-process sharing/transfer and incomplete-visibility regressions, verify evidence survives failure and owned cleanup, and record results and limits in the existing report and ledger. Keep inaccessible holders and unobserved native transfers open. Preserve unrelated changes, gitlinks, launch FAIL and every deployed gate. The user explicitly authorizes independent review, Conventional Commit validation and push to main for this narrow scope.

</frozen-after-approval>

## Implementation Notes

- Investigation: `dashboard_listener_identity` and `verify_dashboard_ready` in `scripts/run_isolated_magic_link_dapr.py` retain only successful observations. Reuse optional diagnostic dictionaries as in the credential handoff; retain fixed stages and numeric identity/socket data only. Fixture initial verification and post-journey capture must write failure receipts before cleanup.
- Reuse `real_listener_handoff_processes`, descriptor visibility injection and the execute/finally harness in `scripts/test_isolated_magic_link_dapr.py`. Check before/after-health refusal, serialization privacy, owned child exits and fixture cleanup. Synthetic Python socket lifecycles remain distinct from native Aspire observations.
- No intent gaps or implementation irreversibles; footprint is two Python files, this workflow spec, the existing evidence report/ledger and narrow receipts. Requested publication follows verification and independent review.
- Pre-change command `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --public-port 18080 --log-directory /tmp/timesheets-dashboard-diagnostic-baseline` exited 1: `An AppHost is already running; no AppHost started`. The foreign session is preserved. No new native observation is established by this refusal.
- Implementation: optional diagnostic dictionaries retain partial socket tables, descriptor counts, holder PID/start samples and fixed failure stages; initial and post-journey failure receipts are written before owned cleanup. The expected process's kernel state is allowlisted. A single diagnostic inventory after failed health cannot turn a refusal into success. Post-journey discovery refusal retains the prior verified expected identity, without claiming a fresh observation.
- Verification: the first pre-test-extension suite run had three assertions caused by returning a sanitized null instead of the synthetic `current` start marker. Restoring the successful observation's existing identity shape corrected those assertions; native expected PID/start remains numeric. A later 189-test full run overlapped independent review commands and had one canonical-admission contention error in an unchanged cleanup test. Both earlier/final receipts are distinguished from serialized verification.
- Final verification: serialized full runner suite passes 193 tests without skips/errors/failures in 46.984 seconds. The six controlled execute scenarios retain identical receipt bytes before/after owned cleanup and reap all owned synthetic processes; their allowlisted receipts are retained separately. Native baseline remains refused before startup because another AppHost was running. No native transfer claim or deployed-gate closure follows.
- Independent post-fix recheck confirms B1–B5 corrections and four focused tests pass, with no material remaining issues. Report/ledger retain the inaccessible-holder and unobserved-native-transfer obligations. Conventional Commit message validation passes before commit; publication is explicitly authorized by the invocation.

## Review Triage Log

- Blind B1 — **medium, patch**: real malformed HTTP status produces `http.client.BadStatusLine`, outside the original normalization tuple. Added `HTTPException` normalization and real malformed/unsupported-protocol regressions; errors remain opaque and receipts contain no response line.
- Blind B2 — **medium, patch**: an HTTP/transport failure after sharing/transfer previously retained only the earlier successful inventory. One additional diagnostic-only inventory now retains conflicting holders under `after_failed_health`; the original health refusal remains authoritative. Controlled sharing/transfer plus HTTP and transport failures cover this path.
- Blind B3 — **medium, patch**: failed post-journey resource discovery/host validation previously left `ownership_failure` null. Retain prior expected numeric PID/start and explicit discovery stage with no fresh observation; regressions cover both discovery gates.
- Blind B4 — **low, patch**: a mid-inventory failure retained holder PIDs but lost readable starts. Sample each relevant holder as it is encountered; partial-inventory regression verifies the retained start and unknown/inaccessible identity cases remain explicit.
- Blind B5 — **low, patch**: matching PID/start did not explain zombie refusal. Retain only allowlisted kernel process states in expected-process observations; zombie regression verifies the fixed stage and state without broadening acceptance.
- Blind B6 — **false, reject**: suggested recording the environment-derived loopback endpoint conflicts with the user's explicit exclusion of environment values from failure evidence. Expected numeric identity, listener inodes, visible holders and fixed failure stage provide the requested ownership diagnostics. Endpoints remain omitted deliberately.
- Supplemental independent safety review — no concrete findings. Serialized dashboard selection passed 20 tests; the new execute regression passed six subcases. Its initial concurrent failures were canonical-admission contention, then resolved by serialized checks.
