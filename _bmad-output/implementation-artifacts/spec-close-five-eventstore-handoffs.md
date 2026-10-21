---
title: 'Close five deferred magic-link EventStore implementation handoffs'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five distinct deferred EventStore implementation handoffs: loader fold integrity and genuine cross-tenant coverage; bounded token-hash index storage; production Time Entry recording; production capability and Time Entry projections; and capability projection helper collisions. Revalidate the four already-delivered implementations and fix the helper's normalization of unrelated capabilities before selecting the requested capability. Keep selected-capability integrity checks, EventStore persistence, fail-closed authorization and opaque external responses. Resolve only these five original entries after verification, then commit and push to main as requested. Deployed acceptance and release readiness remain separate gates.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `4d3c205c940aa562304c5cb5917d761196342ae9`. The user authorizes five deferred items and publication to main. No unresolved product choice or irreversible implementation action exists. The bounded change touches one projection helper, its existing test class, this spec, verification evidence and the five ledger annotations; use the oneshot route.
- Exact ledger summaries: “Restore loader fold integrity for same-sequence events and remove the inert de-duplication path”; “Bound the cross-tenant magic-link token-hash index: prune terminal capabilities and size or shard the single key”; “Add a production EventStore recording writer for Time Entry owner streams before live magic-link acceptance”; “Wire production capability and Time Entry read-model projection handlers for stored magic-link events”; “Resolve magic-link helper collision cases before broadening production command routing”.
- `MagicLinkConfirmationCapabilityProjection.Project` unwraps and normalizes all evidence by stream kind and local sequence before selecting capability identity. Mixed unrelated streams therefore collide. Filter recognized unrelated capability events before deduplication and lifecycle ordering; preserve matching-ID tenant/Time Entry validation, identical replay tolerance and contradictory selected evidence refusal. Cover raw and stored issuances, all three terminals, both input orders, absent selection and selected contradictions. The production capability handler currently supplies a singleton issuance to this helper; direct helper tests are the relevant regression seam.
- The other four are delivered: loader `Equivalent` and normalization plus the all-route concrete-loader cross-tenant HTTP test; v7 numeric tree/page storage and 10,000-owner capacity tests; `TimeEntryRecordSubmissionService` and its keyed processor branch with actor-backed HTTP state assertions; discovered capability and Time Entry handlers with mapped delivery/rebuild tests. Index closure selects the ledger's bounded-storage alternative; automatic terminal garbage collection is not claimed.
- The isolated runtime baseline command `python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-five-eventstore-baseline` exited 1 at preflight: “A Dapr application is already running or inventory is unsafe; no AppHost started”. Existing foreign workloads were preserved. Resource-free checks cannot qualify current-graph AppHost runtime or deployed acceptance.
- Verification: matching Debug/source solution restore and warning-as-error build, focused helper regression, existing loader/HTTP/actor/projection/index evidence, and all six individual default xUnit executables. Both imported EventStore catalog and Works default are `3.126.0`, so no version override is required. Keep the spec in-progress until review and verification finish.
- Regression evidence: 19 new cases join the existing 19 helper cases. Before the fix, 13 mixed/unselected cases failed and the selected-scope refusal controls passed. After selecting capability identity before normalization, all 38 cases pass. The solution build passes with zero warnings/errors.
- Broad verification exposed a preexisting authority source-fitness false positive from the preceding management-boundary commit: the host's isolated fixture now consumes `TimesheetsRequestContext` through the existing tenant-validator interface. `AuthorityBoundaryTests` now permits that exact Development-only fixture adapter while keeping the Contracts/Client ban and other host/presentation exclusions. Runtime authorization code is unchanged. Rebuild and rerun the complete Server suite; the first failing run is retained separately and cannot count as final success.
- The configured Blind Hunter review returned four findings; all are simple corrections within the selected helper boundary and were patched. Preserve unknown payload normalization; provide Time Entry IDs on stored revoke/expire test facts; interleave unrelated collisions in selected-scope refusal cases; and repeat selected issuance/terminal deliveries in mixed-stream replay tests. The review follow-through adds an unknown-payload refusal control, for 39 focused cases total. Rerun affected verification after this correction.

## Review Triage Log

- Medium, patch: the first relevance filter also discarded unknown payloads, relaxing baseline same-sequence contradiction refusal. Restrict filtering to the four recognized capability event shapes and preserve unknown-payload normalization; add an explicit regression.
- Low, patch: stored revoke/expire fixtures lacked the Time Entry ID required by production owner delivery. Populate their authoritative selected owner before wrapping so the mixed-stream matrix uses valid stored shapes.
- Low, patch: selected tenant/owner refusal cases did not interleave unrelated collisions. Add foreign issuance and terminal events at the selected local sequences, retaining the refusal assertions.
- Low, patch: mixed-stream cases did not repeat selected stored deliveries. Repeat selected issuance/terminal events among the foreign collisions in both orders, preserving existing same-capability contradiction tests. No new public surface or deferred item is required.

## Final Verification

- The independent recheck found no concrete defects after all four review corrections. No new deferred work was added. Exactly five original ledger entries now carry dated, source-backed resolution notes; original evidence and all other ledger entries remain unchanged.
- The final warning-as-error solution build passed with zero warnings/errors. All six reviewed default executables returned zero: **2,252 passed, five intentional skips, zero failures/errors/not-run** across 2,257 tests. The final focused helper selection passed 39/39. Results, commands, scope boundaries and the initial failure/superseded-run dispositions are recorded in [verification evidence](tests/five-eventstore-handoffs.md).
- The runtime attempt remains preflight-blocked by an existing Dapr application. No AppHost or fixture infrastructure was started or stopped; no current-graph runtime or deployed acceptance is inferred. Dependencies, gitlinks, agent entry points and production authorization defaults are unchanged.
