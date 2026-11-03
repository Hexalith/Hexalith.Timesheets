---
title: 'Close five deferred scheduler and secret-scan findings'
type: 'bugfix'
created: '2026-11-03'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized five-finding maintenance batch">

## Intent

**Problem:** Two deferred scheduler-observation defects allow malformed HTTP
responses to escape safe failure receipts and allow preservation reads to use
proxies or redirects. A third deferred entry retains three EventStore fixture
secret-scan findings whose current source appears repaired but is unverified.

**Approach:** Fix the two scheduler defects and verify each of the three cited
secret findings through the current-source upstream scanner. This is five
findings across three ledger entries. Keep all deployed and native-runtime
qualification limits and launch FAIL. Commit and push the verified changes to
main as requested; preserve dependency source and gitlinks.

</frozen-after-approval>

## Implementation Notes

- Baseline: `34a521e9daa263612663ca5b70628d2f106d3b9a` on clean `main`.
- Investigation: `scripts/run_isolated_magic_link_dapr.py` catches ordinary
  transport/parse errors but omits `http.client.HTTPException`; its scheduler
  range uses the default `urlopen`. Reuse `owned_http_open`, which disables
  proxies and redirects, and keep the existing 20-second timeout.
- Extend `scripts/test_isolated_magic_link_dapr.py` with failures during open
  and body read, sanitized receipt checks, exact endpoint/body assertions, and
  owned loopback tests proving no proxy/redirect destination is contacted.
  Redis remains a transport double; no shared service is read.
- Independently rebuild and run the EventStore reusable-content scanner against
  current dependency source. Record canonical source identity and explicit
  checks of CatalogBackendHttpTests, CatalogRefusalDiagnosticsTests, and
  CatalogTransportIsolationTests. Do not change the scanner or exclusions.
- Record exact validation in a new verification report and append resolutions
  to the three grouped ledger entries without rewriting historical evidence.
  No domain code, AppHost, topology, dependency pin, or deployment changes.
- Intent gaps: none under the stated five-finding interpretation. Irreversibles:
  only the explicitly requested commit/push. Footprint: two existing Python
  files, this spec, ledger, verification report, and compact evidence receipts.
- The user explicitly selected the five-findings-across-three-entries scope
  during implementation. The upstream scanner passes 1/1 after a current-source
  Debug rebuild with zero warnings/errors; all three cited fixtures are safe.
- New regressions first demonstrate the unsanitized exception and default-opener
  defects. The protected transport exposed unclosed HTTPError response handles;
  the scheduler now closes them before its existing safe refusal. Existing
  scheduler mocks now intercept the protected transport. A context-manager mock
  mismatch and five remaining old mocks caused an intermediate full-suite
  failure; corrected before the final validation.
- Initial complete offline fixture suite passes 237/237 in 69.256 test seconds; scanner
  manifest, canonical source identity, all three fixture hashes and exact XML
  test result were independently checked. Ledger resolutions close three entries
  covering the agreed five findings. Launch FAIL and all runtime gates remain.
- Blind review returned nine findings. Added real malformed-status/truncated-body
  HTTP checks, exact receipt keys and response-context cleanup assertions, then
  reran the affected suite. The final receipt also identifies CPython/version.
  The initial passing suite receipt remains under `initial-fixture-suite.*`.
- The post-review full rerun hits external canonical-admission contention:
  command exit 1, 237 tests with 110 failure and 17 error records. Separate probes
  still refuse; no holder is changed. Final focused scheduler checks pass 3/3
  against the final source hashes. This environmental broad-gate blocker is
  retained separately from the earlier full pass and current focused/scanner
  evidence; no post-review full-suite or runtime pass is claimed.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| R1: deeply nested JSON escapes safe observation metadata | medium; independently reproduced bare RecursionError on both the baseline runner and current source with transport doubles | Defer the pre-existing parser failure; ledger entry and safe synthetic receipt added. |
| R2: scheduler JSON has no body-size bound | maybe-false, medium if supported inventories exhaust memory; json.load reads the whole response, but unacceptable deployed memory use is unmeasured | Defer resource-budget qualification with R3 as one bounded-body observation increment. |
| R3: socket timeout is not a total observation deadline | maybe-false, medium if trickled responses prevent completion; timeout=20 has no aggregate deadline, but no problematic supported service was demonstrated | Defer with R2; preserve current semantics and require owned byte/time-budget evidence. |
| R4: injected protocol exceptions lack an actual HTTP-parser check | low; mocks alone do not exercise the standard-library parser | Patch with owned-server malformed-status and truncated-body responses through the real protected opener. |
| R5: body-failure cases omit response-context cleanup assertions | low; read assertions alone would miss an exit regression | Patch exact exit call and original-error identity checks. |
| R6: unchanged receipt-shape claim lacks exact field-set check | low; selected values do not forbid extra fields | Patch an exact safe-key whitelist assertion. |
| R7: command receipt omits Python implementation/version | low; urllib/JSON behavior depends on interpreter | Patch the final rerun receipt with executable, implementation and complete version. |
| R8: old red output lacks command and intermediate-source identities | low; the historical intermediate bytes were not captured and must not be invented | Patch the report to label red output diagnostic-only; final green source hashes remain authoritative. |
| R9: fixture checks record booleans/hashes without placeholder literals | low; direct review otherwise requires recovering the dependency source | Patch an additive literal-check receipt without changing the original scanner packet. |
