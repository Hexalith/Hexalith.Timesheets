---
title: 'Protect approved-time export across ledger reads, preview and generation'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5b1b1edb4a789a78f3a84c114e72d76f8d7456ce'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md'
---

<frozen-after-approval reason="replacement scope selected for local implementation">

## Intent

Implement five local finance-export obligations identified by the November 20 audit and Stories 6.1–6.3. Finance users need the same authorized evidence in preview, returned rows and the generated CSV. Preserve v1 filters, deterministic ordering, metadata and correction lineage. This increment includes the Story 6.2 rejection guard and fail-closed default. Durable receipt writing, replay after restart and release qualification have separate acceptance criteria.

## Acceptance

1. Given current, original and corrected comments, ledger projection, rebuild and direct query only publish each value when the trusted host permits publication and that value carries supported server publication authority. Cover versions 1, 2, missing and unknown. Preserve IDs and lineage.
2. Given distinct tenant and row export decisions, generation requires both for each published value. Apply to CSV and the entire serialized row, including both correction types. Resolve required policy in trusted tenant context. An unavailable policy blocks generation. Exercise all four decision combinations.
3. Given an authorized preview, report Allowed only for a nonempty universally permitted set of published values. For mixed permissions use Excluded and “Comments are permitted for some values only.” Handle complete exclusion, no comments and PolicyRequired separately. Denied callers get no preview. Preview never invokes the audit recorder.
4. Given a full-scope export, reject an initial browsing cursor explicitly or normalize the entire request and recorded scope to the first page. Verify snapshot continuity across pages, correct membership and advancing cursors. A fresh authorized empty page can precede matching rows. Drift, unidentifiable multipage snapshots, repeated rows, cursor cycles, loss of freshness or disclosure must withhold all output. Keep a legitimate current/superseded pair. Cover preview and generation.
5. Given unavailable, rejected, mismatched or uncertain audit acknowledgement, generation returns zero deliverable rows and bytes. Propagate caller cancellation. An unconfigured recorder or the shipped in-memory implementation cannot attest to durable completion. An explicitly installed recorder may acknowledge the exact export metadata through the existing extension point. Keep production finance delivery gated on a durable implementation.

</frozen-after-approval>

## Implementation

- [x] Use the rules in [AD-7](../planning-artifacts/architecture.md#ad-7--release-finance-output-only-after-snapshot-and-receipt-verification-adopted) and the cases in [Stories 6.1–6.3](../planning-artifacts/epics.md#story-61-enforce-export-comment-policy-on-every-representation).
- [x] Enforce publication in the approved-ledger projection and direct query, using the existing Time Entry evidence publication semantics. Apply finance redaction separately in ApprovedTimeExportService.
- [x] Update the shared preview and generation evaluation, page traversal and audit delivery guard. Keep access checks ahead of policy and protected reads.
- [x] Add focused tests in the owning suites and update affected positive fixtures to explicitly supply policy, checkpoint and recorder evidence. Run relevant Debug suites. Document how to configure the supported policy and recorder.

## Verification

The regression fixtures compare permitted and withheld markers in replayed ledger rows, hydrated reads, preview, CSV and serialized export rows. A frozen ledger reader verifies the complete traversal. Fault cases change snapshot identity, freshness, membership, authorization and audit acknowledgement independently. The tests use the repository's Debug source graph with SDK 11.0.100.

The warnings-as-errors Debug solution build passed. On the updated dependency checkouts, Architecture, Contracts, Server, Works, Projections and Integration reported 95, 108, 807, 76, 799 and 1,065 tests respectively. Across those six suites 2,945 passed and five opt-in or reserved cases were skipped. The report/export/dashboard performance test also passed with `TIMESHEETS_PERF=1`. A separate build using the committed dependency revisions passed all 145 focused finance and ledger cases on the final code.

## Review outcomes

- The service checks the tenant, entry and approval state before hydration. For corrections that move between projects or contributors, it checks the historical scopes before disclosure.
- A multipage reader supplies a version covering all entries in the filtered scope. Preview, generated metadata and the matching audit carry this version. Per-owner sequences cannot establish it. The service rejects stalled or cycling continuations and rechecks export authority after traversal.
- Preview examines comment decisions without constructing export rows or copying event lineage. Tests independently cover host-wide exclusion, reader veto, snapshot drift and UTC acknowledgement.
- Stories 6.1 and 6.3 are ready for review. Story 6.2 retains durable EventStore receipt writing and recovery after restart. The five deployed magic-link items remain open.

### Story 6.1 review findings

- [x] [Review][Patch] Return a blocked `PolicyRequired` preview after identity and reference checks pass but required evidence policy is missing. Actual access denials remain opaque.
- [x] [Review][Patch] Withhold current and nested comment text that still requires export redaction. Preview uses the same decision as CSV and serialized rows.
- [x] [Review][Patch] Recheck access to current and historical row scopes after receipt verification and before releasing the export. Exercise revocation during recording and each distinct correction scope.
- [x] [Review][Patch] Check preview cancellation after evaluation, before constructing a response.
- [x] [Review][Patch] Compare a direct read of unfiltered stored comments with rebuilt current and superseded rows across publication versions and host settings. Exercise a permitted comment through the composed export service and verify that the durable receipt omits its text.
- [x] [Review][Defer] Track independent finance permission at comment issuance and compatibility for older stored values. Track receipt scope flags and reader veto semantics with the receipt and snapshot stories. See [deferred work](deferred-work.md#deferred-from-code-review-of-story-61-2026-11-23).

The direct ledger reader contract supplies filtered rows. Export verifies membership before delivery. The
separate persisted Time Entry evidence handler continues to reject malformed issued decisions under its
own replay contract. A fresh single ledger page can report page readiness before export establishes
whole-scope readiness.
