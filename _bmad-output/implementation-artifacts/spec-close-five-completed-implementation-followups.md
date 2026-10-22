---
title: 'Close five completed implementation follow-ups'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five deferred-work entries whose local implementations already exist: durable atomic magic-link confirmation/adjustment, checkpoint tenant isolation, and three carry-forwards for BMAD hook and tooling maintenance. Verify current behavior, preserve original evidence, and add explicit resolution notes linked to a reproducible evidence report. The three tooling entries overlap the same completed hook fixes; they are three ledger entries, not three additional implementations. Distinguish local verification from deployed acceptance and leave genuinely unfinished storage, migration, runtime and release gates open. Review, commit and push the completed reconciliation to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `622ee40e0df72404f60ee57da06e2963e665749d`. Read AGENTS.md, the declared AI.Tools LLM/state/Git baseline, editor/build/package/test configuration, architecture boundaries, canonical implementation specs and existing regression tests. No tracked .gitattributes applies.
- This is a small documentation reconciliation with no intent gaps, new API, dependency update, domain-data operation or runtime edit. Use oneshot and keep this spec in-progress until verification and independent review finish. User authorization includes commit/push to main.
- Selected exact summaries: “Implement durable, atomic magic-link confirmation and adjustment persistence for unfinished Story 3.3/3.4 behavior”; “The deferred cross-tenant checkpoint freshness gap is closed across Timesheets projection folds”; “Address deferred BMAD hook and test-coverage maintenance in separate increments”; “Harden the deferred BMAD hooks in a separate increment”; “Harden BMAD hooks and remove obsolete hook references in a separate increment”. The first already has a dated supersession field; the checkpoint entry already describes a completed fix. All five lack a standard resolution_note.
- Code map: MagicLinkDurableSubmissionService requires completed status and exact stored-batch readback; MagicLinkEventStoreDomainProcessor emits capability use and its Time Entry effect into one owner stream; MagicLinkPersistedSubmissionTests and MagicLinkEventStoreActorPersistenceTests check persisted batches, replay/concurrency and actor recreation. TimesheetsProjectionCheckpoint.ValidateTenant and the seven affected projection test classes cover ordinal tenant admission, non-enumeration, privacy, incremental prefix preservation and matching-tenant behavior.
- Tooling map: spec-make-codex-hooks-local, spec-pin-bmad-loop-malformed-input-behavior and spec-confine-bmad-loop-event-filenames record completed canonical fixes; spec-close-five-tooling-diagnostic-deferrals records override-regression CI coverage. scripts/tests discovery exercises the hook, installed persona suites and mirrors, and release tooling. Preserve installer-owned skills and ignored local hook configuration.
- Scope: this spec, five ledger resolution notes, an evidence report and focused verification receipts only. Keep original evidence and earlier resolution fields. No AppHost lifecycle operation is needed for documentation-only changes; no runtime or deployed qualification is inferred.
- Verification plan: matching Debug/source solution restore and warnings-as-errors build, focused durable-service and persisted actor/HTTP tests, all seven affected checkpoint projection classes, existing Python tooling discovery, five-entry scope/preservation and whitespace checks. Catalog and Works both currently select EventStore 3.126.0, so no historical override is needed. Obtain canonical revision identifiers from Git and keep current receipts separate from historical measurements.
- Implemented exactly five resolution_note additions, preserving every original summary/evidence/earlier resolution field. Added tests/five-completed-implementation-followups.md and current compressed receipts with canonical baseline/gitlinks, input hashes, exact invocations and results. The generic tooling carry-forward does not close independent PascalCase naming or future-spec lifecycle wording.
- Current verification: matching Debug/source restore and solution build passed; build had zero warnings/errors. All six executables ran individually: Architecture 109, Contracts 92, Server 624 and Works 76 unfiltered; seven affected projection classes 174 and nine persisted HTTP/actor methods 24 filtered. Total 1,099 selected .NET cases passed with no failures/errors/skips/not-run. Python scripts/tests discovery passed 88; its wrappers also execute 86 nested persona cases, counted separately. Static hook ownership/setup/CI checks passed. No runtime was started or stopped.
- Review follow-up narrows the actor-recreation claim to event history/replay, adds explicit source/dependency/count/static-check verification to reproduction, and retains commands/results for five-note ledger preservation and changed-text whitespace. The actual initial deprecated -xml invocations remain untouched; new execution/reproduction uses -result-xml. Python interpreter/platform and all 13 clean dependency checkout revisions are recorded.
- Added current receipts for 61 existing negative integration cases covering tampered use/effect payloads, prior values and command attribution; all pass, bringing selected .NET cases to 1,160. Each persona mirror also ran directly with retained output: 23 party-mode and 20 forge-idea cases per tree, 86 total. These direct checks substantiate the nested count rather than adding it to the 88-test discovery count. No production or test source changed.
- Final independent review confirmed all ten findings addressed and found no concrete remaining problem. The report's verbatim reproduction block then passed all 15 recorded commands, checking the same source/dependency identities, exact XML/Python results and executable static/preservation assertions. Its completion receipt and log are retained separately; no additional default-suite or deployed proof is inferred.
- Publication preparation: fresh nonrecursive origin/main fetch shows zero divergence from the canonical investigation revision. The Conventional Commit message passes the repository's local commitlint. Final scope and staged whitespace checks precede the explicitly authorized commit/push; only this reconciliation's files are staged.

## Review Triage Log

- BH1 — low / patched: the report incorrectly implied terminal status had been read after final actor recreation. The test asserts status before recreation and reuses the status store; the report now confines restart evidence to event history and replay refusal.
- BH2 — low / patched: reproduction omitted static ownership/setup/CI assertions. Their executable command and passing output are now retained as a check and run by reproduction.
- BH3 — low / patched: reproduction could silently exercise different inputs. It now verifies baseline ancestry, committed gitlinks, initialized dependency revisions/clean tracked contents and recorded source hashes before running lanes.
- BH4 — medium / patched: exit-code-only reproduction could overlook reduced or skipped coverage. It now compares generated xUnit totals/passed/failed/skipped/errors/not-run and Python counts/outcome with recorded results.
- BH5 — low / patched: successful wrapper logs suppressed nested counts. Four direct persona-suite receipts now independently substantiate 23/20 cases per mirror and preserve their complete output.
- BH6 — low / patched: interpreter/platform metadata was absent despite version and branch constraints. Recorded CPython 3.14.4, full version/executable and Linux/WSL2 platform; report explicitly withholds native Windows qualification.
- BH7 — low / patched: preservation/whitespace checks had no retained invocations. The executable pinned-baseline check now proves exactly five additive notes, original ledger equality, scoped changed paths and changed-text whitespace, with its output retained.
- BH8 — low / patched: the current integration selection omitted detailed receipt payload/prior-value negatives. Five existing methods now pass 61 additional cases with retained XML/logs; no new tests or implementation changes were needed.
- BH9 — low / patched: runner logs deprecate -xml. Original receipt invocations remain truthful; new execution and reproduction use -result-xml, with output-path replacement updated accordingly.
- BH10 — low / patched: the remaining-gate paragraph lacked navigable successor references. Added links to the canonical durable-submission follow-up, materialization spec, deferred ledger and launch-readiness record. Nothing newly deferred.
