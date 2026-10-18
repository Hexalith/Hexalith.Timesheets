# EventStore restart timeout investigation — 2026-10-18

The user selected the recommended EventStore restart timeout from the deferred-work backlog. The bmad-build workflow remains in step 2 investigation: the historical timeout is verified, but its specific cause and an appropriate corrective change are not yet established. No runtime or production source change has been made. The other independently shippable scopes were appended to `deferred-work.md`.

## Scope and preservation requirements

Repair the isolated magic-link journey's normal `aspire resource eventstore restart` behavior. Retain the restart failure, readiness check, persisted confirm/adjust/race/rebuild/readback assertions, fresh post-restart use, replay denial, shared-state admission and preservation checks, and owned-resource cleanup. Do not substitute a process kill, silently retry a failed restart into success, bypass admission, change shared infrastructure, stop another runtime, update dependencies, or claim deployed acceptance from local evidence.

Timesheets baseline: `edcbba9219b20f0c9cf18db9637467065fc5144e`. The clean EventStore checkout is `ab8fa115c486875be3a22860a273d5d68a26c749`. No submodule was initialized, updated, or modified; no branch, commit, staging, or push was performed.

## Confirmed historical evidence

- Journey evidence: `/tmp/timesheets-checkpoint-tenant-live-projects-complete/run-f0qmfl8y/25-Hexalith.Timesheets.IntegrationTests--class.log`. Record, issue, index, display, confirm/adjust, replay, race and composed Time Entry rebuild observations succeeded before the restart command exited 16. The test failed, as required.
- AppHost log: `/home/administrator/.aspire/logs/cli_20261018T102451548_detach-child_c0e127e5f7464c6194cef82d28806ab2.log`. Resource `eventstore-qrkytvxe` ran `dotnet run --project /home/administrator/projects/hexalith/timesheets/references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj --no-build --configuration Debug --no-launch-profile`.
- Stop began at `10:26:06.860` UTC. DCP logged a completed wait for wrapper PID 19465 at `10:26:13.989`, then `timed out waiting for process to stop` at `10:26:18.989`; the resource became `Unknown`. The restart CLI reported failure at `10:26:23.682`.
- The `Monitor process exited` log has `MonitorPID=19353`, `ChildPID=19465`, and `PID=19465`. The last field identifies the completed wait. It does not establish that parent PID 19353 died. Retained evidence contains no child stack or descriptor inventory proving which descendant remained.

## Source investigation and limits

The installed Aspire CLI is `13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8`; its DCP binary reports product `0.26.5` and source revision `51648b2a8913b1d01df2e365c46cccc0b884a047`.

The [pinned DCP Unix stop implementation](https://github.com/microsoft/dcp/blob/51648b2a8913b1d01df2e365c46cccc0b884a047/pkg/process/os_executor_unix.go) waits for command completion after both SIGTERM and SIGKILL, with six seconds per attempt. [Current upstream source](https://github.com/microsoft/dcp/blob/main/pkg/process/os_executor_unix.go) explicitly handles a target process exiting while descendant-held pipes keep command completion pending. That mechanism is consistent with the observed wrapper exit followed by a timeout. It is a lead, not an established diagnosis; no pipe holder was captured. The current-source link is explanatory and does not authorize an unpinned binary or dependency update.

`references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/BoundedTelemetryShutdownService.cs` and its registration predate the failed run. The actual OpenTelemetry 1.20.0 provider disposal paths use a five-second shutdown budget; older source comments describing an unbounded provider join do not establish the current failure. LoggerProvider is registered by the SDK. A separate cancellation race is visible in the service's queued, serial provider shutdown: cancellation can return before all provider shutdowns have started. That race is not established as this restart's cause and was not patched opportunistically.

The fixture enables Linux child subreaping in `_journey_command` after detached Aspire startup. Group-specific reaping is relevant to orphan investigation, but neither adopted zombies nor their involvement in this failure are established. Zombies cannot hold open pipes.

## Baseline checks completed

- `python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py`: 80/80 passed in 19.300 seconds.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version`: `10.0.401`.
- The initial bare restore succeeded, but `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -m:1 -v minimal` failed with 176 Works.Contracts missing-reference errors and HXW0002 dependency-mode warning. This is separate from the runtime timeout. The existing documented explicit source-reference invocation below passed without changing build files or dependencies.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false -v:q`: exit 0, retained at `/tmp/timesheets-restart-source-restore.log`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false -v:q`: exit 0, zero warnings/errors, 22.88 seconds; `/tmp/timesheets-restart-source-build.log`.
- Temporary host-only diagnostic: `/tmp/timesheets-restart-telemetry-probe-qer7nb0u`. It references EventStore ServiceDefaults and has no Dapr or storage access. Tracer, meter and logger providers all resolved. With an unreachable OTLP endpoint, direct execution stopped in 14 ms and disposed in 4 ms; the `dotnet run --no-build --configuration Debug --no-launch-profile` wrapper exited 0 in 7.195 seconds including a six-second observation, with stop 70 ms and disposal 4 ms. This probe did not reproduce the timeout. Its exploratory build had four assembly-conflict warnings; it is not solution acceptance evidence. Its owned process group exited.

## Current runtime blocker and resumption

The exact attempted live command was:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-restart-live-admission
```

It exited 1 with `A Dapr application is already running or inventory is unsafe; no AppHost started`. Evidence is `/tmp/timesheets-restart-live-admission/run-aqyca5a6`. Cleanup confirms no components were created and zero containers were removed. An earlier attempt with a temporary failure-observation callback was refused at the same admission check, before invoking the journey. The unrelated application was left untouched. The user was asked asynchronously to make the machine idle or select offline diagnostics only; no answer has arrived.

Resume after runtime admission is genuinely eligible. Reuse `IsolatedFixture` and its unchanged read-only stability checks and private infrastructure. Capture sanitized exact-resource EventStore logs and PID/start-time/parent/group/state plus pipe identities before fixture cleanup if restart fails. Use the existing sanitizer before saving or displaying monitoring output; do not dump raw environments, arguments with credentials, state keys/values, or memory. Keep the original failed command status even if diagnostics fail. Use the evidence to distinguish application shutdown from a surviving descriptor holder before choosing a fix in the owning repository. Only then finish the implementation spec and its appropriate workflow route. Do not classify this investigation, the admission refusal, or the passing offline baselines as a repaired restart or a completed live journey. Story 3.6 and release status remain unchanged.

## Resumed evidence — Debug selection and captured pipe holder

The historical blocker above was cleared for later admitted runs. The user requested resumption and then another retry. Timesheets now has user-provided CI commit `c4695a56c272021f316849ad60229871e456cfe6` and SDK 10.0.402. Explicit Debug/source restore and build passed, with zero warnings/errors in 28.56 seconds. Existing Release defaults and Aspire's configuration-insensitive AppHost metadata cache initially selected old Release assets. The completed [prerequisite correction](spec-pin-isolated-restart-debug-assets.md) pins the private child environment and uses Aspire's supported project-local cache bypass. A real validation-only launch proved Debug selection while two stale Release cache entries remained.

The first temporary environment-only attempt (`run-4opc78a8`) was still susceptible to that cache and failed after a foreign `eventstore` appeared. It is collision evidence, not a Debug qualification or a timeout diagnosis. Interrupted/early attempts retained private containers; recovery validated exact recorded IDs, labels, component copies and endpoints before removal. No foreign runtime or shared service was stopped. Those attempts are not acceptance evidence.

The corrected Debug run `/tmp/timesheets-restart-observed-baseline/run-bl0mn9el` reproduced the original fault: preceding confirm/adjust/race/replay/rebuild assertions passed, then the ordinary restart exited 16. The journey failed 0/1 in 85.377 seconds. A temporary observer retained 304 samples with no diagnostic errors. It recorded process identity, parent/group/state and pipe IDs only; it retained no raw environment, arguments, state values or memory. Exact-resource logs were sanitized before storage.

| UTC time | Observation |
| --- | --- |
| 14:39:24.114 | DCP starts stopping wrapper PID 56126. |
| 14:39:30.360 | Wrapper's captured PID/start identity is gone. Actual EventStore PID 58704 is reparented from 56126 to 2241. |
| Through 14:39:36.036 | EventStore is sleeping, still holding stdout `pipe:[130965168]` and stderr `pipe:[130965169]`, originally shared with the wrapper. |
| 14:39:36.234 | DCP reports `timed out waiting for process to stop`. |
| 14:39:36.318 | EventStore's captured identity is first observed gone. |

The source and process evidence now establish a descendant-held output-pipe mechanism for this failure. This was a living child, not a zombie. The exact managed shutdown stage that delayed that child's exit remains unestablished; telemetry and operational-metadata messages do not establish a telemetry root cause. No speculative shutdown-service patch was made. AppHost log: `/home/administrator/.aspire/logs/cli_20261018T143811183_detach-child_a19fefabc7fe40fc9a7e1fea6686644e.log`. Observer: `journey-process-observations.json` in the run directory. Both original failure status and all shared preservation/cleanup assertions were retained: 15 captured identities gone, three private containers and component copies removed, shared state matched.

## Requested retry — passing journey, intermittent fault remains open

After the user's `retry` response, the same corrected composition passed the unchanged full journey **1/1 in 130.510 seconds**, exit 0: `/tmp/timesheets-restart-observed-baseline/run-jilrm2ix`. The temporary observer recorded 468 samples, no diagnostic errors and a stopped observer. A stack-capture hook was prepared for a reparented child, but that condition did not occur in this successful run, so no shutdown stack was obtained.

`cleanup.json` reports complete cleanup, journey descendants gone, three containers removed and component copies removed; all 16 captured process identities are gone. `shared-preservation.json` reports `matches=true`, no mismatches, unchanged persistent/TTL contents and scheduler identity/content/revision. A passing retry demonstrates current local journey execution, not a durable repair of the intermittent fault. Story 3.6, deployed qualification and release status remain unchanged.

The built assets used Timesheets `c4695a56c272021f316849ad60229871e456cfe6` and EventStore `ab8fa115c486875be3a22860a273d5d68a26c749`. During the retry, external work moved twelve reference checkouts, including EventStore to `d55c6f7c11e9bce6f17d490a84c83f1561979d63`. No rebuild was used to silently relabel those assets as the new source revision. All external changes were preserved.

External commit `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` then recorded the reference updates and prerequisite code/configuration changes. No commit or Git mutation was performed by this investigation. Subsequent candidate implementation must rebaseline/build from the current source rather than treating this metadata-only query or earlier executable-suite results as a rebuild.

Final verification of the built baseline completed all six default suites: 1,949 passed, five intentional skips, zero errors/failures. Python passed 82/82. The initial Projections invocation exceeded an outer 180-second timeout; the unrestricted rerun passed 437/437 in 459.692 seconds. Exact commands/log locations are appended to the test summary. Final read-only Aspire/Dapr inventories and the fixture-container label query were empty; Python compilation and `git diff --check` passed.

## Supported corrective seam and implementation boundary

[Aspire's pinned project launch implementation](https://github.com/microsoft/aspire/blob/3751e615ad660621f2a7aa4c390ef48e08a0eed8/src/Aspire.Hosting/ApplicationModel/ExecutableLaunchRecipe.cs#L393-L419) honors a public `ExecutableAnnotation` on the existing `ProjectResource` and suppresses synthesized `dotnet run` arguments. This allows DCP to own the compiled gateway process directly while preserving its project identity, endpoints, references, environment and normal restart command. Legacy `IProjectMetadata` does not expose resolved output properties; the automatic resolver belongs to experimental Project v2. No migration or tool/dependency update is required for the annotation seam.

A read-only SDK query from the workspace succeeds without building:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet msbuild references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj -nologo -target:ComputeRunArguments -getProperty:RunCommand,RunArguments,RunWorkingDirectory -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 /nr:false
```

It resolves the existing native Debug `Hexalith.EventStore` executable, empty run arguments and the gateway project working directory. Resolving through MSBuild avoids hardcoding `bin/Debug/net10.0`. The next scoped implementation would add an additive EventStore.Aspire helper and bind this resolved invocation only for the validated isolated fixture. It spans a public platform seam and the fixture/AppHost contract, so it has a separate implementation spec and bmad-build approval checkpoint. This investigation and the prerequisite fix are not a completed platform repair.

## Approved direct-execution implementation — 2026-10-18

The approved spec now has an additive `WithCompiledProjectExecutable` helper in EventStore.Aspire. It validates run mode and absolute existing executable/working-directory paths, then annotates the same ProjectResource through Aspire's public `ExecutableAnnotation`. It preserves application arguments, environment, endpoints, references and the normal restart command. Timesheets consumes it only for the validated isolated fixture. Both private settings are admitted before the AppHost's no-resource validation exit; ordinary starts retain project execution.

The fixture resolves `ComputeRunArguments` for the exact gateway project from the workspace under SDK **10.0.402**, with explicit Debug/source properties. It rejects failed/malformed metadata, nonempty arguments, missing/non-executable commands and invalid working directories before creating private infrastructure. Case variants and Aspire/.NET-prefixed aliases of inherited private launch settings are removed from a private environment copy. Query output and the selected invocation are retained as sanitized evidence. The persisted journey and its restart/readiness/assertions are unchanged.

Source provenance for this implementation is Timesheets `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63`, plus the uncommitted implementation files. The earlier `ab8fa115` assets and passing retry remain distinct historical evidence. No Git mutation, dependency update, shared-entry-point edit or telemetry change was performed.

Focused verification passed: Python **85/85**, EventStore compiled-project model tests **13/13**, and harmless compiled AppHost guards **35/35**. The EventStore Debug/source test-project build and initial Timesheets Debug/source solution build both passed with zero warnings/errors. Exact commands and current broad verification results are appended to the test summary.

The first fresh default-suite pass exposed an assembly identity mismatch: test/deps references require `Hexalith.EventStore.Contracts, Version=3.125.1.0`, while the copied source DLL identifies as `3.124.0.0` with informational revision `d55c6f7c`. Server and Integration failures report that load error; Projections also reports secondary assertion failures, whose cause is not inferred from that fact alone. Other build processes were observed, but their causal role is unproved. A subsequent full source rebuild still generated 3.124.0: `references/Hexalith.Works/Directory.Build.props` defaults `HexalithEventStoreVersion=3.124.0` and forwards it on ProjectReference edges, while the existing root catalog selects 3.125.1. Verification therefore supplies the existing catalog's 3.125.1 explicitly, without editing any dependency or configuration file, and carries the same property into metadata resolution and startup.

During that verification, the shared filesystem reached zero available bytes. A rebuild aborted with exit 134 and an empty log; no external files or processes were removed. Capacity later recovered externally to approximately 3.5 GB before verification resumed. Both failed broad-suite results and the aborted rebuild are retained; they are not successful acceptance evidence.

## Direct-process acceptance result

The consistent Debug/source rebuild with the existing catalog's explicit version property passed with zero warnings/errors. Its Contracts assembly and deps references agree on 3.125.1, with informational source `d55c6f7c`. The candidate then passed the unchanged isolated persisted journey **1/1 in 128.350s**, exit **0**, at `/tmp/timesheets-direct-eventstore-live/run-4v47r76d`. Normal `aspire resource eventstore restart`, readiness, persisted-use/replay assertions and fresh post-restart use all passed. No failed restart was retried.

`08-dotnet-msbuild.log` and `gateway-invocation.json` retain SDK query/selection evidence. The temporary read-only observer retained **1,088 samples**, no errors, and a stopped observer. `direct-process-proof.json` identifies the selected native executable as the actual command with **zero arguments**, directly parented by DCP PID/start **85083/20039664**, both before and after restart. Gateway PID/start changed from **85299/20039729** to **17630/20046788**; no gateway dotnet-run wrapper was observed. Sanitized `dcp-gateway-lifecycle.log` independently records the old root stopped at **15:20:42.095 UTC**, its completed monitor at **15:20:42.450**, and the new native process monitor at **15:20:42.965**. This demonstrates the corrective launch mechanism, beyond a passing retry alone. It does not identify the managed shutdown stage responsible for the historical child delay.

`shared-preservation.json` reports `matches=true`, no mismatches, preserved persistent/TTL contents and scheduler identity/content/revision. `cleanup.json` reports complete cleanup, journey descendants gone, component copies removed and **three private containers removed**. All 14 initially captured identities are gone; `gateway-process-exit.json` additionally verifies both gateway PID/start identities are gone after cleanup. No shared or unrelated runtime was stopped. Local direct-process acceptance does not certify deployment, legacy migration, Story 3.6 or release readiness.


Final consistent current-source default suites passed **1,949 tests**, with **five intentional skips**, zero errors and zero failures. All six projects ran individually: Architecture 59, Contracts 92, Server 558, Works 76, Projections 437 in 458.888s, and Integration 727 passed plus five skips in 491.347s. Logs are `/tmp/timesheets-direct-eventstore-consistent-<Project>.log`. The explicit verification property resolves the observed assembly mismatch for this run; the initial failures and separate mixed-version configuration limitation remain recorded above. The local direct-process live journey also passed, with shared preservation and complete owned cleanup. Deployed/legacy certification, Story 3.6 and release readiness are unchanged.

Final artifact appending also encountered ENOSPC. After exact SHA-256 verification, only the three completed initial failed-suite logs were relocated to `/dev/shm/timesheets-direct-eventstore-retained-failures/`, with symlinks retaining their original `/tmp` paths. The small final appendices then completed and were flushed. No external files were deleted and no external processes were signaled.


## Direct-execution review corrections — 2026-10-18

The AppHost now compiles the new API only when `Configuration=Debug` and source references are enabled; unsupported direct-launch settings refuse before any resources. A temporary Debug consumer compiled the actual Program.cs against published EventStore.Aspire 3.125.1 (confirmed to lack the helper), and all five ordinary/unsupported no-resource probes passed. No local Release assets were produced. The fixture discards inherited SDK RunCommand/RunArguments/RunWorkingDirectory and StartAction/StartProgram/StartArguments/StartWorkingDirectory, including casing/provider aliases, while retaining project-defined metadata and the caller environment. Native command, fixture-bound direct DCP ownership and old/new PID/start replacement are now checked and recorded by the checked-in lane around the unchanged journey. Primary failures and exact cleanup are preserved; newly observed identities are retained before diagnostic writes. All new public helper-test methods have XML summaries and parameter documentation.

Focused verification passed: Python 92/92; three final cleanup/proof regressions; Debug/source AppHost and helper-test builds with zero warnings/errors; compiled guards 35/35; helper tests 13/13; published-package branch probes 5/5. Only focused checks ran during these corrections. The parent owns the complete solution/six-suite/checked-in-live rerun. Earlier full and live success receipts remain historical, with their qualification limits preserved.

[Sanitized durable local proof, input hashes and exact package qualification](evidence/3-6/20261018-direct-eventstore-local-proof/README.md).


## Preserve unconditional listener fitness — 2026-10-18

The parent's default Integration run exposed `MagicLinkConfirmationCapabilityEndpointTests.AppHostExportsTheInternalListenerPortToTheHostOptionsSection` at line 32 because Program.cs contained private gateway compilation directives. The test was not changed. Only gateway build-mode refusal and delegation moved to the internal `IsolatedGatewayLaunch.cs` file; Program.cs, fixture validation and listener wiring remain unconditional. EventStore.Aspire still owns all executable-annotation plumbing, and the same early package-mode refusal and native launch remain.

Focused verification used only the AppHost build with `BuildProjectReferences=false` (Debug/source, zero warnings/errors), the existing built Integration method (1/1 in 0.102s), compiled guards (35/35), and a temporary Debug/published-package compilation plus five guards (5/5), explicitly including the new helper. No Integration or broad-suite rebuild ran in this correction pass; the parent retains the original failed/interrupted invocation and owns complete re-verification. No local Release assets were produced.

The exact focused method command was:

```bash
tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationCapabilityEndpointTests.AppHostExportsTheInternalListenerPortToTheHostOptionsSection -noColor
```

[Durable correction receipts, package inputs and refreshed input hashes](evidence/3-6/20261018-direct-eventstore-local-proof/README.md#listener-fitness-correction).

## Final checked-in corrective qualification — 2026-10-18

The final Debug/source solution build is clean, and all six default test projects passed individually: **1,949 passed**, **five intentional skips**, no errors/failures. The original listener-security fitness test remains unchanged and passes after isolating only the gateway build-mode adapter. Earlier failed/interrupted invocations remain separate from this complete corrected run.

The checked-in isolated runner passed the unchanged persisted journey **1/1 in 131.536s**, exit **0**. Its own permanent process checks, without a temporary observer, verify the native zero-argument gateway before and after normal restart. PID/start changed **10230/20390107 → 41101/20397845** under the same direct DCP parent **10096/20390046**; the old identity exited. Cleanup verifies all **16** captured identities exited, all three private containers and component copies removed, and shared persistent/TTL plus scheduler identity/content/revision preserved. Final Aspire/Dapr/fixture-container inventories are empty.

The first final admission attempt refused an unrelated six-application Dapr run before creating anything. Once that run ended, fresh admission and baseline stability checks passed and the successful run proceeded. There was no retry of a failed restart and no unrelated runtime stop. [Durable corrected-source proof, exact commands and hashes](evidence/3-6/20261018-direct-eventstore-local-proof/README.md#final-corrected-source-verification) distinguish the final checked-in run from historical observer evidence. The existing Works version-forwarding mismatch remains deferred; verification used the explicit current-catalog version property. This closes the selected local restart correction, without certifying deployment, legacy migration, Story 3.6 or release readiness.
