---
title: 'Close five historical agent-guidance carry-forwards'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five historical deferred guidance requests whose corrections are already delivered: the unwired-index diagnosis, the CLAUDE SDK pin, the overall launch posture, the combined managed-context refresh, and the checkpoint-scope guidance carry-forward. Add evidence-backed dispositions so these stale requests stop being selected as implementation work. Preserve their original evidence and distinguish completed guidance from unfinished runtime and deployed acceptance. Commit and push the completed reconciliation to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `2acdce4ec2d347bdb8be9e170b176c8d85a72937`. Read the declared AI.Tools baseline, state and Git instructions; repository configuration, architecture acceptance boundary, build guidance, existing fitness checks, and earlier runtime/tooling guidance dispositions.
- One small documentation goal, with no intent gaps, migration, new callable surface, or runtime change. The requested commit/push is authorized. Use the oneshot route; status stays in-progress until review and finalization.
- Selected ledger summaries (planning-time lines 237, 478, 514, 553, 747): “Refresh the agent-context files that still describe the token-hash index as unwired”; “Refresh the CLAUDE.md `timesheets` block's SDK pin”; “Refresh the launch posture in the agent entry points' `timesheets` block”; “Refresh the managed repository agent context for the current launch posture and magic-link projection wiring”; “Refresh stale repository guidance in a separate increment” under projection checkpoint scope selection.
- Current AGENTS.md, CLAUDE.md, and .github/copilot-instructions.md are synchronized and already name SDK 10.0.402, overall FAIL, the registered projection assembly and EventStore processing, and unfinished deployed/current-graph acceptance. Program.cs supplies the registration; global.json supplies the SDK pin. Earlier delivered guidance changes are recorded in spec-close-five-runtime-guidance-deferrals.md and spec-close-five-tooling-diagnostic-deferrals.md.
- Planned edits: exactly five resolution notes in deferred-work.md, this spec, and tests/five-agent-guidance-carry-forwards.md with evidence and verification. No agent-entry-point regeneration or managed-context provenance update is needed; original evidence, story status, launch classifications, and archived receipts stay intact.
- Verification: rebuild ArchitectureTests with matching Debug/source restore and warnings-as-errors, then run its existing suite; verify entry-point equality, host registration, ledger preservation, and changed-file scope. No new tests are needed for this documentation-only correction. Obtain the independent oneshot Blind Hunter review, triage every finding, validate a Conventional Commit, fetch/inspect main divergence, and push without force.
- Runtime preflight: aspire ps --format Json found foreign Tenants and platform AppHosts. No runtime was started or stopped; documentation verification supplies no live acceptance evidence.
- Implemented exactly five dated ledger dispositions and the linked evidence summary. A read-only preservation check proves that removing those five added lines reproduces the original ledger bytes, all entry-point bytes match HEAD and each other, the executable SDK and host registrations corroborate the guidance, current launch classifications retain FAIL/CONCERNS, and every evidence-summary link resolves.
- Matching Debug/source ArchitectureTests restore and warnings-as-errors build exited 0 (zero warnings/errors). The complete existing architecture suite passed 109/109, zero failures/errors/skips/not-run, in 11.273 s. `git diff --check` passed. Narrow verification is appropriate to the ledger-only change; other suites were not rerun. Independent review and commit/push remain pending.
- Independent Blind Hunter reviewed 10.612 kB (finding floor four) and returned four traceability findings. All were verified and patched through the added notes/evidence report. Direct Git inspection identifies Story 5.1 commit `f82c446f62d9645735989c8fa2c12257dff4a8c9` as the original SDK/posture/wiring guidance correction. The report now carries that provenance and a baseline-pinned reproduction command that works after commit.
- Final post-review checks passed: the exact documented reproduction command, original-ledger preservation, unchanged/identical entry points, valid report links, and exactly the three planned documentation files. The existing 109/109 architecture result remains applicable; review patches change documentation only. `git diff --check` and commitlint passed. Fetch confirmed origin/main matched the full starting revision with zero divergence. All four review findings are addressed and nothing is newly deferred; the reconciliation is finalized for the requested commit and push.

## Review Triage Log

- **low, patched:** three historical references still call the canonical 2026-09-16 guidance request open. Its added resolution note and the evidence summary now explicitly supersede that wording; original evidence remains unchanged.
- **low, patched:** the matching restart-timeout scope-selection carry-forward could select the same completed guidance again. The selected checkpoint carry-forward disposition and evidence summary explicitly cover its identical SDK/wiring/posture facts without adding a sixth note or inventing another implementation goal.
- **low, patched:** earlier provenance named later refinements rather than the original delivery. Git show confirms the full Story 5.1 revision above; the evidence summary now records it and the exact inspection command.
- **low, patched:** the additional preservation check lacked a reproducible command. The evidence summary now contains its complete read-only Python invocation, pinned to the full starting revision so post-commit runs still verify the original ledger bytes and unchanged entry points. Nothing newly deferred.
