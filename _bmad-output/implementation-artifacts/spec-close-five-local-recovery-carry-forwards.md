---
title: 'Close five historical local recovery carry-forwards'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five deferred ledger entries that already describe delivered local fixes: isolated fixture startup admission, external recording submit-response recovery, telemetry/API-key diagnostic redaction, shared-state preservation checks, and native EventStore restart recovery. Add evidence-backed dispositions so these completed carry-forwards stop appearing as unresolved work. Preserve original evidence and distinguish current focused verification from historical live proof and unfinished deployed acceptance. Review, verify, commit and push the completed reconciliation to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `5f3a83926500600f7cf32ee7828f5056afd99b85`. Read AGENTS.md, the declared AI.Tools LLM/state/Git baseline, editor/build/package/test configuration, boundary record, existing implementation specs, fixture handoff report and October 18 retained restart proof. No tracked .gitattributes applies.
- One small documentation goal with no intent gaps, domain-data mutation, new API, dependency update or runtime change. The user explicitly authorized commit/push. Use oneshot and keep status in-progress until verification and independent review finish.
- Selected exact summaries: “The deferred bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 startup gap is closed by a separate scoped increment”; “The local recording lost-response follow-up from the final materialization review is implemented”; “The deferred local OTLP/API-key diagnostic sanitization gap is closed”; “Shared-state verification now refuses active baselines and reports the specific changed services without exposing state keys or values”; “The selected local restart timeout now has a reviewed and verified direct-execution correction”. All five lack resolution_note despite describing completed implementations.
- Code map: AppHost Program.cs validates run-mode fixture components/control planes before resource construction; IsolatedGatewayLaunch.cs delegates native launch to EventStore. MagicLinkDurableSubmissionService observes the same command after response loss, and TimeEntryRecordSubmissionService requires exact committed event readback. The isolated Python runner owns sanitizer, two-observation baseline admission, preservation diagnostics, DCP/process identity checks and owned cleanup. Existing Python tests, compiled no-resource guard probes, recording service tests and HTTP actor-backed response-loss cases cover these behaviors.
- The earlier spec-close-five-local-fixture-deferrals.md closed the original implementation requests; these five are separate status carry-forwards. The selected restart disposition remains historical October 18 proof, with its original source identities. Current-graph AppHost runtime, subscriber delivery, deployed inventory/authority/shared rebuild, production readback budgets and Works version-forwarding ownership remain separate.
- Planned edits: five additive resolution_note lines in deferred-work.md, this spec and tests/five-local-recovery-carry-forwards.md. Preserve every original ledger byte, entry-point content, archived receipt, gitlink and launch/story status. Reuse existing tests; no new tests are justified for a documentation-only correction.
- Verification: matching Debug/source solution restore and warnings-as-errors build; run fixture Python tests, compiled configuration-only guards, existing recording service/durable submission tests, both actor-backed response-loss cases and architecture tests individually. Record exact commands, results and source identities in the report. Verify five uniquely placed notes, original byte preservation, report links and changed-file scope. Complete configured context-free Blind Hunter review, triage every finding, validate a Conventional Commit, fetch/inspect divergence and push without force.
- Implemented exactly five additive dispositions and the linked report. The report's executable read-only check proves each unique selected entry has one note immediately after its evidence, removing those five lines reproduces the original ledger bytes, all three entry points match baseline, report links resolve and only the three planned documentation files differ.
- Matching Debug/source solution restore and warnings-as-errors build passed (zero warnings/errors, build 12.90 s). Python fixture tests passed 97/97 in 22.306 s. Individual executable selections passed Server 49/49 (1.763 s), actor-backed HTTP 2/2 (1.672 s), Architecture 109/109 (13.308 s), all with zero failures/errors/skips/not-run. Real compiled Debug configuration-only probes passed 35/35, with complete cleanup and no components/containers created. Exact commands and portable summaries are retained in the report; this is focused verification, not a full-suite/live acceptance claim.
- Independent context-free Blind Hunter review is pending. No other review layer is configured. Runtime, dependency, gitlink and launch/story status changes remain outside the increment.
- Independent review completed over 19.322 kB of changed content (finding floor five). It returned five documentation/verification improvements; all were checked against the source and patched in the evidence report. No runtime defect or new deferred work was identified. The earlier pending-review note is superseded by this result.
- Post-review preservation, link, three-file scope and whitespace checks pass. The copied verification block also correctly exits 1 on a synthetic failed assertion rather than allowing the following Git check to hide it. Existing build/test/probe results remain applicable because the patches change documentation only. AppHost validates endpoint syntax; runner provisioning/identity checks supply the separate private-container boundary. Scheduler revision equality is explicitly baseline-only, while final preservation compares cluster identity and retained contents.
- Conventional Commit validation passed after installing the existing locked npm tool dependencies with scripts disabled; no manifest or lockfile changed. The initial validator invocation had no local executable, and the locked install resolved it. A fresh fetch found zero divergence from origin/main at the inspection revision. This reconciliation is finalized for the authorized commit/push; final staged whitespace and source-scope checks precede publication.

## Review Triage Log

- **low / patched:** the copied shell block could hide a failed Python assertion behind a successful Git command. Added `set -e` and verified both the successful command and synthetic assertion-refusal exit 1 without modifying repository content.
- **low / patched:** plain unstaged Git whitespace checking omitted the new report/spec. The preservation command now checks newline/trailing whitespace in every changed file, including untracked documents, and includes a pinned base-relative Git check for committed reproduction. Final staged Git checking covers all three files before commit.
- **low / patched:** the startup report overstated address admission as proof of private control-plane ownership. Source inspection confirms loopback shape, allowed/distinct ports and the separate runner container identity/ownership boundary; the report now distinguishes them.
- **low / patched:** the report did not identify where scheduler revision equality is required. capture_shared_baseline enforces it during admission; shared_preservation deliberately compares cluster identity and retained contents for final preservation. Both rules are now explicit.
- **low / patched:** the earlier and current five-item sets were not mapped precisely. The report now names their four common implementations, identifies routine compiled CI probes as the earlier fifth item and attributes this set's shared-state preservation item independently to its completed spec. All five findings are addressed; nothing newly deferred.
