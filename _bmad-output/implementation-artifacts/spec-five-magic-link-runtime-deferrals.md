---
title: 'Complete five magic-link runtime deferred entries'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '024d3aa24a804c5798017cd84b73041e4fb4dfec'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete five ledger entries: both gateway IConfiguration/options requests; the eight-goal prior-round carry-forward; stored-event readback deadlines; and sealed v6 migration resume validation. These cover three runtime fixes plus obsolete fixture cleanup. Preserve original evidence; close entries only after implementation, verification and review. The explicit five-item request supplies combined scope despite independently shippable fixes.

## Boundaries & Constraints

**Always:** Keep Dapr transport in EventStore, preserve caller gateway overrides, fail-closed authorization, durable schemas and opaque failures. Configuration reload must update credentials on subsequent requests. Bound readback; validate sealed migration state before promotion. Validate commits and push completed changes to main as requested.

**Never:** Change AppHost topology, ServiceDefaults, Works sources, deployed data, domain policies or story/sprint acceptance. Layering, fragment retirement and deployed qualification remain separate. Preserve launch FAIL and package CONCERNS.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Bound target/endpoint/token | SDK routes correctly without process mutation |
| Token rotation or removal | Existing clients use the new credential on subsequent sends |
| Completed command, stalled readback | Finite failure outcome; caller cancellation propagates |
| Unchanged sealed migration retry | Validate and promote without further stage appends |
| Changed source/checkpoint/staged membership | Refuse promotion; transport faults remain retryable |

</frozen-after-approval>

## Code Map

Paths below are repository-relative; ES denotes the owning `references/Hexalith.EventStore` repository, Server denotes `src/Hexalith.Timesheets.Server`, and Projections denotes `src/Hexalith.Timesheets.Projections`.

- Server `Runtime/ServiceCollectionExtensions.cs`: environment capture and caller-preserving registration.
- ES `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` and `Handlers/DaprServiceInvocationHandler.cs`: fixed-token SDK extension and final header ownership.
- Server `MagicLinks/MagicLinkDurableSubmissionService.cs` and `MagicLinkSubmissionPollingOptions.cs`: existing issue/use deadlines; transition readback remains unbounded.
- Projections `MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`: sealed retry commits before checkpoint/source validation and outside the abort boundary.
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`, `MagicLinkDurableSubmissionServiceTests.cs`, `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`: configuration, deadlines and eleven obsolete domain-wide arrangements.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV6MigrationTests.cs`: existing interruption/source controls.

## Tasks & Acceptance

**Execution:**

- [x] ES files above and `tests/Hexalith.EventStore.Client.Tests/Handlers/DaprServiceInvocationHandlerTests.cs`: add generic DI-aware routing/credential factories evaluated per send. Preserve the old overload. Test rotation, removal, invalid targets and forged-header replacement. Read owning guidance, commit/push there first, and obtain its published package before updating consumer pins.
- [x] Server registration and new `Runtime/TimesheetsEventStoreGatewayOptions.cs`: bind `Timesheets:EventStoreGateway` with existing DAPR configuration keys as fallbacks; retain localhost:3500/eventstore defaults and origin validation. Newly resolved clients use current endpoint options; each send uses current credentials. Test reload and repeated registration without environment mutation or changes to caller transports.
- [x] Root `Directory.Packages.props`: select the actual published SDK version consistently across EventStore packages. Record full SDK commit/release/gitlink identities. Use the same version during Debug/source verification when Works forwards a different default. Never claim package compatibility with an unpublished API.
- [x] Submission service/options and server tests above: add positive readback timeout, default two seconds, maximum thirty seconds. Bound every stored-event await with linked cancellation and WaitAsync; preserve shorter outer deadlines. Test non-cooperative reads, successful evidence and caller cancellation for issue, transition and use.
- [x] V6 migration and tests above: validate persisted checkpoint/source digest, final staged format/root, counts and bounded tree/owner membership before sealed promotion. Do not append to sealed stages. Preserve recovery on transport/cancellation. Inject post-seal interruption, source/checkpoint/root/node/roster damage and unchanged retry; assert refused promotion retains the selected generation.
- [x] Loader tests: remove eleven `.WithStream(Tenant().TenantId, null, ...)` arrangements; preserve aggregate streams, catalog fixtures and assertions.
- [x] `README.md`, `_bmad-output/implementation-artifacts/deferred-work.md`, and new `tests/five-magic-link-runtime-deferrals.md` under implementation artifacts: document options/deadlines, retain verification evidence and add exactly five dispositions. The carry-forward's delivered portions are sourced by existing `tests/five-runtime-correctness-carry-forwards.md`, `five-eventstore-handoffs.md` and `five-integration-handoffs.md` in that directory.

**Acceptance Criteria:**

- Given configuration reload and an existing gateway client, when a request is sent, then the SDK uses the current credential without altering caller-owned transports.
- Given completed status and stalled readback, when the budget expires, then submission returns its existing failure outcome; caller cancellation propagates.
- Given an interrupted sealed migration, when state is damaged, then retry refuses promotion; unchanged retry succeeds.
- Given the original ledger, when verification and review finish, then exactly the five named entries receive supported dispositions.

## Implementation Notes

- User approved this complete spec and instructed continuing on 2026-10-23, including the SDK prerequisite and publication-dependent consumer update. Explicit commit/push authorization remains in force. Preserve frozen intent. Read repository guidance and use the applicable Aspire lifecycle skill before any required baseline startup; never disturb foreign runtimes. Complete the implementation and its tests; root workflow will perform final independent review.

- Baseline: clean main at `024d3aa24a804c5798017cd84b73041e4fb4dfec`; ES gitlink/checkout `5ce5f1d84b88c808cc8a526b9decf149de977e55`. Read root/AI.Tools LLM, state/Git guidance, editor/build/test configuration and architecture boundaries. No root .gitattributes exists.
- Independent investigation confirmed the selected residuals. Published 3.126.0 API inspection confirms only fixed-token routing; the generic SDK addition is required. Its publication is a consumer dependency: report an exact blocker if unavailable. Broader layering and 128-mutation fragment-cleanup work are excluded.
- Draft only: no application change, ledger closure, dependency update, commit or push. Await the dispatch approval checkpoint.

## Verification

- Run SDK routing/registration regressions and owning required checks before publication.
- Restore/build the solution in matching Debug/source mode, serialized, warnings as errors; run all six Timesheets test executables individually. Use focused negative controls and temporary mutations to prove rotation, deadlines and sealed validation fail when removed.
- Follow remaining bmad-build implementation/review steps; inspect exact five-entry scope, validate commits, fetch/inspect main divergence and push without force. Check package-mode CI separately. Skipped live/performance lanes supply no deployed acceptance.

## Implementation record — 2026-10-23

- Retained implementation, identities, negative controls, proposed five dispositions and runtime limits are in [tests/five-magic-link-runtime-deferrals.md](tests/five-magic-link-runtime-deferrals.md).
- SDK prerequisite independently reviewed and pushed as `b6c903d0d8460e545cff4ffe0b93959619aa3a51`, rebased onto `bc61fe30b840fd7921097c8b36aa839684e0819e`. Rebased Debug/source client build passed with zero warnings/errors; full client suite passed 2,094 cases with two isolated-Dapr skips and zero failures/errors/not-run.
- Current local consumer implementation has a zero-warning Debug/source solution build. Configuration, readback and sealed-state regressions pass. Removing reload behavior, readback WaitAsync, the independent timer, or sealed-resume validation causes the expected focused failures; source bytes were restored and rebuilt.
- SDK publication and consumer pin/package verification remain pending exact-source hosted gates. The ledger is unchanged until publication, final verification and independent root review support all five dispositions. The earlier complete consumer round passed 2,437 tests with five opt-in/infrastructure skips and zero failures/errors/not-run. A final V6-only checkpoint reconstruction correction subsequently passed all 23 focused cases and both falsifying controls; the report preserves the earlier full-round assembly identities and timing separately.
- Root staged and read the complete 1,538-line baseline-relative diff, including every new file, at `/tmp/timesheets-five-runtime-audit-0ncvbu2w.diff`. Raw full-suite and final focused summaries independently confirm the report. All five matrix rows have passing executed coverage; the matrix audit is `/tmp/timesheets-five-runtime-matrix-audit.json`. Approved frozen intent remains SHA-256-identical, and the ledger is still byte-identical to the baseline. The root audit found no remaining local implementation gap; publication-dependent tasks and final workflow review remain open.
- Upstream main advanced again from `cd193902a52abb02572528958436d79a8ae25b9a` to `98e5f45ac300f08c3b8592f6c7a858d0e0535462`, canceling CI `39928722031` after its qualification run `39928721698` had succeeded. The new commit changes a Builds gitlink and records its package-fixture repair; it does not change SDK runtime files. Current required runs are CI `39932017335`, P1R `39932016916` and commitlint `39932017311`. Root keeps the tested SDK checkout/gitlink at `b6c903d0d8460e545cff4ffe0b93959619aa3a51`, has made no Timesheets commit/push, and will select only an actual verified published package. The optional request to keep other SDK pushes paused does not authorize assuming that main is stable.
- The user subsequently selected “Keep SDK main stable until release.” Root will make no further SDK source pushes during this window, monitor the actual live main and required checks, and perform the already authorized SDK publication and Timesheets completion when those checks permit it. Existing commit/push and publication approval remains in force; do not ask for it again.
- Another upstream runtime repair advanced SDK main to `93be37d26c15ba1bb951f23d13d7e1dd9df1ddde` and canceled the preceding CI. It awaits this coordinator's promoted cleanup before captured-owner checkpoint handoff; the routing API is unchanged. Current required runs are CI `39935474875`, P1R `39935474508` and commitlint `39935475002`. Read-only implementation re-engagement identified final focused coverage after package selection: capability handler methods `Applied_rebuild_selects_complete_new_generation_and_prunes_missing_capability`, `Streamed_rebuild_joins_issuance_and_time_entry_terminal_once`, and `CorruptSelectedStorageCanBeRebuiltWithValidPendingJournal`; composed method `NormalDirectoryCandidateRemainsResolvableAfterTenantRebuild`; both SDK `OwnerSequenceHandoffWaitsForThisCoordinatorsPromotedCleanup` rows. V6 does not seed owner checkpoints, so retain its planned final 23-case check. Do not relabel the earlier full b6 source run as a run of this repaired SDK graph.
- While hosted CI ran, root fast-forwarded the clean local SDK checkout to that repaired source without changing SDK code or pushing, then verified the affected paths. SDK build: zero warnings/errors; new overlap cases 2/2 passed. Matching Timesheets source solution build: zero warnings/errors; three projection promotion/handoff/journal cases and the composed 65-owner index readback case all passed. Exact commands/results and separate source identities are in the retained report. Package selection and final version-specific checks remain pending actual publication.
- CI `39935474875` failed only the tracked-content secret guard, which rejected literal synthetic credentials in two new SDK tests. A local repair was prepared but never committed or pushed. Upstream independently delivered the same fixture-policy correction in `f5d817251083d803b8700d01829119e59e98b1fd`; current main is `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3` after a Builds gitlink synchronization. Root preserved its unused patch under `/tmp/five-sdk-unpushed-fixture-repair.patch`, restored only its own two-file edits, and fast-forwarded to the committed upstream source. The unchanged scanner reports zero findings for both repaired tests and the upstream repair record. The Debug/source client build passed with zero warnings/errors; routing classes passed 14/14 in 0.202 seconds. No SDK source push was made by root during the hold. Current required runs are CI `39941734190`, P1R `39941733739` and commitlint `39941734230`.
- The full local SDK tracked-content guard passed against `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`: 1/1, zero findings/failures/errors/skips/not-run, 532.924 seconds. This uses the unchanged scanner built from the preceding runtime source, with current tracked files. Hosted publication gates remain independently required.
- All required exact-source push runs passed on that unchanged live-main commit: CI `39941734190`, P1R `39941733739`, commitlint `39941734230`. Root dispatched the existing SDK Release workflow as run `39946310678` at 2026-10-23 11:28:25 UTC, using already authorized publication scope and preserving every workflow/environment protection. Package identity remains pending the actual release result.

- SDK Release `39946310678` succeeded and published `3.127.0`; tag, package repository metadata and selected gitlink all resolve to `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`. The downloaded published Client artifact contains both the new generic factory overload and the preserved fixed overload. The consumer CPM override evaluates every EventStore pin to `3.127.0`. Matching Debug/source restore/build passed with zero warnings/errors; final regressions passed 945/945 across Contracts, Server, Architecture, Works, V6, affected projection handoffs and composed index readback. Exactly five additive ledger notes were applied; removing those notes reproduces the original ledger byte-for-byte. Final workflow review and registry availability before Timesheets push remain pending.


## Review Triage Log

All three review layers completed on the final staged diff. The blind layer's size arithmetic is informational; its ten findings and the verification layer's one finding are classified individually below. The edge-case layer returned no findings.

| ID | Finding | Verdict and evidence | Route |
| --- | --- | --- | --- |
| B1 | Selected-source metadata rejection precedes the abort boundary | medium: format/root/single checks at migration lines 39–64 throw before the abort handler when metadata is added to a selected generation during an interrupted migration, leaving Building set. The identical checks already precede the try/catch in baseline `024d3aa24a804c5798017cd84b73041e4fb4dfec`; this is a pre-existing operator corruption-recovery issue. | defer as a separate follow-up; no new runtime defect introduced here |
| B2 | Sealed reconstruction has only a one-tenant success fixture | medium: both sealed fixtures start from one tenant; the existing multi-tenant fixture retries before seal. Resetting the reconstruction builder to the empty root would reject the second tenant without failing current tests. | patch; group with V1 |
| B3 | Sealed fixtures do not cross-product v6 collision/directory layouts | low: the additional fixture is absent, but decoding and source digest enumeration are unchanged shared paths; existing fresh wide/source coverage exercises the enumerator. This rare maintenance-path cross-product adds substantial fixture setup without identifying a changed layout-specific defect. | reject low-value extra scaffolding; no runtime behavior changed for this finding |
| B4 | Damaged sealed retry does not assert that abort releases Building | medium: the new corruption cases assert the selected generation but omit IsBuilding. Bypassing abort only for a sealed stage would keep these cases green while blocking owner delivery; the existing abort assertion covers an unsealed checkpoint. | patch; assert the fence is released after each damaged sealed retry |
| B5 | Cancellation fixture fabricates OCE without canceling the caller | medium: the read seam throws OCE with the still-live test token and accepts any exception, so it does not prove caller cancellation propagation or precise retry state. | patch; cancel a real caller during the awaited read, assert cancellation, then retry |
| B6 | Repeated stalled reads are cut short by an artificial second-read failure | medium: the existing 35-ms-budget cases force a second-read InvalidOperationException. The separate shorter-deadline case stalls only the first read; neither detects extending the overall budget across repeated bounded retries. | patch; exercise issue/use with every read stalled until the one outer deadline |
| B7 | No timeout-then-success readback fixture | medium: the new tests cover immediate valid evidence and timeout failure, but returning false after the first read timeout could pass both while breaking issue/use retry success. | patch; return valid evidence after the first budget expires |
| B8 | Shorter-deadline fixture does not spend budget in status polling | medium: Completed is returned immediately. Restarting the outer budget at readback after status polling would escape this selection while extending a real request. | patch; consume most of the outer budget in status before stalled readback |
| B9 | Standalone configuration requirement is implicit | low: an unconfigured ServiceCollection now gets empty IConfiguration, deliberately avoiding process capture; the README explains the composed host environment provider but does not directly tell standalone consumers to register one. | patch README with the direct configuration requirement; retain approved defaults |
| B10 | Compact verification evidence is not retained outside /tmp | false: the tracked report already retains full/focused test counts, durations, commands, mutation outcomes, full-round assembly hashes and the published artifact/source identities. Hosted SDK runs and the planned consumer CI supply durable artifacts; /tmp references are supplemental raw local logs. | reject on the existing tracked evidence |
| V1 | Nonempty preceding tenant checkpoint lacks sealed-retry verification | medium: pre-verified repository-wide test/route tracing identifies only one-tenant sealed inputs; an empty-root reconstruction mutation would evade them and deny a normal multi-tenant resume. | patch; same root cause as B2, one shared test |

No intent_gap or bad_spec entry remains. Patch entries change only tests and the direct README instruction. The selected five ledger deliveries remain supported; B1 is recorded separately as pre-existing work.


### Review patch verification

The original implementation agent applied all seven patch groups to README and the two affected test classes; production source stayed byte-identical. Root read the complete incremental patch. V1/B2 now exercise three tenants, shared-hash ambiguity, every owner/roster and zero sealed-stage appends. B4 asserts Building is released after each damaged sealed refusal. B5 uses actual caller cancellation during an awaited read and exact cancellation assertions. B6–B8 cover repeated timeouts through the one outer budget, timeout-then-exact-evidence recovery with one submission, and status polling consuming most of the original deadline. B9 explicitly documents caller-owned standalone configuration and its environment provider.

The new multi-tenant test falsifies an empty-root reconstruction mutation: the control compiles and the one selected case fails, then a finally block restores exact production bytes. Matching 3.127.0 Debug/source restore and full solution build passed with zero warnings/errors (build 5.69 seconds). Root post-review selections pass 953/953: Contracts 92, Server 648, Architecture 109, Works 76, V6 24, affected projection paths 3 and composed index readback 1. Earlier complete six-suite evidence remains separately identified; test-only private fixture changes do not require repeating unrelated long local cases. The requested push's package-mode CI runs all six full executables with durable results. Exact local receipts and mutation results are retained in the report. All accepted patch routes are complete; the sole deferred route is the pre-existing B1 source-metadata abort ordering.


### Published-package CI correction

The first authorized main push created `81c6614d2088731855354a4164575d9fc3467841`. Its CI `39950773882`, AppHost configuration job `120404394827`, failed restore with NU1109: the actual SDK 3.127.0 packages require newer Aspire hosting, OpenTelemetry and MCP floors than the unchanged pinned shared catalog. Source-reference compilation had legitimately used the consuming catalog and therefore did not expose these published-package floors. Root extracted the exact eleven requirements from all fourteen official NuGet nuspecs and adds only corresponding root PackageVersion Update entries. No AppHost or ServiceDefaults source/SDK/topology, Works source, shared catalog gitlink or SDK source is edited.

A detached temporary worktree at `/tmp/five-timesheets-package-verification` isolates Release/published-package assets from the main Debug/source graph. Existing Builds and AI.Tools directories are linked read-only; no submodule is initialized or updated. The complete 15-project Release/package restore now passes. Independent prerequisite review and package-mode build/configuration/tests are in progress before the corrective Conventional Commit and main push.


The corrective overrides passed independent prerequisite review with no findings. Exact published-package restore and Release solution build pass for all 15 projects with zero warnings/errors (final build 5.73 seconds); the compiled AppHost configuration-only driver passes all refusal/default-selection checks. The temporary verification setup initially exposed a Git/MinVer rejection of symbolic submodule paths, then a governance test's missing Works checkout fixture; replacing the links with plain copies of ordinary tracked catalog/guidance/Works files resolves both without initialization or changes to the supplied checkouts. Full package-mode Contracts 92, Server 648, Architecture 109 and Works 76 cases pass. The larger full package suites continue alongside hosted CI after the correction is pushed.

Matching Debug/source restore and solution build also pass with the eleven floors (zero warnings/errors, 15.29 seconds); final source selections remain 953/953. No application behavior changed in this corrective commit. The complete accepted implementation and five supported ledger resolutions remain intact; main's hosted CI records the final published-package graph and all six blocking executable results separately.
