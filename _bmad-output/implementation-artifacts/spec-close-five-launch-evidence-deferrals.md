---
title: 'Close five deferred launch-evidence handoffs'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred Story 5.2 review entries still send maintainers toward obsolete launch classifications, source-build instructions, version ground truth, and supposedly current verification/pointer claims. Story 5.1 already corrected much of the launch record, but the remaining documentation handoffs and ledger reconciliation are incomplete.

**Approach:** Complete these five documentation follow-ups using the existing dated Story 5.1/5.2 evidence. Carry the conditional catalog EventStore-version override into CI/CD guidance and the release gates; reconcile the Epic 5 classification context; point Story 5.1 and the September proposal at the accepted later package baseline; distinguish selected dated suite evidence from newer work; and explicitly scope obsolete pointer claims to historical receipts. Annotate exactly five canonical ledger entries with the completed work and evidence. Preserve historical receipts, accepted decisions, story statuses, dependency pointers, and the FAIL release verdict. Verify the affected documentation with existing architecture checks, obtain independent review, then commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Baseline: clean `main` at `f62eb4a8404f7f497c57e0189f81b7f7fbeb565b`. Root baseline, state/Git instructions, editor/build/package configuration, architecture boundaries, existing launch guards and dated evidence were inspected. No nested skill or submodule was loaded or initialized.
- Route facts: no unresolved user-facing decision, migration, deployed-data operation, or new API; a bounded documentation correction across five existing guidance/record files plus this spec and the ledger. Commit and push are explicitly authorized. No runtime startup or full-suite rerun is required for this documentation-only change.
- Code map: `docs/ci-cd.md` owns local/CI dependency-mode instructions; `docs/launch-readiness.md` separates the October 20 selected gates from preserved historical receipts; `epic-5-context.md` still describes the superseded `blocked` classification vocabulary. Story 5.1's active scope and the September proposal need explicit pointers to later accepted Story 5.2 versions. Reuse `LaunchReadinessTests`, its selected XML/count provenance, and existing source configuration; do not replace historical evidence with new claims.
- Current Git observations (not runtime evidence): Builds `17d0c6ffec5fb3c8d8058e67cb85644460cb5256`, EventStore `ae917d3e4dbab20b8982c5222a13611c3e3faf35`, Works `e1b3518de80ba747d0eaf50863e845b8ae8040d8`. Both catalog and Works defaults currently select EventStore `3.126.0`; SDK is `10.0.402` and AppHost SDK is `13.6.1`. These observations do not refresh October 20 verification or current-graph runtime acceptance.
- Implemented the five handoffs across the mapped documentation, with exactly five canonical ledger resolution notes. Existing README policy and Story 5.1 classification/count fixes are credited rather than presented as new implementation. Added links resolve to existing records and acceptance evidence; original proposal and historical story/launch bodies remain byte-identical.
- Verification: matching Debug/source-reference Architecture project restore passed; build passed with zero warnings/errors (0.97 s). The built executable's `-class '*LaunchReadinessTests'` selection passed 16/16, zero failures/skips (0.386 s). The preservation/link probe passed: five additions only in the ledger, six new relative links/anchors resolve, historical bodies unchanged, and no guidance, gitlink or archived evidence changes. Its first run detected two added blank lines in the ledger; those were removed before the passing recheck.
- Independent review produced five bounded documentation corrections, all patched: use the selected Works checkout/imported catalog for version comparison, link newer October 21 verification, retain reproducible commands/probes, clarify that the earlier “no guidance” preservation claim means no agent-entry-point changes, and date/link the scoped story acceptances. CI/CD and Epic 5 guidance intentionally changed. The [verification record](tests/five-launch-evidence-handoffs.md) retains the exact commands and read-only probe; historical evidence and all five original ledger entries remain intact.
- Post-review verification passed: full Architecture 103/103 (12.514 s), including all 16 readiness checks; zero errors/failures/skips/not-run. The retained probe passed with five ledger additions, preserved historical bodies, the eight-document scope, 12 resolving new local links, and matching configuration/Git observations. `git diff --check` and commitlint passed. No review finding was deferred; the source-build forwarding issue and runtime/deployed release gates retain their existing owners and statuses.
- The independent reviewer rechecked all five patches as resolved and independently reran the retained probe successfully. No configured review layer was skipped and no unresolved finding remains.

## Review Triage Log

- BH1 — medium / patch: hardcoded references paths could compare the wrong defaults for explicit roots or sibling fallback. Guidance now selects the actual Works checkout and imported catalog.
- BH2 — low / patch: the newer-verification claim lacked direct supporting links. The dated reconciliation now links all three October 21 closure records without changing the selected October 20 gates.
- BH3 — low / patch: verification notes omitted complete reproduction commands and the probe. Both are retained in the verification record.
- BH4 — low / patch: “no guidance changes” overstated the probe's scope. The clarification and retained probe explicitly identify unchanged agent entry points; CI/CD and Epic 5 guidance did change.
- BH5 — low / patch: the standalone epic context described scoped acceptance without dated evidence links. It now dates the reconciliation and links both existing acceptance records, retaining unfinished release gates.
