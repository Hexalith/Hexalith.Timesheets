---
title: 'Give Aspire direct ownership of the isolated EventStore gateway process'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
baseline_commit: '72d9fdaa813daf9d39d296e268fd2e1086a8dd87'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The isolated persisted journey intermittently fails its normal EventStore restart. Fresh process evidence shows the `dotnet run` wrapper exiting while its living gateway child retains the output pipes through DCP's stop deadline. An unchanged retry passed; the intermittent fault remains open.

**Approach:** For the supported isolated fixture, resolve the gateway's compiled native invocation through the pinned SDK and let Aspire own that executable directly. Add the reusable launch helper in EventStore.Aspire; consume it on the existing gateway ProjectResource. Preserve the normal restart command and all persisted journey assertions.

## Boundaries & Constraints

**Always:** Keep Debug/source mode, private infrastructure, admission/stability checks, sanitized evidence, original failure status, shared-state comparison and exact owned cleanup. Resolve metadata for the exact gateway project using the same workspace, SDK and build properties. Preserve project identity, endpoints, references, environment and Dapr bindings. Rebuild against current source before candidate verification, recording external checkout movement separately.

**Never:** Add a host or topology; change ordinary/publish startup; migrate Project v2; update dependencies; patch telemetry speculatively; pre-shutdown/kill the gateway; retry a failed restart into success; stop unrelated runtimes; stage/commit/push or update gitlinks. Local success does not certify deployment, legacy migration, Story 3.6 or release readiness.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Supported invocation | SDK returns absolute executable file, existing working directory, empty arguments | Existing ProjectResource launches that native executable directly | Retain query and selected-command evidence |
| Unsupported metadata | Query fails, malformed/missing fields, nonempty arguments, unavailable/non-executable command or invalid directory | No AppHost/private infrastructure created | Sanitized nonzero failure |
| Polluted caller | Inherited private launch settings in conflicting casing | Runner discards aliases and resolves fresh metadata in its private environment | Caller environment unchanged |
| Invalid AppHost context | Either private launch setting outside a fully validated isolated run, partial/blank pair, or publish mode | Refuse before resource construction, including validation-only probes | Harmless compiled negative check |
| Ordinary start | Neither new setting supplied | Existing project launch behavior | Existing validations retained |

</frozen-after-approval>

## Code Map

- `scripts/run_isolated_magic_link_dapr.py`: private environment, command evidence, admission, execute/cleanup. Add read-only metadata resolution before infrastructure creation; retain subsequent admission checks.
- `src/Hexalith.Timesheets.AppHost/Program.cs`: fixture admission precedes validation-only exit; the run-mode gateway uses `AddHexalithEventStoreGatewayProject`. `Hexalith.Timesheets.AppHost.csproj` and internal `IsolatedGatewayLaunch.cs` confine the private compiled-launch adapter to Debug/source mode while retaining unconditional listener wiring. Consume the SDK helper without introducing another resource.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/`: owns reusable hosting APIs. Public Aspire `ExecutableAnnotation` suppresses synthesized dotnet-run arguments on ProjectResource; no internal Aspire APIs needed.
- `scripts/local_aspire_magic_link_journey.py`: existing restart/readiness and persistence proof; preserve its assertions and command.
- [Investigation](eventstore-restart-timeout-investigation-2026-10-18.md): captured failure, passing retry and provenance. Baseline assets used EventStore `ab8fa115c486875be3a22860a273d5d68a26c749`; external checkout is now `d55c6f7c11e9bce6f17d490a84c83f1561979d63`.

## Tasks & Acceptance

**Execution:**

- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExecutableExtensions.cs` — add an additive, documented `WithCompiledProjectExecutable` extension accepting executable path and working directory; validate inputs and run mode, annotate the same ProjectResource, preserve existing arguments/environment/endpoints. Leave existing public signatures unchanged.
- [x] `references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/CompiledProjectExecutableTests.cs` — test direct command selection, same resource/graph, application arguments, invalid paths/context and paths with spaces.
- [x] `scripts/run_isolated_magic_link_dapr.py` — query `ComputeRunArguments` for RunCommand/RunArguments/RunWorkingDirectory with explicit Debug/source properties; validate the native invocation, normalize inherited aliases, then set `TIMESHEETS_LIVE_DAPR_GATEWAY_EXECUTABLE` and `TIMESHEETS_LIVE_DAPR_GATEWAY_WORKING_DIRECTORY` privately.
- [x] `src/Hexalith.Timesheets.AppHost/Program.cs`, `IsolatedGatewayLaunch.cs`, `Hexalith.Timesheets.AppHost.csproj` — validate that pair before the no-resource exit, gate the private adapter to Debug/source compilation and consume the platform helper only inside the existing isolated run branch; retain unconditional listener wiring.
- [x] `scripts/test_isolated_magic_link_dapr.py`, `scripts/check_magic_link_apphost_guards.py` — cover the matrix, resolution failure before creation, unchanged caller environment, harmless compiled refusals and ordinary startup.
- [x] `eventstore-restart-timeout-investigation-2026-10-18.md`, `tests/3-6-test-summary.md` under implementation artifacts — append commands, source identities, reviewed results and limits.

**Acceptance Criteria:**

- Given fresh Debug/source assets and eligible admission, when the unchanged isolated journey runs, then normal EventStore restart succeeds and all post-restart persisted-use/replay assertions pass with shared preservation and complete owned cleanup.
- Given the selected invocation, when Aspire starts/restarts the gateway, then DCP owns the resolved native process directly, the old PID/start identity exits and a new identity starts; actual command evidence excludes a dotnet-run wrapper.
- Given default or invalid fixture settings, when compiled guard probes execute, then ordinary selection remains valid and unsafe contexts refuse before resources.

## Implementation Notes

No unresolved intent choices or irreversible operations. The footprint crosses the platform API, fixture and AppHost contract, so implementation uses the dispatch checkpoint. The existing Debug-selection prerequisite is separately reviewed and done. Read-only metadata qualification already returns the native Debug gateway, empty arguments and its project working directory; no corrective platform code has been written.

- Approved by the user on 2026-10-18. The frozen intent was reread unchanged before accepting approval. Preserve the pre-existing investigation, prerequisite-spec and test-summary working-tree edits. EventStore baseline is `d55c6f7c11e9bce6f17d490a84c83f1561979d63`; its worktree is clean at handoff. No stage/commit/push or gitlink update is authorized. The rendered workflow's diff-staging instruction must be satisfied by a temporary unified diff without staging, in accordance with the user's stronger instruction.

- Implementation and acceptance audit complete: the owning platform helper/test files, runner query/environment changes, pre-resource AppHost pair admission and both evidence appendices are present in the unified diff. The original persisted journey is unchanged. Python 85/85, platform model tests 13/13 and compiled guards 35/35 cover every matrix row. Supported launch: `DirectCommandPreservesProjectGraphApplicationArgumentsAndAnnotationsAsync`, the runner resolution test, and actual native-process live receipts; unsupported metadata: metadata/query-failure regressions; polluted caller: casing/prefix regression with unchanged caller; invalid context: compiled pair/component/control-plane/publish refusals; ordinary start: compiled ordinary-defaults probe. All covering cases ran and passed.
- Fresh consistent Debug/source rebuild passed with zero warnings/errors; the six default suites passed 1,949 with five intentional skips. The unchanged live journey passed 1/1 (128.350s), with direct DCP parentage, old/new gateway identity replacement, shared preservation and complete cleanup independently checked by the parent. Receipts are in `/tmp/timesheets-direct-eventstore-live/run-4v47r76d`; exact commands and the existing Works version-forwarding limitation are in the appended test summary. Initial failed results and filesystem incidents remain retained.
- Final corrected-source verification passed after all review patches: clean solution build, six individual default suites (1,949 passed, five intentional skips), Python 92/92, helper tests 13/13, compiled guards 35/35 and package probes 5/5. The checked-in live lane passed 1/1 in 131.536s with native DCP ownership, PID/start replacement, all 16 captured identities exited, shared preservation and complete owned cleanup. The preceding admission refusal created no resources; the known-failing pre-adapter test invocation remains failed/interrupted evidence. Current receipts, source hashes and exact commands are retained in [the durable proof packet](evidence/3-6/20261018-direct-eventstore-local-proof/README.md#final-corrected-source-verification). Verification uses the current catalog's explicit version property; the pre-existing Works forwarding issue remains deferred. No Git mutation is authorized or performed.

## Spec Change Log

## Review Triage Log

- BH1 — high, patch: the AppHost call is compiled in package mode, but the installed EventStore.Aspire 3.125.1 DLL has no `WithCompiledProjectExecutable`. The csproj selects that package outside source mode; root targets enforce Debug/source and Release/package. Gate the source-only call and reject unsupported direct-launch settings without changing ordinary package startup; verify the package compilation branch without producing local Release assets.
- BH2 — medium, patch: the exact-project query accepts inherited SDK `RunCommand=/usr/bin/true` and `RunWorkingDirectory=/tmp`; existing file/argument checks then accept the wrong command. Remove inherited SDK run/Start overrides in the private environment, retaining caller state and project-defined metadata, and add regressions.
- BH3 — low, rejected: owner mode 0610 really passes the helper's any-execute-bit check while the current UID lacks X_OK (independently reproduced as UID 1000). The supported fixture already rejects it through os.access before infrastructure; other API callers receive the runtime permission failure. This unusual permission arrangement would require additional platform-native effective-access machinery beyond a direct correction; it is not added to this wrapper-removal increment.
- BH4 — low, rejected: a manual validation-only probe with missing file paths does print compiled *selection*. This no-resource probe validates settings/context, while the runner checks physical paths before infrastructure and the SDK helper rejects them before actual startup. Extending it would duplicate the centralized validation or add another API; the routine actual-process regression below closes the consequential wiring gap.
- BH5 — medium, patch with VG1: helper/model tests inspect annotations and callback arguments but do not observe the AppHost's actual launch recipe. Existing callback tests already prove application-argument preservation; add actual native-command/DCP ownership checks to the supported isolated acceptance lane.
- BH6 — low, rejected: the new JSON labels are the explicit Debug/source query inputs; exact SDK, version property, source identities, build and actual-command receipts are documented alongside them. A new comprehensive binary/dynamic-dependency digest protocol is beyond a direct correction and native-shim hashing alone would not establish assembly-graph identity. The demonstrated mixed-version configuration remains a separate recorded issue.
- BH7 — medium, patch with VG1: the only native-command and replacement assertions were in an untracked temporary observer. Promote the checks into the checked-in runner so a future ordinary journey success cannot conceal restoration of the wrapper.
- BH8 — low, patch: temporary receipts disappear across cleanup/reboot. Retain a small sanitized proof bundle with input hashes under implementation evidence, linked from the summary; preserve its local qualification limits.
- BH9 — low, rejected per review workflow's spec-edit rule: the initial planning paragraph says no platform code has been written. Subsequent dated audit/implementation notes and the current status record implementation. No frozen intent or planning-history rewrite is made for this finding.
- VG1 — medium, patch (pre-verified gap): deleting the AppHost helper call leaves configuration-only probes, separate helper tests and mocked startup checks green, and the original journey can pass with a wrapper. Add repeatable assertions of actual selected native command, direct DCP parentage and old/new PID/start replacement around the unchanged journey; preserve primary failure and owned cleanup.
- Edge Case Hunter — no findings after tracing and claims comparison.
- Parent C1 — low, patch: new public test methods lack the XML documentation required by the loaded Hexalith baseline. Add summaries and parameter documentation only.
- Parent C2 — medium, defer: Works' pre-existing EventStore-version default/forwarding disagrees with the current root catalog. This was observed before corrective verification and is not caused by direct execution; the successful run uses an explicit property matching the catalog, with no dependency update.
- Parent C3 — medium, patch: full default Integration verification exposed an existing security fitness constraint: `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` rejects conditional compilation anywhere in `Program.cs`. BH1's compile guard triggers that assertion. Preserve the test and unconditional listener wiring; move only the private gateway build-mode adapter into a focused AppHost type while keeping executable plumbing in EventStore.Aspire. The original failed invocation is retained in `/dev/shm/timesheets-direct-final-suites-1ih3uduz/Hexalith.Timesheets.IntegrationTests.log`.

- Patch outcomes: BH1's published-package compilation and five no-resource probes pass with the internal adapter; BH2's real poisoned-environment SDK query and project-defined metadata regressions pass; BH5/BH7/VG1 now have checked-in native-command, direct DCP and replacement checks, including primary-failure and diagnostic-write cleanup regressions. The verification reviewer independently confirmed VG1 closed with five passing focused cases. BH8 has a sanitized durable evidence packet with matching source hashes; C1's public test documentation is complete. C3's original source-fitness test remains unchanged and passes in the focused and complete corrected Integration runs; all 35 compiled guards and five package probes pass after moving the adapter. C2 is appended once to the deferred-work ledger. Complete corrected-source verification and the checked-in live lane subsequently passed as recorded above.

## Verification

- Run the full isolated-runner Python regression module and py_compile.
- Build EventStore.Aspire and its focused AppHost model tests in Debug/source mode; run the focused tests individually.
- Restore/build `Hexalith.Timesheets.slnx` in Debug/source mode with warnings as errors; run each test project separately.
- Run harmless compiled AppHost guards and the unchanged isolated live journey. Retain direct-process replacement, state-preservation and cleanup evidence. An intermittent passing baseline alone does not prove the corrective launch mechanism.
