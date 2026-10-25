---
title: 'Resolve five magic-link eligibility and authorization deferrals'
type: 'bugfix'
created: '2026-10-25'
status: 'done'
baseline_commit: 'a71c2c1e14a8630eed87866b95e6c62445685481'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Confirmation and retired-type adjustment have unresolved eligibility rules. Expiry conflates workload and Party identity. Related delivered fixes remain open in the ledger.

**Approach:** Complete DW-59, DW-62, DW-73, DW-76 and DW-124: align display/commit eligibility, distinguish system expiry authority, and verify settled confirmation/authentication behavior. Review, validate, commit and push to main.

**Approved decisions (2026-10-25):** The user selected the recommendations: confirmation remains valid after approval or correction without changing lifecycle state; adjustment may replace a retired recorded Activity Type with an active permitted type. The existing Project restriction gap DW-63 remains separately open.

## Boundaries & Constraints

**Always:** Preserve opaque failures, single-use protection, ownership checks, server authorization, pure aggregates, EventStore persistence, retired-type confirm-as-is and historical evidence. System expiry defaults to denial. Local tests do not establish deployed acceptance.

**Never:** Infer Party authority from workload identity. Change no AppHost/topology, dependencies, submodules or managed guidance. Keep DW-113, DW-63, runtime/deployment gates open and launch FAIL.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Confirmation | Approved/corrected entry or retired type | Allow confirmation after approval/correction and type retirement; preserve lifecycle state | Opaque denial, no partial writes |
| Adjustment | Retired recorded type, active replacement | Allow active permitted replacement; display and POST agree | Missing/foreign/ambiguous/stale evidence refuses use |
| Expiry | Verified workload or explicit Party | Separate workload authority or existing Party gate | Default denial; reject forged/missing evidence and name collisions |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs`: shared scope/display checks.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`: aggregate confirmation rules.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs`: authoritative processing; baseline expiry fell back to `envelope.UserId`.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`: kernel DI.
- `src/Hexalith.Timesheets/Program.cs`: existing authentication middleware.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`: retired-type and credential regressions.

## Tasks & Acceptance

**Execution:**

- [x] Command service, `TimeEntry.cs` and processor above — apply chosen eligibility consistently; preserve DW-62's settled retired-type confirmation.
- [x] New `src/Hexalith.Timesheets.Server/MagicLinks/IMagicLinkSystemExpiryAuthorizer.cs` and `DenyAllMagicLinkSystemExpiryAuthorizer.cs`, processor and DI above — authorize null-actor expiry through verified workload evidence scoped to tenant/capability/target; keep explicit actors on Party management authorization.
- [x] `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs` and `MagicLinkEventStoreDomainProcessorTests.cs` — test eligibility, system denial/allowance, forged/missing evidence and workload/Party collisions.
- [x] HTTP tests above and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs` — test display/POST agreement, preserved lifecycle state, persisted evidence, atomic refusal and replay. Reuse credential tests for DW-76.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`, this spec and adjacent `tests/five-magic-link-eligibility-deferrals.md` — document decisions/results and exactly five scoped closures; preserve historical notes, explicitly superseding DW-62's old retention disposition.

**Acceptance Criteria:**

- Given each eligibility case, when display/commit run, then results agree and confirmation preserves lifecycle state.
- Given matching workload/Party names, when actorless expiry runs, then Party authority cannot authorize it.
- Given passing credential tests, when DW-76 closes, then DW-113 stays open.
- Given passing checks/review, when publication runs, then a validated Conventional Commit reaches origin/main.

## Implementation Notes

- User approved both recommended decisions and continuation on 2026-10-25. Re-read the draft before applying approval; the updated frozen intent is now locked. Dispatch reflects the authorization/domain/test footprint. Push to main is authorized.
- Independent investigations found foreign Parties Dapr/Aspire processes blocking live AppHost qualification; nothing was stopped or changed.

- Source implementation and targeted verification are complete: matching full source build has zero warnings/errors; focused unit selections passed 120/120 and HTTP selections passed 23/23. Root inspected the staged implementation diff and named XML outcomes for every matrix row. Four full suites passed 1,014 cases; Projections and Integration remain running. The external fast-forward to `1a80faec5360e0276cb908b594922a88895ff27e` is preserved and excluded from this increment.

- All six full executables completed successfully: 2,674 passed, five expected skips, zero failures/errors/not-run. Root checked the six XML receipts and 45 named matrix/credential cases, recorded the portable verification receipt and five scoped ledger closures, and verified that all original ledger evidence is recoverable. The only post-test C# delta is an XML parameter description; the subsequent full source build passed with zero warnings/errors. Independent review and authorized publication remain.

- Review patches are complete. The build passes with zero warnings/errors; focused correction tests passed 98 server and 42 integration cases. Root verified the restored-source fingerprint and expected freshness-only mutant failure. Independent targeted rechecks confirmed B1/B2 and V1 resolved. Final full Server passed 720 and Architecture 118; Integration is running. Unchanged Contracts/Works/Projections receipts remain applicable; no new deferrals were added.

- Final qualification is complete: 2,693 distinct passing cases and five expected skips across the applicable full runs and source-fitness recheck. Post-review Server passed 720 and Architecture 118. Integration had 933 passes and one comment-only source-fitness failure; correcting that comment, rebuilding and rerunning its entire four-case class resolved it without a runtime change. The receipt preserves the raw full-run exit 1 and separate passing correction, rather than claiming an unfiltered all-green rerun. All review fixes were independently rechecked where applicable; no new deferrals remain. Final scope/preservation/commit checks and the explicitly authorized push follow.

## Spec Change Log

- Review corrections preserve frozen intent: repair the demonstrated expiry handoff and temporal checks, document preliminary service results, and add the missing regressions/receipt parameters. Every correction is a direct patch of known reachable behavior or verification; no new public surface or product decision is required. Preserve the explicit principal separation, lifecycle behavior, retired replacement policy and existing compatibility constructor.

## Review Triage Log

| Finding | Verdict and verified evidence | Route |
|---|---|---|
| B1: HTTP workload subject becomes Party actor | high: expiry endpoint, ambient accessor and assertion source fall back to NameIdentifier; the SDK emits workload:<caller> there. The old no-human expiry HTTP case therefore reaches Party authorization and bypasses the new null-actor policy. | patch principal extraction/handoff and HTTP regressions |
| B2: submitted future instant expires early | medium: HandleExpire compares intent.AtUtc to ExpiresAtUtc, while the new processor branch has no processing-clock lower bound. A scoped workload can terminate a still-usable link early. | patch clock guard and fixed-clock regression |
| B3: public service appears authoritative | low: ConfirmAsync produces preliminary events without catalog input; durable HTTP processing separately validates catalog evidence. Document this public service boundary explicitly without adding a new API. | patch XML documentation |
| B4: no post-preflight catalog refusal proof | medium: missing/foreign/ambiguous/stale unit cases exercise adjustment; no confirmation commit test invalidates recorded-type evidence after successful preflight. | patch, grouped with V1 |
| B5: corrected scope combinations lack direct proof | low: unchanged scope predicates still reject contributor/target/type mismatch, but the new corrected-history fixture changes duration only. Additional corrected-scope cases will substantiate the scoped policy disposition. | patch tests |
| B6: retired replacement body assertion removed | low: the old normalized denial assertion was removed; the new HTTP case checks status alone for selecting a retired replacement. Restore canonical opaque-body evidence. | patch test assertion |
| B7: system expiry lacks recorded-owner coverage | low: new direct processor cases use null owner state; new actor persistence begins with an empty state manager. Existing owner guards remain, but the new branch needs a recorded-history regression. | patch tests |
| B8: system expiry Work-target policy binding untested | low: new authorizer tests all use Project targets, while its scope includes Work references. Add exact Work policy binding/refusal coverage. | patch tests |
| B9: receipt route labels are truncated | low: three passing route cases retain indistinguishable truncated labels. Their unique case IDs exist, but full declared route parameters are missing from the portable summary. | patch receipt metadata |
| B10: intermediate spec progress wording | low: the append-only implementation log retains an intermediate running status before its final completed result. The final receipt/result is explicit; this finding proposes only editing this build's spec. | reject under the review rule for spec-only findings; preserve progress history |
| V1: authoritative confirmation guard lacks negative test | medium: pre-verified gap; an otherwise valid confirmation with a fresh unrelated-type catalog is not tested at the real processor. The freshness-only mutant would pass existing relevant tests. | patch, grouped with B4 |

## Verification

- Restore/build matching Debug/source graph with warnings-as-errors using root AGENTS.md commands.
- Run focused regressions, then all six built xUnit executables individually; skipped runtime lanes supply no acceptance.
- Check intent/evidence preservation, five closures, links, scope and whitespace; complete independent review, commitlint, fetch/divergence inspection and push without force.
