# Epic 5 Context: Release Readiness Verification

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Confirm Timesheets is launch-ready only after Epics 1–4 have already implemented their feature behavior. The release owner aggregates evidence, explicit waivers, documentation consistency, and a final gate verdict so v1 does not hide unavailable defaults, overstate integrations, or treat story-complete work as launch-complete.

## Stories

- Story 5.1: Final Launch-Readiness Gate and Documentation Sync
- Story 5.2: Reconcile Package Currency and Platform Dependency Versions
- Story 5.3: Consume the Umbrella-Owned Hexalith.Works Checkout

## Requirements & Constraints

This epic verifies evidence. It must not be the first implementation of Magic-Link state loading, Work-reference validation, planned-effort reporting, export preview, or performance measurement.

Story-complete is not launch-complete. Unresolved host wiring, runtime fixtures, UI, deployment, stakeholder acceptance, legal-hold sign-off, skipped lanes, and accepted waivers must stay visible.

Classify each of the following as implemented, waived, or post-v1. Every waiver names owner, risk, and revisit condition:

- Unavailable defaults, skipped lanes, and deferred integrations
- Legal-hold override (still needs tenant/legal sign-off). v1 default: Time Entry and approval/correction events are indefinite audit evidence; export and Magic-Link audit metadata follow a documented tenant default
- Comment sensitivity / classification
- Export format (CSV is the assumed v1 UI affordance unless a structured API/webhook is chosen)
- Secondary Magic-Link identity verification for high-value/billable entries (already post-v1; do not silently close it)
- Performance evidence against the 500 ms command and 2 s report p95 launch targets

Required launch evidence: tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract and golden-file tests, privacy/logging scans, Magic-Link HTTP no-disclosure tests, a documented owns-vs-references boundary, launch-scope adapters delivered in their owning phases, and performance evidence or an explicit waiver. The release decision is PASS, CONCERNS, FAIL, or WAIVED with traceable evidence.

README, architecture, performance-evidence, launch-readiness, and sprint artifacts must distinguish story-complete from launch-complete. They must not claim live Works integration, a live Magic-Link loader, a preview HTTP endpoint, performance, or UI behavior that is not implemented.

Package currency: Central Package Management only; no inline package versions; no `.sln`. Root npm is not applicable unless a root manifest exists. Pin transitives only for compatibility, security, or deterministic builds, with rationale. Resolve or waive Dapr, Fluent UI, Aspire, or Hexalith.Builds version divergence. Do not mix sibling-submodule pointer updates into unrelated package edits. Restore, build, architecture tests, and affected tests must pass with warnings as errors; skipped or unavailable runtime evidence stays visible. Record a verdict that separates direct pins, npm applicability, transitive drift, and platform/submodule alignment.

Works checkout: Timesheets must not declare, gitlink, initialize, update, or probe a repository-root Hexalith.Works checkout. A caller-supplied `HexalithWorksRoot` is preserved for standalone/CI; otherwise it resolves only to sibling `../Hexalith.Works`. The sole default source is the umbrella-owned `<workspace>/references/Hexalith.Works`. Timesheets stores Project, Work, and Party IDs plus approval facts only—no copied sibling state, planning, work lifecycle, identity, invoicing, payroll, or rate cards.

Logs and traces use structured metadata and correlation IDs only—no event payloads, comments, personal data, tokens, secrets, or command bodies.

Any claimed web UI must follow FrontComposer/Fluent UI rules and WCAG 2.2 AA; do not imply those surfaces exist if they are not implemented.

Period policy for launch: tenant time zone (UTC audit instants and tenant-local period keys), with DST/period-boundary golden files owned by earlier stories.

## Technical Decisions

- Hexalith domain module: .NET 10, `.slnx`, Central Package Management, warnings as errors, additive contract evolution, EventStore as the only authoritative persistence path.
- Policy targets (Dapr SDK `1.18.4`, Fluent UI V5, Aspire, Hexalith.Builds) are not the same as every root or submodule pin. Track actual pins in the launch-readiness package-currency verdict.
- Architecture/fitness tests must reject inline versions, `.sln` files, forbidden package references, EventStore bypass, Contracts infrastructure references, a Timesheets-owned Works declaration or gitlink, and a root-local Works path probe.
- Works contracts resolve from the umbrella sibling or an explicit read-only `HexalithWorksRoot`. Work validation and planned-effort claims require a Works consumer query or EventStore-backed adapter and must fail closed when unavailable, stale, cross-tenant, or unauthorized.
- Export preview, if offered, is a side-effect-free service path. A preview HTTP endpoint is post-v1; docs must not advertise one that does not exist.
- Full EventStore-backed wire-path performance may stay waived until persisted runtime fixtures exist. This epic aggregates owning-story evidence rather than creating the first measurement path.
- CI validates restore/build, package references, architecture tests, unit tests, projection replay/idempotency, security/tenant isolation, and integration lanes that require Docker/Dapr/Aspire.

## UX & Interaction Patterns

- Do not document or market UI, export, Magic-Link, or planned-vs-actual behavior the product does not expose.
- Internal UI, when present, lives in FrontComposer with Fluent UI V5 (V4 icons only if V5 icons are unavailable). External confirmation is a minimal no-disclosure page; invalid or expired links reveal no tenant, Project, Work, Party, or Time Entry details.
- Export language is evidence-only—never invoice, payroll, rates, or revenue recognition. Comments may carry customer or private data; classification remains a launch classification, not a silent assumption.
- Copy stays factual: no celebratory or timer-app language, and no implication that Timesheets owns Party, Project, Work, or finance data.

## Cross-Story Dependencies

- Epic 5 runs after Epics 1–4 own their feature implementations. Magic-Link loader (Epic 3), Work validation (1.10), planned-effort reporting (4.8), export preview (4.9), command/report performance (1.11 / 4.10), retention and legal-hold (1.4), and tenant time-zone/period golden files (2.7 / 4.6) are prerequisites to verify, not work to start here.
- Stories 5.2 and 5.3 produce package-currency and Works-checkout evidence that Story 5.1 must include in the final gate and documentation sync.
- Story 5.3 must keep Stories 1.10 and 4.8 green without a nested Works checkout.
- Sibling-module package or checkout updates stay in their owning repositories unless explicitly approved for a Timesheets change.
