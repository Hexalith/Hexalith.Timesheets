---
title: 'Prove Story 3.6 against local Dapr persistence'
type: 'feature'
created: '2026-10-10'
status: 'in-progress'
route: 'dispatch'
baseline_commit: 'dd1028808b6592b9ff869d2dce5c1c24a5ad7cf5'
review_loop_iteration: 0
context:
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The production Time Entry recording writer, magic-link loader, and atomic use path now pass local tests, but those tests substitute the EventStore gateway and read-model store. No test proves the issue-to-use path against a real local Dapr actor and persisted EventStore state.

**Approach:** Add one opt-in local Dapr acceptance journey using the existing EventStore runtime and Timesheets code. Verify a recorded entry, issued capability, delivered index/catalog, valid GET and confirm/adjust POST, stored effects, restart, and single-use race. Report any missing platform prerequisite precisely; keep Story 3.6 open for production identity, topology, read-model, and deployed-history gates.

**Decision (2026-10-11):** Use the local Dapr target first. Deployed closure remains a later release gate.

## Boundaries & Constraints

**Always:** Use the real EventStore gateway, Dapr actors, and persisted read-model store for the acceptance claim. Development-only identity setup and a fixture access guard may seed authorized test data; identify these substitutions in evidence. Require `Completed` and matching stored-event readback before success. Keep invalid outcomes opaque, and isolate test state from existing data.

**Never:** Reimplement the completed recording writer, add a Timesheets host/topology, weaken production authorization, write domain state outside EventStore, or claim deployed acceptance from local results. Capability and Time Entry read-model handler wiring remains a separate release gate; the loader folds their authoritative streams directly.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Valid journey | Recorded entry, issued link, delivered index and fresh catalog | Valid GET; confirm or adjust `202` after persisted use/effect batch | No success before exact readback |
| Replay and race | Restart, reused token, concurrent submits | Exactly one stored use; later requests get the common opaque denial | No second effect |
| Missing prerequisite | No Dapr resource, binding credential, or healthy store | No acceptance claim | Exact command/result in evidence; story remains open |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` — completed write paths with status/readback verification; reuse unchanged.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` — reads candidate index and fresh catalog, then folds capability and Time Entry streams.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` and `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs` — existing delivered read models required by the journey.
- `src/Hexalith.Timesheets.AppHost/Program.cs` — no EventStore resource or Dapr sidecar; leave it unchanged. `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` owns a runnable local platform topology.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing.Integration/DaprDomainServiceTestFixtureBase.cs` — reusable sidecar fixture; it does not by itself start the EventStore actor service.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — existing HTTP fixture substitutes gateway/store/guard; reuse test data only, not its persistence substitutes.

## Tasks & Acceptance

**Execution:**

- [x] `docs/local-magic-link-dapr-proof.md` — first run and record a preflight for existing EventStore-owned runtime resources, Timesheets Dapr identity, development signing, fresh catalog delivery, and isolated state. If the real path requires a forbidden new host/topology or unavailable platform support, record the exact blocker and stop dependent test work.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — when preflight succeeds, add an opt-in test (`TIMESHEETS_LIVE_DAPR=1`) that calls real local HTTP/Dapr/EventStore endpoints, verifies stored record/issue/use events and index/catalog delivery, and fails rather than skips when an opted-in prerequisite later disappears.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprFixture.cs` — connect to the platform-owned local runtime with isolated test identities and state; run confirm and adjust, a one-winner race, and restart/replay checks without substituting gateway or read-model storage.
- [ ] `docs/local-magic-link-dapr-proof.md` — complete exact local start, credential, projection-delivery, test, state-inspection, restart, and cleanup commands; keep secrets and token material out of captures.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, and `docs/launch-readiness.md` — record executed commands and persisted observations, or the exact blocking prerequisite. Preserve `in-progress`/`FAIL` and separate production gates.

**Acceptance Criteria:**

- Given a healthy isolated local Dapr/EventStore target, when the opt-in test records, issues, projects, and uses a valid link, then HTTP success follows verified stored events and the index/catalog were populated through real delivery.
- Given a committed use, when the actors or processes restart and the token is replayed or raced, then one use/effect batch remains and every losing request returns the common opaque denial.
- Given an unavailable prerequisite, when the opt-in lane runs, then it fails with a support-safe, exact blocker and creates no false acceptance claim.

## Implementation Notes

- 2026-10-11: The checked-in EventStore AppHost does not attach Timesheets or permit its trusted effects; the Timesheets AppHost has no EventStore resource or sidecar. The observed EventStore runtime belongs to another integration test. The local `statestore` scope excludes Timesheets, so a sidecar alone would not deliver the index/catalog. The preflight stop condition applies. The live fixture, opt-in test, dependent runbook steps, and first two acceptance criteria remain open; no persisted journey or matrix test was run. See `docs/local-magic-link-dapr-proof.md` for exact commands and the required platform-owned topology and read-model-store prerequisite.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — succeeds with current `global.json`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings/errors.
- Run all six Timesheets test executables separately; run `TIMESHEETS_LIVE_DAPR=1 tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'` against the prepared target.
