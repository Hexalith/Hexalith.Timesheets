# Epic 3 Context: External Contributor Confirmation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable external contributors to submit, confirm, or adjust one scoped contribution without receiving internal tenant access, while preserving the same tenant isolation, reference validation, EventStore evidence, approval workflow, auditability, and privacy guarantees as internal time capture. The v1 surface is deliberately limited to API integration and single-purpose magic links rather than a full external-party portal.

## Stories

- Story 3.1: Expose External Contributor Confirmation API
- Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities
- Story 3.3: Confirm Time Through Magic Link
- Story 3.4: Adjust Time Through Magic Link
- Story 3.5: Reject Invalid Confirmation Links Without Resource Disclosure
- Story 3.6: Implement EventStore-Backed Magic-Link State Loading
- Story 3.7: Prove Magic-Link No-Disclosure at the HTTP Boundary

## Requirements & Constraints

- External API callers require tenant-scoped authorization and a valid Contributor Party reference. Server-side tenant and resource gates must run before state loading, dispatch, or disclosure; JWT claims and caller-supplied context are evidence, not authority.
- External entries use the normal capture and review path. They must validate Party, Project or Work, and Activity Type references; persist through Hexalith.EventStore; and obey the same submission, approval, rejection, correction, locking, and audit rules as internal entries. Confirmation is contributor evidence, never approval.
- Retried API commands with matching idempotency context must not duplicate entries or confirmation evidence.
- Magic links are server-generated opaque capabilities bound to one tenant, Contributor Party, entry or proposed entry, allowed action, expiry, and single-use state. Store only the token hash and capability metadata; issue, use, revoke, and expiry outcomes are EventStore-backed audit evidence.
- Invalid, malformed, unknown, expired, used, revoked, unauthorized, wrong-recipient, wrong-action, cross-tenant, replayed, stale-catalog, and infrastructure-unavailable cases must fail closed with equivalent external responses. They must reveal no token existence or reason and no tenant, Party, Project, Work, Time Entry, duration, comment, Activity Type, approval, or capability state.
- A valid link may disclose only the proposed date, duration, Activity Type, comment allowed by policy, Billable Flag, and the minimal target context needed for the decision. Adjustments expose only policy-allowed fields and must be validated atomically; failure must persist neither partial entry state nor capability use.
- Store stable sibling identifiers only. Do not persist Party personal data or copied Tenant, Project, or Work data. Comments are sensitive unstructured data.
- Logs and traces may contain correlation-safe outcome metadata and permitted hashed/scoped references only. Never log token values, decoded capability material, comments, command bodies, event payloads, personal data, target names, or protected identifiers.
- Event and contract evolution must remain additive and serialization-tolerant. Event consumers, state folds, and projections must tolerate replay and duplicate delivery deterministically.

## Technical Decisions

- Hexalith.EventStore is the sole authoritative persistence path. Aggregate state, not a projection or token lookup index, decides expiry, revocation, use, scope, and single-use validity.
- Token-hash lookup may use a rebuildable, non-authoritative candidate index derived only from issuance events. Before acting, resolve the candidate and fold the authoritative capability stream plus the scoped Time Entry state; also require a fresh Activity Type catalog. Missing, stale, degraded, or unavailable authority yields the same no-disclosure denial.
- Keep magic-link capability logic and validation orchestration in the server layer, read-model/index handling in projections, and isolated action-specific endpoints in the host. Public contracts remain infrastructure-free and must not expose EventStore envelopes or server-controlled authorization fields.
- Magic-link routes expose only scoped actions such as describe, confirm, or adjust; they must never become token-inspection or general Timesheets browsing endpoints. HTTP transport failures use a uniform ProblemDetails shape while domain rejections remain typed outcomes.
- Use stable string identifiers at module boundaries and sibling-module adapters for Tenants, Parties, Projects, and Works. Do not infer sibling ID formats or call their infrastructure directly.
- Keep the server kernel fail-closed when trusted context or required loaders are unavailable. Production state decisions must use server-established tenant context.

## UX & Interaction Patterns

The external experience is a minimal responsive Fluent UI V5 page outside internal shell navigation. Validate the token before showing any details. Present `Confirm time` and, only when permitted, `Adjust`; use a single focused Fluent dialog for editable fields, clear duration units, adjacent validation, and explicit verb-based actions. The page must work at phone widths and meet WCAG 2.2 AA with reading-order focus, keyboard and touch reachability, no hover-only controls, and no color-only state.

All invalid states use the same accessible, factual failure presentation and one safe recovery path without explaining whether the link existed or why it failed. Internal capability views may show text-bearing status, expiry, and audit metadata, but never the raw token.

## Cross-Story Dependencies

- External submission and confirmation depend on the existing Time Entry capture, Party/target validation, tenant authorization, and approval/correction workflows established by Epics 1 and 2.
- Capability issuance must precede describe, confirm, or adjust. Successful confirm/adjust must atomically consume the single-use capability through authoritative EventStore state.
- Live confirm and adjust flows depend on EventStore-backed token-hash resolution, capability and Time Entry folding, and fresh Activity Type catalog loading; HTTP-boundary tests must then prove equivalent responses and sensitive-field absence across every invalid route and method.
