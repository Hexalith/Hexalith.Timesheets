---
title: 'Persist workload-authenticated external confirmation'
type: 'feature'
created: '2026-11-27'
status: 'done'
route: 'dispatch'
baseline_commit: 'cc09d6383290e0b717de0bc53cd4c19ef1360ae3'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `POST /api/timesheets/external-contributions/{timeEntryId}/confirm` passes `null` as `TimeEntryState` to `ExternalContributionCommandService.ConfirmAsync`, so workload confirmations always fail (`not-recorded`) and never persist `StoredTimeEntryConfirmed` via EventStore.

**Approach:** Enroll internal EventStore command `CommitTimeEntryConfirmation`, add a domain processor branch, and wire `TimeEntryConfirmationSubmissionService` to the HTTP route so confirmation loads the tenant-scoped owner from EventStore, verifies contributor/target/authority, commits once, and acknowledges only after bounded exact-event readback.

## Boundaries & Constraints

**Always:**
- Persist only via `Hexalith.EventStore` (`CommitTimeEntryConfirmation` enrolled in `TimesheetsRouteCatalogDeclarations`, processed by `MagicLinkEventStoreDomainProcessor`); keep `TimeEntry` pure (`Handle`/`Apply`).
- Resolve tenant/actor from trusted server claims via `HttpContextTimesheetsTrustedContextAccessor.ResolveActorId` (reject `workload:` subjects).
- Validate `ConfirmExternalTimeEntry` (including `state.TimeEntryId` and `state.Target`) before same-source `NoOp`; reject conflicting confirmation sources on already-confirmed entries.
- Leave zero events on any stream on refusal (fail-closed pre-submit checks + `DomainResult.NoOp()` in processor).
- Return `202 Accepted` only after verified readback of matching `StoredTimeEntryConfirmed` bound to the deterministic command identity, preserving approval/correction/lock state.
- Bound stream reads, polling, and readback by `MagicLinkSubmissionPollingOptions` + caller `CancellationToken`; log only low-cardinality outcomes (`policy-refusal`, `indeterminate-commit`, `committed`, `already-confirmed`) + `TraceId`.

**Never:**
- Alter public contracts, magic-link confirmation (`CommitMagicLinkUse`), fail-closed `DenyAll*`/`Unavailable*` kernel defaults, `InternalSurfaceGuard`, `AppHost`, or `ServiceDefaults`.
- Reference `"EventStore"` in `ExternalContributionEndpoints.cs` or terms from `DiagnosticsPrivacyTests.SensitiveLoggingTerms` in logs.
- Insert aggregate state directly in the Story 6.4 HTTP success test; record the entry via `POST /api/timesheets/external-contributions/`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output | Error Handling |
|----------|--------------|----------------|----------------|
| Record then confirm | HTTP-recorded entry (`Draft`/`Submitted`/`Approved`), matching tenant/actor/contributor/target | 1 `StoredTimeEntryConfirmed` committed & verified; `202 Accepted`; approval/lock/correction preserved | N/A |
| Duplicate pre/post restart | Same request + tenant + actor sent twice, including after host/actor restart (empty status store) | Re-authorizes, verifies stream event, `202 Accepted`; still 1 confirmation event | N/A |
| Lost submit ack | Gateway throws after actor commit | Status poll + readback verify commit; `202 Accepted`; 1 event | N/A |
| Wrong tenant or contributor | Mismatched tenant claim, `command.Contributor != state.Contributor`, or denied guard | 0 events written; logs `policy-refusal` | `403` opaque ProblemDetails |
| Unrecorded or conflicting source | Missing stream, non-external entry, or confirmed with different `Source` | 0 events written; logs `policy-refusal` | `403` opaque ProblemDetails |
| Route/body ID mismatch or null | `timeEntryId != command.TimeEntryId?.Value` | Rejected before service | `400` opaque ProblemDetails |
| Missing/human auth | Anonymous or non-workload JWT | Rejected at `EventStoreWorkload:any` | `401 Unauthorized` |
| Timeout / gateway fault | Delayed status/readback or gateway 5xx | Withholds acceptance; logs `indeterminate-commit`; retry recovers | `403` opaque ProblemDetails |
| Direct `/process` refusal | Bad origin/actor/tenant/aggregate, corrupt replay, or denied guard | `DomainResult.NoOp()` (0 events appended) | No stream write |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` (`L111-140`, `L974-1023`) -- Aggregate `Handle(ConfirmExternalTimeEntry)` & `ValidateExternalConfirmation`.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` -- Blueprint for workload submission/readback (`CreateMessageId`, `SubmitAndReadStatusWithPreflightAsync`, `HasCommittedStatus`).
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` (`L83-124`, `L269-352`) -- Keyed processor; keep 4- and 5-param constructors; see `CommitApprovedTimeExport` `DomainResult.NoOp()` pattern.
- `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` (`L18-46`) & `ServiceCollectionExtensions.cs` (`L97`) -- Command catalog + DI (`TimesheetsProjectionRouteCatalogDeclarations` reflects automatically).
- `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` (`L41-70`) -- Confirm route; opaque copy `"External contribution request was not accepted."`.
- **Reuse:** `StoredTimeEntryConfirmed` (already folded in `TimeEntryState`, projections, and `MagicLinkDurableSubmissionService.Deserialize<T>`), `ExternalContributionCommandService.ConfirmAsync`, `MagicLinkDurableSubmissionService.ReadStoredEventsAsync`, `MagicLinkHttpBoundaryFactory`, `FinanceReceiptActorGateway` / `CreateMagicLinkActor`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/Commands/CommitTimeEntryConfirmation.cs` -- Create internal intent record `(ConfirmExternalTimeEntry Command, TenantReference Tenant, PartyReference Actor, DateTimeOffset ConfirmedAtUtc)` -- Enrolled command payload.
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` -- Run `ValidateExternalConfirmation` before same-source `NoOp`, check `state.TimeEntryId` and `state.Target`, reject conflicting `ContributorConfirmationSource` -- Enforces confirm-once without bypass.
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/ExternalConfirmationLog.cs` & `TimeEntryConfirmationSubmissionService.cs` -- Implement low-cardinality `LoggerMessage` outcomes and scoped submission service (coarse guard -> bounded owner stream load/fold -> `ExternalContributionCommandService.ConfirmAsync` -> deterministic `messageId` -> verified stream recovery or bounded commit/readback) -- Durable workload confirmation with zero stream pollution on refusal.
- [x] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` -- Add `ProcessConfirmationAsync` for `CommitTimeEntryConfirmation` returning `DomainResult.NoOp()` on refusal/replay failure -- Authoritative actor verification without rejection-event pollution.
- [x] `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` & `ServiceCollectionExtensions.cs` -- Enroll `CommitTimeEntryConfirmation` (`commit-time-entry-confirmation`) and `TryAddScoped<TimeEntryConfirmationSubmissionService>` -- Wires catalog, adapter, and DI.
- [x] `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` -- Wire `/{timeEntryId}/confirm` to `TimeEntryConfirmationSubmissionService`, `TimeProvider`, and `ResolveActorId` -- Replaces `state: null` skeleton without mentioning `"EventStore"`.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/HttpRouteCatalogFixture.cs`, `MagicLinkConfirmationHttpBoundaryTests.RouteCatalog.cs`, `MagicLinkConfirmationHttpBoundaryTests.Retry.cs` -- Update counts (`9->10`, `5->6`), add `timesheets-commit-time-entry-confirmation`, add changed-intent check -- Keeps catalog tests synced.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryConfirmationSubmissionServiceTests.cs`, `ExternalContributionCommandServiceTests.cs`, `MagicLinkEventStoreDomainProcessorTests.cs` -- Unit-test I/O Matrix cases, log categories, processor `NoOp` refusals, and cancellation -- Verifies server/privacy invariants.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionConfirmationPersistenceTests.cs` -- Add composed HTTP + workload auth + `/process` receiver + real `AggregateActor` restart-recovery tests (record via HTTP, confirm, duplicate pre/post restart with cleared status store, approval preservation, wrong tenant/contributor zero-effect refusal, log privacy) -- Proves Story 6.4 end-to-end.

**Acceptance Criteria:**
- Given an entry recorded via `POST /api/timesheets/external-contributions/` under composed workload auth, when confirmed via `POST /api/timesheets/external-contributions/{timeEntryId}/confirm` through `/process` and EventStore readback, then one `StoredTimeEntryConfirmed` commits and survives host/actor restart with approval state preserved.
- Given a different tenant or contributor confirms that entry, when handled by HTTP or `/process`, then an opaque refusal is returned, zero events are appended to any stream, and logs disclose no sensitive data.

## Implementation Notes

## Spec Change Log

## Review Triage Log

| # | Layer | Location | Verdict | Route | Evidence |
|---|-------|----------|---------|-------|----------|
| 1 | verification-gap | `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:30` | medium | defer | Pre-verified: sibling `POST /api/timesheets/external-contributions/` still uses `FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier)` instead of `ResolveActorId`; pre-existing on untouched recording route (Story 6.5 scope). |
| 2 | verification-gap | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:165-172,222-238` | medium | patch | Pre-verified: no test exercises `wasPresentAtPreflight == true` with an earlier stored `ConfirmedAtUtc` and `outcome=already-confirmed`. |
| 3 | verification-gap | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:141-144,193`; `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs:398` | medium | patch | Pre-verified: no test exercises `HasAmbiguousMagicLinkTerminalHistory` in either the confirmation service or domain processor. |
| 4 | verification-gap | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:302-559` | medium | patch | Pre-verified: no test covers multi-page `IsTruncated` pagination or malformed/gapped/cross-tenant stream pages in `LoadOwnerStreamAsync`. |
| 5 | verification-gap | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:180-183` | low | patch | Pre-verified: no test covers `CommandStatus.Rejected` mapping to `outcome=policy-refusal`; direct test addition. |
| 6 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:114-151` | high | patch | Verified: coarse `_accessGuard.AuthorizeAsync` runs outside `try`/`catch` and `deadline`, letting transient guard exceptions escape as HTTP 500 without logging; resource `_externalContributionService.ConfirmAsync` passes unbounded `cancellationToken` instead of `deadline.Token`. |
| 7 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs`; `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` | low | reject | Verified fail-closed behavior matches the I/O Matrix (`indeterminate-commit` + retry recovers); `SubmitAndReadStatusWithPreflightAsync` preflight is pre-existing shared code and adding internal retry loops adds branching/complexity beyond a direct correction. |
| 8 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:34` | low | reject | Verified `MagicLinkDurableSubmissionService` public constructor uses `TimeProvider.System` (internal 4-arg constructor accepts `TimeProvider` for tests); zero production impact and propagating through DI would alter shared magic-link public constructors. |
| 9 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:185-197,617-631` | medium | patch | Verified: `IsCompletedNoOpStatus` omits `status.CommittedEventSequence is null`, and the refresh path maps `refreshedState.HasAmbiguousMagicLinkTerminalHistory` to `PolicyRefusal` instead of `IndeterminateCommit`. |
| 10 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:180-183` | medium | patch | Verified: `CommandStatus.Rejected` returns `PolicyRefusal` without validating `MessageId`, `StatusCode`, `TenantId`, `Domain`, and `AggregateId`. |
| 11 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:492-502,571-585` | medium | patch | Verified: `DeserializeOwnerEvent` accepts short names (`nameof(StoredTimeEntryConfirmed)` / `nameof(TimeEntryContributorConfirmed)`), while `HasVerifiedStoredConfirmation` checks only `FullName` and silently skips short-named confirmation events. |
| 12 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:217-227,597-615` | medium | patch | Verified: committed readback omits `page.Metadata` validation (`FromSequence`, `ToSequence`, `EventCount`, `LastSequenceReturned`, `LatestSequence`) and `streamEvent.UserId == context.Actor.PartyId`. |
| 13 | blind-hunter | `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs:381-421` | medium | patch | Verified: `ProcessConfirmationAsync` checks only `current.Target is null` (allowing uninitialized target to reach `_accessGuard`) and omits `confirmed.ConfirmedAtUtc == intent.ConfirmedAtUtc` (envelope `MessageId` formula is intentionally not enforced on TimeEntry processors like `ProcessRecordAsync`). |
| 14 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs:1031-1045`; `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs:201-213` | medium | patch | Verified: `ValidateExternalConfirmation` omits `state.ConfirmedContributor is not null && state.ConfirmedContributor != command.Contributor`, allowing a post-correction contributor change to hit same-source `NoOp`; `TimeEntryState.Apply` is pre-existing untouched code. |
| 15 | blind-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:36-39,302-559` | low | reject | Verified stream folding duplicates `EventStoreMagicLinkConfirmationCapabilityStateLoader` with hardcoded limits; spec explicitly bounds confirmation by `MagicLinkSubmissionPollingOptions` and extracting a shared loader touches magic-link infrastructure beyond a direct correction. |
| 16 | blind-hunter | `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:45,51` | medium | patch | Verified: `ConfirmExternalTimeEntry command` is non-nullable, so Minimal API parameter binding rejects a JSON `null` body with default 400 before the opaque `command?.TimeEntryId is null` guard executes (tenant claim extraction matches `HttpContextTimesheetsTrustedContextAccessor`; sibling `/` endpoint is deferred in #1). |
| 17 | blind-hunter | `tests/` | medium | patch | Verified: missing DI resolution/lifetime/null-options tests in `RuntimeRegistrationTests`, plus `Work` target, throwing logger, and gaps overlapping #2-#5. |
| 18 | edge-case-hunter | `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs:370-379` | high | patch | Verified: `EventPayloadEvolutionException` inherits from `Exception` (not `InvalidOperationException`), and `TimeEntryState.Apply` can throw `ArgumentNullException` wrapped in `TargetInvocationException`; both escape `ProcessConfirmationAsync` instead of returning `DomainResult.NoOp()`. |
| 19 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:114-123` | high | patch | Verified (shared root cause with #6): coarse `_accessGuard.AuthorizeAsync` is outside `try`/`catch` and `deadline`. |
| 20 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:146-151` | medium | patch | Verified (shared root cause with #6): `_externalContributionService.ConfirmAsync` passes `cancellationToken` instead of `deadline.Token`. |
| 21 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:185-197` | medium | patch | Verified: `IsCompletedNoOpStatus` refresh accepts `HasVerifiedStoredConfirmation` without re-validating and re-authorizing `refreshedState` via `_externalContributionService.ConfirmAsync`. |
| 22 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:217-232` | medium | patch | Verified: two concurrent identical requests both observe `wasPresentAtPreflight == false` with different `clock.GetUtcNow()` values; the second fails `confirmed.ConfirmedAtUtc == confirmedAtUtc` and returns 403 `indeterminate-commit`. |
| 23 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:571-585` | medium | patch | Verified (shared root cause with #11): `HasVerifiedStoredConfirmation` ignores short-named confirmation event types accepted by `DeserializeOwnerEvent`. |
| 24 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs:1036-1045` | medium | patch | Verified (shared root cause with #14): `ValidateExternalConfirmation` does not check `state.ConfirmedContributor` after a correction changes `state.Contributor`. |
| 25 | edge-case-hunter | `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:68-74` | low | patch | Verified: `pollingOptions.Value.IsValid()` throws `NullReferenceException` instead of `OptionsValidationException` when `pollingOptions.Value` is `null`; trivial 1-line guard. |
| 26 | edge-case-hunter (claim) | `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs:370-379` | high | patch | Verified (shared root cause with #18): corrupt replay throwing `EventPayloadEvolutionException` or `TargetInvocationException` wrapping `ArgumentException`/`NullReferenceException` escapes `/process` instead of returning `DomainResult.NoOp()`. |

## Design Notes

- **Zero stream pollution:** `AggregateActor` appends `MagicLinkCommitRejected` on `DomainResult.Rejection`. Matching `CommitApprovedTimeExport`, `ProcessConfirmationAsync` returns `DomainResult.NoOp()` on refusal/replay failure and pre-submit checks fail closed.
- **Restart recovery:** `messageId = CreateMessageId(JsonSerializer.Serialize(["confirm", tenantId, actorPartyId, timeEntryId, contributorPartyId, sourceSystem, externalRequestId]))`. When transient command status is gone after restart, `TimeEntryConfirmationSubmissionService` re-authorizes and verifies the owner stream's `StoredTimeEntryConfirmed` (`CorrelationId == messageId`, `CausationId == messageId`, matching fields) before returning `202`.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` -- expected: exit 0
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` -- expected: exit 0, zero warnings
- Run all 6 built xUnit v3 executables under `tests/<Project>/bin/Debug/net11.0/<Project>` -- expected: exit 0, zero failures
