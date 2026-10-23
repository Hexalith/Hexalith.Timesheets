# Five magic-link runtime deferrals — 2026-10-23

Scope: [approved spec](../spec-five-magic-link-runtime-deferrals.md), Timesheets baseline `024d3aa24a804c5798017cd84b73041e4fb4dfec`. The original EventStore checkout/gitlink was `5ce5f1d84b88c808cc8a526b9decf149de977e55`. No Works source, AppHost topology, ServiceDefaults, domain policy, deployed data or story/sprint acceptance was changed. Launch remains **FAIL**, package currency remains **CONCERNS**.

## Implementation

- EventStore owns the Dapr handler and the new `AddEventStoreDaprServiceInvocation<TState>` API. Its three required arguments resolve DI state once per send, then select the target and credential from that same snapshot. The existing fixed-value overload and its literal-null source compatibility remain covered. The SDK replaces forged routing/credential headers, removes credentials when the current value is null/empty, and refuses blank/whitespace/control-character targets before transport.
- `TimesheetsEventStoreGatewayOptions` binds `Timesheets:EventStoreGateway`; DAPR configuration keys supply fallbacks. Newly resolved clients get the current validated endpoint; existing clients get the current target and credential on subsequent sends. Caller gateways, options, named transports and supporting Location policy retain their registrations. Gateway tests use isolated configuration rather than process-wide environment mutation.
- `MagicLinkSubmissionPollingOptions.ReadbackTimeout` defaults to two seconds, must be positive and at most thirty seconds, and links each stored-event read to the remaining submission/caller deadline. `WaitAsync` bounds non-cooperative transports. Issue/use retain their retry policy and transition now retains an outer submission deadline. Unknown outcomes remain false/opaque; caller cancellation propagates.
- Sealed v6 retries validate checkpoints/source digest, sealed page cursor, final format/root, exact bounded tenant rosters, token and owner membership, total leaf counts, node/page allocation and the promotion boundary. The traversal bound is derived from actual source paths, preserving valid long tenant identifiers. Corrupt state refuses promotion through the abort boundary; transport faults/cancellation retain retryability. Sealed retries append no stage pages.
- Exactly eleven obsolete domain-wide loader arrangements were removed. Aggregate streams, catalog fixtures and all assertions remain.

## SDK prerequisite history and verification

The following records preserve intermediate source and publication states chronologically. The final identity and consumer checks appear under “Published SDK and final consumer verification” below.

The four-file SDK change was independently reviewed before pushing. Review identified an overload ambiguity and the risk of mixing reload snapshots; the generic API and one-state-per-send regression address both. The exact commit message passed the owning pinned commitlint CLI before the final commit/rebase/push. An initial missing-validator preflight mistake was corrected while the commit was still unpushed; no unvalidated commit was pushed.

The SDK remote advanced before publication. The sole own commit was rebased onto `bc61fe30b840fd7921097c8b36aa839684e0819e` and pushed without force as **`b6c903d0d8460e545cff4ffe0b93959619aa3a51`**. GitHub reported administrative rule-bypass messages on the authorized direct-main push for the PR/expected-status policies. Publication still requires successful exact-source CI, P1R qualification and commitlint; those release gates are not bypassed.

```bash
# Run from references/Hexalith.EventStore.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests
node_modules/.bin/commitlint --edit /tmp/five-sdk-commit-message.txt
```

Final rebased build: zero warnings/errors. Final rebased full client suite: **2,094 total, 2,092 passed, zero failed/errors/not-run, two skips requiring isolated Dapr endpoints; 33.254 seconds**. The SDK's prior-baseline run passed 1,926 total / two skips, but the rebased run supersedes it. Logs: `/tmp/five-sdk-rebased-build.log`, `/tmp/five-sdk-rebased-tests.log`, `/tmp/five-sdk-commitlint.log`.

The first rebased build found an incomplete existing `/tmp/dotnet-cli-home/.nuget/packages` extraction (`NU5037`, missing `microsoft.testing.extensions.codecoverage/18.12.0` nuspec). Selecting the existing complete cache with `NUGET_PACKAGES=/home/administrator/.nuget/packages` resolved the blocker without changing dependencies or weakening checks.

Exact-source hosted runs:

- [CI 39926996311](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39926996311)
- [P1R qualification 39926995931](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39926995931)
- [Commitlint 39926996366](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39926996366)

The initial SDK CI was subsequently canceled because upstream main advanced to `cd193902a52abb02572528958436d79a8ae25b9a`. Git ancestry confirms that successor contains `b6c903d0d8460e545cff4ffe0b93959619aa3a51`; its additive authoritative-content change does not modify the routing files or release workflow. The local gitlink remains the tested `b6c903d0d8460e545cff4ffe0b93959619aa3a51`. Successor source checks are [CI 39928722031](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39928722031), [P1R 39928721698](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39928721698) and [commitlint 39928721998](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39928721998). A release must satisfy the workflow's exact live-main rules; the canceled run is not a successful prerequisite.

Publication and actual package identity are pending. The consumer pin remains unchanged at `3.126.0`; source-reference success does not establish compatibility with the unpublished API.

### Repaired publication source

The `cd193902a52abb02572528958436d79a8ae25b9a` and `98e5f45ac300f08c3b8592f6c7a858d0e0535462` CI runs were also superseded by upstream pushes. The current source is `93be37d26c15ba1bb951f23d13d7e1dd9df1ddde`. Its final runtime repair waits for this coordinator's promoted cleanup before captured-owner checkpoint seeding. Required source runs are [CI 39935474875](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39935474875), [P1R 39935474508](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39935474508) and [commitlint 39935475002](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39935475002).

The local SDK checkout was fast-forwarded to that exact source without a source edit, SDK push, or nested submodule initialization. Its Debug/source client-test build passed with zero warnings/errors in 8.55 seconds. Both `OwnerSequenceHandoffWaitsForThisCoordinatorsPromotedCleanup` rows passed (2/2, 0.327 seconds). The matching Timesheets solution build passed with zero warnings/errors in 8.26 seconds, still with explicit source version `3.126.0` while publication is pending. Targeted consumer verification passed:

| Method | Suite | Result |
| --- | --- | --- |
| `Applied_rebuild_selects_complete_new_generation_and_prunes_missing_capability` | Projections | Passed |
| `Streamed_rebuild_joins_issuance_and_time_entry_terminal_once` | Projections | Passed |
| `CorruptSelectedStorageCanBeRebuiltWithValidPendingJournal` | Projections | Passed |
| `NormalDirectoryCandidateRemainsResolvableAfterTenantRebuild` | Integration | Passed |

The projection selection passed 3/3 in 0.585 seconds; composed index readback passed 1/1 in 2.246 seconds, with no failures/errors/skips/not-run. These cover successive promotion/pruning, multiple owner streams, journal recovery and concrete-loader readback. Logs/XML are `/tmp/five-sdk-cleanup-source-{build,tests}.log`, `/tmp/five-sdk-cleanup-source-tests.xml`, `/tmp/five-repaired-sdk-timesheets-build.log`, and `/tmp/five-repaired-sdk-{projection,composed}-tests.{log,xml}`. This is focused evidence for the SDK runtime repair; the earlier full-suite table retains its original SDK identity.

## Timesheets verification

### SDK fixture-policy repair

SDK CI `39935474875` failed only `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, identifying synthetic credential assignments in the two new routing tests. Root reproduced the guard failure locally, prepared a repair, and retained its unused patch at `/tmp/five-sdk-unpushed-fixture-repair.patch`. That repair was never committed or pushed. Upstream independently corrected the fixture values in `f5d817251083d803b8700d01829119e59e98b1fd`; the SDK then advanced to `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3` with a Builds gitlink synchronization. Neither commit changes SDK runtime behavior. Root restored only its own two-file edits and fast-forwarded without initializing nested dependencies.

The unchanged compiled scanner's `FindViolations(string, string)` entry point reports zero findings across both upstream-repaired tests and `_bmad-output/implementation-artifacts/spec-generated-routing-test-credentials-2026-10-23.md`. The temporary probe lives at `/tmp/five-sdk-guard-probe/`; no scanner, allowlist or policy was changed. A full local scan against the superseded local candidate failed one compact dictionary initializer; that result is not a pass for the upstream repair. A subsequent candidate scan was stopped after upstream had already delivered its correction. Current publication gates are [CI 39941734190](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39941734190), [P1R 39941733739](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39941733739) and [commitlint 39941734230](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39941734230). Root made no additional SDK source push during this window.

On the upstream repair, the Debug/source Client test project built with zero warnings/errors in 9.24 seconds; routing classes passed 14/14 in 0.202 seconds. The complete tracked-content guard then passed 1/1 with zero findings/failures/errors/skips/not-run in 532.924 seconds. Commands use the same environment/cache prefixes recorded above, the Client executable with `-class '*DaprServiceInvocation*'`, and the Server executable with `-method '*TrackedReusableContent_DoesNotContainUsableSecrets'`. Logs/XML are `/tmp/five-sdk-fixture-upstream-{build,tests}.log`, `/tmp/five-sdk-fixture-upstream-tests.xml`, and `/tmp/five-sdk-e1-secret-guard.{log,xml}`. This guard result covers current tracked source `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`; its unchanged scanner assembly was built from `93be37d26c15ba1bb951f23d13d7e1dd9df1ddde` before the fixture-only repair.

All three exact-source push prerequisites subsequently passed on `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`: CI `39941734190`, P1R `39941733739`, and commitlint `39941734230`. After independently confirming live main still matched and no current release already existed, root dispatched [Release 39946310678](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39946310678) from main at 2026-10-23 11:28:25 UTC. No protection rule, scanner policy, publication freeze setting or source-proof gate was altered.

Matching Debug/source graph, explicit catalog version forwarding and warnings as errors:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.126.0 -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.126.0 -warnaserror -m:1 /nr:false
```

Restore and the restored-source solution build passed, with zero build warnings/errors. The focused runtime/loader/readback classes passed **164/164**, and migration passed **18/18**, including unchanged sealed retry with a 1,000-character tenant. Full-suite verification runs each executable individually and sequentially with the same `DOTNET_CLI_HOME` and `NUGET_PACKAGES` settings. Current completed receipts:

| Suite | Passed | Skipped | Seconds |
| --- | ---: | ---: | ---: |
| Contracts | 92 | 0 | 0.473 |
| Server | 641 | 0 | 1.934 |
| Architecture | 109 | 0 | 20.245 |
| Works | 76 | 0 | 0.281 |
| Projections | 627 | 0 | 835.736 |
| Integration | 892 | 5 | 766.391 |

The complete round passed **2,437 tests**, with **five expected opt-in/infrastructure skips**, **zero errors/failures/not-run**, **2,442 cases total**. Each executable exited zero. Logs use `/tmp/five-full-<Project>.log`. The Integration run remained CPU-active; a 282-ms diagnostic snapshot beyond twelve minutes identified the existing `ManagementRejectsSignedAssertionsForUnrelatedOperations` case before the successful summary. No test hang or failure was inferred. Neither diagnostic timing nor opt-in skips supplies runtime/performance acceptance. Package-mode CI must use the actual published SDK before this prerequisite can be closed.

The full Projections run exceeded twelve minutes while remaining CPU-active. A 158-ms minidump and managed-stack inspection identified the existing `Ten_thousand_valid_high_entropy_owners_produce_bounded_independent_partitions` test in SDK captured-owner promotion checks (`GetCapturedMarkerAsync`/`GetCapturedPositionAsync` through `CanPromotePartitionOwnersAsync`). The new v6 validator was absent from that active stack. The final timing is recorded with the suite result below; this diagnostic run is not a performance measurement. Stack diagnostics remain under `/tmp/five-projections-progress-{collect,stacks}.log`; no dump is committed.

Temporary controls compiled before their focused regressions ran. Every source file was restored in a `finally` block, followed by a successful zero-warning solution build:

| Removed behavior | Executed | Expected failures observed |
| --- | ---: | ---: |
| Reloading current gateway options, replaced with a fixed options snapshot | 1 | 1 |
| Stored-event `WaitAsync` bound | 6 | 6 |
| Independent stored-event timeout cancellation | 6 | 3 |
| Sealed-resume membership validation | 11 | 7 |

The three caller-cancellation cases still pass when only the independent readback timer is removed; their linked cancellation remains present. The sealed control still rejects source/digest corruption through earlier checkpoint checks; final root/node/roster/owner corruption requires the removed validation. Logs are `/tmp/five-mutation-{rotation,read-wait,read-budget,sealed-validation}-{build,test}.log`; `/tmp/five-runtime-mutations.json` retains the exact summaries.

## Final checkpoint audit correction

A final audit found that generic checkpoint range checks did not prove exact cursor values: a final retirement/page cursor or the initial root/retirement/page cursor could be changed while still satisfying the broad ranges. Five new damage cases reproduced the gap: 16 selected sealed-retry cases ran with five failures against the preceding implementation.

Sealed retries now reconstruct the initial checkpoint and every following checkpoint from the preceding validated root and the next bounded source tenant. The same pure page partitioner is used for normal stage appends and expected cursor calculation. Exact checkpoint equality includes the root, tenant, source generation/digest, ordinal, retirement cursor and page cursor. Reconstruction prepares in-memory operations only; it appends no sealed stage pages and adds no durable fields or identifier limits.

The final V6 suite passes **23/23**, zero failures/errors/skips/not-run, **2.475 seconds**, after a zero-warning focused project build. Two final negative controls compiled and then failed as intended: omitting deterministic checkpoint reconstruction produced **5 failures / 16 cases**; omitting staged-membership validation produced **5 failures / 16 cases**. Source bytes were restored in `finally` and rebuilt before the final passing V6 run. Receipts are `/tmp/five-checkpoint-{before,after}.log`, `/tmp/five-checkpoint-build.log`, `/tmp/five-final-{checkpoint-reconstruction,staged-membership}-{build,test}.log` and `/tmp/five-final-sealed-controls.json`.

The full-suite table above records the earlier complete round, before this V6-only audit correction and its five new tests. It must not be relabeled as a full-suite execution of the later V6 implementation. The SDK identity for that complete round remains `b6c903d0d8460e545cff4ffe0b93959619aa3a51`, using Debug/source version `3.126.0`. The production assemblies from that tested round were preserved from the Integration executable's output before any final solution rebuild:

| Assembly | SHA-256 |
| --- | --- |
| `Hexalith.Timesheets.Server.dll` | `0453a6ffc5943eae3e2183fcf37726b349fc379ce412e2956a1ed9a5533a12d5` |
| `Hexalith.Timesheets.Projections.dll` | `b77b37cddce2d18d1df3b705dc9b3ac382a19eafee08a0a8c31de9e343b9ec4f` |
| `Hexalith.EventStore.Client.dll` | `2c9b147b08871be04c4de5aae5d3bc8ca0bb52ce7ef3e8fa89fb0fdc66964a39` |
| `Hexalith.EventStore.DomainService.dll` | `f68c20cb5f43e3c203ebca3b86b76fe415fa02c14690e65d9f675145cf6bac1f` |

After restoring both final controls, the final matching solution build again passed with zero warnings/errors (**5.88 seconds**). The final runtime/loader/readback selection passed **164/164** (**0.768 seconds**) and V6 passed **23/23** (**2.141 seconds**), each with zero failures/errors/skips/not-run. Final receipts are `/tmp/five-timesheets-final-build.log`, `/tmp/five-server-final-focused.log` and `/tmp/five-migration-final-focused.log`.

The temporary copies/PDBs and manifest are at `/tmp/five-full-tested-assemblies/`. Per root audit direction, the focused final V6 suite/controls cover the correction; unaffected large suites are not repeated solely to reproduce an assembly-version pin.

## Published SDK and final consumer verification

[SDK Release 39946310678](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39946310678) succeeded, including the production approval, all fourteen NuGet pushes, multi-architecture container publication and [release v3.127.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.127.0). The version tag, downloaded Client package's repository metadata and selected SDK gitlink all resolve to **`e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`**. Exact-source CI/P1R/commitlint remain green. No additional SDK source push or protection change was needed.

The published Client release asset was downloaded to `/tmp/five-sdk-published-3.127.0/`. Its SHA-256 is `bad9973f46b62b0f830fb85648e57832596df8b1ab9ad08d5f1c240c6efacf23`. `dotnet-inspect` confirms both the generic DI-state routing overload and the original fixed overload in this actual `3.127.0` package:

```bash
dnx dotnet-inspect -y -- member EventStoreServiceCollectionExtensions --package /tmp/five-sdk-published-3.127.0/Hexalith.EventStore.Client.3.127.0.nupkg -m AddEventStoreDaprServiceInvocation -n 80
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/home/administrator/.nuget/packages dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -warnaserror -m:1 /nr:false
```

The consumer's pre-import CPM override evaluates every catalogued EventStore package to `3.127.0`, without a command-line version override during Release/package-mode evaluation (`/tmp/five-published-cpm-evaluation.json`). Matching Debug/source restore/build passed; build had zero warnings/errors in 21.10 seconds. The final version-specific selections passed **945/945**, zero failures/errors/skips/not-run:

| Selection | Passed | Seconds |
| --- | ---: | ---: |
| Contracts, full | 92 | 0.807 |
| Server, full | 641 | 2.469 |
| Architecture, full | 109 | 17.031 |
| Works, full | 76 | 0.418 |
| V6 migration, full class | 23 | 3.570 |
| Affected projection handoff/journal paths | 3 | 1.059 |
| Composed 65-owner index readback | 1 | 4.064 |

Each built executable ran separately with `-result-xml`; the focused filters are the V6 class and the four exact methods listed under repaired-source verification above. Logs/XML use `/tmp/five-published-*`; restore/build use `/tmp/five-timesheets-published-{restore,build}.log`. The final script initially used the wrong Architecture executable path, stopped before launching it, then resumed with the correct path; no test failure was hidden. Earlier full large-suite evidence retains its own source identity. NuGet delivery availability and Timesheets package-mode hosted CI are checked separately before final completion; source success is not relabeled as a package-mode execution.

## Five ledger dispositions

The five selected entries are the two duplicate gateway configuration requests, the broad eight-goal carry-forward, the stored-event readback deadline and sealed-v6 retry validation. Root audited the complete implementation and executed matrix coverage; exactly five additive ledger notes now record these deliveries. The original ledger is preserved byte-for-byte apart from those five notes and the separate pre-existing follow-up required by review B1. Final workflow review triage and completed patch verification are recorded in the spec.

| Original entry identity | Recorded disposition |
| --- | --- |
| `source_spec: none`; gateway configuration summary in the 2026-09-14 Story 3.6 remediation split (baseline ledger line 103) | Closed by the SDK/current-options implementation: options binding, DAPR configuration fallbacks, current per-send credentials, new-client endpoint reload, preserved overrides and isolated configuration tests; published SDK `3.127.0`, source `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`. |
| `source_spec: none`; identical gateway summary in the 2026-09-15 remaining-work scope split (baseline line 143) | Close as the same delivered configuration request; preserve the September 15 re-verification evidence and cite the same runtime/SDK controls. |
| `source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`; “Eight prior-round deferred goals re-verified against the current tree and confirmed still accurate; not re-filed.” (baseline line 119) | Close as reconciled existing deliveries plus this increment's eleven obsolete fixture removals and gateway-options work. Preserve the supported pure Rebuild helper; do not close the separate shape-layering entry. |
| Backticked `source_spec: _bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`; “Decide and enforce a deadline for magic-link stored-event readback after terminal status.” (baseline line 627) | Close by the positive two-second/default, thirty-second/maximum option, linked cancellation, every-read WaitAsync, successful evidence/cancellation/non-cooperative regressions and falsifying timer/wait controls. This selects a bounded local runtime policy, without deployed latency acceptance. |
| Backticked `source_spec: _bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`; “Verify sealed v6 migration resume against a changed source or damaged staged root.” (baseline line 709) | Close by sealed retry revalidation before promotion, unchanged/long-tenant success, source/checkpoint/root/node/roster/owner/format refusal, retryable transport/cancellation and zero sealed-stage appends. Deployment inventory and operator binding remain separate. |

The eight-goal carry-forward combines delivered work with the remaining fixture/configuration residuals completed here. Existing provenance is retained in [runtime correctness carry-forwards](five-runtime-correctness-carry-forwards.md), [EventStore handoffs](five-eventstore-handoffs.md) and [integration handoffs](five-integration-handoffs.md): concurrency/envelope/null guards, telemetry, gateway support, ETag-aware fixtures, cross-tenant controls, dead-code cleanup, named slot/logger wiring and the explicitly retained pure Rebuild helper. The separate shape-layering, fragment retirement and deployed qualification entries remain open.

## Runtime limits

Using the applicable Aspire lifecycle skill, `aspire ps --format Json` identified two foreign AppHosts. The exact Timesheets AppHost was started with `aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`, inspected with exact-target `aspire describe`, and stopped successfully with exact-target `aspire stop`. The EventStore project resource finished while Timesheets was running. This is baseline inspection only, with no successful live journey or current-graph/deployed qualification claim. Foreign AppHosts were not stopped. Opt-in live/performance skips supply no acceptance evidence.


## Review and registry follow-through

All three final BMAD layers completed. The edge-case layer returned no findings. Root classified the blind layer's ten findings and the verification layer's one finding separately in the spec, grouped only the duplicate multi-tenant coverage gap, and requested seven bounded test/documentation patches from the original implementer. Review B1 identifies a pre-existing abort-boundary problem in the unchanged selected-source metadata guards; one new open ledger entry records it separately from the five delivered entries. No production change is proposed for that pre-existing edge in this increment.

NuGet subsequently served all seven direct/supporting EventStore packages required by this consumer (`Client`, `Contracts`, `DomainService`, `Testing`, `Aspire`, `Server`, `ServiceDefaults`) at `3.127.0`. Inspection directly from `Hexalith.EventStore.Client@3.127.0` confirms the two routing overloads as well; its receipt is `/tmp/five-sdk-nuget-api-3.127.0.log`. Registry propagation was observed rather than assumed from the release tag. Final post-review test checks and Timesheets package-mode CI remain pending.


## Completed review patches

The seven accepted patch groups are implemented in the two affected test classes and README. No production source changed. The original implementer passed 40 focused server cases and all 24 V6 cases; root independently rebuilt and verified the final tree. The added multi-tenant case covers three tenants and shared-hash ambiguity, all token/owner/roster memberships, promotion and unchanged append counts. Damage cases now require Building to be released. The cancellation case cancels a real caller while the read is awaited, requires OperationCanceledException and retains successful retry. Readback tests now cover repeated timeouts until the original deadline, first-timeout/second-read success with one submission, and status polling consuming most of the original budget. Standalone configuration requirements are explicit.

An independent empty-root reconstruction control compiled and failed the new multi-tenant test (1/1 failed as expected). Exact production bytes were restored in finally before the final zero-warning solution build. Control receipts: `/tmp/five-review-multitenant-control-{build,test}.log`, `/tmp/five-review-multitenant-control.xml`, `/tmp/five-review-multitenant-control.json`.

Final matching `3.127.0` Debug/source restore/build passed; build took 5.69 seconds with zero warnings/errors. Post-review executable selections passed **953/953**, zero failures/errors/skips/not-run: Contracts 92 (0.760 s), Server 648 (3.371 s), Architecture 109 (13.908 s), Works 76 (0.510 s), V6 24 (5.128 s), affected projection paths 3 (1.042 s), composed index readback 1 (6.676 s). Exact filters/summaries are `/tmp/five-post-review-receipts.json`; logs/XML use `/tmp/five-post-review-*`. This repeats the complete affected server suite and migration class after test-only private fixture changes, while preserving the earlier full-six-suite run's distinct identity. Package-mode CI on the requested main push runs all six full executables and retains blocking test artifacts; it is a separate published-package check.

The approved intent SHA-256 remains `9544e74a4e8cfc83e42d62516b28f7334e637a4ab93cef66e2137e3a21bcff1d`. The ledger has exactly five delivery annotations and one additional open pre-existing follow-up from B1; removing those six additions reproduces the baseline byte-for-byte. Launch remains FAIL and package currency remains CONCERNS. The root Conventional Commit message is validated before commit; main is pushed without force after inspecting divergence.


## Published dependency-floor correction

Initial Timesheets push `81c6614d2088731855354a4164575d9fc3467841` reached main. [CI 39950773882](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/39950773882), AppHost job `120404394827`, exposed NU1109 downgrades in the published-package graph: the SDK 3.127.0 packages require newer hosting, telemetry and MCP dependencies than the pinned shared catalog. Source-mode compilation had used the consumer's catalog; it was not evidence about these package dependency floors.

All fourteen actual NuGet nuspecs were queried. Their exact eleven Aspire/OpenTelemetry/MCP requirements are retained at `/tmp/five-sdk-3.127-published-floors.json`: Aspire.Hosting/Redis/Testing 13.6.3; Keycloak 13.6.3-preview.1.26521.2; ModelContextProtocol 2.3.0; OpenTelemetry and its five referenced exporter/hosting/instrumentation packages 1.20.1. Root CPM updates only those eleven catalog entries after import. Independent prerequisite review found no issue and confirmed unrelated pins, AppHost source/SDK/topology and the shared catalog gitlink are preserved. No SDK release or Works source change is needed.

A detached temporary worktree isolates Release/published-package assets from the main Debug/source graph. Initial symbolic catalog/guidance paths were rejected by Git/MinVer; plain copies of ordinary tracked files resolved that validation setup problem. The first Architecture run found the omitted existing Works default-checkout fixture; a read-only plain copy restored the fixture and all 109 Architecture cases passed. No submodule or nested dependency was initialized, and no supplied checkout was edited.

Published-package restore passes all 15 projects; final Release solution build passes with zero warnings/errors in 5.73 seconds (`/tmp/five-package-floor-{restore,build-final}.log`). The compiled AppHost configuration-only driver passes every fixture refusal/default-selection check (`/tmp/five-package-apphost-configuration.log`). At this recorded checkpoint full package-mode Contracts 92, Server 648, Architecture 109 and Works 76 cases pass; the Projections and Integration full sweeps continue alongside main's hosted CI. Their individual receipts use `/tmp/five-package-{full,final}-*`; hosted CI retains all six full executable outcomes in its blocking test artifact.

The matching Debug/source graph was restored/rebuilt independently with zero warnings/errors (15.29-second build), and all final source selections pass **953/953**, zero failures/errors/skips/not-run. Receipts: `/tmp/five-floor-source-{restore,build}.log`, `/tmp/five-floor-source-receipts.json`, and `/tmp/five-floor-source-*` logs/XML. The correction changes package configuration and records only; the approved frozen intent and exactly five delivery annotations remain intact. Launch FAIL and package currency CONCERNS are unchanged.
