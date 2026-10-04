---
title: 'Reconcile Story 3.6 historical review work against the implementation'
type: 'chore'
created: '2026-10-04'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 retains 37 unchecked historical review patches after multiple completed increments. Those checkboxes mix delivered changes, accepted limitations and explicitly deferred work, so re-entry cannot reliably identify remaining implementation. One requested verification is still missing: comparing every observable field of a nontrivial folded bundle across event ordering and duplicate variants.

**Approach:** Reconcile each historical item against current code, tests, approved decisions and the deferred-work ledger, with an explicit disposition and supporting evidence. Add the missing complete-state determinism regression, refresh verification and sprint tracking, and record outstanding acceptance limits clearly. Keep the story in review while live EventStore topology, durable atomic writes and release-owned inventory remain unfinished.

</frozen-after-approval>

## Implementation Notes

- Clean main baseline: `2b03356b4866900559c772c402301ac89c084ef5`. The current cached Epic 3 context and completed Story 3.5 supply continuity. Actual `global.json` selects SDK `10.0.401`; the managed agent-context block's older SDK and unwired-index observations remain separately ledgered.
- Three independent read-only investigations checked loader, projection and registration review items. Use the existing story paragraphs as historical findings: close implemented/superseded items with code/test evidence; convert partially or wholly outstanding items to tracked Defer dispositions. Preserve original acceptance criteria, historical review decisions and earlier frozen specifications.
- Main paths: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `sprint-status.yaml`, `deferred-work.md`, `tests/3-6-test-summary.md`, `docs/launch-readiness.md`, and `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`. Reuse the loader test's event fixtures and JSON serialization to compare the entire public bundle across ordered, reversed and shuffled/duplicated lifecycle histories; assert meaningful final transitions too. No new public API, dependency or production implementation is needed.
- Existing deferrals retain ownership: shared shapes into Contracts, IConfiguration gateway binding/independent registration, ServiceDefaults handover, test-double semantics, the uncalled index Rebuild helper, repository-wide test naming, and deployed capability inventory. Mixed catalog cursor coordinates are an accepted informational-metadata limitation in the completed live-catalog spec. Completion of this reconciliation must not claim a deployed inventory or close live/durable acceptance gaps.
- No unresolved product choice or irreversible action was found. Scope is one review-readiness goal with a focused verification repair. Use the matching README source-reference Debug restore/build, then run every test project individually through its direct xUnit v3 executable; retain existing performance skips. Validate all 37 original findings have a disposition, deferred links resolve, story/sprint agree on review, and production/submodule files are unchanged.
- Required pre-edit Aspire check used the explicit AppHost path and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`. `aspire start --no-build --non-interactive --format Json` succeeded; `aspire wait timesheets --timeout 30 --non-interactive` exited 18; describe reported Timesheets Finished with exit 134, and logs reported `Failed to bind to address http://127.0.0.1:8080: address already in use`. `aspire stop --non-interactive` succeeded. This is blocked runtime evidence, not a passing live journey.

- Reconciled all 37 original unchecked patches, preserving each finding and adding a current disposition: 29 implemented/superseded and eight with residual deferred work. The deployed inventory has its own release-owned ledger entry; earlier ledger paragraphs remain historical. Initial Completion Notes are marked historical instead of rewritten. Story/sprint remain review and readiness remains FAIL.
- Added `LoadTokenStateAsyncPreservesCompleteFoldedBundleAcrossOrderingAndDuplicateVariants`, covering the entire serialized bundle and meaningful final transitions across a rich Recorded/Adjusted/Confirmed/Submitted/Approved/Corrected history with Issued/Used capability variants. Matching Debug source-reference restore/build passed (0 warnings/errors), followed by each of the six direct test executables: Architecture 56, Contracts 90, Server 479, Projections 146, Integration 110 (four skips), Works 76; total 957 / 953 pass / four skips / zero failures. No mutation rerun is needed because its verifier and production inputs are unchanged.
- Post-evidence ArchitectureTests rerun passed 56/56. A focused structural check verified all 37 original finding texts are preserved verbatim before their new resolution note, all disposition links/anchors resolve, no unchecked review patch remains, and story/sprint agree on review. `git diff --check` passed; only the seven planned test/evidence/tracking files changed. Shared Builds commitlint accepted the Conventional Commit message.
- Independent Blind Hunter review returned ten findings. The partially resolved logging item now remains a Defer disposition: named slot/logger/context wiring is complete, but store transport exceptions still lack a local safe outcome trace. The final counts supersede the pre-review tally above: 28 implemented/superseded and nine deferred. Added specific current helper-only and double-semantics ledger annotations, and labeled the initial Debug Log historical with a current-verification link. The HTTP double already validates per-key ETags; the remaining ETag gap is in the projection double. Production code is unchanged.
- Patched review verification findings in the single Fact: all nine independently named capability/Time Entry variant combinations now assert expected use, confirmation, submission, approval authority and correction provenance. Distinct submitter/approver/corrector identifiers, changing sensitive-policy comments and canonical unavailable AI metrics supply non-default permitted evidence. ExternalContributor metrics cannot carry provider-reported values under the domain rules, so no invalid automated-agent history was fabricated. The focused Server.Tests Debug rebuild passed with zero warnings/errors.

## Review Triage Log

- **medium / patch — partially closed logging finding:** Verified that ReadModelWritePolicy only logs conflict/exhaustion. Corrected the original composite disposition to Defer and appended the existing transport-diagnostic residual with safe logging boundaries; final tracking count is 28 resolved and nine deferred.
- **medium / patch — baseline-only snapshots:** Comparative serialization could miss an audit field consistently omitted by every variant. Added expected values from the input use, confirmation, submission, approval and correction events alongside whole-bundle comparison.
- **low / patch — interchangeable actors:** The fixture reused Operator for several audit roles. Distinct submitter, approver and corrector references now falsify swaps, with explicit assertions.
- **low / patch — nullable evidence stays default:** Added differing recorded/adjusted/corrected sensitive-policy comments and canonical unavailable AI metrics, checking the final record and evidence snapshots. Provider/estimated metrics are forbidden for ExternalContributor and were not introduced.
- **low / rejected — rich history across pages:** Existing exclusive-cursor, malformed-page, sequence-gap and metadata tests cover paging; this regression tests normalized folding and independent duplicate/order variations. A second rich paginated fixture would add setup and cases beyond a simple correction without an identified paging defect.
- **low / patch — paired variants:** Replaced three index-paired runs with all nine independently varied capability/Time Entry combinations within the same Fact.
- **low / patch — unidentified failing variant:** Added stable variant names to each assertion and snapshot comparison, preserving the single-test inventory.
- **low / patch — initial Debug Log looks current:** Marked the initial commands/counts historical and linked the current verification record; original evidence text remains intact.
- **low / patch — mixed helper ledger:** Appended a current helper-only annotation isolating the static Rebuild residual from already completed loader cleanup.
- **low / patch — mixed double-semantics ledger:** Appended exact current double names and missing storeName/ETag behavior. Direct source inspection confirmed the HTTP double already checks per-key ETags and has no batch Apply path, so no such defect is asserted.

- Final post-review Server.Tests rerun passed 479/479 and ArchitectureTests passed 56/56; the other four lanes are unchanged since their passing invocations. Final inventory remains 957 total / 953 pass / four declared skips / zero failures. Story/sprint remain review; this reconciliation increment is done.
