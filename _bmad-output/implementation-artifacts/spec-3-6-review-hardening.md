---
title: 'Harden Story 3.6 projection and loader boundaries'
type: 'bugfix'
created: '2026-09-14'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - '_bmad-output/implementation-artifacts/deferred-work.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's live projection and loader paths work, but malformed projection input and infrastructure failures can escape their declared fail-closed/retryable outcomes, and several trust-boundary guards are not protected by tests. Live documentation still says valid magic links cannot resolve even though the host now wires the canonical projections and the HTTP journey proves otherwise.

**Approach:** Normalize malformed Activity Type and capability projection deliveries into controlled identity-conflict results, map uncancelled read-model transport/store failures to retryable delivery outcomes, add falsification tests for loader stream and catalog guards, and align live documentation with the implemented resolution path.

## Boundaries & Constraints

**Always:** Preserve opaque magic-link denial behavior, cancellation propagation, EventStore-backed read-model writes, deterministic folds, additive tolerance for unknown event types, and zero-write behavior for rejected deliveries. Keep the overall launch verdict at `CONCERNS` for unrelated waivers.

**Never:** Change public contracts, persisted keys, token-index authority, catalog freshness semantics, AppHost/ServiceDefaults code, topology, or EventStore route authorization. Do not absorb independently deferred work such as the Projections-to-Server layering move, gateway configuration, ETag semantics, handler telemetry discovery, stronger stream-envelope equivalence, test-store fidelity, or agent/planning-artifact synchronization.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Malformed projection delivery | Null event collection or recognized payload with missing/blank identity | No read-model write | `Failed(DeliveryIdentityConflict)` |
| Read-model outage | Uncancelled transport, serialization, get, or save exception during live projection write | No successful acknowledgement | `Retryable(DeliveryStateUnavailable)` |
| Requested cancellation | Canceled operation token | No write | Cancellation propagates |
| Invalid stream page | Returned tenant, domain, or aggregate differs from the request, or one sequence has conflicting events | Loader exposes no capability, Time Entry, or catalog data | Existing opaque unavailable token state |
| Invalid Fresh catalog | Project-scoped item or duplicate Activity Type identifier | Catalog is rejected | Existing empty `Unavailable` catalog |
| Live-resolution documentation | Canonical handlers are discovered and four valid HTTP routes pass without direct seeds | Resolution is recorded as implemented | Unrelated launch waivers remain visible |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/ProjectionEventReader.cs` -- normalization/deserialization boundary; keep unknown-event tolerance while making caller-visible malformed input predictable.
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs` -- `ProjectAsync` fold and write exception mapping; validate deserialized Activity Type identities before value-object dereference.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- parallel capability issuance validation and retryable write mapping; preserve `IndexCandidateConflictException` as identity conflict.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` -- extend malformed-delivery coverage for both handlers and inject non-`InvalidOperationException` get/save failures; assert status, reason, and unchanged state.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- read-only reference for existing page-scope, same-sequence, and catalog-shape gates; production behavior should not need redesign.
- `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- reuse `WithPageTransform` and injected catalogs to pin wrong page scope, conflicting same-sequence events, project scope, and duplicate identifiers.
- `docs/launch-readiness.md`, `README.md`, and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- replace the obsolete unwired-index claim with the wired projection and no-direct-seeding HTTP evidence while retaining other waivers.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Timesheets.Projections/ProjectionEventReader.cs`, `ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs`, and `MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- make malformed recognized deliveries return identity conflict without writing; map uncancelled read-model failures to retryable while propagating requested cancellation.
- [ ] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` -- cover null events, missing/blank identifiers, non-`InvalidOperationException` get/save failures, cancellation, exact reason codes, and unchanged state for both handlers.
- [ ] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- independently falsify every top-level page-scope comparison, the conflicting same-sequence branch, and catalog tenant-scope/project/uniqueness gates; assert the same opaque unavailable state.
- [ ] `docs/launch-readiness.md`, `README.md`, and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- record valid magic-link resolution as implemented through canonical projection delivery without changing unrelated waivers or the overall verdict.

**Acceptance Criteria:**
- Given malformed or unavailable projection input, when either Story 3.6 handler runs, then it returns its declared failed/retryable result, performs no unsafe write, and never swallows requested cancellation.
- Given a gateway or persisted catalog violates a loader trust-boundary guard, when token state is loaded, then capability, Time Entry, and catalog details remain unavailable with no externally distinguishable failure reason.
- Given current executable evidence is compared with live documentation, when the readiness record is checked, then it no longer claims the token-hash projection is unwired or valid links cannot resolve, while unrelated `CONCERNS` remain intact.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds with SDK `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: zero warnings and errors.
- Run each project under `tests/` individually with `dotnet test <project>.csproj --no-build`; if the test host is blocked, run all six built xUnit v3 executables documented in `README.md` -- expected: all default lanes pass, with only declared infrastructure/performance skips.
