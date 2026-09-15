---
title: 'Restore Story 3.6 loader fold integrity'
type: 'bugfix'
created: '2026-09-15'
status: 'done'
route: 'oneshot'
review_loop_iteration: 1
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `EventStoreMagicLinkConfirmationCapabilityStateLoader` accepts same-sequence events when only their type, format, and payload agree, even if correlation-safe envelope evidence differs, and then runs a second de-duplication pass that can hide the normalization contract. The HTTP `cross-tenant` case currently returns the same scripted null as `unknown`, so it does not prove the concrete loader rejects an index candidate whose authoritative capability belongs to another tenant.

**Approach:** Make stream normalization the single fold boundary: require equivalent same-sequence events to agree across the complete `StreamReadEvent` envelope, enforce message-id uniqueness there, and fold the normalized result directly. Add falsifying server tests for equal-payload/different-envelope conflicts and an in-process HTTP journey that reaches the concrete loader with a candidate/capability tenant mismatch while preserving the existing opaque 403 and zero-dispatch behavior.

</frozen-after-approval>

## Implementation Notes

- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- extract one envelope-equivalence helper covering type, serialization format, payload bytes, metadata version, message id, correlation id, causation id, timestamp, user id, and protection metadata; remove `OrderedDistinct` and the per-fold message-id sets after normalization owns both ordering and uniqueness.
- `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- extend the same-sequence conflict proof so changing only envelope metadata fails closed; retain byte-identical duplicate acceptance and repeated-message-id rejection.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- use the projection-backed index plus concrete loader, but script authoritative capability payloads for a different tenant; compare the resulting denial with the canonical unknown-token response and assert no command dispatch or protected detail.
- Preserve endpoint response shape, EventStore/read-model seams, persisted keys, catalog freshness semantics, current AppHost topology, and sibling submodule contents.
- Verify with the SDK in `global.json` (`10.0.401`): warnings-as-errors solution build, focused Server and Integration test executables, then all test projects individually if the focused lanes pass.
- Implemented one `Equivalent` comparison over every `StreamReadEvent` member, with byte-content payload comparison that distinguishes null from empty. `EventStorePayloadProtectionMetadata` already supplies value equality, including compatibility flags.
- `ReadAllEventsAsync` remains the sole owner of ordering, same-sequence equivalence, and cross-sequence message-id uniqueness. Capability and Time Entry folds now consume that normalized array directly; the redundant per-fold sets and `OrderedDistinct` path were removed.
- The concrete-loader HTTP proof first delivers the tenant-scoped candidate through `/project/v2`, then returns an authoritative capability payload for another tenant on the requested stream. Its four denials match the unknown-token baseline, and a new trusted-work counter proves no command work ran. An initial authorization-count assertion was discarded because authorization is intentionally allowed to occur before denial on some routes and is not a dispatch proxy.
- Verification passed: restore; solution build with `-warnaserror` (0 warnings/errors); focused loader tests 58/58; focused HTTP tests 15/15; full Architecture 54/54, Contracts 88/88, Integration 104 total (100 pass, 4 intentional skips), Projections 143/143, Server 458/458, and Works 76/76. Across all projects, 919 of 923 tests passed with the four documented performance/infrastructure lanes skipped.

## Review Triage Log

- Confirmed (medium): blank or whitespace message identifiers could evade the uniqueness guard. Normalization now rejects them, with null, empty, and whitespace regression cases.
- Confirmed (medium): the repeated-message-id test lacked a valid Time Entry stream and could pass for the wrong reason. It now supplies the companion stream so removing the uniqueness guard makes the scenario succeed and falsifies the test.
- Rejected: the event-type, serialization-format, and payload conflict cases were reported as non-falsifying. Removing the corresponding equivalence comparison discards the second same-sequence envelope and folds the first valid event, so each case fails as designed.
- Rejected: exact `DateTimeOffset` offset equality was suggested. Logical instant equality matches `ProjectionEventReader` normalization and the event metadata record contract; differing offsets for the same instant are not conflicting evidence.
- Confirmed (medium): the HTTP proof did not demonstrate that the projected candidate existed or that the authoritative capability stream was read. It now asserts both conditions as well as zero trusted command work.
- Confirmed (low): protection-metadata compatibility flags were not independently exercised. Separate-instance equivalent metadata is accepted and a flag-only conflict is rejected.
- Confirmed (low): launch-readiness test totals were stale after the new cases. The inventory now records 923 total, 919 passing, and four intentional skips.
