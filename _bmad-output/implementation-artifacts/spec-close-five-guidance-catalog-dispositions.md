---
title: 'Close five completed guidance and catalog dispositions'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five completed deferred guidance and package-catalog follow-ups by adding explicit resolution notes and a source-backed verification report. Three historical review bullets already contain prose or inline resolutions; two guidance carry-forwards duplicate delivered synchronization. Preserve their original text, distinguish these five ledger dispositions from new implementations, independently review and verify the reconciliation, then commit and push to main. Preserve the user's existing deployment-authority spec edit and exclude it from the commit. Runtime, product-policy and deployed acceptance work remains open.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: main at `dd0b6796efe0935f94a2c1feb9570d2af8948ec8`. The only pre-existing change is the user's additive edit to spec-3-6-deployment-authority-runtime-continuation.md. The user explicitly approved proceeding while preserving and excluding it. Read root guidance, the declared AI.Tools LLM/state/Git instructions, editor/build/package/test configuration, architecture version and acceptance boundaries, canonical guidance/governance specs and the existing architecture fitness checks. No tracked .gitattributes applies.
- This is one small documentation reconciliation: no unresolved intent gaps, new callable surface, migration or dependency/runtime change. The user authorized commit/push on main. Use the oneshot route and keep this spec in-progress until review/finalization. No epic story or sprint-status transition applies.
- Selected entries: the September 12 Story 5.3 managed-context bullet; its launch-readiness imported-catalog bullet; the September 12 Story 5.2 obsolete SDK-guidance bullet; diagnostic-redaction's generic guidance/launch reconciliation carry-forward; restart-timeout's synchronized-guidance carry-forward. The three prose-resolved bullets receive the same resolution_note convention as surrounding entries; their earlier resolutions remain intact.
- Code map: AGENTS.md, CLAUDE.md and Copilot instructions already agree on SDK 10.0.402, Works checkout/test guidance, projection wiring and unfinished release acceptance. global.json, README.md and the HTTP host corroborate those facts. docs/launch-readiness.md and architecture.md match the imported Builds catalog for Dapr 1.18.10, Aspire 13.6.1 and Fluent UI V5 5.0.1. Existing LaunchReadinessTests checks guidance equality, configuration/catalog alignment, Works ownership and unfinished acceptance.
- Provenance: Story 5.1 revision `f82c446f62d9645735989c8fa2c12257dff4a8c9` delivered SDK/posture/wiring guidance; spec-close-five-submodule-governance-deferrals records Works governance completion; spec-close-five-agent-guidance-carry-forwards already covers the restart carry-forward. Current imported Builds gitlink and checkout both equal `359fee24aeca4d9eabed4b425d4cfa2520b2adda`.
- Planned footprint: this spec, exactly five additive resolution_note lines in deferred-work.md, and tests/five-guidance-catalog-dispositions.md. Preserve all original ledger bytes, entry points, story/launch classifications, archived evidence, source/configuration and gitlinks.
- Verification: restore/build ArchitectureTests on the matching Debug/source graph with warnings as errors, then run its complete existing executable. Record exact commands and outcomes; verify five-note placement, baseline-byte preservation, source/link/scope checks and the user's edit hash. No new tests or AppHost lifecycle operation is needed for a ledger-only reconciliation. Complete the configured context-free Blind Hunter review before finalizing, validate a Conventional Commit, fetch/inspect main divergence and push without force.
- Implemented exactly five adjacent resolution notes and the linked evidence report. Removing those five lines reconstructs the original ledger bytes. The three earlier prose/inline resolutions remain untouched; entry points and all source/configuration inputs are unchanged. The user's existing spec edit retains SHA-256 `44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06` and will be excluded from staging.
- Matching Debug/source ArchitectureTests restore and build exited 0 with zero warnings/errors (build 2.82 s). The complete existing executable passed 109/109, zero failures/errors/skips/not-run (9.372 s). No other suites, new package audit or runtime/deployed operation was needed. Source provenance, five-note preservation and link checks pass; independent review is next.
- The expanded committed-catalog check initially failed on the dependency's CRLF checkout versus its LF Git object. Normalizing only those line endings confirms identical catalog text; the report's reproduction now handles that checkout convention. Original ledger and owned guidance/source preservation checks remain byte-exact.
- Independent context-free Blind Hunter reviewed approximately 16.966 kB, computed a finding floor of five and returned five documentation/verification improvements. Each was checked against the report, actual fitness methods and worktree state and patched. The source/configuration and existing tests are unchanged, so the 109 passing architecture cases remain applicable. The exact expanded reproduction command passes, including all section anchors, original-ledger preservation, existing input preservation, three-file scope and new-artifact whitespace. Nothing newly deferred.
- Final reviewer confirmation: all five original findings are addressed; the documented reproduction exits 0 and no original finding remains unresolved. Commitlint passes the Conventional Commit message. A nonrecursive origin/main fetch shows zero divergence at the full investigation revision. Final staging is restricted to the three owned documentation paths, with complete staged whitespace and user-edit preservation checks before the explicitly requested commit/push.

## Review Triage Log

- BH1 — low / patched: byte-exact checkout/Git comparisons are intentionally local to Timesheets' LF checkout convention. Documented that prerequisite separately from the dependency's existing CRLF conversion; no portable byte-preservation claim remains.
- BH2 — low / patched: git diff --check excludes untracked artifacts. The report's executable check now validates both new documents' line endings, final newline and trailing whitespace; the final staged whitespace check covers the complete commit.
- BH3 — low / patched: added baseline-relative enumeration of tracked and untracked paths, requiring exactly the three owned files after the explicitly preserved user path is excluded. The independent local hash check still verifies the user's bytes.
- BH4 — low / patched: distinguished managed guidance-string assertions from actual host-registration source checks and MSBuild Works-root evaluation; cited DependencyDirectionTests and WorksCheckoutGovernanceTests, all included in the passing complete architecture suite. No runtime behavior is inferred.
- BH5 — low / patched: added a direct link to this owning build spec and section links for each of the five ledger dispositions. The executable report-link check now validates both paths and heading anchors.
