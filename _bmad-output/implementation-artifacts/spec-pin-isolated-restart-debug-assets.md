---
title: 'Keep isolated restart verification on current Debug source assets'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Restart-timeout investigation requires the freshly built Debug source composition. After the CI change made an unspecified configuration select Release, the isolated runner's unqualified `aspire start --no-build` launches existing Release assets instead. Its Debug test executable can therefore exercise a different runtime graph. This is a confirmed prerequisite defect, not an established cause of the earlier EventStore stop timeout.

**Approach:** Pin Debug and source-reference dependency mode in the fixture's private child environment so Aspire starts the assets the documented local build produces. Keep the caller's environment unchanged, and keep admission, private component validation, privacy, all existing live assertions, original failure status, shared-state preservation, and exact owned cleanup intact. Add a regression for inherited Release/package-mode settings. Verify the normal EventStore restart with the unchanged isolated journey when the environment is eligible; retain collisions and any stop failure as failures. Do not change the AppHost topology, SDK/dependency pins, shared services, production behavior or deployment status, and do not infer that this prerequisite correction repairs the historical timeout without fresh evidence.

</frozen-after-approval>

## Implementation Notes

- No unresolved user-intent choices, irreversible changes, new public API or dependency updates. The footprint is `scripts/run_isolated_magic_link_dapr.py`, its existing Python regression module, this spec, and the existing investigation/local-proof evidence records.
- Reuse `IsolatedFixture.environment`, a copy of `os.environ`, for a local Debug/source-mode pin. `run_command`, `_journey_command` and the guard driver already pass this copy to owned child commands. Preserve all runtime ownership and state comparisons.
- Current clean source baseline is the user-provided CI commit `c4695a5`; obtain its full revision for final evidence. `global.json` now selects SDK 10.0.402. Explicit Debug/source restore and solution build passed; build zero warnings/errors in 28.56 seconds (`/tmp/timesheets-restart-resume-build.log`). Python baseline passed 80/80 in 19.340 seconds (`/tmp/timesheets-restart-resume-python-baseline.log`).
- The first resumed attempt selected `bin/Release/net10.0` and Release child commands. It was interrupted during cleanup; its three private containers/components were subsequently removed through exact recorded ID/name/label verification (`/tmp/timesheets-restart-interrupted-cleanup/run-zw_uost6`). Both active Aspire/Dapr inventories were empty before recovery; shared preservation passed. This is not live acceptance.
- A temporary, untracked observer then pinned Debug/source mode and retained sanitized resource logs plus process/pipe metadata before cleanup. Its admitted journey failed before restart when a second `eventstore` app appeared (`/tmp/timesheets-restart-observed-baseline/run-4opc78a8`): 0/1 passed, all 15 captured process identities gone, all three private containers and component copies removed, and shared preservation matched. No foreign runtime was stopped. Do not attribute the historical timeout to that collision.
- Verification plan: demonstrate the inherited-mode regression failing against the current runner, implement the small environment correction, run the full focused Python module and compile checks, then run the supported isolated journey with its normal restart command. Obtain independent review and record the distinction between the corrected prerequisite, current live results and the still-unestablished historical cause.
- Independent review and the live run exposed why environment values alone are insufficient: Aspire 13.6.1 reuses cached RunCommand/TargetPath before evaluating MSBuild, and the cache fingerprint excludes ambient Configuration. Its supported project-local `dotnetAppHostInfoCacheDisabled` escape hatch bypasses read/write of this metadata without deleting shared caches or changing the runtime registry. Add that key to `aspire.config.json`; preserve its AppHost path. Remove case-insensitive aliases of the three mode keys before setting canonical values, since MSBuild treats those names case-insensitively. Retain actual loaded-Debug launch evidence; mocked command-environment assertions alone cannot establish correct asset selection.
- Footprint clarification: `aspire.config.json` is also part of this prerequisite correction. The frozen intent is unchanged; the first footprint bullet omitted this supported cache-bypass setting.
- Final prerequisite verification: all 82 Python regressions passed in 19.228 seconds (`/tmp/timesheets-restart-debug-reviewed-python.log`); both edited Python modules compile, and `git diff --check` passed. A real validation-only CLI qualification retained two stale Release cache entries while launching the Debug AppHost and returning before resources: `/tmp/timesheets-restart-cli-mode-probe/run-b2slcsnk/qualification.json`. Independent review inspected the actual CLI log and confirmed no remaining prerequisite implementation issue.
- Fresh Debug runtime evidence is deliberately mixed: `run-bl0mn9el` reproduced the original restart exit 16 after successful preceding journey assertions; `run-jilrm2ix` subsequently passed the unchanged full journey 1/1 in 130.510 seconds. Both retained successful shared-state comparisons and complete owned cleanup. The latter retry does not establish that the intermittent stop fault is repaired. See the appended investigation for the captured child-held pipes and the separate platform implementation plan.
- These assets were built from Timesheets `c4695a56c272021f316849ad60229871e456cfe6` and EventStore `ab8fa115c486875be3a22860a273d5d68a26c749`. External work subsequently moved twelve reference checkouts, including EventStore to `d55c6f7c11e9bce6f17d490a84c83f1561979d63`. Those changes were preserved and are not part of this correction. No staging, commit, push, branch or dependency update was performed by this workflow.
- External commit `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` subsequently recorded the prerequisite code/configuration changes, initial spec and reference updates. This workflow did not create that commit. The appended verification and triage notes remain separate working-tree edits.
- All six individually invoked default executable suites subsequently completed: 1,949 passed, five intentional skips, zero errors/failures. The initial Projections run exceeded an outer 180-second wrapper; its unrestricted rerun passed all 437 tests in 459.692 seconds. Final Aspire/Dapr inventories and the fixture-container label query were empty. These receipts remain tied to the earlier built-source baseline, not a rebuild of the external commit.

## Review Triage Log

- BH1 — medium, patched: a cached Release launch survives an environment-only pin because Aspire's cache fingerprint omits ambient Configuration. The project-local cache-disable setting and real stale-cache qualification establish effective Debug selection.
- BH2 — medium, patched: conflicting environment-key casing is reachable on Linux and MSBuild treats the names case-insensitively. Remove aliases before setting canonical values; regression and offline MSBuild evaluation cover conflicting lower/uppercase aliases.
- BH3 — medium, patched: mocked command environments cannot establish loaded assets. The real validation-only CLI launch used Debug while stale Release cache entries remained; reviewer confirmed the log and clean no-resource exit.
- BH4 — low, corrected in implementation notes: the original footprint omitted `aspire.config.json`. The added footprint clarification records the final scope without altering frozen intent.
