# Epic 6 Context: Qualify Project Time and Finance Evidence

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

An approver can trace recorded Project time through approval and correction to the effective ledger evidence. A finance operator can reconcile each delivered file to one authorized ledger snapshot and one durable receipt. Both workflows need evidence from the boundary at which users rely on them, including recovery after interruption.

## Stories

- Story 6.1: Enforce export comment policy on every representation
- Story 6.2: Require a durable audit before delivering finance output
- Story 6.3: Read one complete ledger snapshot per export
- Story 6.4: Persist workload-authenticated external confirmation
- Story 6.5: Bound external recording readback and report uncertainty
- Story 6.6: Support independent and composed route discovery
- Story 6.7: Wire one Project capture-to-ledger workflow
- Story 6.8: Qualify the deployed Project workflow
- Story 6.9: Reduce repeated ledger folds while preserving evidence
- Story 6.10: Recover interrupted period submissions and reviews

## Requirements & Constraints

Resolve tenant, actor, target and operation authority at the server boundary. Preserve the original approval and the link to every effective correction. Internal display and finance export have separate comment policies. Apply publication and export decisions to each value, including original and corrected comments, wherever a caller can read it.

Finance export requires fresh, approved billable rows over the entire authorized filter scope. Every returned row and CSV byte must belong to the same ledger version. Verify a durable receipt containing the requester, tenant, filter and output scope, UTC times, correlation, freshness, count, format version and hash of the delivered bytes. Store only audit metadata. Uncertain recording, snapshot drift or lost authorization withholds the file and rows. A retry of the same request after a lost acknowledgement or restart must recover exactly one matching receipt.

## Technical Decisions

EventStore owner streams supply authoritative history. Rebuild projections in valid sequence per tenant and owner, preserving current and superseded evidence when requested. A faster ledger fold must reproduce the golden rows and CSV bytes. Measure elapsed time and allocations at increasing tenant sizes before proposing a separately materialized ledger.

For external recording and confirmation, check the recorded contributor and target, preserve approval state and verify the committed effect. Bound readback by a deadline and honor caller cancellation. An uncertain result permits retry with the original command identity. For multi-entry period operations, record the admitted intent and expected effects before the first owner write. Recover missing effects across restarts and concurrent retries using the original attribution. Keep the period pending until all required histories agree.

Enroll each installed command and named projection once, with its operation schema and receiver method. A standalone Projections consumer must discover its own inventory through declared dependencies. Preserve public type identity and stored JSON compatibility when moving shared discovery ownership.

## UX & Interaction Patterns

An authorized finance operator can review scope, freshness and comment inclusion before requesting a CSV. Preview creates neither a file nor an audit event. Distinguish mixed permissions, complete exclusion and missing policy so the operator knows whether generation can proceed. A denied caller receives no preview. The consuming FrontComposer host owns the rendered experience.

## Cross-Story Dependencies

Finance acceptance combines comment disclosure, complete snapshots and receipt recovery. Qualification of the deployed Project workflow follows composition of the tenant, Party, Project, activity, approval, evidence-policy and ledger providers. Timesheets owns its domain behavior and Platform owns the release composition. The release owner verifies the supported entry points, second-tenant refusal, restart and rebuild on the installed release. Deployed magic-link authority, shared rebuild and ingress privacy remain separate work under Stories 3.6 and 3.7.
