---
title: 'Close shared-state preservation diagnostic evidence gaps'
type: 'feature'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The existing isolated runner cannot distinguish Redis server replacement from same-server content drift or classify persistent-key differences. The historical failed baseline and subsequent fingerprint timeout remain unresolved evidence.

**Approach:** Extend its existing read-only preservation observations with private, run-scoped keyed fingerprints and retain aggregate diagnostics for server identity, persistent additions/removals/updates, and TTL transitions. Preserve strict admission/preservation, foreign sessions/data, existing read scope and cost, historical failures and attribution limits. Do not assign writers from namespaces, sessions or command counters. Add focused regression coverage, independently review and verify, then attempt runtime qualification only through the existing runner/guards; admission refusal ends that attempt. Never delete keys, stop foreign services, change pins/gitlinks or weaken checks. Keep launch FAIL and Story 3.6 deployed acceptance open. Commit and push main after verification.

</frozen-after-approval>

## Implementation Notes

- Route facts: no intent gaps; no new irreversible data operation; the requested commit/push is authorized. Footprint is a small extension to internal Python fixture diagnostics, its guard caller/tests, and dated evidence/docs; no product API, C#, dependency, topology or shared-instruction changes.
- Starting revision: `ccf6fb1dc7469322e2c85f8fe1db4bfe5fcb28bb`; main initially clean. Baseline, Git instructions, root build/configuration, architecture boundary and historical investigation read. No runtime started before implementation because the user requires qualification after review/verification and terminal admission refusal.
- Code map: `scripts/run_isolated_magic_link_dapr.py`: `SharedSnapshot`, `read_only_shared_snapshot`, `shared_preservation`, `IsolatedFixture.capture_shared_baseline` and final preservation. `scripts/check_magic_link_apphost_guards.py` shares those reads/comparison. `scripts/local_runtime_admission.py` stays unchanged. Reuse atomic DB0/13/14 KEYS/DUMP/PTTL/TIME rows and existing etcd range; no expanded key reads, payloads, scans, sampling or commandstats attribution.
- Design: fixture-owned random comparison scope reused for admission and final observations; domain-separated keyed fingerprints of existing Redis digest rows, server identity and scheduler content stay in private attributes. New serialized snapshots/comparisons contain counts, booleans, timestamps and explicit limitations, without per-key material, secret, raw identity or content fingerprints. A bounded INFO server read on the existing connection supplies run identity. Missing/malformed evidence or mismatched comparison scopes refuse.
- Delta semantics: additions/removals count keys absent from the other complete key set; updates compare retained persistent values; persistent-to-expiring and expiring-to-persistent transitions have separate counts. These are observed snapshot differences, not a mutation timeline. A transition cannot qualify as natural expiration; only complete absence after the captured deadline qualifies. Every unexplained change or Redis identity mismatch fails preservation.
- Tests: extend `scripts/test_isolated_magic_link_dapr.py` for identical/replaced/missing Redis identity, offsetting additions/removals and same-count updates, both TTL transitions including elapsed deadlines, natural expiry, private serialization, run scope separation, unchanged read budget/timeout and live/guard call-site propagation. Preserve existing isolation/admission/cleanup regression cases.
- Verification: baseline and final focused fixture module, independent review/verification, exact command receipts and privacy/protected-file checks. No C# changes require another solution build/six-suite run; the security-smoke runner itself rebuilds only after idle admission. One supported `--security-smoke` attempt follows offline verification, with no alternate service audit/retry after refusal. Retain aggregate refusal/cleanup diagnostics and historical evidence links; do not archive foreign inventory rows.
- Completion documentation: append dated diagnostics/qualification outcome to `tests/shared-state-preservation-investigation.md`, link new aggregate evidence and update `docs/launch-readiness.md` without changing FAIL or closing deployed acceptance. Historical archives and prior timeout stay untouched.
- Independent blind review requested complete DB0/13/14/count validation, explicit comparison scope before I/O, observation times and safe failure-stage receipts. Implemented these within the existing read path, including cancellation receipts and no retries. Generator-based counting avoids temporary sets proportional to persistent-key coverage. An independent bounded synthetic retention measurement is planned; it cannot qualify live Redis read cost or explain the earlier timeout.
- Final code verification: 203/203 fixture tests in 47.517s; independent 17 focused tests in 1.172s and 1,049 oracle cases pass. Redis Lua, DB0/13/14, scheduler payload and 20-second timeouts match the starting revision. Two synthetic 829,212-key maps/comparator take 6.190s and 402.3 MiB process peak RSS under a 1 GiB cap, excluding full-reader/server/wire costs. The anomalous original resource counter is retained with its limitation.
- The single guarded security-smoke attempt ran 19:57:43.373139–19:58:04.006099 UTC and exited 1 after 20.634s. Inventory/port admission passed, then the first DB0 shared baseline observation timed out. The new safe phase/category/timing receipt identifies this attempt's failed phase only. No complete pair, preservation result, build, resource or AppHost start exists; cleanup completed and no retry/alternate audit followed. Current runtime qualification and deployed acceptance remain open, launch FAIL. Earlier failed/refused outcomes, timeout and attribution limits remain unchanged.
- Dated command receipts, independent verifier/benchmark sources, safe runtime originals and scope limits are retained in `evidence/shared-state-diagnostics-20261028/`. Updated the existing investigation, local-runner documentation and launch assessment. No C#, pin/gitlink, topology, foreign session or domain data mutation.

## Review Triage Log

- BH1 — medium, patched: equally incomplete Redis coverage could pass the comparator seam. Require exactly DB0/13/14; successful mocks now model complete coverage and missing coverage refuses.
- BH2 — medium, patched: expiring/private coverage and total counts were not reconciled. All persistent/expiring/total counts now match private maps, with focused omission/inconsistency refusals.
- BH3 — medium, patched: persistent-map retention needed representative local-cost evidence. Removed full-key temporary sets and measured bounded synthetic historical-scale retention/comparison independently. Full-reader/Redis/wire costs remain explicitly unqualified; no extra shared observation was run for this measurement.
- BH4 — medium, patched: failed observations lacked a safe phase-specific receipt. Capture phase/database/category/timing/completion on failure/cancellation without response/error text, repeated reads or changing original failure status. The actual guarded attempt exercised DB0 timeout evidence; the earlier timeout remains unexplained.
- BH5 — low, patched: retained aggregate expiry verdict lacked capture timing. Include per-database Redis capture times and observation intervals in new evidence.
- BH6 — medium, patched: argument-free reads could consume observations in distinct scopes. Require an explicit validated scope before I/O and document historical helper source/schema dependence while preserving archived bytes.
- Independent code recheck found no unresolved code defects. Resource-cost verification is retained with explicit full-reader limits; final artifact verification follows the completed attempt without service access.

- Final independent artifact validation passes: safe runtime originals/log hashes, tested/runtime/current source identity, archives, protected historical material/pins/gitlinks and 18 relevant documentation links. See [receipt](evidence/shared-state-diagnostics-20261028/final-artifact-validation.json). No remaining review findings or new review deferrals. Completion applies to diagnostics and the authorized attempt, with runtime qualification and deployed acceptance still open.
