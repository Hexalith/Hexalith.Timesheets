---
title: 'Persist Time Entry recording for Story 3.6 magic-link use'
type: 'feature'
created: '2026-10-10'
status: 'ready-for-dev'
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
- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/Commands/CommitTimeEntryRecord.cs` and `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` — add an internal recording intent and branch that revalidates the actor's current owner state and emits EventStore events only after trusted authorization and Fresh catalog checks.
- [ ] `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — compose recording with the existing keyed `timesheets` processor and keep fail-closed defaults.
- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` and `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` — load required authority, submit to EventStore, verify status and exact stored batch, and return safe failure on every uncertain outcome.
- [ ] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` — cover the matrix, stored end state, restart, concurrency, and receipt mismatch.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — prove record-then-use over HTTP and preserve no-disclosure responses.
- [ ] `docs/launch-readiness.md`, `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, and `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — record verified results and remaining deployment gates accurately.

**Acceptance Criteria:**
- Given an authorized external submission with a Fresh catalog, when the route acknowledges it, then the EventStore Time Entry owner stream contains the exact recorded facts and policy-required submission evidence.
- Given recording was committed, when the actor is recreated and a valid scoped magic link is used, then confirm or adjust can commit once against the recorded entry; replay cannot commit a second effect.
- Given an unauthorized, stale, or conflicting submission, when the route responds, then the owner stream contains no new accepted recording and no protected detail is disclosed.
- Given an uncertain gateway outcome, when the route responds or the caller retries, then it never claims an unverified success, discloses protected detail, or creates a second recording.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore succeeds with the SDK selected by `global.json` (`10.0.401`).
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero diagnostics.
- Run each affected test project under `tests/` individually; use the built xUnit v3 executable if VSTest sockets fail — all affected lanes pass and actor tests assert stored events.
