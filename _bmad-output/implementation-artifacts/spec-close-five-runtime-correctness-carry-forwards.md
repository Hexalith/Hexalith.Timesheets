---
title: 'Close five historical runtime correctness carry-forwards'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five historical deferred requests already satisfied by delivered code: projection/loader correctness, regression-detecting projection and HTTP fixtures, sanitized non-Fresh catalogs, checkpoint tenant isolation, and committed EventStore AppPort/metadata support. Add evidence-backed dispositions to their original ledger entries so the backlog reflects the existing implementation. Preserve original evidence and keep runtime and deployed acceptance separate. Verify, independently review, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Inspection baseline: clean main at `7533cbd95557d0d02943a619a115fc85552d25c4`. Read root AGENTS.md, the declared AI.Tools LLM/state/Git baseline, editor/build/package/test configuration, architecture projection/privacy constraints, boundary record, earlier implementation specs and ledger resolutions. No additional instruction files exist under the owned source/test paths.
- One bounded documentation reconciliation; no unresolved intent gap, runtime edit, migration, new callable surface or dependency change. Commit/push is explicitly authorized. Use oneshot and keep this spec in-progress until review/finalization.
- Selected summaries: “Harden projection and loader correctness across the Story 3.6 review patch items that are not part of live-host catalog freshness”; “Strengthen the Story 3.6 test suite where assertions cannot currently catch the regressions they imply”; “Preserve explicit non-Fresh catalog state only when the no-disclosure bundle can represent it safely”; “Enforce checkpoint tenant scope consistently in Timesheets read-model projections”; “Commit and pin the EventStore-owned AppPort and metadata changes in a later authorized repository update”.
- Code map: ReadModelRebuildConcurrency, ProjectionEventReader, the index/catalog handlers, runtime gateway registration and the concrete magic-link loader carry the delivered correctness guards. MagicLinkStateProjectionHandlerTests (including Operability) asserts plan StoreName/Kind/Concurrency and strict per-component/key ETags; MagicLinkConfirmationHttpBoundaryTests exercises real cross-tenant candidates. The loader sanitizes validated non-Fresh catalog status. TimesheetsProjectionCheckpoint.ValidateTenant protects projection folds. The committed EventStore gitlink is `5ce5f1d84b88c808cc8a526b9decf149de977e55`; inspect that object for AppPort and metadata support, without changing the checkout.
- The broad correctness entry includes a helper-retention decision rather than removal: the supported pure Rebuild composition helper is documented and its narrower follow-up is already resolved by spec-close-five-integration-handoffs.md. Logging.Abstractions is actively used. Preserve open shape-layering, configurable gateway options, automatic delivery, migration and deployed/runtime qualification work.
- Planned changes: five additive resolution_note lines in deferred-work.md, this spec, and tests/five-runtime-correctness-carry-forwards.md with exact sources, limitations and reproducible commands. Do not regenerate entry points, modify historical specs/receipts, bump gitlinks or move story/release status.
- Verify the matching Debug/source graph with warnings-as-errors, then run the existing focused projection/loader/HTTP tests and architecture suite individually. Confirm exactly five notes, preservation of all original ledger bytes, links and file scope. No new tests are needed for a ledger-only reconciliation. Complete the configured independent Blind Hunter review, triage every finding, validate the Conventional Commit, fetch/inspect main divergence and push without force.

- Implemented exactly five additive ledger notes and the evidence report. A read-only byte-preservation check removes those five lines and reproduces the original ledger exactly; all three entry points match baseline, and report links resolve. The broad eight-goal re-verification entry remains separate because obsolete domain-wide loader test setup survives. No runtime source/test/configuration or gitlink changed.
- Matching Debug/source solution restore and warnings-as-errors build passed (SDK 10.0.402; zero warnings/errors). Existing selections passed: Architecture 109/109, loader/runtime registration 125/125, handler/early checkpoint 201/201, HTTP 3/3, checkpoint mismatch 36/36. All exits zero, with no failures/errors/skips/not-run. This is focused verification, not a full-suite or runtime/deployed acceptance claim. Independent review remains pending.

- Independent Blind Hunter reviewed 17.441 kB (finding floor five) and returned five evidence improvements. All were verified and patched in the report. No runtime defect was introduced by this documentation change, no new work is deferred, and only the configured review layer ran.

## Review Triage Log

- **low / patched:** “full duplicate envelopes” overstated the loader comparison. The report now names historical compared fields and excludes comprehensive current-schema parity; NativeEventDigest, GlobalPosition and PayloadVersion are not part of its equivalence check. Runtime code remains unchanged.
- **low / patched:** focused checkpoint commands omitted existing incremental-prefix, selected-issuance and absent-issuance controls. Ran all four existing cases successfully and added their command, result and summary. The earlier broader matching-scope evidence remains explicitly sourced to the original implementation spec.
- **low / patched:** committed-source reproduction omitted the options and sidecar adapter objects. Added exact git-show commands for both, confirming AppPort passes through to DaprSidecarOptions without inferring running-topology acceptance.
- **low / patched:** temporary logs alone would not remain auditable after the workspace disappears. Embedded sanitized portable source identities, exit codes and verbatim runner summaries in the report alongside the exact commands; no extra artifact or sensitive output was added.
- **low / patched:** byte preservation alone did not prove note placement. The reproduction check now requires exactly one note immediately after each of the five intended unique summaries, rejects unrelated/duplicate placements, and still reconstructs every original ledger byte.

- Post-review checkpoint controls passed 4/4 (0.170 s), bringing this focused verification to 478 distinct passing cases, zero failures/errors/skips/not-run. The source build and previous test results remain applicable because review patches affect documentation only. Final preservation, source-object, link, scope and commitlint checks precede commit/push.

- Final post-review checks passed: the exact documented preservation and committed-source commands, all report links, unchanged agent-entry-point bytes and exactly the three planned documentation files. The five intended unique summaries each have one adjacent resolution note and all original ledger bytes remain intact. Conventional Commit validation and git diff --check passed; a fresh fetch found zero divergence from origin/main. All five review findings are addressed, nothing new is deferred, and this reconciliation is finalized for the explicitly requested commit/push.
