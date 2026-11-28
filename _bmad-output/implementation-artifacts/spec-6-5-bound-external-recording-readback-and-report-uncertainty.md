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

## Implementation Notes

- The current EventStore actor clears its terminal status advisory after a successful write. A duplicate returns the cached actor result when the status store is later empty. The gateway may create a `Received` row with no domain or commit metadata, which the workload status endpoint reports as absent. After a matching workload submit acknowledgement, recording locates the exact actor-bound batch in ordered owner-stream pages and checks status again. Tests cover a retained rejection before the recording, a later event with matching lineage, and an observed domain status that takes precedence over recovery.
- The external recording HTTP flow retries with the deterministic EventStore message identity. The declared adapter applies to explicitly keyed admission and ignores the server-generated timestamp. Cross-version replay of commands admitted under the previous timestamp-inclusive adapter remains unverified.
- The composed timeout test allows 750 ms beyond the configured operation timeout for TestServer dispatch, authentication, and response serialization.

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
