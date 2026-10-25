# Five read-model boundary deferrals — verification

Date: 2026-10-25. Source baseline: `6fe2d21c132a4dcc286fdeb0d6b6e84db50c3c77` plus the implementation worktree. Scope: [the approved spec](../../spec-five-read-model-boundary-deferrals.md), DW-26, DW-30, DW-35, DW-39, and DW-140.

Initial broad receipts below predate the corrections in [Post-review focused verification](#post-review-focused-verification). Their counts remain dated evidence; the later focused receipts have their own scope.

## Restore and build

Both commands completed with exit 0 in the required matching Debug/source mode:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

The final build passed in 10.08 seconds with 0 warnings and 0 errors. [Restore output](restore.txt.gz) and [final build output](build.txt.gz) are retained. The first build failed with 10 errors because the loader needed the canonical catalog namespace and existing Projections tests had relied on the removed transitive Server reference. The catalog using and explicit test-only Server references fixed those compile errors; the subsequent 26.79-second build and final 10.08-second build both passed.

## Individual full suites

Each executable was launched separately; these in-process suites ran concurrently. Every command below exited 0. Counts exclude the focused runs so no test is double-counted.

| Executable | Passed | Failed | Skipped | Total | Seconds | XML | Output |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| ArchitectureTests | 118 | 0 | 0 | 118 | 66.068 | [XML](ArchitectureTests.xml.gz) | [output](ArchitectureTests.txt.gz) |
| Contracts-Tests | 108 | 0 | 0 | 108 | 1.264 | [XML](Contracts-Tests.xml.gz) | [output](Contracts-Tests.txt.gz) |
| Projections-Tests | 727 | 0 | 0 | 727 | 1167.391 | [XML](Projections-Tests.xml.gz) | [output](Projections-Tests.txt.gz) |
| Server-Tests | 693 | 0 | 0 | 693 | 9.077 | [XML](Server-Tests.xml.gz) | [output](Server-Tests.txt.gz) |
| IntegrationTests | 918 | 0 | 5 | 923 | 1114.923 | [XML](IntegrationTests.xml.gz) | [output](IntegrationTests.txt.gz) |
| Works-Tests | 76 | 0 | 0 | 76 | 0.540 | [XML](Works-Tests.xml.gz) | [output](Works-Tests.txt.gz) |

Aggregate: **2640 passed, 0 failed, 5 skipped; 2645 total**.

Exact commands (exit 0 for each):

```bash
tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -result-xml /tmp/five-read-model-ArchitectureTests.xml > /tmp/five-read-model-ArchitectureTests.log 2>&1
tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests -result-xml /tmp/five-read-model-Contracts-Tests.xml > /tmp/five-read-model-Contracts-Tests.log 2>&1
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -result-xml /tmp/five-read-model-Projections-Tests.xml > /tmp/five-read-model-Projections-Tests.log 2>&1
tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -result-xml /tmp/five-read-model-Server-Tests.xml > /tmp/five-read-model-Server-Tests.log 2>&1
tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -result-xml /tmp/five-read-model-IntegrationTests.xml > /tmp/five-read-model-IntegrationTests.log 2>&1
tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests -result-xml /tmp/five-read-model-Works-Tests.xml > /tmp/five-read-model-Works-Tests.log 2>&1
```

Declared default skips (all in IntegrationTests):

- `Hexalith.Timesheets.IntegrationTests.PerformanceEvidenceLaneTests.PerformanceLaneIsReservedForLaunchLatencyEvidence`: Performance evidence requires realistic EventStore-backed command/report fixtures in a later data-bearing story.
- `Hexalith.Timesheets.IntegrationTests.CaptureAndGovernanceCommandPerformanceLaneTests.CaptureAndGovernanceCommandAcknowledgementsRecordNfr10P95Evidence`: Set TIMESHEETS_PERF=1 to run the command performance lane.
- `Hexalith.Timesheets.IntegrationTests.InfrastructureLaneTests.RuntimeIntegrationLaneIsReservedForEventstoreStateStoreEvidence`: Infrastructure-dependent EventStore/Dapr/Aspire tests are isolated until a data-bearing story adds runtime fixtures.
- `Hexalith.Timesheets.IntegrationTests.MagicLinkLiveDaprTests.RecordedMagicLinksCommitOneUseAndSurviveActorRestart`: Set TIMESHEETS_LIVE_DAPR=1 to run the local EventStore/Dapr acceptance journey.
- `Hexalith.Timesheets.IntegrationTests.ReportExportDashboardQueryPerformanceLaneTests.ReportExportAndDashboardQueryLatencyRecordsNfr11P95Evidence`: Set TIMESHEETS_PERF=1 to run the report/export/dashboard performance lane.

The perf lanes were not opted in with `TIMESHEETS_PERF=1`, and the live Dapr lane was not opted in with `TIMESHEETS_LIVE_DAPR=1`. These skips are separate from test failures and provide no runtime or deployed acceptance.

## Focused compatibility and bootstrap checks

- Compatibility: 16/16 passed, 0 failed/skipped, 0.203 seconds; [XML](contracts-focused.xml.gz), [output](contracts-focused.txt.gz). The suite checks all 26 Server type forwards and their new owning assemblies, every one of the fourteen pure index JSON shapes, eight persisted wrapper full names and `event` fields, the unchanged catalog key/store/slot/projection identity, domain/publication constants, and the fail-closed policy default.
- Bootstrap/replay class: 63/63 passed, 0 failed/skipped, 18.811 seconds; [XML](bootstrap-focused.xml.gz), [output](bootstrap-focused.txt.gz). Thirty added cases cover both bootstrap paths, direct shards 00/FF, populated/empty/corrupt/JSON-null/malformed rows, selected-shard early-return conflicts, preserved physical bytes and pending recovery state, absent v7 root/format publication, IO/JSON failures and cancellation followed by successful retry, and all 256 direct reads under a measured ceiling of sixteen concurrent reads. Existing fresh success and replay/large-index coverage remain enabled.

```bash
tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests -class Hexalith.Timesheets.Contracts.Tests.ReadModelContractCompatibilityTests -xml /tmp/five-read-model-contracts-focused.xml > /tmp/five-read-model-contracts-focused.log 2>&1
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class Hexalith.Timesheets.Projections.Tests.MagicLinkGlobalIndexEpochTests -xml /tmp/five-read-model-bootstrap-focused.xml > /tmp/five-read-model-bootstrap-focused.log 2>&1
```

The focused runner accepted `-xml` with a deprecation notice recommending `-result-xml`; full suites use `-result-xml`.

## Dependency modes

[The evaluated reference graph](dependency-graph.json) captures Contracts, ReadModels, and Projections in Debug/source and Release/package modes. It records the exact `dotnet msbuild -nologo -p:Configuration=<Debug|Release> -getProperty:UseHexalithProjectReferences,NuGetAudit -getItem:ProjectReference,PackageReference` arguments for each project. These evaluations do not restore or replace the Debug assets.

Contracts has only the inherited build-time MinVer package, with no SDK/project reference. ReadModels references Contracts and the conditional EventStore Client/Contracts SDK seams. Projections references Contracts and ReadModels, and intentionally retains EventStore DomainService composition. Both static and evaluated architecture checks walk the Timesheets-owned closure and cannot reach Server; platform runtime transitive dependencies are excluded from that module-edge rule. Full architecture results also cover the updated 16-project solution inventory and relocated privacy source scans.

## Stored payload registration and serialization

The current host still supplies the Server and Projections assemblies to `AddEventStoreDomainService`. Moving a type does not make it appear in `Assembly.GetTypes` on Server; that behavior is not assumed here. The unchanged `AddTimesheetsMagicLinkEventStoreProcessing` explicitly registers the keyed `MagicLinkEventStoreDomainProcessor`, which constructs the Stored* payloads directly. SDK `DomainServiceWireResult.FromDomainResult` serializes `payload.GetType()` using its unchanged FullName, without scanning payload assemblies. `DomainStateReplay.Rehydrate<TState>` discovers typed Apply parameters: TimeEntryState and MagicLinkCapabilityState retain their wrapper overloads. The loader and projection normalizers use explicit `typeof`/generic wrapper types. The SDK aggregate-registration seam additionally enrolls event types and their defining assemblies through `EventPayloadEvolutionRegistration.AddApplyEventTypes`; no ReadModels assembly scan was added to the host.

The full Integration XML records the existing in-process registration/persistence support:

| Test | Passing cases |
| --- | ---: |
| `ProtectedProcessRouteDispatchesToTheKeyedTimesheetsProcessor` | 1 |
| `EventStoreActorStateStoreCommitsPairAndReloadsItAfterActorRecreation` | 4 |
| `ProposedEntryTerminalMarkerSurvivesActorRecreation` | 2 |
| `IssuedCapabilitySurvivesActorRecreationAndRejectsDistinctReissue` | 2 |
| `ExternalRecordingRouteCommitsOwnerStateBeforeMagicLinkUse` | 1 |
| `ConfirmAndAdjustCommitUseWithMatchingEffectInTargetStream` | 1 |
| `IssueReturnsTokenOnlyAfterIssuanceIsStoredInCapabilityStream` | 1 |

These checks exercise the existing host/kernel registration, SDK actor state-store end-state and actor recreation, stored issuance/recording/submission/use/effect/terminal payloads, and composed projection/loader readback. They remain in-process evidence.

## Long-suite diagnostics

The full runs exceeded the dated 2026-10-20 timings. The current Projections suite contains 727 cases versus 437 in that receipt, with its 10,000-owner independent-partition test passing in 1112.591 seconds. The current Integration suite contains 923 cases versus 732 in that receipt; its 9,998-extra-owner composed shared-source test passed in 939.124 seconds, and normal 10,000-candidate delivery passed in 74.648 seconds. These default-suite timings are not an opt-in performance acceptance result.

Nonfatal mini dumps of the two owned test processes were collected with the already installed `dotnet-dump` tool. Their short diagnostic captures explain the createdump messages in the retained full output; the test processes remained alive. No dump binaries are committed. Managed stack output is retained for [Projections](projections-diagnostic-stacks.txt.gz), [the later Projections sample](projections-repeat-diagnostic-stacks.txt.gz), and [Integration](integration-diagnostic-stacks.txt.gz). The first Projections sample included xUnit invocation/continuation frames around the small terminal-before-issuance rebuild and captured-owner checkpoint commit; the later sample showed captured-marker chunk deserialization and owner-sequence seeding. The new direct-shard guard was absent from these sampled stacks. Integration’s mini dump exposed runner/host waits and native/background-GC threads but could not identify its active async test.

The exact small test sampled in the Projections continuation was reproduced separately under a 90-second bound and passed 1/1 in 0.575 seconds, exit 0: [XML](small-rebuild.xml.gz), [output](small-rebuild.txt.gz). Existing fake-store absence branches were inspected and return `(null, null)` or delegate to the SDK fake, with no evidence of empty-ETag/non-null-default rows for absent direct shards.

```bash
timeout 90s tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*StreamedRebuildJoinsTerminalOwnerBeforeIssuanceOwner' -result-xml /tmp/five-read-model-small-rebuild.xml > /tmp/five-read-model-small-rebuild.log 2>&1
dotnet-dump collect --process-id 35478 --type Mini --output /tmp/five-read-model-35478.dmp
dotnet-dump analyze /tmp/five-read-model-35478.dmp -c 'clrstack -all' -c exit
dotnet-dump collect --process-id 35492 --type Mini --output /tmp/five-read-model-35492.dmp
dotnet-dump analyze /tmp/five-read-model-35492.dmp -c 'clrstack -all' -c exit
dotnet-dump collect --process-id 35478 --type Mini --output /tmp/five-read-model-35478-repeat.dmp
dotnet-dump analyze /tmp/five-read-model-35478-repeat.dmp -c 'clrstack -all' -c exit
```

## Scope and remaining acceptance

No deployed state, migration, dependency version, submodule pointer, AppHost, or topology was changed. No runtime lifecycle operation was attempted. Direct legacy writers still require inventory before deployment; production authority, current-graph runtime qualification, deployed continuity, and certified shared rebuild remain unfinished. The launch verdict remains **FAIL**.

`git diff --check` and the new-source trailing-whitespace/final-newline check passed before final publication.

## Post-review focused verification

Date: 2026-10-25. These corrections follow review of the initial implementation. The frozen intent was not changed. Earlier full-suite receipts above were not rerun during this correction and do not claim coverage of the post-review tree.

- The release allowlist, exact archive/symbol counts, strict preflight/workflow count, candidate source mappings, publication expectations, and current CI/CD documentation now include the approved sixth package, `Hexalith.Timesheets.ReadModels`. Its isolated consumer exercises the unchanged legacy shard address and checks the ReadModels assembly version. Release-tooling fixtures cover the required package, omitted-package refusal, Server's shipped ReadModels dependency, stale five-package preflight refusal, and wrong transitive ReadModels versions. All existing archive/provenance/source/version/security refusals remain enabled.
- Each direct shard store invocation now runs inside an async task, so a synchronous GetAsync throw becomes a faulted task and `WhenAll` drains all started reads before refusal. Both bootstrap paths have a controlled gated synchronous-throw regression checking that refusal waits for held reads and no read remains active before retry.
- The observer no longer uses `Task.Delay(1)`. Concurrency assertions accept a valid peak of one and retain the ceiling of sixteen; explicit gates control the synchronous-fault test.
- Both bootstrap paths now cover persisted JSON-null single-root StateKey rows and single-root IO/JSON read failures/cancellation. Refusal compares source bytes and ETags plus PendingFoldFailures/ParkingEvidence to captured recovery status, and checks absent v7 root/format publication.

| Focused check | Result | Exit | Receipt |
| --- | --- | ---: | --- |
| Projections.Tests project build, Debug/source, no restore | 0 warnings, 0 errors; 5.29 s | 0 | [output](post-review-build.txt.gz) |
| Release-tooling unit suite | 72 passed; 0.139 s | 0 | [output](post-review-release.txt.gz) |
| Five affected bootstrap test methods | 40 passed, 0 failed/errors/skipped; 0.660 s | 0 | [XML](post-review-projections.xml.gz), [output](post-review-projections.txt.gz) |

Exact commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false > /tmp/five-read-model-post-review-build.txt 2>&1
python3 -m unittest discover -s scripts/tests -p test_release_tooling.py -v > /tmp/five-read-model-post-review-release.txt 2>&1
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*.DirectLegacyShardsRefuseBothBootstrapPathsWithoutLosingRecoveryEvidence' -method '*.SelectedLegacyShardCannotHideLastDirectLegacyShard' -method '*.EmptyDirectShardInventoryBootstrapsWithAtMostSixteenConcurrentReads' -method '*.DirectIndexReadFailureOrCancellationKeepsBootstrapRetryable' -method '*.SynchronousDirectShardFaultDrainsStartedReadsBeforeRetry' -result-xml /tmp/five-read-model-post-review-projections.xml > /tmp/five-read-model-post-review-projections.txt 2>&1
```

These checks made no registry writes and did not restore Release/package assets. An actual six-package pack and isolated NuGet-only consumer smoke run are separate follow-up verification. `git diff --check` passed. Launch remains **FAIL** and deployed/runtime acceptance remains unfinished.

## Parent verification after review fixes

The final Debug/source solution build passed with zero warnings/errors in 11.61 seconds: [output](parent-post-review-build.txt.gz). The complete affected bootstrap/replay class then passed **73/73**, with no failures/errors/skips, in 17.068 seconds: [XML](parent-post-review-projections.xml.gz), [output](parent-post-review-projections.txt.gz). This rerun covers the final async-invocation correction and all forty new refusal/recovery cases; the earlier six-suite totals remain explicitly scoped to the pre-correction run. Unaffected long suites were not repeated.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class Hexalith.Timesheets.Projections.Tests.MagicLinkGlobalIndexEpochTests -result-xml /tmp/five-read-model-parent-post-review-projections.xml
```

### Isolated Release/package validation

A detached temporary worktree at the full baseline revision received the current binary diff. Only its root-declared Builds and AI.Tools references were linked read-only for shared configuration/guidance; no submodule was initialized or updated. Release restore/build/pack used that separate worktree, preserving this checkout's Debug/source assets. All 491 tracked source/configuration files checked matched this checkout byte-for-byte. [The source manifest](package-source.sha256.gz) and [command/exit/timing receipt](package-verification.json) bind the tested inputs. The temporary worktree was removed after verification.

All four commands exited **0**, using test-only version `0.0.0-ci-read-model-boundaries`:

| Check | Seconds | Evidence |
| --- | ---: | --- |
| Matching Release/package solution restore | 2.907 | [output](package-restore.txt.gz) |
| Pack all six primary and six symbol archives | 14.625 | [output](package-pack.txt.gz) |
| Validate archive/dependency inventory and checksums | 0.049 | [output](package-validate.txt.gz), [checksums](package-checksums.sha256) |
| Restore, compile and execute all six isolated NuGet-only consumers | 20.807 | [output](package-consumers.txt.gz) |

The receipt retains literal paths and commands. Equivalently, from an isolated checkout with an empty candidate directory:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:MinVerVersionOverride=0.0.0-ci-read-model-boundaries -m:1 /nr:false
python3 scripts/pack-release-packages.py <candidate-directory> 0.0.0-ci-read-model-boundaries
python3 scripts/validate-nuget-packages.py <candidate-directory>
python3 scripts/validate-consumer-package-references.py <candidate-directory>
```

No package was published. Registry access supplied external restore dependencies only. Existing publication authorization/provenance/archive guards remain enabled. Review's unverified combined bootstrap latency question is retained as DW-190; it does not change the launch FAIL or establish current runtime acceptance.
