# Epic 3 Context: External Contributor Confirmation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Let external contributors submit, confirm, or adjust scoped time without becoming full internal users, so their effort enters the same approval and audit workflow as internal time. v1 supplies API-only integration and single-purpose magic-link confirmation; a full external portal is outside scope. Valid links must resolve scoped state in the live host while invalid links disclose nothing sensitive.

## Stories

- Story 3.1: Expose External Contributor Confirmation API
- Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities
- Story 3.3: Confirm Time Through Magic Link
- Story 3.4: Adjust Time Through Magic Link
- Story 3.5: Reject Invalid Confirmation Links Without Resource Disclosure
- Story 3.6: Implement EventStore-Backed Magic-Link State Loading
- Story 3.7: Prove Magic-Link No-Disclosure at the HTTP Boundary

## Requirements & Constraints

External API callers require tenant-scoped authorization and a valid Contributor Party reference. Server-side tenant and resource gates run before aggregate load, dispatch, projection access, or disclosure. JWT claims and caller-submitted identity or authorization fields are evidence, not authority. Unverifiable required references and missing policy fail trust-bearing actions closed.

API-created entries follow ordinary Time Entry capture, validation, approval, rejection, correction, locking, and audit rules. Required capture evidence includes whole-minute duration, one Project or Work target, Activity Type, billable flag, and contributor attribution. Policy chooses Draft or Submitted workflow state. Contributor confirmation never grants approval. Matching idempotency context must avoid duplicate entries and submitted evidence.

A magic link is an opaque, server-generated capability scoped to one tenant, Contributor Party, proposed contribution, allowed action, expiry, and single-use state. Revocation and expiry are auditable. Persist only the token hash and capability metadata; issue and use flow through EventStore events. Never log, project, export, or put raw tokens or decoded capability material in comments.

Malformed, unknown, expired, revoked, used, unauthorized, cross-tenant, wrong-recipient, wrong-action, replayed, stale-catalog, unresolved, and rate-limited requests share equivalent external disclosure behavior. Failures reveal no token existence, reason, tenant, Party, Project, Work, Time Entry, duration, comment, Activity Type, approval state, or capability state. Only authorized internal audit may distinguish failure categories. Diagnostics retain correlation-safe outcome metadata and only permitted scoped/hash references; command bodies, personal data, target names, raw envelopes, and sensitive comments are excluded.

Valid confirmation displays only proposed date, duration, Activity Type, billable flag, and minimal target context. Comment disclosure requires explicit external policy and redaction; it is excluded by default. Adjustment exposes only policy-allowed fields. Invalid adjustment persists no partial entry and consumes no capability. Secondary identity verification for high-value or billable links is post-v1. Tenant-local dates define business periods; audit instants remain UTC. Legal-hold sign-off is a separate launch gate.

## Technical Decisions

Hexalith.EventStore is the authoritative persistence path. Keep aggregates pure and changes append-only. Do not introduce SQL, Redis, direct Dapr state calls, local JSON, or direct projection mutation for Timesheets domain state. Store stable sibling Tenant, Party, Project, and Work identifiers only; those modules retain ownership of their data.

The rebuildable token-hash candidate index locates a candidate without granting use authority. Issuance must populate it through the configured projection path and EventStore IReadModelStore, without direct test seeding. Authoritative loading folds capability state, scoped Time Entry state, and a fresh Activity Type catalog. Missing or unavailable dependencies preserve opaque failure behavior. Duplicate delivery, replay, and rebuild must yield deterministic, idempotent results. Single-use, revoke, and expiry decisions derive from authoritative folds.

Public contracts remain infrastructure-free and exclude caller-supplied authority. External routes describe, confirm, or adjust one capability without browsing broader Timesheets data. Keep server authorization defaults fail-closed. Event/contract changes remain additive; use sibling-module adapters for reference validation.

Local persistence proof, qualification of the current runtime graph, and deployed authority, complete inventory/continuity, and shared-rebuild acceptance are distinct gates. Historical local proof cannot close deployed acceptance. FR14 remains unfinished at that boundary and the overall release verdict remains FAIL until the outstanding gates are satisfied.

## UX & Interaction Patterns

Timesheets supplies contracts and FrontComposer-compatible metadata. A consuming FrontComposer host owns rendered Fluent UI V5 behavior, responsive layout, and browser/WCAG evidence; do not add a Timesheets UI project.

Validate the token before any detail appears. Confirmation is a minimal single-purpose surface outside broad internal navigation, usable on a phone and by keyboard or touch, with clear duration units and accessible focus order. Adjustment uses one focused dialog. Actions use explicit verb phrases. Status pairs visual treatment with text. Every invalid state provides one neutral message and one safe recovery path without revealing whether the link existed or why it failed.

## Cross-Story Dependencies

External submission and confirmation depend on Epic 1 capture, Party attribution, Activity Type catalogs, comment policy, reference validation, and tenant gates, plus Epic 2 approval, correction, locking, and period rules. Confirmation remains contributor evidence throughout those workflows.

Issuance precedes describe, confirm, and adjust. Accepted use prevents replay. Runtime proof must exercise authoritative loading and exact stored-event receipts through the configured host, while HTTP-boundary proof independently establishes equivalent failures and privacy-safe diagnostics. Epic 3 owns this behavior and its unfinished acceptance; Epic 5 reconciles evidence without substituting release assessment for implementation or deployed qualification.
