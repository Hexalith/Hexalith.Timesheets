# Local direct EventStore proof — 2026-10-18

This small sanitized packet retains the earlier successful native-process journey and focused evidence for the review corrections. Source identities and exact input hashes are in [input-hashes.json](input-hashes.json); [SHA256SUMS](SHA256SUMS) binds the retained packet files.

The base source identities are Timesheets `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63`, with uncommitted implementation changes. SDK 10.0.402, Debug/source mode and the existing catalog's explicit `HexalithEventStoreVersion=3.125.1` were used. No dependency version was added to runner source.

The `historical-live/` receipts retain the 128.350s passing journey, SDK command selection, direct DCP ownership, old/new PID/start replacement, DCP lifecycle timeline, shared-state comparisons and complete owned cleanup. They predate the review corrections. The unchanged journey/component hashes and historical observer input are retained separately from hashes of the corrected candidate. Uncommitted launch-source and compiled-binary bytes were not hash-captured at the time of that historical run; this packet does not retroactively claim complete historical source-byte attestation.

The `focused-review/` receipts cover 92 Python checks, three final cleanup/proof regressions, 35 compiled no-resource AppHost guards, 13 helper tests, and five package-branch probes. The runner now verifies its native command, exact fixture-bound DCP parent and PID/start replacement itself before/after the unchanged journey; it retains both identities for cleanup even when evidence writing fails. These focused receipts do not replace the parent's full post-review verification.

Package compilation qualification linked the actual AppHost `Program.cs` into a temporary **Debug** consumer of published `Hexalith.EventStore.Aspire` **3.125.1**, whose DLL lacks `WithCompiledProjectExecutable`. The temporary project and metadata stub inputs are retained as text in `inputs/`. Its location was `/dev/shm/timesheets-package-branch-943n6z1g`; no local Release assets were produced. Ordinary run and publish probes returned before resources; executable-only, directory-only and paired direct settings refused with the Debug/source-build error. Exact probe arguments and exits are in `focused-review/package-branch-probes.json`.

Commands used for these focused checks:

```bash
python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --configuration Debug --no-restore -p:BuildProjectReferences=false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.125.1 -warnaserror -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home HexalithEventStoreVersion=3.125.1 python3 scripts/check_magic_link_apphost_guards.py --configuration-only --log-directory /dev/shm/timesheets-direct-review-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build /dev/shm/timesheets-package-branch-943n6z1g/PackageBranchProbe.csproj --configuration Debug --no-restore -m:1 /nr:false -v:q
# From references/Hexalith.EventStore:
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --configuration Debug --no-restore -p:BuildProjectReferences=false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.125.1 -warnaserror -m:1 /nr:false -v:q
tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests -class '*CompiledProjectExecutableTests' -noColor
```

Validate retained packet bytes with `sha256sum --check SHA256SUMS` from this directory. Current candidate input hashes describe the corrected source snapshot and must be checked again if those files change. Deployment, legacy migration, Story 3.6 completion and release readiness remain outside this local qualification.


## Listener fitness correction

The parent's default Integration invocation exposed `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` at line 32: Program.cs must contain no conditional compilation, keeping listener security wiring unconditional. That original failed/interrupted invocation remains retained by the parent and is not acceptance evidence.

Only the private gateway build-mode refusal and EventStore SDK delegation moved to the internal `IsolatedGatewayLaunch.cs` type. Program.cs has no conditional compilation; fixture validation and public/internal listener declarations remain unchanged. No public API, reflection or annotation plumbing was added to the AppHost type. The same EventStore.Aspire helper owns the executable annotation.

`listener-correction/` retains the AppHost-only Debug/source build (zero warnings/errors), the unchanged built Integration source-fitness method (1/1, 0.102s), compiled guards (35/35), and published-package probes (5/5). The temporary Debug package project explicitly includes both Program.cs and IsolatedGatewayLaunch.cs. Its updated project input and both source inputs are retained in `inputs/`, with the earlier project input preserved under `inputs/before-listener-correction/`. Current candidate hashes include the new internal type.

The focused method ran without rebuilding Integration or its dependencies:

```bash
tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationCapabilityEndpointTests.AppHostExportsTheInternalListenerPortToTheHostOptionsSection -noColor
```

The AppHost-only and temporary-package Debug build commands remain those listed above. Corrected full solution/default-suite/live verification remains owned by the parent after handoff.

## Final corrected-source verification

The corrected candidate passed complete verification after the listener adapter correction. [Structured results](post-review/verification.json), all six individual suite logs, the clean solution build, Python output, compiled guards, and checked-in live proof are retained under `post-review/`. The suite harness is retained in `inputs/final-suite-harness.py.txt`; it runs individual project executables with at most two project processes concurrently. Source hashes match the final candidate, including `IsolatedGatewayLaunch.cs`.

The solution build passed with zero warnings/errors. Default suites passed **1,949**, with **five intentional skips** and no errors/failures: Architecture 59, Contracts 92, Server 558, Works 76, Projections 437, Integration 727 plus five skips. Python passed 92/92, helper tests 13/13, compiled guards 35/35, and the package probes 5/5. Projections took 493.055s and Integration 525.417s in the final concurrent-project run.

The checked-in isolated runner passed the unchanged live journey **1/1 in 131.536s**, exit 0. Native gateway PID/start changed **10230/20390107 → 41101/20397845**, with zero arguments and the same direct DCP parent **10096/20390046**. The permanent replacement receipt confirms the old identity exited. All **16** captured identities exited after cleanup, all three private containers and component copies were removed, and shared Redis persistent/TTL plus scheduler identity/content/revision matched. Final Aspire/Dapr inventories and the fixture-container query were empty.

The first live admission attempt refused six unrelated Dapr applications and created no resources. The next attempt ran only after the other run ended and fresh admission/stability checks passed; no failed restart was retried. The earlier known-failing listener invocation was deliberately interrupted through its recorded owning test harness and is retained separately; it is not acceptance evidence. An additional HTTP 500 appeared after its SIGTERM, without an uninterrupted reproduction; the complete corrected Integration rerun passed.

Verification still requires the explicit existing-catalog property `HexalithEventStoreVersion=3.125.1` because of the separately deferred Works forwarding mismatch. No dependency or gitlink update, staging, commit, push, or unrelated runtime stop occurred. This is local qualification; deployment, legacy migration, Story 3.6 and release readiness remain unchanged.

Final commands from the workspace:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.125.1 -warnaserror -m:1 /nr:false -v:q
# Each project runs separately; exact commands and timing are retained in the harness/results.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home HexalithEventStoreVersion=3.125.1 tests/Hexalith.Timesheets.<Project>/bin/Debug/net10.0/Hexalith.Timesheets.<Project> -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home HexalithEventStoreVersion=3.125.1 python3 scripts/check_magic_link_apphost_guards.py --configuration-only --log-directory /dev/shm/timesheets-direct-corrected-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home HexalithEventStoreVersion=3.125.1 python3 scripts/run_isolated_magic_link_dapr.py --log-directory /dev/shm/timesheets-direct-corrected-live
```

## Publication follow-up — 2026-10-18

The user subsequently requested pushing all changes, authorizing commits and the owning EventStore pointer update. EventStore fast-forwarded three existing remote commits to `2490f4f409e015155c046afa55b37997b04a271d` without initializing or updating nested submodules. The helper was committed and pushed as `9b66153515687463fc1b1e929f8b0e73d8145e2c`; Timesheets records its pointer separately in `88f4ae0c3268cbe43e6feb9ff649133c5c03ae83`. The prior qualification receipts retain their original source identities and are not relabeled as these later revisions.

An additional dependency rebuild failed with **CS1704/MSB3243** duplicate `Hexalith.Commons.UniqueIds` references. The same focused test-project build with `-p:BuildProjectReferences=false` passed with zero warnings/errors in 1.24s, and the helper tests passed **13/13 in 3.280s**. This fallback does not certify the failed whole-dependency graph. The failed command, run from EventStore, was:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.125.1 -warnaserror -m:1 /nr:false -v:q
```

The successful command adds `-p:BuildProjectReferences=false`; the focused executable invocation uses `-class '*CompiledProjectExecutableTests' -noColor`. [Publication receipts](publication/verification.json) retain the failure, focused results, exact commit messages and successful validation with each repository's pinned commitlint. The evidence logs are explicitly included despite the repository's general `*.log` ignore rule.

Publication preparation removed only surplus terminal blank lines from five retained receipts to satisfy the Git whitespace check. Original historical input hashes remain recorded separately from refreshed retained-byte hashes in `input-hashes.json`; JSON payloads, log content and original run files are unchanged.
