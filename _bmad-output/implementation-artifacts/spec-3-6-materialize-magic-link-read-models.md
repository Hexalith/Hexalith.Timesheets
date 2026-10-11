---
title: 'Materialize Story 3.6 magic-link read models from EventStore delivery'
type: 'feature'
created: '2026-10-11'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '8c042a3a1a969deeedd29f85b0b0539bcac029c5'
review_loop_iteration: 3
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 passes a local Dapr journey, but no production handler materializes capability or Time Entry evidence read models from stored events.

**Approach:** Add named EventStore handlers for both models, including order-independent merging of capability issuance and Time Entry terminal events. Live delivery and rebuild must yield the same tenant-scoped view. Production identity and deployed acceptance remain Story 3.6 gates.

## Boundaries & Constraints

**Always:** Use EventStore `IReadModelStore`, `ReadModelWritePolicy`, and SDK slots/rebuild contracts. Validate tenant, owner, capability, lineage, duplicates, and freshness before publication. Keep incomplete capabilities undisclosed and read models non-authoritative for use.

**Never:** Expose tokens or hashes; call Dapr state directly; assume cross-stream order; change AppHost or ServiceDefaults; claim story or launch completion from local proof.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Delivery | Recording, issuance, terminal effect | Scoped evidence and capability rows reflect events | No public detail added |
| Reorder/replay | Terminal first, duplicates, restart | Pending fact stays undisclosed; later issuance yields one row | Conflict fails deterministically |
| Rebuild | Complete histories | Canonical rows/freshness; stale rows pruned | Failed rebuild leaves live view intact |
| Store outage | Read/write unavailable | No partial view | Retryable SDK result; safe diagnostics |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` — reuse the one-owner fold and stored-wrapper normalizer.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs` — reuse the final fold only after joining both streams; one SDK request cannot supply both.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` — reuse named route, CAS, failure, and shared rebuild patterns; keep the index non-authoritative.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IAsyncDomainSharedProjectionRebuildHandler.cs` — combine owner histories and prune stale rows. `src/Hexalith.Timesheets/Program.cs` already scans Projections.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkCapabilityReadModelAddress.cs` and `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModelAddress.cs` — define stable tenant-scoped addresses with no token-derived key or payload.
- [ ] `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjectionHandler.cs` — add an aggregate-owned handler with safe decoding, complete fold, CAS persistence, and rebuild plan.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkCapabilityProjectionView.cs` — hold issuance, pending terminal facts, and the published row in one tenant-and-capability-scoped value; the SDK epoch catch-up value limit requires separate physical values for each capability.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` — merge both owner streams, retain terminal-first facts, reject contradictions, and replace/prune the shared view during rebuild.
- [ ] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionHandlerTests.cs` and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionHandlerTests.cs` — prove delivery order, replay, identity conflict, outage, freshness, live/rebuild equivalence, and pruning via the persisted store seam.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — deliver stored owner events to both routes and read back models in the opt-in journey.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `docs/launch-readiness.md`, and `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — record exact evidence and remaining gates.
- [ ] `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModelAddress.cs` and tests — derive the SDK canonical aggregate-owned key for valid `AggregateIdentity` components; reject invalid colon-bearing IDs before address creation and retain canonical erasure support.
- [ ] `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjectionHandler.cs` and handler tests — preserve newer persisted evidence on old-prefix replay; reject contradictory second recordings and histories with effects preceding recording; prove stored adjustment decoding and both read/write outages.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` and tests — reject malformed recognized wrappers, incomplete issuance fields, changed same-sequence issuance content, and legacy same-stream terminal-before-issue; keep irrelevant deliveries from rewriting the tenant view; prove two-capability retention through the production epoch path. Persist no token-derived fingerprint, key, or payload; use the SDK's canonical event/journal identity for changed-hash replay.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` and tests — fence live delivery against shared rebuild promotion with the SDK epoch contract, keep candidate state within the SDK byte ceiling for valid supported histories, and prove stale persisted rows are unreachable through selected-generation reads after an applied rebuild. Rebuild accumulation must avoid quadratic tenant-wide work; failed rebuild must leave the selected generation intact.
- [ ] `references/Hexalith.EventStore` SDK and SDK tests — extend the shared rebuild contract with bounded, partitioned, durable candidate staging so Timesheets can rebuild all valid histories up to the supported 10,000 aggregate count without forcing complete tenant contents into one 1 MiB candidate. Preserve side-effect-free retry semantics and active-generation isolation; prove crash/retry, failed rebuild, high-entropy history, and promotion behavior in the EventStore repository that owns this contract.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` and host/handler tests — make expiry display truthful as of observation without granting use authority, and assert both new routes are registered in a normally run composed-host test.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` and handler tests — prevent a direct fallback from partially writing two capabilities; handle expected malformed or contradictory epoch folds through the SDK's supported failure/parking contract so one journal head cannot block later valid delivery; assert selected-generation issuance, use, terminal-first order, conflict, and store outage on the production epoch path.
- [ ] `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjectionHandler.cs` and handler tests — verify persisted owner identity and full prefix payload lineage before retaining an old row or replacing it with a higher cursor; reject recognized effects the pure fold skipped because they are invalid in the current state, without publishing misleading Fresh lineage.
- [ ] Handler tests — deliver recorded/submitted/approved Time Entry DTOs through the persisted handler and assert decision and lock evidence; rebuild an already expired capability and assert the selected generation displays expiry correctly. Prove expiry does not remain falsely Active when time passes without a new event.
- [ ] `references/Hexalith.EventStore` and Timesheets rebuild staging — define a recoverable cleanup or bounded-retention lifecycle for operation-scoped owner chunks, manifests, streamed page/key markers, and contribution indexes after commit and abort. Repeated full rebuilds must not retain an unbounded copy of complete private candidate data. Test the physical private state after repeated committed and aborted sessions, including crash/retry.
- [ ] Shared epoch delivery and capability handler — check authoritative owner sequence continuity across separate delivery requests, including the first accepted event and a rebuild capture watermark. A later issuance must not publish before a missing earlier owner event is settled; valid complete histories must still deliver and rebuild alike. Add a normal SDK test that delivers a later event first and then the earlier event.
- [ ] EventStore SDK outage handling — return `Retryable` with a safe state-unavailable reason when read-model store I/O fails during writer registration or journaling, before handler folding starts. Test both points. Keep cancellation and programming/identity errors distinct.
- [ ] Handler and fixture regressions — reject missing required Time Entry recording fields, mismatched approval states, and default terminal timestamps; add normal epoch revoked/expired, reverse owner rebuild order, two-tenant keyed Time Entry, and every-stream-page readback tests. Make the opt-in journey reread both new models after EventStore restart and label prior passing local evidence as historical until a latest-tree run passes.

**Acceptance Criteria:**

- Given stored owner histories, when SDK delivery runs, then persisted tenant-scoped models match canonical folds without granting use authority.
- Given reordered or replayed histories, when delivery and rebuild run, then both converge or fail safely without disclosing incomplete capability state.
- Given a store outage, when delivery runs, then the SDK receives a retryable failure and the last complete view remains.

## Implementation Notes

Review iteration 1 rejected the first implementation. Its passing local Dapr journey is a regression target for re-derivation, not current-tree evidence. Preserve the bounded transient `403` retry and the explicit persisted readback assertions.

## Spec Change Log

- Review iteration 1: Blind and edge-case reviews found stale-prefix overwrite, incomplete/contradictory folds, malformed wrapper acceptance, same-sequence issuance ambiguity, stale expiry display, and an unfenced or oversized shared rebuild candidate. The verification review found gaps for second-capability retention, stored adjustment decoding, applied pruning, and default host route discovery. Added explicit re-derivation tasks above and avoided carrying forward the known-bad handler assumptions. Later SDK inspection refuted the colon-key collision for valid EventStore identities; keep canonical aggregate-owned erasure. KEEP: EventStore `IReadModelStore`/slot usage, separated owner sequence spaces, undisclosed terminal-first facts, safe token-free published rows, the passing local Dapr readback fixture, and exact evidence about production gates.
- Step-03 verification clarified the physical storage shape: the SDK epoch path bounds one logical write to 64 KiB, so a tenant-wide value cannot safely hold the supported capability set. The handler uses one tenant-and-capability value and generation-selected reads; the shared rebuild candidate still represents the complete tenant set and promotion hides stale prior-generation rows. The first 10,000-history fixture repeated most fields and concealed the candidate-size risk, so it was replaced with varied valid identities and issuance metadata, and the candidate representation was compacted.
- Review iteration 2: the second implementation still tested mostly its direct fallback while production dispatch uses epoch catch-up. A malformed fold can pin the epoch journal; direct multi-capability writes can publish a partial view. The persisted issuance fingerprint is derived from the token hash, contrary to the no token-derived payload task. Time Entry replay lacks payload lineage checks and can mark ignored transitions Fresh. The candidate still exceeds 1 MiB with sufficiently long valid fields and does quadratic rebuild work. Amend the implementation tasks below to require normal epoch-path terminal/conflict/outage coverage, SDK-supported poison-event handling, token-independent persisted evidence, complete Time Entry lineage and transition validation, truthful expiry display, and a bounded or partitioned rebuild strategy for valid supported histories. Avoid carrying forward the known-bad direct-write, candidate-size, and persisted-fingerprint assumptions. KEEP: SDK canonical aggregate-owned Time Entry key and erasure slot, tenant-and-capability logical partition, terminal-first pending facts, generation-selected promotion and applied pruning test, strict duplicate-app preflight and synthetic stream position only in the local fixture, and exact evidence that the current combined live journey is unverified because another AppHost reused `eventstore`.
- Human decision after review iteration 2: extend the EventStore SDK contract for partitioned rebuild staging rather than lowering the approved capacity. Implement and verify the SDK change in its owning repository, then consume it from Timesheets. Do not treat a short-identifier compression fixture as proof for arbitrary valid high-entropy histories.
- Review iteration 3: Independent review found that operation-scoped private rebuild records accumulate without a closeout policy, normal epoch delivery can receive an isolated later owner sequence before its prefix, and store faults during SDK registration/journaling become `Indeterminate`. The non-frozen tasks now require recoverable bounded scratch lifecycle, cross-request owner sequencing, and retryable early SDK I/O failures. It also requires the concrete validation, keyed-read, page-integrity, reverse-order, terminal-branch, and restart regression checks from the triage log. Avoid carrying forward unbounded private candidate retention, an unguarded first owner sequence, and generic early-outage classification. KEEP: the token-free per-capability generation-selected view; terminal-first Time Entry facts; deterministic poison-event parking; aggregate-owned Time Entry CAS and payload lineage; 40 KiB bounded fragments; per-owner durable chunks; bounded streamed stage; applied pruning; 10,000 high-entropy and greater-than-64-MiB capacity fixtures; normally composed route assertion; and the opt-in Dapr journey with explicit persisted model readbacks and strict duplicate app-ID preflight.

## Review Triage Log

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1 — evidence key collision | false | reject: EventStore `AggregateIdentity` rejects colons in both tenant and aggregate IDs before persisted owner delivery; the cited cross-tenant pair cannot reach this handler through the SDK. The address must reject invalid direct calls and retain the SDK canonical key. |
| Blind 2 — older prefix overwrites evidence | high | bad_spec: `ProjectAsync` writes every complete prefix without comparing it with the stored cursor; an older replay can remove later confirmation or adjustment. |
| Blind 3 — second recording | medium | bad_spec: the existing pure fold applies every later `TimeEntryRecorded`; normalization rejects same-sequence conflicts but allows a second recording at another sequence. |
| Blind 4 — skipped pre-recording effect | medium | bad_spec: the pure fold silently ignores an effect while its model is null, then publishes a later recording from the same contiguous history. |
| Blind 5 — legacy terminal before issuance | medium | bad_spec: the new shared handler retains a capability-stream terminal before issuance and synthesizes an issue-first replay, bypassing the pure fold's ordering rejection. |
| Blind 6 — same-sequence hash conflict | medium | bad_spec: the persisted sanitized issuance row omits the hash; comparing only that row and sequence accepts a different token hash on later delivery. |
| Blind 7 — incomplete issuance fields | high | bad_spec: JSON can deserialize null fields into the issuance record, and the handler checks only some fields before constructing a published row. |
| Blind 8 — stale expiry badge | medium | bad_spec: issuance is folded at `IssuedAtUtc`; an old issuance rebuilt after expiry can still carry an Active expiry badge. |
| Blind 9 — shared rebuild race | high | bad_spec: this handler implements the unfenced shared rebuild interface; a live CAS before promotion can be overwritten by a candidate built from older histories. The SDK exposes an epoch interface for this case. |
| Blind 10 — candidate size ceiling | medium | bad_spec: the shared candidate contains full issuance, terminal and published rows, while the SDK caps candidate bytes at 1,048,576; an active tenant can exceed that before the 10,000-history count limit. |
| Blind 11 — irrelevant delivery write | low | patch: no recognized event returns `current`, but `ProjectAsync` still calls the CAS policy, creating or rewriting a tenant view for ordinary unrelated histories. |
| Blind 12 — no automatic invocation proof | false | reject: the spec and local proof claim explicit SDK route delivery; the script calls those named routes and verifies readback. Neither claims an automatic subscription invokes them. |
| Blind 13 — no post-restart new-model read | false | reject: the added proof claims new-model readback after confirm and adjust; its post-restart claim is for the pre-existing catalog and index checks, which the script retains. |
| Blind 14 — pruning plan only | medium | patch: the test inspects an empty plan but does not apply it to an existing tenant row; it cannot prove stale persisted rows are pruned. |
| Edge 1 — older prefix | high | bad_spec: the same reachable overwrite as Blind 2 remains after checking the write-policy call and stored cursor. |
| Edge 2 — second recording | medium | bad_spec: the same later-sequence overwrite as Blind 3 is accepted by the existing pure fold. |
| Edge 3 — evidence key collision | false | reject: the SDK accepts only colon-free tenant and aggregate IDs for real owner streams, so the proposed collision is unreachable through valid delivery. Direct address calls should fail closed on invalid IDs. |
| Edge 4 — null stored wrapper event | medium | bad_spec: a recognized `StoredMagicLinkIssued` or terminal wrapper can deserialize with `Event = null`; the handler's null-coalescing decode then treats it as unrelated and acknowledges it. |
| Edge 5 — changed hash replay | medium | bad_spec: the same sanitized-row comparison as Blind 6 cannot distinguish a changed token hash at the same sequence. |
| Gap 1 — preserve prior capability | medium | patch: existing tests and the live script assert only the current capability, leaving tenant-view retention across a second issuance unverified. |
| Gap 2 — stored adjustment decode | medium | patch: normal handler tests do not deliver `StoredTimeEntryAdjusted`; only the opt-in journey checks the adjusted persisted row. |
| Gap 3 — persisted rebuild pruning | medium | patch: the empty plan test does not seed and reread the destination key, so a wrong-key plan could pass. Same test gap as Blind 14. |
| Gap 4 — default route discovery | medium | bad_spec: direct handler tests do not prove host registration, and the only metadata assertion runs in an opt-in lane skipped by normal verification. A route registration regression could escape the default gate. |
| Gap other — stale EventStore pointer in readiness | medium | defer: the current gitlink was already `b57310fb` at HEAD before this change, while the edited readiness text retains the older `cf10b4c7` claim. Correcting repository-wide pointer prose is pre-existing documentation work. |
| Round 2 Blind 1 — poisoned epoch journal | high | bad_spec: `FoldCatchUpAsync` throws ordinary fold exceptions after the SDK journals a delivery; the coordinator handles only `SharedProjectionFoldFailureException`, so the head remains and blocks later deliveries. |
| Round 2 Blind 2 — direct tests miss epoch path | high | bad_spec: the normal dispatcher selects `FoldCatchUpAsync` for this handler; conflict, terminal, and outage assertions call `ProjectAsync`, leaving the production path largely untested. |
| Round 2 Blind 3 — partial direct multi-capability write | medium | bad_spec: a direct `ProjectAsync` request with two capability groups saves the first before the second can fail. Although normal dispatch uses epochs, this public fallback advertises success/failure semantics and can leave partial state for direct callers. |
| Round 2 Blind 4 — old-prefix lineage | medium | bad_spec: an older same-owner delivery with different payload at an earlier ordinal is retained without comparing its lineage to the stored row; direct delivery can acknowledge inconsistent history. |
| Round 2 Blind 5 — divergent higher prefix | high | bad_spec: a higher-cursor direct delivery replaces the stored Time Entry row without proving it extends the stored event history. The stored lineage has event types and ordinals but no payload digest. |
| Round 2 Blind 6 — ignored invalid effect | medium | bad_spec: the pure Time Entry fold skips a recognized effect that is invalid for the current state; the new handler still publishes a Fresh row at the later cursor with that event absent from lineage. |
| Round 2 Blind 7 — stored Time Entry owner | medium | patch: `Merge` compares cursors before checking the existing row's `TimeEntryId`, so a malformed stored row can survive an older replay. A direct identity guard and test suffice. |
| Round 2 Blind 8 — expiry ages without delivery | medium | bad_spec: the new persisted badge is computed once; after expiry passes without another event it remains Active, and Fresh metadata carries no observation timestamp to qualify it. |
| Round 2 Blind 9 — candidate byte limit | high | bad_spec: compacting a varied short-ID fixture does not bound valid identifier and metadata lengths; a tenant can exceed 1 MiB before the SDK's 10,000-history count ceiling. |
| Round 2 Blind 10 — quadratic rebuild work | medium | bad_spec: each `AccumulateAsync` inflates and recompresses the whole tenant candidate, making 10,000-history rebuilding quadratic and at risk of practical timeout. |
| Round 2 Blind 11 — old generation remains physical | false | reject: SDK generation selection deliberately makes old rows unreachable from current reads while retaining the previous generation physically. The frozen matrix asks that stale rows be pruned from the live view; the promoted-generation test verifies that behavior. |
| Round 2 Blind 12 — gitlinks/readiness | false | reject for this increment: the EventStore and Tenants gitlink changes in the baseline-to-tree diff were already committed before this work; `references/Hexalith.Projects` is an unrelated concurrent unstaged change. The stale EventStore prose is the previously logged pre-existing documentation issue. |
| Round 2 Gap 1 — fenced terminal delivery | high | bad_spec: normal dispatch uses epoch catch-up, but the normally run test checks only issuance there; a missing terminal decode in catch-up would leave consumed capabilities displayed as Issued. |
| Round 2 Gap 2 — approval handler coverage | medium | patch: pure-fold approval tests do not pass through the new persisted handler decoder. A normal handler test should assert submitted, approved, decision, and lock evidence after persisted delivery. |
| Round 2 Gap 3 — rebuilt expiry coverage | medium | patch: the direct expiry test and nonexpired rebuild equivalence test do not verify an expired issuance in the selected rebuilt generation. |
| Round 2 Edge 1 — partial direct write | medium | bad_spec: confirmed independently; `ProjectAsync` writes one capability group before it validates and saves a later group. The epoch route avoids this, but the direct fallback remains callable. |
| Round 2 Edge 2 — candidate overflow | high | bad_spec: confirmed independently; valid high-entropy identities and metadata can exceed the SDK candidate byte ceiling despite the short-ID stress test. |
| Round 2 Edge 3 — token-derived fingerprint | high | bad_spec: `Fingerprint` hashes the full issuance including `TokenHash` and persists that digest in the capability view. This violates the nonfrozen task's no token-derived read-model payload constraint, even though it does not store the raw token hash. |
| Round 2 Edge 4 — physical stale rows | false | reject: the SDK retains inactive generations by design; active-generation reads no longer expose the stale capability. The frozen rebuild criterion concerns the current view, and physical deletion is not required by that contract. |
| Round 3 Blind 1 — streamed page metadata retention | medium | bad_spec: `StageStreamPageAsync` persists a page and key marker for every mutation; commit leaves them behind, so repeated large rebuilds grow private storage. Grouped with Blind 2–3 as missing scratch lifecycle. |
| Round 3 Blind 2 — partition retention | medium | bad_spec: accepted owner manifests and chunks remain after both commit and abort; a repeated 10,000-owner rebuild copies the full inventory again. Same missing scratch lifecycle as Blind 1. |
| Round 3 Blind 3 — contribution index retention | medium | bad_spec: operation-scoped capability indexes are never reclaimed after promotion or abort, adding one private record per capability per run. Same missing scratch lifecycle as Blind 1. |
| Round 3 Blind 4 — owner sequence gap | medium | bad_spec: fenced dispatch checks consecutive events only within a request and the epoch journal tracks global position, so a later event can be accepted before its earlier owner event; a terminal-before-issuance history can temporarily publish a later issuance and rebuild rejects the full history. |
| Round 3 Blind 5 — default terminal timestamp | medium | patch: `ValidateTerminal` checks references but accepts default `UsedAtUtc`, `RevokedAtUtc`, and `ExpiredAtUtc`, which can publish year-one transition evidence. |
| Round 3 Blind 6 — Time Entry envelope lineage | low | reject: `EventPayloadLineage` intentionally compares canonical evidence content and sequence; message ID and envelope timestamp do not change the evidence fold. Ordinary EventStore delivery has its own persisted event identity, while adding a second public lineage contract for anomalous direct metadata replay is disproportionate. |
| Round 3 Blind 7 — persisted expiry badge | false | reject: `ReadObservedAsync` recomputes expiry and freshness at observation time; the raw selected value is storage, and deployed reader wiring remains an explicit launch gate. |
| Round 3 Blind 8 — revoked/expired epoch coverage | medium | patch: normal epoch tests assert `Used` but do not exercise the separate revoked and expired terminal fields and display branches. |
| Round 3 Blind 9 — reverse rebuild owner order | medium | patch: the current join test orders issuance before the Time Entry terminal; the reverse accepted-owner order needs an assertion. |
| Round 3 Blind 10 — post-restart new-model read | medium | patch: the opt-in fixture rereads old catalog/index state after restart but not the new evidence and capability values whose durability it claims. |
| Round 3 Blind 11 — ambiguous evidence prose | low | patch: a historical passing run is followed by an unqualified sentence saying the journey now passes, while the latest tree lacks a passing isolated rerun. |
| Round 3 Blind 12 — one type per file | false | reject: neither the Hexalith baseline nor repository configuration states that rule; the second small public storage record is a related fragment shape. |
| Round 3 Gap 1 — keyed Time Entry persisted read | medium | patch: the test store ignores keys and all normal handler cases use one tenant; a wrong tenant key in `ProjectAsync` would pass. Add a two-tenant canonical-key assertion. |
| Round 3 Gap 2 — interior stream pages | medium | patch: large streamed-stage tests read only endpoint rows, so an omitted interior page could escape even when the page count is sealed. Read a row from every page. |
| Round 3 Edge 1 — null recording fields | medium | patch: `OwnerOf` may dereference a null `TimeEntryId`, and the pure fold can publish other null required recording fields; validate recognized payloads and return a deterministic failure. |
| Round 3 Edge 2 — wrong approval state | medium | patch: the pure projection accepts a submitted/approved payload's supplied state without checking that it matches the event transition, allowing misleading approval or lock evidence. |
| Round 3 Edge 3 — more than 127 fragments | false | reject for valid stored histories: EventStore caps a produced event payload at 1 MiB, and a capability has at most one issuance and one terminal; the near-limit paired test produces under 127 40 KiB fragments. Oversized caller-submitted events are outside the authoritative history bound. |
| Round 3 Edge 4 — default terminal timestamp | medium | patch: same reachable missing timestamp validation as Blind 5. |
| Round 3 Edge 5 — foreign fold failure position | false | reject for this change: the Timesheets `Park` helper always supplies the current delivery's source stream and envelope position. The generic coordinator condition predates this increment. |
| Round 3 Edge 6 — registration/journal outage | medium | bad_spec: SDK epoch registration and journal store calls can throw before the new read/write wrappers run; the dispatcher catches them generically as `Indeterminate`, while the frozen outage row requires a retryable result. |

## Design Notes

Use an aggregate-owned Time Entry slot and a shared capability slot. The shared slot holds a logical tenant view partitioned into per-capability values to respect the SDK epoch value limit. Terminal and issuance events have independent sequence spaces. Keep terminal-first Time Entry facts pending until a validated issuance, but reject a legacy capability-stream terminal that precedes issuance in that same stream. Use the SDK epoch contract because live delivery can race promotion; normal delivery bypasses `ProjectAsync`. Expected fold conflicts must use the SDK's parking/failure mechanism or be rejected before journaling. Extend the EventStore SDK's shared rebuild contract for bounded partitioned staging: the current single opaque candidate cannot represent all supported valid histories. Keep accumulation retry-safe, failed rebuild isolated from active reads, and work bounded per owner. Preserve the live Dapr fixture and its readback assertions. Persist no fingerprint or other data derived from the token hash in the capability view; rely on SDK journal identity for same-sequence conflicting deliveries. The Time Entry model may need an internal full-payload lineage digest in addition to its display lineage to validate replay and extensions.

## Verification

Re-run the affected project builds, six default test executables, and opt-in Dapr readback after re-derivation. The first implementation passed those checks before review, but was reverted. Its solution builds failed with the existing EventStore.Contracts duplicate UniqueIds MSB3243/CS1704 conflict in source-reference mode and Works HXW0002 with 176 missing references in the fallback mode.

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero errors or record the known EventStore.Contracts UniqueIds conflict.
- Build affected test projects; run their xUnit v3 executables individually — zero failures. Run opt-in Dapr with its local topology.
