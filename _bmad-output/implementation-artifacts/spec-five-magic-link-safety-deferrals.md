---
title: 'Harden five magic-link request and recovery boundaries'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c56ac4fdf5f4648f2168555f837856555a15431a'
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete five deferred entries: unknown authority events; total loader read amplification; unexpected public-route faults; unusable ExistingTimeEntry issuance; and invalid selected metadata during interrupted v6 migration recovery. The explicit five-item request supplies combined scope. Close entries only after implementation, verification and review; commit and push to main.

## Boundaries & Constraints

**Always:** Preserve EventStore ownership, fail-closed gates, opaque invalid-link responses, cancellation, recognized event compatibility and legitimate rejection histories. Share history-read allowances across each loader invocation.

**Never:** Change AppHost, ServiceDefaults, siblings, dependencies, stored events, enum values or approval/correction policy. Execute no live migration. Preserve launch FAIL and unfinished deployed acceptance.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Current events, wrappers and rejection no-ops | Existing valid resolution survives |
| Unknown authority event | Opaque denial; no partial authority |
| Exhausted allowance / stalled read | Bounded denial; caller cancellation propagates |
| Unexpected public-handler fault | Fixed opaque HTTP 500, private headers and sanitized Error diagnostic; ordinary invalid links retain 403 |
| ExistingTimeEntry issuance | Reject before token generation; preserve historical reads and existing use denial |
| Invalid metadata on migration retry | Refuse promotion; release this operation's Building fence |
| Fresh malformed source / foreign operation / transient fault | No new fence / no foreign abort / retry remains possible |

## Approved Decisions

The user approved both recommendations and continuing on 2026-10-23: unexpected public-handler faults return a fixed opaque HTTP 500 with sanitized Error diagnostics; ordinary invalid links retain 403 and genuine request cancellation propagates. Reject new ExistingTimeEntry issuance before generating tokens; preserve historical reads, enum compatibility and current use denial. This explicitly narrows Story 3.2's originally described issuance scope without enabling additional approval/correction behavior.

</frozen-after-approval>

## Code Map

All paths are repository-relative. Server = `src/Hexalith.Timesheets.Server`; Host = `src/Hexalith.Timesheets`; Projections = `src/Hexalith.Timesheets.Projections`; test filenames below are under `tests/Hexalith.Timesheets.{Server,Integration,Projections}.Tests` as indicated.

- Server `MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`: Deserialize ignores unknown types; owner scanning requests only terminals; ReadAllEventsAsync follows advancing LatestSequence without total limits. Reuse sanitized `MagicLinkStateReadLog.cs` diagnostics.
- Server `MagicLinks/MagicLinkCommitRejected.cs`: current producer no-op, explicitly admit it. Owner streams also contain ordinary Time Entry events.
- Server `MagicLinks/MagicLinkConfirmationCapability.cs`: ValidateIssue accepts ExistingTimeEntry, ValidateUse rejects it. Adjacent `MagicLinkConfirmationCapabilityCommandService.cs` generates tokens after partial preflight; IssueResolvedAsync handles durable validation.
- Host `Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`: four public delegates lack fault boundaries; retain headers and Denied helper.
- Projections `MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`: selected format/root and selected/direct single-root checks precede the abort try/catch; reuse existing abort/catch-up handling.

## Tasks & Acceptance

- [x] Loader: use complete stream-specific event admission separately from consumed event subsets. Reject unknown types; preserve known stored/domain aliases, rejection no-ops and ordinary owner events during terminal scans.
- [x] Loader, new Server `MagicLinks/MagicLinkStateLoadingOptions.cs`, and Server `Runtime/ServiceCollectionExtensions.cs`: invocation-local limits default to 64 pages, 10,000 raw events, 16 MiB payload and five seconds. Bind `Timesheets:MagicLinks:StateLoading`; validate positive limits capped at 256 pages, 100,000 events, 128 MiB and thirty seconds. Charge repeated reads/events before buffering; bound non-cooperative awaited stream reads. Preserve constructor and caller-registration compatibility.
- [x] Public endpoints: implement decision 1 for all four handlers; shield responses from diagnostic-provider failures. Distinguish genuine caller cancellation from faults.
- [x] Aggregate/service: implement decision 2 consistently before token generation and through durable validation; retain historical event compatibility.
- [x] V6 migration: include resumed metadata checks within operation-owned abort handling. Preserve fresh preflight, completed retry, foreign-operation safety and transient/cancellation recovery.
- [x] Server `EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`, `RuntimeRegistrationTests.cs`, `MagicLinkConfirmationCapabilityCommandServiceTests.cs`, `MagicLinkEventStoreDomainProcessorTests.cs`: exercise aliases/no-ops, unknown suffixes, exact/over limits, repeated streams, growing histories, stalled reads, cancellation and issuance decisions.
- [x] Integration `MagicLinkConfirmationHttpBoundaryTests.cs`, `MagicLinkPersistedSubmissionTests.cs`: verify all four fault responses, log privacy and durable issuance behavior. Projections `MagicLinkV6MigrationTests.cs`: corrupt selected format/root and selected/direct single metadata after sealed/unsealed interruption; assert unchanged selection/contents/ETags and released Building. Include fresh malformed and foreign-operation controls.
- [x] `README.md`, `docs/launch-readiness.md`, ledger and new `_bmad-output/implementation-artifacts/tests/five-magic-link-safety-deferrals.md`: record behavior/options, commands/results and exactly five supported dispositions; preserve historical receipts.

**Acceptance:** Given valid supported histories within budget, when resolved, then current workflows succeed; unknown events or exhausted allowances deny without partial authority. Given handler faults or issuance requests, when processed, then the chosen policies apply consistently. Given interrupted migration metadata corruption, when retried, then selection is preserved and this operation releases Building. Given successful verification/review, when publishing, then the five dispositions and implementation reach main.

## Implementation Notes

- This is the implementation handoff within the root bmad-build run. The root owns workflow routing, independent review, staging, commit validation and publication; the implementation agent completes code/tests/evidence from this spec and reports back. Do not dispatch another build workflow or run remote operations.

- User approval: “apply recommended” followed by “approve and continue” on 2026-10-23. Both behavior decisions are resolved. Existing commit/push authorization remains in force; complete implementation, verification and review before publishing. No further approval is required for the authorized scope.
- All changes must stay in Timesheets. The foreign AppHost baseline blocks the prescribed live startup; record that limitation, continue source/in-process checks and leave foreign processes untouched. Load the owning Hexalith LLM/state/Git guidance through the permitted root-declared AI.Tools checkout. Use the pinned 3.127.0 EventStore source-build property consistently and never change supplied sibling sources or gitlinks.

- Root staged and read the complete baseline-relative diff including all new files at `/tmp/timesheets-five-safety-audit-pln3ymxi.diff`, then inspected the endpoint fitness correction and evidence updates. Frozen intent SHA-256 remains `551ef5329ad6b2fb1d18a44df84d645d88bbee56b02d25e4348aea240ae87081`; removing the five additive resolution notes reproduces the original ledger exactly. All seven matrix rows have executed passing coverage; raw per-case audit: `/tmp/five-safety-root-matrix-audit.json`.
- Implementation tasks are complete. Two broad lanes remain running and are mandatory publication gates: full Projections and the isolated heavy actor-grounded integration method. The complementary integration source fitness failure was corrected and all four endpoint fitness cases passed separately. Independent review may inspect the stable implementation now; completion and push remain blocked until broad receipts are collected, final evidence is reconciled, and review is complete.

- Final root verification after B4/B5/B8 passed all six unfiltered executables: 2,520 total, 2,515 passed, zero failed/pending/other and five existing Integration skips. Build: zero warnings/errors. Final structured receipt: `tests/five-magic-link-safety-results.json`; report: `tests/five-magic-link-safety-deferrals.md`. All frozen matrix rows have executed passing coverage. Exactly five original entries are resolved, with three separately recorded pre-existing follow-ups. Installed commitlint accepted the intended commit message; origin/main matched the starting baseline at the pre-publication fetch. User-authorized commit/push follows these completed gates.

## Spec Change Log

## Review Triage Log

| ID | Finding | Verdict and evidence | Route |
| --- | --- | --- | --- |
| B1 | Pending journal cannot fold against corrupt selected metadata during abort | medium: the normal fold refuses corrupt format/root/single metadata and SDK CatchUp leaves pending work in Aborting. The same fold and abort path are present in the baseline; metadata corruption after Begin already reaches it through the existing old-format/root checks. Safely repairing selected metadata or bypassing its integrity validation is outside this fix's guarantees. | defer existing corrupt-source/journal recovery; qualify the empty-journal release proof |
| B2 | Interrupted AbortAsync cannot resume through MigrateAsync | medium: baseline and current MigrateAsync have no Aborting branch, while SDK BeginAsync rejects that phase. A transient cleanup failure already stranded the baseline after any existing deterministic abort. | defer existing abort-resume protocol gap; group with E1 |
| B3 | Candidate/catalog awaits have no history deadline | maybe-false, medium if confirmed in the deployed transport: those awaits predate this change and remain outside the explicitly documented raw authority-history allowance. A non-cooperative read-model dependency can stall; actual Dapr transport deadline evidence would settle production reachability. | defer separate read-model deadline qualification; no whole-request deadline claim |
| B4 | Deadline/cancellation can expire during folds before authority publication | medium: current checks run only around stream awaits; normalization/deserialization/folding can consume the remaining allowance, and the final catalog boundary can return after request cancellation. Rechecking the existing invocation deadline/cancellation at publication prevents late authority. | patch existing budget checks at fold/publication boundaries with fail-closed timeout and propagated caller cancellation |
| B5 | Validated options retain a mutable caller object | low: Options.Create callers can mutate the retained object after validation and exceed caps. Copying then validating the private settings is a direct correction with no new public surface. | patch defensive validated snapshot |
| B6 | Unknown event arriving only in the second Time Entry read lacks a dedicated fixture | low: the extra scheduling fixture is absent, but both owner reads invoke the same complete admission set and strict decoder; the current suffix tests execute that shared rejection boundary. No implementation defect is identified. | reject low-value additional timing fixture |
| B7 | Stored wrapper alias cross-product coverage is incomplete | low: all eight source mappings were checked and are correct; existing alias, stored terminal and replay tests plus the new issuance/recorded controls exercise the common decoder. A 24-form lifecycle fixture matrix adds scaffolding without demonstrating a mapping defect. | reject low-value mirrored mapping coverage |
| B8 | Exact two-read timing assertion can flake under scheduler pauses | medium: valid total-deadline cancellation can occur before two reads finish, making completedReads==2 fail a correct result. Fewer than three successful reads plus the opaque-denial assertion preserves the reset-per-read falsification without requiring one precise schedule. | patch the over-specific assertion; retain the mutation control |
| B9 | Fault tests lack whole-response equality across all sources | low: selected fields are checked and all paths use the sole constant Faulted response; both loader/service sources and privacy checks execute. No present route-specific response discrepancy is identified. | reject extra snapshot normalization scaffolding |
| B10 | Budget limit failures use coarse existing diagnostic categories | low: configured page limits share timeout and event/byte limits share integrity categories. The diagnostics remain fixed/private and the public refusal is correct; a new exception taxonomy would add complexity for rare tuning cases. | reject optional diagnostic taxonomy expansion |
| E1 | Transient abort cleanup leaves an unresumable Aborting phase | medium: same underlying baseline protocol gap as B2; SDK AbortAsync is resumable, but MigrateAsync calls Begin instead of exposing an authorized abort continuation. | defer with B2, preserving operation/inventory safety |

Review patches B4/B5/B8 are applied. The focused restored loader run passed 134/134 with zero skips; temporary deadline-reset and mutable-options mutants both failed, and original production hashes were restored. Final root build passed with zero warnings/errors; all six final suite invocations are in progress.

Verification-gap layer: no gaps reported. All three layers completed before triage. The blind finding-floor arithmetic is informational, not an implementation defect.

## Verification

- Baseline is clean main at the full revision above; SDK 10.0.402. Guidance, architecture boundaries, build/test configuration and workflows were inspected. No root .gitattributes exists.
- Run focused regressions and falsifying controls, then matching Debug/source solution restore/build with warnings as errors and all six test executables individually. Use `-p:HexalithEventStoreVersion=3.127.0` consistently while Works forwards a different default. Record exact blockers separately.
- Live qualification is excluded: the isolated driver refuses any running AppHost; a foreign Tenants AppHost is active. Do not stop it or infer deployed acceptance.
- Finish independent bmad-build review, validate Conventional Commit text with commitlint, fetch/inspect divergence, then push without force. Commit/push authorization remains in force.
