# Test Automation Summary — Story 3.6 (EventStore-Backed Magic-Link State Loading)

> **Superseded on 2026-09-17.** The 2026-06-22 generation record below is retained as historical
> evidence only. Its framework versions, server count, and claims that HTTP/projection-host coverage
> did not exist no longer describe the repository.

## Current Verification

### Round-24 directory and tenant-bounded rebuild proof (2026-10-12)

The source-reference Projections test project built with `--no-restore
-warnaserror -p:UseHexalithProjectReferences=true
-p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q`: zero warnings
and errors. `dotnet test ... --no-build --filter
FullyQualifiedName~TenantReplacementPreservesCandidatesBeyondFourHundredGlobalPages`
exited 1 before discovery because Microsoft.Testing.Platform rejects the
VSTest target on .NET 10; verification used the built xUnit executable. The
direct test executable passed the complete
`MagicLinkNumericIndexTests` class **3/3 in 19.755 seconds**. Its large test
now drives the actual index handler's streamed tenant finalization for four
tenants of 8,700 contributions each, producing more than 400 selected global
roster pages. It repeatedly replaces and prunes A and B, restores A, verifies
unrelated C and D candidates and shared-hash ambiguity, and bounds the
large replacement operation count below three times one tenant's inventory.
`MoreThanFourHundredNumericDirectoryPagesRemainSelectable` passed **1/1 in
0.358 seconds** with 401 selected numeric pages in one shard, two tenant
identities, an integrity-checked interior page, and resolution through the
concrete loader. `OneShardTenThousandDistinctMaximumOwnersUseBoundedDirectoryPages`
passed **1/1 in 8.015 seconds**: all 10,000 selected roster contributions
match the rebuild input and each resolves to its expected unique candidate;
an interior candidate also resolves through the concrete loader.
The exact executable selections were:

```text
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class Hexalith.Timesheets.Projections.Tests.MagicLinkNumericIndexTests -noColor
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*MoreThanFourHundredNumericDirectoryPagesRemainSelectable' -noColor
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*OneShardTenThousandDistinctMaximumOwnersUseBoundedDirectoryPages' -noColor
```

The full Projections executable
`tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -stopOnFail -noColor`
exited 0: **322/322 passed in 453.930 seconds**. The earlier intermittent
SDK promoted-value retirement failure did not recur in either the focused
tenant test or the full lane; its underlying batch result remains unknown.
The SDK's direct executables passed
`-class Hexalith.EventStore.Client.Tests.Projections.SharedProjectionEpochCoordinatorTests -noColor`
**20/20 in 0.349 seconds** and
`-class Hexalith.EventStore.DomainService.Tests.DomainPartitionedSharedProjectionRebuildTests -noColor`
**37/37 in 110.861 seconds**. These cover epoch retry, promotion/cleanup,
tenant-scoped global operation identity, and partitioned rebuild behavior.
The executable paths were
`references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests`
and
`references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests`,
respectively.
The live Dapr journey remains blocked by the
unrelated Folders runtime's duplicate `eventstore` ID. Story 3.6 and release
remain `FAIL`.

### Round-23 V7 bootstrap and effective-value checks (2026-10-12)

The full default Integration lane after the saved V7 edits exited 0:
**702 passed, five expected opt-in skips out of 707, in 515.854 seconds**.
It began before the final bootstrap performance edit. A source-reference
solution build after that edit passed with `--no-restore -warnaserror`, zero
warnings/errors. Focused Projection checks passed **3/3** for fresh V7
bootstrap, direct legacy-root denial, and a legacy value in the final shard.
The first-issuance legacy scan now reads all 256 possible shards in bounded
groups of 16 concurrent reads. This addresses a plausible cause of the
earlier `/project/v2` timeout; it has not been validated in a live run.
Narrow Python V7 selection and effective-evidence tests passed **8/8**,
including an interior candidate, cross-tenant ambiguity, server Base64 path
encoding, and an adjusted comment. `py_compile` and both repositories'
`git diff --check` passed. The full default Integration rerun against the
final bootstrap edit also exited 0: **702 passed, five expected skips out of
707, in 504.340 seconds**.

The local Dapr preflight cannot safely launch Timesheets: the unrelated
Folders AppHost owned a Dapr `eventstore` ID on app port 8080, while the
Timesheets journey requires exactly one `eventstore` ID on app port 8082.
Folders then briefly disappeared and restarted within ten seconds of two
empty preflight reads, so a stable isolated run could not begin. Folders was
left untouched and no Timesheets AppHost was started. No latest-tree live candidate or
effective-value readback is claimed. Story 3.6 and release remain `FAIL`.

### Round-22 bounded V7 tenant index and latest-tree attempt (2026-10-12)

The source-reference solution build passed with `--no-restore -warnaserror`
and zero warnings/errors. A V7 copy-on-write trie now uses bounded tenant
rosters and immutable node references. Focused verification passed:
Architecture **56/56**, Contracts **92/92**, Server **543/543**, and Works
**76/76** passed in their default lanes. The concentrated 10,000-owner
rebuild passed **1/1**; normal
10,000-candidate HTTP delivery **1/1 in 35.811 seconds**; selected-generation
promotion race **1/1**; ordered insertion across equal-prefix pages **1/1**;
and the more-than-400-roster-page tenant replacement **1/1** in isolated
runs. Narrow Python index and lineage tests passed **6/6** and `py_compile`
passed. The fresh full Projections command
`tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -noColor`
exited 1: **320 passed, one failed out of 321, in 522.274 seconds**. The
only failure was the new tenant replacement test: SDK promoted-value
retirement returned non-success during `BeginAsync` for `replace-tenant-a`.
The cleanup outcome remains under investigation. The tenant-bounded rebuild
task remains unchecked until this is resolved.

A fresh diagnostic rerun with the SDK result status included in any
retirement failure passed **321/321 in 517.904 seconds** with
`-stopOnFail -noColor`; the intermittent failure did not reproduce, so its
underlying batch status is still unknown. No full default Integration lane
was run after the V7 tree edits; its opt-in live test failed as recorded
below.

The latest-tree isolated Dapr journey failed **0/1 in 38.545 seconds** and
**0/1 in 77.228 seconds** at capability-index `/project/v2` delivery with
`delivery_state_unavailable`; it did not reach V7 candidate or effective
Time Entry readback. EventStore logs showed pending named-projection retries,
500/503 responses, actor rate limiting, and a host timeout. The Timesheets
AppHost was stopped; Dapr and Aspire returned empty inventories, and Redis
was not cleared. The failed attempts are recorded in
[the local proof](../../../docs/local-magic-link-dapr-proof.md). Story 3.6,
deployed acceptance, and release remain `FAIL`.

### Round-21 selected lookup and effective-value checks (2026-10-12)

The source-reference solution built with `--no-restore -warnaserror` and zero
warnings or errors. All six default Timesheets test executables exited 0;
the Integration lane reported its five expected opt-in skips, including the
live Dapr journey. Focused global-index tests passed **9/9**, including more
than 400 numeric directory pages, all 10,000 distinct one-shard contributions,
an interior-page loader candidate, and ordered insertion across a same-hash
page boundary. The selected-generation promotion race passed **2/2** for
inline and directory lookup. Record submission receipt tests passed **14/14**
and the adjustment policy-change HTTP test passed **1/1**. Narrow Python
lineage and selected-index tests passed **5/5**; `py_compile` and both
repositories' `git diff --check` passed.

The tenant-slice global rebuild capacity task remains open. The current index
rebuild still reads every selected directory and collision page, materializes
all owners in one list, and rewrites unrelated owners' admission markers. The
SDK's `ReadGenerationAsync` reads only the requested generation's physical
key, while streamed staging writes only supplied mutations. A sparse stage
therefore makes unchanged admissions and pages disappear after promotion.
Safe bounded replacement requires an explicit generation inheritance or
immutable-page-reference contract with deletion masking, a tenant-owned
prior-contribution roster, and bounded index manifests, plus retry and
cross-tenant tests. No such contract is implemented or claimed here.

The latest-tree isolated Dapr journey remains unrun because the unrelated
Folders runtime still owns a duplicate `eventstore` Dapr ID. It was left
untouched. Story and deployed launch gates remain `FAIL`.

### Round-20 selected integrity and receipt verification (2026-10-12)

The current source-reference solution build passed with `--no-restore
-warnaserror -p:UseHexalithProjectReferences=true
-p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false`, zero warnings and
errors. All six default Timesheets test executables exited 0 individually:
ArchitectureTests, Contracts.Tests, Server.Tests, Works.Tests,
Projections.Tests, and IntegrationTests. The Integration run retained its
five declared infrastructure/performance skips. The runner's quiet reporter
did not print totals, so no new overall case count is claimed here.

Focused checks passed: global index epoch **7/7**, including concentrated
10,000-owner collision pages and 10,000 distinct maximum-length owners in
one shard; normal mapped 10,000 high-entropy candidate delivery **1/1**;
normal mapped 65 same-shard hashes and concrete directory resolution before
and after tenant rebuild **1/1**; persisted adjustment comment tampering
**3/3**; record submission receipt class **13/13**; comment policy suffix
replay **1/1**; and EventStore sessionless cross-tenant global Abort **1/1**.
The narrow Python effective-value and packed-lineage suite passed **3/3**,
`py_compile` passed, and both Timesheets and EventStore `git diff --check`
passed. The selected index now verifies complete directory/collision page
manifests and content digests; persisted comments are compared to an
independently issued server decision and recording event IDs must be nonblank
and distinct. The policy toggle requires full owner replay before an enabled
version can be selected.

The latest-tree isolated Dapr journey has not run yet. Preflight found an
unrelated Folders Aspire runtime with its own Dapr `eventstore` ID. The
journey requires a unique `eventstore` ID; that runtime was left untouched.
No local Redis projection state was cleared. Deployed inventory, authority,
and certified shared rebuild remain open; Story 3.6 and release remain
`FAIL`.

### Round-19 five-task implementation (2026-10-12)

The restored round-18 tree plus the round-19 changes built through the
source-reference Debug solution with `--no-restore -warnaserror
-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons
-m:1 /nr:false`: zero warnings and errors. All six default Timesheets
executables passed: Architecture **56/56**, Contracts **92/92**, Server
**535/535**, Works **76/76**, Projections **307/307** in 448.086 seconds,
and Integration **693 passed, five declared skips** out of 698 in 470.414
seconds. Overall: **1,759 passed, five skipped, zero failed**. The final
build and targeted composed owner-duplicate route passed after the last
owner-admission change. Both repositories pass `git diff --check`.

The one-shard capacity test staged, promoted, and read back 10,000 distinct
128-character hashes with maximum-length owner IDs. Numeric directory pages
kept every canonical staged value below 1 MiB and returned the exact selected
candidate; the earlier 10,000-owner concentrated same-hash page case also
passes. A composed route test covers a later issuance for an owner on an
earlier page and collision ambiguity. HTTP tests cover required redaction
despite positive server authority and an explicitly cleared replacement
comment through a second link. EventStore global-epoch cross-tenant tests
passed **3/3**, including the same operation ID and empty inventory case.
Python syntax and evidence-lineage tests passed **2/2**.

The first latest-tree Dapr attempt failed **0/1 in 68.606 seconds** when
the second recording stayed 403; a restart attempt failed **0/1 in 40.834
seconds** on a 503 negative owner read, and a five-second diagnostic retry
failed **0/1 in 19.560 seconds** at the same read. EventStore warnings showed anonymous
actor 429 limits and pending projection retries; the owner-read 503 was
backed by actor 429 exceptions. The fixture now waits across the configured
60-second anonymous tenant rate-limit window for recording and actor reads,
without changing authorization. The final isolated journey passed **1/1 in
106.313 seconds** with one `eventstore` and one `timesheets` Dapr ID. It
verified persisted confirm/adjust, race, restart, and selected readbacks.
The Timesheets AppHost was stopped afterward; Dapr and Aspire were empty.
The round-18 user-requested repeat failure is retained in the local proof.
Deployed owner inventory, installation authority, and a certified live shared
rebuild remain open. Story 3.6 and release remain `FAIL`.

### Round-18 implementation (2026-10-12)

Before the numeric page-address correction, the source-reference solution build with `--no-restore -warnaserror` passed
with zero warnings and errors. Timesheets Architecture 56/56, Contracts
92/92, Server 533/533, Works 76/76, and Projections 305/305 passed. Full
Integration ran 696 cases: 691 passed, five expected skips, zero failed.
The full Projections run took 360.472 seconds and the full Integration run
399.597 seconds; focused checks after their final related edits passed.

The new 10,000-owner same-hash capacity test passed SDK staging, promotion,
all selected-page readbacks, and tenant-slice prune while preserving an
unrelated hash. The global-index class passed 4/4, the new HTTP
server-issued display theory 2/2, and the state-loader class 88/88. Positive
external-record and magic-link-adjustment HTTP cases read back exact
server-stamped comments and accepted responses. EventStore's changed
idempotency class passed 27/27; a mapped coordinator test now accepts the
9,999-to-10,000 transition and rejects event 10,001 before route dispatch
(1/1 after a zero-warning test-project build). Both repositories pass
`git diff --check`.

The selected-page address was then corrected to use a fixed shard ordinal
and allocated numeric page ID; the hash-to-page manifest and page hash
binding remain inside private index values. Old hash-derived v3 page
manifests have no numeric ID map and fail closed. The affected Integration,
Projections, and Server test projects each built with `-warnaserror` and
zero warnings/errors. Focused global-index tests passed 4/4, the composed
normal route with two paged hashes in one shard passed 1/1, and the loader
class passed 89/89. The 10,000-owner concentrated test passed 1/1 after
checking that a stage retry leaves the previous selection intact until
commit. The broad lanes above were not repeated after this focused fix.

Selected legacy v2 index rows containing only a unique owner remain
unverified and fail closed. The committed baseline had no v2 selected shard,
and the current tenant-scoped capture cannot prove the absence of hidden
cross-tenant collisions. The isolated round-18 Dapr journey passed **1/1
in 118.195 seconds** with one app ID per resource, persisted
confirm/adjust, race, restart, and selected readbacks. Dapr and Aspire were
empty after stopping the AppHost. A subsequent warning-as-error
source-reference build succeeded without changing the live host binary
hashes. No deployed source inventory/authority certification is claimed;
Story 3.6 and release remain `FAIL` pending those gates.

### Round-17 seven-task implementation (2026-10-12)

The restored round-16 code plus the round-17 fixes build from source references with `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 -nodeReuse:false -p:UseSharedCompilation=false -warnaserror`: zero warnings and errors. Full Timesheets lanes passed on the round-17 implementation for Architecture 56/56, Contracts 92/92, Server 530/530, Works 76/76, and Projections 304/304. Subsequent narrow fixture and validation edits passed their focused classes, without repeating the six-minute Projections lane. The full Integration run executed 694 cases: 687 passed, five declared skips, and two E2E assertions compared raw caller comments against newly server-stamped committed comments. After correcting those assertions, both pass in a focused fresh-build rerun, together with the new coordinator failed-commit/restart case (3/3). Normal mapped index poison, cross-tenant collision, and composed restart cases pass in focused runs. Both repositories pass `git diff --check`; the OpenAPI JSON parses.

The new regressions cover persisted ambiguity across tenant slices and prune, fail-closed legacy unique-only index rows, poison-event parking through `/project/v2`, server-owned comment decisions for external recording, magic-link adjustment, and correction, equal-lineage policy migration and removal, prior-tenant scratch cleanup, old MessageId reuse outside the receipt tail, duplicate domain metadata, and exact Time Entry row/event/payload lineage after a failed coordinator commit and host restart. The selected Time Entry policy defaults to no text publication; historical events without a server decision remain undisclosed. Independently certified deployed owner inventory and authority remain release gates.

After that in-process verification, the isolated round-17 Dapr journey passed **1/1 in 127.469 seconds** with exactly one `eventstore` and one `timesheets` app ID. It checked persisted confirm/adjust, race, EventStore restart, and selected readbacks. The Timesheets AppHost was stopped; `dapr list` and `aspire ps --format Json` were empty afterward. This local run does not certify deployed coverage, authority, or a live shared rebuild.

The full EventStore Client run reported 1,488/1,489 passing; its `SharedProjectionChunkedStageTests.TenThousandAggregateManifestPromotesOneGenerationAndSealsChunks` cleanup-timing assertion passed 1/1 when isolated. The DomainService full run reported 752/753 passing; its only failure is `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers`, which requires an absent nested Tenants checkout. Repository instructions prohibit initializing that nested submodule. Focused tests for the changed SDK paths passed. These full-lane failures do not establish a regression in the round-17 paths.

The EventStore Server full run reported 4,428 passed, one failed, and 25 declared skips out of 4,454. Its sole failure, `ProjectionDeliveryIdempotencyCoordinatorTests.AnchoredSuffixAdvancesDurablePrefixWithoutReplayingPriorEvents`, seeded an old completed-state fixture without the new full-prefix MessageId digest, so the coordinator correctly withheld incremental admission pending a full replay. The fixture now includes the digest, and the test class passes 26/26 after a zero-warning Server.Tests build. A separate assertion pins the old fixture's fail-closed full-replay fallback. The ten-minute full Server lane was not repeated after this focused repair.

### Round-16 global index and incremental mapped delivery (2026-10-12)

The source-reference solution built with `--no-restore -warnaserror
-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons
-m:1 /nr:false`, zero warnings and zero errors. All six default executables
passed on the current tree: Architecture 56/56, Contracts 92/92, Server
523/523, Works 76/76, Projections 301/301, and Integration 687 passed with
five expected skips out of 692. Overall: **1,735 passed, five skipped, zero
failed**. Both repositories pass `git diff --check`.

Focused SDK and composed-host tests cover the 256-shard selected global
index, exact 10,000 high-entropy candidates through normal `/project/v2`
delivery, cross-tenant collision and tenant-slice prune, versioned source
identity, the 9,999→10,000 mapped suffix with bounded source/HTTP calls,
failed commit and restart, and coordinator-produced stage through the
authenticated host. The actor-grounded composed producer passed both two
and 10,000-owner cases in 359.474 seconds with a longer TestHost client
timeout. This is test coverage, not deployed source certification.

The first isolated local Dapr run failed **0/1 in 46.255 seconds** because
the fixture still read the retired single index key. After it was changed
to read the selected global shard, the rerun passed **1/1 in 116.727
seconds** with one `eventstore` and one `timesheets` Dapr ID, persisted
confirm/adjust, the one-winner race, restart, and exact selected readbacks.
Dapr and Aspire were empty after stopping this AppHost. This does not certify
deployed source coverage, authority, or a live shared rebuild. Story 3.6
and release remain `FAIL`.

### Historical round-15 bounded mapped delivery and replay (2026-10-12)

The source-reference solution built with `-warnaserror`, zero warnings, and
zero errors. Six default executables passed: Architecture 56/56, Contracts
92/92, Server 523/523, Works 76/76, Projections 293/293, and Integration
682 passed with five expected skips out of 687. Overall: **1,722 passed,
five skipped, zero failed**. The Architecture lane first found two stale
readiness prose assertions; those assertions were corrected, rebuilt with
zero warnings, and the lane then passed 56/56.

Focused EventStore coordinator 28/28, retry worker 10/10, orchestrator
79/79, catalog 8/8, DomainService host 89/89, and part-route authorization
14/14 passed. A real actor-backed ordinary source path delivered over 64 MiB
without the legacy whole-history array; a composed Timesheets host covered
10,000-event mapped stage/commit/verify/readback, all three named routes,
partial failure, and durable retry. A focused Dapr selected-read outage
regression passed after the broad suites.

The isolated local Dapr journey passed **1/1 in 112.376 seconds** with one
`eventstore` and one `timesheets` app ID, persisted confirm and adjust, the
one-winner race, restart, and exact selected readbacks. Dapr and Aspire were
empty after stopping the AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records commands and limits. This does not certify deployed coverage,
authority, or a live shared rebuild. Story 3.6 and release remain `FAIL`.

### Historical round-14 source recovery and exact selected evidence (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed on the round-14
tree: Architecture 56/56, Contracts 92/92, Server 523/523, Works 76/76,
Projections 274/274, and Integration 682 passed with five expected skips out
of 687. Overall: **1,703 passed, five skipped, zero failed**. After the
readiness table was refreshed, the affected Architecture fitness class was
rebuilt with `-warnaserror` and passed 13/13. Both repositories pass
`git diff --check`.

EventStore's partitioned rebuild class passed 32/32, source producer 17/17,
and roster actor 7/7. The actor-grounded Timesheets integration passed 2/2,
including a 10,000-owner case. These cover pre-session Begin-page reclaim,
SDK phase receipts and sealed rejection recovery, and bounded producer owner
metadata. Focused Time Entry, capability selected-row, issue readback,
confirm/adjust command correlation, configured actor-name, OpenAPI, and
Python exact lineage checks passed.

The opt-in local Dapr journey passed **1/1 in 67.664 seconds** on this tree.
Its first attempt failed 0/1 in 117.700 seconds on a transient transport
failure in the added post-restart lineage stream probe; the bounded read-only
retry was then added. The passing rerun verified persisted confirm/adjust,
the race, EventStore restart, exact selected Time Entry lineage, and
capability readbacks. Strict pre/post checks each found one `eventstore` and
one `timesheets` Dapr ID; both Dapr and Aspire were empty after stopping the
Timesheets AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records the command. Deployed source coverage, authority, selected readback,
and a certified live shared rebuild remain open. Story 3.6 and release stay
`FAIL`.

### Historical round-13 paged Begin and selected repair (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed on the round-13
tree: Architecture 56/56, Contracts 92/92, Server 523/523, Works 76/76,
Projections 267/267, and Integration 679 passed with five expected skips out
of 684. Overall: **1,693 passed, five skipped, zero failed**. Both
repositories pass `git diff --check`. Focused EventStore Client, Server,
Contracts, and DomainService regressions passed, including the paged 10,000
maximum-length erased-owner Begin, 1,001 colliding source owners, reserved
capture admission under pending writes, and nonzero global-position JSON.
The partitioned DomainService class passed 31/31. The Projections lane covers
legacy writer lineage migration, corrupted selected capability repair with a
pending valid journal event, and fragment-read promotion races. Integration
covers a lost issue response and exact stored command identity.
After refreshing the launch-readiness evidence, the affected Architecture
fitness class was rebuilt with `-warnaserror` and passed 13/13.

The opt-in local Dapr journey passed **1/1 in 125.174 seconds** on this tree.
It verified persisted confirm/adjust, the one-winner race, EventStore restart,
and selected Time Entry and capability readbacks. Strict pre/post checks each
found one `eventstore` and one `timesheets` Dapr app ID; both Dapr and Aspire
were empty after stopping this Timesheets AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records the command. This does not certify deployed source coverage, a live
shared rebuild, or deployed authority; Story 3.6 and release remain open.

### Historical round-12 bounded source rebuild (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed: Architecture
56/56, Contracts 92/92, Server 523/523, Works 76/76, Projections 264/264,
and Integration 675 passed with five expected skips out of 680. Overall:
**1,686 passed, five skipped, zero failed**. The full Integration run preceded
the final EventStore hash-page edit; final-tree composed near-limit
commit/readback, cursor-tamper, and scratch-cleanup tests passed afterward.
Both repositories pass `git diff --check`.

The EventStore partitioned DomainService class passed 30/30; later focused
hash-page tamper, 30,008-chunk fenced-manifest, page-boundary commit cleanup,
and near-limit abort/retry checks passed. The source producer/gateway class
passed 16/16. A 10,000-owner test streamed three cryptographic event payloads
per owner with one active history reader at peak. A 10,000-event owner test
split two 1,000,000-byte random events into separately bounded serialized
requests. A 64 × near-1-MiB random-event case completed, committed, and read
back the last payload hash. Timesheets streamed 10,000 terminal contributions
without retaining an owner-wide view map; 4,000 contributions matched the
existing fold byte-for-byte. A composed chunked issuance and Time Entry
terminal owner rebuilt to the selected Used row. The maximum modeled 30,008
output chunks use paged hashes, and the owner manifest passed the actual
1-MiB fenced SDK batch gate. Commit and abort cleanup reclaimed pages and
receipts. Production installation authority still defaults deny.

The passing local Dapr journey below is round-11 evidence. The round-12 tree
has no new live Dapr run or certified shared-rebuild coverage; Story 3.6 and
the release decision remain open.

### Historical round-11 isolated magic-link Dapr journey (2026-10-11)

The opt-in `MagicLinkLiveDaprTests` executable invocation passed **1/1 in
114.866 seconds** with `TIMESHEETS_LIVE_DAPR=1`. Strict `dapr list` checks
before and after the journey each found one `eventstore` and one `timesheets`
app ID; after `aspire stop`, Dapr and Aspire lists were empty. The fixture
checked real persisted global-position continuity, both persisted confirm
and adjust batches, one-winner confirm race, restart, and exact selected Time
Entry and capability readbacks. The exact command and earlier failed attempts
are in [the local Dapr proof](../../../docs/local-magic-link-dapr-proof.md).

Two preceding isolated current-tree retries failed `[403,403]` at the confirm
race. The first had stored recording, use, confirmation, and competing commit
rejection: a delayed/lost POST response prevented the committed winner's 202.
Concurrent exact-status polling plus stored-event readback fixed that case;
four focused HTTP boundary tests and ten submission-service tests passed.
The second had only the recording event: signed workload stream reads received
429 and exhausted the transport timeout before submission. EventStore's
limiter treated DaprInternal workload calls as anonymous. The finite limiter
now keys them by the verified bound tenant and workload identity; its
configured-limiter regression passed 1/1. XML comments were checked on all
added public and internal source declarations in the modified and new
Timesheets and EventStore files; missing comments on four internal helpers
were added. The affected Timesheets Integration,
Server.Tests, and host builds and the EventStore host, Server.Tests, and root
AppHost builds passed with zero warnings/errors. The EventStore host rebuilt
after the comment edits with zero warnings/errors. A broader EventStore
IntegrationTests build under `-warnaserror` remains blocked by historical
Admin.UI/Sample `MSB3277` dependency conflicts and missing Sample.Api assets;
it is not counted as a passing lane.
The updated launch-readiness fitness class passed **13/13** after its two
historical no-live-pass assertions were revised; its affected project built
with zero warnings/errors. `git diff --check` passed in both repositories.

This passing local journey is distinct from a certified shared-rebuild Dapr
run and deployed acceptance. No production installation coverage receipt
issuer, independent virgin-store/all-writer certification, complete legacy
actor-key census, or deployed exact-binding identity issuer is installed.
Deployed selected readback and legacy terminal-writer retirement remain open.

### Round-11 source inventory producer (2026-10-11)

The final Debug source-reference Timesheets solution build passed with zero
warnings and errors. All six test executables passed: Architecture 56/56,
Contracts 92/92, Works 76/76, Server 523/523, Projections 258/258 (including
the greater-than-64-MiB case), and Integration 671 passed with five declared
skips out of 676. The total is **1,676 passed, five skipped, zero failed**.
Affected EventStore source-reference suites passed Client shared projection
87/87, DomainService shared rebuild 43/43, and Server source/route/erasure
61/61. The earlier round-10 full DomainService run remains historical at
732/734 with two unrelated guardrail/catalog failures; no broad EventStore
rerun was required for this increment.

The EventStore source roster registers owner writes before persistence,
persists a 64-page source cut, releases appends only after SDK Begin, and
holds erasure until every owner manifest is accepted. Its recovery state
survives restarted actor instances and producer retries after lost Begin,
Accumulate, Finalize, Stage, and Commit responses. A certified clean roster
accepted 10,000 owners and rejected an omitted captured owner. The operator
route rejected non-administrators and noncanonical tenant/domain scope; the
Dapr gateway rejected tenant, domain, and version-mismatched registrations.
The composed Timesheets capability handler rebuilt two and 10,000 high-entropy
owners through the real DomainService dispatcher with selected readback
(2/2, 307.2 seconds); the two-owner case then rebuilt completed erasure
tombstones and pruned its previously selected row. Focused EventStore source,
route, and erasure tests passed 15/15 after the frozen-count correction.
The producer also denies domains without mandatory writer-fence configuration.
No production `IInstallationCoverageAuthority` receipt issuer, certified
deployed legacy inventory, or live Dapr rebuild is present. Those remain
release gates.

### Round-11 partial implementation verification (2026-10-11)

The preserved round-10 Timesheets and EventStore code patches applied cleanly.
The Timesheets Debug source-reference solution built with zero warnings/errors
using the command below. The focused Time Entry handler class passed 41/41;
the new selected terminal-cursor test and existing terminal-first regression
passed 2/2; the re-signed compact ordinal test passed 1/1. The launch-readiness
fitness class passed 13/13 after its current-language assertion was updated.
The EventStore Client shared-epoch coordinator class passed 19/19. Python
compilation of the updated opt-in journey passed; no latest-tree Dapr journey
ran, so restart and rate-limit behavior remain unverified in the live fixture.
A full Projections executable run was voluntarily canceled after roughly three
minutes in its 10,000-owner capacity case because the changed code does not
affect that capacity path; the runner reported an `OperationCanceledException`
and exit 58. That run is **not** counted as a passing full suite.
The Time Entry handler now fails older-prefix delivery closed while preserving
the selected longer row, including when its approval field was changed and its
storage digest recomputed. EventStore DomainService builds with zero warnings
and errors; the focused independent scratch worker fault/retry and restarted
cleanup tests pass 2/2. The 10,000-owner high-entropy fault case passed 1/1
in 87.3 seconds after Commit stopped waiting for private scratch deletion; it
passed again in 88.0 seconds after the durable work index was added. The hosted
DomainService sweep scans a bounded durable work index every 30 seconds and
retries failed private cleanup after a response or restart. At that partial
verification point, the production shared-rebuild Begin inventory producer
was still open. The later source-inventory increment above adds its code
path; independent deployed coverage certification and live proof remain open.
Story 3.6 and launch remain open.

### Round-10 worktree verification (2026-10-11)

The Timesheets Debug source-reference solution built with zero warnings and
errors using `-p:HexalithCommonsRoot=/tmp/no-commons`. The default
Architecture, Contracts, Server, Projections, Works, and Integration suites
passed **56/56, 92/92, 523/523, 255/255, 76/76,
and 669 passed with five declared skips**, respectively: **1,671
passed, five skipped, zero failed**. The Projections full executable included
the greater-than-64-MiB capacity case; its standard lane separately passed
254/254 after the quiet full run exited zero. The first full
Integration run exposed two test fixtures lacking the new authoritative source
head; both fixtures were corrected and the full suite reran with zero failures
(`674` total in the JUnit report).

EventStore Client passed **1,487/1,487**. EventStore DomainService passed
**732/734**; only the two previously recorded missing nested-Tenants guardrail
subject and named-endpoint catalog-registry assertion failed. The strengthened
10,000-owner high-entropy rebuild capacity case passed **1/1 in 91.779 seconds**:
committed scratch cleanup failed on purpose, selected reads and live delivery
continued, a new epoch began, and retry removed old private scratch for all
10,000 captured owners. Focused SDK checks passed for actor-proven empty Time Entry deletion versus active
empty rejection through the normal mapped coordinator, mixed-catalog
Stage/Commit/Verify, sealed erased-owner tombstones versus active positive-head
omission, evolved captured-event identity, and selected-view repair/retry.

At that round-10 checkpoint, the shared Begin request sealed erased owners
alongside source heads but had no production Begin inventory producer. The
round-10 worktree had no opt-in Dapr rerun; the earlier round-9 0/1 issuance
HTTP 403 attempt below was then the latest live observation. The current
producer and latest-tree local pass are recorded above. Deployed erasure
authority, story acceptance, and release gates remain open.

The build command was
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false -v:q`.
The Projections full run used its built `net10.0` executable with
`-noLogo -reporter quiet`; the standard count came from a second run excluding
only `Large_valid_capability_inventory_streams_more_than64MiB_through_full_rebuild`.
EventStore DomainService ran its built `net10.0` executable with `-noLogo`;
the final strengthened capacity case then ran with `-method
'*HighEntropyPartitionsPromoteAndPruneWithoutTenantCandidateGrowth'`.

### Round-9 worktree verification (2026-10-11)

The Timesheets source-reference solution built with zero warnings and errors
using `-p:HexalithCommonsRoot=/tmp/no-commons`. At that checkpoint, default Architecture,
Contracts, Server, Projections, Works, and Integration executables passed
56/56, 92/92, 523/523, 252/252, 76/76, and 669 passed with five expected
skips: **1,668 passed, five skipped, zero failed**. Projections ran as 251
standard cases and one separate capacity case above 64 MiB; the changed Time
Entry handler class was rerun 36/36. EventStore Client passed 1,487/1,487;
the full DomainService executable passed 727/729. Its two failures are the
same historical missing nested-Tenants guardrail subject and named-endpoint
catalog-registry assertion. Focused EventStore coordinator and partitioned
dispatcher suites passed 19/19 and 18/18; streamed staging passed 9/9. The mapped Time Entry prune
regression passed after hashing canonical selected-row JSON. The changed
EventStore stream controller class passed 26/26. The broad EventStore Server
executable was stopped after roughly seven minutes of CPU-heavy execution
without a completed result; its full-suite status is unverified.

With the opt-in fixture setting enabled, the checked-in Aspire topology
started healthy on an earlier round-9 checkpoint and immediate preflight showed exactly one `eventstore` and
one `timesheets` Dapr ID. That journey passed persisted catalog
readback and non-fixture denial, then failed 0/1 in 83.705 seconds at
capability issuance HTTP 403; a diagnostic capability stream read returned
404. Timesheets logged a workload stream-read request exhausting its HTTP
timeouts. EventStore logged rate limiting and named-projection retry activity;
the underlying cause remains unproved. Only the Timesheets AppHost was
stopped. An unrelated topology later registered a duplicate `eventstore` ID,
so no further isolated run was started. The current tree includes later captured-envelope
and rebuild-head changes and has no live run; no latest-tree end-to-end pass is claimed.

### Historical round-8 full verification and isolated Dapr attempts (2026-10-11)

All six default Timesheets xUnit v3 executables were rebuilt with explicit
Debug source references and `-p:HexalithCommonsRoot=/tmp/no-commons`, then
ran individually: Architecture 56/56, Contracts 92/92, Server 523/523,
Projections 236/236, Works 76/76, and Integration 669 passed with five
expected skips. Overall: **1,652 passed, five skipped, zero failed**.
All six builds and the AppHost build reported zero warnings and errors.
EventStore Client passed **1,482/1,482**. The full DomainService executable
passed 725/727; its two failures remain the missing nested-Tenants guardrail
subject and the named-endpoint catalog-registry assertion recorded at the
prior checkpoint. No nested submodule was initialized.

`aspire start --no-build` and `aspire wait eventstore` / `aspire wait
timesheets` succeeded. `dapr list` showed one app for each ID before the
opt-in run and again after the third attempt. The first opt-in test failed
**0/1 in 77.897 seconds** after persisted catalog and non-fixture denial,
at capability issuance HTTP 403. The second failed **0/1 in 31.062 seconds**
after issuance and selected-model readback, when both confirm race requests
returned HTTP 403. The third failed **0/1 in 31.637 seconds** after a passing
confirm race and composed Time Entry rebuild readback, at adjust issuance
HTTP 403; a diagnostic capability stream read was unavailable. EventStore
logs included rate-limit and named-projection retry messages, without enough
correlation to establish the cause of any opaque 403. The fixture now reports
safe stored-event counts or names on issuance/race failure. `aspire stop`
stopped only the Timesheets AppHost, and `dapr list` was empty afterward.
No end-to-end live pass for that checkpoint is claimed. The public-API/live-fixture
task remains open, and production gates remain separate.

### Focused continuity verification (2026-10-11, same implementation)

After the round-7 baseline, the EventStore continuity, cancellation, abort
closeout, and Time Entry envelope/transition checks passed the affected
focused suites: EventStore partitioned rebuild **16/16**, fenced dispatch
**17/17**, and coordinator plus streamed stage **23/23**; Timesheets capability
handler **40/40**, Time Entry handler **22/22**, and composed HTTP boundary
**573/573**. The composed suite includes the mapped 10,000-event Time Entry
rebuild and failed-rebuild isolation. The three affected test projects and
the Timesheets IntegrationTests project built with zero warnings and errors
using `-p:HexalithCommonsRoot=/tmp/no-commons` where required.

The opt-in Dapr journey had not completed on that tree. The earlier
live observations below remain historical, and the public-API/live-fixture
task remains open for a clean end-to-end run. The fixture retains strict
duplicate-ID preflight, exact persisted readbacks, and bounded read-only
429/500/503 post-restart probes.

### Re-derived materialization verification (2026-10-11, prior checkpoint)

The six default Timesheets executables passed individually after the capture,
fold, cleanup, and compact-lineage changes: Architecture 56/56, Contracts
92/92, Server 523/523, Projections 232/232, Works 76/76, and Integration
669 passed with five expected skips. Overall: **1,648 passed, five skipped,
zero failed**. The Integration lane includes mapped HTTP replay of a 10,000
event, high-entropy Time Entry history, complete selected lineage readback,
and failed rebuild isolation. The Projections lane includes the corresponding
live aggregate-owned write and full-prefix digest comparison.

EventStore Client passed **1,481/1,481**. The partitioned DomainService class
passed **13/13**, including the dispatcher handoff regression;
the full DomainService executable passed 718/720, with only the two previously
recorded unrelated nested-Tenants guardrail and named-endpoint catalog
assertion failures. Paused owner scratch write versus abort, committed and
aborted terminal batch-marker removal, and committed-session crash/retry have
focused passing tests. Both repository `git diff --check` commands passed.
Affected project builds passed with `-p:HexalithCommonsRoot=/tmp/no-commons`;
the supported source-reference solution command still fails at
`Hexalith.EventStore.Contracts` with duplicate `Hexalith.Commons.UniqueIds`
CS1704. The default solution build fails in sibling `Hexalith.Works.Contracts`
with missing polymorphic-serialization references.

The opt-in live test started after read-only preflight showed no Dapr apps.
`aspire start --no-build` followed a focused AppHost build; `aspire wait`
reported both Timesheets and EventStore healthy, and `dapr list` showed exactly
one `timesheets` and one `eventstore` ID when the fixture began. A separate
AppHost added a second `eventstore` during the run. The fixture passed
persisted catalog delivery/readback and the non-fixture denial, then failed
capability issuance with HTTP 403: **0/1, 87.102 seconds**. The duplicate ID
and the 403 occurred in the same run; this observation does not prove the 403's
cause. Only the Timesheets AppHost was stopped, using explicit `aspire stop`;
the other AppHost was left running. Latest-tree live acceptance remains
provisional. The fixture's persisted Time Entry readbacks now verify the
lossless compact lineage bytes, and its post-restart read-only stream, status,
and state probes allow five bounded attempts for HTTP 429/500/503.

### Earlier materialization verification (2026-10-11)

All six default Timesheets xUnit v3 executables ran individually: Architecture
56/56, Contracts 92/92, Server 523/523, Projections 224/224, Works 76/76,
and Integration 668 passed with five expected skips. Overall: **1,639 passed,
five skipped, zero failed**. The Integration run includes persisted composed-host
issuance/use through `/project/v2` and a mapped full Time Entry replay that
deletes an existing selected row. The EventStore Client executable passed
1,479/1,479. The affected EventStore DomainService classes passed 55/55; its
full executable passed 581/583, with the same two unrelated historical
failures: missing nested Tenants guardrail project and the isolated named
endpoint catalog-registry assertion. The affected Timesheets Projections and
Integration project builds passed with zero warnings or errors. The
source-reference solution build still fails with the known duplicate
`Hexalith.Commons.UniqueIds` MSB3243/CS1704 conflict; the package-reference
solution build fails compiling sibling Works.Contracts with missing
polymorphic-serialization types. Both repositories' `git diff --check` passed.

The latest opt-in Dapr test exited 1 after catalog delivery and persisted
readback, at its combined non-fixture issue/missing-owner assertion. An
unrelated ChatBot AppHost introduced a second `eventstore` Dapr ID during the
run. The original fixture did not print each status, so that exact failure's
two statuses cannot be reconstructed. A diagnostic with both IDs present
returned alternating 500/404 for the same signed missing-owner read through
Timesheets' sidecar, while the Timesheets-owned EventStore sidecar returned
404 on all eight probes. A separate signed non-fixture issuance returned 403.
The fixture now reports only the two statuses if this assertion fails again.
A second opt-in attempt stopped at strict preflight with two `eventstore` IDs.
A final clean-preflight attempt reached persisted catalog, the non-fixture
denial, both exact persisted composed Time Entry rebuild readbacks, the confirm
race, and the adjust journey. It exited 1 after 122.714 seconds when the first
post-restart owner stream read returned HTTP 401. ChatBot had again introduced
a second `eventstore` ID during this run. Only the Timesheets AppHost started
for these attempts was stopped; ChatBot was left untouched. The earlier 1/1
opt-in success remains historical evidence, and current-tree local acceptance
remains provisional.

### Earlier materialized read-model increment (2026-10-11)

The latest default Timesheets run passed Architecture 56/56, Contracts 92/92,
Server 523/523, Projections 220/220, Works 76/76, and Integration 667 passed
with five expected skips: **1,634 passed, five skipped**. The Integration lane
includes an always-on composed-host test that invokes the mapped named Time
Entry rebuild route in the mixed projection catalog and reads the persisted
aggregate-owned evidence result. Two more always-on cases reject malformed
confirmation and correction through normal dispatch and mapped rebuild without
persisting a Fresh row. The full EventStore Client executable passed
1,477/1,477. After fixing handler-local cancellation classification, the full
EventStore DomainService run reported 581 passed and two previously recorded
unrelated failures: the absent nested Tenants guardrail and isolated
named-endpoint catalog assertion. The focused handler-local, store-owned, and
caller cancellation cases passed 6/6.

The final targeted pass after the round-4 corrections passed the EventStore
partitioned rebuild class 10/10 (including dispatcher-level promotion and
checkpoint-seeding interleaving), fenced dispatch class 13/13 (including
store-owned task cancellation), streamed-stage class 7/7 (including near-limit
operation-ID cleanup), and epoch coordinator class 11/11 (including legacy
checkpoint rebuild recovery). The Time Entry and capability handler classes
passed 13/13 and 33/33. The composed-host IntegrationTests project and
EventStore Client project built with zero warnings or errors. The opt-in
fixture now tests the mapped named Time Entry rebuild route and exact persisted
readback and permits five bounded read-only 429/500/503 probes after EventStore
restart; Python compilation and both repositories' `git diff --check` passed.
That fixture revision had not completed a live run. Its final
isolated attempt reached persisted catalog delivery and a stored Time Entry
record, then its first capability stream read returned HTTP 429 after an
unrelated ChatBot AppHost restarted with a second `eventstore` Dapr ID. The
Timesheets AppHost was stopped; the unrelated host was left untouched. The
preexisting 1/1 result below remains historical evidence.

An earlier checkpoint passed all six default Timesheets executables: Architecture
56, Contracts 92, Server 523, Projections 218, Works 76, and Integration 664
with five skips (**1,629 passed, five skipped**). Focused EventStore
partitioned rebuild, fenced dispatch, and streamed-stage suites passed 5/5,
8/8, and 5/5. A later targeted audit closed a promotion/seeding race with a
durable SDK handoff fence: a deterministic interleaving test proves a second
rebuild and ordinary delivery wait while checkpoints are seeded. After that
change, the affected partitioned, fenced, and streamed-stage classes passed
6/6, 8/8, and 5/5; three Timesheets capability rebuild cases passed 1/1 each.
The final fenced class passed 10/10 after adding Dapr-exception outage cases.
The new tests cover post-capture owner sequence handoff,
interrupted scratch cleanup and retry after coordinator restart, abort cleanup,
store outages before folding, and streamed-page readback. A later audit also
proved committed scratch cleanup can resume after another epoch starts.
Python compilation
of the opt-in fixture and both repositories' `git diff --check` passed.
The full EventStore Client executable passed 1,474/1,474. The full
DomainService executable passed 573/575; its two failures are the existing
absent nested-Tenants guardrail and the isolated named-endpoint catalog
registry assertion, outside the changed shared projection paths.

The source-reference solution build still exits 1 in EventStore.Contracts with
MSB3243/CS1704 for duplicate `Hexalith.Commons.UniqueIds` versions 2.30.1.0
and 1.0.0.0. A package-mode attempt exits 1 in sibling Works.Contracts with
missing `Hexalith.PolymorphicSerializations` types. Two then-current isolated
opt-in attempts reached persisted delivery: one stopped on command-status HTTP
500, and the next passed the confirm race but received three opaque 403
responses on adjust POST. A third attempt stopped before writing data when
ChatBot restarted with a duplicate `eventstore` Dapr ID. The Timesheets
AppHost was stopped and the unrelated process left untouched. The prior 1/1
opt-in result below is historical evidence for the earlier revision.

### Earlier materialized read-model run (2026-10-11)

That earlier uncommitted revision added SDK-delivered Time Entry evidence
and capability views. All six default Timesheets executables passed:
Architecture 56, Contracts 92, Server 523, Projections 206, Works 76, and
Integration 664 passed with five skips (**1617 passed, five skipped**). The
EventStore Client suite passed 1469/1469; focused partitioned DomainService
tests passed 3/3. The full DomainService suite passed 565/567, with one
failure requiring a prohibited nested Tenants submodule and one isolated
named-endpoint catalog-registry failure. The prescribed source-reference
Timesheets solution build is blocked by the pre-existing duplicate
`Hexalith.Commons.UniqueIds` assembly conflict (MSB3243/CS1704); affected
Timesheets project builds passed with zero warnings or errors.

The isolated opt-in Dapr run passed **1/1 in 160.146 seconds** with exactly
one `eventstore` and one `timesheets` Dapr app. It covered both new model
readbacks for confirm and adjust, the one-winner race, EventStore restart,
replay denial, and a fresh post-restart link. Two earlier attempts reached
the new readbacks but later returned transient opaque HTTP 403 responses.
Story 3.6 remains `in-progress`; launch remains `FAIL` for production gates.

### Opt-in local Dapr/EventStore acceptance (2026-10-11)

The checked-in Timesheets Aspire composition now runs distinct healthy
EventStore and Timesheets Dapr applications. The opt-in
`MagicLinkLiveDaprTests` and `MagicLinkLiveDaprFixture` run the isolated
[persisted journey](../../../docs/local-magic-link-dapr-proof.md#run-the-persisted-journey).
Development-only signed binding claims and a fixture guard supply test
identity. The real Timesheets HTTP writer receives EventStore `Completed` plus
exact readback before each `202`; the fixture also queries command status
directly and matches `Completed`, event count, owner, and committed sequence
to each stored record, issue, and use batch. The test independently checks stored record,
issue, use and effect payloads, delivered Fresh catalog and candidate index,
recording retry, one-winner race, equal opaque denials, and unchanged streams
after an EventStore process restart. A race loser can append a rejection
marker, but cannot append a second use or effect. The optional Timesheets
idempotency key was removed from the writer because EventStore has no trusted
intent adapter for Timesheets commands; the stable MessageId remains the retry
identity. No EventStore contract or deployed topology was changed.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build src/Hexalith.Timesheets/Hexalith.Timesheets.csproj --no-restore -m:1 -v:q` and the corresponding
IntegrationTests project build passed with zero warnings/errors. Direct
`python3 scripts/local_aspire_magic_link_journey.py` exited 0. The opted-in
executable command
`TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'`
exited 0 with one passed, zero failed/skipped; the final status-strengthened
run passed in 73.929s. It required the explicit AppHost fixture setting,
denied a signed non-fixture request before any capability owner event,
checked exact adjustment values, reread catalog and index after restart, and
used a fresh post-restart link. This proves local
persistence only. Story 3.6 stays `in-progress` and launch stays `FAIL` for
production binding issuance, read-model wiring, deployed history inventory and
deployed acceptance.

The same exact opted-in command passed 1/1 in 71.494s after the authorization
review fixes, then exited 1 after direct status assertions were added: valid
confirm GET returned opaque `403` after persisted readback (83.307s). Direct
`python3 scripts/local_aspire_magic_link_journey.py` exited 0 against that
Aspire session. The next opted-in run exited 1 when adjust recording returned
opaque `403` (32.549s). Sanitized Timesheets warnings showed outbound
10-second timeouts and 429 retries. A bounded three-attempt wait was added
only for the stable-identity recording command and read-only valid GET; any
other status or continuing denial fails. The final opted-in run exited 0 with
one passed and zero failed/skipped in 73.929s.

The six default test executables passed individually: Architecture 56/56,
Contracts 92/92, Server 523/523, Projections 174/174, Integration 663 passed
and five skipped (668 total, including this opt-in lane), Works 76/76. Overall:
1,589 tests, 1,584 passed and five skipped. The current solution build command
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`
exited 1 in `references/Hexalith.EventStore.Contracts` with MSB3243/CS1704:
duplicate `Hexalith.Commons.UniqueIds` assemblies, versions `2.30.1.0` and
`1.0.0.0`. The connected-topology proof had already recorded that same
failure before this fixture/writer increment.

The post-review default commands, run from the repository root without the
live opt-in variable, had these exact results:

| Exact command | Result |
| --- | --- |
| `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` | Exit 0; 56 passed. |
| `tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` | Exit 0; 92 passed. |
| `tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` | Exit 0; 523 passed. |
| `tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` | Exit 0; 174 passed. |
| `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` | Exit 0; 663 passed, five skipped. |
| `tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests` | Exit 0; 76 passed. |
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` | Exit 1; EventStore.Contracts MSB3243/CS1704 duplicate UniqueIds versions above. |
| `python3 -m py_compile scripts/local_aspire_magic_link_journey.py` | Exit 0. |
| `git diff --check` | Exit 0. |

The release table's current Build gate was corrected to `FAIL`. The exact
focused command
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'`
first exited 1 with 12/13 passed because it still expected `PASS`.
After that expectation was corrected,
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-restore -m:1 -v:q`
exited 0 with zero warnings/errors; the focused command exited 0 with 13/13
passed, and the full ArchitectureTests command above exited 0 with 56/56 passed.

### Historical local Dapr acceptance preflight (2026-10-11)

The preflight in [the local Dapr proof record](../../../docs/local-magic-link-dapr-proof.md) stopped before the opt-in acceptance journey. `docker info --format '{{.ServerVersion}}'` returned `29.8.2`, and `dapr list` exited 0 but listed no `timesheets` app ID. `aspire ps --non-interactive` found an EventStore AppHost run owned by a separate EventStore integration test; it was not modified. Source inspection found no Timesheets Dapr module or trusted-effect allow-list entry in the EventStore AppHost, while the Timesheets AppHost declares no EventStore resource or Dapr sidecar. The EventStore local `statestore` component also excludes Timesheets and directs domain services to use a separate scoped component, so adding a sidecar alone would not deliver the persisted index/catalog. The environment exposed no Timesheets/EventStore/Dapr/signing variable names. A connected, isolated Timesheets identity and persisted index/catalog delivery cannot be prepared from the checked-in topology without the separately owned platform work.

Per the spec's preflight stop condition, `MagicLinkLiveDaprTests` and `MagicLinkLiveDaprFixture` were not added, and `TIMESHEETS_LIVE_DAPR=1` was not run. No record, issue, use, restart, race, or stored-event observation was made. `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'` exited 0 with 13/13 passed; `git diff --check` found no whitespace errors. The 2026-10-10 in-process and actor tests below remain the latest full Timesheets suite evidence; this documentation-only preflight did not rerun restore or build. Story 3.6 remains `in-progress` and release remains `FAIL`.

### EventStore Time Entry recording increment (2026-10-10)

The external submission route now submits an internal recording intent to the existing keyed `timesheets` processor. The processor rehydrates current owner state, checks verified workload origin and actor, server-side authorization and references, and a Fresh Activity Type catalog before emitting one `StoredTimeEntryRecorded` and, when policy requires, one `StoredTimeEntrySubmitted`. The route returns `202` only after a bounded `Completed` status and matching stored-event readback. A stable command identity makes retries safe after uncertain results. The existing magic-link use path can then fold the recorded owner state.

In-process HTTP tests prove record-then-confirm, one recording across concurrent requests and retry, submitted-policy evidence, opaque denials for unauthorized, unavailable-catalog and conflicting submissions, and no second append after `PublishFailed` or a mismatched receipt. Review tests also cover actor-bound collision-safe retry identity, retained rejection and terminal markers before recording, exact readback correlation/scope/audit instant, and stored submission folds in loader and projection. The actor state-store fixture now seeds recording through the production processor and proves confirm/adjust after actor recreation and replay denial. These tests substitute the EventStore gateway, read-model store, and access guard; they do not prove live Dapr persistence, production caller authentication, or a trusted resource-binding issuer.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching warning-as-error Debug solution build passed with zero diagnostics after review patches. Six Timesheets executables ran individually: Architecture 56/56, Contracts 92/92, Server 523/523, Projections 174/174, Integration 663 passed and four skipped (667 total), Works 76/76. Overall: 1588 total, 1584 passed, four skipped, zero failed. A separate focused HTTP class rerun passed 567/568: `AssertSensitiveMaterialAbsent` falsely matched the generic substring `60` in a permitted timestamp; this pre-existing flaky assertion is tracked in `deferred-work.md`. Story 3.6 remains `in-progress`; release remains `FAIL`.

### Review patch closure: replay, polling, receipts, and projection scope (2026-10-10)

The in-process EventStore actor fixture now persists a rejected command before a later valid issue or use and proves the valid command commits both before and after actor recreation. The HTTP gateway fixture persists rejection outcomes and successful batches that report `EventsStored` or `PublishFailed`; `Received` stores no batch. All three statuses yield an opaque `403`. The fixture returns delayed `Completed` statuses, distinguishes workload-only routes, and checks malformed status receipts and tampered stored issue/use/confirm/adjust/expire events. A composed host test resolves `Program.cs`'s assertion source, then sends a signed request through its real workload gateway with tenant, domain, and authenticated actor bindings; blank `party_id` falls back to `NameIdentifier`. Direct processor positive/negative pairs exercise verified origin for use and origin/actor for issue and revoke. Unit tests cover a five-second default, configurable polling deadline, a 10-ms minimum interval, configuration binding and startup validation, cancellation, terminal statuses, and stable retry message identity. Projection tests reject unscoped or cross-tenant recordings and cross-tenant terminal evidence in entry, ledger, and period folds, including empty periods. OpenAPI includes verified `202`, malformed-body `400`, and opaque `403` cases. These tests do not establish a deployed EventStore/Dapr topology or deployed stream inventory.

The matching source-reference Debug restore and solution build passed with zero warnings/errors using `-p:Configuration=Debug -p:UseHexalithProjectReferences=true` for restore and `--configuration Debug -p:UseHexalithProjectReferences=true` for build. The IntegrationTests project rebuild after this review passed with zero warnings/errors. The bare documented build without the project-reference property failed in the Works submodule with `HXW0002` and missing dependency references; this is a pre-existing build-mode issue, not a passing gate. All six Timesheets executables ran individually: Architecture 56/56, Contracts 92/92, Server 512/512, Projections 173/173, Integration 652 passed plus four intentional skips (656 total), Works 76/76. Total: 1565 tests, 1561 passed, four skipped. The affected `MagicLinkConfirmationHttpBoundaryTests` class passed 557/557 after review patches. Timesheets HEAD pins EventStore `cf10b4c7e8c86ae6ad06c842ed567dd0e491ad46`, which local EventStore `origin/main` contains; earlier unpublished-pointer notes below are historical. Live topology, trusted production binding issuer, and deployed-history inventory remain open, so launch remains `FAIL` and Story 3.6 remains `in-progress`.

### Durable submission and workload gateway increment (2026-10-10)

The changed worktree uses an EventStore workload channel with signed tenant and domain bindings for submit, message-primary status, and stream read. EventStore stamps a verified `timesheets` origin; issue and revoke also require a signed actor binding. The Timesheets processor rejects absent origin, malformed intent, and forged actor/hash evidence. It uses actor-provided Time Entry state during `/process` and reads only the separate capability stream. Stored wrapper events are normalized before affected Time Entry and capability read-model folds.

Local proof covers valid confirm/adjust and issue/revoke/expire HTTP submissions, `Completed` status plus exact stored-event readback, one-winner concurrency and replay denial, legacy Used/Revoked/Expired histories, opaque Received/EventsStored/PublishFailed outcomes, protected keyed `/process` dispatch, and actor state-store commit/reload for confirmation, adjustment, and proposed-entry revoke/expire. Regressions cover ordinary multi-entry reports with equal local sequences, distinct payloads at one owner/sequence, explicit MissingStream for an unrecorded proposed-entry owner, denial on MissingEvent or malformed 404, queued issuance and use after expiry, expiry advancing during awaited authorization for issuance and both use actions, undefined numeric actions deserialized from JSON, repeated physical issuance, and a valid signed command crossing the real MediatR authorization pipeline. Other regressions cover malformed or terminal-only issuance and receipt field and audit-source mismatches. EventStore controller tests cover workload route policies, cross-tenant and cross-domain denial, reserved-origin spoofing, a workload status Location, and preservation of human routes. These tests use local in-memory stores and an in-process gateway fixture; they do not prove deployed Dapr persistence.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors. The six individual Timesheets executables passed: Architecture 56/56, Contracts 92/92, Server 495/495, Projections 160/160, Integration 606 passed plus four declared skips (610 total), Works 76/76. Total: 1489 tests, 1485 passed, four skipped. The EventStore Server.Tests and Client.Tests projects built with zero warnings/errors. The focused EventStore controller and signed HTTP middleware selection passed 43/43; workload client transport passed 2/2. The EventStore server selection used `-class '*WorkloadGatewayControllerTests' -class '*DaprInternalAuthenticationHandlerTests'`; the client selection used `-class '*EventStoreGatewayWorkloadClientTests'`.

Release remains `FAIL`. The AppHost has no EventStore resource, deployed capability and Time Entry histories are unknown, and no live Dapr-backed write/restart/concurrency proof has run. The current authority-mode `JwtWorkloadAssertionIssuer` cannot sign per-request tenant/domain/actor bindings, so workload submission fails closed in that mode; production needs a trusted resource-binding issuer. Old capability-stream terminal writers must stop before the new Time Entry owner path is enabled. The EventStore submodule changes also require publication before a Timesheets gitlink can reference them.

### Fail-closed public POST and catalog status increment (2026-10-10)

At Timesheets baseline `61d6fc7a31544985bed5458972f81b7050b9c775`, the changed worktree returns the shared opaque 403 for valid confirm and adjust POST decisions because no durable commit occurs. Valid Fresh-catalog GET displays remain 200. The concrete loader retains only the status of a well-formed persisted non-Fresh catalog; it discards capability, Time Entry, items, cursor, timestamp, and detail. Missing, malformed, and unreadable catalogs remain Unavailable. The HTTP fixture's EventStore gateway records zero submit attempts for valid POSTs, and GET remains available after those POSTs. These are in-process assertions, not persisted concurrent single-use proof.

Read-only deployment and stream-inventory check: `src/Hexalith.Timesheets.AppHost/Program.cs` still declares no EventStore resource; the repository contains no Timesheets deployment endpoint or stream inventory artifact; and the available environment exposes no `EVENTSTORE`, `DAPR`, `APP_API_TOKEN`, `JWT`, `ASPIRE`, `AZURE`, or `KUBE` variable names (`printenv | cut -d= -f1 | rg -i 'EVENTSTORE|DAPR|APP_API_TOKEN|JWT|ASPIRE|AZURE|KUBE'`). The existing 2026-10-09 AppHost attempt failed on missing internal credentials. No deployed stream read was possible, so whether capability and Time Entry histories exist remains unknown. A release owner needs a deployed EventStore endpoint, read-only stream access, and tenant/aggregate scope to inventory those histories before choosing a write owner or migration. No stream migration was started.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors. Direct focused classes passed: loader 80/80 and HTTP boundary 462/462. The six direct executables ran individually: Architecture 55/56 (the pre-existing Aspire `13.6.1` package-verdict assertion), Contracts 92/92, Server 490/490, Projections 150/150, Integration 557 pass/four intentional skips, Works 76/76. Total 1425: 1420 pass, one pre-existing failure, four skips. Story 3.6 remains in progress; durable atomic single-use, persisted concurrency, deployed-history inventory, and live-host proof remain open. Release stays `FAIL`.

Post-review verification removed the stale OpenAPI submit `202` responses, passed the two new contract cases, and strengthened status-field assertions. A changed readiness fitness assertion initially failed on old prose, was corrected, and the Architecture lane reran at 55/56 with only the pre-existing Aspire package-verdict assertion failing. Story 3.7 was reopened because its valid POST `202` acceptance is suspended; the current OpenAPI response description states that durable submission is unavailable.

### Post-review GET-only authorization guard (2026-10-09)

EventStore `f87affaefef5693fd80452eea02999e46fa1888b` closes a review-found authorization gap: an explicit weak policy on a GET-only canonical route could suppress the denying fallback. Timesheets `61dc18bdb3e28ed8bbee64488954b8f42b90c4e7` changes only its EventStore gitlink from `fe4e7559` to that local commit. Both commit messages passed commitlint; commit validation was not bypassed. No fetch or push occurred. Local EventStore `origin/main` remains at `fe4e7559`, so the current gitlink is **not yet proven fetchable in a fresh clone**; EventStore publication must precede any Timesheets push.

- SDK DomainService.Tests Debug build passed with zero warnings/errors. The five-method inventory/public-route selection, including the new weak-policy cases, passed 22/22 (`/tmp/timesheets-story36-review-sdk-focused.log`); the whole ExtensionsTests class passed 81/81 (`/tmp/timesheets-story36-review-sdk-class.log`). The full SDK executable ran 549 tests: 548 passed and the same absent nested-Tenants guardrail subject failed (`/tmp/timesheets-story36-review-sdk-full.log`). Repository policy forbids initializing that nested submodule.
- Timesheets source-reference Debug restore/build passed with zero warnings/errors (`/tmp/timesheets-story36-review-build.log`). The two HTTP classes passed 482/482 (`/tmp/timesheets-story36-review-http-classes.log`); the four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-review-scratch-http.log`). The six separate executables ran 1415 tests: Architecture 55/56 with the existing Aspire 13.6.1 package-verdict failure, Contracts 90/90, Server 482/482, Projections 150/150, Integration 557 passed and four intentional skips, Works 76/76. Overall: 1410 passed, one existing failure, four skips. Captures are `/tmp/timesheets-story36-review-<Project>.log`.

```bash
# Run the following SDK commands from references/Hexalith.EventStore.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRejectsWeakExplicitAuthorization' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
# Run the following Timesheets commands from the Timesheets root.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
# Each of the six projects was also run individually as DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>.
```

### Upstream pointer verification (2026-10-09)

Timesheets `46265ba71138617ac387bd68e63aa0d7e5685947` pins EventStore `fe4e7559f17ed53eea005b0ae5a4f6a358221780`, which local EventStore `origin/main` contains. The five owned SDK files are byte-identical between `fe4e7559` and the previously integrated `bd976eff37e80fa404ee47d4031ecf6c2545b541`. No new fetch or push was performed for this verification. `46265ba` moved the final gitlink alongside other files and pointers; the earlier `298899bfbdaeaa6df7528d32309b826f5b63f95d` remains the separate gitlink-only commit. The earlier section below describes the now-superseded local pointer and its then-unproven remote reachability.

- In `references/Hexalith.EventStore`, the Debug DomainService.Tests build passed with zero warnings/errors. The focused selection passed 19/19 (`/tmp/timesheets-story36-upstream-sdk-focused.log`); the entire ExtensionsTests class passed 78/78 (`/tmp/timesheets-story36-upstream-sdk-class.log`). The full DomainService executable ran 546 tests, with 545 passing and one `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers` failure because `references/Hexalith.EventStore/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` is absent (`/tmp/timesheets-story36-upstream-sdk-full.log`). Repository policy prohibits initializing that nested submodule.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
```

- In Timesheets, the source-reference Debug restore and build passed with zero warnings/errors. The two HTTP classes passed 482/482 (`/tmp/timesheets-story36-upstream-http-classes.log`), including the identical signed projection dispatch refused with 404 on the public port and persisted internally. Four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-upstream-scratch-http.log`).

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
mkdir -p /tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

- Each Timesheets test executable ran separately as `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>`. Captures are `/tmp/timesheets-story36-upstream-<Project>.log`. Architecture 55/56: the sole failure is the existing `Launch_readiness_record_captures_package_currency_verdict_dimensions` expectation for Aspire 13.6.1. Contracts 90/90; Server 482/482; Projections 150/150; Integration 557 passed and four intentional skips (561 total); Works 76/76. Overall: 1415 total, 1410 passed, one failure, four skips. These are in-process results; live persisted topology and durable capability-use writes remain open, and the release decision remains `FAIL`.

### Prior local public-route review-patch verification (2026-10-09)

The final local EventStore integration is commit `bd976eff37e80fa404ee47d4031ecf6c2545b541` on `fix/story-3-6-public-routes`, created from the locally available `origin/main` (`f463442cca19e4199982a23a08bae4a490767d4a`) by applying the SDK public-route commit `f615dd5d`, the externally captured inventory patch `dc140b0f`, and the diagnostics fixture correction `0b1b1455` without rewriting any existing main commits. Commitlint validated the integrated commit. Timesheets commit `298899bfbdaeaa6df7528d32309b826f5b63f95d` changes only its EventStore gitlink from `dc140b0f` to `bd976eff`. Local `git branch -r --contains bd976eff37e80fa404ee47d4031ecf6c2545b541` returned no branch. No fetch or push occurred in this run; remote reachability is unproven, and a fresh clone cannot rely on this pointer until EventStore publishes it. The earlier external `/pushall` commits `dc140b0f` and `2a3c21b` captured in-flight edits and mixed gitlinks; they are preserved as history.

- SDK build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false` passed with zero warnings/errors (`/tmp/timesheets-story36-sdk-integrated-build.log`). The focused inventory/public-route selection below passed 19/19 (`/tmp/timesheets-story36-sdk-integrated-focused.log`). The entire `EventStoreDomainServiceExtensionsTests` class passed 78/78 (`/tmp/timesheets-story36-sdk-integrated-class.log`). The full SDK DomainService test executable ran 546 tests: 545 passed and one `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers` failure because its subject is absent at `references/Hexalith.EventStore/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` (`/tmp/timesheets-story36-sdk-integrated-full.log`). Repository instructions prohibit initializing that nested submodule to satisfy the guardrail.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
```

- Timesheets source-reference restore and build passed with zero warnings/errors. Captures: `/tmp/timesheets-story36-integrated-restore.log` and `/tmp/timesheets-story36-integrated-build.log`.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

- The two HTTP classes passed 482/482, including the same projection dispatch and signed workload assertion on the public and internal ports; public returned 404 and internal persisted the index entry (`/tmp/timesheets-story36-integrated-http-classes.log`). Four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-integrated-scratch-http.log`).

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
mkdir -p /tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

- All six Timesheets executables ran individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>` and captured to `/tmp/timesheets-story36-integrated-<Project>.log`. Architecture 55/56 (one existing Aspire 13.6.1 package-verdict mismatch in `Launch_readiness_record_captures_package_currency_verdict_dimensions`); Contracts 90/90; Server 482/482; Projections 150/150; Integration 557 pass / four intentional skips (561 total); Works 76/76. Overall 1415 total, 1410 pass, one failure, four skips. The earlier `/tmp/timesheets-public-*` captures from 16:36 are pre-patch. No live persisted topology, durable write, or release acceptance is claimed.

- Prior public-route compatibility rerun (2026-10-09, before the review patches), changed worktree based on `3e1ca73ba51f0c91467b74ac3dc85f88b6526b7e`: the EventStore SDK public-route/startup tests passed 24/24; Timesheets source-reference Debug restore/build passed with zero warnings/errors; `MagicLinkConfirmationHttpBoundaryTests` and `InternalSurfaceGuardTests` passed together (482/482), including anonymous calls to all four capability routes; all four scratch-root HTTP privacy cases passed. Six Timesheets lanes: 1415 total, 1410 pass, one pre-existing Architecture package-verdict failure, four intentional skips. The live AppHost started, but `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 because APP_API_TOKEN and JWT Authority/SigningKey are missing; `aspire stop` exited 0. In-process route and privacy verification does not prove live persisted topology or durable capability use.

  Live-topology commands and results for this rerun, in order (all with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`):

  ```bash
  aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json # exit 0
  aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive # exit 18
  aspire logs timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --tail 30 # exit 0
  aspire stop --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive # exit 0
  ```

  The logs report `Domain-service internal credentials are not configured: APP_API_TOKEN must be configured. Authentication:JwtBearer requires exactly one of Authority or SigningKey to be configured.` Captures: `/tmp/timesheets-public-aspire-{start,wait,logs,stop}-last.log`.
- Prior spec-17 verification (2026-10-09), changed worktree based on `c75de3c2f6328e63e77682b6d3cac6c54a7ded87`: SDK `10.0.401`; README source-reference Debug restore/build passed with zero warnings/errors. The corrected non-HTTP privacy theory passed 432/432, including twelve new Timesheets/framework category, message and state controls that permit tenant identifiers in diagnostics while the external response rejects both fixture tenants. Six direct executables ran individually: Architecture 55/56; Contracts 90/90; Server 482/482; Projections 150/150; Integration 508 pass / 42 fail / four skips; Works 76/76. Total: 1408, 1361 pass, 43 fail, four skips. All 42 Integration failures are unexpected 401s; the Architecture failure is the existing Aspire package-verdict mismatch. The four scratch-root HTTP cases were rerun against this build and still failed at 401 before privacy assertions. Final logs: `/tmp/timesheets-story-3-6-spec-17-build-final.log`, `-privacy-final.log`, `-<Project>-final.log`, and `-scratch-http-final.log`; restore is in `-restore.log`. AppHost start exited 0, wait exited 18 because `APP_API_TOKEN` and JWT Authority/SigningKey are missing, and stop exited 0. No live HTTP or persisted-topology acceptance is claimed.
- Current focused command (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.PrivacyAssertionsRespectCategoryAndResponseScope'` exited 0 (432/432). This proves the helper scopes, not current HTTP behavior.
- Prior spec-16 final post-review verification (2026-10-09), changed worktree based on `bb7fe1fb93681a0c77eb8eab76a19e2232326def`: SDK `10.0.401`; the README source-reference Debug restore/build passed, and the post-review solution rebuild passed with zero warnings/errors. The in-lane non-HTTP privacy theory passed 420/420: harmless vocabulary in message/category/state across three framework/third-party categories with populated, empty and whitespace tokens; universal protected values across four categories; blank/whitespace-token protected-value failures; two distinct request tokens and hashes; response-only tenant exclusions; Timesheets/response generic exclusions; realistic permitted diagnostics and serialized production default ProblemDetails controls. All six final executables ran individually: Architecture 56 total / 55 pass / one failure; Contracts 90/90; Server 482/482; Projections 150/150; Integration 542 total / 496 pass / 42 fail / four existing skips; Works 76/76. Overall: 1396 total, 1349 pass, 43 fail, four skips. The 420 new cases account for the entire increase from the prior 976-test inventory. No new failure was introduced: the package-verdict failure and 42 unexpected-401 HTTP failures are unchanged. Before independent review, the initial matrix passed 189/189 and six lanes reported 1165 total / 1118 pass / 43 fail / four skips. All four selected scratch-root HTTP cases still stop at 401. Final-build logs are `/tmp/timesheets-story-3-6-spec-16-build-final.log`, `-privacy-helpers-final.log` and `-<Project>-final.log`; the `-scratch-http.log` run preceded the post-review rebuild; exact full commands remain in README.
- Prior spec-16 focused command (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.PrivacyAssertionsRespectCategoryAndResponseScope'` exited 0 (420/420 after review; 189/189 before review). These synthetic helper cases establish assertion behavior without proving current HTTP-boundary conformance, scopes or exception diagnostics.
- Prior spec-16 runtime baseline (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` exited 0; `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18. Describe reported Timesheets Finished and security Healthy; `aspire logs timesheets --tail 40` reported missing `APP_API_TOKEN` and the required JWT Authority/SigningKey configuration. Explicit-AppHost `aspire stop` exited 0. No production source, credentials, dependency pin or authorization policy was changed.
- Prior privacy-assertion and record-patch verification (2026-10-08), changed worktree based on `aba488b18c4a4b96e08442538c41481c752c0382`: the prescribed source-reference Debug restore/build passed with zero warnings/errors. All six direct executables ran individually: Architecture 56 total / 55 pass / one failure; Contracts 90/90; Server 482/482; Projections 150/150; Integration 122 total / 76 pass / 42 failures / four existing skips; Works 76/76. Overall: 976 total, 929 passed, 43 failed, four skipped. The architecture failure is `Launch_readiness_record_captures_package_currency_verdict_dimensions`, which expects the current imported Aspire `13.6.1` text while the dated verdict records `13.6.0`. Every integration failure is an unexpected 401 after the pre-existing `da172a8effe484be96c17aaef1c007e1ecb8f27c` EventStore update enabled workload authorization before the endpoint paths under test. The four scratch-content-root privacy cases also stop at that 401, before their diagnostic assertions. These failures are unresolved; they are not a passing HTTP/privacy or release gate.
- Prior focused fallback verification (2026-10-08): a retained reflection verifier exercised the rebuilt IntegrationTests assembly's privacy helpers independently of HTTP authorization. All 144 checks passed: harmless domain vocabulary in framework messages is accepted; raw token/hash and all six protected fixture identifiers are rejected in category, message and structured state across three framework/third-party categories; Timesheets logs and response bodies retain the generic-vocabulary exclusions. Command: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project _bmad-output/implementation-artifacts/tests/3-6-privacy-assertion-verifier/Verifier.csproj --configuration Debug -- "$PWD/tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0"`. This is helper-level evidence, not an HTTP boundary or exported-scope/exception proof. Before rebuilding, the previous IntegrationTests binary reproduced the checkout-path false positive with `ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked`; the selected privacy test failed on the framework content-root message.
- Prior runtime baseline (2026-10-08): explicit-AppHost `aspire start` succeeded, but `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18. Describe reported Timesheets Finished; logs reported missing `APP_API_TOKEN` and missing JWT Authority/SigningKey configuration. `aspire stop` succeeded. This current startup blocker differs from spec-14's historical port conflict. No authentication bypass, dependency change or submodule pointer change was made.
- Latest passing full run: spec-14 final post-review verification (2026-10-05), changed worktree based on `ee87e39f9f43e442e5426bb090afbe70be26492a`: source-reference Debug restore/build passed with zero warnings/errors. All six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 482/482, Projections 150/150, Integration 122 total / 118 pass / four existing skips, Works 76/76 (976 total, 972 pass, four skips, zero failures). These final results include four additional HTTP review-remediation cases; the focused 72/72 loader, 101/101 projection and 39/39 HTTP/guard counts belong to the closure run before independent review. Live topology, durable atomic writes, concurrent single-use, non-Fresh diagnostic distinctions and deployed Activity Type inventory remain open; release remains `FAIL`. See [spec-14 final verification](../spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md#final-post-review-verification).
- Prior record-correction increment (2026-10-05), changed worktree based on `c9f543f0623c7cde775eac99d4f39ce70fb4c84e`: documentation only. The increment ran no new tests. The four record patches are closed; acceptance and release gaps remain open. Review triage then verified `aab64cf`, after `c9f543f` moved the Builds, EventStore, FrontComposer and Tenants pointers: the source-reference Debug restore/build passed with zero warnings and errors, and all six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 479/479, Projections 146/146, Integration 106 pass/4 declared skips, and Works 76/76 (957 total, 953 pass, four skips).
- Prior review-patch increment (2026-10-05), changed worktree based on `c5fcf987046018c980c5def7c2d3e1a9ce310abe`: source-reference Debug restore/build passed with zero warnings and errors. All six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 479/479, Projections 146/146, Integration 106 pass/4 declared skips, and Works 76/76 (957 total, 953 pass, four skips). New assertions pin the fail-closed `InternalSurfaceOptions` defaults and use MessageIds whose lexical order conflicts with event sequence in the complete-fold regression. Eight review patches were closed; acceptance and release gaps remained open. Pre-edit Aspire start succeeded; `aspire describe` reported Timesheets Finished with exit code 134; Aspire was stopped. No bind-failure cause was captured for that attempt. This is not a live EventStore proof.
- Prior date / build baseline: 2026-10-04, changed worktree based on `2b03356b4866900559c772c402301ac89c084ef5` (historical-review reconciliation and complete-fold determinism increment).
- Prior (2026-10-04) SDK: .NET `10.0.401` (`rollForward: latestPatch`).
- Prior (2026-10-04) Restore: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` passed.
- Prior (2026-10-04) Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings and errors.
- The historical bare solution build and its explicit-Debug retry on 2026-10-03 failed with `HXW0002` and missing Works serialization references (176 errors): solution traversal removes the external project's Configuration, requiring the supported source-reference option. The first source-reference build hit `NETSDK1064` for unrestored Roslynator.Analyzers 4.15.0; restoring with the same source-reference mode resolved it. No package pins or submodule pointers changed.
- Prior (2026-10-04) Direct xUnit v3 executables, each invoked individually as `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>`: ArchitectureTests 56/56; Contracts.Tests 90/90; IntegrationTests 110 total, 106 pass, 4 declared skips; Projections.Tests 146/146; Server.Tests 479/479; Works.Tests 76/76.
- Prior reconciliation inventory: 957 tests, 953 passed, 4 declared infrastructure/performance skips, 0 failures. The existing performance lanes were left disabled.
- The first ArchitectureTests invocation in the prior alternate-configuration increment failed one of 55 cases because the shared catalog now expresses CommunityToolkit.Aspire.Hosting.Dapr as `$(HexalithAspireHostingDaprVersion)`. The existing catalog reader was adapted to resolve exact property references to unconditional defaults, preserving the concrete-version assertion and excluding the unrelated Folders-only override. The focused ArchitectureTests Debug build passed with zero warnings/errors; its rerun passed 55/55. The other five lanes also passed in that prior increment; current six-lane results are recorded above.
- Prior review coverage (retained): three server availability combinations, default loader resolution, canonical deactivation delivery plus all four external HTTP routes, configured-port projection delivery with persisted index/catalog assertions, public-port refusal of a dispatch proven to persist internally, all four public routes after internal delivery, and two HTTP claim-scoped administrator flows for new and existing capabilities through the actual host accessor and authorization request context. Opaque-denial HTTP coverage passed on 2026-10-05; the prior spec-17 rebuilt HTTP cases stopped at unexpected 401 responses before those assertions; the current rerun above passes.
- Prior alternate-configuration increment (baseline `30fc6d3092cbb376c500176ceeef351109e3fa7f`): the existing `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` fitness check counts exactly one case-insensitive Timesheets InternalSurface configuration-key prefix in comment-stripped AppHost source, covering double-underscore exports, colon keys and command-line settings while accepting unrelated InternalSurface URLs. It rejects literal `AllowOnAnyPort` in both AppHost and host source and preserves both interpolated-verbatim prefix orders (`@$"` and `$@"`). The retained verifier `python3 _bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py` passed baseline, eight harmless literal/URL controls and restored-source checks; all 21 unsafe mutations were rejected, including colon-key/WithArgs overrides, host Configure/in-memory bypasses, interpolated-verbatim URL-hidden overrides and an actual end-of-line AppHost export comment with the chain semicolon on the next line. Block-comment and directive substitutes also retain their semicolon outside the disabled region. Both production files were restored byte-for-byte. This remains bounded lexical source fitness evidence; nested interpolation, escaped identifiers/keys, computed keys and runtime/deployment configuration need separate semantic verification. The verifier is a separate focused command; the one added catalog-reader regression raises the inventory to 956 tests.
- Prior reconciliation increment: all 37 historical unchecked review patches now carry evidence and a disposition (28 implemented/superseded, nine with remaining deferred work). `LoadTokenStateAsyncPreservesCompleteFoldedBundleAcrossOrderingAndDuplicateVariants` compares the entire serialized public bundle for ordered, reversed and shuffled/duplicate histories covering adjustment, contributor confirmation, submission, approval and approved correction, with explicit transition assertions and Fresh catalog metadata. The full Server.Tests lane passed 479/479. Deployed capability inventory, non-Fresh diagnostic distinctions, live topology, durable writes and concurrent single-use proof remain open.
- Prior reconciliation post-review verification: the focused Server.Tests Debug build with `--no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors; Server.Tests reran 479/479 and ArchitectureTests reran 56/56 after the review patches. The four other test projects are unchanged since their passing invocations. The one added Fact now exercises nine named independent ordering combinations with expected audit provenance, distinct actors, sensitive-policy comments and canonical unavailable AI metrics.
- Cumulative coverage includes the concrete loader, canonical projection delivery, four-route HTTP no-disclosure, correction-scope contract metadata, serialized scope replay, and legacy mismatched-retry rejection.
- The administrator HTTP flows inject `HttpContext.User` through `ClaimsStartupFilter` and substitute `ScriptedAccessGuard`; they prove claim-to-accessor and authorization-context mapping only, not authenticated deployment. At that prior verification baseline, the host registered no authentication scheme. The current SDK registers workload and sidecar authentication; the 2026-10-08 HTTP journeys are blocked by its authorization policies, as recorded above.
- The configured-port tests simulate `Connection.LocalPort` before the production guard in TestServer and bind the literal `Timesheets:InternalSurface:Port` setting through `IWebHostBuilder.UseSetting`. The endpoint fitness test ties the AppHost's `Timesheets__InternalSurface__Port` export to the Timesheets resource's unconditional, distinct 8080/8081 listeners and the host options section. They prove host configuration/middleware behavior, not a real listener, network isolation, durable command submission, or concurrent single-use consumption. Adding an in-memory configuration source at the deferred minimal-host callback initially failed with a disposed ConfigurationManager; the setting-based fixture passed.
- Historical runtime smoke (2026-10-03): the first Aspire start timed out during restore. A subsequent no-build start succeeded, but `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18 because Timesheets failed to start; describe showed Timesheets Finished and security Unhealthy. `aspire stop` completed cleanup. No passing current-topology evidence is claimed.
- Runner guidance: the current `dotnet test tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj --no-build` invocation exits 1 with Microsoft.Testing.Platform's unsupported VSTest-target error on .NET 10; README now uses the verified direct executables by default. Runner configuration remains unchanged.
- Prior runtime check (2026-10-04, repeated before this reconciliation increment): `aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` succeeded; `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18. Describe reported Timesheets Finished (exit 134); `aspire logs timesheets --tail 18` captured `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. All commands used the explicit AppHost path and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`; `aspire stop` succeeded. This environment conflict does not explain the historical October 3 failure or prove a running magic-link journey.

- Prior alternate-configuration increment independent review patched four findings (unrelated URLs, ancestor conditions, disabled-region semicolons and interpolation controls) and grouped two verified semantic-analysis limitations into one deferred ledger item. Separate focused reproductions confirmed escaped and concatenated keys plus an escaped property identifier still pass the lexical check, with exact source-byte restoration. The final ArchitectureTests regression covers both element and ancestor conditions; its post-review lane has 56 tests.

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-22
**Engineer:** QA automation (generation only — no code review)
**Feature under test:** `EventStoreMagicLinkConfirmationCapabilityStateLoader` + rebuildable token-hash → capability index

## Framework Detected

- .NET 10 / xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `6.0.0-rc.1` (existing project conventions reused — no new framework introduced).
- No UI/HTTP surface exists in this backend-only story (the HTTP-boundary `WebApplicationFactory` proof is owned by Story 3.7), so "E2E" here means **API/service-level loader tests** exercised through the real EventStore SDK seams via scripted gateway/read-model stubs.

## Generated Tests

All added to `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`
(extending the dev's existing 16 loader tests — no rewrite).

### Gap tests applied (8 new cases)

- [x] `LoadTokenStateAsync_fails_closed_for_blank_token_without_any_read` (Theory: `""`, `"   "`) — **AC3**: blank/malformed token collapses to the identical opaque state *before* any index/EventStore read.
- [x] `LoadTokenStateAsync_fails_closed_when_token_hashing_rejects_malformed_token` — **AC3**: `DeriveHash` `ArgumentException` path fails closed with no reads (previously uncovered catch branch).
- [x] `LoadTokenStateAsync_returns_folded_expired_state_proving_index_is_not_expiry_authority` — **AC3**: parity with revoked/used — expiry truth lives only in the folded aggregate; the index stays a non-authoritative candidate resolver.
- [x] `LoadActivityTypeCatalogAsync_folds_fresh_tenant_catalog_for_admin_paths` — **AC1/AC4**: admin (tenant-less) catalog happy path returns a Fresh folded catalog from the domain-wide read.
- [x] `LoadActivityTypeCatalogAsync_folds_only_tenant_scoped_items_and_reflects_rename_and_deactivation` — **AC2**: project-scoped activity types excluded; rename/deactivate fold applied deterministically.
- [x] `LoadActivityTypeCatalogAsync_folds_events_across_continuation_pages` — **AC2**: the `ReadAllEventsAsync` continuation-token paging loop accumulates every page (previously entirely uncovered).
- [x] `LoadCapabilityAsync_folds_terminal_state_for_admin_revoke_and_expire_paths` — **AC1**: admin revoke/expire observe an already-terminal capability as terminal, not as a fresh issue.

### Test-harness extensions (no production code changed)

- `ThrowingTokenGenerator` stub (proves the malformed-token catch path).
- `ScriptedGatewayClient.WithPagedStream(...)` — multi-page continuation-token support so the paging loop is exercised.
- New deterministic event builders: `Expired()`, `SecondActivityCreated()`, `ProjectScopedActivityCreated()`.

## Coverage

- **Loader (`LoadTokenStateAsync` / `LoadCapabilityAsync` / `LoadActivityTypeCatalogAsync`):** all three methods now have happy-path + fail-closed + admin-path + determinism + paging coverage.
- **Acceptance-criteria mapping:**
  - **AC1** ✅ valid token resolves + folds capability/Time Entry/Fresh catalog; admin paths fold existing state.
  - **AC2** ✅ fold determinism across orderings/duplicates, catalog filtering, multi-page rebuild, index rebuild from `Issued`.
  - **AC3** ✅ full invalid set externally indistinguishable: blank, malformed, hashing-throw, unknown-hash, missing-aggregate, hash-mismatch, cross-tenant, missing-Time-Entry, read-throw, revoked, used, expired.
  - **AC4** ✅ fail-closed on missing trusted tenant + explicit Unavailable catalog on read failure.
- **Server.Tests lane:** **403 passed / 0 failed / 0 skipped** (was 395; +8 new cases).

## Verification

```
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/...csproj -warnaserror -m:1 /nr:false
→ 0 Warning(s), 0 Error(s)

DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
→ Total: 403, Errors: 0, Failed: 0, Skipped: 0
```

(`dotnet test` is blocked by local VSTest socket permissions — `SocketException (13): Permission denied` — so the README direct xUnit v3 executable fallback was used, per the story's Debug Log convention.)

## Notes / Deferrals

- The full HTTP-boundary equivalence test (`WebApplicationFactory` / `Mvc.Testing`) remains deferred to **Story 3.7** — the Timesheets module has no runtime host fixtures yet (all read-side readers are `Unavailable` stubs). Service-level no-disclosure equivalence across the AC3 invalid set is already proven by `MagicLinkConfirmationCapabilityCommandServiceTests`.
- Tests are independent (each builds its own scripted gateway/read-model), use deterministic fixed timestamps (no `Date.now`/sleeps/waits), and assert end-state (folded state / freshness), not mock call counts.

## Next Steps

- Run the suite in CI alongside the existing ArchitectureTests / Contracts / Projections / Integration lanes.
- When Story 3.7 lands the runtime host, lift the loader equivalence proofs to the HTTP boundary.

## Spec-15 checkout-path reproduction commands (2026-10-08)

Before rebuilding, the previous Debug binary failed one selected case on the harmless framework content-root message:

```bash
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics'
```

After rebuilding, all four selected cases stop at unexpected 401 responses before the corrected assertions:

```bash
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

Create that empty scratch directory before either invocation. The prior binary's failure is dated evidence, not reproducible from the current rebuilt output. The retained [privacy assertion verifier](3-6-privacy-assertion-verifier/Program.cs) and its command above reproduced 144 helper checks on 2026-10-08 without changing authentication; the current in-lane theory supersedes it.

The same four scratch-root HTTP cases were rerun on 2026-10-09 from the spec-16 pre-review output with the second command above: four failures, all `HttpStatusCode.Unauthorized`, before any privacy log loop. The spec-16 420-case in-lane theory superseded the retained verifier; the current spec-17 theory has 432 cases and also checks the exact fixture comment in every category.

Independent review strengthened eight test/record findings and deferred one pre-existing assertion false-positive risk: generic `60` substring checks can reject permitted correlation or timestamp data. The corresponding field-aware assertion follow-up is recorded in the spec-16 deferred-work section. These are test controls, not current HTTP or persisted-state evidence.
