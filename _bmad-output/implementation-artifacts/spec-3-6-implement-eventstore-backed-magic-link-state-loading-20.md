---
title: 'Connect Story 3.6 recording to live EventStore state'
type: 'feature'
created: '2026-10-10'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's loader and atomic use pass local tests, but production never records Time Entries in their EventStore owner streams. The local host lacks an EventStore resource and resource-binding issuer; deployed history is uninspected.

**Approach:** Compose recording with the existing keyed `timesheets` processor, wire the required read models, and prove persisted issue-to-use behavior at the selected Dapr target. Keep the story open until all external gates have evidence.

## Boundaries & Constraints

**Always:** Use EventStore, server-established authorization, verified workload bindings, and the Time Entry owner stream. Require `Completed` plus stored-event readback. Preserve opaque denial, the port split, and replay-safe folds.

**Never:** Add another keyed processor, direct state/projection writes, token authority, UI, new topology, or a completion claim from TestServer alone. Do not mutate deployed histories or stop legacy writers without release approval.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Record then use | Authorized capture; fresh catalog; valid link | Owner stream records entry, then one use/effect batch | Verify stored events before success |
| Retry or race | Duplicate record or concurrent use | One recording and one use after restart | Opaque loser denial |
| Unavailable | Missing binding, stale catalog, wrong tenant, failed store | No event or disclosure | Fail closed; safe diagnostics |

</frozen-after-approval>

## Open Questions

- Which acceptance target can we use? **A — local Dapr:** build and verify owned paths; keep Story 3.6 open pending production issuer, history inventory, and rollout proof. **B — deployed:** provide the EventStore/Dapr target, trusted binding issuer, read-only history access, and release owner; attempt full closure after implementation.

## Code Map

- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — sole keyed processor registration.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — routes commands; use requires `TimeEntryState.IsRecorded`.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs` — authorization and catalog validation; `TimeEntry.Handle` emits recording events.
- `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` — currently reports `202` without a durable record and supplies an unavailable catalog.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` — gateway status/readback pattern.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs` and `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` — pure folds needing production handlers; token index handler already runs.
- `src/Hexalith.Timesheets.AppHost/Program.cs` — no EventStore resource; no topology edit in this spec.
- `src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs` — fails closed without resource-binding issuer.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/Commands/CommitTimeEntryRecord.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — add an internal record intent to the existing keyed processor; validate verified origin, tenant, actor, owner state, and replay.
- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` and `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` — submit through the gateway, load trusted state/catalog, and acknowledge only matching stored `TimeEntryRecorded`.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkCapabilityProjectionHandler.cs` and `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjectionHandler.cs` — deliver stored effects to rebuildable, tenant-scoped read models.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — prove persisted record/use, restart, race, opaque denial, and end state; exercise selected Dapr target without gateway/store substitution.
- [ ] `docs/launch-readiness.md` and `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md` — record evidence and retain unproven gates.

**Acceptance Criteria:**

- Given trusted capture and a fresh catalog, when recording commits, then one scoped `TimeEntryRecorded` exists in the owner stream and success follows matching readback.
- Given a recorded entry and valid link, when confirm or adjust runs through Dapr-backed HTTP, then one use/effect batch survives restart; replay and racing requests get opaque denial.
- Given an untrusted binding, stale state, wrong tenant, or store failure, when a request runs, then no unauthorized event or protected detail escapes.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` — succeeds under current `global.json` (`10.0.401`).
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` — warning-free.
- Run affected `tests/<Project>/bin/Debug/net10.0/<Project>` executables separately — all pass.
- Run selected Dapr journey; inspect persisted state before and after restart.
