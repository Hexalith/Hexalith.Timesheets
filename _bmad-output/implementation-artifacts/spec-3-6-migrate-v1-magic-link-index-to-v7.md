---
title: 'Story 3.6: explicit verified v1-to-v7 magic-link index migration'
type: 'feature'
created: '2026-10-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 60580d974c6e77a7e9fe0cbab93f22670c02ecda
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - references/Hexalith.AI.Tools/hexalith-state-instructions.md
---

<frozen-after-approval reason="user explicitly authorized scoped specification and implementation">

## Intent

The stored v1 unique-only magic-link index blocks ordinary v7 delivery, while the existing v6 operator migration intentionally rejects it. Implement a separate explicit operator migration using Hexalith.EventStore, recovering collision candidates from independently verified complete source inventory. Preserve existing work and fail-closed delivery.

## Boundaries & Constraints

Always require authenticated internal ingress, server-side migration authority, and an independently verified, immutable, replayable cross-tenant issuance-contribution inventory. Bind expected contribution count/digest, source-cut revision, and enforced writer/delivery quiescence to the server-side receipt; verify again before promotion and on resume. A caller fingerprint, legacy rows, delivery checkpoint census, or observed maximum never establishes completeness. Inventory supplies all retained issuance candidates, including terminal capabilities and collisions, or independently verified erasure exclusions.

Never change ordinary projection/loader behavior, the v6 migration, Redis contents, AppHost, ServiceDefaults, dependencies, or submodule pointers. Use only EventStore read-model/epoch APIs for migration writes. Keep source v1 data available until promotion and retain direct legacy data afterward. No fake authority registration, certification, live seeding, commits, or staging.

An empty pending global journal and externally enforced source/delivery quiescence are explicit prerequisites: the existing SDK cannot begin with uncovered legacy deliveries, and their captured event identities cannot be fabricated. Refuse nonempty journals and mixed v6/v7 source layouts. The source provider remains unavailable by default; missing installation proof is a runtime blocker, not an implementation choice to ask the user to invent.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Error handling |
|---|---|---|---|
| Authority absent | Anonymous caller, default denial, or missing verified inventory | No epoch reservation or source mutation | Opaque refusal |
| Unique-only legacy | v1 root and complete source with omitted collisions | Build all certified candidates; ambiguity remains | Never infer completeness from v1 |
| Invalid inventory | Wrong digest/count, duplicate/conflicting owners, invalid ordering, more than 10,000 contributions per tenant, or revoked receipt | No promotion | Reject before reservation where possible; abort private stage after reservation |
| Unsafe source | Pending journal, mixed layouts, or changed legacy root/source cut | Source remains selected/untrusted | Reject; no checkpoint forgery |
| Interrupted migration | Store failure/cancellation after page or checkpoint, or before seal/commit | Same operation resumes after service/coordinator recreation | Keep durable private stage for retry |
| Failed migration | Source/receipt invalidated after staging | No v7 selector; legacy retained; private generation isolated | Recoverable abort |
| Successful migration | Complete verified immutable inventory, unchanged v1 and quiescence | Atomically select v7 token/owner/tenant trees | Retry same receipt idempotently; reject changed receipt/operation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs` — reference append/checkpoint/seal recovery; preserve implementation.
- `src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkIndexMigrationAuthority.cs` — existing operator authority, default-deny; retain v6 compatibility.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkTokenHashCapabilityIndexTreeBuilder.cs` / `TreeReader.cs` — reuse bounded tenant replacement and numeric addressed v7 nodes, retirements and roster.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs` — registered-writer fence, durable append ordinals, private generations, seal, commit, abort. Begin requires journal coverage; this migration requires an empty journal instead.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/SharedProjectionSourceRosterActor.cs` — independent installation coverage and writer receipts are prerequisites; per-tenant capture cannot certify global legacy coverage. No installed authority exists here.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Streams/SourceNamespaceSnapshotReader.cs` — authenticated exact-prefix verification pattern; tenant-only and bounded to 1,000 sources, unsuitable as a global census. Checkpoint enumeration may skip missing/corrupt pages.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs` / `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` — separate internal `migrate-v1` route and composed auth/default-deny checks.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Timesheets.Projections/MagicLinks/` — add verified inventory provider/receipt contracts and unavailable default, one type per new file; document independent coverage and quiescence obligations.
- [x] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV1Migration.cs` — preflight direct/selected v1 source; stream deterministic tenant/owner ordered contributions, bound retained tenant rows to 10,000 and append pages to eight operations, verify complete digest/count, persist receipt/source-bound checkpoints, recover retry/restart, and seal/promote only after final source and authority validation. Abort invalid builds; preserve retryable I/O stages. Verify operation/receipt identity on completed retries.
- [x] `src/Hexalith.Timesheets/Endpoints/MagicLinks/` — register unavailable inventory and new migrator, map authenticated separate route, keep v6 behavior.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV1MigrationTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` — cover every matrix row, collision preservation across tenants, bounded physical staging, interrupted node/checkpoint/seal/commit recovery, failed-stage isolation and successful persisted readbacks. Test fixtures represent simulated verification only.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, `3-6-resume-checkpoint.md`, `docs/local-magic-link-dapr-proof.md`, `docs/launch-readiness.md` — append exact evidence and missing real prerequisites; keep Story 3.6/release FAIL and historical evidence intact.

**Acceptance Criteria:**

- Given direct or selected v1 state and a verified complete inventory, when the explicit operator migration succeeds, then selected v7 contains every source candidate and collisions remain unresolved by lookup.
- Given any missing authority/completeness/quiescence evidence, when migration or ordinary delivery is attempted, then no trusted empty/bootstrap generation is selected.
- Given passing local migration tests without deployed inventory/authority or live journey acceptance, when evidence is recorded, then Story 3.6 remains in progress and release FAIL.

## Implementation Notes

The user authorized specification plus implementation in this turn. No further permission gate is needed. Current root working tree was clean at start. Preserve the existing spec-21 and all prior story work. SDK pin is actually 10.0.401 in current global.json.

Implemented through EventStore private streaming stages with immutable ownership and abort witnesses using ReadModelWritePolicy; witnesses survive disposal of private checkpoints. New input bounds and byte-bounded append batching retain at most one 10,000-row tenant. Initial pre-review verification passed 43 v1 cases, 31 composed ingress cases and all six default suites. Final focused migration/digest checks passed 64/64 and ingress passed 37/37; final broad verification passed 1,867 tests with five expected skips and zero failures. The solution build has zero warnings/errors. Matrix coverage and exact commands are recorded in tests/3-6-test-summary.md. No concrete provider/authority, live migration or journey was created. Tenant ListTenantsQueryHandler returns a paginated read-model view and cannot certify complete legacy source coverage.


Review preservation requirements: keep the separate route, complete independently verified inventory, default-deny authority, uninstalled provider, source-count/digest validation, bounded tenant staging, all existing collision/restart/abort tests, and all prior Story 3.6 work. Apply the smallest verified fixes without reverting existing work. Reservation ownership must survive Begin before checkpoint zero; denial cannot authorize abort; changed receipt contents cannot destroy an old stage; final source/authority checks follow the final replay; completion repair uses original durable evidence and current operator authority. Test opaque validation/refusal and retryable SDK errors, independent digest vectors, and actual 10,000-row/byte bounds. Existing consumer checkpoints must survive migration; their absence remains a separately verified prerequisite, never reconstructed from candidate rows.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
|---|---|---|
| Blind 1: final scan revocation | high | Scratch enumeration 4 revocation still selects epoch 1. Current checks precede the long scan. Patch final authority, source and journal checks after enumeration. |
| Blind 2: reservation ownership gap | medium | Durable Begin can throw before checkpoint 0; revoked lookup then leaves Building without receipt ownership. Patch durable ownership before Begin using existing EventStore witness/policy. |
| Blind 3: denied retry aborts stage | high | A denied matching retry enters catch and writes abort state despite authority false. Patch cleanup authorization; denial must preserve the private operation. |
| Blind 4: changed checkpoint root | medium | Probe changes checkpoint 1 root while retaining counters; resume drops the first tenant. Patch proof of checkpoint content and staged/source membership before skipping or promotion. |
| Blind 5: owner sequence handoff | medium | Defer pre-existing SDK checkpoint/coverage prerequisite: sequence 2 with no checkpoint fails the same acceptedSequence+1 rule before and after migration; ReadOwnerCheckpointAsync retains existing checkpoints independently of generation. Add preservation/fail-closed tests and explicit release blocker; never seed invented continuity. |
| Blind 6: malformed request identifiers | medium | ArgumentException escapes the v1 route. Patch opaque request refusal and ingress cases. |
| Blind 7: expired receipt after selector CAS | medium | Completed receipt proof exists but current inventory validation runs first, denying repair. Patch authorized repair of already selected completion, bound to original receipt, with no new promotion. |
| Blind 8: SDK errors map to 404 | medium | SDK incomplete-page/fence errors can leave a retryable stage; broad InvalidOperationException catch presents refusal. Patch typed evidence refusal and opaque retryable service failure. |
| Blind 9: supported byte/capacity bounds | medium | Existing test covers 160 short rows and operation/roster counts. Patch valid 10,000-row and maximum-width byte-splitting evidence. |
| Blind 10: independent codec vectors | low | Fixtures use the same digest helper on both sides; external implementations lack fixed vectors. Patch version-1 encoding documentation and independent literal vectors without adding a production certificate or changing receipt semantics. |
| Edge 1: final scan revocation | high | Same reproduced defect as Blind 1; patch same post-scan checks. |
| Edge 2: checkpoint-zero reservation gap | medium | Same durable reservation defect as Blind 2; patch ownership before Begin. |
| Edge 3: denied matching retry cleanup | high | Same unauthorized abort defect as Blind 3; patch explicit cleanup grant. |
| Edge 4: malformed changed receipt | medium | Shape validation throws before receipt equality clears ownership, causing original-stage abort. Patch comparison before shape validation and assert unchanged staged operation. |
| Edge 5: malformed request | medium | Same route ArgumentException gap as Blind 6; patch request refusal. |
| Edge 6: final validation claim | high | Final VerifyPromotionAsync still checks authority/source before enumeration. Same defect as Blind 1; patch post-scan checks. |
| Verification 1: post-seal invalidation | medium | Pre-verified gap: existing revocation occurs before earlier check; removing final check preserves all existing tests. Patch a fired seal callback plus legacy/selector/abort assertions. |
| Verification 2: invalid verified inventory HTTP refusal | medium | Pre-verified gap: existing HTTP failures are UnauthorizedAccessException, while invalid inventory is tested only at service level. Patch composed 404 with persisted unchanged legacy/epoch. |
| Parent 1: intervening migration during reservation | medium | The predicted ownership epoch can differ from Begin's acquired epoch after an intervening successful migration; throwing before ownership binding strands the new Building epoch. Patch preflight fence revalidation and actual-epoch binding/isolation. Deterministic preflight and post-witness callbacks now retain the intervening selected v7 generation and leave no private epoch. |
| Parent 2: concurrent abort during ownership recovery | medium | A concurrent abort can complete after the recovery status read, allowing Begin to acquire a later epoch. Patch actual-epoch binding and authorized isolation on recovery, preserving legacy selection and terminal operation identity. A fired receipt-lookup callback now proves recovery leaves neither Building nor Aborting and the terminal retry cannot allocate again. |

Three independent reviewers used /tmp/timesheets-v1-migration-final-review-k4y7m82g.diff; only the edge reviewer received the scoped spec separately. The spec was excluded from the blind review diff to keep its claims out of those reviewers' context. Every finding was classified before grouping. Same-root findings are fixed together; no review finding is discarded or merged out of this log. The three reproduced probes and checkpoint/continuity counterexamples are retained at /tmp/timesheets-blind-review-probe/{Program.cs,ReservationFaultStore.cs,output.log}.

### Review closure

All patch findings above are implemented without changing the frozen intent or public authority/provider contracts. The final focused migration/digest run passed 64/64; composed ingress passed 37/37. The parent audited the changed boundaries and their persisted-state assertions.

| Findings | Implemented behavior and passing regression |
|---|---|
| Blind 1; Edge 1, 6; Verification 1 | Post-replay boundary validation rejects receipt, authority or source changes during final enumeration and after successful sealing; `FinalReplayAndSuccessfulSealChangesAreCheckedBeforePromotion` asserts callbacks fired, unchanged selection and authorized isolation. |
| Blind 2; Edge 2 | Original receipt/source ownership persists before Begin and binds the actual epoch; `BeginWithoutCheckpointZeroKeepsFullReceiptOwnershipForRevokedLookupRecovery` proves both durable-Begin and checkpoint-zero failures recover after recreation and revoked lookup. |
| Blind 3; Edge 3 | Cleanup requires current authority; `DeniedMatchingRetryPreservesPersistedPrivateOperationThenRecoversWithAuthority` verifies unchanged epoch/checkpoint ETag, absent abort witness and later authorized success. |
| Blind 4 | Completed tenant membership and exact global leaf counts are checked against full inventory; `CorruptedCheckpointRootCannotDiscardCompletedTenantMembership` rejects the reproduced root substitution and retains legacy selection. |
| Blind 5 | Pre-existing continuity remains deferred in `deferred-work.md`. `ExistingSdkOwnerCheckpointSurvivesMigrationAndMissingContinuityStaysDeliveryGap` proves unchanged checkpoint, accepted next sequence for the known owner, and the same fail-closed gap for a missing owner before/after migration. |
| Blind 6; Edge 5 | Malformed identifiers receive empty opaque HTTP 404 with no reservation; `Malformed_v1_identifiers_are_opaquely_refused_without_reservation`. |
| Blind 7 | Already selected completion repairs only its marker using original identity and current operator grant; expiry, changed identity, denied grant and invalid v7 layouts are covered by the two `Selected...` completion tests. |
| Blind 8 | Typed validation maps to opaque 404; SDK/store interruption maps to empty retryable 503 while retaining a resumable private stage; `V1_store_and_sdk_stage_failures_are_opaque_retryable_with_durable_private_state`. |
| Blind 9 | `TenThousandMaximumWidthContributionsPersistWithinMeasuredByteBounds` verifies every persisted row/owner and exact 10,000-row roster; largest physical values total 194,660 bytes, largest roster page 155,722 bytes, and 156 byte-split pages. Logical append width remains at most eight. |
| Blind 10 | Version-1 byte encoding is documented and independently computed literal contribution/receipt vectors pass in `MagicLinkV1InventoryDigestTests`, including Unicode, escapes and empty input. |
| Edge 4 | Full reserved receipt equality precedes shape validation; `MalformedChangedReceiptCannotAbortReservedOriginalReceipt` verifies unchanged private state and recovery with the original receipt. |
| Verification 2 | `Invalid_installed_v1_inventory_is_opaquely_refused_and_retains_legacy` proves typed invalid inventory yields empty 404, unchanged epoch and retained legacy root. |
| Parent 1 | `InterveningSuccessfulMigrationIsRetainedAcrossPreflightAndLateReservationRaces` proves preflight refusal or authorized isolation of the actual acquired epoch, retention of the intervening selected generation, and terminal retry without another allocation. |
| Parent 2 | `ConcurrentAbortDuringUnboundOwnershipRecoveryAbandonsOnlyActuallyAcquiredEpoch` proves fired concurrent abort, no leaked private epoch, no terminal reallocation and successful fresh operation. |

## Verification

Use source-reference Debug restore/build with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`, `-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false`, and `-warnaserror` for build. Run focused v1/v6/numeric/global-index tests, internal ingress tests and launch fitness, then all six test executables individually. Write exact logs to `/tmp`. Do not run the opt-in live journey unless independently verified cross-tenant coverage, migration authority, quiescence/journal recovery, and unique runtime ownership genuinely exist; report their absence without starting an AppHost.

Final parent verification passed the Debug solution build in 10.07 seconds with zero warnings/errors and all six individual default suites: 1,872 total, 1,867 passed, five expected skips, zero failures. Full commands and per-suite totals are in [the test summary](tests/3-6-test-summary.md). The final unified diff, including untracked files, is `/tmp/timesheets-v1-migration-postreview-4uzfa6gj.diff`. The scoped migration implementation is complete; Story 3.6, its original live-dependent spec tasks and release remain in progress/FAIL. No staging, commit, push, dependency or submodule changes were authorized or performed.
