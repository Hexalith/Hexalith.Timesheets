---
title: 'Bound external recording readback and report uncertainty'
type: 'feature'
created: '2026-11-28'
status: 'done'
baseline_commit: '973653e61c7394f200e6890a556f6170cc884ed0'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `TimeEntryRecordSubmissionService.SubmitAsync` calls `gateway.ReadWorkloadStreamAsync` directly with no readback deadline, overall timeout/retry loop, or outcome logging; `CommitTimeEntryRecord` hashes `SubmittedAtUtc`; and `POST /api/timesheets/external-contributions/` uses `DateTimeOffset.UtcNow` and short-circuits null identity before logging. Stalled reads can hang, operators cannot distinguish policy refusal from indeterminate commit, and retries after post-commit timeout and host restart can fail.

**Approach:** Reuse `MagicLinkSubmissionPollingOptions` and `MagicLinkDurableSubmissionService.ReadStoredEventsAsync` to bound submission, polling, and readback under one `TimeProvider` deadline and caller `CancellationToken`, log low-cardinality outcomes via `ExternalRecordingLog`, inject `TimeProvider` on `POST /`, and exclude `SubmittedAtUtc` from `CommitTimeEntryRecord`'s idempotency adapter.

## Boundaries & Constraints

**Always:**
- Persist only via `Hexalith.EventStore`; keep deterministic `messageId` (`CreateMessageId(["record", tenant, actor, entryId, sourceSystem, externalRequestId])`).
- Bound `SubmitAsync` by snapshotted `Timeout` + `TimeProvider` + caller `CancellationToken`; bound each read via `ReadStoredEventsAsync` (`min(deadline, ReadbackTimeout)` + `.WaitAsync`), retrying stale pages and retryable faults (`408/429/5xx`, `HttpRequestException`, `TimeoutException`, non-caller `OperationCanceledException`) every `PollInterval`.
- Rethrow caller `OperationCanceledException` when `cancellationToken.IsCancellationRequested` without logging `indeterminate-commit`.
- Log fixed outcomes (`policy-refusal`, `indeterminate-commit`, `committed`, `already-recorded`) + `Activity.Current?.TraceId` via `[LoggerMessage]` in `ExternalRecordingLog` (swallow logger exceptions); classify `CommandStatus.Rejected` as `policy-refusal` only when `status.IsRejected` and identity fields match.
- Verify `page.Metadata`, `MetadataVersion == 1`, `PayloadVersion is null or 1`, canonical unprotected `ProtectionMetadata`, strict JSON (`AllowDuplicateProperties = false`, `RespectRequiredConstructorParameters = true`), `CausationId == messageId`, `CorrelationId == messageId`, and non-default UTC `submitted.SubmittedAtUtc <= submittedAtUtc` (`Submitted` policy).
- Keep one C# type per `.cs` file and backward-compatible ctors on `MagicLinkDurableSubmissionService` and `TimeEntryRecordSubmissionService`.

**Never:**
- Alter public contracts, `ProcessRecordAsync` (`Reject()`), magic-link submission flows, fail-closed kernel defaults, `AppHost`, or `ServiceDefaults`.
- Place any `DiagnosticsPrivacyTests.SensitiveLoggingTerms` (`token`, `response`, `comment`, `secret`, `bearer`, `magic-link`, etc.) on any logging line, or `"EventStore"` in `ExternalContributionEndpoints.cs`.
- Disclose tokens, comments, identities, request IDs, or payloads in HTTP errors or telemetry.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output | Error Handling |
|----------|--------------|----------------|----------------|
| Transient read stall / stale / 503 | Read #1 stalls (`ReadbackTimeout` < `Timeout`), stale, or 503; read #2 valid | Cancels read #1; retries after `PollInterval` without resubmit; `202` + `committed` | N/A |
| Stalled read exhausts deadline | Read stalls past `Timeout` | Cancels read tokens; returns within bound + test tolerance; logs `indeterminate-commit` | `403` opaque |
| Post-commit timeout + restart + retry | Attempt 1 commits (`Draft`/`Submitted`), readback times out; host restarts (empty status store, later clock); Attempt 2 retries same command | Attempt 1: `403` + `indeterminate-commit`; Attempt 2: recovers 1 recording batch via EventStore idempotency, `202` | Attempt 1: `403`; Attempt 2: `202` |
| Caller cancellation | Caller cancels during submit, poll, or readback | Cancels transport token; no `indeterminate-commit` log | Rethrows `OperationCanceledException` |
| Policy / identity / input refusal | Null identity, blank/null fields, default/non-UTC timestamp, or verified `Rejected` (`IsRejected`) | Zero extra calls; logs `policy-refusal` + `TraceId` | `403` opaque |
| Uncertain status / receipt mismatch | Infra `Rejected` (`!IsRejected`), `PublishFailed`, `TimedOut`, corrupt/mismatched readback | No duplicate submit; logs `indeterminate-commit` + `TraceId` | `403` opaque |
| Preflight `Completed` duplicate | Status already `Completed` | Skips submit; bounded readback; `202` + `already-recorded` | N/A |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` (`L24-51`, `L415-427`, `L600-702`) -- Snapshot options (null-safe `OptionsValidationException`), optional `TimeProvider? clock = null` on public ctor, `internal` `PollingOptions`, `Clock`, `IsRetryableGatewayFailure`.
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalRecordingLog.cs` (new) -- `[LoggerMessage]` (`EventId = 1403/1404`), mirroring `ExternalConfirmationLog.cs`.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` (`L20-161`) -- Bounded deadline + `ReadStoredEventsAsync` retry loop, strict readback verification, `IsRejectedStatus`, `ExternalRecordingLog`.
- `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` (`L45-51`) -- Exclude `SubmittedAtUtc` from `CommitTimeEntryRecord` adapter.
- `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` (`L24-46`) -- Inject `TimeProvider`, pass `clock.GetUtcNow()`, route null identity into `SubmitAsync`.
- **Reuse:** `MagicLinkSubmissionPollingOptions`, `ReadStoredEventsAsync`, `HasCommittedStatus`, `HasStoredEventIdentity`, `HasSupportedEventMetadata`, `ComposedConfirmationHost` pattern.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` -- Snapshot `pollingOptions.Value`, add optional `TimeProvider? clock = null` to public ctor (replacing separate internal 4-arg ctor), expose `internal` `PollingOptions`, `Clock`, `IsRetryableGatewayFailure` -- Shares validated bounds and DI clock cleanly.
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/ExternalRecordingLog.cs` -- Add internal static partial logger with the 4 outcome constants, `TraceId`, swallowed exceptions, and zero forbidden terms -- Separates refusal from uncertainty privately.
- [x] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` -- Add optional `IOptions<MagicLinkSubmissionPollingOptions>?`, `ILogger<TimeEntryRecordSubmissionService>?`, `TimeProvider?` ctor params; enforce caller cancellation, non-blank fields, non-default UTC `submittedAtUtc`; bound submit+poll+readback under `_polling.Timeout`; add `ReadCommittedRecordPageAsync` retry loop via `ReadStoredEventsAsync`; verify metadata/protection/strict JSON/`CausationId`/`SubmittedAtUtc <= submittedAtUtc`; emit `ExternalRecordingLog` -- Bounds readback and reports outcomes.
- [x] `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` -- Exclude `SubmittedAtUtc` in `CommitTimeEntryRecord` adapter (`new { payload.Command, payload.Tenant, payload.Actor }`) -- Preserves canonical intent on post-restart retries.
- [x] `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` -- Inject `TimeProvider clock` on `POST /`, pass `clock.GetUtcNow()`, forward null identity into `SubmitAsync` -- Aligns clock and refusal logging with confirm route.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs` & `RuntimeRegistrationTests.cs` -- Update `Page(...)` `CausationId`, unit-test all I/O Matrix cases, logging privacy, `IsRejected` split, strict JSON/metadata, `SubmittedAtUtc <= submittedAtUtc`, and `TimeProvider` DI resolution -- Verifies server bounds and invariants.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionRecordingPersistenceTests.cs` & `MagicLinkConfirmationHttpBoundaryTests.Retry.cs` -- Add composed HTTP + workload auth + `/process` + real `AggregateActor` tests for delayed readback bound + tolerance, post-commit timeout -> host restart (empty status store, later clock) -> same-identity retry (`Draft`/`Submitted`), refusal vs indeterminate logs, zero sensitive disclosure, and adapter timestamp stability -- Proves Story 6.5 end-to-end.

**Acceptance Criteria:**
- Given a composed host with configured `MagicLinkSubmissionPollingOptions`, when `POST /api/timesheets/external-contributions/` hits a stalled gateway read or caller cancellation, then the delayed read returns opaque `403` within the bound plus documented tolerance (logging `indeterminate-commit`), and caller cancellation cancels the read and rethrows `OperationCanceledException`.
- Given a recording committed before a post-commit readback timeout, when the host restarts with an empty status store and later clock and retries the same command identity, then EventStore holds one recording batch (`Draft` or `Submitted`), the retry returns `202`, refusals and uncertain commits log distinct low-cardinality outcomes, and responses/telemetry disclose no sensitive data.

### Review Findings

Independent review of `973653e..a63200d` on 2026-11-28.

- [x] [Review][Decision] **High** Missing-status recovery could report `committed` after publication failed. The approved fix requires `Completed` before verifying the owner batch and refuses an explicit `PublishFailed` acknowledgement. EventStore restores missing status from the retained successful terminal result on exact replay. [Recording status gate](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L124) · [EventStore status repair](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.CommandRecovery.cs#L143)
- [x] [Review][Decision] **Medium** Changing the v1 keyed fingerprint can make a pre-upgrade replay conflict. The recorded pre-launch decision below retains `intent-v1` because there are no callers supplying an explicit key for this command. The declaration keeps `intent-v1` but omits `SubmittedAtUtc` from the canonical payload. EventStore compares the new digest with the persisted digest and can reject even an identical replay using a live pre-upgrade key. Decide how to preserve existing admissions while giving new ones a timestamp-independent fingerprint. [Intent adapter](../../src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs#L45) · [Admission digest comparison](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L284)

- [x] [Review][Patch] **High** Refuse a recording with a missing service date before gateway work. A composed HTTP probe returned `202` and persisted `0001-01-01` when the date was omitted. The evidence projection rejects the recorded event. Explicit JSON `null` returns an unlogged `400` and misses the required-field refusal path. [HTTP binding](../../src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs#L37) · [Recording gate](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L74) · [Projection validation](../../src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjectionHandler.cs#L777)
- [x] [Review][Patch] **Medium** A transient `Processing` observation previously disabled the owner-readback fallback. Under the approved publication rule, polling can still accept a later restored `Completed` status and verify the batch. Status that stays absent leaves the outcome indeterminate. [Status polling](../../src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs#L714) · [Recovery cases](../../tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs#L776)
- [x] [Review][Patch] **Medium** Refuse a full readback page that reports completion before the stream head. The validator permits `IsTruncated = false` when the page ends below `LatestSequence`. A focused probe confirmed that the Completed path accepts that contradictory page. [Page validation](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L250)
- [x] [Review][Patch] **Medium** Cover a stalled first owner-stream read during missing-status recovery. Existing deadline tests use a Completed receipt. An alternate read that used the entire operation deadline would pass those tests and prevent a second attempt. [Recovery read](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L169) · [Existing transient read tests](../../tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs#L493)
- [x] [Review][Patch] **Low** Check malformed page metadata on the missing-status path. The Completed-path corruption tests would pass if the alternate path skipped its independent page validation. [Recovery validation](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L171) · [Existing recovery fixture](../../tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs#L850)
- [x] [Review][Patch] **Low** Assert cancellation propagation on the server in the composed HTTP test. The client task and the completion filter can both signal after caller abort even if the endpoint swallows the cancellation exception. [Composed cancellation test](../../tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionRecordingPersistenceTests.cs#L153) · [Completion filter](../../tests/Hexalith.Timesheets.IntegrationTests/RecordingRequestCompletionFilter.cs#L11)
- [x] [Review][Patch] **Low** Cover the recording route's authenticated malformed-body `400`. The null and blank source tests exercise policy refusal. An authenticated request with an incompatible JSON shape should exercise the explicit `JsonException` branch and leave gateway and actor state untouched. [HTTP binding](../../src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs#L43) · [Existing input test](../../tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionRecordingPersistenceTests.cs#L169)

- [x] [Review][Defer] **Medium** Establish the supported restart retry window. Recovery requires a submit acknowledgement when status is missing. EventStore's existing owner idempotency expires after a configurable period of 24 hours by default. The composed test advances the request clock without qualifying actor expiry. [Owner expiry](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L927)
- [x] [Review][Defer] **Medium** Qualify the installed gateway and transport telemetry. The composed client synthesizes the status placeholder and filters it as absent. It captures the outcome logger, while the installed client sends the tenant and command message ID in the status URI. This extends the existing installed-transport deferral. [Composed gateway](../../tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionConfirmationPersistenceTests.cs#L1181) · [SDK status request](../../references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs#L150)
- [x] [Review][Defer] **Medium** Verify subject binding on historical Completed receipts. The existing receipt path checks command lineage and lacks the recovery path's stored `UserId` comparison. Add a uniform check after qualifying historical event envelope identities. [Completed receipt check](../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs#L307)
- [x] [Review][Defer] **Low** Bound individual status probes within the operation budget. A stalled preflight or confirmation status lookup can consume the operation deadline even when further probes would succeed. The shared helper previously used the remaining operation budget for status reads. [Status lookup](../../src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs#L668)

The Debug/source warning-as-error build, 264 focused Server tests, and all five composed recording tests passed. A broader Integration run was canceled during `ActorGroundedSharedSourceProducerCommitsComposedCapabilityAndSelectedReadback(extraOwners: 9998)`.

After the publication fix, both Debug/source warning-as-error builds passed. Verification passed 267 Timesheets Server tests, 20 composed recording and confirmation tests, 12 diagnostics privacy tests, and 85 EventStore command-recovery tests. The composed restart cases verify `202` after confirmed publication and an opaque `403` after failed publication. Both preserve one recording batch. The deployed recovery path requires the EventStore status-repair change.

#### Rejected

- **False** A later nonadjacent event can extend the recovered commit batch. EventStore persists each command batch as one contiguous sequence range. An unrelated successor establishes that the batch has ended, and a later event is outside that range.
- **False** The open-ended Completed read can mistake a sparse page for ordinary lag. The installed reader fails at the first missing event in its contiguous range. The recording still fails closed when its page does not match the receipt.
- **Low** The story frontmatter says `done` while sprint tracking says `review`. The review workflow synchronizes these fields when the findings are resolved.

#### Adversarial Code Review (`973653e..7069f3d`, 2026-11-28)

- [x] [Review][Defer] **High** Unpublished (`PublishFailed`) recordings return `202 Accepted` / `committed` after restart when status is lost (`blind-hunter+edge-case-hunter+acceptance-auditor`) [src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs:729] — deferred: Published EventStore 5.6.0 lacks exact-replay Completed status repair; remove `tryRecoverCommit` once `Hexalith.Builds` pins EventStore > 5.6.0.

- [x] [Review][Patch] **High** Refuse missing `ServiceDate == default` and undefined `BillableState` enum values before gateway work (`blind-hunter+edge-case-hunter+verification-gap+acceptance-auditor`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:74]
- [x] [Review][Patch] **Medium** Enforce `StrictBodyJsonOptions` (`AllowDuplicateProperties = false`) and consistent `Results.Problem(..., 400)` on `POST /` (`blind-hunter+edge-case-hunter`) [src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:37]
- [x] [Review][Patch] **Medium** Reject contradictory non-truncated pages (`IsTruncated == false`) ending before `expectedEnd` (`LatestSequence` / `ToSequence`) (`blind-hunter+edge-case-hunter+verification-gap+acceptance-auditor`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:296]
- [x] [Review][Patch] **Medium** Enforce monotonic `LatestSequence` across recovery pages (preventing a lagging replica from skipping lookahead verification) and cap `TryRecoverCommittedBatchAsync` with stream page/event/byte ceilings (`blind-hunter+edge-case-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:210]
- [x] [Review][Patch] **Medium** Move pre-submit `_commentIssuer.Issue` out of the commit `try` block so pre-gateway policy/authority exceptions are not mislogged as `indeterminate-commit` (`blind-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:110]
- [x] [Review][Patch] **Medium** Fix `"missing-status"` unit test (currently returns `Completed` instead of `null`), remove duplicate `"publish-failed-acknowledgement"` case, and add coverage for post-recovery status-precedence re-read and `MissingStream`/retryable faults in `RecoverCommittedBatchAsync` (`blind-hunter+edge-case-hunter+verification-gap`) [tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs:778]
- [x] [Review][Patch] **Medium** Add deterministic `PollingTestTimeProvider` unit tests for `ReadbackTimeout` stalls, retryable 503s, and lagging-stream (`null`) retries on the `CommittedEventSequence == null` recovery path (`blind-hunter+verification-gap+acceptance-auditor`) [tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs:776]
- [x] [Review][Patch] **Medium** Add recovery-path unit tests for malformed `StreamReadMetadata` and non-contiguous sequence gaps in `TryRecoverCommittedBatchAsync` (`blind-hunter+verification-gap+acceptance-auditor`) [tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs:776]
- [x] [Review][Patch] **Medium** Capture and assert server-side `OperationCanceledException` propagation in `RecordingRequestCompletionFilter` and `CallerCancellationCancelsComposedReadbackWithoutLoggingAnIndeterminateCommit` (`blind-hunter+verification-gap+acceptance-auditor`) [tests/Hexalith.Timesheets.IntegrationTests/RecordingRequestCompletionFilter.cs:11]
- [x] [Review][Patch] **Low** Resolve `ExternalContributionRequestContext.Resolve(httpContext)` before `body.Deserialize` on `POST /` so missing-identity callers always enter `SubmitAsync` and receive opaque `403` + `policy-refusal` (`acceptance-auditor`) [src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:34]
- [x] [Review][Patch] **Low** Add authenticated `400 BadRequest` integration tests covering JSON `null` and incompatible JSON shapes (`JsonException`) on `POST /api/timesheets/external-contributions/` (`verification-gap`) [tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionRecordingPersistenceTests.cs:226]
- [x] [Review][Patch] **Low** Write `EmitMissingStatusPlaceholder`'s domainless `Received` row before `ProcessCommandAsync` in `ComposedConfirmationHost` to match real gateway ordering (`blind-hunter`) [tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionConfirmationPersistenceTests.cs:1183]

- [x] [Review][Defer] **Medium** Verify stored envelope `UserId` subject binding on `Completed` receipt readback (`edge-case-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:353] — deferred: pre-existing on the `Completed`-sequence readback path; requires qualifying historical event-envelope identities first.
- [x] [Review][Defer] **Low** Bound individual status probes within the operation budget in `SubmitAndReadStatusWithPreflightAsync` (`edge-case-hunter`) [src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs:669] — deferred: pre-existing in shared `SubmitAndReadStatusWithPreflightAsync` (shared with confirmation).

##### Rejected (`973653e..7069f3d`, 2026-11-28)

- **False** Preflight and readback retry loops omit elapsed-deadline guards and delay clamping (`blind-hunter`) — Refuted: `deadlineToken` is linked to `new CancellationTokenSource(_polling.Timeout, _clock)` (`TimeEntryRecordSubmissionService.cs:96-97`, `MagicLinkDurableSubmissionService.cs:640-641`), cancelling both `GetWorkloadCommandStatusAsync(...).WaitAsync(deadlineToken)` and `Task.Delay(..., _clock, deadlineToken)` at the exact deadline; `SubmitAsync` catches the timeout `OperationCanceledException` and logs `indeterminate-commit`.
- **Low** `TryRecoverCommittedBatchAsync` definitive `false` polls until `Timeout` during missing-status recovery (`edge-case-hunter`) — Still fails closed (`403` / `indeterminate-commit`), unlikely in everyday use, and fixing adds tri-state delegate/branch complexity (also moot if Decision 1 removes `tryRecoverCommit`).
- **Low** Missing constructor null checks on `TimeEntryRecordSubmissionService`/`MagicLinkDurableSubmissionService` and DI clock assertion on `TimeEntryRecordSubmissionService` (`blind-hunter`) — Unlikely in everyday DI usage; `SnapshotPollingOptions(null)` throwing `OptionsValidationException` is intentional and tested; fixing adds constructor guards and internal test accessors for negligible everyday benefit.
- **Rejected (spec-edit)** Spec frontmatter `status: 'done'` and `[x]` review markers contradict `sprint-status.yaml` and open findings (`blind-hunter+acceptance-auditor`) — Triage rule rejects findings whose fix edits the spec under review (the underlying `PublishFailed` code contradiction is tracked in Decision 1 above, and Step 4 synchronizes story/sprint status on completion).
- **Rejected (decision)** Changing `CommitTimeEntryRecord` intent payload under unchanged `intent-v1` (`blind-hunter+edge-case-hunter+acceptance-auditor`) — Rejected by human decision: Timesheets is pre-launch and no caller sends an explicit `IdempotencyKey` for `CommitTimeEntryRecord`; keeping `intent-v1` as the initial launch schema.

#### Adversarial Code Review (`973653e..2204928`, 2026-11-28)

- [x] [Review][Patch] **Medium** Propagate tri-state `Task<bool?>` from `RecoverCommittedBatchAsync` / `tryRecoverCommit` so a definitive owner-stream mismatch terminates recovery polling instead of re-scanning up to 64 pages / 16 MiB until timeout (`blind-hunter+edge-case-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:183]
- [x] [Review][Patch] **Medium** Make `ExternalRecordingBody.DurationMinutes` (`int?`) and `BillableState` (`BillableState?`) nullable so explicit JSON `null` flows to `SubmitAsync` (`403` + `policy-refusal`) consistently with `ServiceDate` and omitted fields, and add integration tests (`blind-hunter+edge-case-hunter`) [src/Hexalith.Timesheets/Endpoints/ExternalRecordingBody.cs:23]
- [x] [Review][Patch] **Medium** Add authenticated HTTP integration tests covering bodyless `POST` (`400` opaque problem) and non-JSON `text/plain` (`415`) on `/api/timesheets/external-contributions/` — test uncovered `.Accepts<SubmitExternalTimeEntry>("application/json")` causing `ConsumesMatcherPolicy` to route `text/plain` to an unclassified synthetic 415 endpoint rejected with `401`; switched to `.WithMetadata(new AcceptsMetadata([], typeof(SubmitExternalTimeEntry), isOptional: false))` (`verification-gap+blind-hunter`) [src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:40]
- [x] [Review][Patch] **Low** Move the contiguous sequence check (`lastRead - request.FromSequence != page.Events.Count`) into `ValidatePage` so `ReadCommittedRecordPageAsync` fails fast on gapped non-truncated pages instead of spinning until timeout (`blind-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:320]

- [x] [Review][Defer] **High** Unpublished (`PublishFailed`) recordings return `202 Accepted` / `committed` after restart when status is lost (`acceptance-auditor+edge-case-hunter`) [src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs:729] — deferred: carried forward; published EventStore 5.6.0 lacks exact-replay `Completed` status repair; remove `tryRecoverCommit` once `Hexalith.Builds` pins EventStore > 5.6.0.
- [x] [Review][Defer] **Medium** Post-restart recovery passes `wasPresentAtPreflight: false` (`existingCommand: false`), rejecting retained v2-comment recordings across a v2-to-v3 upgrade (`blind-hunter+edge-case-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:165] — deferred: extends existing cross-version comment-publication compatibility deferral; flipping `existingCommand` unconditionally on missing-status recovery would weaken fresh-commit v3 validation.
- [x] [Review][Defer] **Medium** Primary `Completed`-sequence readback omits `stored.UserId` actor verification (`blind-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:335] — deferred: carried forward; pre-existing on the `Completed` path and requires qualifying historical gateway/actor `UserId` envelopes first.
- [x] [Review][Defer] **Medium** Unverified `StoredTimeEntryApproved` and `StoredTimeEntryApprovedCorrected` deserialization in `TimeEntryConfirmationSubmissionService` (`verification-gap+blind-hunter`) [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryConfirmationSubmissionService.cs:593] — deferred: introduced by Story 6.7 (`5e2cf95`) overlapping this file, not Story 6.5; track under Story 6.7 review.
- [x] [Review][Defer] **Medium** Unverified `CreateIntent` behavior for the four `CommitProjectTimeEntry*` idempotency adapters (`verification-gap+blind-hunter`) [src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs:74] — deferred: introduced by Story 6.7 (`5e2cf95`) overlapping this file, not Story 6.5; track under Story 6.7 review.
- [x] [Review][Defer] **Low** Status lookups in `SubmitAndReadStatusWithPreflightAsync` lack per-probe timeouts (`blind-hunter`) [src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs:669] — deferred: carried forward; pre-existing in shared helper used by confirmation and recording.
- [x] [Review][Defer] **Low** Catch-all around `SubmitWorkloadCommandAsync` swallows synchronous non-retryable rejections and serialization errors, polling empty status until timeout (`blind-hunter`) [src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs:692] — deferred: pre-existing in shared `SubmitAndReadStatusWithPreflightAsync` (before `973653e`); still fails closed (`403` / `indeterminate-commit`).
- [x] [Review][Defer] **Low** `TimesheetsRouteCatalogDeclarations.Routes`, `Operations`, and `ReceiverMethods` recompute schemas/collections on every access (`blind-hunter`) [src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs:50] — deferred: introduced by Stories 6.6/6.7 (`67eca81`/`5e2cf95`) overlapping this file; startup-only cost, track under Story 6.6/6.7 review.

##### Rejected (`973653e..2204928`, 2026-11-28)

- **False** Recovery scan exits on first unrelated event after batch (`blind-hunter`) — Refuted: EventStore's single-threaded `AggregateActor` commits each command's events atomically as one contiguous sequence range; an unrelated successor proves the batch has ended.
- **False / Spec-edit** Unconditional `SubmittedAtUtc <= intent.SubmittedAtUtc` weakens fresh commits and breaks backward clock skew (`blind-hunter`) — Refuted for fresh commits (post-restart retries have `wasPresentAtPreflight == false` and a later clock, so `==` would break AC2); relaxing `<=` would edit the frozen spec constraint at line 28.
- **False** Recovered batches log `committed` instead of `already-recorded` (`blind-hunter`) — Refuted: frozen spec I/O Matrix line 46 defines `already-recorded` for `Preflight Completed duplicate`, whereas restart recovery has no preflight status and `ExternalContributionRecordingPersistenceTests:129` asserts `committed`.
- **False** Unclamped preflight delay and `cancellationToken` check in polling catch (`blind-hunter`) — Refuted: `deadlineToken` cancels `Task.Delay` at the exact `polling.Timeout` in preflight, and polling lines 758–768 immediately exit on `remaining <= TimeSpan.Zero` or cancelled `deadlineToken` without an extra gateway call.
- **False** Uncaught `InvalidOperationException` and `BadHttpRequestException` in `ExternalContributionEndpoints` (`blind-hunter`) — Refuted: `HasJsonContentType()` at line 45 prevents `InvalidOperationException`, and ASP.NET Core handles `BadHttpRequestException` natively with its proper HTTP status (413/408/400).
- **False** Wall-clock `Stopwatch` flake risk in `RecordingReadbackTimeoutIsOpaqueAndRestartRecoversOneBatch` (`blind-hunter`) — Refuted: deterministic `PollingTestTimeProvider` unit tests already cover exact bounds in `TimeEntryRecordSubmissionServiceTests.cs`, and AC1 explicitly requires the composed HTTP timeout bound plus documented tolerance.
- **Low** Missing constructor null guards on `TimeEntryRecordSubmissionService` and `MagicLinkDurableSubmissionService` (`blind-hunter+edge-case-hunter`) — Rejected: unlikely in everyday DI usage and fixing requires adding constructor guards/complexity.

## Implementation Notes

- The EventStore source actor can restore a missing status or a matching domainless `Received` placeholder from a cached successful result. Since the restored status has no committed sequence, Timesheets verifies the exact actor-bound batch in ordered owner-stream pages. The published EventStore 5.6.0 compatibility path also permits exact owner-stream proof after a matching submission acknowledgement and two absent status reads. An observed `PublishFailed` status takes precedence. If that status has been lost, the compatibility path can acknowledge persisted evidence whose publication failed. The recorded deferral calls for removing that path after the catalog adopts a release with status repair.
- The external recording HTTP flow retries with the deterministic EventStore message identity. The declared adapter applies to explicitly keyed admission and ignores the server-generated timestamp. Cross-version replay of commands admitted under the previous timestamp-inclusive adapter remains unverified.
- The recording HTTP route checks trusted identity before reading the body. It validates JSON syntax before constructing domain values so an incomplete document receives the opaque bad-request response even when an earlier field would fail domain validation. Missing service dates, nonpositive durations, and invalid billable states produce policy refusals before a gateway call.
- Owner-stream recovery is limited to 64 pages, 5,000 events, and 16 MiB of event payloads per scan. Pages must be contiguous and advertise a nondecreasing head. Recording readback rejects continuation tokens and complete pages that end before the requested stream range.
- The composed timeout test allows 750 ms beyond the configured operation timeout for TestServer dispatch, authentication, and response serialization.
- Two composed tests that assert outcomes now use two-second fixture budgets. Confirmation recovery used 150 ms and failed-publication recording recovery used 500 ms. The composed timing test still measures its 700 ms operation timeout with the documented tolerance.

## Spec Change Log

## Review Triage Log

| Layer and finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind hunter, recovery starts at sequence one | High | Patch. The actor persists rejected events and Time Entry replay ignores them, so a valid recording can start at a later sequence. |
| Blind hunter, approval policy changes during restart | Medium | Defer. The pre-existing readback already requires the event count configured by the current policy. This story qualifies restart with the same policy and a later clock. |
| Blind hunter, comment-publication policy changes during restart | Medium | Defer. The pre-existing no-status submission path compares against the current publication rules. Recovery across a policy revision requires its own compatibility coverage. |
| Blind hunter, Completed status correlation | Medium | Patch. The shared status checker accepts a different correlation on an otherwise matching receipt. |
| Blind hunter, initial status lookup fails on transient faults | Medium | Patch. The first lookup previously ran outside the polling loop. Recording retries before deciding whether to submit. |
| Blind hunter, missing stream after Completed | Medium | Patch. EventStore distinguishes a missing stream from other HTTP 404 responses. A Completed receipt allows retrying that specific missing-stream reason within the original deadline. |
| Blind hunter, previous timestamp fingerprint on the HTTP route | False | Reject. The HTTP submission sends no explicit `IdempotencyKey`, so it uses actor message identity. The adapter change and its regression test apply independently to keyed admission. |
| Blind hunter, composed gateway exposes a domainless Received row | Medium | Patch. The real workload status controller returns HTTP 404 for that row, which the client converts to an absent status. |
| Blind hunter, telemetry from outbound HTTP | Medium | Defer. The pre-existing SDK status URL contains tenant and message identifiers and the transport instrumentation is owned by EventStore. The composed test verifies the recording outcome log and external HTTP response. |
| Blind hunter, token on an untruncated readback page | Low | Patch. The generic stream validator allows the contradictory metadata and the current endpoint never emits such a token. |
| Edge case hunter, durable rejection before recording | High | Patch. Rehydration leaves a rejected owner eligible for a later recording. Verify the original batch at its persisted location after restart. |
| Edge case hunter, Completed receipt accepts a different correlation | Medium | Patch. The gateway client binds message ID and tenant, so recording must require the expected correlation before using the receipt's range. |
| Edge case hunter, actor is checked only in missing-status recovery | Low | Defer. The existing Completed path binds the actor through the deterministic command identity and owner-verified command lineage. A universal envelope subject check needs historical compatibility evidence. |
| Edge case hunter, cancellation assertion races the endpoint | Medium | Patch. The transport can finish before the endpoint catches cancellation and emits outcomes. Wait for the server pipeline before inspecting logs. |
| Verification gap, missing-stream recovery retry | Medium | Patch. Existing retry tests supplied a Completed receipt and never delivered a missing-stream fault to alternate recovery. |
| Verification gap, one-sided lineage on the lookahead | Medium | Patch. Existing cases used two matching values or two unrelated values, leaving each individual guard unverified. |
| Verification gap, private exception and structured logging | Medium | Patch. Both prior capture sinks formatted the message and discarded the logger exception. Assert the recorded fields and empty exception. |
| Verification gap, domainless status fixture | Medium | Patch. Subject the composed fixture to the real workload status domain check and verify the persisted advisory separately. |
| Blind hunter, lost publication status | High | Carried deferred. The EventStore 5.6.0 acknowledgement supplies command identity without a terminal state. The restart fixture confirms the compatibility behavior recorded above. |
| Blind hunter, missing billable state or duration | Medium | Patch. Omitted fields decode to `Unknown` and zero and reach the gateway before the aggregate refuses them. |
| Blind hunter, malformed JSON response | Medium | Patch. The framework parses the body-bound parameter before the handler, bypassing the opaque problem response. |
| Blind hunter, direct recording receiver accepts invalid data | High | Defer. An authorized direct `CommitTimeEntryRecord` dispatch reaches the existing receiver without the external HTTP guard. The owner can emit a default date or undefined billable value that the projection refuses. |
| Blind hunter, continuation token on a truncated page | Medium | Patch. The installed owner-stream gateway emits no continuation token and requires exclusive sequence paging. The previous validator only refused tokens on complete pages. |
| Blind hunter, Project SDK query entity identity | High | Out of scope for Story 6.5. The Story 6.7 query guard refuses the aggregate ID populated by the gateway when the entity ID is omitted. |
| Blind hunter, Project gateway query authority configuration | High | Out of scope for Story 6.5. Story 6.7 requires delegated context, and the documented setup omits the gateway's required-query enrollment. |
| Blind hunter, Project mutation receipt correlation | Medium | Out of scope for Story 6.5. The Story 6.7 submission classifier checks the message identity without comparing the correlation. |
| Blind hunter, omitted Party authority fields | High | Out of scope for Story 6.5. The Story 6.7 fixture omits restriction and erasure evidence, which the reader treats as false. |
| Blind hunter, Project record and correction facts | High | Out of scope for Story 6.5. Story 6.7 can commit facts that the evidence projection refuses. |
| Edge case hunter, malformed body before identity resolution | Medium | Patch. The composed raw-body probe returned 400 for a workload caller without a trusted actor, before the recording policy could refuse it. |
| Edge case hunter, stalled status probe | Low | Carried deferred. A preflight status lookup can consume the remaining deadline, as recorded in the shared polling deferral. |
| Edge case hunter, incomplete Project ledger suffix | Medium | Out of scope for Story 6.5. The Story 6.7 ledger fold does not catch the suffix replay exception handled by the Time Entry evidence projection. |
| Edge case hunter, requirement for Completed before recovery | High | Carried deferred. The later EventStore 5.6.0 compatibility decision preserves recovery with a matching acknowledgement and verified owner events when status is absent. |
| Verification gap, contributor ledger pagination | Medium | Out of scope for Story 6.5. The Story 6.7 pagination test checks an approver and cursor replay without showing a contributor page containing only their own data. |
| Verification gap, raw JSON syntax tests | Medium | Patch. The composed probe reproduced both framework failures with a raw unterminated object. Cover a caller with a trusted actor and one without it. |

## Design Notes

- **Restart recovery:** The actor deduplicates the same message after restart. The implementation notes describe how readback proceeds when the previous status is unavailable. Excluding `SubmittedAtUtc` from the adapter and allowing `submitted.SubmittedAtUtc <= submittedAtUtc` preserves the original recording across clock advances and refuses future timestamps.
- **Scope boundary:** `ProcessRecordAsync` stays `Reject()` (pinned by existing tests); Story 6.4's unrelated confirmation/magic-link replay refactor remains separate.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` -- expected: exit 0
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` -- expected: exit 0, zero warnings
- Run all 6 built xUnit v3 executables under `tests/<Project>/bin/Debug/net11.0/<Project>` -- expected: exit 0, zero failures

The post-review Debug/source restore and warning-as-error build passed. Focused
post-review verification passed all 232 selected Server cases and all 19 composed
recording and confirmation cases. The composed recording cases verify a single
persisted Draft or Submitted batch after a real earlier rejection, readback
timeout, and host restart with a later clock. The catalog intent regression also
passed and verifies timestamp stability separately from actor deduplication.

The pre-edit Aspire baseline reached healthy Timesheets. EventStore exited with
code 1, and the AppHost stopped successfully. The composed tests provide local
HTTP and actor evidence. Installed gateway and deployed transport evidence remain
open.

All six default suites passed with 3,230 passing tests, five expected Integration
skips, and zero failures. Architecture passed 95, Contracts 109, Server 1,048,
Works 76, Projections 801, and Integration 1,101. The final focused Integration
checks passed 47 recording, confirmation, and finance cases plus four catalog
and projection coordination cases.

The review repair passed the Debug/source solution restore and warning-as-error
build, 131 focused recording service cases, and 39 composed recording and
confirmation cases. The focused runs exercised both recovery paths, verified
caller cancellation at the server, and confirmed that malformed JSON and policy
refusal responses contain no protected data. The compiled AppHost configuration
and resource model probes passed. In the pre-edit Aspire baseline, Timesheets
became healthy and EventStore exited with code 1. The exact AppHost stopped
successfully.

On the final Debug/source build, all six default suites passed with 3,304
passing tests, five expected Integration skips, and zero failures. Architecture
passed 95, Contracts 109, Server 1,078, Works 76, Projections 802, and
Integration 1,144.

After closing the 2026-11-28 (`973653e..2204928`) adversarial review patches,
the Debug/source warning-as-error build passed with 0 warnings and all six
default suites passed with 3,309 passing tests, five expected Integration skips,
and zero failures (Architecture 95, Contracts 109, Server 1,080, Works 76,
Projections 802, Integration 1,147).
