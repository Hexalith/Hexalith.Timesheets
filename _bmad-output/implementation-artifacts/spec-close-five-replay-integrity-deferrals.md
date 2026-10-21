---
title: 'Close five deferred replay-integrity concerns'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 3f82f27c871687a22118c1c0dea4060e3ff30eda
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Authoritative capability and Time Entry folds accept impossible lifecycle orders or contradictory Activity Type scope evidence. Close five ledger concerns together: capability transitions; Time Entry lifecycle ordering; approved correction without approval; nested adjustment scopes; correction previous scopes. Reject malformed recognized history before mutation and preserve valid and legacy replay. Commit and push the verified result to main.

## Boundaries & Constraints

Always follow sanctioned Handle transitions; keep aggregates pure, persistence through EventStore, and public denials opaque. Preserve omitted nested scopes through deterministic preceding-scope inheritance, wrapped payloads, rejected-command no-ops, and canonical duplicate normalization. Distinct physical events remain distinct.

Never rewrite events, alter unknown-event or confirmation-after-approval policy, add dependencies/topology/public contracts, or edit sibling checkouts. This is defensive integrity work: no sanctioned-writer reachability, deployed corruption, deployed acceptance, or launch readiness is established.

## I/O & Edge-Case Matrix

| History | Expected behavior |
|---------|-------------------|
| First issuance followed by one terminal transition | Preserve valid folded result |
| Terminal before issuance, repeated issuance/terminal, post-terminal issuance | Integrity exception before mutation; loader denies opaquely |
| Record, submit, approve/reject; rejected correction/resubmission; approved correction | Preserve sanctioned state and lineage |
| Repeated recording or lifecycle event before its required predecessor | Reject selected-owner history before mutation/materialization |
| Explicit previous adjustment/correction scope differs from folded scope | Reject contradictory prior evidence |
| Explicit adjusted nested scope differs from top-level scope | Reject contradictory adjusted evidence |
| Nested scope omitted in supported legacy history | Retain current deterministic inheritance without catalog inference |

</frozen-after-approval>

## Code Map

Production paths below are relative to `src/Hexalith.Timesheets.Server/` unless qualified:

- `MagicLinks/MagicLinkCapabilityState.cs`: permissive Apply methods; wrappers delegate here.
- `TimeEntries/TimeEntryState.cs`: authoritative folds; preserve terminal-capability bookkeeping.
- `TimeEntries/TimeEntry.cs`: sanctioned Handle transitions define valid lifecycle.
- `MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`: already guards issuance and adjustment scopes; integrity exceptions fail closed. Avoid duplicating existing defenses.
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`: full replay skips illegal transitions and resets on repeated recording; incremental replay requires prior state.
- Adjacent `TimeEntryEvidenceProjectionHandler.cs`: already validates full/suffix snapshots, scopes, and approval lineage; preserve fences and conflict handling.

## Tasks & Acceptance

Paths resolve through the Code Map and named test directories.

- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryStateTests.cs` and `MagicLinkCapabilityStateTests.cs`: add focused lifecycle/scope regressions, unchanged-state assertions after refusal, valid and legacy cases.
- [x] `MagicLinks/MagicLinkCapabilityState.cs` and `TimeEntries/TimeEntryState.cs`: guard transitions and explicit nested scopes before field changes; preserve wrapped replay.
- [x] `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`: align full/incremental refusal without rejecting unrelated-owner filtering or canonical redelivery.
- [x] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`: prove opaque denial for malformed histories; repair positive fixtures missing required approval/rejection transitions. Check affected `TimeEntryAggregateTests.cs` and `MagicLinkEventStoreDomainProcessorTests.cs` in that directory.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` and `TimeEntryEvidenceProjectionHandlerTests.cs`: prove full/suffix refusal, unchanged persisted selection after invalid delivery, and equivalent valid replay.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: add dated resolution notes to the five exact concerns after verification; preserve original evidence and other entries.

Acceptance:

- Given malformed matrix history, when authoritative or evidence replay processes it, then it cannot reopen capability authority or publish contradictory evidence.
- Given valid current or supported legacy history, when replayed or canonically redelivered, then state and evidence remain equivalent.
- Given an invalid transition or projection delivery, when inspected afterward, then preceding state remains intact with no invalid committed write.

## Implementation Notes

Baseline: `3f82f27c871687a22118c1c0dea4060e3ff30eda`, clean main. Selected ledger entries are the exact five Intent concerns (historical positions 37, 38, 45, 46, 47); they overlap across layers and form one goal. No implementation begun, unresolved intent gaps, migration, or data mutation. Footprint: three production files, two new state test files, affected fixtures, ledger, spec. Existing Apply methods gain integrity refusal; no new public surface.

Implementation completed after user approval. The three folds now refuse malformed recognized lifecycle/scope evidence before mutation. Full evidence replay shares its transition core with incremental replay while retaining one linear lineage accumulation. Synthetic Superseded command-lock fixtures use test-only state seeding because sanctioned writers emit no such event. Exactly five ledger resolution notes were added.

Focused audit passed: six new state tests; Server suite 572/572; direct evidence/handler classes 127/127. Covering tests exercise every matrix row, including legacy omissions, wrapped events, no-ops, canonical redelivery and persisted contents/ETag preservation. Negative regressions failed the baseline first (five state tests and one projection test). Full solution restore/build passed with zero warnings/errors. Final full Projections and Integration results are still being collected; no completion claim is made until those exit successfully. Runtime recheck found eleven foreign Dapr entries and two foreign AppHosts; no runtime was started or stopped.

Review patches completed: all four command-processor replay boundaries now map only TargetInvocationException wrapping InvalidOperationException to existing opaque rejection. Actual raw/wrapped replay and cancellation tests cover those paths. Destination-enum regressions now exercise nine wrong-state cases across state, loader and direct evidence folds; streamed preparation covers all sixteen malformed handler histories with persisted contents/ETag and empty-store assertions. The successful legacy fixture's previous duration matches its 75-minute adjusted state. Focused review checks passed 120 Server tests and 127 projection tests, with clean builds.

The full integration attempt exposed one additional synthetic terminal-only fixture constructing an impossible state before its processor assertion. Its setup now passes raw and wrapped terminal-only history through ProcessAsync, retaining both rejection checks; the focused method passed 1/1 after a clean IntegrationTests build. Earlier interrupted runs are not acceptance evidence. The final Integration run uses the repaired fixture. Three pre-existing audit/legacy-delivery differences were deferred; two low-impact direct-capability suggestions were rejected with reasons above.

Three renamed projection regressions were normalized to PascalCase after the full projection pass; their bodies are unchanged. The projection test project rebuilt cleanly and the affected class was rerun successfully; this naming-only correction changes no production behavior.

Final post-review acceptance: matching Debug/source solution restore and warnings-as-errors build exited 0 (zero warnings/errors). All six individual executables exited 0 on the patched production graph. Works and catalog both select EventStore 3.126.0; no override was used.

| Suite | Passed | Skipped | Seconds |
|-------|-------:|--------:|--------:|
| ArchitectureTests | 103 | 0 | 14.855 |
| Contracts.Tests | 92 | 0 | 0.791 |
| Server.Tests | 589 | 0 | 2.274 |
| Projections.Tests | 574 | 0 | 578.184 |
| IntegrationTests | 848 | 5 | 537.443 |
| Works.Tests | 76 | 0 | 0.464 |

Total: 2,282 passed, five intentional skips, zero failures/errors/not-run. The Integration pass includes the repaired terminal-only fixture; the Projections pass includes all sixteen streamed refusal histories. The subsequent naming-only rebuild passed the affected 51-test projection class. No source or behavior changed afterward. Interrupted predecessor runs were discarded and are not acceptance receipts. Runtime/deployed qualification remains unverified because foreign AppHosts occupy shared placement. Commitlint and whitespace checks passed; remote main matched the inspection baseline when fetched.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
|---------|-----------------|----------|
| Blind B1: reflected fold refusal escapes processor rejection | medium / patch | SDK InvokeWithCancellation wraps non-cancellation Apply failures in TargetInvocationException; all four processor replay catches omit that wrapper. New refusals therefore bypass Reject. Catch only the demonstrated wrapped integrity failure and test real replay dispatch. |
| Blind B2: invalid positive legacy correction fixture | low / patch | The new valid-history test adjusts duration to 75 but supplies a 60-minute correction PreviousValues snapshot. Repair that literal snapshot while retaining legacy omitted scopes. |
| Blind B3: destination enum regressions absent | medium / patch | New state/projection guards validate event destination enums, but the type-based negative matrices skip valid event types carrying wrong destination states. Add targeted before-mutation and evidence regressions. |
| Blind B4: persisted legacy adjustment omission differs from direct folds | medium / defer | Handler full, suffix and streamed validators already compare adjustment snapshots and nested adjusted scope without omission inheritance; these lines are unchanged. The direct folds' existing omitted-scope behavior remains preserved. Runtime legacy-delivery compatibility needs its own policy and tests. |
| Blind B5: streamed rebuild refusal matrix absent | medium / patch | Existing streamed tests cover valid equivalence/erasure, while the new refusal matrix exercises full/suffix delivery only. Extend it to streamed preparation and assert selected contents/ETag preservation. |
| Blind B6: approved correction decision attribution differs | medium / defer | State/direct projection predate this increment in accepting mismatched source decision ID/scope; handler already checks that lineage. This change guards lifecycle predecessor and Activity Type scopes, not all audit attribution. |
| Blind B7: rejected correction decision attribution differs | medium / defer | State/direct projection previously accepted mismatched rejection ID/reason; handler already compares prior rejection. Broader audit-lineage parity remains a separate defensive increment. |
| Blind B8: direct capability terminal identity unchecked | low / rejected | Direct state accepts mismatched terminal identities, as before; loader MatchesCapabilityIdentity rejects them before Apply, and processor issue replay rejects any existing capability. No affected production caller bypassing those fences was shown. Adding direct identity policy/legacy guards is disproportionate to this rarely encountered malformed-input gap. |
| Blind B9: direct expiry event can precede expiry instant | low / rejected | HandleExpire already refuses early expiry, and a malformed stored expiry can only terminalize/deny the link. This pre-existing timestamp-consistency defense has no demonstrated everyday caller harm; a new replay-time prerequisite is outside the selected order/scope fixes. |
| Verification V1: initial non-Draft recording untested | medium / patch, grouped with B3 | Pre-verified regression gap: removing only the new recording destination-state guard leaves state/loader matrices green. Add non-Draft authoritative and opaque-loader cases. |

Edge-case reviewer returned no findings. All three layers completed before triage. Patch groups: processor exception boundary; valid fixture; destination-state coverage (B3/V1); streamed refusal coverage. No intent or spec loopback is required: each fix is local, adds no public surface, and addresses a demonstrated state or missing regression.


## Verification

Build focused Server.Tests/Projections.Tests in Debug/source mode; run new classes with xUnit v3 single-dash filters and demonstrate failures before the fix. Then:

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`

Compare Works/catalog EventStore defaults; add the catalog override to both commands only if they differ. Run all six freshly built Debug xUnit executables individually. Record default skips separately.

Runtime preflight: `dapr list -o json` found seven foreign apps; `timeout 20s aspire ps --format Json` identified the running Tenants AppHost. Both exited 0. Leave it running; no Timesheets start while shared placement is occupied. Record this separately from offline evidence.

Review changes, run `git diff --check`, validate Conventional Commit with installed commitlint, fetch/inspect divergence, commit owned files, and push main without force after workflow completion.
