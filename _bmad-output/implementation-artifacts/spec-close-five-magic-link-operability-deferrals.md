---
title: 'Close five magic-link operability deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred gaps make fail-closed magic-link failures harder to diagnose or verify: projection transport failures lack safe outcomes, the projection test store ignores component identity and ETags, loader exceptions lack safe outcomes, streamed rebuild contributions bypass candidate validation, and the local index diagnostic ignores epoch readiness.

**Approach:** Add metadata-only internal failure diagnostics, enforce realistic test-store isolation/concurrency, reuse existing contribution validation in streamed rebuilds, and admit local diagnostic snapshots only when the production epoch lifecycle permits reads. Add focused regressions and resolve exactly those five canonical ledger entries using current evidence. Verify, review, commit, and push to main as requested; preserve the existing MCP configuration edits.

</frozen-after-approval>

## Implementation Notes

- Planning: no unresolved product choices, migrations, dependency changes, or deployment actions. The footprint is localized diagnostics, validation, and their tests; existing public responses, authority gates, contracts, and persistence topology stay intact. Use the oneshot route.
- Baseline: Timesheets `068b20cea2af22fa203d3e1eb325b00c1834687a`; EventStore gitlink `5ce5f1d84b88c808cc8a526b9decf149de977e55`; Builds `359fee24aeca4d9eabed4b425d4cfa2520b2adda`; Works `d60c29569de975bd280098e2e2f37406a30cf705`. SDK 10.0.402; both catalogs select EventStore 3.126.0.
- Code map: projection handlers under `src/Hexalith.Timesheets.Projections/{MagicLinks,ActivityTypes}` already distinguish deterministic folds from retryable storage failure. Reuse their optional loggers and `ValidateCandidateEntry`/`Guarded` boundary; do not wrap cursor transport faults as invalid content. JSON-null partitions are legitimate no-issuance sentinels.
- Loader: `EventStoreMagicLinkConfirmationCapabilityStateLoader` already catches expected read failures. Add optional logging without changing constructor compatibility or opaque results. Record constant operation/category and platform trace identity only; never exception objects/messages, raw identifiers, tokens, hashes, state keys, or payloads. Preserve cancellation.
- Test map: `MagicLinkStateProjectionHandlerTests` contains the remaining scripted-store defect; the HTTP fixture already uses the platform's isolated in-memory store. `MagicLinkGlobalIndexEpochTests` has a reusable streamed cursor and real stores. Loader tests have scripted gateway/store faults. Python diagnostic tests run through CI discovery.
- Local diagnostic: `scripts/magic_link_index_selection.py` must check lifecycle, pending journal, offboarding, and conflict readiness both before selecting and when rechecking a candidate. Consult the current EventStore coordinator rather than inventing a new readiness policy. Preserve v6/v7 content validation.
- Verification: matching Debug/source restore/build with warnings as errors; run the six built xUnit executables individually, Python diagnostic tests and existing tooling discovery. Record exact outcomes and blockers separately from local/deployed acceptance.
- Runtime baseline: `aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` started the AppHost; `aspire describe` reported Timesheets Finished, exit 134. Stopped this exact AppHost successfully before edits/builds. This does not qualify current-graph runtime or deployed acceptance; other running AppHosts were left alone.
- Implemented fixed internal projection outcomes (event 1302) and loader operation/failure categories (event 1301), correlated only with the platform trace ID. Optional logging preserves existing construction and opaque public results. Caller cancellation is not logged as a dependency failure.
- Implemented composite component/key storage and conditional ETag writes in the remaining projection fake; the HTTP fake already uses the platform's in-memory store. Added negative concurrency/isolation tests and complete structured-log capture checks.
- Streamed contribution decoding now reuses `Guarded` and `ValidateCandidateEntry` before hashing. Regressions cover 16 malformed inputs, exact cursor-fault propagation, caller cancellation, and a committed JSON-null no-issuance contribution with an empty tenant roster.
- The diagnostic now refuses non-Open epochs, pending delivery, offboarding/handoff, unresolved or malformed conflict/parking evidence, overflow, and conservative capacity exhaustion before/after selected-root reads. It also checks readiness at final v6/v7 candidate rechecks. Raw JSON supplies no ETag; these are conservative local diagnostics, not production admission or deployed proof. The production loader's handling of degraded-only SDK results was not changed.
- Initial verification: restore and warnings-as-errors build pass; 24 Python diagnostic tests and 88 tooling tests pass. Initial .NET runs exposed a blanket no-logger assertion and two fixture expectations; replaced the blanket with read-only and protected-detail checks plus structured runtime log tests, and corrected effective-entry read timing/empty-roster assertions. Final reruns pending.
- Required review: the configured blind hunter ran with a fresh context; no configured review layers were skipped. Reviewed all eight findings and applied seven bounded corrections. Full SDK typed-state/boundary validation remains outside this local readiness diagnostic and is recorded separately below and in the deferred ledger.

- Final gates passed: matching Debug/source restore/build, zero build warnings/errors, all six executables individually (2,394 passed, five intentional opt-in skips, zero failures/errors/not-run), focused projection checks 226/226, Python diagnostic checks 25/25, and tooling discovery 88/88. Full counts/timings and the runtime limitation are recorded in `tests/five-magic-link-operability-handoffs.md`. Only final test documentation comments followed the behavior-changing patches; the final solution rebuild also passed.
- Exactly five canonical ledger entries received resolution annotations, preserving all original ledger lines. One full SDK typed-state/boundary-validation follow-up was added from review. The MCP files remained unchanged and unstaged; no submodule pointers were staged. Remote main matched the inspection baseline when fetched.

## Review Triage Log

- Medium, patched: a throwing diagnostic provider could escape existing failure catches. Both outcome helpers now suppress provider faults; tests exercise a broken provider and retain opaque/retryable results.
- Medium, patched: `deepcopy` preceded the diagnostic depth check. Admission now runs first; a depth-500 regression proves bounded fixed-message refusal before content reads.
- Low, patched: delimiter-based fake ETags admitted a constructed component/key ambiguity. Length-prefixed components and a foreign-ETag regression remove the ambiguity.
- Medium, patched: log tests did not enforce field allowlists. Both now assert exact structured field sets, fixed categories, null exception references, and platform trace identity.
- Low, patched: streamed cancellation was tested only before cursor enumeration. The cursor now cancels immediately before yielding malformed bytes, proving cancellation takes precedence over decoding.
- Low, patched: cursor faults were tested only before any contribution. Fault tests now accumulate valid content first and preserve selected durable state/ETags without yielding writes.
- Medium, patched: loader diagnostics used only legacy fixtures. A production-coordinator epoch read fault now verifies wrapped dependency categorization and opaque results, including provider failure.
- Medium, deferred: complete SDK typed epoch/boundary validation was already absent from local diagnostic admission. Examples include inconsistent copy-on-write anchors, null checkpoint maps, writer-set mismatch, and invalid streaming counters. The bounded readiness check expressly does not claim full typed-state/ETag parity; a separate increment must decide whether to share authoritative validation rather than duplicate all platform rules.
