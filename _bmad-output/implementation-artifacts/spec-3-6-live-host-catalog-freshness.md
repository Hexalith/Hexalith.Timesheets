---
title: 'Reach a Fresh Activity Type catalog in the live host'
type: 'bugfix'
created: '2026-09-14'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '19fc5b8e7cbc52616069cfcd28d3654cf04eedb4'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A deployed host can never serve a valid magic link. The loader accepts only a `Fresh` Activity Type catalog, `Merge(preserveFreshness: true)` returns `Fresh` only when the persisted model is *already* `Fresh`, and the only path that can first produce `Fresh` — `FinalizeAsync` behind `/project/rebuild/shared/v1` — has no caller anywhere in the repo. Every live delivery writes `Stale`, so every valid link fails closed.

**Approach:** Let a live `/project/v2` delivery promote the tenant catalog to `Fresh` once the handler has proven the delivered history is complete from its creation event, making the host self-healing with no operator step and no topology change. Keep `FinalizeAsync` authoritative for rebuilds and stop it publishing a cursor that moves backwards.

**Decision (2026-09-14):** `ProjectionRequest` is `(TenantId, Domain, AggregateId, Events[])` — it carries no head sequence, `IsLastPage`, or completeness flag — so decision 1's literal trigger ("the folded cursor reaches stream head") cannot be evaluated. Promotion instead rests on what the handler proves locally: a contiguous `1..N` history containing the creation event. `Fresh` therefore comes to mean "every aggregate this host has been delivered, each folded from creation" rather than "tenant-complete". This is the accepted relaxation recorded in code-review decision 1. The loader's `Fresh`-only gate is not widened. A tenant activity type that has emitted no event since this read model was created stays absent from a `Fresh` catalog, so an adjust naming it is wrongly rejected — fail-closed, and accepted here.

## Boundaries & Constraints

**Always:** Write the catalog through `IReadModelStore`/`ReadModelWritePolicy`; keep every external response opaque and fail-closed; keep folds deterministic under replay, duplicate and reordered delivery; keep the cursor monotonic; prove freshness through the mapped HTTP route, never by seeding the read model.

**Never:** Change the no-disclosure responses, the loader's accept condition, the token-hash index, or capability/Time Entry folding; add topology, an AppHost resource, a second host, a startup rebuild trigger, or UI; drive `/project/rebuild/shared/v1` from Timesheets; change the persisted state key. Leave the Projections→Server reference, the gateway config seam, `docs/launch-readiness.md` and the ETag-vs-value concurrency defect alone — separate deferred goals.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First live delivery | Empty catalog; complete history from creation event | Catalog persists `Fresh` with that aggregate's items | N/A |
| Sibling delivery | Catalog already `Fresh`; second aggregate delivered | Stays `Fresh`; both item sets present; cursor advances | N/A |
| Incomplete history | Missing sequence, no creation event, or identity conflict | Catalog unchanged | `Failed(DeliveryIdentityConflict)`; never promotes |
| Lagging rebuild | Persisted cursor above the rebuild candidate's | `Fresh` published at the higher cursor | Cursor never regresses |
| Loader read | Catalog not `Fresh`, malformed, or unreadable | Same opaque denial | Unavailable catalog; no disclosure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs` -- `Merge`'s freshness ternary (`:247-252`) is the only place live freshness is decided; `ProjectAsync` reaches it via `preserveFreshness: true` (`:74`). `FinalizeAsync` publishes `candidateModel.ProjectionFreshness.Cursor ?? "0"` (`:144-146`) without comparing the persisted cursor it overwrites. Reuse `ParseCursor` (`:265`) and the max-of-two merge (`:253-256`).
- `src/Hexalith.Timesheets.Projections/ProjectionEventReader.cs:11-37` -- `Normalize` already throws unless the history is a contiguous `1..N` run with unique message ids. That plus `FoldAggregate`'s creation-event check (handler `:190-193`) is the completeness proof to promote on. Do not change it.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:279-291` -- the `Fresh`-only gate and its `UnavailableCatalog()` fallback. Read-only; this change must not touch it.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs:221` -- `Catalog_live_delivery_is_not_fresh_until_deterministic_shared_rebuild_finishes` locks in the defect and must be replaced. `ScriptedReadModelStore` (`:385`) exposes `Get<T>`/`Set<T>` for arranging an already-`Fresh` catalog.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs:694-741` -- `ProjectValidStateAsync` dispatches the catalog over `/project/v2` (`:697`), asserts `Stale`, then builds `Fresh` in-process and hand-applies it (`:711-740`). Once delivery promotes, delete `:702-740` and assert off the HTTP dispatch. `DispatchProjectionAsync` (`:784`) is the helper; `DirectIndexSeedCount` (`:976`, asserted `:155`) is the anti-seeding pattern to mirror.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs` -- promote the merged catalog to `Fresh` when the folded history is complete, and have `FinalizeAsync` publish `max(persisted, candidate)` -- so the host reaches `Fresh` on its own and a lagging rebuild cannot rewind the cursor.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` -- replace the test at `:221` with the matrix rows: first delivery, sibling delivery onto an already-`Fresh` catalog, incomplete history, rebuild cursor floor -- the already-`Fresh` merge arm is uncovered today, so dropping it leaves every test green.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- delete the in-process rebuild block and add a catalog anti-seeding counter to `ProjectionBackedReadModelStore`, asserted beside `DirectIndexSeedCount` -- so the valid journey proves the catalog the host produces, not one the fixture manufactures.

**Acceptance Criteria:**
- Given a host whose read model has never been rebuilt, when an aggregate's complete history is delivered over `/project/v2` and a valid token is presented, then the four external routes resolve and dispatch as before, with no direct catalog or index write by the test.
- Given any incomplete, identity-conflicting, or unreadable delivery, when token state is then requested, then the external response is the existing opaque denial and no capability-use event is emitted.

## Implementation Notes

- Live promotion relies only on the handler's existing proof: `Normalize` accepts one contiguous `1..N` history and `FoldAggregate` requires the matching creation event before `Merge` can mark the catalog `Fresh`.
- Shared rebuild finalization preserves the rebuilt items while publishing the greater of the persisted and candidate cursors.
- The HTTP valid journey now obtains catalog freshness exclusively through the mapped `/project/v2` route; the fixture has no direct rebuild-plan application path and records zero direct catalog or index seeds.

## Spec Change Log

## Review Triage Log

## Design Notes

`ProjectionRequest` carries no head signal, and `ProjectionUpdateOrchestrator.cs:120-124` frames full-prefix delivery as provisional rather than a wire guarantee — so promotion must rest on what the handler proves locally, never on trusting the orchestrator. The cursor mixes `GlobalPosition` with per-aggregate `SequenceNumber` (`:194-197`; `GlobalPosition` is `0` for legacy DTOs) and so is not comparable across aggregates; it is informational only — the loader gate never reads it — so make it monotonic without unifying the two spaces.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror` -- expected: zero warnings and errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/<P>/<P>.csproj --no-build` for `Hexalith.Timesheets.Projections.Tests`, `.IntegrationTests`, `.Server.Tests`, `.ArchitectureTests` -- expected: all pass, perf lanes skipped. If VSTest sockets are blocked, run `tests/<P>/bin/Debug/net10.0/<P>` directly per `README.md:20-28`.

**Results (2026-09-14):**
- Solution build with `-warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- `dotnet test --no-build` was blocked before discovery because Microsoft.Testing.Platform no longer supports the VSTest target under the .NET 10 SDK. Direct xUnit v3 executables passed: Projections 90/90; Integration 84/84 with 4 expected performance/infrastructure skips; Server 436/436.
- Architecture broad lane: 53/54 passed. The one failure is the pre-existing package-currency mismatch between `docs/launch-readiness.md` (`CommunityToolkit.Aspire.Hosting.Dapr` `13.5.1-beta.751`) and the shared package catalog/test (`13.5.1-beta.752`); this spec explicitly excludes that document. Focused architecture evidence passed: DependencyDirection 8/8 and DiagnosticsPrivacy 12/12.
