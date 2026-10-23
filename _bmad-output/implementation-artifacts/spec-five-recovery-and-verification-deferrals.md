---
title: 'Close five recovery and verification deferrals'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: '357db9d8c2df951a60c8ffb59c0d61c6a7bb3c8c'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open ledger items leave obsolete capability fragments, incomplete local diagnostic admission, interrupted aborts, unbounded read-model awaits, and inconsistent test names.

**Approach:** Retire replaced fragments through SDK batches; conservatively validate supported diagnostic snapshots; authorize abort continuation through an immutable witness; share the loader deadline across reads; rename attributed tests to PascalCase. Complete these five items as the requested batch, then commit and push to main.

## Boundaries & Constraints

**Always:** Preserve opaque failures, caller cancellation, fail-closed defaults, event identities, selected-generation integrity, journal contents, and historical evidence. Use EventStore read-model protocols; close ledger items against passing evidence.

**Never:** Edit references, dependencies, topology, or managed guidance; run live migrations; delete corrupt selected metadata; discard deliveries; or claim deployed acceptance. Witness-free aborts remain refused. Diagnostics provide neither full SDK parity nor ETag admission. Launch remains FAIL.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| View replacement | Valid fragmented prior view | Publish replacement before deleting exact obsolete fragments | Prepared SDK operations resume after interruption |
| Diagnostic admission | Supported Open/drained snapshot | Admit only internally consistent typed fields and scope | Malformed or unsupported state gets fixed privacy-safe refusal |
| Abort recovery | Aborting plus matching witness and current authority | Resume SDK abort without promoting the stage | Foreign/missing bindings refuse without mutation |
| Loader dependency stall | Index, tree, coordinator, or catalog read ignores cancellation | Return unavailable within invocation deadline | Caller cancellation still propagates |
| Test rename | Attributed method with underscores | Preserve discovered cases and assertions | Refuse identifier collisions |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkCapabilityProjectionStorage.cs` and new sibling continuation/transition records: preserve the existing constructor and omit optional continuation metadata from flat rows. A continuation binds a validated flat base, bounded token-free transition fragments and reconstructed-view digest/length. Only one layer is supported.
- `/tmp/five-recovery-before-capacity-redesign/`: preserved pre-redesign handler and regression source for KEEP reference; the faulty retirement source and its added tests were reverted before re-derivation. All unrelated approved changes remain in the working tree.
- `/tmp/five-recovery-naming-tool/`, `/tmp/five-recovery-inventory-*.json`, and the compressed rename manifest: original runnable audit material to retain with exact repeatable invocations.

- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs`: FoldCatchUpAsync restores prior storage; WriteView writes fragments then pointer.
- `scripts/magic_link_index_selection.py`, `scripts/test_magic_link_index_selection.py`: _admit_epoch and ready_epoch; SDK typed validators are internal/private.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`: Begin proves inventory; status omits fingerprint. Reuse V1 witness pattern through ReadModelWritePolicy.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`, sibling `MagicLinkStateLoadingBudget.cs` and `MagicLinkStateLoadingOptions.cs`: history budget bounds streams; candidate/catalog awaits remain outside it.
- `tests/`: initial naming scan finds 786 method candidates across 83 files, including possible helpers; inventory attributed methods before editing.

## Tasks & Acceptance

**Execution:**

- [x] `MagicLinkConfirmationCapabilityProjectionHandler.cs` and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionHandlerTests.cs` — preserve ordered exact retirement when the fold fits 128 operations. Otherwise publish a bounded continuation retaining the validated flat base as reachable storage and storing only the normalized token-free issuance row or terminal fact in immutable transition fragments. Restore through existing Merge/Publish or Apply rules, verify complete-view digest/length and scope, refuse nested/ambiguous continuations, and retain pointer/ETag rechecks. Prove transition fragments plus pointer fit 128 operations for supported views. Do not persist issuance events or TokenHash. Cover all terminal kinds, terminal-first issuance, high-entropy/maximal inputs, legacy fragments, malformed/foreign/nested bases, interrupted publication, redelivery and crossing readers. Rebuild staging keeps flat representation; compatible readers must precede continuation writers at deployment.
- [x] `scripts/magic_link_index_selection.py` and its test file — validate required containers, typed scalars, anchor, writers, checkpoints, retained names, and counters; refuse unsupported staging/chunks. Test four ledger corruptions and healthy SDK-shaped snapshots; reject malformed inline captured-digest lengths/counts; document parity limitations.
- [x] `MagicLinkTokenHashCapabilityIndexV6Migration.cs`, new sibling `MagicLinkV6MigrationAbortWitness.cs`, and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV6MigrationTests.cs` — persist scope/operation/inventory/epoch/source-generation witness after Begin and before abort handling. Add RecoverAbortAsync checking existing witness and authority. Test cold recovery, foreign/missing/corrupt bindings, witness-write faults, repeated recovery, and promotion refusal. Independently assert migration-only cleanup before explicit recovery; cover a healthy pending journal and interruption during stage deletion. Clarify SDK-owned status reconciliation separately from Timesheets fence/source writes. Initialization failure leaves Building retryable; Aborting never creates witnesses.
- [x] Loader/budget/options files above and `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` — bound candidate/catalog awaits with remaining invocation deadline without charging history pages; bound standalone catalog calls. Test stalled index/coordinator/tree/catalog reads, late completion, cancellation, and healthy resolution. Assert elapsed time with a scheduling margin; release stalled dependencies in finally, await their completion and cover a late fault.
- [x] `tests/**/*.cs` and active references — rename attributed underscore methods to PascalCase; preserve attributes/data/assertions, detect collisions, and update filters.
- [x] `README.md`, `_bmad-output/implementation-artifacts/deferred-work.md`, and artifact-relative `tests/five-recovery-and-verification-deferrals.md` — record closures, commands/results, remaining limitations, actual source dependency revisions/dirty state, and runnable Roslyn/discovery audit sources with exact commands. Retain earlier receipts as pre-review history; qualify final sources after the capacity fix.

**Acceptance Criteria:**

- Given all five tasks, when focused and full gates pass, then each ledger closure names its evidence and remaining deployment obligations.
- Given renamed tests, when suites run, then equivalent cases execute without attributed underscore names or collisions.

## Implementation Notes

- Pre-review history (superseded for final-source qualification): all six executable suites passed: 2,543 passed, zero failed, five expected skips. Python gates pass 129 + 88 checks; all 35 compiled configuration probes pass.
- Pre-review parent audit verified source/receipt hashes, case totals and executed matrix coverage against that earlier diff. All 786 renames preserve source semantics and discovery; two audit regressions bring final discovery to 2,548 cases.
- Pre-review parent audit corrected migration-only success after abort cleanup, missing required diagnostic arrays, uncertain persisted-witness retry/ETag behavior, and stale status across witness reads. Regression receipts cover each correction. The original candidate-core signature remains for existing reflection fixtures.
- Witness-free aborts, corrupt-source repair, SDK atomic binding, full diagnostic parity/ETag admission, runtime qualification and deployed acceptance retain the documented limits.
- Review iteration 1 implements bounded one-layer continuations while retaining ordinary exact retirement and flat rebuilds. Complete-size or lifecycle mismatches preserve the accepted journal head. Focused corrections pass 17/17, loader 145/145, Python 130 + 88, and compiled guards 35/35. Final discovery preserves all 2,546 original cases and adds 16 regressions (2,562 total). Runnable audit sources and exact dependency identities are retained with review receipts; all six corrected-source gates pass (2,557 passed, zero failed, five expected skips). Final source/receipt hashes are in review-iteration-1/verification.json.
- Final review corrections are test/audit/documentation only; all 12 runtime/diagnostic source hashes still match the full gates. Eight supplementary cases bring current discovery to 2,570 (2,546 equivalent originals plus 24 additions, no removed/altered cases). The parent independently passes the 18-case selection; implementation receipts also pass 19 projection and one server cases. The strict 786-method audit allows only four exact documented active-reference text updates and otherwise compares tokens/trivia exactly. Supplementary receipts and hashes are in final-review-patches/verification.json. All direct review corrections are complete; no new work was deferred. Deployment requires the documented complete-stop upgrade.

## Spec Change Log

- 2026-10-23, iteration 1: E1 exposed the SDK's 128-operation live-fold ceiling. The non-frozen fragment task now requires a bounded base-plus-transition fallback when replacement plus deletion cannot fit. This avoids blocking a valid journal head. KEEP the four other implementation lanes, 786 source-preserving renames, existing flat/legacy storage, staged rebuild behavior, ordered ordinary retirement, opaque failures, scoped validation, source/ETag preservation and reader rechecks. No SDK or dependency change is authorized.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| E1 — retirement exceeds fold capacity | high | bad_spec | SDK CatchUpAsync rejects more than 128 logical operations. A valid prior 47-fragment view and replacement 94-fragment view need 142 operations with deletes; retry cannot finish. The plan omitted this bound. |
| B1 — captured digest lengths | medium | patch | Parent reproduced admission of 0, 1 and 64 decoded bytes with owner sequence 1. SDK ValidateCheckpoint rejects empty, non-32-byte-aligned or excessive digest counts; add the demonstrated checks. |
| B2 — SDK status maintenance | low | patch | GetStatusAsync reconciles SDK control indexes before returning. Authority is checked first, and baseline migration already uses this SDK behavior. Clarify that witness refusal makes no Timesheets source/fence writes; SDK-owned discovery reconciliation is not a read-only operation. |
| B3 — masked migration retry cleanup | medium | patch | The test calls explicit recovery before asserting the result of migration retry, masking an omitted recovery call. Check persisted state before that second call. Same root cause as V1. |
| B4 — healthy pending journal recovery | medium | patch | New recovery fixtures have empty journals. Add a healthy-source pending-delivery case and assert selected contribution and drained journal. |
| B5 — interrupted stage deletion | medium | patch | Fault injection currently stops at the Aborting transition. Add a fault during deletion and prove restarted recovery removes private stage values and metadata. |
| B6 — loose deadline test | low | patch | A 2-second observation window can miss a 1-second regression against a 150-ms budget. Measure elapsed time with an explicit scheduling margin. |
| B7 — abandoned dependency completion | low | patch | The stall fixture releases without awaiting dependency completion and lacks finally cleanup. Observe completion, release on assertion failure, and cover a late dependency fault. |
| B8 — manually assembled healthy snapshot | maybe-false | reject | No current SDK incompatibility is demonstrated; the fixture deliberately represents the documented supported subset. An actual SDK-produced snapshot failing admission would settle the concern. Potential harm is low; avoid introducing a generator solely for this speculative case. |
| B9 — source dependency identities | medium | patch | Source-mode receipts omit dependency HEAD/dirty identities. Record canonical gitlinks, actual HEADs and dirty state for root-declared source checkouts. |
| B10 — rename audit reproducibility | low | patch | The Roslyn tools and comparison commands exist only under /tmp. Retain runnable audit sources and exact invocations with the evidence. |
| V1 — independent retry cleanup assertion | medium | patch | Pre-verified mutation can remove MigrateAsync's recovery call while the explicit call repairs state before assertions. Add an independent persisted-state assertion; group with B3. |
| Final B1 — reuse of an aborted operation ID | false | reject | The demonstrated second migration reuses an operation ID after its reservation has ended, violating the documented unique-ID maintenance contract. Recovery itself remains idempotent and cannot promote; the replacement-epoch regression explicitly identifies ID reuse as a contract violation. No supported-call promotion failure is shown. |
| Final B2 — continuation rollout procedure | low | patch | Readers and writers ship together without a writer switch. Make the existing reader-before-writer requirement executable by documenting a complete-stop upgrade: pause delivery, stop all old consumers, replace every reader/writer, then resume. No mixed-version rollout is supported. |
| Final B3 — duplicate fallback serialization | low | reject | WriteLiveView serializes the bounded complete view and then WriteView repeats it before choosing continuation. This is bounded allocation overhead on unusually large views, not an incorrect result; avoiding it requires representation-selection branches or parameters. Reject the additional complexity for this uncommon low-impact case. |
| Final B4 — late task observation and candidate continuation | low | reject | WaitAsync can leave a non-cooperative provider/composite task finishing after timeout. The invocation still returns within its bound and cannot publish its late value; continuation performs only existing read paths. There is no configured unobserved-task crash handler. Production task ownership and additional read guards would add branches for a provider that ignores cancellation; reject this uncommon low-impact extension. |
| Final B5 — composite completion in stall fixtures | low | reject | LastRead intentionally tracks the stalled dependency, not its enclosing candidate task. The receipt promises dependency completion and the test observes that exact task; a composite may still finish read-only work afterward. Exposing private composite completion would add a test seam for no demonstrated contamination or failed assertion. |
| Final B6 — malformed transition fragment/payload controls | medium | patch | Existing malformed controls alter only pointer/base bindings. Add public-read rejection cases for missing/truncated/substituted/digest-invalid fragments and digest-consistent zero/multiple facts. The multiple-fact omission shares Final V1's root cause. |
| Final B7 — exact live-fold boundary controls | medium | patch | Current tests prove bounded successful folds but do not pin the 128/129 replacement-plus-retirement boundary. Add direct cases proving exact retirement at 128 and reachable-base continuation at 129. |
| Final B8 — refusal source-mutation assertions | medium | patch | The foreign/missing/corrupt witness theory compares epoch/witness ETags but omits selected source records. Snapshot their values and ETags before both refused entry points and compare afterward. |
| Final B9 — rename verifier changes literal text | low | patch | Verify.cs applies substitutions to complete method text, so an assertion literal or comment matching an old method name can change undetected. Rewrite Roslyn identifier tokens with four exact audited active-reference exceptions (two source-fitness literals and two comments), comparing every other token/trivia byte. All 786 methods pass; this directly protects the retained audit's stated source-preservation guarantee. |
| Final B10 — stale ledger closure totals | low | patch | Two new closure notes still cite pre-review totals without labeling them historical. Point them at the final full-gate totals and diagnostic count; distinguish any supplementary test-only review evidence. |
| Final V1 — ambiguous continuation regression | medium | patch | Pre-verified: removing facts != 1 lets a correctly hashed Used+Revoked transition select Used and satisfy a matching complete-view digest. Add an executable public ReadViewAsync rejection control; group with Final B6. |

## Verification

- Restore/build `Hexalith.Timesheets.slnx` in matching Debug/source mode with `-p:HexalithEventStoreVersion=3.127.0 -m:1 /nr:false`; build with `--no-restore -warnaserror`.
- Run focused projection/loader regressions, then all six built xUnit executables individually; compare discovered case inventory before/after renaming.
- Run `python3 -m unittest discover -s scripts -p 'test_magic_link_index_selection.py'` and relevant Python discovery; run compiled AppHost configuration-only guards.
- Complete independent workflow review, corrections, commitlint, commit, fetch/divergence inspection, and push to main; exclude reference gitlinks.

Investigation baseline: `357db9d8c2df951a60c8ffb59c0d61c6a7bb3c8c`. Working tree was clean on main. Planning made no production changes and ran no runtime acceptance tests.
