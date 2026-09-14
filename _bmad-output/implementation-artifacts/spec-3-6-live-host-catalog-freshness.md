---
title: 'Reach a Fresh Activity Type catalog in the live host'
type: 'bugfix'
created: '2026-09-14'
status: 'in-review'
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

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 — promotion lacks a wire-level stream-head signal | false | rejected | The production orchestrator reads the aggregate from sequence zero with `GetEventsAsync(0)` before named dispatch, so a creation-only request is not emitted for a stream whose current history also contains deactivation. The frozen decision also explicitly accepts local contiguous `1..N` proof because `ProjectionRequest` has no head field. |
| BH2 — an older replay can overwrite newer state for the same aggregate | false | rejected | Production delivery is serialized per aggregate and rereads the full current actor history from sequence zero; it does not admit an arbitrary older prefix as a live update. |
| BH3 — rebuild finalization can combine older contents with a newer cursor | false | rejected | EventStore suppresses live projection delivery while an operator rebuild is active, so the claimed live advance between candidate creation and finalization is not admitted. A write after finalization reads current is additionally protected by the returned ETag match. |
| BH4 — an unsupported Activity Type event can be skipped before promotion | false | rejected | Every Activity Type event currently emitted by this module is handled. The finding demonstrates no current producer or current event whose skipped semantics would make the catalog wrong; rejecting hypothetical future event types would also conflict with additive consumer tolerance. |
| BH5 — unreadable live delivery has no projection-handler test | medium | patch | `ProjectionEventReader` rejects recognized events with malformed JSON or unsupported serialization, but the changed handler suite did not exercise that failure/no-write path required by the second acceptance criterion. |
| BH6 — no HTTP test chains rejected delivery to opaque denial and no dispatch | medium | patch | Existing tests prove the projection and denial halves separately, but no concrete-loader boundary test starts with an incomplete/conflicting/unreadable `/project/v2` delivery and verifies the combined denial/no-command outcome. |
| BH7 — cursor-floor verification covers only persisted-ahead ordering | medium | patch | With only persisted `42` and candidate `7`, an implementation that always chooses persisted would pass; both orderings are required to verify `max(persisted, candidate)`. |
| BH8 — live promotion is not tested over an existing Stale catalog | medium | patch | The empty and already-Fresh arrangements do not prove an upgrade from the exact pre-change persisted `Stale` state. |
| BH9 — verification omits unrelated repository test lanes and records one broad architecture failure | low | rejected | The story verification accurately records the broad pre-existing documentation mismatch and does not claim that lane passed; changing this build's spec to conceal or absorb the explicitly excluded launch-readiness issue is forbidden. The changed projects and their consumers were built and exercised. |
| EC1 — one complete aggregate marks a wider non-Fresh catalog Fresh | false | rejected | The frozen decision explicitly redefines `Fresh` as every aggregate delivered to this host having been folded from creation, not tenant-complete inventory. The claimed consequence applies the rejected tenant-complete meaning. |
| EC2 — live data can advance during candidate creation/finalization | false | rejected | EventStore checks for an active domain rebuild before live delivery and returns without dispatch while it is active; the stated trigger is excluded by the production caller. |
| EC3 — missing freshness metadata causes an uncontrolled finalization failure | low | rejected | Rebuild candidates are created and accumulated by this handler with non-null metadata; malformed persisted/candidate state fails the internal rebuild rather than becoming loader-visible Fresh authority, so no external disclosure or unsafe write was demonstrated. |
| EC4 — pre-creation mutations can precede creation in a promoted history | false | rejected | The authoritative aggregate cannot emit lifecycle events before its creation event, and production projection delivery replays that aggregate's full EventStore history. The reviewer demonstrated only an impossible direct-handler input. |
| EC5 — a rejected delivery can leave usable Fresh data and still permit capability use | false | rejected | Rejection intentionally leaves previously proven catalog data unchanged. A token for the rejected, never-projected aggregate has no catalog item and fails validation; a token using unrelated retained authority is not invalidated by a failed sibling delivery. |
| VG1 — existing Stale catalogs are not covered by live-promotion verification | medium | patch | Pre-verified gap: no repository test delivers complete history onto an existing `Stale` catalog, so a null-or-Fresh-only promotion defect would pass. |
| VG2 — rebuild cursor selection verifies only the persisted-ahead arm | medium | patch | Pre-verified gap: no other catalog finalization test proves that a candidate cursor greater than persisted is retained. |

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
