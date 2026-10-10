---
title: 'Make Story 3.6 fail closed while durable writes remain unavailable'
type: 'bugfix'
created: '2026-10-10'
status: 'done'
baseline_commit: '61d6fc7a31544985bed5458972f81b7050b9c775'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The public confirm and adjust POST routes return `202 Accepted` for in-memory domain results although neither capability use nor the Time Entry effect is persisted. The loader also collapses every persisted non-Fresh catalog state to Unavailable, hiding the trust state needed to diagnose Story 3.6 safely.

**Approach:** Make both POST routes return the existing opaque denial until a verified durable EventStore commit path exists. Retain sanitized non-Fresh catalog status internally without retaining usable capability, Time Entry, items, cursor, timestamp, or detail. Keep valid GET display behavior. Record the remaining acceptance gaps truthfully.

## Boundaries & Constraints

**Always:** Preserve identical public denial shape and privacy protections. Keep `AddTimesheetsServerKernel` fail-closed and domain aggregates pure. Preserve EventStore as the sole persistence path. Story 3.6 stays `in-progress` and release stays `FAIL` until durable single-use, persisted concurrency, and live-host proof pass.

**Never:** Add AppHost topology, a Timesheets-local persistence mechanism, sequential best-effort writes, a fake commit receipt, or a new UI. Do not start a stream migration while deployed histories remain unverified.

**Decisions (2026-10-10):** Keep the approved no-topology boundary. Treat deployed capability/Time Entry histories as unknown and perform read-only inventory before designing a new write owner. Prefer one EventStore aggregate owner over an unqualified two-stream transaction if inventory permits; this design is deferred from the present fail-closed increment.

## I/O & Edge-Case Matrix

| Scenario | State | Behavior |
|----------|-------|----------|
| Valid token, Fresh catalog | GET display succeeds; POST reaches pure domain success | GET remains 200; POST returns shared 403 with no persisted effect |
| Invalid, used, or unknown token | Any public route | Existing shared opaque denial |
| Persisted Stale/Rebuilding/Degraded/Unknown catalog | Otherwise valid token | Sanitized internal state; null capability/Time Entry; shared public denial |
| Missing, malformed, or unreadable catalog | Otherwise valid token | Internal Unavailable; shared public denial |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs:165` — POSTs return 202 from `WasDispatched` without a writer; preserve routing and denial helper.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:124` — drops catalog status with the entire bundle; `LoadActivityTypeCatalogAsync` maps all non-Fresh to Unavailable.
- `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs` — create sanitized metadata with only the status enum, not persisted cursor, date, or detail.
- `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs:902` — currently expects Stale to become Unavailable; add complete status and malformed/read-fault matrix.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs:276` — valid POST tests expect 202 with a substituted gateway that never writes; retain valid GET contrast and assert POST opacity/no durable effect.
- `docs/launch-readiness.md:59` — already records the durable and live-topology gaps; update current evidence only.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` — sanitize well-formed non-Fresh catalogs and carry their status through `MagicLinkEndpointTokenState` with null authority; keep malformed/unreadable as Unavailable.
- [x] `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` — remove both false 202 success paths and use the same denial result for successful pure decisions; do not dispatch or persist events.
- [x] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — cover the matrix, equal public denial shape, valid GET contrast, and absence of POST writes.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — inspect available deployment evidence and EventStore stream inventory read-only; record whether histories exist or the exact missing access.
- [x] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, and `docs/launch-readiness.md` — record verification and the still-open durable/topology gates without marking Story 3.6 done.

**Acceptance Criteria:**

- Given a valid token and Fresh catalog, when the public GET and POST routes run, then GET returns the allowed display and POST returns the shared opaque denial without a persisted capability-use or Time Entry event.
- Given a persisted non-Fresh catalog, when the loader resolves an otherwise valid token, then it retains only that catalog status internally, clears usable authority and catalog data, and all public routes retain the same denial shape.
- Given absent, malformed, or unreadable catalog state, when the loader runs, then its internal status is Unavailable and no public detail or event escapes.
- Given deployed history cannot be verified from available access, when tracking is updated, then the write migration and Story 3.6 completion remain explicitly open.

## Implementation Notes

- The catalog loader now validates persisted shape, then returns a status-only catalog for each recognized non-Fresh state. Token loading clears capability and Time Entry authority before returning that status.
- The public confirm and adjust POST handlers keep their current domain checks but always emit the common opaque denial. The in-process gateway observed no submissions; successful GET displays remained available after denied POSTs.
- No deployed EventStore endpoint, read-only credentials, or tenant/aggregate scope was available for a stream inventory. Existing histories remain unknown; no migration or write-owner change was attempted.
- Source-reference Debug restore/build passed with zero warnings/errors. Focused loader and HTTP classes passed 80/80 and 462/462. The six test executables yielded 1420 pass, one pre-existing Architecture assertion failure, and four intentional skips out of 1425. See the linked test summary for exact commands and limits.

## Spec Change Log

- 2026-10-10: Implemented the approved fail-closed increment and updated the story, deferred-work ledger, README, and launch evidence. Story 3.6 and release remain open.
- 2026-10-10: Review corrected the checked-in OpenAPI response contract, made freshness assertions independent, reopened Story 3.7's valid POST criterion, and marked the live resolution gate blocked. Final verification reran all six lanes; only the pre-existing Aspire package-verdict assertion fails.
- 2026-10-10: This bounded fail-closed spec is done. Story 3.6 remains in progress under the approved durable-write, persisted-concurrency, and live-topology gates.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 OpenAPI advertises 202 | medium | patch | Both submit operations still declare 202 at the checked-in OpenAPI paths, while the handlers now always deny successfully bound requests. Remove the stale responses and pin this in a contract test. |
| BH2 Story 3.7 still claims valid POST 202 | medium | patch | Story 3.7 is marked done while AC3 and a checked task require POST success. Reopen its current tracking and record that its earlier evidence was historical. |
| BH3 Story 3.6 checked success task | medium | patch | The checked HTTP task still says valid POSTs succeed. Its current task state must reflect the suspended write path. |
| BH4 no operator-facing non-Fresh diagnosis | low | reject | The new loader retains the status in its internal return value, which is the approved increment. Emitting it in endpoint logs would add a privacy-sensitive distinction and an operational logging design beyond this change. |
| BH5 valid POSTs logged as Unknown | low | reject | Unknown is the existing safe generic boundary category; it does not assert that the token was invalid. A new validity-specific log category could disclose the distinction this denial intentionally hides. |
| BH6 service exception can produce 500 | medium | defer | Both handlers already awaited the same services before this change; an uncaught access-guard or domain-service exception can still reach the framework error path. It needs a separate decision on exceptional request handling rather than a blanket catch that hides faults. |
| BH7 HTTP test does not inspect pure result | low | reject | The HTTP denial is unconditional by design, while workflow tests assert successful pure confirm and adjust results. Instrumenting the HTTP test to inspect discarded results would mirror service tests without proving persistence. |
| BH8 concrete-loader denial lacks separate equality comparison | low | reject | The concrete-loader test uses the same route and denial helper covered by the scripted-loader byte-equivalence test; another comparison adds no distinct behavior check. |
| BH9 no concrete non-Fresh HTTP case | low | reject | Loader tests cover every retained status, and the HTTP suite covers the shared denial path. Forcing non-Fresh persistence into the projection-only fixture would add a separate seeding seam solely for this combination. |
| BH10 malformed JSON can return 400 | false | reject | The matrix's POST row requires a valid request reaching a pure domain decision. Malformed JSON fails request binding before that row applies, as the existing binding test states. |
| BH11 live-resolution classification | medium | patch | The launch row says `implemented / waived` although submit behavior is disabled and no live topology exists. Mark the live resolution gate blocked. |
| EC1 service exception can produce 500 | medium | defer | This is the same pre-existing uncaught-service-exception path as BH6; the observed denial is only guaranteed when loading and domain checks complete. |
| VG1 status test uses the production helper as oracle | medium | patch | Both assertions compare results to `StatusOnly(status)`, so a helper that collapses states could pass. Assert the input enum and null sensitive metadata fields independently. |

## Design Notes

The domain service returns event objects, not a durable receipt. Until EventStore owns an atomic single-use commit, `WasDispatched` is insufficient for HTTP success. Keep the status distinction internal; external response and logs must not reveal it. A later write story should enroll a Timesheets EventStore processor and prove the selected single-owner design against persisted history and concurrent POSTs.

## Verification

**Commands:**

- Follow `README.md` source-reference Debug restore/build and run the six test executables individually; expect no new failures.
- Run focused loader and HTTP boundary classes directly; verify identical denial responses and a passing Fresh/non-Fresh matrix.
