---
title: 'Close Story 3.6 privacy-assertion and verification-record patches'
type: 'bugfix'
created: '2026-10-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'aba488b18c4a4b96e08442538c41481c752c0382'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 has six accepted review patches. Its all-category privacy assertions reject harmless framework messages when a checkout path contains domain vocabulary, and its verification records omit or mislabel current evidence.

**Approach:** Keep raw-token, token-hash and protected-fixture-identifier checks on every captured log category; restrict generic domain-vocabulary checks to Timesheets logs, retaining the complete response-body checks. Close the five accompanying record patches: add the missing test double to the story inventory, refresh the test summary with dated spec-14 final evidence, distinguish pre-review focused counts from final totals, state the logger harness limits, and normalize the spec-14 deferred-work section. Preserve historical findings and the open live EventStore, durable-write and release gates. The story stays in-progress until workflow finalization.

</frozen-after-approval>

## Implementation Notes

- Planning found no unresolved intent choice or irreversible action. This is a small test-helper correction and five documentation patches; no production API, dependency or new callable type is needed.
- Reuse the three log assertion call sites and retain `CaptureFailureAsync`'s full response-body assertion in `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`. Keep all logger categories captured.
- Update `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `tests/3-6-test-summary.md` within that directory, `deferred-work.md`, `sprint-status.yaml`, and `docs/launch-readiness.md` with exact dated evidence and individual patch closures.
- Verify existing HTTP privacy tests with a scratch content root containing the formerly rejected words, then run the source-reference Debug restore/build and all six test projects individually. Performance lanes retain their default skips. Record Aspire resource state separately from in-process evidence.

- Implemented the three log call-site updates and separate protected-material checks. Every captured category remains visible; full response-body exclusions are retained. No production source was changed.
- Corrected the cumulative test inventory, current/prior summary labels, pre-review focused-count label, logger scope/exception/malformed-diagnostic caveat and the three spec-14 ledger paths/heading/spacing.
- The exact README source-reference Debug restore/build passed with zero warnings/errors. Individual direct lanes: Architecture 55/56, Contracts 90/90, Server 482/482, Projections 150/150, Integration 76 pass/42 fail/four skip, Works 76/76; 976 total, 929 pass, 43 fail, four skips. Logs: `/tmp/timesheets-story-3-6-build.log` and `/tmp/timesheets-story-3-6-<Project>.log`.
- Unexpected 401s occur before the changed privacy assertions. Read-only history investigation attributes all 42 integration failures to the prior `da172a8effe484be96c17aaef1c007e1ecb8f27c` EventStore pointer update, whose SDK adds workload fallback authorization and restricts anonymous metadata to health probes. The remaining architecture failure compares the imported Aspire 13.6.1 catalog with the existing dated 13.6.0 readiness verdict. These are unresolved compatibility gates; no fallback policy or SDK inventory was weakened.
- Before the rebuild, the prior IntegrationTests binary reproduced the content-root false positive. After rebuilding, the four affected privacy cases are blocked by the 401s. A retained reflection verifier passed 144 independent checks against the rebuilt private helpers, including all protected values in category/message/state, harmless framework vocabulary, Timesheets vocabulary and unchanged response-body checks. Exact command is recorded in the current test summary; this is not passing HTTP-boundary evidence.
- Explicit-AppHost Aspire start succeeded; `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18. Resource logs identify missing APP_API_TOKEN and JWT Authority/SigningKey configuration; stop succeeded. This differs from the historical port-conflict evidence. Release stays FAIL and the story is not complete.

- Independent Blind Hunter review returned six findings. All six received concrete corrections: current gate verdicts now reflect failing HTTP/full-suite verification, the prior authentication observation is dated, verifier source is retained under `tests/3-6-privacy-assertion-verifier/` in the implementation-artifact directory, cumulative inventory includes this spec and verifier, compatibility/package follow-ups are ledgered, and exact scratch-content-root invocations are recorded. The increment is complete; the story's acceptance and release remain incomplete.

- Post-review documentation checking briefly added an architecture failure when the historical zero-failure inventory wording was removed. Preserved that inventory explicitly as historical evidence and corrected the classification-table row separately from the release-gate row; the final ArchitectureTests rerun is recorded below.
- Final documentation rerun: ArchitectureTests 56 total / 55 pass / the one pre-existing package-verdict failure, confirming no added architecture failure. The retained verifier reran successfully with all 144 checks. Commit message validated with `npx --yes --package @commitlint/cli --package @commitlint/config-conventional commitlint --extends @commitlint/config-conventional --edit /tmp/timesheets-story-3-6-commit-message.txt` (exit 0).

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Current full-suite table still PASS | medium | patch | The current run has 43 failures. Changed the full-suite verdict to FAIL and the HTTP verdict to FAIL, qualified privacy evidence as CONCERNS, and preserved the October 5 passing run as history. |
| Authentication statement still current | medium | patch | The SDK now registers workload and sidecar schemes. Dated the prior no-scheme observation and named current authorization incompatibility. |
| Passing verifier exists only in temporary storage | medium | patch | Retained the verifier's Program.cs and project under the implementation-artifact tests directory, updated its command and reran the 144 checks without modifying authentication. |
| Spec missing from cumulative inventory | low | patch | Added spec-15 and the retained verifier's two source files to Evidence and tracking. |
| New blockers absent from deferred ledger | medium | patch | Added two source-linked follow-ups: supported capability-route/internal-fixture security integration and Story 5.2 catalog observation reconciliation. Their underlying defects predate this increment and remain deferred. |
| Focused scratch-root invocation omitted | low | patch | Recorded both exact direct-runner commands, the original one-case path false positive and the current four-case 401 blocker; clarified that the old binary is historical evidence. |
