---
title: 'Add durable magic-link submission with legacy-stream compatibility'
type: 'feature'
created: '2026-10-10'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: 'aa57e591780404f779f87ed72806c6acf41aee40'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 loads valid magic links, but submit routes deny valid decisions because capability use and its Time Entry effect are not persisted. EventStore commits one aggregate stream per command; the two effects currently belong to separate streams.

**Approach:** Make the Time Entry stream own new terminal events and matching Time Entry effects atomically. Keep issuance in the capability stream and fold historical terminal events there. Revalidate server-resolved intent inside EventStore processing; return success only after verified commit. Preserve opaque denials.

## Boundaries & Constraints

**Always:** Preserve legacy replay, server authorization, Fresh catalog trust, concurrent single-use, safe diagnostics, pure aggregates, and fail-closed kernel defaults. Deployed inventory and live topology remain release gates.

**Never:** Use sequential writes, trust HTTP snapshots as commit authority, treat gateway acceptance as completion, mutate read models, add AppHost topology or UI, migrate unknown histories, or mark Story 3.6 done from stubs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Confirm or adjust | Valid unused link and Fresh catalog | One committed use event plus matching Time Entry event in the same stream | Success after commit status |
| Replay or concurrent submit | Same capability | One winner and one Time Entry effect | Losers receive shared opaque denial |
| Revoke or expire | Issued capability | Terminal event in the Time Entry stream | Later use denied |
| Historical terminal state | Old capability stream contains Used/Revoked/Expired | Fold remains terminal | No rewrite or reissue |
| Invalid or unavailable authority | Wrong scope, stale catalog, missing source, or read fault | No accepted mutation | Shared opaque denial; safe internal category |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` — replace in-memory Accepted/always-denied results with committed command outcomes; preserve GET and denial helper.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs` — reuse pure decisions; `WasDispatched` is not a receipt.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` — give actor processing a capability-stream-only read; fold target terminal events only for external resolution, using the actor-provided target state inside `/process`.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs` — add terminal markers to the Time Entry fold so the target actor detects replay.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs`, `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`, `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceListProjection.cs`, and `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs` — normalize internal stored payloads before every affected read-model fold and fail closed on contradictory history.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandStatusController.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/StreamsController.cs`, and `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs` — provide a platform-owned workload-authenticated command submit/status/read channel without weakening human routes.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` — verify every issuance field and all knowable Time Entry effect/audit fields from committed readback; keep unknown outcomes opaque.
- `src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs` and EventStore HTTP security fixtures — prove the production binding source and registered middleware policies, not only direct controller calls.

## Tasks & Acceptance

**Execution:**

- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandStatusController.cs`, `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/StreamsController.cs`, and `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs` — add separate workload-only submit/status/read operations that stamp a gateway-verified origin into the command envelope; keep human endpoints and RBAC intact. Add matching client methods in `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` and its interface.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/Commands/CommitMagicLinkUse.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — keep resolved intents internal; reject absent trusted origin and malformed members; issue to capability stream and atomically emit use plus matching Time Entry effect in the owner stream. Recheck authorization against gateway-verified origin, not serialized actor fields.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` and `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs` — preserve legacy terminal folds and detect ambiguous histories; while processing a Time Entry actor command, load only the different capability stream and use `DomainServiceCurrentState` for the owner, with no owner-stream callback.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs`, `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`, `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceListProjection.cs`, and `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs` — fold internal stored payloads into the same results as legacy domain events, preserving tenant checks, duplicate-delivery behavior, and fail-closed ambiguity handling.
- [ ] `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`, `src/Hexalith.Timesheets/Program.cs`, and `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` — bind the platform workload client and keyed processor; return Accepted only after Completed status and verified event batch; keep independent requests distinct and unknown outcomes opaque.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`, `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkPersistedSubmissionTests.cs`, and EventStore controller/security tests under `references/Hexalith.EventStore/tests/` — prove protected real `/process` dispatch, workload-only submit/status/read, actor commit/reload for confirm and adjust, one-winner concurrency, legacy Used/Revoked/Expired history, non-Completed status denial, projection results, and equal opaque failures.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, and `docs/launch-readiness.md` — record local persisted-state evidence separately from deployed Dapr and release gates.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — reject terminal-only or otherwise corrupt capability state before issuance and malformed nested issuance members; allow an issued proposed-entry link to receive revoke/expire in its Time Entry owner stream before the entry is recorded; reject mismatched recorded owner identity.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — recheck expiry against an injected server processing clock immediately before a use decision, including commands queued before expiry, while keeping aggregate methods pure and timestamps coherent.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` — compare all issued-event scope/action/expiry/source/single-use fields with the request, compare transition audit source, and check adjustment effect fields against the submitted edits and internally consistent prior/adjusted identity, target, scope, category, and AI metrics. Verify exact owner-stream sequence and the Completed status as before.
- [ ] `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryStoredEventNormalizer.cs` and capability projection — reject distinct payloads sharing one stream sequence regardless of event type or message ID; keep equivalent duplicate delivery idempotent and preserve blank-message legacy events.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/`, `tests/Hexalith.Timesheets.Server.Tests/`, and `references/Hexalith.EventStore/tests/` — exercise malformed/orphan issuance, proposed-entry revocation/expiry and actor recreation, queued-past-expiry denial, full issuance/adjustment/transition receipt mismatch, sequence collision, production assertion binding source, and signed HTTP middleware admission for each workload route. Keep live Dapr proof as an explicit release gate.

**Acceptance Criteria:**

- Given a valid unused link and Fresh catalog, when confirm or adjust is submitted, then success follows one committed batch with capability use and exactly one matching Time Entry effect.
- Given concurrent use, revoke, expire, or replay, when persisted histories are inspected, then each capability has at most one terminal transition and no partial Time Entry effect.
- Given legacy terminal history or invalid authority, when any public route runs, then it cannot reopen the link or expose a reason, token, or protected detail.
- Given no deployed EventStore access or runnable default topology, when reporting verification, then local persisted proof is distinguished from Story 3.6 live acceptance and release remains blocked.

## Implementation Notes

Kubernetes and GitHub show no Timesheets deployment. Azure login is unavailable; AppHost declares no EventStore resource. Deployed history remains unknown, so release inventory stays open.

## Spec Change Log

- 2026-10-10, review loop 1: BH1/BH2/BH7, EC1/EC2, and O1 showed that the first implementation could deadlock its owner actor, trust serialized authority, bypass token presentation through a direct internal command, skip wrapped effects in read models, and submit through a human-only EventStore endpoint without credentials. Revised the Code Map and tasks to require a workload-authenticated platform command/status/read channel, gateway-stamped origin, owner-state-only actor processing, and full read-model normalization. The known-bad state was a green in-process gateway fixture while the real EventStore path could not run safely. KEEP: internal command and payload types in Server; one actor-owned use/effect batch; legacy terminal folding; opaque denials; Completed plus read-back success proof; distinct independent request IDs; actor commit/reload tests; and explicit open deployment gates.
- 2026-10-10, review loop 2: L1-EC1 and L1-BH6 found proposed-entry revocation blocked by a missing recorded owner and expiry checked against the earlier HTTP timestamp. L1-BH8/BH9/BH11/BH13 and L1-VG3 found incomplete readback/ambiguity proof and missing actor or middleware verification. Amended Code Map and tasks for terminal-only owner states, a processing clock, complete knowable receipt comparisons, cross-type sequence conflicts, and HTTP authorization tests. The known-bad state was a passing in-process suite that could still issue an unusable capability, use an expired link after queue delay, or miss a broken middleware policy. KEEP: the loop-1 workload submit/status/read channel with signed tenant/domain/actor bindings; gateway-stamped origin; no owner-actor callback; atomic use/effect batches; local confirm/adjust actor recreation; opaque unknown outcomes; fail-closed authority-mode issuer; all prior legacy folds and projection normalization; and explicit deployment/history/legacy-writer release gates. The loop-2 code backup is `/tmp/timesheets-story-3-6-before-loop2.patch` for selective reuse only.

## Review Triage Log

| Finding | Verdict | Verified evidence and route |
| --- | --- | --- |
| BH1 — owner-stream read during processing | high | `LoadCapabilityForTenantAsync` reads the target stream; EventStore `StreamsController` calls that same aggregate actor, whose reentrancy is disabled. Route: bad_spec. |
| BH2 — actor supplied by internal intent | high | Gateway submission derives `CommandEnvelope.UserId` from the JWT subject, but issue and transition processing authorize the separate `Issuer`/`Actor` fields in the payload. A permitted submitter can assert another actor. Route: bad_spec. |
| BH3 — issue without existing Time Entry | false | The cited test issues a `ProposedTimeEntry` capability; that target is intentionally absent before capture. Existing issuance rules allow it, and use-time validation checks the target. Rejected. |
| BH4 — null nested intent members | medium | JSON can deserialize nonnullable record members as null; the processor dereferences `Tenant` and identifiers outside its deserialization catch, producing an exception rather than a rejection. Route: patch. |
| BH5 — separate HTTP retry gets a new message ID | false | Independent HTTP requests must have distinct IDs so the actor can reject a concurrent loser. Status retries within one submission retain the same ID; a later request is a replay and must receive opaque denial. Rejected. |
| BH6 — short status-poll window | false | The spec requires unknown commit outcomes to fail closed; a nonterminal status after the bounded wait returns the opaque denial. No success is falsely reported. Rejected. |
| BH7 — wrapped Time Entry effects absent from read projections | high | Evidence, list, period, and approved-ledger folds recognize domain events, while the new stream contains `StoredTimeEntryConfirmed`/`StoredTimeEntryAdjusted`; those effects are skipped. Route: bad_spec. |
| BH8 — capability status projection has no live handler | medium | The helper is invoked only in tests and has no discovered projection handler; this wiring gap predates this increment. Route: defer. |
| BH9 — cross-tenant terminal event in capability helper | medium | The helper compares capability and target IDs but never tenant, so a mixed input list can apply another tenant's terminal event. Route: patch. |
| BH10 — second terminal event hidden in capability helper | medium | Once the first terminal event changes state, the `Issued` guard ignores another terminal event, unlike the authoritative loader's ambiguity denial. Route: patch. |
| BH11 — token returned before index delivery | maybe-false | Projection delivery is asynchronous and may delay resolution; the diff does not establish a live delivery failure or a required immediate-readiness contract. Live issue-to-resolve timing and delivery guarantees would settle this. Route: defer, medium unverified. |
| EC1 — forged actor in issue/revoke | high | Same authenticated-origin defect as BH2: processor ignores the envelope identity and trusts the payload's actor fields. Route: bad_spec. |
| EC2 — direct use with stored hash | high | The processor accepts the hash in an internal command without proof that the public endpoint saw the one-time token. A submitter with internal command access and a known hash can bypass token presentation. Route: bad_spec. |
| EC3 — malformed required intent fields | medium | Same reachable null-member exception as BH4. Route: patch. |
| EC4 — legacy writer races new owner | maybe-false | Two deployed writer versions could each commit a terminal event during a rolling transition, but deployed versions and histories are unavailable; the release record requires stopping legacy writers. Inventory and deployment sequencing would settle this. Route: defer, high unverified. |
| EC5 — mismatched target terminal event in helper | medium | A terminal event with the right capability ID and wrong target is ignored, leaving the helper's model Issued rather than invalid. Route: patch. |
| EC6 — conflicting issuance in helper | medium | The helper selects the first issuance and does not reject a different later one, diverging from the authoritative loader. Route: patch. |
| VG1 — keyed `/process` dispatch untested | medium | The HTTP and actor fixtures resolve the concrete processor directly; removing keyed registrations leaves those tests green while the domain router fails. Route: patch. |
| VG2 — non-Completed status untested | medium | The fixture emits only synchronous Completed/Rejected statuses, so no HTTP test proves PublishFailed, TimedOut, or pending status yields opaque denial. Route: patch. |
| VG3 — adjusted actor replay untested | medium | Stored adjustment payloads are asserted, but actor recreation tests only confirmation; a no-op adjustment fold would pass. Route: patch. |
| O1 — gateway command transport lacks authorization | high | The configured `EventStoreGatewayClient` sends Dapr routing headers only. EventStore `CommandsController` uses a human-only JWT policy and requires a `sub` claim, so an anonymous magic-link host call cannot submit through this path. Route: bad_spec. |


| L1-BH1 — dirty EventStore gitlink | false | Review ran before step-05 commits by workflow design. The SDK and root commits/pointer are still pending, and remote publication remains a documented release gate; no completed clean checkout was claimed. Rejected as a current-diff finding. |
| L1-BH2 — authority issuer cannot bind resources | medium | `JwtWorkloadAssertionIssuer.CanBindResources` is false in authority mode, so the new route fails closed there. The issuer limitation predates this change and the frozen intent leaves live topology as a release gate. Route: defer until a trusted resource-binding issuer exists. |
| L1-BH3 — fail-closed production kernel validators | medium | `Program` still uses the fail-closed server kernel; AGENTS explicitly requires that default, and concrete adapters were already absent before this increment. Route: defer as the existing live-host gate. |
| L1-BH4 — terminal-only capability stream can be reissued | medium | Actor replay can set `IsTerminal` while `Exists` remains false, and `ProcessIssueAsync` rejects only `Exists`; the pure issue handler has the same guard. A malformed historical stream can gain an issuance and yield an unusable token. Route: patch, mooted by loopback. |
| L1-BH5 — issuance needs an existing Time Entry | false | Same claim and code situation as carried BH3: `ProposedTimeEntry` links may be issued before a Time Entry exists, and use-time checks validate the target. Rejected. |
| L1-BH6 — expiry uses HTTP timestamp during actor processing | high | `ProcessAsync` passes `intent.UsedAtUtc` to the capability decision and never checks processing time; a queued command can pass expiry after the deadline. Route: bad_spec. |
| L1-BH7 — bounded status polling loses a later receipt | false | Same location and claim as carried BH6: a bounded unknown outcome must remain opaque; a later HTTP request is a distinct replay identity. The issued-token edge is a documented consequence of fail-closed unknown outcomes, not a false success. Rejected. |
| L1-BH8 — issuance receipt omits scope/action/expiry | medium | `SubmitIssueAsync` checks identity, hash, issuer, entry, and time but not contributor, target, Activity Type, action, expiry, source, or single-use. It can acknowledge a materially different stored capability. Route: bad_spec. |
| L1-BH9 — adjustment receipt omits effect fields | medium | `VerifyCommittedBatchAsync` checks editable values and basic identity but not adjusted target, contributor category, AI metrics, scope, or consistency of previous values; a different effect can be acknowledged. Route: bad_spec. |
| L1-BH10 — transition receipt omits audit source | medium | `SubmitTransitionAsync` checks terminal identity/time but not `Source`, so it can acknowledge incorrect attribution. Route: patch, mooted by loopback. |
| L1-BH11 — projection normalizer accepts same-sequence different events | medium | The dedupe key includes event type, allowing two distinct payloads at one stream sequence through; their fold order can vary. Route: bad_spec under the required fail-closed ambiguity behavior. |
| L1-BH12 — no runtime owner-stream projection handlers | medium | The named Time Entry projection helpers had no production handlers before this increment; the new normalization is usable by local folds but live projection delivery remains unwired. Route: defer as a pre-existing wiring gap; do not claim deployed read models. |
| L1-BH13 — revoke/expire actor persistence not exercised | medium | HTTP gateway fixtures store management terminal events, but actor recreation covers only confirm/adjust. The matrix's management owner-stream write lacks actual actor-state proof. Route: bad_spec. Live Dapr acceptance remains a separate release gate. |
| L1-EC1 — proposed-entry link cannot be revoked | high | `ProcessTransitionAsync` requires `TimeEntryState.IsRecorded`; an issued proposed-entry link can have no recorded owner yet, so revoke/expire cannot persist a terminal marker. Route: bad_spec. |
| L1-EC2 — null nested issuance fields | medium | The processor checks `Scope` but not `Scope.TimeEntryId` or `Command.Source`; the pure issue handler can construct a persisted event with a null required member. Route: patch, mooted by loopback. |
| L1-EC3 — issuance readback has incomplete scope check | medium | Same root cause as L1-BH8: the receipt verifies only a subset of the issued event. Route: bad_spec. |
| L1-VG1 — stored action and expiry untested | medium | No persisted-issuance assertion checks action or expiry, so an incorrect stored event can pass the current HTTP test. Same root cause as L1-BH8; route: bad_spec. |
| L1-VG2 — production assertion source untested | medium | The HTTP fixture replaces the gateway and the client test uses a stub assertion source; no test executes the production binding source. Route: patch, mooted by loopback. |
| L1-VG3 — registered workload policies lack HTTP test | medium | Attribute reflection and direct controller invocation pass if policy registration is removed; no signed request traverses EventStore middleware for the three new routes. Route: bad_spec. |

## Design Notes

All terminal commands resolve to the Time Entry owner. The processor validates issuance and current target history. It must use actor-provided Time Entry state during processing and never call the owner actor through stream replay. Deployment must stop old capability-stream terminal writers before enabling this path; ambiguous history fails closed. `SubmitCommandAsync` acknowledges acceptance; status plus stream contents prove commit.

The internal `CommitMagicLink*` commands are in `src/Hexalith.Timesheets.Server/MagicLinks/Commands/` rather than the task's proposed public Contracts path. This keeps server-resolved tenant and token-hash fields out of public contracts while preserving the task's behavior. EventStore payload wrappers are also internal to Server.

The EventStore gateway must authenticate Timesheets as a workload for command submit, status, and stream read. A human command endpoint cannot serve anonymous magic-link decisions. The gateway must stamp verified workload origin into the envelope through a reserved, non-caller-controlled field. The Timesheets processor accepts internal commit commands only with that origin, so a direct human command cannot redeem a stored token hash or assert another actor. Existing human and protected domain-service routes retain their current policies. Missing credentials fail closed.

An issued `ProposedTimeEntry` capability may precede a recorded Time Entry. Its owner stream may therefore contain only a revoke/expire marker; the processor must still persist that marker and later prevent use. A terminal-only capability stream is corrupt and cannot accept a fresh issuance. Use-time expiry is checked with the EventStore processing clock, not solely the HTTP request instant. Readback proves the exact stored scope and audit data that the caller can know, and checks internal consistency for actor-derived correction fields. Same-sequence distinct projection payloads are contradictory even when their types differ. Production assertion creation and operation-policy middleware need executable tests; direct controller tests alone do not establish route admission.

## Verification

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore passes.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero new diagnostics.
- Run each built Timesheets test executable individually, plus persisted EventStore fixture tests — relevant lanes pass with stored-state assertions.

**Prior loop-1 local result:** Restore and warning-as-error build passed. Architecture 56/56, Contracts 92/92, Server 490/490, Projections 154/154, Integration 573 passed with four declared skips, and Works 76/76. Focused EventStore controller tests passed 71/71 and workload client transport tests passed 2/2. `git diff --check` passed in both repositories. Independent review then found the loop-2 defects above; these results do not verify the revised code.

**Prior loop-1 matrix audit:** `MagicLinkPersistedSubmissionTests` covered confirm/adjust batches, concurrent and replay use, revoke/expire including an expiry without a human actor, legacy Used/Revoked/Expired streams, and non-Completed status denials. `MagicLinkConfirmationHttpBoundaryTests` covered opaque invalid and stale-catalog responses. `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests` covered missing source, wrong scope, and read faults. `MagicLinkEventStoreActorPersistenceTests` covered protected keyed `/process` dispatch and actor recreation for both stored effects. These classes ran in the passing Server or Integration lanes; projection and EventStore workload security cases ran in their passing focused lanes. Repeat the audit against the loop-2 implementation.

**Release gate:** These are local actor state-store and in-process gateway results. No deployed Dapr EventStore or history inventory is available. The authority-mode issuer cannot sign required resource bindings and fails closed; rollout must also stop legacy capability-stream terminal writers. Story 3.6 and release readiness remain open.

**Prior attempt:** The first implementation built and passed its in-process tests, but independent review found the live-path defects in the Change Log. Those results do not verify this revised design. The in-memory actor state manager can prove local commit/reload; deployed Dapr persistence and live topology remain separate gates.
