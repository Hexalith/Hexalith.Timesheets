---
title: 'Close Story 3.6 durable magic-link review gaps'
type: 'bugfix'
created: '2026-10-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fde8f30f355064a4eb9396460298ce04338a4d8d'
context:
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A rejected magic-link command poisons an EventStore stream. Short status polling, incomplete tenant evidence, inaccurate gateway fixtures, and stale contracts obscure live behavior.

**Approach:** Make rejection replay safe, bound and configure status polling, enforce trustworthy projection scope, prove workload submission, then align the API and release evidence.

## Boundaries & Constraints

**Always:** EventStore owns writes; only `Completed` with exact stored-batch readback acknowledges use. Invalid or uncertain outcomes share the opaque denial. Keep deployed Dapr, binding, recording, projection-handler, and history-inventory gaps open until proven.

**Never:** Bypass EventStore, trust the token index for use, claim live acceptance from TestServer, or alter the already-correct gitlink and story status.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Rejection replay | Rejection, then valid command; actor recreated | Later command commits | Rejection is a no-op |
| Status delay | `Processing` becomes `Completed` by deadline | Exact batch yields success | Timeout or `PublishFailed` denies |
| Tenant mismatch | Missing or different trusted scope | No read model | Fail closed |
| Public submission | Valid confirm or adjust commits | Documented HTTP `202` | Otherwise opaque `403` |

</frozen-after-approval>

## Code Map

- `MagicLinkDurableSubmissionService.SubmitAndReadStatusAsync` — fixed 5 × 100 ms poll; exact `Completed` readback exists.
- `TimeEntryState.Apply`, `MagicLinkCapabilityState.Apply` — missing rejection fold.
- `TimeEntryEvidenceProjection.MatchesTenant` — terminal checks incomplete; recording needs trusted envelope scope.
- `EventStoreGatewayWorkloadAssertionSource.IssueAsync` — blank `party_id` blocks fallback.
- `MagicLinkConfirmationHttpBoundaryTests.ScriptedEventStoreGateway` — omits persisted rejections; two tests use the real gateway.
- `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.ScriptedGatewayClient` — aliases human and workload reads.
- `timesheets-capture-contracts.v1.json` — submit routes advertise only `403`.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Timesheets.Server/{TimeEntries/TimeEntryState.cs,MagicLinks/MagicLinkCapabilityState.cs}` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` — add no-op rejection folds; prove valid work after persisted rejection before and after recreation.
- [x] `src/Hexalith.Timesheets.Server/MagicLinks/{MagicLinkSubmissionPollingOptions.cs,MagicLinkDurableSubmissionService.cs}` and `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — validate a bounded 5-second default/100-ms poll and inject options; retain `Completed` and exact readback.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/{MagicLinkConfirmationHttpBoundaryTests.cs,MagicLinkPersistedSubmissionTests.cs}` and new `tests/Hexalith.Timesheets.Server.Tests/MagicLinkDurableSubmissionServiceTests.cs` — store rejected/stored-status outcomes in fakes; pin deadline, status, receipt fields, cancellation, faults, and message IDs.
- [x] `tests/Hexalith.Timesheets.Server.Tests/{EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs,RuntimeRegistrationTests.cs}` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — distinguish workload calls, prove assertion routes/header, register the fake in every HTTP factory mode.
- [x] `tests/Hexalith.Timesheets.Server.Tests/MagicLinkEventStoreDomainProcessorTests.cs` — prove verified-origin and actor guards with valid positive/negative issue, use, and revoke pairs.
- [x] `src/Hexalith.Timesheets.Projections/{TimeEntries/TimeEntryProjectionEvent.cs,TimeEntries/TimeEntryEvidenceProjection.cs,TimesheetPeriods/TimesheetPeriodProjectionEvent.cs,TimesheetPeriods/TimesheetPeriodSummaryProjection.cs}` and `tests/Hexalith.Timesheets.Projections.Tests/{TimeEntryEvidenceProjectionTests.cs,ApprovedTimeLedgerProjectionTests.cs,TimesheetPeriodSummaryProjectionTests.cs}` — require tenant scope from delivery envelopes for recordings, preserve it through period conversion, and reject cross-tenant terminals across dependent folds.
- [x] `src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs`, OpenAPI JSON, and `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` — align blank-claim fallback and advertise verified `202` plus opaque `403`.
- [x] `README.md`, Story 3.6/3.7 files, `sprint-status.yaml`, `deferred-work.md`, `tests/3-6-test-summary.md`, `docs/launch-readiness.md` under `_bmad-output/implementation-artifacts` where applicable — reconcile review items against HEAD, current evidence, and explicit release gates.

**Acceptance Criteria:**

- Given a persisted rejection, when the same actor processes a later valid command before or after recreation, then replay succeeds and only the valid command changes capability or Time Entry state.
- Given delayed or non-`Completed` gateway status, when a public submit route runs, then only a timely `Completed` plus matching stored batch returns `202`; every other outcome remains opaque and privacy safe.
- Given cross-tenant or unscoped recording evidence, when any dependent projection folds it, then it fails closed without publishing a read model.
- Given the composed host and a valid binding issuer, when the loader or submitter calls EventStore, then workload-only routes carry signed tenant/domain bindings and an actor binding for authenticated management calls; blank `party_id` uses `NameIdentifier`.

## Implementation Notes

The diff audit confirmed all eight execution tasks and all four acceptance criteria against the changed code and tests. The I/O matrix is exercised by persisted rejection/recreation actor tests, delayed and non-Completed HTTP status tests, unscoped/cross-tenant projection tests, and valid confirm/adjust HTTP submissions with opaque denial comparisons. These tests ran in the passing Integration, Server, Projections, and Contracts lanes. Live Dapr and deployed-history proof remains a separate Story 3.6 release gate.

## Spec Change Log

## Review Triage Log

| Layer / finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1 — unbounded stream readback | medium | Defer. All three readbacks use the request token without a service deadline. This predates the polling change; deployed latency policy and live topology must settle the broader readback bound. |
| Blind 2 — timed-out gateway work keeps running | medium | Patch. `WaitAsync` bounds the waiter but the gateway receives only the request token; link a deadline token to its calls. |
| Blind 3 — stored-event correlation absent | medium | Defer. EventStore persists command correlation/causation metadata, but receipt verification checks sequence and payload only. This pre-existing proof gap needs a receipt-policy change across issue, transition, and use. |
| Blind 4 — prior adjustment audit snapshot unchecked | medium | Defer. Existing adjustment receipt verification compares several prior fields but not service date, duration, billable state, or comment to loaded Time Entry state. Passing resolved state into verification changes the existing receipt contract. |
| Blind 5 — checkpoint tenant unchecked | medium | Defer. Evidence and period folds accept a Fresh checkpoint scoped to another tenant, and ledger can publish export readiness from it. This pre-existing checkpoint contract needs a common scope validation across all read models. |
| Blind 6 — no combined host-to-workload proof | medium | Patch. The composed host test resolves the real assertion source but uses a fake gateway; the route test uses a real gateway but substitutes its assertion source. Add a combined DI/transport regression. |
| Blind 7 — configuration binding untested | medium | Patch. Service tests use `Options.Create` and host tests use defaults, so a changed binding key would pass. |
| Blind 8 — malformed JSON `400` omitted | low | Patch. The public route's model binder returns `400` before the handler, as the existing HTTP test proves; add that response to the OpenAPI operations. |
| Blind 9 — contradictory Completed rejection type | low | Patch. A `Completed` receipt with `RejectionEventType` set passes existing status checks; reject it and test the field. |
| Blind 10 — tenant scope optional at construction | low | Patch. Both projection event records default `TenantId` to null, so omitted scope compiles and later throws; require an explicit constructor argument while retaining null as a tested denial input. |
| Blind 11 — retry request identity changes | medium | Defer. Endpoints already use `TraceIdentifier`, so a later HTTP retry after an uncertain commit gets a new message ID. A reconciliation policy must define the user-visible result. |
| Edge 1 — gateway token after timeout | medium | Patch, same root as Blind 2. `WaitAsync` does not cancel the transport call. |
| Edge 2 — sub-millisecond polling interval | medium | Patch. Current option validation accepts one tick and can issue a near-tight loop of status requests. Set a safe minimum and test it. |
| Edge 3 — empty period misses terminal tenant | medium | Patch. A period with no included entries skips the evidence fold, so a cross-tenant magic-link terminal with matching envelope scope reaches a summary. Validate all normalized Time Entry payloads before publishing. |
| Verification 1 — bound options not tested | medium | Patch, same root as Blind 7. A wrong configuration section would retain defaults. |
| Verification 2 — revoke/expire arms untested | medium | Patch. Removing either new tenant switch arm leaves the existing used-only test green and lets the list publish a row. |
| Verification 3 — period rejection arm untested | medium | Patch. The added tenant condition for `TimesheetPeriodRejected` has no negative test; a cross-tenant decision could enter the summary if removed. |

All ten patch groups were applied and passed targeted tests, followed by the warning-as-error solution build and all six full Timesheets test lanes. The five deferred findings are recorded in `deferred-work.md`; none is claimed as complete by this spec.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — passed under SDK 10.0.401.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — passed with zero warnings/errors. The bare build without `UseHexalithProjectReferences=true` failed in Works with `HXW0002` and missing dependency references; this is a pre-existing build-mode blocker.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>` — each of the six projects ran individually: Architecture 56/56, Contracts 92/92, Server 512/512, Projections 173/173, Integration 652 passed/four intentional skips, Works 76/76. Total 1561 passed, four skipped, zero failed. The focused magic-link HTTP class passed 557/557.
- `git diff --check` — passed after review fixes. Live Dapr-backed EventStore acceptance and deployed history inventory were unavailable; release remains `FAIL` and Story 3.6 stays `in-progress`.
