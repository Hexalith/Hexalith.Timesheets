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
