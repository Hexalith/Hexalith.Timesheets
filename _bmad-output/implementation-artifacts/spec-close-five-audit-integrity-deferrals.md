---
title: 'Close five deferred magic-link audit-integrity gaps'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: b53227b006069c2c380a81ab867ed2e7ed29dcc6
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Accepted magic-link receipts and replayed Time Entry evidence must preserve the actual preceding audit history. Close exactly five deferred concerns together: receipt command attribution; complete adjustment prior-value verification; omitted-scope legacy adjustment delivery; approved-correction attribution; rejected-correction attribution. Verify, commit, and push the completed changes to main.

## Boundaries & Constraints

Always preserve EventStore persistence, pure aggregate folds, opaque public denials, server-trusted pre-submit state, cancellation, bounded existing polling, and valid canonical redelivery. Compare correction attribution with the preceding approval/rejection. For receipts created by current submission paths, require both correlation and causation to equal the submitted command identity. Capture adjustment history before submission rather than trusting stored receipt claims.

Never rewrite events, migrate deployed state, change approval/confirmation product policy, expose protected audit data, change topology/dependencies/sibling checkouts, or claim deployed acceptance. Preserve legacy omitted nested scopes through deterministic inheritance without current-catalog inference. Keep the original audit snapshots while normalizing comparisons and effective folded state.

## I/O & Edge-Case Matrix

| Input/state | Expected behavior |
|-------------|-------------------|
| Valid issue, revoke, expire, confirm or adjust receipt | Preserve successful status and exact stored-batch verification |
| Missing/foreign correlation or causation on any receipt event | Refuse success through existing opaque behavior |
| Adjustment receipt has altered prior date, duration, billable state or comment | Refuse success against the captured trusted prior snapshot |
| Adjustment receipt changes any other prior-value field | Verify the complete snapshot, including identity, scope, category and AI metrics |
| Legacy adjustment omits nested previous/adjusted scope | Full, suffix and streamed delivery agree with direct folds; later events remain valid |
| Explicit contradictory nested scope | Refuse before publishing invalid evidence |
| Approved correction names another approval decision or scope | Authoritative/direct replay refuses before mutation; loader denies opaquely |
| Rejected correction names another rejection decision or reason | Same refusal; matching lineage remains valid |

</frozen-after-approval>

## Code Map

Production paths are relative to `src/`; test filenames below resolve in the matching `tests/Hexalith.Timesheets.{Server.Tests,Projections.Tests,IntegrationTests}/` directory.

- `Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs`: correlation already checked; causation and trusted prior values missing. Reuse receipt/retry flow.
- `Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`: POST handlers already hold trusted `state.TimeEntryState`; carry its snapshot into submission.
- `Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`: validate correction attribution before mutation.
- `Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`: retain shared full/incremental core; add attribution fences.
- Adjacent `TimeEntryEvidenceProjectionHandler.cs`: correction attribution already fenced; normalize legacy adjustment comparisons/digests in suffix, full and streamed validation.

## Tasks & Acceptance

- [x] State/evidence files above: align correction attribution with existing persisted-handler checks.
- [x] Handler above: normalize omitted previous scope from preceding state and adjusted scope from the top-level event; keep subsequent digests consistent.
- [x] Submission/endpoints above: capture complete trusted prior values before submission; require them for adjustment success and verify causation. Preserve compatible call shapes where practical.
- [x] Server `TimeEntryStateTests.cs`, `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`, `MagicLinkDurableSubmissionServiceTests.cs`: matrix refusal, unchanged state, matching positives and opaque loader cases.
- [x] Projection `TimeEntryEvidenceProjectionTests.cs`, `TimeEntryEvidenceProjectionHandlerTests.cs`: full/suffix/streamed legacy equivalence, repeated adjustment, contradictions, attribution and unchanged persisted contents/ETags.
- [x] Integration `MagicLinkPersistedSubmissionTests.cs`: tamper every metadata/prior-value field across receipt actions and both use-event positions; retain valid persistence/response-loss behavior. Adjust adjacent `MagicLinkConfirmationHttpBoundaryTests.cs` fixtures if affected.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: add dated resolution notes to exactly five selected entries after verification; preserve evidence.

Acceptance:

- Given contradictory audit history, when verified or replayed, then it cannot authorize success or publish contradictory evidence.
- Given valid current or supported legacy history, when delivered through each supported path, then effective state and evidence agree.
- Given refused replay/delivery, when inspected afterward, then preceding state and selected storage remain intact.

## Implementation Notes

Investigation baseline: clean main at the full revision in frontmatter. Ledger selections are the summaries above at planning-time lines 595, 599, 870, 874 and 878. Correlation is already checked; the attribution closure must add causation checks and regression coverage rather than duplicate existing behavior. No unresolved intent gaps or migration/data mutation is needed. Five production files plus tests/tracking; commit/push authorized. Status remains in-progress after approval until review and final verification finish.


Implementation update (2026-10-22):

- `TimeEntryState` and the shared full/incremental evidence core now compare approved source decision/scope and rejected decision/reason with the preceding decision before mutation or publication.
- Persisted evidence full, suffix, and streamed validation inherit omitted adjustment previous scope from the preceding effective values and adjusted scope from the event's top-level scope. Effective snapshot digests carry that normalized scope into subsequent adjustments and corrections; audit snapshots remain unchanged.
- The HTTP adjustment handler captures every prior snapshot field before dispatch. The submission service carries this server-trusted snapshot separately from stored receipts and requires exact prior-value equality for adjustment success. Original public overloads remain available; adjustment success through either overload requires a supplied trusted snapshot.
- All receipt events now require both correlation and causation to equal the submitted command identity. The HTTP persistence fixture now records EventStore-compatible command causation.
- Added complete prior-value tampering coverage, correlation/causation tampering across every receipt action and both use-event positions, matching positive cases, opaque loader refusals, direct-fold non-mutation checks, legacy repeated scope-changing adjustments, and full/suffix/streamed rejection checks that preserve selected contents and ETags.
- No runtime topology, deployed data, dependency, or sibling checkout changed. Final solution/all-six gates, workflow review, and commit/push remain with the parent build workflow.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
|---------|-----------------|----------|
| B1: adjustment without trusted snapshot can commit before refusal | medium / patch | Retained overload reaches SubmitAndObserveStatusAsync before the new required snapshot check. Refuse this demonstrated impossible-verification state before submission and assert zero submissions. |
| B2: concurrent valid capabilities can change the predecessor between load and commit | false / reject | The owner processor already rechecks current persisted state, and the new verifier correctly refuses a receipt whose actual predecessor differs from the trusted pre-submit snapshot. No false success or audit attribution is accepted. Receipt refusal has never promised rollback; broader stale-snapshot coordination/reconciliation is already separately tracked. Atomic predecessor reservation is not needed to satisfy this receipt-integrity change. |
| B3: snapshot-only overload cannot verify a commented adjustment | medium / patch | Without a resolved capability, expected comment issuance is skipped and existing verification requires capability context. Reject this demonstrably unsupported call before dispatch; preserve the resolved HTTP path. |
| B4: existing prior-comment preservation/removal/policy not exercised | medium / patch | New field matrix seeds null prior comment only. Add a valid existing-comment receipt and removal/text/policy tampering to exercise complete pre-submit capture. |
| B5: prepared legacy rebuild values not extended by suffix delivery | low / patch | Existing assertions compare prepared bytes only. Seed each prepared value and append a valid event to exercise the continuing digest. |
| B6: first adjustment audit snapshots overwritten before assertion | low / patch | Assertions inspect the second adjustment only. Check both complete original snapshots immediately after first delivery. |
| B7: legacy parity checks only three fields and omit authoritative replay | low / patch | Extend this existing focused test with authoritative replay and full effective/audit comparisons; no new production path is needed. |
| B8: canonical duplicate targets approval rather than legacy adjustment | low / patch | Redeliver the actual adjustment events and assert unchanged selected contents. |
| B9: repeated legacy adjustment followed only by rejected correction | low / patch | Extend the existing parameterized history with approved correction and omitted corrected scope, covering its continuing digest/attribution branch. |

Edge-case and verification-gap layers returned no findings. All three reports were collected before triage. Patches add no public surface and address demonstrated pre-dispatch states or direct gaps in the new focused regressions.

## Verification


Implementation verification receipts (2026-10-22; local focused checks):

- Built Server.Tests, Projections.Tests, and IntegrationTests individually using `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build <project.csproj> --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`; all passed with zero warnings/errors. Catalog and Works defaults both select EventStore `3.126.0`; no override was needed.
- Before production fixes, direct xUnit regressions failed as intended: server attribution/loader 5/5 failures; projection attribution/legacy-adjustment 5/5 failures; submission service 3/6 failures (causation and missing trusted prior, with existing correlation refusals and matching confirmation already passing); integration tampering 18/38 failures (14 causation cases and prior date/duration/billable/comment). Logs: `/tmp/audit-server-red.log`, `/tmp/audit-projection-red.log`, `/tmp/audit-submission-red.log`, `/tmp/audit-integration-red.log`.
- Green server command: `tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class '*TimeEntryStateTests' -class '*EventStoreMagicLinkConfirmationCapabilityStateLoaderTests' -class '*MagicLinkDurableSubmissionServiceTests'` — 122 passed, zero failed/skipped.
- Green projection command: `tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*TimeEntryEvidenceProjectionTests' -class '*TimeEntryEvidenceProjectionHandlerTests'` — 132 passed, zero failed/skipped.
- Green integration matrix command: `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*MagicLinkConfirmationHttpBoundaryTests.Every_receipt_requires_submitted_command_correlation_and_causation' -method '*MagicLinkConfirmationHttpBoundaryTests.Adjustment_receipt_requires_every_trusted_prior_value'` — 38 passed, zero failed/skipped, 18.549 s. Log: `/tmp/audit-integration-matrix-green.log`.
- HTTP focused class command is running: `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkConfirmationHttpBoundaryTests'`. `MagicLinkPersistedSubmissionTests.cs` belongs to that same partial class. Result pending in `/tmp/audit-integration-green.log`.
- `git diff --check` passed after implementation. Final full gates and review remain pending; these focused checks supply no runtime or deployed acceptance.

Build focused test projects in Debug/source mode and show new negative regressions fail before fixes. Run affected xUnit classes with single-dash filters. Then restore and build the solution using matching configuration:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Run all six freshly built xUnit executables individually. Works and catalog currently select EventStore 3.126.0; apply a catalog-derived override to both commands only if defaults diverge. Record intentional skips separately. Runtime preflight found a foreign Tenants AppHost and six associated Dapr apps plus memories; leave them untouched and do not claim live qualification. Review through the remaining build workflow, run `git diff --check`, validate a Conventional Commit using commitlint, fetch/inspect divergence and push owned changes to main without force.

Parent audit: every matrix row has executed passing focused coverage (122 server, 132 projection, 38 receipt cases). The full affected HTTP class also completed: 772 passed, zero failures/errors/skips/not-run in 592.504 seconds. Review patches and final full gates remain pending. The two concurrently modified MCP configuration files are unrelated and excluded from staging/review/commit.

Review patch verification (2026-10-22): all requested local patches are complete. Unsupported adjustment calls now refuse before submission with caller cancellation preserved. New checks cover zero submissions, existing prior-comment matching/removal/text/policy cases, complete first audit snapshots, actual adjustment redelivery, authoritative/direct/persisted parity, and suffix continuation from array/streamed rebuilt values through both correction paths. Targeted runs passed: service 22/22, expanded projection matrix 8/8, integration 43/43; builds and whitespace checks passed. The two newly added receipt matrix methods now use PascalCase. The parent read the full staged implementation diff and the complete patch diff; no acceptance or matrix gap remains. Final solution and all-six gates are in progress.


Final post-review verification (2026-10-22): matching Debug/source solution restore exited 0; warnings-as-errors solution build exited 0 with zero warnings/errors (7.06 s). All six freshly built executables exited 0 on the final patched graph. Source/test input hashes remained unchanged during verification. No EventStore version override was needed.

| Suite | Passed | Skipped | Seconds |
|-------|-------:|--------:|--------:|
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 10.805 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.775 |
| Hexalith.Timesheets.IntegrationTests | 890 | 5 | 503.990 |
| Hexalith.Timesheets.Projections.Tests | 583 | 0 | 515.066 |
| Hexalith.Timesheets.Server.Tests | 606 | 0 | 2.152 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.508 |

Total: 2,350 passed, 5 intentional skips, zero failures/errors/not-run. The skipped infrastructure and performance lanes retain their existing opt-ins; no runtime/deployed acceptance is inferred. All eight accepted review findings were patched and targeted checks passed; the remaining concurrency suggestion was rejected with the specific reasoning in the triage log. Nothing newly deferred. Commitlint and staged whitespace checks passed. Remote main matched the inspection baseline when fetched. Both unrelated MCP configuration files remained byte-for-byte unchanged and excluded from staging/commit.

Local receipts: `/tmp/timesheets-audit-final-results.json`, `/tmp/timesheets-audit-final-inputs.json`, `/tmp/timesheets-audit-final-restore.log`, `/tmp/timesheets-audit-final-build.log`, and the per-suite logs named in the results. The full final implementation diff is `/tmp/timesheets-audit-integrity-ni87hyal.diff`. The tables and commands here retain the acceptance summary independently of temporary logs.
