# Story 3.6 resume checkpoint — 2026-10-12

## Current v1 migration continuation

The scoped `spec-3-6-migrate-v1-magic-link-index-to-v7.md` implementation is
locally verified. The separate authenticated `migrate-v1` route reconstructs
v7 from independently verified complete issuance inventory, retaining legacy
data and ordinary fail-closed behavior. The v6 migrator is unchanged. Durable
checkpoints, pre-Begin ownership and bound EventStore witnesses cover
page/checkpoint/seal/commit interruption, reservation races and abort cleanup
after checkpoint deletion. Final promotion verifies exact staged membership
and rechecks source, receipt, authority and journal after replay.

SDK `10.0.401` source-reference solution restore/build passed with zero
warnings/errors. Post-review migration/digest checks passed 64/64 and composed
ingress passed 37/37. Three independent reviews are triaged and their patches
verified; the pre-existing source-owner continuity prerequisite is deferred.
The final six-suite run passed 1,867 tests with five expected skips and zero
failures. The scoped migration spec is done; Story 3.6 remains in progress.
Exact commands, matrix coverage and logs are in `tests/3-6-test-summary.md`.

No AppHost or live journey was started. Independently verified complete
cross-tenant coverage, real operator authority, enforced source/delivery
quiescence, authoritative owner delivery continuity/handoff and recovery of
the previously recorded four uncovered legacy journal deliveries remain
unavailable. Default authority denies and the
inventory provider is unavailable. Do not invent certification or clear
Redis. Story 3.6 and release remain **FAIL**, Story 3.6 remains in progress,
and both original live-dependent tasks remain unchecked. Existing spec-21,
AppHost, ServiceDefaults, SDK sources and gitlinks are unchanged.

## Prior continuation before v1 migration

Resume `spec-3-6-materialize-magic-link-read-models.md`; it and Story 3.6
remain in progress, with both live-dependent tasks unchecked. The current
increment strengthens the independent selected comment-policy proof and
token-free legacy-index diagnostics. SDK `10.0.401` source-reference restore
and Debug solution build passed with zero warnings/errors. Python passed
9/9, projection acceptance matrix passed 10/10, and launch-readiness fitness
passed 13/13. Exact commands and logs are in `tests/3-6-test-summary.md`.

The latest isolated Dapr journey failed 0/1 in 76.410 seconds after the
recorded Time Entry check: `delivery_state_unavailable;
legacy-direct-root=present-unverified entries=72`. V7 candidate, use/adjust,
and restart acceptance were not reached. Only the Timesheets AppHost was
stopped; Dapr and Aspire are empty and Redis was preserved.

The existing operator endpoint is `migrate-v6`; its migrator explicitly
rejects a direct or selected v1 single root even with migration authority.
A separate full v1 migration implementation and independently certified
cross-tenant owner inventory/authority are required. No concrete inventory
authority is registered here. The user does not know those inputs; do not
invent certification, clear the store, or bootstrap over legacy state.
Story 3.6/release remain **FAIL**, and build review is pending because the
live tasks have not passed. Preserve the concurrently created untracked
`spec-3-6-implement-eventstore-backed-magic-link-state-loading-21.md`; it is
outside this continuation's edits. Do not stage, commit, or alter gitlinks
merely to resume. Earlier rounds below are historical.

## Round-24 continuation

The unfinished v7 tree described below has been completed and built. The latest
source-reference solution build passed with warnings treated as errors; default
Integration passed 702 cases with five expected skips, and full Projections
passed 322/322. Focused EventStore Client epoch tests passed 20/20 and
DomainService partitioned rebuild tests passed 37/37. The two large-directory
and tenant-bounded rebuild tasks are checked in the current
`spec-3-6-materialize-magic-link-read-models.md`. The prior Projections run had
one intermittent promoted-retirement cleanup failure, which did not recur on
the passing full rerun; its underlying batch status remains unknown.

The latest isolated Dapr journey remains unverified. Two attempts before the
bounded v7 legacy-shard bootstrap change failed at `/project/v2` with
`delivery_state_unavailable`. After that change, the unrelated Folders AppHost
repeatedly reclaimed the Dapr `eventstore` ID on port 8080, so no safe Timesheets
retry ran. Do not stop Folders or race its restart. Once its owner pauses it,
preflight a stable unique `eventstore` ID and run the isolated journey; only
then update the two live-dependent unchecked spec tasks. Story 3.6 and release
remain **FAIL** because live proof and deployed inventory, authority, and shared
rebuild certification are absent. The notes below describe the earlier saved
state and are historical.

The user requested **save and stop**. All work is preserved in the unstaged working trees of Timesheets and `references/Hexalith.EventStore`. Do not commit, stage, revert, clean, or update submodules merely to resume. Continue `$bmad-build 3.6` from `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` (`review_loop_iteration: 21`). Story and release gates remain **FAIL**.

## Historical round-21 code state

- Round-21 work added SDK sparse copy-on-write generations, append/seal streamed staging, recoverable retired-value cleanup, and a v7 numeric token index with tenant roster, explicit v6 migration, and an authenticated internal operator route with default-deny migration authority.
- The latest atomic edit to `MagicLinkTokenHashCapabilityIndexTreeBuilder`, `MagicLinkTokenHashCapabilityIndexTreeReader`, `MagicLinkTokenHashCapabilityIndexTreeRoot`, `MagicLinkTokenHashCapabilityIndexTenantRoster`, and new `MagicLinkTokenHashCapabilityIndexTenantRosterPageReference` is **incomplete and unbuilt**. It begins an owner trie and page-range hints for incremental ordinary insertion. Compile, finish, and verify it before relying on this tree.
- The isolated 10,000-owner fresh-ordinary HTTP test was stopped with SIGINT after about 4.5 minutes and 13 GiB RSS (exit 58). Its confirmed cause was the prior v7 ordinary path reading, checking, sorting, and rewriting every tenant roster page on each issuance. Finish bounded touched-page insertion and direct owner lookup; rerun the measured lane.
- Integration default excluding that 10,000-owner case exited 1 because `ComposedNamedCapabilityRoutePersistsIssuedAndUsedSelectedRow` in `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` around line 812 still asserts a selected v6 shard. Update it to assert the selected v7 root and rerun.
- Spec task around line 184 (tenant-bounded global index rebuild) remains unchecked. The latest-tree isolated Dapr journey has not run: Folders currently owns an `eventstore` Dapr app ID. Leave Folders untouched and wait for its owner to pause it before a unique-ID preflight and Timesheets run.

## Verification before the incomplete edit

- Documented source-reference Timesheets solution build passed with zero warnings and errors. Direct Release solution build failed in the known Works configuration mode with `HXW0002` and missing `Hexalith.PolymorphicSerializations`; retain both exact results.
- Contracts, Server, Architecture, and Works default test executables exited 0. Projections default excluding its long 10,000-owner case exited 0.
- Focused v7 numeric index test verified all 2,048 unrelated B candidates after repeated A replacement/prune; focused v6 migration, fresh bootstrap, route security, EventStore Client copy-on-write/stream/epoch, and DomainService partitioned/fenced/dispatcher tests passed.
- `git diff --check` passed in both repositories before the incomplete edit. Re-run after completing it.
- The explicit v6 migration uses repeated full scans to select one tenant at a time, with bounded memory, durable checkpoints, post-promotion v6 retirement, authenticated internal route, and default-deny server authority. It costs O(tenant count × global v6 rows) and backpressures owner delivery during its maintenance window. Deployed certification/authority are absent, so this is code-level proof only.

## Historical round-21 next steps

1. Inspect and complete the incremental owner-trie/page-bound edit without discarding user or prior story changes. Build Server/Projections and run focused ordinary, rebuild, migration, collision, and retention tests.
2. Correct the remaining composed integration assertion, rerun the 10,000-owner ordinary HTTP lane with memory/time measurement, and finish all six Timesheets default lanes plus relevant EventStore SDK lanes.
3. Update the spec task boxes and test/launch evidence with exact latest-tree results. Stage a review diff including untracked files from both repositories, then run the three context-free `$bmad-build` review layers as the rendered step 04 requires. Previous round-20 review archives are described in the spec; do not revert code unless a reviewed bad-spec loopback requires it.
4. If the Folders owner pauses its AppHost, preflight `dapr list` for one `eventstore` ID before running the opt-in Timesheets live journey. Preserve the Story 3.6/release FAIL gate without independently certified deployed inventory, production authority, and live shared rebuild proof.

## Initial explicit v1 migration handoff (2026-10-12)

Continue `spec-3-6-migrate-v1-magic-link-index-to-v7.md`. The separately
implemented `migrate-v1` route and verified-inventory migration now exist;
the earlier statements that this implementation is missing are historical.
The new provider remains unavailable by default and the existing authority
remains default-deny. Focused warning-as-error Debug builds passed. Simulated
migration tests passed 42/42 and composed ingress tests passed 31/31; exact
commands/logs are in `tests/3-6-test-summary.md`. Parent broad verification
and independent review remain pending for this implementation handoff.

No AppHost or live journey was started, no Redis content was modified, and
no staging, commits, dependency updates or submodule-pointer changes occurred.
Before a real migration, independently installed immutable cross-tenant
issuance inventory, server-side authority, source-cut and enforced writer/
delivery quiescence evidence, and an empty pending global journal are required.
The previously observed four uncovered legacy journal deliveries require
verified recovery outside this migration; their captured identities cannot
be fabricated. Resume alone must never seed trusted empty state over legacy.
The prior failed live acceptance remains unchanged. Story 3.6 remains in
progress and Story 3.6/release remain **FAIL**.

Abort-cleanup recovery was subsequently completed through an immutable
EventStore read-model witness retaining the exact operation/receipt/source
binding after SDK checkpoint deletion. The new recreation test verifies
mismatched/unauthorized refusal, matching cleanup with a revoked lookup,
unchanged legacy selection, and successful fresh-operation persisted v7
readback. The latest focused v1 suite passed 43/43 with no skips; its Debug
build passed with zero warnings/errors. Exact commands/logs are in the
Story 3.6 test summary. No live or deployed proof was added; Story 3.6 stays
in progress and release stays **FAIL**.
