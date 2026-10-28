---
title: 'Investigate the failed shared-state preservation baseline'
type: 'chore'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The retained security-smoke baseline passed its live journey but failed preservation when shared Redis DB0 increased from 829212 to 829223 persistent keys. Its cause and ownership are unclassified.

**Approach:** Trace retained evidence and existing isolation/preservation logic, observe shared services read-only without retaining keys, values or foreign session details, and fix only demonstrated defects. Use the existing isolated runner and admission guards; an admission refusal ends that runtime attempt. Preserve foreign sessions/data, historical failures and unresolved limits. Never delete keys, stop foreign services, weaken checks, or change pins/gitlinks. Keep launch FAIL and deployed acceptance open. Independently review and verify changes, then commit and push main as requested.

</frozen-after-approval>

## Implementation Notes

- Route decision: no intent gaps or new irreversible operation is needed; the requested commit/push is authorized. Investigation found no demonstrated code defect. The small change is evidence and documentation only, with no public API or runtime behavior change.
- Code map: `scripts/run_isolated_magic_link_dapr.py` owns `SharedSnapshot`, `shared_preservation`, `read_only_shared_snapshot`, private infrastructure, exact component/runtime validation and final preservation. `scripts/local_runtime_admission.py` owns the advisory lock. Reuse them unchanged. `scripts/test_isolated_magic_link_dapr.py` exercises isolation, strict preservation, admission refusal and owned cleanup without real infrastructure.
- Historical input: `tests/remaining-repair-and-guidance-deferrals.md` and its `evidence/remaining-repair-and-guidance-20261028/baseline-runtime/retention-map.json`. All 106 archives validate. Persistent rows and Redis server identity were not retained; historical writer attribution cannot be reconstructed. Report eleven net additional keys, not exactly eleven additions.
- Scope: add a dated investigation report and aggregate evidence/command sources, link it from the existing report and launch assessment. Preserve the old failed outcome verbatim. No production/runner source, build configuration, dependency checkout, gitlink, shared instruction or sprint-status change is planned.
- Before editing, the read-only full fingerprint audit timed out with one foreign AppHost/eight Dapr sidecars observed. Its wrapper exited zero but explicitly recorded `completed: false`; this is failed evidence collection. The supported security-smoke runner then refused admission with exit 1 before resource creation. No runtime retry follows.
- Subsequent metadata-only observation is not runtime qualification or a full preservation comparison: INFO counters increased while this fixture was stopped. It cannot retrospectively attribute the eleven-key discrepancy. Retain both failed collection and terminal refusal.
- Verification: independently check artifact integrity, exact commands, source and dependency preservation, privacy and document links/claims; run the existing focused fixture tests. No C# or guard code changes require a new solution build or six-suite rerun. Review the final documentation independently before Conventional Commit validation and non-force publication.
- The existing fixture module passes 193/193 in 48.827 seconds. Independent verification confirms all 318 historical hash/length checks, byte-identical analysis reproduction, current temporary-original/source identities and refusal summaries. All thirteen dependency heads/statuses, ten protected file hashes and root gitlinks match the [preservation receipt](evidence/shared-state-preservation-20261028/protected-preservation.json).
- Independent documentation review returned seven findings, triaged below. Corrections touch only the new evidence packet and completion records. Offline verification reproduces the historical output in normal, `-O` and `-OO` modes; twelve intentionally corrupt integrity/source cases refuse across those modes. Live audit scripts were not rerun. Exact commands, source hashes and results are in [checks](evidence/shared-state-preservation-20261028/checks.json).
- Final independent recheck confirms all seven findings resolved, three offline reproductions, twelve negative refusals, exact sources/receipts, protected hashes and resolved links. No new review deferrals remain. The historical attribution and acceptance limits above remain open; completion applies only to this investigation and evidence packet.

## Review Triage Log

- BH1 — medium, patch: root `*.log` ignores the original test receipt. Retained its exact bytes as `fixture-tests.log.gz`, verified both compressed/original SHA256 and updated the result reference. No test result is lost.
- BH2 — low, patch: review/completion records were pending while the report referred to them. This log and the linked preservation receipt supply the results; final status follows independent recheck.
- BH3 — low, patch: no exact start/end timestamps were captured for the failed audit/refused attempt. Retained available original file-write timestamps with explicit limitations in `timeline.json`; documented that sequence is session evidence, not independent execution timing proof. Neither operation was rerun and no timestamp was invented.
- BH4 — medium, patch: summary-only hashes did not retain the safe admission/cleanup originals. Copied both originals byte for byte and verified their hashes. Foreign inventory rows remain excluded; the aggregate count/argv transformation was independently checked against temporary originals and that privacy limit remains explicit.
- BH5 — medium, patch: the new offline historical verifier used assertions removed by Python optimization. Replaced them with explicit refusal checks. Normal and optimized outputs reproduce identically; deliberately wrong archive hash, original hash, byte length and source snapshots all refuse at optimization levels zero, one and two. Exact original helper bytes remain losslessly archived.
- BH6 — medium, patch: repository HEAD alone did not bind temporary helpers to executed bytes. Retained and verified SHA256 for all three original execution sources, including the archived initial historical helper; final offline helper revisions have separate execution-source receipts.
- BH7 — low, patch: historical source equality was copied from the summary. The final offline analysis compares the original decompressed source snapshots directly and checks agreement with the summary. A changed-snapshot negative case refuses under every optimization mode.
