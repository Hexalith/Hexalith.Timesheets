# Five recovery and verification deferrals — 2026-10-23

Implementation baseline: `357db9d8c2df951a60c8ffb59c0d61c6a7bb3c8c`.
Approved scope: [amended spec](../spec-five-recovery-and-verification-deferrals.md).
Final corrected-source receipts: [review iteration 1](../evidence/completed-followups/20261023-five-recovery-and-verification/review-iteration-1/).
Earlier receipts in the parent evidence directory are **pre-review history**;
their passing source hashes and totals do not qualify the capacity redesign.

## Changes and executed coverage

| Spec scenario | Implementation and evidence |
| --- | --- |
| View replacement | Exact old-digest `IdempotentAbsent` deletion follows replacement fragments and pointer when the logical fold fits 128 operations. Larger folds retain the validated flat base as reachable storage and publish bounded token-free transition fragments before a scoped single-layer continuation pointer. The original storage constructor and flat JSON representation remain compatible; null continuation metadata is omitted. Reads restore a flat one-fact base, apply a normalized issuance row or terminal fact through existing Merge/Publish or Apply rules, then check complete-view digest/length and scope. Nested/ambiguous continuations refuse. Pointer/generation/ETag crossing rechecks remain. |
| Capacity and lifecycle | `MaximalEntropyTransitionsUseBoundedContinuationAndRetainReachableFlatBase` covers nine combinations: use/revoke/expire, legacy capability-owned terminals, terminal-first issuance, modern/legacy fragments, near-1-MiB high-entropy issuance and terminal inputs, operation/value limits, no issuance event or TokenHash in transitions, interruption before publication, redelivery, reachable bases and crossing readers. Malformed, foreign, nested, empty, completed, digest/length and count bindings refuse. Rebuilds keep flat staging. `UnsupportedCompleteViewCapacityPreservesPointerFragmentsAndJournalHead` proves unsupported complete size or an incomplete lifecycle preserves source ETag/fragments and keeps the accepted head retryable. Ordinary retirement tests cover inline conversion, foreign scopes and interruption after publication. |
| Diagnostic admission | The supported Open/drained subset requires typed containers/scalars, bounded copy-on-write anchor, global writer set, checkpoint fields, retained names and zero unsupported streaming/cleanup counters. Inline captured digests must be nonempty, 32-byte aligned and no more numerous than the owner sequence. Four ledger corruption reproductions, healthy SDK-shaped snapshots and the new 0/1/31/33/64-byte regressions pass. Unsupported staging/chunks refuse with fixed privacy-safe messages. This is a conservative subset without full SDK parity or ETag admission. |
| Abort recovery | An immutable witness binds store/tenant/domain/family, operation, certified inventory, reserved epoch and selected source generation after Begin and before abort handling. Initialization faults leave Building retryable; Aborting only reads an existing witness. Recovery renews authority, verifies every binding, rechecks epoch/source/operation/phase and resumes SDK abort without promotion. Tests cover cold/repeated recovery, ten foreign/missing/corrupt/authority cases, before/after-write faults, ETag-preserving retry and promotion refusal. Migration-only cleanup is independently asserted before explicit recovery. Healthy pending delivery is selected into the old generation with a drained journal; interruption after a committed private-stage delete is resumed after restart, removing remaining private values and stage metadata. |
| Loader stalls | One monotonic invocation deadline covers candidate index/coordinator/tree, history and catalog waits without charging read-model calls against history page/event/byte counters. Standalone catalogs receive their own budget. Eleven read-model cases cover non-cooperative waits, cancellation, late success/fault and healthy later resolution; the full loader class passes 145/145. Tests measure elapsed time with a 300 ms scheduling allowance, release dependencies in finally, and await their eventual completion. The catalog case first spends 650 ms in candidate lookup and must finish within the shared one-second budget plus scheduling allowance. |
| Test rename | All 786 attributed underscore methods in 83 files retain attributes, signatures, data, assertions and trivia after identifier-token substitution, with four exact documented active-reference updates (two source-fitness literals and two complete comments). Current Roslyn inventory has zero attributed underscore names; collision checks and compilation pass. Historical before/after discovery preserves 2,546 cases. Full-gate discovery has 2,562 cases, including 16 later regressions. Final discovery after eight supplementary test-only additions has 2,570 cases: all originals remain equivalent. Class/method mappings, argument display names, explicit flags, traits and skips are compared. |

Focused review selection: **17 passed, zero failed/skipped**. Loader class:
**145 passed, zero failed/skipped**. Runnable Roslyn/discovery/dependency audit
sources and exact invocations are retained in
[audit-sources/README.md](../evidence/completed-followups/20261023-five-recovery-and-verification/audit-sources/README.md).

## Commands and source qualification

All commands use SDK `10.0.402`, Debug/source mode and the consuming EventStore
catalog version `3.127.0`. Matching restore/build graphs:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -warnaserror -m:1 /nr:false
```

Both exit 0; the build has zero warnings/errors. Source-mode dependency identity
is more precise than a package label: actual EventStore HEAD is
`e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`. The retained
[dependency identities](../evidence/completed-followups/20261023-five-recovery-and-verification/review-iteration-1/dependency-identities.json)
record full canonical gitlinks, actual HEADs, dirty flags and porcelain status for
all 13 root-declared checkouts. Every checkout is clean and matches its gitlink.
This inventory includes dependencies not compiled by every project. Timesheets
itself was dirty with this approved implementation batch; no reference gitlink,
dependency or topology was changed.

Each full xUnit executable runs individually:

```bash
tests/<Project>/bin/Debug/net10.0/<Project> -result-xml /tmp/five-review-full-<Project>.xml
```

The focused projection selection uses six `-method` arguments:
`*ColdRecoveryDrainsHealthyPendingDeliveryAndResumesInterruptedStageDeletion`,
`*ColdAuthorizedAbortRecoveryResumesAndRepeatedRecoveryNeverPromotes`,
`*MaximalEntropyTransitionsUseBoundedContinuationAndRetainReachableFlatBase`,
`*UnsupportedCompleteViewCapacityPreservesPointerFragmentsAndJournalHead`,
`*ReplacementRetiresOnlyValidatedPriorFragmentsAfterPublishingPointer`, and
`*InterruptedRetirementResumesPreparedDeletesAndCrossingReaderSeesReplacement`.
Loader filtering uses `-class Hexalith.Timesheets.Server.Tests.EventStoreMagicLinkConfirmationCapabilityStateLoaderTests`.
Discovery runs every executable with `-preEnumerateTheories -list full/json`.

| Other verification | Result |
| --- | --- |
| `python3 -m unittest discover -s scripts -p 'test_magic_link_index_selection.py'` | 30 passed |
| `python3 -m unittest discover -s scripts -p 'test_*.py'` | 130 passed |
| `python3 -m unittest discover -s scripts/tests -p 'test_*.py'` | 88 passed |
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only` | 35/35 compiled probes; no resources enabled |
| Retained Roslyn source audit | 786 exact source-preserving renames; zero remaining attributed underscores |
| Retained discovery audit | 2,546 equivalent original cases; 16 additions; no removed/altered cases |

## Full corrected-source executable gates

The final full gates and source/receipt hashes are recorded in
[review verification](../evidence/completed-followups/20261023-five-recovery-and-verification/review-iteration-1/verification.json).

<!-- corrected-source-suite-table -->
All six corrected-source suites pass: **2,557 passed, zero failed, five expected skips**.

| Suite | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| ArchitectureTests | 109 | 109 | 0 | 0 | 11.892 s |
| Contracts.Tests | 92 | 92 | 0 | 0 | 0.495 s |
| IntegrationTests | 903 | 898 | 0 | 5 | 825.775 s |
| Projections.Tests | 689 | 689 | 0 | 0 | 873.563 s |
| Server.Tests | 693 | 693 | 0 | 0 | 7.752 s |
| Works.Tests | 76 | 76 | 0 | 0 | 0.293 s |

These are local correctness receipts. They do not qualify runtime/deployed acceptance or performance.
<!-- /corrected-source-suite-table -->

## Supplementary review corrections

Eight later test-only cases add public transition-fragment/fact-set refusals and
exact 128/129-operation boundary controls. The existing ten foreign-witness cases
now check selected-source values and ETags independently around both refused
entry points. These additions were checked only by the focused selection; the
six full runs above remain the 2,562-case / 2,557-pass / five-skip receipts and do
not include the eight supplementary cases. The stricter Roslyn audit replaces
identifier tokens only, plus four exact allowlisted active references (two
source-fitness literals and two complete comment trivia values), each scoped by
file/method/complete old/new text. Every other token, string literal, comment and
trivia remains byte-exact; all 786 original methods pass again.

The parent independently reran the 18-case selection: **18 passed, zero failed or
skipped**. Implementation receipts additionally record 19 projection cases
(including one existing comment-reference test) and one existing server case,
all passing. Both focused project builds have zero warnings/errors. Runtime and
diagnostic sources are unchanged from the full gates. Final discovery preserves
all 2,546 original cases with 24 additions (2,570 total), no removals or altered
original identities. Supplementary source, assembly and receipt hashes are in
[final review verification](../evidence/completed-followups/20261023-five-recovery-and-verification/final-review-patches/verification.json).

The independent parent selection was:

```bash
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*PublicReadRejectsMalformedTransitionFragmentsAndFactSets' -method '*ExactFoldCapacityBoundaryRetiresOrRetainsTheValidatedFlatBase' -method '*AbortRecoveryRefusesForeignMissingOrCorruptBindingsWithoutMutation' -result-xml /tmp/five-review-final-parent.xml
```

All three review layers completed. Every direct correction is resolved; no new
deferred-work entries were added.

## Deployment obligations and preserved limitations

Use a complete-stop deployment: pause delivery, stop all old consumers/readers/writers,
replace every instance with compatible code, then resume delivery. Continuation
readers and writers activate together; mixed-version rollout is unsupported.
Continuations retain a reachable flat base plus one transition until generation
cleanup; rebuild staging remains flat. Unsupported complete-view capacity keeps
the journal head retryable. No nested continuation or unbounded transition chain
is supported.

Witness refusal makes no Timesheets source, witness or fence writes. SDK
`GetStatusAsync` may reconcile its own control/discovery indexes; it is not a
read-only storage primitive. No witness is synthesized in Aborting. Pending
deliveries whose normal SDK fold refuses corrupt selected metadata remain in
Aborting until a separate certified source-repair procedure exists. Recovery
neither repairs/deletes corrupt selected metadata nor discards deliveries.
The SDK Abort API has no atomic expected-epoch/inventory binding after the final
status check. Operation IDs must remain unique and maintenance serialized.

The diagnostic supplies neither complete SDK parity nor ETag admission.
Successful production resolution and runtime qualification remain required.
The parent workflow's read-only Aspire preflight found a foreign Tenants AppHost
and preserved it. Configuration-only probes constructed no resources. No live
migration, live Dapr acceptance, performance qualification or deployed acceptance
was run. Launch remains **FAIL** and Story 3.6 obligations remain open.

The original 2,548-case / 2,543-pass receipts and initial reflection-fixture failure
remain pre-review history. The candidate core retains the original private
`ResolveCandidateAsync` signature required by those fixtures. Independent
workflow review, commitlint, final Git audit, commit and push are handled by the
parent workflow after implementation verification.
