---
title: 'Prove Story 3.6 live magic-link loading against a direct v1 index'
type: 'bugfix'
created: '2026-10-12'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 has no passing latest-tree isolated live journey. Ordinary issuance on `magic-link-token-hash-capability-index` throws when the direct v1 key exists, and the dispatcher reports `delivery_state_unavailable`. The last isolated runs failed there.

**Approach:** Prove valid-link loading and selected effective Time Entry evidence on one latest-tree isolated journey. Ordinary delivery stays fail-closed while that v1 key exists. Apply the recorded migration decision only as a separate explicit operation. Local proof does not close deployed certification.

## Boundaries & Constraints

**Always:** Resolve the candidate through the selected index version. Compare effective duration, billable state, approval state, target, and contributor with the stored events. Keep packed lineage, token-free diagnostics, duplicate-id preflight, and the existing recording-retry delays. Cover approval, metrics, comment, and a directory candidate in the narrow Python tests. Preflight one `eventstore` and one `timesheets` id.

**Never:** Do not clear a projection store, bootstrap v7 over the direct v1 key, or treat unique-only legacy rows as use authority. Do not change AppHost or ServiceDefaults, stop another AppHost, or weaken opaque 403. Do not mark Story 3.6 or release readiness PASS. Do not add live approval, comment, or metric submission just to repeat the narrow tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Clean index | No direct v1 root and no legacy shard | First issuance bootstraps v7 and the journey reads that candidate | Replay stays an opaque denial |
| Direct v1 root | `StateKey` has a value | Ordinary issuance and v6 migration publish no v7 root | Retryable `delivery_state_unavailable` |
| Legacy v6 shard | A selected shard exists and no v7 root | Ordinary delivery stays on the shard path | Invalid manifest fails closed |
| Store outage | Legacy scan cannot read | No partial v7 root | Retryable unavailable; cancellation propagates |

</frozen-after-approval>

## Open Questions

- Direct v1 root — options: Keep fail-closed (issuance and v6 migration keep refusing `StateKey`; the journey records that failure and does not delete the key; Story 3.6 stays FAIL) / Add a separate certified migration (explicit default-deny operator action, resumable, not used by ordinary delivery or v6 migration; unique-only rows grant no use authority; the journey then runs without deleting unrelated keys; deployed gates stay FAIL)

## Code Map

- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` — `HasSelectedLegacyIndexAsync` throws for a direct or selected v1 `StateKey`, then scans 256 shards in batches of 16. Do not bypass that throw on ordinary delivery.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs` — refuses a single-root source. Copy its authority and epoch pattern into a new type only if a migration is chosen.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs` — keep `migrate-v6`. A new migration needs its own route.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs` — `LegacyIndexPreventsFreshOrdinaryV7Bootstrap` covers today's refusal.
- `scripts/magic_link_index_selection.py`, `scripts/magic_link_evidence_lineage.py`, and `scripts/local_aspire_magic_link_journey.py` — reuse `selected_candidate`, `effective_evidence_values_match`, `complete_evidence_lineage`, and `ACTOR_RATE_LIMIT_RETRY_DELAYS`. Leave the EventStore `delivery_state_unavailable` mapping.

## Tasks & Acceptance

**Execution:**

- [ ] `scripts/local_aspire_magic_link_journey.py` — compare the selected candidate and effective evidence with the stored recording and adjustment. Record one isolated latest-tree run, or the exact preflight or `delivery_state_unavailable` failure. Do not delete `StateKey`.
- [ ] `scripts/test_magic_link_index_selection.py` and `scripts/test_magic_link_evidence_lineage.py` — keep directory, approval, metrics, and comment coverage where those facts exist.
- [ ] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` — keep ordinary delivery fail-closed while `StateKey` exists. If the decision adds a migration, add a new certified type and route beside the v6 migration and prove both from `MagicLinkGlobalIndexEpochTests`.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `docs/launch-readiness.md`, and `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — record exact results and leave Story 3.6 and release readiness FAIL.

**Acceptance Criteria:**

- Given no direct v1 root and no legacy shard, when the isolated journey issues one capability, then the selected candidate matches that issuance and the evidence matches the stored recording and adjustment.
- Given the recorded direct-v1 decision, when ordinary issuance observes `StateKey`, then it does not treat that key as an empty v7 store.
- Given only local proof, when evidence is written, then Story 3.6 and release readiness remain FAIL.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Ordinary delivery has three states: a v1 `StateKey` throws, a v6 shard stays on the shard path, and no legacy state may bootstrap an empty v7 root. A certified v1 migration would be a fourth operation, not a change to those states. The earlier recording-retry 403 is the opaque rate-limit surface; later runs failed in index delivery.

## Verification

**Commands:**

- `python3 scripts/test_magic_link_index_selection.py` — expected: exit 0
- `python3 scripts/test_magic_link_evidence_lineage.py` — expected: exit 0
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --filter LegacyIndexPreventsFreshOrdinaryV7Bootstrap` — expected: pass

**Manual checks (if no CLI):**

- `dapr list` shows no foreign `eventstore` before the opt-in journey. Do not stop another AppHost or delete the v1 key. Stop only the Timesheets AppHost afterward.
