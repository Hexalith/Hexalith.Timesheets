---
title: 'Complete EventStore-backed magic-link state loading'
type: 'bugfix'
created: '2026-09-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'eb114724a17579fe5f46c0a73b07f0c9ed6b6d6c'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 was reopened because no running projection writes the loader's token-hash index. Tests inject state, while current EventStore rejects the loader's catalog read and uses sequence paging, so valid live tokens still fail closed.

**Approach:** Add rebuild-capable EventStore handlers for the loader's index/catalog, map the SDK routes in the existing host, and adapt loading to the current SDK. Prove valid confirm/adjust HTTP journeys without directly seeding the index.

## Boundaries & Constraints

**Always:** Write read models through `IReadModelStore`/`ReadModelWritePolicy`; keep aggregate folds authoritative; derive external scope from resolved capability state; preserve opaque denials, fail-closed defaults, cancellation, replay idempotency, and privacy-safe diagnostics.

**Never:** Store/log raw tokens or protected payloads; trust external claims for link scope; write Dapr state directly; add caches, topology, hosts, UI, package upgrades, sibling edits, or redesign command persistence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid link | Issued capability, Time Entry, Fresh catalog | Concrete loader resolves/folds; GET 200 and POST Accepted | No index seeding |
| Replay/rebuild | Duplicate/reordered delivery or tenant rebuild | Deterministic candidate/catalog | Retry concurrency conflicts |
| Invalid/unavailable | Invalid scope, missing state, non-Fresh catalog, read failure | Same opaque 403; no dispatch; incomplete data is not Fresh | Propagate cancellation; otherwise fail closed |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- resolver/folder; replace unsupported catalog/domain read and continuation paging.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkTokenHashCapabilityIndex*.cs` -- reuse the safe candidate schema, fold, and reader address.
- `src/Hexalith.Timesheets.Projections/MagicLinks/` and `ActivityTypes/` -- add named async writers and deterministic shared rebuilds.
- `src/Hexalith.Timesheets.Projections/Hexalith.Timesheets.Projections.csproj` -- add Server and EventStore DomainService references.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` -- route the gateway client through the platform Dapr service-invocation handler while retaining overridable registrations.
- `src/Hexalith.Timesheets/Hexalith.Timesheets.csproj`, `Program.cs` -- reference/scan projections and map the canonical SDK routes.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` -- use resolved capability scope for external context; keep `Denied()` unchanged.
- `tests/Hexalith.Timesheets.{Server,Projections,Integration,Architecture}Tests/` -- cover persistence, paging, host discovery, valid HTTP paths, and privacy.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Projections/{MagicLinks,ActivityTypes}/` -- implement named writers/rebuild plans using current serialization and optimistic concurrency.
- [x] `src/Hexalith.Timesheets.Server/MagicLinks/` -- read the persisted catalog and page streams with exclusive `FromSequence`; reject incomplete/ambiguous state.
- [x] `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` -- configure supported EventStore service invocation without weakening DI overrides.
- [x] `src/Hexalith.Timesheets/Program.cs`, `src/Hexalith.Timesheets/Hexalith.Timesheets.csproj`, and `src/Hexalith.Timesheets.Projections/Hexalith.Timesheets.Projections.csproj` -- discover handlers and map SDK routes without AppHost/topology changes.
- [x] `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` -- use resolved external scope while preserving admin context.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/`, `tests/Hexalith.Timesheets.Server.Tests/`, `tests/Hexalith.Timesheets.IntegrationTests/`, and `tests/Hexalith.Timesheets.ArchitectureTests/` -- prove persisted shape/rebuild, paging/freshness, discovery/privacy, and real-loader valid HTTP routes.

**Acceptance Criteria:**
- Given an issued capability reaches the configured named projection route, when live delivery or rebuild completes, then the exact loader-visible index contains only token hash, tenant reference, and capability ID and is deterministic/idempotent.
- Given supported authoritative streams and a Fresh catalog, when valid confirm or adjust tokens reach all four HTTP routes, then the concrete loader resolves and folds state and the existing success outcomes occur without scripted loader/index seeding.
- Given paging, replay, duplicate delivery, or any non-Fresh/unavailable authority, when state is loaded, then incomplete data is never treated as Fresh and every non-cancellation failure remains opaque and fail-closed.
- Given any invalid-link category, when routes are exercised after the new wiring, then status/body/header equivalence, no-dispatch guarantees, and sensitive-data absence remain unchanged.

## Implementation Notes

- Added discoverable named handlers for the safe global token-hash candidate index and tenant-addressed Activity Type catalog. Both use the platform read-model write policy; shared rebuild candidates are canonical and retain no protected capability payload.
- Replaced the unsupported domain-wide catalog stream read with the persisted catalog, changed aggregate reads to exclusive sequence paging, and reject mismatched, incomplete, ambiguous, or regressing stream responses.
- Routed the default gateway through Dapr service invocation while preserving registered overrides. The canonical EventStore host owns shared defaults once, discovers the projection assembly, and maps the SDK routes.
- External link authority now comes from the authoritatively folded capability. The valid integration journey delivers issuance through `/project/v2`, uses the concrete loader on all four external routes, and never seeds the index directly.

## Spec Change Log

- 2026-09-13: Implemented and independently audited the approved Story 3.6 residual scope; all execution tasks and matrix rows verified.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| VG1 — fixtures use short event type names only | medium | patch | Production EventStore stream records carry fully qualified type names, while all concrete Timesheets fixtures use `Type.Name`; this leaves the production deserialization path unproved. |
| VG2 — valid HTTP claims match the capability | medium | patch | The valid concrete-loader journey injects the same tenant and contributor claims as the capability, so a regression back to claim-derived external scope could pass. |
| VG3 — default Dapr gateway transport is unverified | medium | patch | Registration tests prove only override preservation and do not observe the default base address, `dapr-app-id`, or API-token headers. |
| VG4 — catalog live delivery bypasses `/project/v2` | medium | patch | The integration fixture calls the catalog handler directly before its shared rebuild, so host discovery and dispatch of that handler are not exercised. |
| VG5 — sequence gaps lack projection and loader coverage | medium | patch | Neither suite supplies a history with a missing sequence even though both readers promise to reject incomplete history. |
| VG6 — live token-hash collision lacks coverage | medium | patch | The live handler's conflict branch is security-relevant but has no test proving failure and preservation of the first candidate. |
| VG7 — catalog merge lacks a sibling-aggregate preservation assertion | medium | patch | The catalog live-delivery test contains one aggregate only, so replacement of the rest of the tenant catalog would pass unnoticed. |
| VG8 — malformed Fresh catalogs lack coverage | medium | patch | Loader tests cover non-Fresh catalogs, but not malformed entries marked Fresh; those inputs must still become Unavailable. |
| BH1 — gateway requests always receive 401 without bearer auth | false | rejected | EventStore deliberately authenticates Dapr service invocations through `dapr-caller-app-id` and an allow-list, issuing a system principal; `AddEventStoreDaprServiceInvocation` is the intended client path, so a forwarded bearer token is not required. |
| BH2 — a live hash collision leaves the first candidate resolvable | low | rejected | The behavior is real only after two independent opaque tokens produce the same SHA-256 hash; persistent ambiguity would require a nontrivial index redesign and is not justified for this negligible everyday likelihood. |
| BH3 — cross-tenant collision evidence is lost across tenant rebuild order | low | rejected | This also requires a SHA-256 collision before rebuild ordering matters, and retaining global ambiguity across tenant-scoped rebuilds is a nontrivial persistent-state redesign. |
| BH4 — an older full history can regress a newer live catalog aggregate | false | rejected | The EventStore orchestrator reads the actor's full current history, serializes delivery per aggregate, and gates named delivery with durable head-sequence admission/checkpoints; the alleged stale-prefix delivery is not admitted by the production caller. |
| BH5 — no automatic catalog bootstrap can ever produce Fresh state | false | rejected | The host maps the shared-rebuild protocol and the catalog handler finalizes a Fresh tenant snapshot; the approved acceptance explicitly supplies a Fresh catalog after live delivery or rebuild rather than requiring an automatic startup rebuild. |
| BH6 — duplicate-sequence equivalence ignores conflicting metadata | medium | patch | `ProjectionEventReader` currently treats payload/type equality as sufficient while deriving the catalog cursor from the unnormalized input, so conflicting global positions or message identity can affect published state. |
| BH7 — one message ID at different sequences is silently accepted by projections | medium | patch | Unlike the loader, `ProjectionEventReader.Normalize` has no cross-sequence message-ID uniqueness check, allowing one persisted identity to represent two state transitions. |
| BH8 — repeated issuance or lifecycle-before-issuance corrupts the index | false | rejected | The index intentionally extracts one distinct authoritative issuance value only; identical replays map to the same candidate and lifecycle events are irrelevant to this non-authoritative lookup projection. |
| BH9 — foreign namespaces ending in a known event name are accepted | medium | patch | The loader's suffix match accepts an unrelated fully qualified type name and then deserializes it as a Timesheets event, contrary to fail-closed recognized-type handling. |
| BH10 — incomplete page metadata can be accepted as complete | medium | patch | A nonempty page may omit `LastSequenceReturned` when `LatestSequence` equals the cursor, allowing returned events to be folded without an advancing metadata proof. |
| BH11 — event payload identities are not checked against stream scope | medium | patch | Recognized capability and Time Entry events are applied without validating their tenant/aggregate identifiers, so a misrouted or corrupt event can mutate the requested fold. |
| BH12 — non-caller `OperationCanceledException` escapes opaque failure handling | medium | patch | Catch filters exclude every cancellation exception rather than only cancellation requested by the supplied token; a timeout-style cancellation from a read dependency can escape instead of failing closed. |
| BH13 — unsafe Dapr endpoint/port text can create an off-host URI | high | patch | The resolver accepts endpoint paths/userinfo and interpolates an arbitrary port string; a value such as `80@host` parses with a remote host and could receive the Dapr API token. |
| BH14 — integration is not a deployed Dapr/EventStore end-to-end test | false | rejected | The approved story requires an in-process concrete-loader HTTP journey without index seeding and forbids topology changes; external sidecar deployment validation is not a claimed acceptance boundary. |
| BH15 — every concrete-loader invalid category lacks HTTP duplication | low | rejected | The concrete loader's failure categories and the HTTP boundary's opaque equivalence are independently executable and joined by a thin endpoint call; duplicating every corrupt transport fixture over HTTP is nontrivial, low-value test coupling. |
| BH16 — launch-readiness still describes the pre-story gap | false | rejected | The readiness record is intentionally reconciled by reopened Story 5.1 after Story 3.6 is accepted; while this story remains in review, its existing gap statement is not a completed-state claim. |
| BH17 — sprint status differs from spec review status | false | rejected | This is the workflow's deliberate transient state: the spec enters `in-review` before patching, and sprint status is synchronized to `review` only in the presentation step. |
| BH18 — the spec's `dotnet test` verification command is not reproducible | low | rejected | The current .NET 10/MTP runner does fail before discovery and repository guidance supplies built test executables, but the only proposed correction edits this build's spec, which review rules reject. |
| BH19 — projection handler contains multiple nested types | low | patch | Three nested records/classes violate the repository's one-C#-type-per-file rule and will evade file-oriented ownership and architecture checks; extraction is a direct private-to-internal correction. |
| EC1 — nonempty page with null last sequence is accepted | medium | patch | This is the same malformed metadata defect as BH10: event data can be consumed without a reported exclusive-cursor advance. |
| EC2 — an unsolicited `ToSequence` bound is accepted | medium | patch | Loader requests are unbounded, but response metadata can report a bound and thereby present a partial prefix as complete; scope validation does not reject it. |
| EC3 — a Fresh catalog with a blank label is accepted | low | patch | Catalog validation checks scope and identifier but not the required display label, allowing an unusable Fresh item through; adding the missing shape check is direct. |
| EC4 — live collision preserves the original candidate | low | rejected | This duplicates BH2 and depends on a negligible SHA-256 collision; making collision ambiguity durable would add substantial state complexity. |
| EC5 — malicious Dapr port text can exfiltrate the API token | high | patch | This duplicates BH13 and is directly reachable from environment configuration because URI userinfo syntax changes the parsed host. |
| EC6 — a later tenant rebuild can re-add a globally ambiguous hash | low | rejected | This duplicates BH3, depends first on a cryptographic collision, and needs a cross-tenant tombstone/ambiguity model rather than a direct correction. |
| EC7 — recognized JSON `null` is ignored and can leave a link reusable | medium | patch | `JsonSerializer.Deserialize` may return null for a recognized lifecycle type and the switch then performs no transition, violating malformed-recognized-event fail-closed behavior. |

## Design Notes

The index stores candidates only; use/revoke/expiry stays authoritative in the capability fold. Rebuild replaces only the target tenant's lookup slice, while catalog state stays tenant-addressed with explicit freshness.

## Verification

Run with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`:

- `dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- succeeds under SDK 10.0.401.
- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- zero warnings/errors.
- Run Server, Projections, Integration, and Architecture test projects individually with `dotnet test <project> --no-build` -- all relevant tests pass; perf remains opt-in.
