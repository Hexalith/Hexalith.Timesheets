# Five deferred magic-link EventStore implementation handoffs

Verification baseline: clean main `4d3c205c940aa562304c5cb5917d761196342ae9`, 2026-10-21.
Scope and review triage: [build spec](../spec-close-five-eventstore-handoffs.md).

## Implementation evidence

| Original deferred item | Delivered behavior and relevant evidence |
| --- | --- |
| Loader fold integrity | `EventStoreMagicLinkConfirmationCapabilityStateLoader` compares full envelopes, requires contiguous sequences and unique nonblank message IDs, and has no inert `OrderedDistinct` path. Its unit suite covers ten conflicting envelope fields, valid replay equivalence and repeated/blank IDs. `Concrete_loader_cross_tenant_candidate_is_indistinguishable_from_an_unknown_token_on_every_route` covers all four actual HTTP routes with a concrete cross-tenant candidate. |
| Bounded cross-tenant index | V7 `MagicLinkTokenHashCapabilityIndexTreeBuilder` and `TreeReader` use numeric trie nodes and bounded candidate/tenant-roster pages instead of one global dictionary. `MagicLinkNumericIndexTests` and `MagicLinkGlobalIndexEpochTests` cover 10,000 high-entropy candidates, concentrated same-hash collisions, long owner IDs, directory selection and tenant replacement/pruning. This closes the original bounded-storage alternative. Automatic terminal-event garbage collection is not claimed. |
| Production recording writer | `TimeEntryRecordSubmissionService`, `ExternalContributionEndpoints` and `MagicLinkEventStoreDomainProcessor.ProcessRecordAsync` compose with the existing keyed processor. `External_recording_route_commits_owner_state_before_magic_link_use`, retry/concurrency cases and record/submission tests assert committed EventStore actor state and exact readback. Fail-closed production authorization still requires deployment-owned adapters. |
| Production capability and Time Entry projections | The composed host registers the projection assembly. `MagicLinkConfirmationCapabilityProjectionHandler` and `TimeEntryEvidenceProjectionHandler` materialize stored wrappers through the SDK. `ComposedNamedCapabilityRoutePersistsIssuedAndUsedSelectedRow`, `ComposedNamedRebuildCommitsAggregateOwnedTimeEntryEvidence` and `ComposedNamedRebuildRetainsTenThousandEventTimeEntryHistory` assert selected stored state through mapped delivery/rebuild routes. Automatic subscriber delivery is a separate open item. |
| Capability helper collisions | `MagicLinkConfirmationCapabilityProjection.Project` selects the requested capability before deduplication and terminal-order checks. Unrelated raw/stored issuance and use/revoke/expire events may share local sequence numbers without colliding. Selected contradictory evidence, wrong tenant/Time Entry and illegal terminal ordering still fail closed. |

The first four implementations predate this increment and were revalidated. The fifth required the bounded helper fix. Original ledger evidence is retained alongside dated resolution annotations; deployed inventory, legacy certification, subscriber delivery, catalog rebuild fencing and current-graph AppHost acceptance remain separate work.

## Regression verification

Matching Debug/source restore and solution build succeeded with zero build warnings/errors:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Works and the imported catalog both select EventStore `3.126.0`; no version override was needed.

The focused helper command ran against the new tests before and after the correction:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkConfirmationCapabilityProjectionTests'
```

Before correction: 38 total, 13 failed. After the initial correction: 38 passed. Review added an unknown-payload control and strengthened the stored-owner and replay fixtures; the reviewed focused run passes **39/39**, including 12 mixed-stream combinations (three terminals, raw/stored, both orders) with repeated selected deliveries, absent selected capability despite unrelated contradictory evidence, and six matching-ID foreign-tenant/wrong-owner refusal cases interleaved with unrelated collisions. Existing selected contradiction and identical-replay controls remain green.

The first complete Server run found a preexisting source-fitness false positive: the preceding management-boundary change made the isolated Development fixture implement the server's tenant-validator interface, whose parameter uses the server-owned authority context. The source guard now permits that exact fixture file, retaining the public Contracts/Client ban and excluding other host/presentation files. Runtime authorization and its fixture-admission predicates are unchanged. The initial failed run is not counted as final success.

## Final default suites

After the review corrections and final zero-warning solution build, each individual executable below ran with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home` and exited zero. The path pattern is `tests/<Project>/bin/Debug/net10.0/<Project>`; no solution-level test command was used.

| Project | Passed | Skipped | Seconds |
| --- | ---: | ---: | ---: |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.660 |
| Hexalith.Timesheets.Server.Tests | 564 | 0 | 2.117 |
| Hexalith.Timesheets.Projections.Tests | 569 | 0 | 610.798 |
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 13.243 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.373 |
| Hexalith.Timesheets.IntegrationTests | 848 | 5 | 602.021 |

Total: **2,257 tests, 2,252 passed, five intentional skips, zero failures/errors/not-run**. The skips retain the existing infrastructure/performance reservations and opt-in performance/live-Dapr lanes. The final helper selection also passed 39/39. Earlier partial/superseded runs are excluded from these totals.

`git diff --check`, the exact-five ledger addition check and `npx --no-install commitlint --edit /tmp/timesheets-five-eventstore-commit-message.txt` pass. Publication uses the requested main branch.

## Review

The configured Blind Hunter review returned four findings. All were patched: preserve unknown-payload normalization, use valid owner IDs on stored terminal fixtures, interleave unrelated collisions in selected-scope refusal tests, and repeat selected deliveries in the mixed-stream matrix. The independent recheck found no concrete defect remaining. All four findings and dispositions are recorded in the build spec; no new deferred item was added. Pre-review long-running suites were superseded and are not counted as final verification.

## Runtime boundary

The repository's isolated runtime command was attempted before code changes:

```bash
python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-five-eventstore-baseline
```

It exited 1 during preflight: `A Dapr application is already running or inventory is unsafe; no AppHost started`. Sanitized temporary receipts are under `/tmp/timesheets-five-eventstore-baseline/run-20lma66g`. No fixture resources were started and the existing foreign workload was preserved. This run supplies no new live-runtime or deployed-acceptance evidence. The release verdict remains **FAIL**.
