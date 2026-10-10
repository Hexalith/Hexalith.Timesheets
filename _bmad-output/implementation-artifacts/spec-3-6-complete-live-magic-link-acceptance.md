---
title: 'Persist Time Entry recording for Story 3.6 magic-link use'
type: 'feature'
created: '2026-10-10'
status: 'done'
baseline_commit: edb80bbf8812d0c8aeab401765b01f992a1ae060
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's local magic-link use path requires a recorded Time Entry in its EventStore owner stream, but production has no writer for `TimeEntryRecorded`. The external contribution route supplies an unavailable Activity Type catalog and never submits its pure service result to EventStore, so a subsequent valid magic link still fails closed.

**Approach:** Make the existing external submission route commit recording through the keyed Timesheets EventStore processor, verify the stored batch before acknowledging it, and exercise recording followed by magic-link use against the same actor state. This is the next repository-owned Story 3.6 increment; live Dapr acceptance and release status remain open.

**Scope decision (2026-10-10):** The user chose the recording increment. Keep Story 3.6 `in-progress` and release `FAIL` after this work; live Dapr, trusted binding issuance, read-model delivery, and deployed-history gates require later evidence.

## Boundaries & Constraints

**Always:** Use Hexalith.EventStore as the only domain write path; reconstruct owner state from events; recheck server-side tenant, actor, reference, policy, and Fresh Activity Type authority at the commit decision. Keep one keyed `timesheets` processor. Preserve idempotency, approval-state policy, and the public route's safe failure shape. Return `202` only after a timely `Completed` status and matching stored-event readback.

**Never:** Trust request claims as authority, write a projection or Dapr state directly, duplicate a recording on retry, add a second keyed processor, change AppHost topology, or claim deployed acceptance from an in-process actor fixture. Do not change `ExistingTimeEntry` capability semantics in this increment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Authorized recording | New scoped entry, validated references, Fresh catalog | Owner stream stores one `TimeEntryRecorded` and any policy-required submission event; HTTP acknowledges only a verified commit | Deny if receipt differs |
| Retry or concurrent request | Same external request/entry or actor recreation | One logical recording; subsequent valid magic-link confirm or adjust sees it | No duplicate or partial state |
| Untrusted or stale | Missing authority, wrong tenant/actor, invalid references, non-Fresh catalog | No accepted recording | Safe denial; no protected payload in logs |
| Uncertain gateway result | Timeout or failed status/readback after submit | Never claim success without a matching stored batch | Retry must not duplicate a possible commit |
| Conflicting existing entry | Same owner ID with different facts or source | Existing state remains authoritative | Reject without append |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` — existing POST route passes null state and an unavailable catalog; it has no persistence path and fails closed in the default host.
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandService.cs`, `TimeEntryCommandService.cs` — reuse record/submission policy and authorization decisions; keep aggregate decisions pure.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — existing keyed owner processor rejects magic-link use unless `TimeEntryState.IsRecorded`; compose recording without another keyed registration.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — keyed processor and gateway registrations.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` — reuse its bounded Completed/status/readback pattern without making the index authoritative.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` — replace or extend the fixture-only recording seed with the production processor and assert persisted actor state after recreation.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — preserve valid GET and opaque denial behavior; prove record-then-use through the mapped route.
- `docs/launch-readiness.md`, `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md` — report the completed prerequisite without closing live acceptance.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/Commands/CommitTimeEntryRecord.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — add an internal recording intent and branch that revalidates the actor's current owner state and emits EventStore events only after trusted authorization and Fresh catalog checks.
- [x] `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — compose recording with the existing keyed `timesheets` processor and keep fail-closed defaults.
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` and `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` — load required authority, submit to EventStore, verify status and exact stored batch, and return safe failure on every uncertain outcome.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` — cover the matrix, stored end state, restart, concurrency, and receipt mismatch.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — prove record-then-use over HTTP and preserve no-disclosure responses.
- [x] `docs/launch-readiness.md`, `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, and `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — record verified results and remaining deployment gates accurately.

**Acceptance Criteria:**
- Given an authorized external submission with a Fresh catalog, when the route acknowledges it, then the EventStore Time Entry owner stream contains the exact recorded facts and policy-required submission evidence.
- Given recording was committed, when the actor is recreated and a valid scoped magic link is used, then confirm or adjust can commit once against the recorded entry; replay cannot commit a second effect.
- Given an unauthorized, stale, or conflicting submission, when the route responds, then the owner stream contains no new accepted recording and no protected detail is disclosed.
- Given an uncertain gateway outcome, when the route responds or the caller retries, then it never claims an unverified success, discloses protected detail, or creates a second recording.

## Implementation Notes

Recording uses the existing keyed `timesheets` actor processor. The processor rehydrates the owner stream and rechecks workload origin, verified actor, tenant, entry identity, Fresh Activity Type catalog, server-side access and reference validation, and optional submission policy before emitting one atomic event batch. The route sends a stable command identity derived from tenant, trusted actor, entry, and collision-safe external source fields, then verifies a timely `Completed` status and the stored facts before returning `202`. For Submitted policy, a new command requires the exact audit instant; an already committed retry uses its stored UTC instant. Stored submission wrappers are folded and normalized through the existing read-model paths.

The in-process HTTP journey uses an authenticated workload fixture with a substituted access guard, gateway, and read-model store. It does not establish production external caller authentication or deployed EventStore persistence. The default kernel remains fail-closed.

## Spec Change Log

- 2026-10-10: Implemented the recording increment, verified six individual Timesheets lanes, and retained Story 3.6 `in-progress` and release `FAIL`.
- 2026-10-10: Applied review patches for retry identity, retained rejection/terminal handling, exact receipts, verified actor evidence, and wrapped submission folds. The post-patch full suite passed; a separate focused HTTP run hit the already tracked numeric-substring privacy assertion false positive.

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| Blind 1: actor omitted from retry identity | medium / patch | `SubmitAndReadStatusAsync` returns an earlier Completed status before dispatch; another actor with the same source and facts can receive 202. Bind the stable identity to the trusted actor. |
| Blind 2: definitive rejection pins retry | false / reject | A repeated external request ID is the same idempotency context and must retain its definitive outcome. A corrected new attempt uses a new external request ID; an uncertain result retains the old one. |
| Blind 3: retained rejection blocks Submitted policy | medium / patch | Rehydrating a retained rejection produces a non-null unrecorded state, and `ExternalContributionCommandService` passes it to submission validation instead of applying the new record. |
| Blind 4: proposed-link terminal blocks recording | medium / patch | A proposed-entry revoke or expiry creates a terminal marker before recording; `ProcessRecordAsync` rejects any such marker although `TimeEntry.Handle` only rejects an already recorded entry. |
| Blind 5: project Activity Type unavailable | false / reject | Story 3.6's approved recording increment serves valid magic-link use, whose authoritative catalog and capability rules accept tenant Activity Types only. The existing general command contract supports project Activity Types, but project-scoped catalog delivery is a separate path and was already unavailable before this change. |
| Blind 6: receipt accepts wrong Activity Type scope | medium / patch | The processor can record only a tenant-scoped Activity Type through its current Fresh catalog, but readback checks only `!= Unknown`; a tampered Project scope could be acknowledged. Require Tenant scope. |
| Blind 7: receipt omits submitted instant | medium / patch | The stored `TimeEntrySubmitted.SubmittedAtUtc` is checked only for a UTC offset, so a different UTC instant can pass. |
| Blind 8: receipt omits command identity | medium / patch | The readback validates sequence and facts but does not bind the events to the submitted command. EventStore gives each event its own MessageId, so the correct link is each event's CorrelationId, which the gateway sets to the command message ID. |
| Blind 9: newline-delimited identity collision | medium / patch | `ExternalContributionSource` permits newlines; distinct source field pairs can create the same hash input. |
| Blind 10: wrapped submission projection untested | medium / patch | Projection tests use raw submission events and do not exercise the newly added `StoredTimeEntrySubmitted` normalizer arm. |
| Edge 1: newline identity collision | medium / patch | Confirmed by `ExternalContributionSource` accepting newline characters; same root cause as Blind 9. |
| Edge 2: wrong scope receipt | medium / patch | Confirmed by the `!= Unknown` check; same root cause as Blind 6. |
| Edge 3: submitted instant mismatch | medium / patch | Confirmed by the offset-only check; same root cause as Blind 7. |
| Verification 1: actor binding test gap | medium / patch | Recording actor tests provide matching gateway actor metadata; none sends missing or mismatched metadata through `ProcessRecordAsync`. |
| Verification 2: wrapped submission loader test gap | medium / patch | Loader tests supply raw `TimeEntrySubmitted`; none reloads `StoredTimeEntrySubmitted` through the new wrapper arm. |
| Verification 3: wrapped submission projection test gap | medium / patch | Projection tests supply raw `TimeEntrySubmitted`; none exercises the new normalizer arm; same root cause as Blind 10. |
| Verification other: submitted instant mismatch | medium / patch | Confirmed by the offset-only readback check; same root cause as Blind 7. |

All accepted patches are applied and covered by the post-patch full suite. Blind 2 and Blind 5 are rejected for the reasons above. No new deferred work was added; the focused privacy-test numeric-substring false positive was already in the deferred-work ledger.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore succeeds with the SDK selected by `global.json` (`10.0.401`).
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero diagnostics.
- Ran all six built xUnit v3 test executables individually after review patches: Architecture 56/56, Contracts 92/92, Server 523/523, Projections 174/174, Integration 663 passed/four skipped, Works 76/76. Total: 1584 passed, four intentional skips, zero failed. Actor tests assert stored events after recreation; HTTP tests assert recorded facts and optional submission evidence. A separate focused HTTP class rerun passed 567/568; its one failure was the already tracked `"60"` substring false positive in timestamp diagnostics, not a protected-data disclosure.
