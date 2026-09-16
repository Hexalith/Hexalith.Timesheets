---
title: 'Close Story 3.6 loader policy and evidence gaps'
type: 'bugfix'
created: '2026-09-16'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'ab4b48f8c11f8f239f0da923cd21824c5f2083a9'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 still permits inconsistent capability/Time Entry Activity Type evidence and project-owned types although the production catalog is tenant-only. Correction events also discard the server-resolved scope, so a Project-to-Tenant correction replays with the old scope and an otherwise-valid reissued link fails closed.

**Approach:** Enforce one tenant-owned boundary across loading, issue, display, confirm, and adjust, and add one backward-compatible optional scope fact to correction snapshots so future replay preserves the authorized decision. Keep legacy scope-less corrections deterministic and fail-closed; complete falsifying tests and release-risk evidence without migration tooling or topology changes.

## Boundaries & Constraints

**Always:** Keep EventStore authoritative and the token index non-authoritative. Derive correction scope server-side; new correction/adjustment snapshots record it, aggregate/projection/ledger folds agree, and legacy missing scope retains the preceding scope with compatible duplicate handling. Require matching IDs plus tenant-scoped Time Entry/catalog evidence; preserve opaque denials, cancellation, and Project/Work targets using tenant types.

**Never:** Accept caller-supplied scope, infer legacy scope from a current catalog, rewrite historical events, or add reconciliation/bulk migration. Do not add dummy reads, response padding, latency thresholds, a project-catalog writer, direct Dapr state access, topology, UI, package upgrades, sibling edits, or deferred platform/agent-context work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Supported target | Project or Work target with one tenant-owned Activity Type | Issue, describe, confirm, and adjust retain existing valid behavior | No new disclosure or persistence path |
| New cross-scope correction | Server resolves Project-to-Tenant or Tenant-to-Project | Event, aggregate, evidence projection, and ledger record the resolved new scope | Tenant result may load; Project result remains opaque/fail-closed |
| Legacy scope-less correction | Old event omits scope | Replay retains its preceding scope and duplicates remain idempotent | A stale Project scope is not repaired by token reissue |
| Disallowed type | Project-owned Activity Type during issue/adjust or in existing folded state | Reject before token generation or dispatch; existing links return the canonical opaque denial | No capability-use or Time Entry event |
| Inconsistent bundle | Capability and Time Entry differ by Activity Type ID/scope or a later event has another tenant | Loader returns the unavailable bundle | Same external denial as unknown token |
| Read/integrity failure | Index read throws or page `EventCount` disagrees with events | Fail closed without EventStore command work | Caller cancellation still propagates |
| Early rejection | Blank or hash-derivation-rejected token | No read-model or EventStore I/O | Response remains envelope-equivalent; latency is not guaranteed constant |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionValues.cs` and `openapi/timesheets-capture-contracts.v1.json` -- add optional nullable scope; absent means legacy, never caller authority.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` and `TimeEntryState.cs` -- emit/apply resolved scopes and preserve legacy duplicate no-op semantics.
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` and `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs` -- keep current and superseded scopes aligned with their snapshots.
- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- validate bundle ID/scope/catalog resolution; keep folds authoritative and apply XML/naming conventions.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs` -- enforce tenant ownership across issue/display/confirm/adjust while retaining Project/Work targets.
- `tests/Hexalith.Timesheets.{Contracts,Server,Projections,Integration}Tests` -- cover additive JSON, correction replay/idempotency, ledger lineage, loader integrity, Work success, four-route opacity, and zero I/O.
- `docs/launch-readiness.md`, its architecture fitness test, Story 3.2–3.6 append-only clarifications, and `sprint-status.yaml` -- record bounded timing, legacy inventory limits, resolved follow-ups, and coherent tracking.

## Tasks & Acceptance

**Execution:**
- [ ] Correction contracts/folds -- add optional server-derived scope, compatible legacy idempotency, projection parity, superseded-ledger scope, optional OpenAPI shape, and focused contract/aggregate/projection/export proofs.
- [ ] Loader and command service -- enforce matching tenant-owned evidence, complete read/page/fold guards, public-code conventions, and positive Project/Work behavior.
- [ ] Loader/service/HTTP tests -- cover every matrix row, correction-to-tenant success, legacy correction denial, raw response length, zero I/O/dispatch, privacy, and genuine cancellation.
- [ ] Readiness and historical artifacts -- pin structured waiver rows, distinguish reissuable vs legacy-unrepairable state, supersede stale observations/follow-ups, and synchronize Story 3.6 status.

**Acceptance Criteria:**
- Given a newly authorized correction or magic-link adjustment changes the Activity Type scope, when its event is serialized and replayed, then the optional snapshot scope is emitted from server-resolved authority and the aggregate, evidence projection, and current/superseded ledger rows preserve the corresponding scopes.
- Given old serialized correction events, when upgraded code replays or retries them, then they deserialize without the new member, retain prior scope, and remain deterministic/idempotent without catalog inference.
- Given a valid tenant-owned Project or Work link, when it is issued or used, then success remains available; any project-owned, mismatched, or unavailable evidence emits no protected work and returns the canonical denial.
- Given replay, index/read failure, wrong-tenant events, malformed pages, blank input, or rejected hashing, when loading runs, then it fails closed with documented zero-I/O early exits and genuine cancellation propagation.

## Implementation Notes

## Spec Change Log

- 2026-09-16: Review loop 1 reverted the implementation and paused for human resolution of the corrected-Time-Entry scope intent gap recorded below.
- 2026-09-16: Human resolution permits one optional, backward-compatible correction-snapshot scope field while forbidding history rewrites and migration tooling. Planning now covers aggregate/projection/ledger parity, legacy idempotency, and the KEEP requirements from the first implementation: tenant-only magic links, opaque denials, zero-I/O early exits, and complete loader integrity evidence.

## Review Triage Log

| ID | Verdict | Route | Evidence |
|---|---|---|---|
| BH-01 | medium | defer (moot pending loopback) | The loader accepts one matching inactive catalog item, `DescribeAsync` rejects it through `TryResolveDisplayLabel`, and `ConfirmAsync` has no catalog availability check. The resulting GET-denied/POST-accepted asymmetry is real but predates this diff. |
| BH-02 | medium | defer (moot pending loopback) | The magic-link path consumes the tenant catalog, while project restriction availability is computed only by `TenantActivityTypeCatalogProjection.ProjectForProject`; a restricted tenant type can therefore be selected for a Project. This policy gap predates the remediation. |
| BH-03 | false | reject | The replacement ownership predicate is unconditional on target kind. The Project-owned negative test exercises that exact predicate, while the Work positive test exercises Work authorization and target handling; there is no remaining Work-specific rejection branch. |
| BH-04 | low | reject | Work success is covered across issue, describe, confirm, and adjust by the command-service test, and the loader has no target-kind-specific branch. Adding a second concrete-loader/HTTP Work journey would be extra integration depth rather than evidence of a reachable defect. |
| BH-05 | low | reject | Loader tests independently cover mismatched IDs, project-scoped Time Entry state, and an unresolved capability Activity Type; the HTTP test proves the combined project-scoped bundle is opaque on all routes. Additional four-route permutations would add substantial test setup without exposing a different code path. |
| BH-06 | false | reject | The denial path logs only correlation ID, timestamp, and outcome category through the source-generated boundary message; the loader emits no logs. Capability, Time Entry, and Activity Type identifiers cannot reach that logger, and existing diagnostics tests inspect the same sink. |
| BH-07 | low | patch (moot pending loopback) | Launch readiness claims denial body-length equivalence, but the HTTP assertions compare normalized JSON after removing `traceId`; no assertion pins raw-body length. A direct raw-length assertion is warranted. |
| BH-08 | low | patch (moot pending loopback) | The new architecture test searches unrelated substrings across the entire readiness document, so it does not pin the two rows, their classifications, owners, risks, and revisit conditions as structured records. |
| BH-09 | low | patch (moot pending loopback) | The document declares only three atomic classifications but the new ownership row uses the established compound `implemented / waived` form. The vocabulary needs to define compound states or the row needs one canonical value. |
| BH-10 | low | patch (moot pending loopback) | The overall `CONCERNS` narrative omits the newly introduced ownership-inventory and timing waivers, leaving the summary inconsistent with its classification table. |
| BH-11 | medium | patch (moot pending loopback) | The original Story 3.6 artifact remains `in-progress` while the remediation spec and sprint tracker move to review, which gives tracking consumers contradictory lifecycle state. |
| BH-12 | medium | patch (moot pending loopback) | Story 3.6 still states that projection handlers do not exist, event matching uses suffixes without format checks, and catalog loading replays the domain. Current code disproves all three statements, so an append-only supersession notice is needed. |
| BH-13 | low | patch (moot pending loopback) | Story 3.4 retains an unchecked concrete-loader follow-up after the loader and live handlers landed. The historical row can remain, but the appended clarification should explicitly identify its resolution. |
| BH-14 | low | patch (moot pending loopback) | The tenant-only policy changes issuance and confirmation semantics owned by Stories 3.2 and 3.3, yet only Stories 3.4 and 3.6 receive append-only clarifications. The closed records remain historical, but a short supersession note would prevent continuity readers from applying the old broader scope. |
| BH-15 | maybe-false | defer (moot pending loopback) | The repository has no deployed EventStore access or release runbook, so it is not possible here to determine whether the inventory/revoke/reissue instruction is operationally reproducible. Deployed query access and the release evidence location would settle the claim. |
| BH-16 | low | reject | The verification totals omit the exact direct-executable filters, but the proposed fix edits this build's spec; review policy rejects findings whose fix is to edit the current spec. Repository test guidance still identifies the executable fallback. |
| VG-01 | medium | patch (moot pending loopback) | Pre-verified gap: `TenantOwnedActivityTypeRemainsValidForWorkTargetMagicLinkPaths` asserts only `WasDispatched`, which is also true for an authorized rejection; its later states are independently constructed. It must assert success, a non-null issue response, and the emitted Work-target issuance event. |
| EC-01 | medium | intent_gap | `TimeEntryCorrected` and `TimeEntryApprovedCorrected` can change `ActivityTypeId` after the correction service resolves a tenant scope, but their contracts carry no scope and `TimeEntryState.Apply` retains the old scope. The new tenant-scope guard therefore rejects a reachable corrected entry and makes reissue alone insufficient. Fixing this requires a human choice among additive event evolution, legacy-state remediation, or an explicit fail-closed waiver, conflicting with the frozen no-persistence/schema-migration boundary. |

## Design Notes

`TimeEntryCorrectionValues.ActivityTypeScope` is nullable and omitted when null: null alone means a legacy event never recorded scope; explicit `Unknown` remains invalid evidence. New writers populate previous and corrected snapshots from authoritative server state/resolution. Folds use `CorrectedValues.ActivityTypeScope ?? currentScope`; the superseded ledger uses `PreviousValues.ActivityTypeScope ?? currentScope`. Duplicate comparison treats legacy null as compatible only when every previously recorded value agrees. Existing `TimeEntryAdjustedThroughMagicLink.ActivityTypeScope` remains authoritative and its new nested snapshot mirrors it.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- restore succeeds with the SDK pinned by `global.json`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- succeeds with zero warnings and errors.
- Run each built xUnit v3 executable under `tests/*/bin/Debug/net10.0/` individually, including Works.Tests -- all non-skipped tests pass; only documented infrastructure/performance skips remain.
