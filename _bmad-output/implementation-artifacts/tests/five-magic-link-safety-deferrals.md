# Five magic-link safety deferrals — verification receipt

Date: 2026-10-23. Baseline: `c56ac4fdf5f4648f2168555f837856555a15431a`.
SDK: `10.0.402`; Debug/source-reference mode; EventStore property `3.127.0`.

## Five dispositions

The five selected entries are implemented, fully verified and independently reviewed. Three pre-existing follow-ups are recorded separately below; original failed and incomplete receipts are preserved.

| Deferred entry | Implemented behavior | Disposition |
| --- | --- | --- |
| Unknown authority events | Separate complete capability/owner admission sets; unknown events deny without partial state; known stored/domain aliases, ordinary owner facts and rejection no-ops survive. | Resolved; final verification and review passed. |
| Total loader read amplification | One invocation-local allowance counts all raw pages/events/bytes, including repeats and duplicates; shared monotonic deadline bounds non-cooperative awaited stream reads. Defaults 64 / 10,000 / 16 MiB / 5s; positive caps 256 / 100,000 / 128 MiB / 30s. | Resolved; final verification and review passed. |
| Unexpected public-handler faults | All four handlers return fixed opaque 500 ProblemDetails with private headers and sanitized Error event 37002. Ordinary invalid links retain 403; actual request cancellation propagates; logging-provider failures cannot replace responses. | Resolved; final verification and review passed. |
| ExistingTimeEntry issuance | Service rejects before token generation; aggregate and durable processor reject issuance; historical reads/enum and existing use denial survive. Story 3.2 issuance scope is explicitly narrowed without approval/correction changes. | Resolved; final verification and review passed. |
| Invalid selected migration metadata on retry | Revalidate selected format/root/single and direct single metadata inside operation-owned abort handling, after validating immutable operation/inventory ownership. Refuse promotion; preserve selected source contents/ETags. Empty-journal aborts release Building; pending/corrupt-source and interrupted-abort recovery retain their separate follow-ups. | Resolved; final verification and review passed. |

## Commands and receipts

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -warnaserror -m:1 /nr:false
```

Restore passed (`/tmp/five-safety-restore.log`). The solution build passed with zero warnings/errors (`/tmp/five-safety-build.log`).

Focused server command:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class '*EventStoreMagicLinkConfirmationCapabilityStateLoaderTests' -class '*RuntimeRegistrationTests' -class '*MagicLinkConfirmationCapabilityCommandServiceTests' -class '*MagicLinkEventStoreDomainProcessorTests'
```

Passed 219/219, zero skips, before the additional raw-duplicate and cumulative-time controls (`/tmp/five-safety-focused-server.log`). An earlier full server run includes those controls and passed 668/668 with zero skips (`/tmp/five-safety-final-server-after-mutation.log`, structured `/tmp/five-safety-final-server-after-mutation.json`).

Focused projection command:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkV6MigrationTests'
```

Passed 39/39, zero skips (`/tmp/five-safety-focused-projections.log`).

Full HTTP boundary command (also includes the persisted-submission partial class):

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home timeout 600s tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkConfirmationHttpBoundaryTests' -longRunning 30 -reporter verbose -result-ctrf /tmp/five-safety-focused-integration.json
```

The structured CTRF is the authoritative receipt for this run. The reused text log `/tmp/five-safety-focused-integration.log` received a late flush from the terminated earlier attempt and contains overlapping text; do not treat that text summary as the current result. Older 10,000-owner cases are reported by long-running diagnostics. No foreign process was stopped.

Other suites run their built executables individually with `-result-ctrf /tmp/five-safety-final-<Project>.json`; logs have the same prefix and `.log` suffix. The final solution build after malformed-JSON and constructor compatibility controls passed with zero warnings/errors (`/tmp/five-safety-final-build-with-json.log`). The full server executable passes 670/670, zero skips (`/tmp/five-safety-final-server-compatibility.{log,json}`), and final focused V6 passes 49/49 (`/tmp/five-safety-final-v6-json.{log,json}`). `StateLoaderRetainsItsOriginalSevenParameterPublicConstructor` verifies the original IL signature; `KernelSelectsTheOptionsAwareLoaderConstructor` proves DI resolves and validates the new options.

The first full Projections invocation was bounded by Python `subprocess.run(..., timeout=600)` and ended with `subprocess.TimeoutExpired` after 600 seconds; `/tmp/five-safety-final-Hexalith.Timesheets.Projections.Tests.log` is incomplete. The earlier HTTP timeout sent TERM at 600 seconds, but xUnit remained CPU-active inside the existing 9,998-extra-owner fixture, so only that owned process was subsequently stopped. Incomplete text is retained at `/tmp/five-safety-http-bounded-incomplete.log`. Neither incomplete attempt counts as a pass.

A safe minidump of that HTTP process identified `TryWriteCapturedIdentityRecordsAsync` → `SeedCapturedOwnerSequenceCoreAsync` → `DomainSharedProjectionRebuildDispatcher.SeedPartitionOwnerSequencesAsync` in the unchanged SDK. Diagnostics are `/tmp/five-safety-http-progress-{collect,stacks}.log`; the dump stays under `/tmp`. EventStore checkout and root gitlink both identify `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`. Historical full Debug/source receipts already report Integration 766s and Projections 835s; the initial 600s broad limits were insufficient. These diagnostic timings are not performance qualification.

Final broad verification therefore uses generous finite process bounds and complementary Integration filters:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home timeout --kill-after=30s 1800s tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -longRunning 60 -reporter verbose -result-ctrf /tmp/five-safety-final-projections-complete.json
DOTNET_CLI_HOME=/tmp/dotnet-cli-home timeout --kill-after=30s 1800s tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*ActorGroundedSharedSourceProducerCommitsComposedCapabilityAndSelectedReadback' -longRunning 60 -reporter verbose -result-ctrf /tmp/five-safety-isolated-heavy-http.json
DOTNET_CLI_HOME=/tmp/dotnet-cli-home timeout --kill-after=30s 1800s tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method- '*ActorGroundedSharedSourceProducerCommitsComposedCapabilityAndSelectedReadback' -longRunning 60 -result-ctrf /tmp/five-safety-final-integration-complement.json
```

The completed pre-review broad receipts are full Projections 658/658 in 782.942 seconds (`/tmp/five-safety-final-projections-complete.json`) and the isolated heavy Integration method 2/2 in 609.089 seconds (`/tmp/five-safety-isolated-heavy-http.json`). The complementary Integration invocation ran 901 cases: 895 passed, one source-fitness assertion failed, and five declared opt-in/infrastructure skips. Its failed receipt is retained at `/tmp/five-safety-integration-complement-initial.{log,json}`. Updating that assertion for the approved separate 500 helper passed the four-case endpoint class (`/tmp/five-safety-endpoint-fitness-inside-root.{log,json}`), with all other integration cases already passed. The unique pre-review Integration coverage is therefore 903 cases, 898 passing and five intentional skips; it is not represented as one uniformly passing invocation. Focused new HTTP cases passed 7/7 (`/tmp/five-safety-focused-new-http.{log,json}`). The first isolated output directory could not discover the repository root and produced four setup failures; rerunning the same isolated build under the ignored repository bin directory passed. This fixture-path failure is retained separately and is not a production defect.

Architecture 109/109, Contracts 92/92 and Works 76/76 also passed. These pre-review results precede the final review corrections; final root verification will be recorded separately.

## Matrix audit

All names below are executed xUnit tests; structured receipts retain per-case status.

| Frozen matrix row | Covering tests |
| --- | --- |
| Current events, wrappers, rejection no-ops survive | `UnknownAuthoritySuffixDeniesWhileKnownAliasesAndRejectionNoOpsSurvive` first resolves a valid alias/no-op history, then falsifies it with an unknown suffix; existing concrete-loader HTTP valid journeys remain required. |
| Unknown authority event denies | Both capability and owner variants of `UnknownAuthoritySuffixDeniesWhileKnownAliasesAndRejectionNoOpsSurvive`. |
| Exhausted allowance / stalled read / caller cancellation | `InvocationAllowanceChargesRepeatedStreamsAtExactAndOverBoundaries` (pages/events/bytes/raw duplicates), `AdvancingGrowingHistoryStopsAtTheSharedPageAllowance`, `CumulativeSuccessfulReadsShareOneInvocationDeadline`, `NonCooperativeStreamReadIsBoundedAndCallerCancellationPropagates` (timeout/cancellation), plus option-binding cap controls. |
| Unexpected public faults and ordinary denial/cancellation | `ThrowingMagicLinkDependencyKeepsQueryValuesOutOfDiagnostics` (fault and unrelated dependency cancellation), `CommandServiceAuthorizationFaultsAfterSuccessfulLoadingStayOpaqueForEveryRoute`, `DiagnosticProviderFailuresPreserveOpaqueDenialOrFaultForEveryRoute`, `GenuinePublicRequestCancellationPropagatesWithoutErrorDiagnosticsForEveryRoute`, plus the existing opaque-invalid-response matrix. |
| ExistingTimeEntry issuance | `ExistingTimeEntryIssuanceIsRejectedBeforeTokenGenerationAndByTheAggregate`, `DurableExistingTimeEntryIssueRejectsBeforeConsultingDependencies`, `ExistingTimeEntryIssueProducesNoTokenAndNoDurableSubmission`, and historical/use-denial controls. |
| Invalid migration metadata releases Building | Eighteen `InvalidSelectedMetadataOnRetryReleasesOnlyItsBuildingFence` cases (format/root/selected-single/direct-single after sealed/unsealed interruption, including direct malformed JSON and SDK-wrapped malformed JSON), asserting unchanged selected source and corrupted metadata contents/ETags. Aborted private stages may be cleaned by the SDK. |
| Fresh malformed / foreign / transient fault controls | Four `FreshInvalidSelectedMetadataNeverReservesAFence` cases; `RetryMustProveOperationAndInventoryAuthorityBeforeOwningAbort` (foreign operation, foreign inventory, authority denied, followed by valid/completed retry); existing `SealedResumeTransportFailureOrCancellationRemainsRetryable` and other completed-resume controls. |

## Cumulative deadline mutation

Built Server.Tests with the same Debug/source-reference/EventStore properties. The baseline focused method passed. A temporary `_elapsed.Restart()` at every read (incorrectly granting a fresh deadline) made `CumulativeSuccessfulReadsShareOneInvocationDeadline` fail: it published authority instead of denying the total 2s deadline. The two early reads each take 800ms; the third would complete under an independent 2s timeout.

Receipts: `/tmp/five-safety-cumulative-baseline.{log,json}`, `/tmp/five-safety-cumulative-mutant.{log,json}`, and `/tmp/five-safety-cumulative-mutation.json`. Original bytes were restored; original/restored SHA-256 both equal `fbf681d8ed1236bc9d1770a2232ee023b032918e18d75f88e24f3dfdd2bc593e`. The restored project build passed, followed by the full 668/668 server run.

## Initial failures and corrections

- Initial focused server run: 219 total, one failure. A new test asserted `WasDispatched == false` for an aggregate rejection, but that property means an authorized domain result was produced. Corrected it to assert `DomainResult.IsRejection`, no issue response and zero token generations. Retained `/tmp/five-safety-focused-server-initial.log`.
- Initial focused projections run: 39 total, four failures. The new sealed-abort tests incorrectly expected private staged roots to remain. SDK abort correctly removes private stages. Corrected the assertion to preserve the selected generation, selected source/corrupt metadata contents and ETags. Retained `/tmp/five-safety-focused-projections-initial.log`.
- Earlier broad HTTP attempt was terminated after a quiet period; its incomplete text is retained at `/tmp/five-safety-focused-integration-incomplete.log`. The retry uses a finite process timeout, verbose long-running diagnostics and a separate structured receipt. The isolated new request-cancellation control independently passed 1/1 (`/tmp/five-safety-cancellation.log`).

## Limits and pending gates

No live migration or AppHost startup ran. The prescribed isolated live baseline is excluded because a foreign Tenants AppHost is active; it was left untouched. No sibling sources, dependencies, gitlinks, stored events, enum values, AppHost or ServiceDefaults files were changed. Source/in-process evidence supplies no deployed authority, inventory, continuity, shared-rebuild or current-graph runtime acceptance. Story 3.6 stays in progress; launch remains **FAIL**.

## Independent review and preserved limitations

All three review layers completed. Verification-gap reported no gaps. Root recorded one verdict for each of ten blind findings and one edge-case finding in the spec. Three direct corrections were applied and focused-tested: publication-time deadline/cancellation checks, a defensive validated options snapshot, and a scheduling-tolerant cumulative-deadline assertion.

Three pre-existing follow-ups were recorded separately: corrupt selected-source recovery with pending journal entries; operation/inventory-authorized continuation after an interrupted abort; and actual read-model transport/overall-resolution deadline qualification. The baseline already used the same ordinary catch-up fold and had no Aborting continuation. No test in this increment proves that a corrupt selected source can safely drain pending delivery or that an interrupted abort resumes through MigrateAsync. The new empty-journal metadata tests prove operation-owned abort initiation and safe fence release when abort drains successfully. Queued work must remain fail-closed rather than be discarded or folded by bypassing integrity checks. The five-second StateLoading deadline is an authority-history allowance, not a bound on every candidate-index/catalog await.

The pre-review root matrix audit at `/tmp/five-safety-root-matrix-audit.json` checked executed passing cases for all seven frozen rows: 2 current-event cases, 2 unknown-event cases, 8 budget cases, 6 public-fault cases, 3 issuance cases, 18 migration metadata cases and 9 migration controls. Final correction/full-suite evidence and publishing remain root gates.


## Final review corrections

The restored loader class passed 134/134 with zero skips (`/tmp/five-safety-review-restored-loader.{log,json}`). Eight publication-boundary expiry/cancellation cases and four caller-options mutation cases cover the review corrections. Deadline reset and retaining the mutable caller object each cause the intended focused failure; `/tmp/five-safety-review-mutations.json` confirms restored file hashes. The late-publication test fixture delays/cancels during post-transport materialization or the final catalog boundary, and the cumulative test still rejects restarting the timer per read without requiring exactly two successful reads.

Final production SHA-256 values after restoration:

- `MagicLinkStateLoadingBudget.cs`: `ffba9946c8deb1192980163418c4ef4669f4ca6e164d955a1bec5e480b6f0c44`.
- `EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`: `204b6d2afd3be74f5d998e24c7ae2d72935c0064493d7be5158b083d0646c064`.

Root's final matching Debug/source solution build passed with zero warnings/errors in 17.77 seconds (`/tmp/five-safety-root-final-build.log`). Root then started all six built executables individually, with separate logs and CTRF receipts under `/tmp/five-safety-root-final-<Project>.{log,json}`, using `timeout --kill-after=30s 1800s`, `-longRunning 60` and `-result-ctrf`. These final invocations include the corrected endpoint source check in the regular build. Commit text passed the repository's installed commitlint; all final receipts have now passed, as recorded below.


## Final root verification

All six suites ran unfiltered, individually, against the final restored regular Debug/source build. Every invocation exited zero. These supersede the pre-review and complementary-run evidence above.

| Suite | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| Architecture | 109 | 109 | 0 | 0 | 11.052 s |
| Contracts | 92 | 92 | 0 | 0 | 0.566 s |
| Server | 682 | 682 | 0 | 0 | 6.040 s |
| Projections | 658 | 658 | 0 | 0 | 836.698 s |
| Works | 76 | 76 | 0 | 0 | 0.341 s |
| Integration | 903 | 898 | 0 | 5 | 769.690 s |
| **Total** | **2,520** | **2,515** | **0** | **5** | |

There were zero pending/not-run/other results. The five Integration skips are the existing infrastructure/performance opt-ins; they supply no runtime acceptance. The full restored solution build had zero warnings/errors. Timing here identifies the test receipts, not an NFR or performance qualification.

[Machine-readable final results](five-magic-link-safety-results.json) retain per-suite counts, durations, assembly/receipt SHA-256 hashes, source identities and the executed matrix audit. Every frozen matrix row has passing executed coverage; the final budget row includes all eight late-publication cases and four options-snapshot mutation cases. All three review layers completed; three direct corrections passed, and the three pre-existing limitations above remain deferred. Exactly five selected ledger entries are resolved.

The commit message `fix: harden five magic-link safety boundaries` passed installed commitlint. User authorization is to commit and push main after these gates. Live/deployed acceptance and the launch FAIL verdict remain unchanged.
