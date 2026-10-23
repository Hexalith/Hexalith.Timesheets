---
title: 'Consolidate five duplicate operational follow-ups'
type: 'chore'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five overlapping deferred-work entries into their existing, specific
operational follow-ups: the pre-handler history/bootstrap question, repeated
cross-request use reconciliation, repeated deployed read-model wiring, and two
generic storage/migration scope-selection entries. Add explicit successor
references and a verification report while preserving each original observation.
These are five tracking dispositions; their underlying deployment, migration,
storage and retry requirements remain open in the canonical entries. Preserve
release FAIL, package CONCERNS, Story 3.6/3.7 status, historical evidence and the
user's deployment-authority spec content. Verify, independently review, commit
this increment and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Read the rendered bmad-build workflow, root instructions, AI.Tools LLM/state/Git
  baselines, editor/build/package/test configuration, architecture acceptance
  boundary and existing launch fitness tests. No root .gitattributes exists.
  The workflow's dirty-tree checkpoint was resolved by the user's permission to
  preserve and exclude the existing deployment-authority edit. It was subsequently
  committed separately as `5a82cdc7f457347b9cd89f1947678e53dea58404`, now the
  investigation baseline. Its SHA-256 remains
  `44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06`.
- One small documentation consolidation, with no unresolved product choice,
  dependency change, runtime implementation, new API or operational mutation.
  Commit/push is explicitly authorized. Select oneshot; no epic story or sprint
  transition applies. Keep this spec in-progress until workflow finalization.
- Code map: deferred-work.md retains the canonical October 10 cross-request-use
  requirement, October 11 deployed inventory/rebuild requirement and owner
  continuity requirement, October 13 direct-shard/sealed-v6/operator/storage
  follow-ups, and the unfenced-catalog-rebuild question. The selected five
  entries repeat or wrap those requests; none supplies evidence of their delivery.
- Supporting sources: docs/deployed-magic-link-2026-10-17.md separates prepared
  hosting artifacts from installed wiring and lists immutable inventory,
  authority, continuity and maintenance prerequisites; the user's October 22
  continuation records their later blocked posture. The current index handler
  refuses selected/direct legacy roots instead of auto-populating them.
  IMagicLinkV1VerifiedInventoryProvider and its unavailable default preserve
  fail-closed admission. Existing local journeys use manual mapped delivery.
- Read-only investigation found that Time Entry actor revalidation does not
  prove cross-owner catalog linearizability, and that the catalog still uses the
  non-epoch shared rebuild contract. Leave both genuine requirements open.
  Historical October 4 port-conflict evidence cannot diagnose October 3's failure.
- Footprint: this spec, exactly five additive resolution_note lines in
  deferred-work.md, and tests/five-operational-followups.md. Preserve all original
  ledger bytes, canonical open tasks, source/configuration, entry points,
  story/sprint statuses, evidence and submodule pointers.
- Verification: matching Debug/source ArchitectureTests restore/build with
  warnings as errors, the complete existing architecture executable, and a
  baseline-relative preservation/scope/link check. No new tests or AppHost
  operation is needed for documentation-only tracking consolidation. Run the
  configured context-free Blind Hunter, triage all findings, validate a
  Conventional Commit, fetch/inspect main divergence and push without force.
- Implemented exactly five successor notes and the report. Independent
  investigation confirmed the two exact duplicates and required retaining
  pre-handler histories with no existing rows, both index and catalog population,
  catalog-key/fencing risks and subscriber delivery in the successor coverage.
  All those requirements remain explicitly open. The canonical tasks and every
  original ledger byte are preserved; only the three planned documents differ
  from the investigation baseline.
- Matching Debug/source architecture restore/build passed, zero warnings/errors
  (build 1.93 s). The complete architecture executable passed 109/109 with zero
  failures/errors/skips/not-run in 16.876 s. Preservation, user-content hash,
  scope, LF/whitespace and unique-heading local-link checks pass. Independent
  review is next; no runtime or deployed operation was needed.
- Blind Hunter returned five findings. Added one non-closing followup_note to
  the canonical inventory task so open-task selection retains the transferred
  coverage and runbook requirement. The report now identifies each remaining
  canonical task by exact source/summary pair, checks index bytes in optional
  commit-readiness mode and retains the observed command output. The five
  supersession notes and the one scope addendum can be removed to reconstruct
  the original ledger byte for byte. These review corrections change only the
  three planned documents and preserve all runtime/acceptance gates.

- The independent reviewer rechecked all five corrections and confirmed each
  resolved. Its default reproduction, unique links, all nine exact canonical
  source/summary identities and whitespace checks pass. No new work is deferred.
  Commitlint passed the Conventional Commit, and the nonrecursive main fetch
  showed no divergence from the investigation baseline. Final staging is
  restricted to the three owned documents and requires VERIFY_STAGED=1 before
  commit; the user's separately committed spec and all gitlinks remain intact.

## Review Triage Log

- BH1 — medium / patched: the unchanged canonical inventory task could hide
  bootstrap scope from an open-task-only reader. Added a reciprocal non-closing
  followup_note with no-row history coverage, both projections before traffic
  and a reference to the report. The original evidence remains intact.
- BH2 — medium / patched: the transferred acceptance omitted the original
  runbook deliverable. Both the successor note and canonical addendum now
  require documenting the chosen replay/rebuild/migration procedure and its
  verification, without claiming a qualified runbook already exists.
- BH3 — low / patched: broad section links required interpreting which tasks
  survive. The coverage table now lists each exact source_spec/summary pair;
  the pre-format catalog-key bullet is identified by original text and section.
- BH4 — medium / patched: the earlier check allowed partial or stale staged
  content. VERIFY_STAGED=1 now requires the complete owned file set and exact
  index/worktree byte equality, while the default mode supports pre-stage and
  post-commit inspection. Final publication must use the stricter mode.
- BH5 — low / patched: commands/timings alone did not retain their output.
  Added a linked compact receipt in the report with inspected source identity,
  SDK version, exact observed restore/build/test summary and check output.
  No additional runtime execution or acceptance is implied.
