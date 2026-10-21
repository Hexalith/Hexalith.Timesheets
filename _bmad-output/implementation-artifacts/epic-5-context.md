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

Classify launch concerns with the approved dispositions implemented, unfinished, waived, or post-v1 (`sprint-change-proposal-2026-09-12.md` §3). Story 5.1 reconciled `docs/launch-readiness.md` to these four atomic labels; compound labels such as `implemented / waived` separate delivered controls from approved residual limitations. `blocked` describes an execution condition, not a launch disposition or waiver. An unfinished v1 requirement cannot be relabeled as waived without an approved scope change. Every waiver must identify its owner, risk, approval evidence, and revisit condition. Reconciled 2026-10-21: [Story 5.1 acceptance](evidence/5-1/20261020-final-gate/story-acceptance.json) and [Story 5.2 acceptance](evidence/5-2/20261020-final-acceptance/story-acceptance.json) close their separate reconciliation scopes, while deployed FR14 acceptance, current-graph runtime qualification, tenant/legal policy approval, configuration, and stakeholder acceptance remain unfinished. The release verdict remains FAIL while those gates are open; story acceptance alone does not authorize release.

Launch evidence must cover tenant isolation; approval and correction lineage; projection replay and rebuild; export contracts and golden files; privacy-safe logging; Magic-Link HTTP no-disclosure; owns-versus-references boundaries; concrete launch-scope adapters delivered in their owning phases; and command/report performance against the 500 ms and 2 s p95 targets, or an explicit approved waiver. Story-complete is not launch-complete: unresolved host wiring, runtime fixtures, deployment, stakeholder acceptance, legal-hold sign-off, skipped lanes, and accepted waivers remain visible.

Ratified launch classifications that must not be silently closed: unavailable defaults, skipped lanes, and deferred integrations; the legal-hold override, which still needs tenant/legal sign-off while the v1 default keeps Time Entry and approval/correction events as indefinite audit evidence and lets export and Magic-Link audit metadata follow a documented tenant default; comment sensitivity and classification; export format; secondary Magic-Link identity verification for high-value or billable entries, which remains post-v1; and performance evidence against the launch targets.

Documentation and sprint records must match the implemented boundary. They must not claim live Works, Magic-Link, preview-endpoint, performance, or rendered UI behavior without corresponding evidence. CSV is the verified v1 export format; structured API/webhook export is post-v1. Comments are sensitive unstructured evidence: diagnostics and exports exclude them by default, external disclosure requires explicit policy, and missing trust-bearing policy fails closed. Secondary Magic-Link identity verification remains post-v1; legal-hold policy remains a launch gate.

Package currency uses Central Package Management with no inline versions and no legacy `.sln`. Root npm is applicable only when a root manifest exists; release tooling added the root `package.json` and `package-lock.json`, so root npm is now applicable and `npm outdated`/`npm audit` findings stay visible with owner, risk, and revisit condition, while CI/CD release tooling owns the manifest, lockfile, and npm versions. Add transitive pins only for compatibility, security, or deterministic-build reasons and document the rationale. Keep direct package currency, npm applicability, transitive drift, vulnerabilities/deprecations, and platform/submodule alignment as separate verdicts. Sibling package or pointer changes belong to their owning repositories unless explicitly approved, and sibling-submodule pointer updates are never mixed into unrelated package edits.

This Timesheets repository is the umbrella pin for Works: its sole declared checkout is the `references/Hexalith.Works` submodule (`<workspace>/references/Hexalith.Works`), enforced by `WorksCheckoutGovernanceTests`. Timesheets must not declare, gitlink, initialize, mutate, or probe a repository-root `Hexalith.Works` checkout (a root `.gitmodules` path, gitlink, or `$(MSBuildThisFileDirectory)Hexalith.Works` probe), and must not initialize `Hexalith.Timesheets/Hexalith.Works` or nested submodules. A caller-supplied `HexalithWorksRoot` is preserved as read-only input for standalone/CI builds; otherwise MSBuild resolves `references/Hexalith.Works` first and the sibling `../Hexalith.Works` second. Timesheets retains stable sibling identifiers and approval facts only, never copied Project, Work, Party, tenant, planning, identity, invoicing, payroll, or rate-card state.

## Technical Decisions

- Build with .NET 10 through `Hexalith.Timesheets.slnx`, Central Package Management, and warnings as errors. The current release baseline is SDK `10.0.402` with `rollForward: latestPatch` (renegotiated on 2026-10-18 from the 2026-09-12 approved `10.0.401`) and latest stable, .NET 10-compatible direct packages. The Timesheets-owned AppHost SDK is `13.6.1`, aligned with the imported stable Aspire catalog line, and keeps NuGet-backed orchestration (`AspireUseCliBundle=false`, with only `ASPIRE010` suppressed). Regenerate restore assets before verification and do not rewrite historical evidence as if it ran on the new baseline.
- The imported Dapr SDK `1.18.10` family, Fluent UI V5, Aspire, and Hexalith.Builds policy targets must be distinguished from actual root and submodule pins. Resolve Dapr, Fluent UI, Aspire, or Hexalith.Builds divergence or retain it as a traceable waiver. One submodule divergence is open and unwaived: Works forwards its own hard-coded `HexalithEventStoreVersion` default into source-reference builds instead of the consuming catalog's EventStore version. The values differed on 2026-10-18 (`3.124.0` against `3.125.1`) and coincided at `3.126.0` at Timesheets commit `929ea22` on 2026-10-20; whenever they differ, source-reference test lanes pass `-p:HexalithEventStoreVersion=<catalog version>` until the deferred-work entry "Align the existing Works EventStore-version forwarding with the consuming source-build catalog in a separate build increment" closes.
- Architecture fitness tests reject inline versions, `.sln` files, forbidden dependencies, EventStore bypass, infrastructure references from Contracts, a repository-root Works declaration/gitlink, and a root-local Works path probe.
- EventStore remains the only authoritative persistence path. Projections are rebuildable and non-authoritative; trust-bearing paths fail closed when required state, reference validation, authority, or freshness cannot be established.
- Logs and traces use structured metadata and correlation IDs without event payloads, comments, personal data, tokens, secrets, or full command bodies.
- Tenant time zone is canonical for business dates and period keys, UTC instants remain the audit source of truth, and DST/period-boundary behavior remains explicit launch evidence.
- Export preview remains side-effect free. A preview HTTP endpoint is post-v1 and must not be advertised unless implemented.
- Full EventStore-backed wire-path performance may remain waived until persisted runtime fixtures exist; Epic 5 aggregates owning-story measurements and waivers rather than introducing the first performance lane.
- Restore, warnings-as-errors build, architecture tests, affected unit/projection/security tests, Works adapter tests, and available integration lanes, including those that need Docker, Dapr, or Aspire, form the release verification gate. Skipped or unavailable runtime evidence is reported, not hidden.

## UX & Interaction Patterns

This repository supplies FrontComposer contracts and metadata, not a Timesheets UI project. Rendered Fluent UI V5 behavior (V4 icons only where V5 icons are unavailable), browser interaction, responsive layout, and WCAG 2.2 AA evidence belong to the consuming FrontComposer host and must not be claimed here before they exist.

External confirmation remains a minimal no-disclosure experience: invalid, expired, used, revoked, unauthorized, missing-state, or unavailable paths reveal no tenant, Project, Work, Party, Time Entry, duration, comment, or failure detail. Product and release copy stays factual and evidence-oriented, with no celebratory or timer-app language; export language is evidence-only, never invoice, payroll, rates, or revenue recognition, and it must not imply Timesheets owns finance or sibling-domain data.

## Cross-Story Dependencies

- Epic 5 follows the feature work in Epics 1–4. These prerequisites are verified here, not started here: Magic-Link state loading (Epic 3, Story 3.6), Work validation (Story 1.10), planned-effort reporting (Story 4.8), export preview (Story 4.9), command and report performance (Stories 1.11 and 4.10), retention and legal hold (Story 1.4), and tenant time-zone/period golden files (Stories 2.7 and 4.6).
- Valid-link Magic-Link host resolution remains an unfinished Epic 3 prerequisite; invalid-link no-disclosure evidence remains independently valid.
- Story 5.2 establishes the current SDK/package baseline. Stories 5.2 and 5.3 produce the package-currency and Works-checkout evidence that Story 5.1 then incorporates into the final gate while it reconciles all launch, planning, guidance, and sprint artifacts.
- Story 5.3 supplies the completed checkout-governance evidence and must keep Stories 1.10 and 4.8 (the Work validation and planned-effort adapters) green with only the `references/Hexalith.Works` checkout, never a repository-root or nested-submodule Works checkout.
