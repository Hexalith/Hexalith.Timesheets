---
title: 'Materialize Story 3.6 magic-link read models from EventStore delivery'
type: 'feature'
created: '2026-10-11'
status: 'ready-for-dev'
route: 'dispatch'
baseline_commit: '8c042a3a1a969deeedd29f85b0b0539bcac029c5'
review_loop_iteration: 0
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
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkCapabilityProjectionView.cs` — hold issuance, pending terminal facts, and published rows in one tenant-scoped store value.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjectionHandler.cs` — merge both owner streams, retain terminal-first facts, reject contradictions, and replace/prune the shared view during rebuild.
- [ ] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionHandlerTests.cs` and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionHandlerTests.cs` — prove delivery order, replay, identity conflict, outage, freshness, live/rebuild equivalence, and pruning via the persisted store seam.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — deliver stored owner events to both routes and read back models in the opt-in journey.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `docs/launch-readiness.md`, and `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — record exact evidence and remaining gates.

**Acceptance Criteria:**

- Given stored owner histories, when SDK delivery runs, then persisted tenant-scoped models match canonical folds without granting use authority.
- Given reordered or replayed histories, when delivery and rebuild run, then both converge or fail safely without disclosing incomplete capability state.
- Given a store outage, when delivery runs, then the SDK receives a retryable failure and the last complete view remains.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Use an aggregate-owned Time Entry slot and a shared capability slot. Terminal and issuance events have independent sequence spaces. Keep terminal-first facts pending until a validated issuance; rebuild a replacement tenant view to prune removed candidates. Use the SDK epoch contract if live delivery can race promotion.

## Verification

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero errors or record the known EventStore.Contracts UniqueIds conflict.
- Build affected test projects; run their xUnit v3 executables individually — zero failures. Run opt-in Dapr with its local topology.
