# Epic 3 Context: External Contributor Confirmation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Let external contributors submit, confirm, or adjust scoped time without becoming full internal users, so their effort can enter the same approval and audit path as internal time. v1 offers only an API and a single-purpose magic link. Valid links must resolve in the running host; failing closed on a missing lookup is not a substitute for that behavior.

## Stories

- Story 3.1: Expose External Contributor Confirmation API
- Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities
- Story 3.3: Confirm Time Through Magic Link
- Story 3.4: Adjust Time Through Magic Link
- Story 3.5: Reject Invalid Confirmation Links Without Resource Disclosure
- Story 3.6: Implement EventStore-Backed Magic-Link State Loading
- Story 3.7: Prove Magic-Link No-Disclosure at the HTTP Boundary

## Requirements & Constraints

External API callers need tenant-scoped authorization and a valid Contributor Party reference. Tenant and resource gates run before aggregate load, command dispatch, projection read, or any magic-link disclosure. JWT claims and caller-supplied tenant, user, or authorization fields are evidence, not authority.

API-created entries use the same Time Entry flow as internal entries: whole-minute duration, one Project or Work target, Activity Type, billable flag, and the configured Draft or Submitted state. They follow the same approval, rejection, correction, locking, and audit rules. Confirmation and adjustment are contributor evidence, not approval. Retries with the same idempotency context must not create a second entry or a second confirmation.

A magic link is a server-generated opaque capability, not a login session. It is bound to one tenant, one Contributor Party, one entry or proposed entry, one allowed action, an expiry, and single-use state, and it can be revoked. Persist only the token hash and capability metadata. Issue, use, revoke, and expiry are EventStore audit events. Raw tokens and decoded capability material are never logged, projected, exported, or stored in comments.

Every invalid path returns the same external failure: malformed, unknown, expired, used, revoked, unauthorized, wrong recipient, wrong action, cross-tenant, replayed, stale catalog, missing dependency, and rate-limited. The response must not reveal whether a link existed or why it failed, and must not reveal tenant, Party, Project, Work, Time Entry, duration, comment, Activity Type, approval, or capability state. Only authorized internal audit may distinguish those categories. Repeated abuse stays no-disclosure.

A valid page is shown only after the token is validated. It may show the proposed date, duration, Activity Type, billable flag, and the minimum target context needed for that confirmation. Comments are sensitive: external display is off unless policy explicitly allows it, and a missing comment policy fails the trust-bearing action closed. Adjustment exposes only policy-allowed fields. Invalid adjustments persist no partial entry and do not consume the capability.

Store stable Tenant, Party, Project, and Work identifiers only. Confirmation fails closed when a required reference, catalog, or authority cannot be resolved; stale display hydration is not enough for confirmation. Logs and traces carry correlation-safe outcome metadata and permitted hash or scope references only.

Secondary identity verification for high-value or billable links is post-v1. Audit instants are UTC; business dates follow tenant-local period policy. Magic-link audit metadata follows the documented tenant retention default. Legal-hold override remains a separate launch gate.

## Technical Decisions

EventStore is the only authoritative store for Time Entry and magic-link state. Do not introduce SQL, Redis, Dapr state, local JSON, or direct projection mutation for that state.

Single-use, revoked, and expired truth comes from folding the capability aggregate. A rebuildable candidate index, folded only from issuance events and holding the token hash, may locate a candidate; it is not authority. The live host must populate that index through the existing projection host, at the address the loader reads, without a second host or a new topology. The loader then folds capability state, folds the scoped Time Entry, and loads a fresh Activity Type catalog. Missing, stale, or unavailable state uses the same opaque external failure. Data Protection may protect purpose-bound material, but it is not the revocation or single-use record.

Keep capability decisions, validation, and policy in the server. Keep the candidate index in projections. Keep public contracts infrastructure-free: no EventStore envelopes and no caller-supplied authority fields. External routes are describe, confirm, and adjust for one capability; they must not expose browsing APIs. Transport failures share one ProblemDetails shape; domain rejections stay typed outcomes. The server kernel stays fail-closed until the host supplies trusted tenant context and a real loader.

Event and contract changes stay additive. Folds and projections must be deterministic under duplicate delivery, replay, and rebuild. Cross-module checks go through Tenants, Parties, Projects, and Works adapters. Do not add a Timesheets UI project.

Valid-link resolution in the running host is unfinished v1 work: the candidate index is not populated, so a valid token fails closed. That gap cannot be waived. Invalid-link no-disclosure is independent and already required.

## UX & Interaction Patterns

The external surface is a minimal responsive page outside internal shell navigation, built with Fluent UI V5 for a FrontComposer consumer. This repository supplies contracts and metadata; it does not own rendered UI or a Timesheets UI project.

Validate the token before any detail. On success, show only the allowed confirmation fields, with clear duration units. Actions are verb phrases: confirm time, and adjust when the capability allows it. Adjustment is one focused dialog. The page must work on a phone and by keyboard or touch, target WCAG 2.2 AA, and pair every state with text.

Every invalid outcome uses the same factual message and one safe recovery path, with no account-style navigation and no cause. Internal issued-link views use text status, expiry, and audit metadata, and never show the raw token.

## Cross-Story Dependencies

External submission and confirmation depend on Epic 1 capture, Party attribution, Activity Type catalog, comment policy, reference validation, and tenant gates, and on Epic 2 approval, rejection, correction, and locking. Confirmation never approves.

Issuance must exist before describe, confirm, or adjust. A successful confirm or adjust records capability use so the token cannot be reused. Live confirm and adjust stay fail-closed until the projection host populates the candidate index and the loader can fold authoritative state. HTTP proof must show an issued valid token reaching describe, confirm, and adjust, and must show equivalent no-disclosure responses for every invalid case.

Epic 3 owns that residual valid-link wiring. Epic 5 only checks the evidence afterward and must not be the first implementation of magic-link loading.
