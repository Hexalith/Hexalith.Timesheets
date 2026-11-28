---
title: 'Bound external recording readback and report uncertainty'
type: 'feature'
created: '2026-11-28'
status: 'ready-for-dev'
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
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs` -- Snapshot `pollingOptions.Value`, add optional `TimeProvider? clock = null` to public ctor (replacing separate internal 4-arg ctor), expose `internal` `PollingOptions`, `Clock`, `IsRetryableGatewayFailure` -- Shares validated bounds and DI clock cleanly.
- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/ExternalRecordingLog.cs` -- Add internal static partial logger with the 4 outcome constants, `TraceId`, swallowed exceptions, and zero forbidden terms -- Separates refusal from uncertainty privately.
- [ ] `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` -- Add optional `IOptions<MagicLinkSubmissionPollingOptions>?`, `ILogger<TimeEntryRecordSubmissionService>?`, `TimeProvider?` ctor params; enforce caller cancellation, non-blank fields, non-default UTC `submittedAtUtc`; bound submit+poll+readback under `_polling.Timeout`; add `ReadCommittedRecordPageAsync` retry loop via `ReadStoredEventsAsync`; verify metadata/protection/strict JSON/`CausationId`/`SubmittedAtUtc <= submittedAtUtc`; emit `ExternalRecordingLog` -- Bounds readback and reports outcomes.
- [ ] `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` -- Exclude `SubmittedAtUtc` in `CommitTimeEntryRecord` adapter (`new { payload.Command, payload.Tenant, payload.Actor }`) -- Preserves canonical intent on post-restart retries.
- [ ] `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs` -- Inject `TimeProvider clock` on `POST /`, pass `clock.GetUtcNow()`, forward null identity into `SubmitAsync` -- Aligns clock and refusal logging with confirm route.
- [ ] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryRecordSubmissionServiceTests.cs` & `RuntimeRegistrationTests.cs` -- Update `Page(...)` `CausationId`, unit-test all I/O Matrix cases, logging privacy, `IsRejected` split, strict JSON/metadata, `SubmittedAtUtc <= submittedAtUtc`, and `TimeProvider` DI resolution -- Verifies server bounds and invariants.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionRecordingPersistenceTests.cs` & `MagicLinkConfirmationHttpBoundaryTests.Retry.cs` -- Add composed HTTP + workload auth + `/process` + real `AggregateActor` tests for delayed readback bound + tolerance, post-commit timeout -> host restart (empty status store, later clock) -> same-identity retry (`Draft`/`Submitted`), refusal vs indeterminate logs, zero sensitive disclosure, and adapter timestamp stability -- Proves Story 6.5 end-to-end.

**Acceptance Criteria:**
- Given a composed host with configured `MagicLinkSubmissionPollingOptions`, when `POST /api/timesheets/external-contributions/` hits a stalled gateway read or caller cancellation, then the delayed read returns opaque `403` within the bound plus documented tolerance (logging `indeterminate-commit`), and caller cancellation cancels the read and rethrows `OperationCanceledException`.
- Given a recording committed before a post-commit readback timeout, when the host restarts with an empty status store and later clock and retries the same command identity, then EventStore holds one recording batch (`Draft` or `Submitted`), the retry returns `202`, refusals and uncertain commits log distinct low-cardinality outcomes, and responses/telemetry disclose no sensitive data.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

- **Restart recovery:** Empty post-restart status store yields `WasPresentAtPreflight == false`, while `AggregateActor`'s durable `IdempotencyRecord` repairs `Completed` without duplicating events. Excluding `SubmittedAtUtc` from the adapter and allowing `submitted.SubmittedAtUtc <= submittedAtUtc` lets retries across clock advances pass while rejecting future timestamps.
- **Scope boundary:** `ProcessRecordAsync` stays `Reject()` (pinned by existing tests); Story 6.4's unrelated confirmation/magic-link replay refactor remains separate.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` -- expected: exit 0
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` -- expected: exit 0, zero warnings
- Run all 6 built xUnit v3 executables under `tests/<Project>/bin/Debug/net11.0/<Project>` -- expected: exit 0, zero failures
