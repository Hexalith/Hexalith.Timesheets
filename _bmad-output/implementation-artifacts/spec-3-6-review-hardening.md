---
title: 'Harden Story 3.6 projection and loader boundaries'
type: 'bugfix'
created: '2026-09-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '48041f44b020493a144fb866ac00c27343bed063'
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
- [x] `src/Hexalith.Timesheets.Projections/ProjectionEventReader.cs`, `ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs`, and `MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- make malformed recognized deliveries return identity conflict without writing; map uncancelled read-model failures to retryable while propagating requested cancellation.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` -- cover null events, missing/blank identifiers, non-`InvalidOperationException` get/save failures, cancellation, exact reason codes, and unchanged state for both handlers.
- [x] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- independently falsify every top-level page-scope comparison, the conflicting same-sequence branch, and catalog tenant-scope/project/uniqueness gates; assert the same opaque unavailable state.
- [x] `docs/launch-readiness.md`, `README.md`, and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- record valid magic-link resolution as implemented through canonical projection delivery without changing unrelated waivers or the overall verdict.

**Acceptance Criteria:**
- Given malformed or unavailable projection input, when either Story 3.6 handler runs, then it returns its declared failed/retryable result, performs no unsafe write, and never swallows requested cancellation.
- Given a gateway or persisted catalog violates a loader trust-boundary guard, when token state is loaded, then capability, Time Entry, and catalog details remain unavailable with no externally distinguishable failure reason.
- Given current executable evidence is compared with live documentation, when the readiness record is checked, then it no longer claims the token-hash projection is unwired or valid links cannot resolve, while unrelated `CONCERNS` remain intact.

## Implementation Notes

- Projection normalization now classifies missing collections and malformed recognized identities as delivery identity conflicts before any read-model write.
- Both live projection writers convert uncancelled read-model transport and serialization failures to retryable delivery outcomes while preserving caller cancellation.
- Loader trust boundaries now collapse invalid stream scope, ambiguous sequence content, and malformed fresh catalogs to a fully opaque unavailable token state.
- Readiness documentation records canonical projection delivery and the four-route valid HTTP journey while retaining the overall `CONCERNS` verdict and unrelated waivers.

## Spec Change Log

- 2026-09-14: Implemented Story 3.6 projection/loader hardening, added falsification coverage, and synchronized live-resolution documentation.
- 2026-09-14: Review hardened malformed envelopes, duplicate issuance detection, cancellation/state snapshots, loader falsifiability, and current release-gate evidence.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| VG1 — uncancelled store cancellation is untested | medium | patch | The changed catch maps an `OperationCanceledException` to retryable when the caller token is not cancelled, but the new theory covers only HTTP/JSON exceptions and requested cancellation. |
| BH1 — broad catches retry programming defects indefinitely | false | rejected | No reachable programming defect was demonstrated. The untyped `IReadModelStore` seam can surface transport, serialization, retry-exhaustion, and corrupt persisted-state failures through the same call; classifying that state as unavailable is the declared fail-safe outcome. |
| BH2 — retryable outcomes have no correlation-safe diagnostics | false | rejected | The handler returns the bounded `DeliveryStateUnavailable` reason through the projection protocol, and the EventStore coordinator logs tenant/domain/aggregate/projection/status/duration and maintains the retry ledger. Raw exception logging is neither needed nor privacy-safe here. |
| BH3 — a null projection-event element escapes normalization | medium | patch | `Normalize` dereferences elements in `GroupBy`; a JSON-null array element raises `NullReferenceException` before the handler's identity-conflict mapping. |
| BH4 — a null or blank event type escapes or is silently ignored | medium | patch | `Matches` calls `Split` on a wire-deserialized non-nullable property that can still be JSON null, while a blank type currently becomes an unknown event and acknowledges malformed input. |
| BH5 — page-scope falsification can pass at the missing Time Entry stage | medium | patch | The new scope test supplies only the capability stream, so removing the scope comparison still reaches the same opaque assertion through the absent downstream Time Entry. |
| BH6 — same-sequence conflict falsification can pass downstream | medium | patch | The conflict test likewise lacks valid Time Entry/catalog state, so it does not prove the ambiguity guard is the failing stage. |
| BH7 — unchanged-state tests retain the seeded object graph | low | patch | The fake store and expected value share one graph; a future in-place collection mutation could satisfy the equality assertion. Snapshotting the seed is a direct test correction. |
| BH8 — no exception-after-commit save test | false | rejected | The matrix requires a retryable acknowledgement for an outage, not proof that an ambiguous transport failure cannot have committed. Existing projection replay/idempotency tests cover safe redelivery after a write. |
| BH9 — cancellation coverage omits get/retry and uncancelled timeout paths | medium | patch | Requested cancellation is tested only immediately before save, and uncancelled `OperationCanceledException` mapping is untested; exercising both get/save boundaries directly covers the changed decision. |
| BH10 — direct-seed counters do not instrument `TrySaveAsync` | false | rejected | The valid fixture contains no direct `TrySaveAsync` seed; that method is the production projection writer and counting it would label the required `/project/v2` writes as direct seeds. The documented current journey is accurate. |
| BH11 — agent and planning guidance still describes an unwired index | medium | defer | The contradiction is real but predates this diff and is already outside the runtime/documentation files changed here; agent-context edits require synchronized managed-baseline work. |
| BH12 — release-gate test counts remain dated 2026-09-12 | medium | patch | `docs/launch-readiness.md` still presents the 800-test baseline beside newly updated Story 3.6 claims, while the 2026-09-14 run executed 859 passing tests plus four skips. |
| EC1 — null projection-event element | medium | patch | Independently confirms BH3: the collection guard does not validate individual elements before sequence grouping. |
| EC2 — identical issuance payloads at different sequences collapse | medium | patch | `FoldIssuance` calls `Distinct()` after sequence normalization, so two equal issued records at sequences 1 and 2 become one and write a candidate. |
| EC3 — duplicate issuance history violates malformed-delivery claim | medium | patch | Independently confirms EC2: aggregate sequence is discarded before record equality, masking a second issuance rather than returning identity conflict. |
| EC4 — project-scope catalog case triggers two guards | medium | patch | The `project-scope` fixture sets both `Scope.Project` and a project reference, so it cannot independently falsify the tenant-scope comparison. |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds with SDK `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: zero warnings and errors.
- Run each project under `tests/` individually with `dotnet test <project>.csproj --no-build`; if the test host is blocked, run all six built xUnit v3 executables documented in `README.md` -- expected: all default lanes pass, with only declared infrastructure/performance skips.

**Results (2026-09-14):** Restore passed; the warning-as-error solution build passed with zero warnings and errors. The six direct xUnit executables ran 876 tests: 872 passed and four declared integration lanes skipped. The final focused review evidence passed `MagicLinkStateProjectionHandlerTests` (46), `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests` (44), `LaunchReadinessTests` (11), and `MagicLinkConfirmationHttpBoundaryTests` (14), all with zero skips. `git diff --check` passed, and the isolated Aspire AppHost reached a healthy `security` resource before stopping cleanly.
