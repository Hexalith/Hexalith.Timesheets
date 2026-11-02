# Five current deferred work items — verification, 2026-11-02

## Scope and dispositions

The four Timesheets internal command payloads now have exact owner route declarations, a keyed processor inventory and complete-payload semantic idempotency adapters. The in-process HTTP fixture observes the actual projections plus those compiled command routes, binds Development admissions and commits one catalog generation through prepare/ready/commit. A valid protected `/process` invocation returns the original issue effect; forged origin returns the processor's rejection and an undeclared discriminator remains a catalog 503. The mapped external retry and concurrent paths retain exactly one stored original effect. This is source-fixture admission, not deployed authority or an unmocked runtime pass.

The runner's shared Redis reader now refuses a database with more than 1,024 keys before Lua. Its Lua checks cardinality again atomically before `KEYS`, then bounds each key's memory usage at 64 KiB and elapsed script time at 250 ms. A large or changing database yields no snapshot, and the existing failure path writes only phase, database, timing and exception category. Complete per-key fingerprints and expiry deadlines remain the comparison input for stores within the bound. The approximately 829k-key historical DB0 has not been reread; large-DB qualification is a separate open gate.

The Current Assessment names attempts 7–9 and the attempt-9 20.02-second shared DB0 baseline timeout while retaining Story 3.6 in-progress and overall launch **FAIL**. The attempt-8 README identifies the ignored logs absent from Git; the original `SHA256SUMS` bytes are unchanged.

## Commands and results

| Check | Result |
|---|---|
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` | Exit 0 |
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` | Exit 0, zero warnings/errors |
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py` | Exit 0, 206 passed after the review fix |
| `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor` | Run once after assessment edit; exit 0, 16 passed |
| Full built ArchitectureTests executable, `-noColor` | Exit 0, 118 passed |
| Focused IntegrationTests: `InternalCommandCatalogUsesExactPayloadsAndRejectsChangedIntent` | Exit 0, one passed |
| Focused IntegrationTests: `ProtectedProcessRoutePreservesCatalogAdmissionAndKeyedProcessorRejection` | Exit 0, one passed; valid, forged and undeclared HTTP paths |
| Focused IntegrationTests: `KeyedHttpRetryReconcilesOpaqueCommittedOriginalEffects` | Exit 0, two passed; original stored effects and conflict denial |
| Focused IntegrationTests: `ConcurrentIdenticalKeyedConfirmationsHaveExactlyOnePersistedEffect` | Exit 0, one passed |

## Broad Server.Tests follow-up

The first complete built Server.Tests run during sequential review exited 1: **746 total, 11 failed, 735 passed**. Ten `ReadFailuresLogOnlySafeOperationCategoryAndPlatformTrace` variants and the non-failing-provider `ProductionEpochReadFailureStaysOpaqueEvenWhenDiagnosticProviderFails` variant read a generated `LoggerMessage` state from NSubstitute's retained call after the logging state had been cleared. Their expected four structured keys appeared as an empty sequence; no production logging failure was demonstrated. The original diagnostic is `/tmp/timesheets-five-suites-nr8M7wSY/Hexalith.Timesheets.Server.Tests.log` in this workspace.

The test-only `EagerStructuredLogCapture` now copies the exact structured key/value set and formatted text inside `ILogger.Log<TState>`. Assertions still require one warning with EventId 1301, null exception, exactly `Operation`, `Outcome`, `TraceId`, and `{OriginalFormat}`, the expected safe values, and no sensitive value in either fields or text. The diagnostic-provider-failure case still verifies an opaque loader result and no emitted log. No production code changed in this follow-up.

| Follow-up check | Result |
|---|---|
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` | Exit 0, zero warnings/errors |
| Built Server.Tests `-method '*ReadFailuresLogOnlySafeOperationCategoryAndPlatformTrace' -noColor` | Exit 0, ten passed |
| Built Server.Tests `-method '*ProductionEpochReadFailureStaysOpaqueEvenWhenDiagnosticProviderFails' -noColor` | Exit 0, two passed |
| Built Server.Tests `-noColor` | Exit 0, 746 passed, zero failed/errors/skipped/not-run |
| Matching full Debug/source solution build with `--no-restore -warnaserror -m:1 /nr:false` | Exit 0, zero warnings/errors |

The required six built xUnit v3 executables then ran individually and serially. Contracts passed **108/108**; Server passed **746/746** after the test capture fix; Projections passed **787/787** (751.281 seconds); Works passed **76/76**; Architecture passed **118/118**; Integration passed **1,047**, with five existing skips and zero failures (1,052 total; 1,111.003 seconds). Combined: **2,882 passed / five skipped / zero failed or errored**. The initial Server failure above remains part of the record.

An isolated, locally installed `redis:6` container was started with `docker run --pull=never`, a private random loopback port, and no persistent volume. The actual `read_only_shared_snapshot` Lua path captured a one-key store, refused a 70-KiB value inside Lua, and refused a 1,026-key store before Lua. All three checks passed; the owned container was removed. This verifies Lua execution and refusal semantics on disposable state only, not the large shared DB0.

## Review-fix iteration

The compiled Server owner source now publishes four operation schemas and an attributed adapter factory. The focused catalog test reads both inventories through the SDK, validates the operation contracts, and changes an authority-bearing payload field in each of the four adapters; each change produces different semantic intent and each forged discriminator is refused. The HTTP fixture checks the complete installed adapter inventory and verifies the mapped `/process` and `/project/v2` POST endpoints and workload authorization before admitting targets. The direct `/process` test continues to assert a returned effect with no stored event. Production host enrollment and deployed projection-owner composition remain with the pre-existing Story 3.6 deployment gate.

Redis observation now checks a conservative 32 MiB server memory bound before `DBSIZE` and again inside Lua before `KEYS`, uses exact `MEMORY USAGE ... SAMPLES 0`, pairs `TIME` directly with `PTTL`, and checks elapsed time once more before success. The 250 ms limit is cooperative: Redis commands within Lua are not preempted. A fake RESP test verifies memory refusal before `KEYS` even with small cardinality. On an owned disposable `redis:6` container at a random loopback port, 512 seeded keys with a test-only zero-millisecond budget produced a sanitized DB0 refusal. A second owned empty Redis container with a test-only negative budget exercised the final timer branch and produced the same sanitized refusal. Both containers were removed; shared port 6379 was untouched.

| Review-fix check | Result |
|---|---|
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` | Exit 0, zero warnings/errors |
| Built IntegrationTests `-method '*InternalCommandCatalogUsesExactPayloadsAndRejectsChangedIntent' -noColor` | Exit 0, one passed |
| Built IntegrationTests `-method '*ProtectedProcessRoutePreservesCatalogAdmissionAndKeyedProcessorRejection' -noColor` | Exit 0, one passed |
| Built IntegrationTests `-method '*KeyedHttpRetryReconcilesOpaqueCommittedOriginalEffects' -noColor` after fixture changes | Exit 0, two passed |
| Four focused Python snapshot tests: populated reader, cardinality refusal, Lua growth/budget refusal, long-key memory refusal | Exit 0, four passed |
| Built ArchitectureTests `-class '*LaunchReadinessTests' -noColor` after current-identity refresh | Exit 0, 16 passed |
| Matching full Debug/source solution build with `--no-restore -warnaserror -m:1 /nr:false` after review fixes | Exit 0, zero warnings/errors |
| `git diff --check` and `git diff --cached --check` | Exit 0, no whitespace errors |

The original attempt-8 manifest and committed README were compared with `git show HEAD:<path>` without editing historical bytes. A passing Python assertion checked the original README as the current file's exact prefix, unchanged SHA256SUMS bytes, 34 matching tracked rows, exactly three ignored logs absent from Git (`runner/01-aspire---version.log`, `runner/02-dapr---version.log`, `runner/03-docker-version.log`), and the expected updated README mismatch. A clean checkout's full `sha256sum -c SHA256SUMS` cannot pass. No missing log bytes or replacement hashes were fabricated.

## Remaining limits

No live runner, shared Redis read, AppHost startup or deployed acceptance ran in this increment. The Lua time check runs between keys and before success; Redis does not interrupt an individual command at 250 ms, so this is a cooperative work bound rather than a strict wall-clock guarantee. The reader deliberately refuses oversized state, so a complete comparison for the observed large DB0 remains unqualified. Story 3.6's deployed authority, inventory/continuity and shared-rebuild gates, the current Aspire 13.6.4 runtime gate, and launch **FAIL** remain open.
