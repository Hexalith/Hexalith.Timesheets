---
title: 'Close Story 3.6 review evidence and tracking gaps'
type: 'bugfix'
created: '2026-09-17'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b0f4ee12a46d0db1d7454d54ebc37b2837200b93'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
  - '_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 remains in progress after its loader-policy remediation because accepted review patches leave server-derived contract metadata, scope-lineage regression evidence, release counts and ownership guidance, and lifecycle tracking inaccurate or insufficiently pinned.

**Approach:** Close the accepted review increment without changing the tenant-only magic-link policy or its opaque failure behavior: document the existing defensive gates, add falsifying contract/replay tests, refresh verified release evidence, and reconcile the Story 3.6 artifacts.

**Decision:** Durable confirm/adjust persistence is split into a dedicated high-priority remediation for unfinished Story 3.3/3.4 work. This increment must not imply that substituted HTTP journeys prove persisted writes.

## Boundaries & Constraints

**Always:** Keep EventStore authoritative, the token-hash index non-authoritative, correction scope server-derived, legacy null-scope replay deterministic, Project and Work targets supported with tenant-owned Activity Types, and every invalid external path on the canonical opaque denial.

**Never:** Infer or rewrite historical scope, add migration tooling, weaken no-disclosure, treat projection state as write authority, add topology/UI/package/sibling changes, absorb already-deferred availability or Project-restriction policy, or implement confirm/adjust as two non-atomic EventStore submissions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Issuance policy | Work target with project-owned Activity Type | Typed `ActivityTypeScopeMismatch` with `activityTypeId/scope-mismatch` | No token generation or dispatch |
| Legacy retry | Scope-less rejected correction retried with mismatched values | Retry rejects rather than emitting another correction | Prior scope remains unchanged |
| Serialized lineage | Approved correction or magic-link adjustment carrying resolved scope | Production deserializer/fold preserves top-level and nested scope agreement | Malformed history remains fail closed |
| Release evidence | Final rebuilt test executables and ownership inventory | Counts, risks, revisit actions, summaries, and tracker state agree | Do not claim unexecuted or waived evidence |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- explain which catalog ownership checks are canonical versus defence in depth; behavior stays unchanged.
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` and `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` -- mark optional correction scope read-only/server-derived and pin its exact shape.
- `src/Hexalith.Timesheets.Server/TimeEntries/{TimeEntry.cs,TimeEntryState.cs}` and `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` -- document intentional legacy-null fallback semantics; do not change folds.
- `tests/Hexalith.Timesheets.Server.Tests/{MagicLinkConfirmationCapabilityCommandServiceTests.cs,TimeEntryAggregateTests.cs,EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs}` -- pin issuance rejection, nested adjustment scope, mismatched retry, and serialized approved/adjusted replay.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- centralize raw-byte measurement and state the fixed-length trace-id assumption.
- `docs/launch-readiness.md` and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- correct final counts, ownership/correction risks, real versus defensive gates, and robust structured-row parsing.
- `_bmad-output/implementation-artifacts/{3-6-implement-eventstore-backed-magic-link-state-loading.md,deferred-work.md,sprint-status.yaml,tests/3-6-test-summary.md}` -- reconcile current files, relative sources, timestamp/status, and superseded evidence.

## Tasks & Acceptance

**Execution:**
- [ ] Clarify loader/fold intent in comments and OpenAPI; extend exact contract assertions without changing runtime policy.
- [ ] Add falsifying server tests for rejection identity, scope agreement, serialized replay, and rejected-correction mismatch behavior.
- [ ] Harden HTTP/readiness test helpers so body-length and structured-table claims fail for the intended reason.
- [ ] Run the complete six-project verification lane, then update release counts and Story 3.6 evidence from those results only.
- [ ] Reconcile the canonical story File List, deferred source paths, current test-summary supersession, and sprint timestamp/status.

**Acceptance Criteria:**
- Given correction scope is server-derived, when consumers inspect OpenAPI, then the optional field is read-only, documented, and absent from required input while the eight-value CLR API remains unchanged.
- Given approved, adjusted, or legacy rejected histories, when serialized replay or retry runs, then scope lineage is deterministic and mismatched retries emit no second correction.
- Given final verification passes, when release and tracking artifacts are read, then their counts, ownership risks, files, status, and supersession notes match the executed evidence.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- restore succeeds under the pinned SDK.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- build succeeds with zero warnings/errors.
- Run each built xUnit v3 executable under `tests/*/bin/Debug/net10.0/`, including Works.Tests; keep performance skips unless `TIMESHEETS_PERF=1` is explicitly set -- all default lanes pass and only declared skips remain.
- `git diff --check` -- no whitespace errors.
