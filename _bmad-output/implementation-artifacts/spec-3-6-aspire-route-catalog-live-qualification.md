---
title: 'Qualify the current Aspire route-catalog journey for Story 3.6'
type: 'chore'
created: '2026-11-02'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1086a565b932210cc421e1b66f39f68a86a3e7ee'
story_key: '3-6-implement-eventstore-backed-magic-link-state-loading'
context:
  - 'AGENTS.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="user explicitly authorized implementation and push in this invocation">

## Intent

**Problem:** The eight-route compiled owner source is enrolled in the HTTP host, but the current Aspire 13.6.4 journey does not activate the SDK's private catalog bootstrap. Its unrelated shared Redis preservation scan prevents local qualification and can block foreign clients.

**Approach:** Compose the SDK catalog with a run-private owner profile over the runner's freshly created isolated Redis, observe the real receivers and workload policies, and exercise valid, forged, undeclared, and keyed-retry requests with EventStore/Dapr persisted end-state assertions. Record source, restart/rebuild, preservation, and owned cleanup evidence; update deferred-work and launch records and push verified changes to main.

## Boundaries & Constraints

**Always:** Use the compiled eight-route source, selected Debug/source assets, supported SDK preparation/composition and private Development custody. Require idle admission, exact owned component/sidecar identity, fail-closed authorization, persisted readback, strict cleanup and source consistency. Keep Story 3.6 in progress and launch FAIL until deployed authority, inventory/continuity and shared-rebuild acceptance is proven.

**Never:** Read or lock foreign shared Redis for this journey; clear or change foreign state; synthesize receiver, workload or persisted outcomes; weaken the default-deny routes; expose keys, tokens, payloads or private catalog custody; initialize nested submodules or mix the Release/package graph into the local runtime.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Valid | Declared command and projection via actual host | Accepted, one durable EventStore effect and selected Dapr read model | Refuse on incomplete receiver/catalog observation |
| Forged or undeclared | Invalid workload binding or absent route | Denied before domain effect | Verify persisted streams and read models unchanged |
| Keyed retry | Same key and intent, then conflicting intent | One durable effect; conflict denied | Verify exact stored events and read models |
| Unsafe storage or runtime | Missing private owner, drifted sidecar, occupied admission | Stop before writes or fail closed | Retain sanitized blocker and cleanup receipt |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/TimesheetsProjectionRouteCatalogDeclarations.cs` — sole compiled source for four Server commands and four named projections; preserve exact route IDs and schemas.
- `src/Hexalith.Timesheets/Program.cs` — already enrolls Server and Projections; SDK finite receiver fence activates only under private bootstrap.
- `src/Hexalith.Timesheets.AppHost/Program.cs` — wire selected Timesheets owner, gateway and receiver cohort after Dapr sidecars; keep ordinary startup inert.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalog{AspireExtensions,OwnerPreparationExtensions,CompositionOptions}.cs` — supported preparation and composition APIs; read-only dependency.
- `scripts/run_isolated_magic_link_dapr.py` — owns admission, private Redis/placement/scheduler, source snapshot, lifecycle and cleanup; replace shared scan gate with proven owned isolation for this mode.
- `scripts/local_aspire_magic_link_journey.py` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — extend existing persisted journey with actual catalog observations and request matrix.
- `scripts/test_isolated_magic_link_dapr.py`, HTTP route-catalog tests, `docs/launch-readiness.md`, deferred-work and Story 3.6 records — regression and evidence targets.

## Tasks & Acceptance

**Execution:**
- [x] AppHost and runner — prepare selected compiled owner, compose SDK catalog with an owned isolated backend/profile, retain private custody and ensure ordinary starts remain inert.
- [x] Runner and tests — avoid all shared-store I/O in owned mode; require exact owned infrastructure and actual sidecar/receiver/workload observations before fixture requests.
- [ ] Journey and tests — drive valid, forged, undeclared and keyed-retry requests; assert stored EventStore events and Dapr read models before and after restart/rebuild.
- [x] Evidence and records — capture full source identities, command outcomes, lifecycle/preservation/cleanup receipts; update deferred-work, test summary, Story 3.6 and launch FAIL.
- [ ] Verify focused and relevant broad gates, review the diff, validate a Conventional Commit, fetch/divergence-check and push main.

**Acceptance Criteria:**
- Given a fresh owned store and compiled eight-route source, when Aspire starts, then the SDK reports the exact real receivers, policies and owner routes without a shared-store read.
- Given valid, forged, undeclared and keyed-retry calls, when the host processes them, then exact persisted EventStore/Dapr end state matches the allowed effects and denied calls add none.
- Given an unsafe or incomplete owner/backend observation, when qualification runs, then it fails closed before an unsupported claim and cleans only owned resources.
- Given local proof and unfinished deployed acceptance, when records are reconciled, then Story 3.6 stays in progress and launch remains FAIL.

## Implementation Notes

Planning found no human-only intent gap. The user authorized local isolated fixture writes and publication. The isolated store is disposable; the push is the only lasting external side effect. Changes will affect the AppHost, runner, journey/tests, a new evidence packet and existing records. Historical shared-store failures remain unchanged.

## Spec Change Log

- 2026-11-02: Implemented the private Development owner selection, SDK composition, runner custody/receiver checks and persisted request matrix without changing the frozen intent. The first supported runtime attempt refused idle admission before resource creation; live acceptance remains open.

## Review Triage Log

| Finding | Verdict and disposition | Evidence |
| --- | --- | --- |
| B1: profile file passed where directory required | high / patched | The journey's runtime verifier now receives the private profile's parent; focused journey regression passes. |
| V2: profile file passed where directory required | high / patched | The same focused journey regression covers this independent duplicate finding. |
| B2: AppHost did not prevalidate exact receiver grants | medium / patched | Exact closed policy text is checked before SDK composition; mutation guards pass. |
| E1: AppHost did not prevalidate exact receiver grants | medium / patched | The same exact policy check covers this independent duplicate finding. |
| B3: owner profile coordinates were incompletely bound | medium / patched | Fixture identity, authority key, digest generation and secret coordinates are now checked before resources. |
| B4: cleanup could discard keys after incomplete SDK custody | medium / patched | Cleanup retains protected profile and fails if complete SDK custody cannot be verified. |
| B5: duplicate authority metadata could mask a foreign endpoint | high / patched | Loaded metadata names and exact private Redis values must match; simulated drift tests pass. |
| B6: partial readback fields could appear complete | medium / patched | Facet digests, required hosts, epoch and SDK binding shape are compared before fixture writes. |
| B7: owner hashes were only syntax-checked | medium / patched | Observed executable hashes now match the captured Debug/source binary hashes. |
| B8: undeclared 404 could be a missing endpoint | medium / patched | The journey now requires 403 and unchanged persisted state. |
| B9: keyed retries compared only event names | medium / patched | Both retry and conflicting intent compare the complete stored stream. |
| B10: restart lacked sidecar reinspection | medium / patched | The runner repeats exact Dapr component, ACL and sidecar checks after gateway restart. |
| V1: runtime catalog branch lacked a call-site test | medium / patched | Simulated `verify_runtime_components` coverage rejects changed ACL and authority component. |
| E2: symlinked ancestors could redirect private custody | medium / patched | AppHost rejects linked ancestors before opening private profile inputs. |
| E3: partial JSON observation escaped structured refusal | medium / patched | Malformed SDK files now raise a sanitized `FixtureError`; focused receipt test passes. |
| E4: sidecars and receivers could select different SDK directories | medium / patched | The runner requires both sidecars and hosts to identify one exact private directory. |
| E5: Timesheets admission inventory was not exact | medium / patched | The nontechnical Timesheets source is required to report no admission policies; synthetic mismatch rejects. |
| E6: writer activation preceded receiver observation | high / patched | Committed SDK catalog and receiver observation now precede fixture writer activation. |
| Review follow-up: ordinary readback binding shape | high / patched | SDK requires null binding fields for nontechnical receivers; the runner now mirrors that condition. |
| Review follow-up: provenance diff included itself | medium / patched | Source provenance now hashes an explicit code/test diff from the pinned baseline commit. |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — matching source restore.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings/errors.
- Focused Python and six built xUnit v3 executables individually — relevant cases pass; retain any incomplete broad lane honestly.
- Supported isolated runner once per prepared attempt, then inspect persisted receipts, owned cleanup, and `git diff --check`.

**Current result, 2026-11-02:** Matching Debug/source restore and final warnings-as-errors solution build pass with zero warnings/errors. The runner suite passes 216/216, catalog HTTP cases pass 30/30, and LaunchReadiness passes 16/16. The single `--route-catalog` runner attempt exited 1 at idle admission because a separate EventStore AppHost already owned eight Dapr applications. [Sanitized receipts](evidence/3-6/20261102-route-catalog-live-proof/README.md) show zero fixture resources and complete no-resource finalization. Shared Redis was not read or locked. Actual SDK receiver/workload and persisted EventStore/Dapr end state, restart/rebuild preservation, source consistency across a live lifecycle and owned runtime cleanup are unobserved. Current runtime qualification and deployed acceptance remain open; Story 3.6 is in progress and launch is FAIL. The six broad built executables were not rerun in full for this blocked local attempt; the focused checks above are the current verification scope.
