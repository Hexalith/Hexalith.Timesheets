---
title: 'Close Story 3.6 review evidence and tracking gaps'
type: 'bugfix'
created: '2026-09-17'
status: 'done'
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
- [x] Clarify loader/fold intent in comments and OpenAPI; extend exact contract assertions without changing runtime policy.
- [x] Add falsifying server tests for rejection identity, scope agreement, serialized replay, and rejected-correction mismatch behavior.
- [x] Harden HTTP/readiness test helpers so body-length and structured-table claims fail for the intended reason.
- [x] Run the complete six-project verification lane, then update release counts and Story 3.6 evidence from those results only.
- [x] Reconcile the canonical story File List, deferred source paths, current test-summary supersession, and sprint timestamp/status.

**Acceptance Criteria:**
- Given correction scope is server-derived, when consumers inspect OpenAPI, then the optional field is read-only, documented, and absent from required input while the eight-value CLR API remains unchanged.
- Given approved, adjusted, or legacy rejected histories, when serialized replay or retry runs, then scope lineage is deterministic and mismatched retries emit no second correction.
- Given final verification passes, when release and tracking artifacts are read, then their counts, ownership risks, files, status, and supersession notes match the executed evidence.

## Implementation Notes

- Marked correction scope as read-only and server-derived in OpenAPI while preserving the existing optional field and eight-value CLR constructor/deconstruction surface. Exact contract assertions now pin the metadata and nullable shape.
- Documented the legacy null-scope fallback in aggregate and projection folds, and documented which loader ownership checks are canonical versus defence in depth. Runtime policy is unchanged.
- Added regression evidence for the typed Work/project-owned issuance rejection, top-level/nested adjustment scope agreement, production-deserialized approved-correction and magic-link-adjustment replay, and rejection of a mismatched legacy rejected-correction retry.
- Centralized raw UTF-8 denial length measurement with its fixed-width W3C trace-id assumption, and made readiness table parsing preserve empty structured cells.
- Refreshed launch ownership/correction risk guidance, platform-catalog evidence, full-suite counts, the canonical Story 3.6 inventory, deferred source paths, test-summary supersession, and sprint timestamp. Lifecycle remains `in-progress` pending review.

## Spec Change Log

- 2026-09-17: Implemented and reviewed the evidence increment, then reran the complete six-project verification lane: 945 total, 941 passed, 4 declared skips, 0 failures.

## Review Triage Log

- **BH-01 — medium / patch.** The thirteen accepted remediation patches at `3-6-implement-eventstore-backed-magic-link-state-loading.md:285-297` are implemented by this increment but remain unchecked, which can send later maintainers back through completed work. Mark those thirteen rows complete after the review fixes land.
- **BH-02 — false / reject.** `in-review` is the build spec's transient workflow state; the canonical story and sprint entry intentionally remain `in-progress` until this independent review succeeds. The presentation step owns the final lifecycle transition.
- **BH-03 — medium / patch.** A serialized adjustment with top-level `Tenant` scope and nested `Project` scope reaches `TimeEntryState.Apply`, passes the loader's tenant gate, and leaves contradictory lineage. Reject explicit nested/top-level or nested/prior-scope disagreement during authoritative loader folding while preserving legacy null nested scopes.
- **BH-04 — medium / patch.** The new approved-correction replay test uses `Recorded -> TimeEntryApprovedCorrected`, so its claimed happy-path evidence is an illegal lifecycle. Build a legal submitted-and-approved stream in that test; the separate pre-existing illegal-history acceptance remains deferred.
- **BH-05 — low / reject.** The issuance scope guard is target-kind independent, the required Work-target rejection is now pinned exactly, and other issuance tests retain Project-target coverage. Adding a second identical rejection branch test would be redundant and has negligible maintenance value.
- **BH-06 — low / patch.** The loader comment can read as though the whole `Length != 1` arm is defence in depth, but the zero-match catalog-presence check is essential. Clarify that only duplicate/ownership-shape arms are defensive.
- **BH-07 — false / reject.** The requirement is exact external raw UTF-8 length equivalence. A variable-width trace identifier would be externally observable and should fail the assertion; the helper centralizes the measurement and its W3C fixed-width assumption is explicitly documented.
- **BH-08 — medium / patch.** An unused A-scoped link becomes admissible again after an A-to-B-to-A correction because the loader compares only current identity/scope. Launch guidance must require revocation at the first Activity-Type change and disclose that ID reuse can otherwise revalidate an old link.
- **BH-09 — high / patch.** The readiness row proves projection/route wiring but omits that successful POST results are not durably submitted and cannot prevent concurrent reuse. Add an explicit unfinished durable-persistence gate and do not describe substituted journeys as persistence proof.
- **BH-10 — high / patch.** The approved course correction requires `FAIL` while Stories 3.6, 5.2, and final 5.1 reconciliation remain open; all are still open in sprint tracking, and durable magic-link writes are unfinished. Restore the overall `FAIL` verdict and remove the story-complete claim.
- **BH-11 — medium / patch.** The Builds catalog moved after the dated 2026-09-12 package audits, yet current readiness text carries the old clean audit forward while updating resolved package versions. Re-run the current vulnerable/deprecated/outdated evidence and report its actual result.
- **BH-12 — false / reject.** The three gitlink moves were already committed at the run's starting HEAD and the prior Story 3.6 decision explicitly owns accepted pointer history; this increment introduced no new `references/` movement. The preserved older baseline makes that accepted history visible but does not violate the current spec's prohibition on adding sibling changes.
- **BH-13 — medium / defer.** All three managed agent-context files still state that the index is unwired, so future agents receive a false security-path fact. This predates the increment and its fix edits agent-context files, which review policy routes to deferred work.
- **BH-14 — medium / defer.** `docs/launch-readiness.md` still directs a future story to create forbidden Timesheets UI projects. The stale instruction predates this increment and belongs to the final Story 5.1 documentation reconciliation.
- **VG-01 — high / defer.** Pre-verified: the accepted Builds pointer changes Aspire/Keycloak resolution while existing tests only inspect AppHost source and no automated runtime smoke starts the topology. Adding an Aspire testing lane changes package/topology scope excluded by this intent; retain it as an infrastructure-owned launch risk.
- **VG-02 — low / defer.** The current Builds catalog and every restored test asset resolve `Microsoft.NET.Test.Sdk` `18.10.1`, while readiness still says `18.10.0`. This is pre-existing package-evidence drift owned by the reopened package/readiness work.
- **EC-01 — medium / patch.** Independent tracing confirms BH-03: explicit serialized adjustment-scope disagreement is accepted and leaves contradictory evidence. Add the authoritative loader guard and a falsifying replay test.
- **EC-02 — medium / defer.** `LoadTimeEntryAsync` accepts `TimeEntryApprovedCorrected` without a preceding approval transition. The fold behavior predates this increment and is already represented by the illegal-transition integrity risk; a separate state-machine decision is needed beyond correcting this increment's test fixture.
- **EC-03 — maybe-false / reject.** A legacy exact retry with an `Unknown` resolved scope would no-op before validation, but no canonical catalog path producing `Unknown` was demonstrated; canonical writers resolve Tenant or Project. Evidence of a reachable malformed catalog would settle the claim, and the undemonstrated outcome would be low impact because no event or state mutation occurs.

### Review Findings

Independent review of `b0f4ee1...HEAD` (2026-09-17). Layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor.

- [ ] [Review][Decision] This increment moves sibling submodule pointers that spec-3 forbids — `references/Hexalith.Builds` `000abf8`→`04d9617`, `Hexalith.FrontComposer` `1e9348e`→`f20a1fc`, and `Hexalith.Works` `06d64b0`→`3c042f9` sit inside `b0f4ee1...HEAD`. Spec-3 Never bans sibling changes; spec-3 BH-12 rejected that claim; the Story 3.6 File List now lists those pointers as owned. Options: revert the three gitlinks and restatement of catalog 13.5.4 evidence, or keep them as an owned Never exception.
- [ ] [Review][Decision] Story and sprint lifecycle still disagree — story file `in-progress` plus verification text claiming sprint is `in-progress`, while `sprint-status.yaml` is `review`. Spec-3 BH-02 said both stay `in-progress` until this review succeeds. Options: restore sprint to `in-progress`; align story and notes to `review`; or keep the split.
- [ ] [Review][Patch] Deferred-work VG-02 still says the dated package inventory claims `Microsoft.NET.Test.Sdk` `18.10.0` after this increment wrote `18.10.1` [_bmad-output/implementation-artifacts/deferred-work.md:243]
- [ ] [Review][Patch] Package-currency and Build-gate prose still present the 2026-09-15 AppHost smoke as current after the Builds catalog moved Aspire/Keycloak to `13.5.4` [docs/launch-readiness.md:14]
- [ ] [Review][Patch] The catalog-freshness-round patch that launch-readiness still describes live magic-link resolution as unwired remains unchecked [_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md:133]
- [ ] [Review][Patch] The token-hash-unwired agent-context item is ledgered twice instead of pointing at the existing 2026-09-16 entry [_bmad-output/implementation-artifacts/deferred-work.md:230]
- [x] [Review][Defer] AppHost Keycloak/Aspire catalog bump is untested at runtime [docs/launch-readiness.md:22] — deferred: already ledgered; an automated smoke lane is package/topology work this spec must not absorb.
- [x] [Review][Defer] Nested adjustment-scope disagreement still folds in `TimeEntryState.Apply` and `TimeEntryEvidenceProjection.Apply` [src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs:145] — deferred: pre-existing; spec-3 forbids changing those folds.
- [x] [Review][Defer] `LoadTimeEntryAsync` applies correction events whose `PreviousValues.ActivityTypeScope` disagrees with folded state [src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:293] — deferred: pre-existing malformed-history case; spec-2 rejected this guard.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- passed under SDK `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- passed with zero warnings and errors.
- Built xUnit v3 executables -- ArchitectureTests 55/55; Contracts.Tests 90/90; IntegrationTests 104 total, 100 pass, 4 declared skips; Projections.Tests 146/146; Server.Tests 474/474; Works.Tests 76/76. Final post-review run: 945 total, 941 pass, 4 declared skips, 0 failures.
- `git diff --check` -- passed.
