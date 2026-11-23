---
title: 'Read one complete ledger snapshot per export'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
baseline_commit: '5b1b1edb4a789a78f3a84c114e72d76f8d7456ce'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
---

## Scope

[Story 6.3](../planning-artifacts/epics.md#story-63-read-one-complete-ledger-snapshot-per-export) requires preview and generation to read the complete, fresh, billable filter scope at one ledger version. The review covered the snapshot changes introduced in `124dc5b` and the receipt and authorization changes through `178d031`.

## Review Findings

- [x] [Review][Patch] Honor a reader veto even when the page is empty. Traverse a fresh, authorized empty page with an advancing continuation. Return a retryable block when the reader becomes unavailable during traversal.
- [x] [Review][Patch] Reject a resource disclosure denial during pagination. Reauthorize the retained pages’ rows before reporting readiness or recording a receipt, then check all generation rows again after recording and before delivery.
- [x] [Review][Patch] Derive the effective current and superseded scope flags while retaining the original query filters. Recover earlier v1 receipts that copied both flags from the filters.
- [x] [Review][Patch] Reject blank snapshot identifiers and verify independent row-level checkpoint and timestamp drift. Apply an approved correction between two projection reads and assert that preview and generation withhold the result. Compare the ordered current and superseded rows and CSV with a single-page reference on the stable history.
- [x] [Review][Defer] Qualify the installed reader’s snapshot stability, correction lineage and cursor progression. The reader supplies a canonical cursor per position and certifies the whole filtered version. Exercise that contract through the actual release composition in [launch readiness](../../docs/launch-readiness.md).

### Rejected

- A complete atomic read can omit the snapshot ID under the existing reader contract. Multipage reads require the ID on every page.
- A custom recorder that reconstructs the original event fields can continue to acknowledge an atomic read without a checkpoint. Multipage acknowledgement requires the certified checkpoint.

## Verification

The Debug build passed with warnings as errors. All six .NET suites passed on the refreshed `main` checkouts, with 2,981 passing tests, five default skips and zero failures. The focused server and integration builds and all 152 finance and ledger tests passed against the committed dependency revisions. The compiled AppHost configuration and endpoint-model probes passed.

The live `aspire run --non-interactive --isolated --no-build` attempt stopped when the CLI could not create its backchannel directory on the read-only home filesystem. Installed finance qualification remains in the launch gate.
