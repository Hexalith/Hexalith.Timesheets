# Story 3.6 resume checkpoint — 2026-10-12

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
