# Epic 5 Context: Release Readiness Verification

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Confirm Timesheets is launch-ready only after Epics 1–4 have delivered the behavior they own. The release owner consolidates executable evidence, explicit waivers, package and workspace-dependency alignment, documentation consistency, and the final gate verdict so v1 does not hide unavailable defaults, overstate integrations, or confuse story completion with launch completion.

## Stories

- Story 5.1: Final Launch-Readiness Gate and Documentation Sync
- Story 5.2: Reconcile Package Currency and Platform Dependency Versions
- Story 5.3: Consume the Umbrella-Owned Hexalith.Works Checkout

## Requirements & Constraints

Epic 5 verifies and reconciles release evidence; it is not the first implementation location for Magic-Link state loading, Work-reference validation, planned-effort reporting, export-preview behavior, or performance measurement.

Classify launch concerns as implemented, unfinished, waived, or post-v1. An unfinished v1 requirement cannot be relabeled as waived without an approved scope change. Every waiver must identify its owner, risk, approval evidence, and revisit condition. Until valid-link Magic-Link resolution, the approved SDK/package baseline, and final documentation synchronization are complete, the release verdict remains FAIL; afterward it may be PASS, CONCERNS, FAIL, or WAIVED based on the remaining evidence.

Launch evidence must cover tenant isolation; approval and correction lineage; projection replay and rebuild; export contracts and golden files; privacy-safe logging; Magic-Link HTTP no-disclosure; owns-versus-references boundaries; concrete launch-scope adapters; and command/report performance against the 500 ms and 2 s p95 targets, or an explicit approved waiver. Unresolved host wiring, runtime fixtures, deployment, stakeholder acceptance, legal-hold sign-off, skipped lanes, and accepted waivers remain visible.

Documentation and sprint records must match the implemented boundary. They must not claim live Works, Magic-Link, preview-endpoint, performance, or rendered UI behavior without corresponding evidence. CSV is the verified v1 export format; structured API/webhook export is post-v1. Comments are sensitive unstructured evidence: diagnostics and exports exclude them by default, external disclosure requires explicit policy, and missing trust-bearing policy fails closed. Secondary Magic-Link identity verification remains post-v1; legal-hold policy remains a launch gate.

Package currency uses Central Package Management with no inline versions and no legacy `.sln`. Root npm is not applicable unless a root manifest exists. Add transitive pins only for compatibility, security, or deterministic-build reasons and document the rationale. Keep direct package currency, npm applicability, transitive drift, vulnerabilities/deprecations, and platform/submodule alignment as separate verdicts. Sibling package or pointer changes belong to their owning repositories unless explicitly approved.

Timesheets must not declare, gitlink, initialize, mutate, or probe a repository-local Hexalith.Works checkout. A caller-supplied `HexalithWorksRoot` is preserved for standalone/CI builds; otherwise it resolves only to the umbrella sibling `../Hexalith.Works`, whose source is `<workspace>/references/Hexalith.Works`. Timesheets retains stable sibling identifiers and approval facts only, never copied Project, Work, Party, tenant, planning, identity, invoicing, payroll, or rate-card state.

## Technical Decisions

- Build with .NET 10 through `Hexalith.Timesheets.slnx`, Central Package Management, and warnings as errors. The approved release baseline is SDK `10.0.401` and latest stable, .NET 10-compatible direct packages; the Timesheets-owned AppHost SDK target is `13.5.3`. Regenerate restore assets before verification and do not rewrite historical evidence as if it ran on the new baseline.
- Dapr SDK `1.18.7`, Fluent UI V5, Aspire, and Hexalith.Builds policy targets must be distinguished from actual root and submodule pins. Resolve divergence or retain it as a traceable waiver.
- Architecture fitness tests reject inline versions, `.sln` files, forbidden dependencies, EventStore bypass, infrastructure references from Contracts, a Timesheets-owned Works declaration/gitlink, and a root-local Works path probe.
- EventStore remains the only authoritative persistence path. Projections are rebuildable and non-authoritative; trust-bearing paths fail closed when required state, reference validation, authority, or freshness cannot be established.
- Logs and traces use structured metadata and correlation IDs without event payloads, comments, personal data, tokens, secrets, or full command bodies.
- Tenant time zone is canonical for business dates and period keys, UTC instants remain the audit source of truth, and DST/period-boundary behavior remains explicit launch evidence.
- Export preview remains side-effect free. A preview HTTP endpoint is post-v1 and must not be advertised unless implemented.
- Full EventStore-backed wire-path performance may remain waived until persisted runtime fixtures exist; Epic 5 aggregates owning-story measurements and waivers rather than introducing the first performance lane.
- Restore, warnings-as-errors build, architecture tests, affected unit/projection/security tests, Works adapter tests, and available integration lanes form the release verification gate. Skipped or unavailable runtime evidence is reported, not hidden.

## UX & Interaction Patterns

This repository supplies FrontComposer contracts and metadata, not a Timesheets UI project. Rendered Fluent UI V5 behavior, browser interaction, responsive layout, and WCAG 2.2 AA evidence belong to the consuming FrontComposer host and must not be claimed here before they exist.

External confirmation remains a minimal no-disclosure experience: invalid, expired, used, revoked, unauthorized, missing-state, or unavailable paths reveal no tenant, Project, Work, Party, Time Entry, duration, comment, or failure detail. Product and release copy stays factual and evidence-oriented; it must not imply Timesheets owns finance or sibling-domain data.

## Cross-Story Dependencies

- Epic 5 follows the feature work in Epics 1–4. Work validation, planned-effort reporting, export behavior, performance lanes, retention policy, and period-boundary evidence are prerequisites to verify, not work to start here.
- Valid-link Magic-Link host resolution remains an unfinished Epic 3 prerequisite; invalid-link no-disclosure evidence remains independently valid.
- Story 5.2 establishes the current SDK/package baseline. Story 5.1 then incorporates that evidence and reconciles all launch, planning, guidance, and sprint artifacts.
- Story 5.3 supplies the completed checkout-governance evidence and must keep the Work validation and planned-effort adapters green without a nested Works checkout.
