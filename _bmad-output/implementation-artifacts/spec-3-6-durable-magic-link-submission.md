---
title: 'Add durable magic-link submission with legacy-stream compatibility'
type: 'feature'
created: '2026-10-10'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` — fold legacy capability and new Time Entry terminal events without trusting the token index as authority.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs` — add terminal markers to the Time Entry fold so the target actor detects replay.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs` — reconcile cross-stream terminal status.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` — one-aggregate submit and status query.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/CommitMagicLinkUse.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — add server-resolved intent and processor; issue to capability stream, route terminal commands to Time Entry stream, recheck authority, and emit one atomic batch.
- [ ] `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` and `src/Hexalith.Timesheets/Program.cs` — register the keyed processor and trusted host submission path without changing fail-closed kernel defaults.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`, `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`, and `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs` — preserve old folds and read new terminal events from the Time Entry owner.
- [ ] `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` — return Accepted only after terminal committed status and verified event batch; unknown commit outcomes stay fail closed and retries use stable command identity.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkPersistedSubmissionTests.cs` — assert persisted effects, concurrency, replay, legacy history, and equal denials.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, and `docs/launch-readiness.md` — record local persistence evidence and outstanding deployment gates.

**Acceptance Criteria:**

- Given a valid unused link and Fresh catalog, when confirm or adjust is submitted, then success follows one committed batch with capability use and exactly one matching Time Entry effect.
- Given concurrent use, revoke, expire, or replay, when persisted histories are inspected, then each capability has at most one terminal transition and no partial Time Entry effect.
- Given legacy terminal history or invalid authority, when any public route runs, then it cannot reopen the link or expose a reason, token, or protected detail.
- Given no deployed EventStore access or runnable default topology, when reporting verification, then local persisted proof is distinguished from Story 3.6 live acceptance and release remains blocked.

## Implementation Notes

Kubernetes and GitHub show no Timesheets deployment. Azure login is unavailable; AppHost declares no EventStore resource. Deployed history remains unknown, so release inventory stays open.

## Spec Change Log

## Review Triage Log

## Design Notes

All terminal commands resolve to the Time Entry owner. The processor validates issuance and current target history. Deployment must stop old capability-stream terminal writers before enabling this path; ambiguous history fails closed. `SubmitCommandAsync` acknowledges acceptance; status plus stream contents prove commit.

## Verification

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore passes.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero new diagnostics.
- Run each built Timesheets test executable individually, plus persisted EventStore fixture tests — relevant lanes pass with stored-state assertions.
