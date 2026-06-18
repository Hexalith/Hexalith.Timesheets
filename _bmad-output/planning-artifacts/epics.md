---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/.decision-log.md
requirementsExtraction:
  status: confirmed
  functionalRequirements: 23
  nonFunctionalRequirements: 15
  additionalRequirements: 33
  uxDesignRequirements: 37
---

# timesheets - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for timesheets, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Glossary

- **EventStore authority** - Hexalith.EventStore is the authoritative persistence path for Timesheets domain state changes. Stories must not introduce direct authoritative SQL, Redis, Dapr state, broker-backed CRUD, or projection mutation.
- **Projection freshness** - Read models must expose whether data is fresh, stale, rebuilding, degraded, or unavailable. Trust-bearing actions must not treat stale or unavailable projections as fresh authority.
- **Approved ledger** - The rebuildable read model of approved time evidence. It is produced from domain events and is not a separate source of truth.
- **Correction** - An additive domain event or compensating command that preserves original values, corrected values, actor, timestamp, reason where required, and lineage. It is not an in-place edit.
- **Confirmation** - An external contributor action that confirms or adjusts scoped proposed time. It is not approval and does not grant internal tenant access.
- **Finance export evidence** - Approved billable evidence exported for downstream consumers. It excludes rates, invoices, payroll, taxes, and revenue-recognition decisions.
- **No-disclosure response** - A neutral response for invalid, expired, used, revoked, unauthorized, malformed, or unknown magic links that does not reveal whether a tenant, contributor, target, period, or entry exists.

## Requirements Inventory

### Functional Requirements

FR1: A Contributor can create a Time Entry with date, positive duration, exactly one Project or Work Target Reference, Activity Type, comment, Billable Flag, Contributor Party ID, and initial Approval State; creation emits a durable event and queryable projection, with human/external durations recorded in whole minutes.

FR2: Timesheets validates Project and Work Target References through sibling module boundaries before accepting trust-bearing submitted or approved time, stores only stable IDs and minimal correlation metadata, and fails closed when a required reference cannot be verified.

FR3: Timesheets preserves Time Entry history and correction lineage by emitting events for changes to entry facts or approval state, retaining original/correcting links, and preventing silent overwrite of approved entries.

FR4: A Contributor can submit Draft Time Entries for approval individually or in a Timesheet Period; submission records submitter, timestamp, scope, validates required fields/reference/activity/comment policy, and supports correction/resubmission of rejected entries without losing rejection history.

FR5: An Approver can approve or reject submitted Time Entries with policy-governed reasons; approval makes entries eligible for the Approved-Time Ledger, rejection records approver metadata and reason, and self-approval is denied by default unless policy allows it.

FR6: Approved Time Entries are locked from direct mutation; direct edits are rejected, and changes must be expressed through additive corrections that preserve original approval evidence and can be included or excluded from reporting through query options.

FR7: A Contributor can submit a weekly or monthly Timesheet Period scoped to one Contributor, one Tenant, and one period; an Approver can approve or reject that period while preserving the included Time Entry IDs and grouped review evidence.

FR8: Timesheets distinguishes entry Approval State from period Approval State so mixed entry states remain visible, period approval does not flatten entry history, and rejected entries can be corrected/resubmitted without rebuilding the whole period.

FR9: Timesheets resolves approver authority through fail-closed tenant and Project/Work context, using Tenants membership/roles and Project/Work approver projections with exact role precedence left to architecture/policy.

FR10: Authorized users can manage tenant-level Activity Types with stable IDs, display labels, active/inactive state, optional billable-default metadata, historical reportability, and no historical fact rewrite when renamed.

FR11: Authorized users can manage project-level Activity Types scoped to a Project Reference and tenant; tenant-level types remain available unless the project explicitly restricts the catalog, and reports group by stable IDs rather than display labels.

FR12: Every Time Entry is attributed to a Contributor Party ID and can distinguish internal, external, and AI-agent contribution without storing Party personal data; display data is hydrated at read time.

FR13: External contributors can submit or confirm Time Entries through an API surface without becoming full internal users, while still using tenant-scoped authorization, valid Party references, and the normal approval/correction/reference-validation/audit workflow.

FR14: External contributors can confirm or adjust a specific Time Entry through single-use, scoped, expiring Magic-Link Confirmation; invalid, expired, or already-used links reveal no tenant, Project, Work, Party, or Time Entry details.

FR15: AI-agent Contributors can attach AI Effort Metrics to Time Entries, including wall-clock execution time, model/tool runtime, billable effort, and provider-reported token consumption; missing provider metrics are represented as unavailable/unknown, not zero.

FR16: Authorized users can query Time Entries by Contributor, Project Reference, Work Reference, period, Activity Type, Billable Flag, Approval State, and source type, with tenant isolation, result-level authorization, inclusion/exclusion of non-current states, and projection freshness metadata.

FR17: Timesheets produces actual-time reports by Project and Work item, including planned-vs-actual comparison when Works supplies planned/estimated effort, without copying Project hierarchy or Work lifecycle state.

FR18: Timesheets maintains a rebuildable Approved-Time Ledger projection containing Time Entry ID, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comment, with corrected/superseded evidence visible.

FR19: Authorized finance/accounting consumers can export approved billable time filtered by tenant, Project, Work, Contributor, Activity Type, period, and Billable Flag; export output includes stable IDs and correction lineage but no rates, invoice totals, taxes, payroll, or revenue recognition.

FR20: Reports show AI-agent effort separately from human/external effort while allowing combined views where meaningful; AI reports include wall-clock time, model/tool runtime, billable effort, token metrics, AI Party filters, and Work Reference filters without implying units are interchangeable.

FR21: Timesheets persists all Time Entry, Timesheet Period, Activity Type, approval, rejection, and correction state changes through Hexalith.EventStore, with replayable aggregates/projections and domain rejections represented through Hexalith-compatible rejection patterns.

FR22: Timesheets exposes REST/SDK command and query contracts plus FrontComposer-compatible metadata for internal UI, API clients, automation, and downstream consumers; contracts must not accept server-controlled tenant/user/authorization context or expose EventStore internals.

FR23: Timesheets keeps sibling responsibilities outside its boundary by storing only Project IDs, Work IDs, Party IDs, tenant scope, and approval/correction facts, while excluding project planning, work lifecycle, identity/profile, tenant membership, invoicing, payroll, and rate-card ownership.

### NonFunctional Requirements

NFR1: Every Time Entry, Timesheet Period, Activity Type, projection, export, and command/query path is tenant-scoped; cross-tenant reads and writes must fail closed and be tested adversarially.

NFR2: Approval, rejection, correction, and export-relevant changes must be append-only events; approved evidence must never be silently overwritten.

NFR3: Durable Timesheets events store Party IDs and contribution evidence only, not Party display names, contact details, profile fields, personal-data objects, or copied Project/Work state.

NFR4: Corrections must preserve original values, corrected values, actor, timestamp, reason where required, and lineage to the affected entry.

NFR5: Approved-time exports must be auditable actions with requester, filters, timestamp, and output scope recorded where required by policy.

NFR6: Invalid or expired magic links reveal no tenant, Project, Work, Party, Time Entry, duration, or comment details.

NFR7: Retention policy for Time Entry events, export records, and Magic-Link Confirmation audit metadata must be decided before launch readiness.

NFR8: All command, query, API, Magic-Link Confirmation, export, and admin paths enforce tenant and resource gates; JWT tenant claims are not sufficient authority by themselves.

NFR9: Projections tolerate at-least-once delivery, duplicate events, replay, and rebuild, and reads expose stale, rebuilding, degraded, or unavailable states instead of pretending data is fresh.

NFR10: Common command acknowledgements should complete within 500 ms p95 in a warmed local service, subject to architecture sizing.

NFR11: Common report queries should return within 2 seconds p95 for tenant, project, and period filters, subject to architecture sizing.

NFR12: Logs and traces use structured metadata and correlation IDs without logging event payloads, comments, personal data, tokens, secrets, or full command bodies.

NFR13: Internal and external web UI surfaces follow Hexalith FrontComposer/Fluent UI rules and target WCAG 2.2 AA where applicable.

NFR14: Event and contract evolution must be additive and serialization-tolerant; previously emitted event fields must not be removed or renamed.

NFR15: Dates, periods, and approval cutoffs must be tenant-policy aware, with an explicit time-zone and period policy required for launch readiness.

### Additional Requirements

- Use an internal Hexalith domain-module scaffold as the starter, not the official Aspire Starter Application as the application architecture.
- Use public .NET/Aspire templates only for empty project shells such as `.slnx`, class libraries, Web API, ServiceDefaults, AppHost, and xUnit projects.
- Make the first implementation story scaffold the module shell with `.slnx`, Central Package Management, Contracts, Client, Server, Projections, Testing, ServiceDefaults, AppHost, and architecture tests.
- Normalize scaffolded projects to Hexalith conventions: `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`, no inline package versions, no `.sln`, warnings as errors, and module-local solution membership.
- Use .NET 10, nullable enabled, implicit usings, file-scoped namespaces, centralized package versions, and `Hexalith.Timesheets.*` namespaces.
- Preserve root-level submodule rules and never introduce recursive submodule initialization.
- Persist domain state changes only through Hexalith.EventStore; do not create direct SQL, Redis, Dapr state, or broker-backed authoritative CRUD storage.
- Establish baseline aggregate boundaries for `TimeEntry`, `TimesheetPeriod`, and Activity Type/catalog governance.
- Treat Magic-Link Confirmation, export audit, reference validation, and reporting as capabilities around domain events unless later decisions require separate aggregates.
- Implement approved-entry changes as additive compensating events with superseding correction lineage by default; offset-entry correction is deferred.
- Make operational views, approval queues, detail views, reports, AI reports, and the Approved-Time Ledger rebuildable projections with freshness/degraded/rebuilding metadata.
- Keep projections idempotent, replay-safe, duplicate-tolerant, checkpointed, and non-authoritative for writes.
- Validate Tenant, Party, Project, and Work references through adapters/projections/clients; submission, approval, export, correction, and magic-link confirmation fail closed when required authority cannot be resolved.
- Allow draft display to tolerate stale hydration only where policy allows; do not use stale data for approval, export, or confirmation decisions.
- Use JWT/OIDC authentication aligned with Hexalith modules, with Keycloak when enabled and development fallback only where AppHost supports it.
- Treat JWT tenant/user claims as request evidence, not authorization authority.
- Run tenant/resource gates before aggregate load, command dispatch, projection read, export, or magic-link disclosure.
- Implement Magic-Link Confirmation as server-generated opaque capabilities with token hashes, tenant/contributor/entry scope, allowed action, expiry, single-use state, audit events, and no-disclosure failure states.
- Never log, project, export, or persist magic-link token values or decoded capability material in comments.
- Expose writes through the EventStore command pipeline and hide aggregate/EventStore envelope mechanics from public command/query contracts.
- Return typed query/read models with projection freshness/trust metadata; transport errors use ProblemDetails while domain rejections remain typed domain outcomes/events.
- Use Dapr for service invocation, pub/sub, actors/state abstractions, and EventStore integration through existing Hexalith patterns; target Dapr SDK `1.18.4` unless later architecture changes it.
- Use Aspire AppHost for local topology, orchestration, observability, Dapr components, Redis/state/pubsub, Keycloak, and sibling services; AppHost owns topology only, not domain rules.
- Use .NET SDK container support if containers are needed; do not add Dockerfiles unless a later deployment decision explicitly requires them.
- Use `.NET` configuration with `__` environment nesting and keep secrets out of source control.
- Build through `Hexalith.Timesheets.slnx`, restore package versions from `Directory.Packages.props`, and treat warnings as errors.
- Organize projects by package first: host, Contracts, Client, Server, Projections, UI when needed, Testing, ServiceDefaults, and AppHost.
- Keep `Contracts` infrastructure-free; it contains commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only.
- Keep `Server` responsible for aggregate decisions, command handlers, policies, validation orchestration, export orchestration, and magic-link capability logic.
- Keep `Projections` responsible for read-model handlers, replay behavior, freshness, Approved-Time Ledger, and operational reports.
- Add architecture/fitness tests for forbidden package references, inline package versions, `.sln` files, direct persistence bypass, and Contracts infrastructure references.
- Add security tests for tenant isolation, magic-link no-disclosure behavior, and fail-closed authorization.
- Add projection replay/idempotency tests for every projection and export golden-file tests for export evidence.

### UX Design Requirements

UX-DR1: Internal Timesheets surfaces must run inside the existing `FrontComposerShell`; Timesheets must not introduce a parallel internal navigation shell.

UX-DR2: The external Magic-Link Confirmation surface must be a minimal responsive web page for one scoped contribution confirmation and may sit outside full internal shell navigation.

UX-DR3: Timesheets UI must use FrontComposer first, then Blazor Fluent UI V5 components; raw HTML, custom CSS, JavaScript, or third-party components are not allowed when an equivalent FrontComposer, Fluent UI, or Blazor component exists.

UX-DR4: The visual system must inherit Hexalith FrontComposer and Fluent UI professional themes with no Timesheets-specific palette, typography ramp, decorative cards, marketing hero sections, or gamified timer styling.

UX-DR5: Timesheets-specific layout decisions must use the documented spacing scale (`4px`, `8px`, `12px`, `16px`, `24px`, `32px`, `24px` page gutter, `8px` dense row gap, `16px` section gap) only where component defaults are insufficient.

UX-DR6: Custom component shapes, if unavoidable, must keep restrained radii: `4px` small controls, `6px` compact surfaces, `8px` dialogs/panels, and `9999px` only for status affordances where appropriate.

UX-DR7: `FrontComposerProjectionView` must be the default for time entry lists, periods, catalogs, reports, and ledgers, and must surface projection freshness when stale, rebuilding, degraded, or unavailable.

UX-DR8: `FrontComposerGeneratedForm` must be the default for record, submit, approve, reject, correct, manage catalog, and export commands, with validation messages beside fields.

UX-DR9: `FluentDataGrid` must be the primary component for queues, reports, ledgers, catalogs, and period rows, with sorting/filtering where needed, row-click detail navigation, keyboard traversal, and visible selected counts for batch actions.

UX-DR10: `FluentAccordion` must group pages, dialogs, or panels that have two or more sibling titled sections, with the primary section expanded by default.

UX-DR11: The only primary content on a page must not be hidden inside an accordion.

UX-DR12: `FluentAccordionItem` sections must be used for Evidence, Approval, Correction Lineage, AI Metrics, Export Scope, Audit Metadata, and Policy sections where those sections appear.

UX-DR13: `FluentTabs` may be used only for closely related subviews such as Entries/Periods/Ledger or Human/External/AI report partitions, not unrelated navigation.

UX-DR14: `FluentDialog` must be used for focused approve, reject-with-reason, submit period, correct approved entry, export review, and external confirmation adjustment decisions, with only one dialog layer.

UX-DR15: `FluentButton` text must be a clear verb phrase, with one primary action per command context and consequence-aware confirmation for irreversible or evidence-changing actions.

UX-DR16: `FluentMessageBar` must communicate persistent state or policy information such as stale projections, permission denied, invalid/expired magic link, comments policy, export scope warning, or unresolved approval authority.

UX-DR17: `FluentToast` may be used only for transient feedback after successful save, submit, approve, reject, correct, export, or refresh actions; it must not carry audit-critical information.

UX-DR18: A dense `FilterBar` must appear above grids and reports with only filters relevant to the current surface, and filters must be preserved during drill-in and back navigation.

UX-DR19: `StatusBadge` must display Approval State, period state, Billable Flag, contributor category, correction state, projection freshness, and magic-link state with text, not color alone.

UX-DR20: The dashboard must surface my current period, pending actions, approval workload, and report shortcuts through operational UI rather than marketing content.

UX-DR21: Record Time Entry must be reachable from Project/Work context actions, dashboard actions, and command surfaces, and must capture date, duration, Target Reference, Activity Type, comment, Billable Flag, and Contributor Party reference.

UX-DR22: My Timesheet Period must show Draft, Submitted, and Rejected entries for a weekly or monthly period, with entry Approval State and required-field correction status visible before submission.

UX-DR23: Time Entry Detail must expose evidence, approval state, correction lineage, AI metrics where present, and audit metadata without implying the entry is a mutable spreadsheet row.

UX-DR24: The Correction Flow must start from prior entry values but save an additive correction or resubmission, never a direct mutation of approved evidence.

UX-DR25: Approvals Queue and Period Approval Detail must support filtering submitted entries/periods, show period state separately from entry states, and allow approval/rejection with reasons where required.

UX-DR26: Activity Type Catalog must support tenant and project Activity Types, active/inactive state, billable defaults, historical reportability, and governance details.

UX-DR27: Operational Reports must show actual time by Contributor, Project, Work, Activity Type, Billable Flag, Approval State, period, and contributor category.

UX-DR28: AI Effort Report and Time Entry Detail must separate human/external duration from AI wall-clock, model/tool runtime, billable effort, and token metrics.

UX-DR29: Approved-Time Ledger must show approved evidence with stable IDs, approval metadata, billable status, comments where allowed, and correction lineage, and must disable export when filters return no approved entries.

UX-DR30: Export Review Dialog must summarize filters, output scope, included evidence fields, and explicitly avoid invoice, payroll, rate, tax, and revenue-recognition language.

UX-DR31: Magic-Link Confirmation must validate the token before showing details and, when valid, show only the proposed date, duration, Activity Type, comment, Billable Flag, and target context needed for confirmation or adjustment.

UX-DR32: Magic-Link invalid, expired, used, revoked, unauthorized, or unknown states must use the same no-disclosure failure surface and reveal no tenant, Project, Work, Party, Time Entry, duration, or comment details.

UX-DR33: UI copy must be factual, short, consequence-aware, non-celebratory, and must not imply Timesheets owns Party, Project, Work, invoice, payroll, rate, or revenue decisions.

UX-DR34: Projection freshness states must remain visible on reports, ledgers, and approval queues, and material freshness changes should be announced through an accessible status region.

UX-DR35: All actions must be keyboard reachable, including grid navigation, filter changes, dialog decisions, row actions, and magic-link confirmation; hover-only controls are not permitted.

UX-DR36: Duration inputs must state units clearly, AI metric fields must distinguish minutes, runtime, billable effort, and token counts, and missing token metrics must display `Unavailable` or `Not reported by provider`, never `0`.

UX-DR37: Responsive behavior must support full desktop/laptop internal workflows, reduced-column tablet/narrow desktop layouts, and fully usable phone layouts for external Magic-Link Confirmation.

### FR Coverage Map

FR1: Epic 1 - Trusted Time Capture & Activity Governance
FR2: Epic 1 - Trusted Time Capture & Activity Governance
FR3: Epic 1 - Trusted Time Capture & Activity Governance
FR4: Epic 2 - Submission, Approval, Period Review & Corrections
FR5: Epic 2 - Submission, Approval, Period Review & Corrections
FR6: Epic 2 - Submission, Approval, Period Review & Corrections
FR7: Epic 2 - Submission, Approval, Period Review & Corrections
FR8: Epic 2 - Submission, Approval, Period Review & Corrections
FR9: Epic 2 - Submission, Approval, Period Review & Corrections
FR10: Epic 1 - Trusted Time Capture & Activity Governance
FR11: Epic 1 - Trusted Time Capture & Activity Governance
FR12: Epic 1 - Trusted Time Capture & Activity Governance
FR13: Epic 3 - External Contributor Confirmation
FR14: Epic 3 - External Contributor Confirmation
FR15: Epic 1 - Trusted Time Capture & Activity Governance
FR16: Epic 4 - Approved Time Ledger, Reporting & Finance Export
FR17: Epic 4 - Approved Time Ledger, Reporting & Finance Export
FR18: Epic 4 - Approved Time Ledger, Reporting & Finance Export
FR19: Epic 4 - Approved Time Ledger, Reporting & Finance Export
FR20: Epic 4 - Approved Time Ledger, Reporting & Finance Export
FR21: Epic 1 - Trusted Time Capture & Activity Governance
FR22: Epic 1 - Trusted Time Capture & Activity Governance
FR23: Epic 1 - Trusted Time Capture & Activity Governance

### NFR Coverage Map

_Added 2026-06-19. All 15 NFRs trace to ≥1 story by acceptance-criteria substance. "Primary" = the story that most directly establishes the NFR._

| NFR | Theme | Primary | Also covered in |
|-----|-------|---------|-----------------|
| NFR1 | Tenant isolation (adversarial fail-closed) | 1.2 | 1.1, 1.7, 1.8, 2.2, 2.3, 2.6, 2.8, 3.1, 3.5, 4.1, 4.2, 4.4 |
| NFR2 | Append-only, no silent overwrite | 2.5 | 1.3, 2.4, 2.6, 4.2 |
| NFR3 | Store references only, no personal/sibling data | 1.7 | 1.2, 1.6, 1.8, 1.9, 3.1, 4.3 |
| NFR4 | Correction provenance | 2.6 | 2.4, 4.2 |
| NFR5 | Export auditability | 4.6 | 4.5 |
| NFR6 | Magic-link no-disclosure | 3.5 | 3.2, 3.3 |
| NFR7 | Retention policy (launch gate) | 1.4 | — |
| NFR8 | Tenant+resource gates on all paths | 1.2 | 1.5, 1.7, 2.3, 3.1, 4.1, 4.2, 4.5 |
| NFR9 | Projection at-least-once / replay / freshness | 1.8 | 2.6, 2.8, 4.1, 4.2, 4.3 |
| NFR10 | Command ack ≤500 ms p95 | 1.1 | (evidence harness; later stories add data) |
| NFR11 | Report query ≤2 s p95 | 4.3 | 1.1 |
| NFR12 | Privacy-safe logging | 1.4 | 1.1, 1.7, 1.9, 3.1, 3.4, 3.5 |
| NFR13 | WCAG 2.2 AA / FrontComposer+Fluent UI | 4.7 | 1.5, 1.6, 1.7, 1.8, 2.1, 2.3, 2.4, 3.3, 3.4, 4.1–4.5 |
| NFR14 | Additive, serialization-tolerant evolution | 1.3 | 1.9, 4.6 |
| NFR15 | Time-zone / period policy (launch gate) | 2.7 | 4.6 |

### UX-DR Coverage Map

_Added 2026-06-19. All 37 UX-DRs trace to ≥1 story._

| UX-DR | Theme | Primary | Also in |
|-------|-------|---------|---------|
| UX-DR1 | FrontComposerShell, no parallel shell | 4.7 | 1.1 |
| UX-DR2 | External magic-link minimal page | 3.3 | 3.4, 3.5 |
| UX-DR3 | FrontComposer-first, Fluent UI V5 | 1.3 | 1.1, all UI stories |
| UX-DR4 | Inherit Hexalith themes, no bespoke brand | 1.3 | 4.7 |
| UX-DR5 | Spacing scale | 1.3 | UI stories |
| UX-DR6 | Restrained radii | 1.3 | UI stories |
| UX-DR7 | ProjectionView default + freshness | 4.1 | 1.5, 1.8, 4.2, 4.3 |
| UX-DR8 | GeneratedForm for commands | 1.7 | 2.1, 2.3, 2.4, 4.5 |
| UX-DR9 | FluentDataGrid for queues/reports/ledgers | 4.1 | 1.5, 2.3, 4.3 |
| UX-DR10 | Accordion for 2+ titled sections | 1.8 | 2.6 |
| UX-DR11 | Primary content not hidden in accordion | 1.8 | 4.7 |
| UX-DR12 | Accordion sections (Evidence/Approval/…) | 1.8 | 2.6, 4.5 |
| UX-DR13 | FluentTabs for related subviews only | 4.3 | 4.1, 4.4 |
| UX-DR14 | FluentDialog for focused decisions | 2.3 | 2.6, 2.7, 3.4, 4.5 |
| UX-DR15 | FluentButton verb phrase + confirm | 2.3 | 2.5, 4.5 |
| UX-DR16 | MessageBar for persistent state/policy | 1.4 | 2.1, 2.2, 4.5 |
| UX-DR17 | Toast transient only | 2.1 | 2.3 |
| UX-DR18 | Dense FilterBar, preserved on drill-in | 4.1 | 1.6, 4.3 |
| UX-DR19 | StatusBadge text not color | 1.8 | 1.5, 2.3, 2.7, 3.2, 4.x |
| UX-DR20 | Operational dashboard, not marketing | 4.7 | — |
| UX-DR21 | Record Time Entry reachable + fields | 1.7 | — |
| UX-DR22 | My Timesheet Period states | 2.7 | — |
| UX-DR23 | Time Entry Detail, not mutable row | 1.8 | 2.6 |
| UX-DR24 | Correction flow additive | 2.4 | 2.6 |
| UX-DR25 | Approvals Queue / Period Detail | 2.8 | 2.3 |
| UX-DR26 | Activity Type Catalog | 1.5 | 1.6 |
| UX-DR27 | Operational Reports dimensions | 4.3 | — |
| UX-DR28 | AI Effort Report separation | 4.4 | 1.9 |
| UX-DR29 | Approved-Time Ledger + disable empty export | 4.2 | 4.5 |
| UX-DR30 | Export Review Dialog, no finance language | 4.5 | 4.6 |
| UX-DR31 | Magic-link validate-before-detail | 3.3 | 3.4 |
| UX-DR32 | Magic-link no-disclosure failure | 3.5 | 3.2 |
| UX-DR33 | Factual, non-celebratory copy | 4.5 | 1.4, 4.7 |
| UX-DR34 | Freshness visible + accessible status region | 4.1 | 4.2, 4.3, 4.7 |
| UX-DR35 | Keyboard-reachable, no hover-only | 1.7 | 2.3, 3.3, 4.1 (all UI) |
| UX-DR36 | Duration/AI units; missing tokens "Unavailable" | 1.9 | 1.7, 4.4 |
| UX-DR37 | Responsive desktop/tablet/phone | 3.3 | 1.7, 3.4 |

## Epic List

### Epic 1: Trusted Time Capture & Activity Governance

Users can record auditable time against Project or Work references for internal, external, and AI contributors, with Activity Type catalogs, Party attribution, reference validation, EventStore persistence, and module boundary enforcement in place.

**FRs covered:** FR1, FR2, FR3, FR10, FR11, FR12, FR15, FR21, FR22, FR23

### Epic 2: Submission, Approval, Period Review & Corrections

Contributors can submit entries and periods; approvers can approve or reject entries and periods; approved entries are locked from direct edit; corrections preserve evidence and lineage.

**FRs covered:** FR4, FR5, FR6, FR7, FR8, FR9

### Epic 3: External Contributor Confirmation

External contributors can submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.

**FRs covered:** FR13, FR14

### Epic 4: Approved Time Ledger, Reporting & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

**FRs covered:** FR16, FR17, FR18, FR19, FR20

## Epic 1: Trusted Time Capture & Activity Governance

Users can record auditable time against Project or Work references for internal, external, and AI contributors, with Activity Type catalogs, Party attribution, reference validation, EventStore persistence, and module boundary enforcement in place.

### Story 1.1: Set Up Initial Timesheets Project from Hexalith Module Scaffold

**Requirements:** FR21, FR22, FR23

As a Hexalith builder,
I want a Timesheets module shell that follows Hexalith architecture, build, package, and test conventions,
So that all future time-capture stories can be implemented on a stable EventStore-backed foundation.

**Acceptance Criteria:**

**Given** the Timesheets workspace is empty or unscaffolded
**When** the module shell is created
**Then** it contains `Hexalith.Timesheets.slnx`, Central Package Management, `Directory.Build.props`, `.editorconfig`, and projects for host, Contracts, Client, Server, Projections, Testing, ServiceDefaults, and AppHost
**And** no `.sln` file or inline package versions are introduced.

**Given** the module shell exists
**When** architecture fitness tests run
**Then** they verify package boundaries, Contracts infrastructure isolation, no direct persistence bypass, no forbidden `.sln` usage, and no inline package versions
**And** the tests are placed where future stories can extend them.

**Given** Timesheets domain state will be event-sourced
**When** the host and server projects are wired
**Then** EventStore integration points are present for future command handling
**And** no authoritative SQL, Redis, Dapr state, or broker-backed CRUD store is introduced.

**Given** Timesheets must enforce tenant/resource security from the first executable slice
**When** initial authorization and reference-validation abstractions are added
**Then** they fail closed by default
**And** no command/query path accepts caller-supplied server-controlled tenant, user, or authorization context as authority.

**Given** Timesheets references sibling modules
**When** contracts and abstractions are created
**Then** Timesheets stores and exposes stable Tenant, Party, Project, and Work IDs only
**And** it does not copy Party personal data, Project state, Work lifecycle state, or Tenant membership state.

**Given** future UI stories will use FrontComposer and Fluent UI V5
**When** initial UI/metadata entry points are scaffolded
**Then** they are compatible with `FrontComposerShell` and generated command/projection surfaces
**And** no parallel custom portal, custom theme, raw HTML-first component model, or Fluent UI V4 component dependency is introduced.

**Given** logs and telemetry are configured
**When** the module shell emits diagnostics
**Then** structured logs include correlation-safe metadata only
**And** comments, command bodies, event payloads, personal data, secrets, and magic-link tokens are not logged.

**Given** command and query performance targets are launch-relevant
**When** the initial test infrastructure is scaffolded
**Then** it includes a place for performance evidence covering `500 ms p95` common command acknowledgements and `2s p95` common report queries
**And** the harness is isolated so later stories can add realistic tenant/project/period data without slowing the fast unit baseline.

**Given** the scaffold is complete
**When** restore, build, and the initial architecture/unit test lane are run
**Then** the solution builds with warnings as errors
**And** the test baseline passes or any infrastructure-dependent tests are clearly isolated from the fast baseline.

### Story 1.2: Enforce Tenant and Resource Authorization Gates

**Requirements:** FR2, FR12, FR21, FR22, FR23

As a Hexalith security implementer,
I want tenant and resource authorization gates in place before feature commands and queries expand,
So that every future Timesheets action fails closed from the first executable slice.

**Acceptance Criteria:**

**Given** a Timesheets command, query, projection read, export request, or confirmation request enters the host
**When** tenant, user, Project, Work, or Party authority cannot be resolved
**Then** the request fails closed before aggregate load, command dispatch, projection disclosure, export, or magic-link disclosure
**And** the denial does not reveal protected tenant, contributor, target, period, or entry details.

**Given** a caller supplies tenant, user, or authorization context in a request body
**When** authorization is evaluated
**Then** caller-supplied server-controlled fields are treated as untrusted input
**And** authority comes from host/server policy, Tenants, Projects, Works, Parties, and Timesheets policy sources.

**Given** tenant/resource authorization tests run
**When** they cover missing tenant, disabled tenant, unknown user, non-member, insufficient role, cross-tenant target IDs, stale projections, and unavailable sibling authority
**Then** all unauthorized, stale, ambiguous, or unavailable cases fail closed.

**Given** a write command is authorized
**When** the command reaches domain handling
**Then** EventStore remains the only persistence authority
**And** the command does not copy Party personal data, Project state, Work lifecycle state, or Tenant membership state into Timesheets.

**Given** UI actions are rendered for a user
**When** the user lacks authority for capture, catalog changes, approval, correction, confirmation, report, ledger, or export actions
**Then** FrontComposer/Fluent UI V5 surfaces hide or disable the action according to policy
**And** copy remains specific enough to guide action without protected detail disclosure.

### Story 1.3: Publish Time Capture API Contracts and FrontComposer Metadata

**Requirements:** FR1, FR10, FR11, FR15, FR21, FR22, FR23

As an API, SDK, or UI consumer,
I want stable Timesheets capture and catalog contracts with generated UI metadata,
So that integrations can record and manage time evidence without learning EventStore internals.

**Acceptance Criteria:**

**Given** capture and catalog commands, events, queries, value objects, and read models are published
**When** consumers reference `Hexalith.Timesheets.Contracts`
**Then** contracts expose Timesheets concepts only
**And** EventStore envelope mechanics, aggregate internals, projection rebuild mechanics, and infrastructure types are hidden.

**Given** a command or query accepts tenant, user, correlation, or authorization context
**When** the public contract is reviewed
**Then** server-controlled context is not accepted as caller authority
**And** tenant/resource authority is resolved by host/server policy instead.

**Given** API or SDK consumers use Timesheets capture contracts
**When** they submit commands or queries
**Then** stable DTOs support Time Entry capture, Activity Type catalog management, AI metrics, and Time Entry evidence reads
**And** feature-specific metadata can be extended by later stories without breaking additive contract evolution.

**Given** FrontComposer metadata foundations are generated or registered for capture and catalog workflows
**When** internal UI surfaces are composed
**Then** they use FrontComposer command/projection patterns and Fluent UI V5-compatible metadata
**And** feature stories remain responsible for their own detailed UI fields, validation states, and workflow-specific metadata.

**Given** contract and metadata tests run
**When** package boundary, consumer-driven contract, and UI conformance checks execute
**Then** they prove Contracts remains infrastructure-free, no inline package versions are added, Fluent UI V4 components are not introduced, and public metadata smoke tests pass.

**Given** API documentation or OpenAPI artifacts are produced
**When** consumers inspect the public surface
**Then** docs describe Timesheets commands, queries, states, and validation outcomes
**And** they do not expose EventStore internals or imply Timesheets owns Party, Project, Work, Tenant, invoice, payroll, rate, or revenue state.

### Story 1.4: Establish Evidence Retention and Comment Sensitivity Policy

**Requirements:** FR3, FR21, FR23

> **Policy (ratified 2026-06-19):** v1 default — Time Entry/approval/correction events retained as indefinite audit evidence; export records and magic-link audit metadata per documented tenant default; **legal-hold override is an explicit launch-readiness gate** requiring tenant/legal sign-off (PRD §9, NFR7/GOV-7).

As a compliance-minded tenant operator,
I want explicit retention and comment sensitivity policy for time evidence,
So that Time Entries, comments, exports, and confirmation metadata are handled consistently before trusted capture expands.

**Acceptance Criteria:**

**Given** Timesheets stores Time Entry evidence, comments, export records, and magic-link confirmation metadata
**When** policy defaults are configured
**Then** retention categories and default retention behavior are documented in configuration and public guidance
**And** unresolved legal-hold or tenant-specific overrides are visible as launch-readiness gaps rather than hidden assumptions.

**Given** comments may contain customer/private data
**When** a Time Entry, correction, rejection, or export includes comments
**Then** comment sensitivity rules define where comments may be displayed, exported, retained, redacted, or excluded
**And** the rules do not copy Party personal data or sibling-owned Project/Work state into Timesheets.

**Given** logs, traces, support diagnostics, projections, and exports are produced
**When** they include operational metadata
**Then** comments, event payloads, command bodies, personal data, token values, and secrets remain excluded unless an explicitly authorized export policy allows comment fields
**And** redaction/logging tests prove the exclusion.

**Given** retention or comment policy is missing for a trust-bearing action
**When** approval, correction, magic-link confirmation, or export would depend on that policy
**Then** the action is blocked or marked not launch-ready according to configured policy
**And** users receive consequence-aware copy without protected detail leakage.

**Given** policy information appears in UI
**When** contributors, approvers, or finance users encounter comments or exports
**Then** FrontComposer/Fluent UI V5 surfaces use message bars, field help, or export review text to explain the relevant policy
**And** the copy avoids invoice, payroll, rate, tax, and revenue-recognition ownership language.

### Story 1.5: Configure Tenant Activity Types

**Requirements:** FR10

As an authorized tenant operator,
I want to define and maintain tenant-level Activity Types,
So that contributors can classify time consistently without rewriting historical evidence.

**Acceptance Criteria:**

**Given** an authorized tenant operator is in a valid tenant context
**When** they create a tenant-level Activity Type with a stable ID, display label, active state, and optional billable-default metadata
**Then** the change is persisted through Hexalith.EventStore as an auditable domain event
**And** the Activity Type is available for future tenant-scoped Time Entry capture.

**Given** an existing tenant-level Activity Type
**When** an authorized tenant operator updates its display label or billable-default metadata
**Then** a new event records the change
**And** historical Time Entry facts continue to reference the stable Activity Type ID rather than being rewritten.

**Given** an existing tenant-level Activity Type
**When** an authorized tenant operator deactivates it
**Then** the Activity Type is unavailable for new Time Entries
**And** it remains visible and reportable for historical entries.

**Given** a caller lacks tenant authority or supplies a tenant context they do not control
**When** they attempt to create, update, or deactivate a tenant Activity Type
**Then** the command fails closed
**And** no Activity Type event or projection change is produced.

**Given** the tenant Activity Type catalog projection is queried
**When** read models are returned
**Then** they include projection freshness metadata and active/inactive status text
**And** stale, rebuilding, or unavailable projections are not presented as fresh catalog authority.

**Given** Activity Type catalog UI is generated or composed
**When** the catalog is displayed
**Then** it uses FrontComposer/Fluent UI V5 surfaces with a dense grid or projection view, visible status badges, keyboard reachable commands, and no color-only state.

### Story 1.6: Configure Project Activity Types

**Requirements:** FR2, FR11

As an authorized project operator,
I want to define project-scoped Activity Types,
So that project-specific work can be categorized without copying Project state into Timesheets.

**Acceptance Criteria:**

**Given** an authorized project operator has access to a Project Reference
**When** they create a project-level Activity Type
**Then** the Activity Type is scoped to the tenant and Project Reference
**And** Timesheets stores the stable Project ID only, not copied project name, hierarchy, lifecycle, or ownership state.

**Given** a project-level Activity Type exists
**When** its display label, active state, or billable-default metadata changes
**Then** the change is persisted through EventStore as an auditable domain event
**And** historical Time Entry references remain tied to the stable Activity Type ID.

**Given** a project explicitly restricts its catalog
**When** a contributor selects an Activity Type for that project
**Then** only allowed tenant-level and project-level Activity Types are available
**And** restricted or inactive Activity Types cannot be selected for new entries.

**Given** a Project Reference cannot be verified through the Projects boundary for a trust-bearing catalog change
**When** the command is submitted
**Then** the command fails closed
**And** no project Activity Type is created or changed.

**Given** reports group by Activity Type
**When** tenant-level and project-level Activity Types share a display label
**Then** reports group by stable Activity Type IDs and scopes
**And** display labels do not merge distinct catalog concepts.

**Given** the project Activity Type catalog UI is displayed
**When** users filter or inspect project-scoped entries
**Then** the surface uses FrontComposer/Fluent UI V5 patterns, shows scope and active state text, preserves filters during drill-in/back navigation, and remains keyboard accessible.

### Story 1.7: Record Draft Time Entry Against Project or Work

**Requirements:** FR1, FR2, FR12, FR21

> **Dependency (2026-06-19):** the Work-reference path requires a `Hexalith.Works` consumer read/validate query (see architecture "Reference-validation adapter maturity"). The Project path via `GetProjectAsync` is unblocked; the Work path is gated on the Works `GetWorkItem`-query vs. adapter-bridge decision.

As a contributor,
I want to record draft time against exactly one Project or Work reference,
So that my work evidence is captured early with the right contributor, activity, duration, and billable context.

**Acceptance Criteria:**

**Given** a contributor is authorized in a tenant context
**When** they record a draft Time Entry with date, positive whole-minute duration, Contributor Party ID, Activity Type, comment, Billable Flag, and exactly one Target Reference
**Then** Timesheets persists the change through EventStore as a durable domain event
**And** the resulting projection exposes the draft Time Entry with projection freshness metadata.

**Given** a draft Time Entry command contains both a Project Reference and a Work Reference, or neither
**When** the command is handled
**Then** the command is rejected as a domain outcome
**And** no successful Time Entry event is emitted.

**Given** a draft Time Entry command has a non-positive human/external duration or missing required capture fields
**When** the command is handled
**Then** validation rejects the command with field-specific errors
**And** no partial Time Entry state is persisted.

**Given** a Contributor Party ID is supplied
**When** Timesheets records the entry
**Then** the Party reference is validated at the boundary according to the configured policy
**And** Timesheets stores the Party ID only, not Party display name, contact details, or profile data.

**Given** a Project or Work target cannot be verified for a trust-bearing write
**When** the Time Entry is submitted for a trust-bearing state
**Then** the command fails closed
**And** draft capture behavior only tolerates stale display hydration where policy explicitly allows it.

**Given** a user records time from a Project/Work context, dashboard, or command surface
**When** the capture UI is shown
**Then** it uses FrontComposerGeneratedForm or equivalent Fluent UI V5 components, states duration units clearly, validates fields beside inputs, and preserves entered values after interrupted commands.

**Given** logs and telemetry are emitted during capture
**When** the command succeeds or fails
**Then** logs include correlation-safe metadata only
**And** comments, command bodies, event payloads, personal data, target names, and secrets are not logged.

### Story 1.8: Display Time Entry Evidence from Read Models

**Requirements:** FR3, FR12, FR21, FR23

As a contributor or reviewer,
I want to view recorded Time Entry evidence and how it was produced,
So that I can trust the entry without assuming Timesheets owns sibling module data.

**Acceptance Criteria:**

**Given** one or more Time Entry events exist for an entry
**When** the Time Entry detail is queried
**Then** the read model shows current evidence, approval state, contributor Party ID, target reference, Activity Type, billable flag, date, duration, comment where allowed, and event lineage
**And** it identifies EventStore events as the source of authority.

**Given** sibling display data is available from Parties, Projects, or Works
**When** the Time Entry detail is rendered
**Then** display labels may be hydrated at read time
**And** the durable Timesheets event data remains stable references only.

**Given** sibling display hydration is stale, unavailable, or unauthorized
**When** the Time Entry detail is rendered
**Then** the UI shows an explicit stale, unavailable, or denied state
**And** it does not substitute copied or guessed Party, Project, or Work data.

**Given** Time Entry projections receive duplicate or replayed events
**When** projections rebuild
**Then** the resulting read model is idempotent and equivalent to the expected event lineage
**And** duplicate delivery does not create duplicate entry records.

**Given** a user opens Time Entry Detail
**When** evidence, approval, AI metrics, correction lineage, or audit metadata sections are present
**Then** the surface uses FrontComposer/Fluent UI V5 components with accordions for multiple titled sections, visible status badges, projection freshness, and keyboard navigation.

**Given** unauthorized or cross-tenant access is attempted
**When** Time Entry evidence is queried
**Then** the request fails closed with no protected identifiers or sibling-owned data disclosed
**And** the denial is covered by adversarial tenant-isolation tests.

### Story 1.9: Project AI-Assisted Time Capture Metrics

**Requirements:** FR12, FR15

As an AI agent operator,
I want AI-agent Time Entries to capture wall-clock, runtime, billable effort, and token metrics separately,
So that automation effort is visible without converting tokens or runtime into human hours.

**Acceptance Criteria:**

**Given** an AI-agent contributor is represented by a valid Party ID
**When** an AI Time Entry is recorded
**Then** the entry can include wall-clock execution time, model/tool runtime, billable effort, and provider-reported input/output/total token counts
**And** each metric carries explicit units and source metadata.

**Given** provider token metrics are unavailable
**When** the AI Time Entry is recorded or displayed
**Then** token fields are represented as unavailable or not reported
**And** the system never stores or displays missing provider metrics as zero.

**Given** AI effort metrics are included on a Time Entry
**When** the domain event is persisted
**Then** the event remains additive and serialization-tolerant
**And** AI metrics do not change the authoritative human/external duration semantics.

**Given** an AI-agent command attempts to bypass tenant, Party, Project, Work, Activity Type, approval, or audit rules
**When** the command is handled
**Then** the command fails closed
**And** AI-agent capture follows the same reference-validation and tenant-isolation rules as human/external capture.

**Given** AI metrics are shown in Time Entry Detail or AI effort surfaces
**When** users inspect the entry
**Then** AI wall-clock, model/tool runtime, billable effort, and token metrics are visually separated from human/external duration
**And** unavailable metrics are shown with explicit text, not silence or color-only signals.

**Given** telemetry is emitted for AI capture
**When** metrics are accepted or rejected
**Then** logs do not include token values, prompts, responses, command bodies, comments, secrets, or personal data
**And** only correlation-safe operational metadata is recorded.

## Epic 2: Submission, Approval, Period Review & Corrections

Contributors can submit entries and periods; approvers can approve or reject entries and periods; approved entries are locked from direct edit; corrections preserve evidence and lineage.

### Story 2.1: Submit Draft Time Entries for Approval

**Requirements:** FR4

As a contributor,
I want to submit draft Time Entries for approval,
So that recorded effort can move from private capture into reviewable evidence.

**Acceptance Criteria:**

**Given** a contributor owns or is authorized to submit one or more Draft Time Entries in a tenant
**When** they submit the entries for approval
**Then** each valid entry transitions from Draft to Submitted through EventStore-backed domain events
**And** submitter, timestamp, tenant scope, and submission scope are recorded.

**Given** a Draft Time Entry is missing required fields, an active Activity Type, required comments, or a valid trust-bearing Project/Work reference
**When** the contributor attempts submission
**Then** only the invalid entry is blocked where policy allows partial submission
**And** field-level correction information is returned without mutating valid entries.

**Given** tenant or resource authority cannot be resolved
**When** a submission command is handled
**Then** the command fails closed
**And** no Submitted event or projection update is produced.

**Given** a submission command is retried with the same idempotency context
**When** the command is processed more than once
**Then** the resulting state remains a single Submitted transition
**And** duplicate events or duplicate projection rows are not created.

**Given** the contributor uses the internal UI to submit entries
**When** validation fails or succeeds
**Then** FrontComposer/Fluent UI V5 surfaces show entry status, blocking fields, projection freshness, and persistent message-bar state where needed
**And** entered values and filters remain available after an interrupted command.

### Story 2.2: Enforce Timesheet Approval Authority Policy

**Requirements:** FR5, FR7, FR9

As a tenant or project governance owner,
I want Timesheets to resolve who can approve which entries and periods before review actions occur,
So that approval decisions fail closed and follow a clear policy rather than ad hoc role checks.

**Acceptance Criteria:**

**Given** tenant admins, project managers, work owners, finance reviewers, and contributors may overlap
**When** approver authority is evaluated
**Then** Timesheets applies an explicit authority policy with precedence rules for entry approval, period approval, rejection, correction, and export eligibility
**And** the authority source used for a decision is recorded where approval evidence requires it.

**Given** self-approval is denied by default
**When** a contributor attempts to approve their own entry or period
**Then** the command is rejected unless policy explicitly allows self-approval for that context
**And** the rejection is auditable without leaking protected cross-tenant detail.

**Given** Tenants, Project, or Work authority projections are stale, unavailable, ambiguous, or contradictory
**When** a trust-bearing approval decision is attempted
**Then** the decision fails closed
**And** the UI exposes unresolved authority or stale evidence as a blocking state.

**Given** approver authority policy changes
**When** the policy is configured or updated
**Then** future approval decisions use the new policy
**And** previously recorded approval evidence remains attributable to the policy/source in effect at decision time.

**Given** approval surfaces display available actions
**When** the current user lacks authority
**Then** approve/reject controls are disabled or hidden according to policy
**And** copy is specific enough to guide action without exposing protected identifiers or sibling-owned state.

**Given** authority resolution tests run
**When** they cover tenant admin, project approver, work owner, finance reviewer, contributor self-approval, missing user, disabled tenant, stale projection, and cross-tenant attempts
**Then** all unauthorized, stale, or ambiguous cases fail closed.

### Story 2.3: Approve or Reject Submitted Time Entries

**Requirements:** FR5, FR9

As an approver,
I want to approve or reject submitted Time Entries with policy-governed reasons,
So that reviewed effort becomes trusted evidence or returns to the contributor for correction.

**Acceptance Criteria:**

**Given** a Submitted Time Entry and an approver with resolved tenant and Project/Work authority
**When** the approver approves the entry
**Then** the entry transitions to Approved through an EventStore-backed domain event
**And** approver, timestamp, authority source, and approval scope are recorded.

**Given** a Submitted Time Entry requires rejection or clarification
**When** the approver rejects the entry with a required reason
**Then** the entry transitions to Rejected through an EventStore-backed domain event
**And** the rejection reason, approver, timestamp, and affected entry ID are preserved for later correction.

**Given** self-approval is not explicitly allowed by policy
**When** a contributor attempts to approve their own submitted entry
**Then** the approval command is rejected
**And** no Approved event or ledger-eligible projection state is produced.

**Given** approver authority cannot be resolved from Tenants, Project, or Work context
**When** an approval or rejection command is handled
**Then** the command fails closed
**And** the UI shows unresolved authority without disclosing protected cross-tenant details.

**Given** approval and rejection states are displayed in an Approvals Queue or Time Entry Detail
**When** users review the entry
**Then** status badges include text, projection freshness is visible, dialogs focus required fields, and keyboard users can approve or reject without hover-only controls.

### Story 2.4: Correct Rejected Entries for Resubmission

**Requirements:** FR3, FR4, FR8

As a contributor,
I want to correct rejected entries after review,
So that specific problems can be resolved without losing the original rejection evidence.

**Acceptance Criteria:**

**Given** a submitted Time Entry is Rejected
**When** the contributor opens the correction flow
**Then** the prior values and rejection reason are visible where authorized
**And** the correction is saved as an additive EventStore-backed event, not a direct edit.

**Given** a rejected entry correction is submitted
**When** the correction changes date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or AI metrics
**Then** original and corrected values are linked in lineage
**And** the entry can be resubmitted according to policy without deleting the rejection reason.

**Given** a correction would cross tenant boundaries, use an inactive/disallowed Activity Type, or reference unverifiable Project/Work/Party data for a trust-bearing change
**When** the command is handled
**Then** it fails closed
**And** no partial correction is persisted.

**Given** correction events replay into read models
**When** projections rebuild
**Then** rejected, corrected, and resubmitted states remain deterministic and idempotent
**And** projection freshness is visible wherever correction state is displayed.

**Given** the correction UI is displayed
**When** users inspect the rejected entry and correction form
**Then** FrontComposer/Fluent UI V5 components show rejection reason, correction lineage, field validation, consequence-aware copy, labels, focus order, and keyboard-accessible actions
**And** no copy implies approved evidence can be directly edited.

### Story 2.5: Enforce Approved Entry Locking

**Requirements:** FR6, FR8

As a contributor or reviewer,
I want approved entries to be locked from direct edits,
So that approved evidence remains trustworthy once it has become review evidence.

**Acceptance Criteria:**

**Given** a Time Entry is Approved
**When** a user attempts to directly edit date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or Approval State
**Then** the command is rejected as a domain outcome
**And** no approved event history is rewritten.

**Given** an approved entry is included in a period approval or ledger-eligible state
**When** lock state is evaluated
**Then** the lock decision comes from EventStore-backed domain state and approved entry events
**And** the Timesheet Period aggregate does not duplicate entry state as authority.

**Given** lock enforcement tests run
**When** they cover Draft, Submitted, Rejected, Approved, Corrected, Superseded, duplicate command, concurrent command, and cross-tenant attempts
**Then** only allowed transitions succeed
**And** invalid transitions produce typed domain rejections.

**Given** lock state is displayed in Time Entry Detail, period review, reports, or ledger reads
**When** projections are stale, rebuilding, degraded, or unavailable
**Then** lock state is shown with projection freshness
**And** stale read models are not used as write authority.

### Story 2.6: Add Approved Entry Corrections

**Requirements:** FR3, FR6, FR8

As an authorized contributor or reviewer,
I want approved entries to be corrected through additive events,
So that mistakes can be fixed without editing approved evidence in place.

**Acceptance Criteria:**

**Given** an Approved Time Entry needs a correction
**When** an authorized user adds a correction with corrected values and a reason where required
**Then** a compensating correction event is persisted through EventStore
**And** original values, corrected values, actor, timestamp, reason, and lineage to the affected entry are preserved.

**Given** a correction supersedes prior effective values
**When** Time Entry Detail or reports show the entry
**Then** users can see current effective values and correction lineage
**And** the UI does not imply the approved record was edited in place.

**Given** a correction command is submitted by an unauthorized user or across tenants
**When** the command is handled
**Then** the command fails closed
**And** no correction event, projection update, or protected details are disclosed.

**Given** projection handlers replay approval and correction events
**When** duplicate or out-of-order deliveries are encountered within supported replay rules
**Then** the effective read model remains idempotent
**And** projection freshness reflects rebuilding or degraded states when trust is limited.

### Story 2.7: Submit Timesheet Periods

**Requirements:** FR4, FR7, FR8

> **Policy (ratified 2026-06-19):** canonical period policy = **tenant time zone** (UTC audit instants + tenant-local period keys); DST/period-boundary cases are launch gates proven by golden files (PRD §10 / §14 Q1, NFR15).

As a contributor,
I want to submit a weekly or monthly Timesheet Period containing my entries,
So that my work can be reviewed as a coherent period without losing entry-level evidence.

**Acceptance Criteria:**

**Given** a contributor has draft or submitted entries within a tenant-policy period boundary
**When** they submit a Timesheet Period
**Then** the period is scoped to one Contributor, one Tenant, and one weekly or monthly period
**And** the included Time Entry IDs and period boundary are recorded through EventStore-backed events.

**Given** period boundaries are calculated
**When** a Timesheet Period is created or submitted
**Then** the calculation uses the configured tenant time-zone/period policy
**And** UTC audit instants remain separate from tenant-local period keys.

**Given** one or more included entries are invalid for submission
**When** the contributor submits the period
**Then** blocking entries are identified with correction guidance
**And** valid entries and review context are not silently discarded.

**Given** a contributor attempts to submit a period for another contributor or tenant without authority
**When** the command is handled
**Then** it fails closed
**And** no period submission event or cross-tenant details are produced.

**Given** the contributor reviews My Timesheet Period in the UI
**When** the period contains Draft, Submitted, Rejected, Corrected, or Superseded entries
**Then** period state is shown separately from entry states
**And** status badges, filters, required-field markers, and keyboard navigation remain accessible.

### Story 2.8: Approve or Reject Timesheet Periods

**Requirements:** FR5, FR7, FR8, FR9

As an approver,
I want to approve or reject a submitted Timesheet Period while preserving entry-level decisions,
So that grouped review evidence does not flatten mixed entry states.

**Acceptance Criteria:**

**Given** a submitted Timesheet Period and an approver with resolved authority
**When** the approver approves the period
**Then** the period approval is recorded through EventStore-backed events
**And** included approved entries become locked from direct edit without erasing entry-level states.

**Given** a submitted Timesheet Period contains entries needing rejection
**When** the approver rejects the period or selected entries with required reasons
**Then** rejection evidence is recorded with approver, timestamp, reason, affected entries, and period scope
**And** rejected entries retain a correction path for the contributor.

**Given** a period contains mixed Approved, Rejected, Corrected, or Superseded entries
**When** the Period Approval Detail is queried or displayed
**Then** the UI shows period state and entry states separately
**And** no single period badge hides mixed entry evidence.

**Given** approver authority cannot be resolved or projection state is stale beyond trust policy
**When** period approval is attempted
**Then** approval fails closed
**And** the UI explains unresolved authority or stale evidence without exposing protected identifiers.

**Given** period approval events replay into projections
**When** projections rebuild
**Then** period summary, entry state, and lock state remain consistent and idempotent
**And** rebuilding/freshness status is available to approval and review surfaces.

## Epic 3: External Contributor Confirmation

External contributors can submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.

### Story 3.1: Expose External Contributor Confirmation API

**Requirements:** FR12, FR13

As an external contributor integration,
I want to submit or confirm Time Entries through an API-only surface,
So that external effort can enter the same approval workflow without granting full internal access.

**Acceptance Criteria:**

**Given** an external contributor API caller has tenant-scoped authorization and a valid Contributor Party reference
**When** they submit or confirm a Time Entry with required capture fields
**Then** the entry is persisted through the same EventStore-backed Time Entry flow as internal entries
**And** it enters the configured Draft or Submitted workflow state according to policy.

**Given** an external API request lacks tenant authority, uses an invalid Party reference, or references unverifiable Project/Work data for a trust-bearing write
**When** the command is handled
**Then** it fails closed
**And** no entry, projection update, or protected target/contributor detail is disclosed.

**Given** an external API-created entry enters review
**When** approvers inspect it
**Then** the entry shows external contributor category, source metadata, Party ID, target reference, Activity Type, billable flag, and approval state
**And** it follows the same approval, rejection, correction, locking, and audit rules as internal entries.

**Given** an external contributor confirms time through the API
**When** the confirmation is accepted
**Then** confirmation is recorded as contributor evidence, not approval
**And** approval still requires the configured Timesheets approval workflow.

**Given** external API requests are retried or duplicated
**When** idempotency context matches a prior accepted command
**Then** the system avoids duplicate Time Entries or duplicate submitted evidence
**And** projection replay remains idempotent.

**Given** telemetry is emitted for external submission
**When** requests succeed or fail
**Then** logs contain correlation-safe outcome metadata only
**And** comments, command bodies, personal data, token values, target names, and protected identifiers are not logged.

### Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities

**Requirements:** FR14

> **Policy (ratified 2026-06-19):** v1 magic-link baseline = single-use scoped expiring links (FR-14); **secondary identity verification for high-value/billable entries is deferred to post-v1** (explicit assumption — PRD §14 Q4).

As an authorized internal user,
I want to issue a scoped Magic-Link Confirmation capability for one external contribution,
So that an external contributor can confirm or adjust proposed time without broader tenant access.

**Acceptance Criteria:**

**Given** an authorized user has permission to request external confirmation for a specific contribution
**When** they issue a magic link
**Then** Timesheets creates a server-generated opaque capability scoped to tenant, Contributor Party, proposed entry or entry, allowed action, expiry, and single-use state
**And** only a token hash and capability metadata are stored.

**Given** a magic link is issued
**When** audit evidence is persisted
**Then** issue metadata is recorded through EventStore-backed events
**And** token values or decoded capability material are not logged, projected, exported, or stored in comments.

**Given** the issuer lacks tenant/resource authority or the proposed confirmation references invalid Project, Work, Party, or Activity Type data
**When** link issuance is attempted
**Then** the command fails closed
**And** no usable capability is produced.

**Given** a magic-link capability exists
**When** it is revoked or expires by policy
**Then** the state change is auditable
**And** future use of the link follows the same no-disclosure invalid-state response.

**Given** operators view issued confirmation requests
**When** the internal UI displays link state
**Then** it uses FrontComposer/Fluent UI V5 status badges with text, expiry state, audit metadata, and no raw token values.

### Story 3.3: Confirm Time Through Magic Link

**Requirements:** FR13, FR14

As an external contributor,
I want to confirm a scoped proposed Time Entry from a magic link,
So that my effort can be attributed and reviewed without internal account access.

**Acceptance Criteria:**

**Given** a magic-link token is valid, unexpired, unused, and scoped to one confirmation
**When** the external contributor opens the confirmation page
**Then** Timesheets validates the token before displaying details
**And** shows only the proposed date, duration, Activity Type, comment, Billable Flag, and minimal target context needed for this confirmation.

**Given** the external contributor confirms the proposed entry
**When** they submit confirmation
**Then** the entry is attributed to the scoped Contributor Party
**And** confirmation use, timestamp, source, and resulting Time Entry state are recorded through EventStore-backed audit events.

**Given** the external contributor confirms scoped time
**When** the confirmation succeeds
**Then** confirmation is recorded as contributor evidence, not approval
**And** the Time Entry still enters the configured Timesheets approval workflow.

**Given** the confirmation has been accepted
**When** the same token is used again
**Then** reuse is rejected with the generic no-disclosure invalid-link response
**And** no previous confirmation details are shown.

**Given** the external confirmation page is used on a phone viewport
**When** the contributor reviews or confirms the proposed entry
**Then** the page remains fully usable with Fluent UI V5 components, clear duration units, accessible focus order, keyboard/touch reachable controls, and no internal shell navigation.

### Story 3.4: Adjust Time Through Magic Link

**Requirements:** FR13, FR14

As an external contributor,
I want to adjust allowed fields in a scoped magic-link confirmation,
So that proposed time can be corrected before it enters approval without granting internal access.

**Acceptance Criteria:**

**Given** a magic-link token is valid, unexpired, unused, tenant-scoped, and allows adjustment
**When** the external contributor opens the adjustment flow
**Then** only policy-allowed fields are editable
**And** internal-only fields, approval state, tenant context, and broader Timesheets navigation remain unavailable.

**Given** the external contributor submits adjusted time
**When** the command is handled
**Then** the adjustment follows the same EventStore-backed validation, target-reference, Activity Type, tenant-isolation, and audit rules as Time Entry capture
**And** confirmation remains distinct from approval.

**Given** an adjusted value uses an invalid duration, disallowed Activity Type, unauthorized target, or cross-tenant reference
**When** the adjustment command is handled
**Then** the command fails closed
**And** no partial Time Entry, confirmation use, or protected details are persisted.

**Given** adjustment telemetry and audit evidence are recorded
**When** the adjustment succeeds or fails
**Then** token values, decoded capability material, comments beyond policy, command bodies, personal data, and target names are not logged
**And** only hashed/scoped references and outcome categories are available where policy allows.

**Given** the adjustment page is used on phone or keyboard-only navigation
**When** the contributor changes allowed fields and submits
**Then** Fluent UI V5 components provide labels, validation messages, focus order, clear units, and accessible action controls
**And** no hover-only or color-only state is introduced.

### Story 3.5: Reject Invalid Confirmation Links Without Resource Disclosure

**Requirements:** FR14

As an external contributor or support operator,
I want invalid, expired, used, revoked, unauthorized, or unknown magic links to reveal nothing sensitive,
So that external confirmation cannot be used to probe tenant, Project, Work, Party, Time Entry, or duration details.

**Acceptance Criteria:**

**Given** a magic-link token is invalid, expired, used, revoked, unauthorized, malformed, or unknown
**When** the link is opened
**Then** the external page returns the same neutral failure state
**And** it reveals no tenant, Project, Work, Party, Time Entry, duration, comment, Activity Type, or approval details.

**Given** repeated invalid link attempts occur
**When** abuse detection or rate limiting applies
**Then** the response remains no-disclosure
**And** operational telemetry records only correlation-safe outcome categories.

**Given** an invalid-link failure is displayed
**When** the user reads the page
**Then** the copy provides one safe recovery path without account-like navigation
**And** it does not reveal whether the link ever existed or why it failed.

**Given** invalid-link telemetry, support diagnostics, or audit events are reviewed
**When** operators inspect records
**Then** token values, decoded token material, comments, command bodies, personal data, and target names are absent
**And** only hashed/scoped references and outcome categories are available where policy allows.

**Given** no-disclosure behavior is tested
**When** test cases cover expired, used, revoked, unauthorized, malformed, unknown, cross-tenant, wrong-recipient, repeated-token, and enumeration attempts
**Then** all cases produce equivalent external disclosure behavior
**And** only authorized internal audit views can distinguish failure categories.

## Epic 4: Approved Time Ledger, Reporting & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

### Story 4.1: Query Time Entries from Rebuildable Read Models

**Requirements:** FR16

As an authorized operational user,
I want to query Time Entries by contributor, target, period, Activity Type, billable flag, approval state, and source type,
So that I can find and review the effort evidence relevant to my work.

**Acceptance Criteria:**

**Given** an authorized user queries Time Entries within a tenant
**When** they filter by Contributor, Project Reference, Work Reference, period, Activity Type, Billable Flag, Approval State, or source type
**Then** Timesheets returns matching read models with stable IDs and projection freshness metadata
**And** result-level authorization is enforced.

**Given** a query includes Draft, Rejected, Corrected, Superseded, or Approved state options
**When** results are returned
**Then** each entry state is explicit
**And** users can include or exclude non-current states according to query options.

**Given** a query crosses tenant boundaries or requests unauthorized target data
**When** it is handled
**Then** it fails closed or filters results according to policy
**And** no protected Party, Project, Work, or Time Entry details are disclosed.

**Given** projection state is stale, rebuilding, degraded, or unavailable
**When** query results are shown
**Then** freshness/trust metadata is visible in the API and UI
**And** stale data is not presented as fresh decision authority.

**Given** the operational query UI is displayed
**When** users filter, sort, page, drill into detail, and navigate back
**Then** it uses FrontComposerProjectionView or FluentDataGrid, preserves filters, provides keyboard traversal, and shows status badges with text.
**And** it does not expose raw EventStore stream browsing as a Timesheets UI path.

### Story 4.2: Project Approved Time Ledger from Domain Events

**Requirements:** FR18, FR19

As a finance or audit consumer,
I want an Approved-Time Ledger projection with approval metadata and correction lineage,
So that approved effort can be used as trusted downstream evidence without becoming separate authoritative storage.

**Acceptance Criteria:**

**Given** Time Entries are approved
**When** approval events are projected
**Then** the Approved-Time Ledger includes Time Entry ID, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comment where policy allows
**And** EventStore remains the source of authority.

**Given** an approved entry is corrected or superseded
**When** ledger projections rebuild
**Then** original and corrected/superseded evidence remains visible with lineage
**And** query options can include or exclude superseded entries.

**Given** duplicate, replayed, or rebuilt event streams are processed
**When** the Approved-Time Ledger projection is regenerated
**Then** the ledger output is deterministic and idempotent
**And** it does not accumulate duplicate rows.

**Given** ledger reads depend on projection state
**When** the projection is stale, rebuilding, degraded, or unavailable
**Then** the API and UI expose freshness/trust metadata
**And** finance export actions cannot treat stale data as fresh unless explicitly allowed by policy.

**Given** unauthorized or cross-tenant ledger access is attempted
**When** the ledger is queried
**Then** access fails closed or filters according to policy
**And** no protected contributor, target, comment, or ledger details are disclosed.

**Given** the Approved-Time Ledger UI is displayed
**When** users review approved evidence
**Then** FrontComposer/Fluent UI V5 surfaces show stable IDs, approval metadata, billable status, comments where allowed, correction lineage, filters, projection freshness, keyboard flow, labels, and focus management.

### Story 4.3: Produce Project and Work Actual-Time Reports

**Requirements:** FR16, FR17

> **Dependency (2026-06-19):** planned-vs-actual consumes `WorkItemEffort` from `Hexalith.Works`; actual read access depends on the Works consumer-query decision (see architecture "Reference-validation adapter maturity").

As a project or work reviewer,
I want actual-time reports by Project and Work item,
So that I can compare effort against operational expectations without Timesheets owning Project or Work state.

**Acceptance Criteria:**

**Given** approved or selected Time Entries exist for Project References
**When** a Project actual-time report is queried
**Then** Timesheets rolls up time by Project Reference, period, Contributor, Activity Type, Billable Flag, Approval State, and contributor category
**And** it does not require copied Project hierarchy or lifecycle state.

**Given** Time Entries exist for Work References and Works provides planned or estimated effort
**When** a Work actual-time report is queried
**Then** Timesheets can compare actual time with Works-provided planned/estimated effort
**And** the report identifies Works as the source of planned/estimated values.

**Given** Works or Projects display hydration is stale, unavailable, or unauthorized
**When** reports are rendered
**Then** the report shows reference-state and freshness information
**And** it does not guess or persist copied display state.

**Given** report projections receive duplicate or replayed events
**When** projections rebuild
**Then** rollups remain idempotent and deterministic
**And** freshness states indicate rebuilding or unavailable data where appropriate.

**Given** approved ledger data is available
**When** actual-time reports need approved evidence
**Then** reports consume rebuildable read models or approved-ledger projections
**And** they do not re-create approval authority from raw entry state.

**Given** report users interact with filter-heavy surfaces
**When** they change filters or switch related report views
**Then** FrontComposer/Fluent UI V5 components provide FilterBar, FluentDataGrid, status badges, related tabs where appropriate, and accessible keyboard/focus behavior.

**Given** common report filters are run against realistic tenant/project/period data
**When** performance tests execute
**Then** common report queries target 2 seconds p95 according to launch sizing assumptions
**And** deviations are visible as quality evidence rather than hidden implementation details.

**Given** report contract tests run
**When** seeded Project and Work report scenarios are compared
**Then** stable ordering, period boundary behavior, redaction rules, and selected report fields are verified by deterministic fixtures or golden files.

### Story 4.4: Surface AI Effort Reporting

**Requirements:** FR15, FR20

As an AI agent operator or project reviewer,
I want AI effort reported separately from human and external effort,
So that automation cost and runtime are visible without implying all units are interchangeable.

**Acceptance Criteria:**

**Given** Time Entries include AI effort metrics
**When** AI effort reports are queried
**Then** reports include wall-clock time, model/tool runtime, billable effort, and provider-reported token counts where available
**And** each metric preserves its explicit unit and source metadata.

**Given** AI token metrics are unavailable or not reported by a provider
**When** AI reports or Time Entry Detail display those fields
**Then** the UI shows `Unavailable` or `Not reported by provider`
**And** missing metrics are never displayed or exported as zero.

**Given** combined human, external, and AI effort reports are displayed
**When** totals are shown
**Then** human/external duration totals and AI runtime/token metrics remain visually and semantically separated
**And** no default token-to-hours conversion is performed.

**Given** an AI report is filtered by AI agent Party or Work Reference
**When** results are returned
**Then** tenant isolation, result-level authorization, projection freshness, and reference-state metadata are enforced
**And** Timesheets stores stable references only.

**Given** AI effort reporting UI is displayed
**When** users inspect metrics and drill into entries
**Then** FrontComposer/Fluent UI V5 surfaces use clear labels, status badges, accessible tables, and explicit unavailable states
**And** AI insight is not presented as approval, payroll, invoicing, or finance authority.

### Story 4.5: Generate Finance Export from Approved Ledger

**Requirements:** FR18, FR19

As a finance or accounting consumer,
I want to export approved billable time with stable IDs and correction lineage,
So that downstream billing workflows receive trustworthy evidence without Timesheets becoming an invoicing system.

**Acceptance Criteria:**

**Given** a finance consumer has export authority in a tenant
**When** they filter the Approved-Time Ledger by tenant, Project Reference, Work Reference, Contributor, Activity Type, period, and Billable Flag
**Then** the export scope is previewed from approved ledger data
**And** projection freshness and export readiness are visible before export.

**Given** approved billable ledger rows match the requested filters
**When** the user confirms export
**Then** the export output includes stable IDs, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comments where policy allows
**And** requester, filters, timestamp, correlation ID, and output scope are auditable.

**Given** corrected or superseded approved entries exist
**When** export output is generated
**Then** lineage is sufficient for downstream reconciliation
**And** included/excluded superseded values follow the selected query options.

**Given** no approved entries match the filters or projection freshness is not acceptable for export
**When** the user opens the export action
**Then** export is disabled or blocked with persistent explanation
**And** no empty or misleading finance evidence file is produced.

**Given** export output is generated
**When** rows are written
**Then** ordering is deterministic across repeated exports with the same filters and source events
**And** no raw EventStore envelopes, copied sibling-owned state, rates, invoice totals, taxes, payroll values, or revenue-recognition data are included.

**Given** export UI is displayed
**When** users review scope and confirm
**Then** a FluentDialog summarizes filters, output scope, included evidence fields, freshness state, and what Timesheets will not calculate
**And** copy avoids invoice, payroll, rate, tax, and revenue-recognition ownership language.

### Story 4.6: Verify Finance Export Evidence and Audit Trail

**Requirements:** FR18, FR19

> **Policy (ratified 2026-06-19):** export time-zone handling follows the tenant-time-zone period policy (Story 2.7); boundary cases covered by golden files (NFR15).

As a finance or audit reviewer,
I want export evidence and audit records to be deterministic and verifiable,
So that downstream reconciliation can trust the exported approved-time evidence.

**Acceptance Criteria:**

**Given** a finance export is requested
**When** the export is accepted
**Then** requester, filters, timestamp, correlation ID, freshness state, output scope, and export format/version are recorded as audit evidence
**And** the audit evidence does not include secrets, raw tokens, command bodies, or copied sibling-owned state.

**Given** the export is generated
**When** contract and golden-file tests run
**Then** output schema, stable field names, time-zone handling, correction lineage, billable filtering, and deterministic regeneration are verified
**And** no rates, invoice totals, taxes, payroll values, revenue-recognition data, raw EventStore envelopes, or copied sibling-owned state are included.

**Given** comments are included or excluded by export policy
**When** export redaction checks run
**Then** comment visibility follows the evidence retention and comment sensitivity policy
**And** unauthorized comment fields are absent from export output and diagnostics.

**Given** export filters include tenant-local dates or period boundaries
**When** time-zone boundary tests run
**Then** UTC audit instants and tenant-local period keys are handled consistently
**And** edge cases around period boundaries are covered by golden files.

### Story 4.7: Surface Timesheets Dashboard Overview

**Requirements:** FR16, FR18, FR20

As an internal Timesheets user,
I want a dashboard that summarizes my current period, pending actions, approval workload, and reporting shortcuts,
So that I can start common capture, review, and finance workflows from one operational surface.

**Acceptance Criteria:**

**Given** an internal user opens the Timesheets module
**When** the dashboard loads inside `FrontComposerShell`
**Then** it shows the user's current period status, pending corrections or submissions, approval workload where authorized, and shortcuts to operational reports and the Approved-Time Ledger
**And** it does not introduce a parallel shell, marketing hero, decorative card-heavy landing page, or custom portal.

**Given** the dashboard depends on projections for counts, period state, approval workload, or ledger freshness
**When** any projection is stale, rebuilding, degraded, or unavailable
**Then** the dashboard shows explicit freshness/status messaging
**And** it does not present stale data as fresh decision authority.

**Given** the dashboard composes status from existing projections
**When** it renders current period, approval workload, report shortcuts, or ledger shortcuts
**Then** it remains read-only aggregation over existing read models
**And** it introduces no new approval logic, finance math, export calculations, or write authority.

**Given** a user lacks approver or finance authority
**When** the dashboard is rendered
**Then** approval workload, ledger, and export actions are hidden or disabled according to policy
**And** no protected tenant, contributor, Project, Work, or Time Entry details are disclosed.

**Given** the dashboard has no current entries, approvals, or exportable ledger data
**When** empty states are shown
**Then** the page provides the single most relevant action, such as `Record time`, and avoids misleading finance/export affordances.

**Given** users navigate from dashboard shortcuts to capture, period review, approvals, reports, or ledger
**When** they drill in and return
**Then** filters, period context, and projection status are preserved where applicable
**And** navigation remains keyboard accessible.

**Given** dashboard UI is tested
**When** accessibility and conformance checks run
**Then** FrontComposer/Fluent UI V5 components, status badges with text, message bars, focus order, and WCAG 2.2 AA behavior are verified
**And** hover-only controls or color-only statuses are not introduced.
