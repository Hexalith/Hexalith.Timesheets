---
title: 'Close five historical host integration deferrals'
type: 'chore'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile five stale deferred ledger observations covering three delivered concerns: non-Fresh catalog sanitization, local EventStore host wiring, and protected internal HTTP routes. Preserve the original observations and add evidence-backed dispositions, including explicit references to the runtime and deployed acceptance work that remains open. Verify existing implementation evidence, independently review the reconciliation, and commit and push to main as requested. These are five historical ledger dispositions, not five new runtime fixes.

</frozen-after-approval>

## Implementation Notes

- Inspection baseline: clean main at `63f89f96be8089f42bd37bcb4dca006454ca9549`. Read AGENTS.md, the root-declared AI.Tools LLM/Git baseline, editor/build/package/test configuration, boundary record, architecture acceptance boundary, existing closure specs and relevant host/loader/HTTP tests. No tracked .gitattributes or additional owned-path AGENTS.md applies.
- One small documentation reconciliation with no intent gaps, migration, domain-data mutation, new callable API or dependency change. Commit/push is explicitly authorized. Use oneshot and keep this spec in-progress until verification and independent review finish.
- Selected baseline ledger lines: 76 (non-Fresh collapse), 78 (no eventstore resource), 81 (anonymous internal routes), 192–194 (wire/prove resource duplicate), and 210 (non-Fresh duplicate). The two topology observations are superseded only as missing-resource diagnoses; their surviving current-graph runtime and deployed proof requirements stay with the existing canonical open entries. The other two duplicate observations do not imply independent implementations.
- Code map: EventStoreMagicLinkConfirmationCapabilityStateLoader.LoadActivityTypeCatalogAsync validates catalog shape and retains status only, and LoadTokenStateAsync withholds capability/Time Entry state. AppHost Program.cs wires the gateway, domain module, internal app port, generated signing material and app-channel tokens in run mode. The HTTP host invokes SDK registration and authentication/authorization plus InternalSurfaceGuard; credential and listener tests assert refused storage end states and admitted mapped delivery.
- Planned changes: exactly five additive resolution_note lines in deferred-work.md, this spec, and tests/five-historical-host-integration-deferrals.md. Preserve original ledger bytes, agent entry points, archived receipts, gitlinks, story statuses and launch classifications. Historical October 18 local proof used the earlier AppHost graph and does not qualify the current NuGet-orchestration graph or deployment.
- Verification: matching Debug/source solution restore and warnings-as-errors build, existing loader tests, internal-surface tests, selected HTTP credential/listener/freshness tests, and architecture suite as individual executable runs. Record exact commands, source identities and portable summaries. Confirm five adjacent dispositions, original-byte preservation, valid report links and three-file scope; no new tests are warranted for documentation-only edits. Complete the configured context-free Blind Hunter review, triage all findings, validate a Conventional Commit, fetch/inspect divergence and push without force.
- Implemented exactly five additive dispositions and the evidence report. Removing those five note lines reproduces every original ledger byte; the original observations, managed entry points, source/configuration, gitlinks and story/launch statuses are preserved. The report separates five ledger entries from three underlying delivered concerns and retains the canonical unfinished runtime proof requirements.
- Matching Debug/source solution restore and warnings-as-errors build passed (zero warnings/errors; build 36.50 s). Individual selections passed loader 112/112, internal-surface 39/39, selected HTTP 5/5 and architecture 109/109: 265 distinct cases, zero failures/errors/skips/not-run. Exact commands, full source identities and portable runner summaries are retained in the report. No runtime was started or stopped. The preservation/link/scope command passed after correcting the guard source link it caught; independent review remains pending.
- Source recheck tightened the credential-test claim: it asserts refusal codes and legacy direct-index absence, while selected index/catalog content preservation is asserted by the separate public-listener cases. Updated the added note/report accordingly; no test or runtime behavior changed.
- Independent context-free Blind Hunter reviewed 17.908 kB of changed content (finding floor five) and returned five evidence/traceability findings. All were checked against source and corrected in the added notes/report. The earlier pending-review notes are superseded by this completed review; no new deferred work or runtime change is required.

## Review Triage Log

- **low / patched:** “without storage writes” overstated the credential regression's assertion. The test sends an empty request, checks 401/403 and legacy key absence; the separate public-listener theory compares selected index/catalog contents. The note and report now distinguish those proofs.
- **low / patched:** the HTTP freshness method covers scripted stale versus unresolved confirmation submission, not every status/route pair. The report now preserves that limitation and assigns the complete five-status matrix to the loader tests, matching the canonical prior verification.
- **low / patched:** the original internal-route finding spans more than the selected credential test. Added links to the pinned SDK route catalog, endpoint policy mappings and security extension, verified against the committed EventStore gitlink. The report separates that source evidence from the narrower HTTP credential selection.
- **low / patched:** “earlier graph” alone omitted why local persistence proof cannot qualify the current graph. Git show confirms historical AppHost SDK 13.5.3/CLI bundle, and the retained README records the EventStore 3.125.1 override; the report now contrasts those with current 13.6.1/NuGet orchestration and 3.126.0 source verification.
- **low / patched:** quoted canonical handoff titles were hard to find among repeated observations. Added direct links to the resolved freshness spec/report and ledger section, automated AppHost smoke section and current-graph qualification section. Original ledger observations and every unfinished gate remain intact.

- Final verification: all five findings are addressed and nothing is newly deferred. The exact documented preservation/link/scope check passes, all three ledger-section anchors resolve, and the three SDK policy-source files match the committed EventStore object. The existing 265 passing cases and zero-diagnostic build remain applicable because review corrections affect documentation only. Conventional Commit validation and whitespace checks pass; a fresh fetch found zero divergence from origin/main. The reconciliation is finalized for the explicitly authorized commit/push, with exactly the three planned documentation files and no gitlink, runtime, configuration, agent-guidance or launch/story status change.
