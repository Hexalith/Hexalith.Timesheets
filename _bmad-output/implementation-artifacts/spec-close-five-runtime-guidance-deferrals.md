---
title: 'Close five deferred verification and runtime guidance items'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred guidance goals with source-backed documentation and verification: ServiceDefaults ownership handover; FrontComposer UI revisit policy; fail-closed kernel wording; explicit Debug/source restore/build guidance; and current Microsoft.Testing.Platform runner/workflow guidance. Preserve dated evidence and distinguish corrections from previously delivered behavior. Keep the three managed agent entry points synchronized. Commit and push the completed changes to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: Timesheets `283cac642790b3da85467ca43c3252b3b2416319`, clean main. Root `.gitmodules` declares the baseline instructions dependency; dependencies remain read-only.
- Small documentation-only change: no intent gaps, new callable surface, deployment, or state mutation. The user explicitly requested five deferred items and the eventual push. Use the oneshot route for this bounded guidance reconciliation.
- Selected ledger summaries: “Record the Hexalith.Timesheets.ServiceDefaults ownership handover to the EventStore SDK in docs/launch-readiness.md”; “Correct the launch-readiness UI revisit condition to match the FrontComposer policy”; “Reconcile the agent guidance ... fail-closed ... with the spec-mandated EventStore loader registration”; “Fix the agent entry points' Running and verifying flow ...”; “Correct the agent entry points and the dotnet test runner entry ...”.
- UI policy and explicit build graph instructions were already delivered; verify and reconcile their ledger entries without claiming new implementation. README and managed entry points still contain obsolete VSTest rejection statements. Kernel registration intentionally binds a concrete EventStore loader while authorization and unavailable sibling/read-model defaults remain closed. Program.cs delegates shared observability, health, discovery, and resilience to AddEventStoreDomainService; local ServiceDefaults remains in the architecture-required scaffold.
- Planned edits: README.md (verified individual MTP command); docs/ci-cd.md (same runner); docs/launch-readiness.md (handover); AGENTS.md, CLAUDE.md, .github/copilot-instructions.md (managed kernel/runner wording only); deferred-work.md (five goal dispositions and canonical duplicate links). No runtime code, topology, dependencies, classification, story status, approvals, or archived receipts change.
- Verification: matching Debug/source restore/build; individual test suites through dotnet test --project with the same configuration and source-reference property; existing architecture checks for normalized guidance, UI ownership, and host SDK ownership. Review with the required independent blind hunter and record all dispositions here.

- Implemented the planned six-document guidance changes and exactly five canonical resolution notes. Original ledger text, shared entry-point baseline, and historical launch receipts remain byte-for-byte unchanged. All three entry points remain byte-identical. Matching Debug/source restore succeeded; full solution build passed with zero warnings/errors (16.24 s). Architecture 103/103, Contracts 92/92, and Server 558/558 have passed through the documented MTP command; remaining suites are running.

- Independent Blind Hunter review computed 16.21 kB and a finding floor of five. All five findings were checked against the ledger, source registrations, and managed-context provenance. Added five duplicate supersession notes pointing at the five canonical dispositions; these do not count as additional deferred goals closed.
- The generated managed-context stamp retains the original context-generation baseline. This spec and its test summary record the October 21 limited edits at the full investigation revision; this run did not perform a full bmad-project-context refresh.

- Projections completed through the documented MTP runner: 549 passed, zero failed/skipped; summary duration 9m 23.038s. Integration and Works remain in the same verification run.

- Final verification: all six individual MTP commands returned exit 0 from the matching Debug/source build: 2203 tests, 2198 passed, 5 intentional skips, zero failures. Exact per-suite counts/durations are in `tests/five-runtime-guidance-handoffs.md`. No performance/live-Dapr opt-in was enabled.
- Final preservation and scope checks passed: five canonical resolution notes, five predecessor supersession links, one separately deferred managed-guidance clarity suggestion, original ledger evidence and historical receipts intact, byte-identical agent entry points, and only the nine planned documents changed. The Conventional Commit message passed commitlint.

## Review Triage Log

- **low, patched:** the earlier combined documentation entry still looked open for ServiceDefaults decision 5. Added a supersession note pointing at the canonical handover; kept its original evidence.
- **low, patched:** the second forbidden-UI entry lacked a pointer to the already-delivered NFR13 policy. Added its canonical supersession note; no UI or launch classification changed.
- **low, patched:** predecessor SDK/build/runner entries could be selected again despite the current dispositions. Added three canonical supersession notes; preserved the separate Works propagation and forwarding follow-ups.
- **low, deferred:** explicitly naming DaprClient, IReadModelStore and IReadModelBatchStore would make the managed standalone-kernel guidance more actionable. The current dependency requirement is correct, and existing resolution tests supply the read seams. The oneshot workflow classifies review suggestions editing managed agent instructions as deferred; recorded a separate clarity follow-up without changing bindings.
- **false, rejected:** the original managed-context provenance comment is not a receipt for this limited update. It identifies the October 20 context-generation baseline managed by bmad-project-context. This spec and the new verification summary explicitly identify the October 21 worktree baseline and narrow scope; a full context refresh was neither performed nor claimed.
