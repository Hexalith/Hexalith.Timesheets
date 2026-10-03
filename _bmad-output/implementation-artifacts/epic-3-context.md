# Epic 3 Context: External Contributor Confirmation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable external contributors to submit, confirm, or adjust scoped time through API integration and single-purpose magic links without internal tenant access. Preserve the authorization, reference validation, approval, audit, and privacy guarantees of internal capture. Valid-link resolution through the running host remains a v1 requirement even when unresolved links fail safely.

## Stories

- Story 3.1: Expose External Contributor Confirmation API
- Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities
- Story 3.3: Confirm Time Through Magic Link
- Story 3.4: Adjust Time Through Magic Link
- Story 3.5: Reject Invalid Confirmation Links Without Resource Disclosure
- Story 3.6: Implement EventStore-Backed Magic-Link State Loading
- Story 3.7: Prove Magic-Link No-Disclosure at the HTTP Boundary

## Requirements & Constraints

- External callers require tenant-scoped authorization and a valid Contributor Party reference. Server-side tenant/resource gates precede loading, dispatch, and disclosure; JWT claims and caller context are evidence, not authority.
- External entries validate Party, Project or Work, and Activity Type references and follow normal capture, submission, approval, rejection, correction, locking, and audit rules. Confirmation records contributor evidence and does not approve an entry.
- Retried API commands with matching idempotency context must not duplicate entries or confirmation evidence.
- Server-generated opaque capabilities bind tenant, Contributor Party, entry/proposed entry, allowed action, expiry, and single-use state. Persist only hashes and capability metadata; audit issuance, use, revocation, and expiry through events.
- Malformed, unknown, expired, used, revoked, unauthorized, wrong-recipient, wrong-action, cross-tenant, replayed, stale-catalog, and unavailable cases return equivalent opaque failures. Reveal neither token existence/failure reason nor tenant, Party, target, entry, duration, comment, Activity Type, approval, or capability details.
- A valid link may disclose only the proposed date, duration, Activity Type, Billable Flag, and minimal target context needed for the decision. External comment display is excluded by default and requires explicit policy/redaction; missing comment policy fails trust-bearing actions closed. Adjustments expose only policy-allowed fields; failure must persist neither partial entry state nor capability use.
- Persist only stable sibling identifiers, never copied Tenant, Party, Project, or Work data. Comments are sensitive unstructured evidence.
- Telemetry contains correlation-safe outcomes and permitted hashed/scoped references only; exclude tokens, decoded capabilities, comments, bodies, event payloads, personal data, target names, and protected identifiers.
- Event and contract evolution must remain additive and serialization-tolerant. Event consumers, state folds, and projections must tolerate replay and duplicate delivery deterministically.
- Secondary identity verification for high-value/billable entries is post-v1. Use UTC audit instants and tenant-local dates/periods. Confirmation audit retention follows a documented tenant default; legal-hold sign-off remains a launch gate.

## Technical Decisions

- Hexalith.EventStore is the sole authoritative persistence path. Aggregate state, not a projection or token lookup index, decides expiry, revocation, use, scope, and single-use validity.
- Resolve token hashes through a rebuildable, non-authoritative issuance-event index, then fold authoritative capability and scoped Time Entry streams and require a fresh Activity Type catalog. Missing/stale/unavailable authority produces the same opaque denial.
- Populate/rebuild that index through the configured EventStore projection handler and platform read-model store at the loader's address. Candidates hold only token hash, tenant reference, and capability identifier. Forbid direct projection mutation. Valid-link HTTP proof requires issuance and projection delivery without test-only index seeding.
- Keep magic-link capability logic and validation orchestration in the server layer, read-model/index handling in projections, and isolated action-specific endpoints in the host. Public contracts remain infrastructure-free and must not expose EventStore envelopes or server-controlled authorization fields.
- Expose scoped describe/confirm/adjust actions without token-inspection or general browsing endpoints. HTTP failures use uniform ProblemDetails; domain rejections remain typed outcomes. Cross-module checks use adapters/clients, never sibling infrastructure directly.
- Keep the server kernel fail-closed when trusted context or required loaders are unavailable. Production state decisions must use server-established tenant context.
- Reuse the existing domain host and platform projection infrastructure without new topology. Timesheets owns contracts, action/status semantics, and FrontComposer metadata; a consuming host owns rendering and browser/accessibility evidence. Do not add a Timesheets UI project.

## UX & Interaction Patterns

The consuming FrontComposer host provides a minimal responsive Fluent UI V5 page outside internal navigation. Validate before displaying details. Offer `Confirm time` and permitted `Adjust` through one focused dialog, with clear units, adjacent validation, and verb-based actions. Target phone usability and WCAG 2.2 AA: logical focus, keyboard/touch access, and text-bearing states. Timesheets metadata checks do not establish rendered conformance.

All invalid states use the same accessible, factual failure presentation and one safe recovery path without explaining whether the link existed or why it failed. Internal capability views may show text-bearing status, expiry, and audit metadata, but never the raw token.

## Cross-Story Dependencies

- External submission and confirmation depend on the existing Time Entry capture, Party/target validation, tenant authorization, and approval/correction workflows established by Epics 1 and 2.
- Capability issuance must precede describe, confirm, or adjust. Successful confirm/adjust must atomically consume the single-use capability through authoritative EventStore state.
- Live flows require index population, authoritative folds, and fresh catalog loading. HTTP tests prove equivalent invalid responses and sensitive-field absence across routes/methods.
- Epic 3 owns residual projection population and valid-link HTTP proof; existing invalid-link evidence remains accepted. Epic 5 reconciles readiness afterward. Unfinished v1 valid-link behavior cannot become a launch waiver.
