---
stepsCompleted: [1, 2]
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

FR1: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR2: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR3: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR4: Epic 2 - Submission, Approval, Period Review & Corrections
FR5: Epic 2 - Submission, Approval, Period Review & Corrections
FR6: Epic 2 - Submission, Approval, Period Review & Corrections
FR7: Epic 2 - Submission, Approval, Period Review & Corrections
FR8: Epic 2 - Submission, Approval, Period Review & Corrections
FR9: Epic 2 - Submission, Approval, Period Review & Corrections
FR10: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR11: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR12: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR13: Epic 3 - External Contributor Confirmation
FR14: Epic 3 - External Contributor Confirmation
FR15: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR16: Epic 4 - Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export
FR17: Epic 4 - Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export
FR18: Epic 4 - Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export
FR19: Epic 4 - Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export
FR20: Epic 4 - Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export
FR21: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR22: Epic 1 - Actor-neutral Time Capture & Activity Governance
FR23: Epic 1 - Actor-neutral Time Capture & Activity Governance

## Epic List

### Epic 1: Trusted Actor-Neutral Time Capture & Activity Governance

Users can record auditable time against Project or Work references for internal, external, and AI contributors, with Activity Type catalogs, Party attribution, reference validation, EventStore persistence, and module boundary enforcement in place.

**FRs covered:** FR1, FR2, FR3, FR10, FR11, FR12, FR15, FR21, FR22, FR23

### Epic 2: Submission, Approval, Period Review & Corrections

Contributors can submit entries and periods; approvers can approve or reject entries and periods; approved entries are locked from direct edit; corrections preserve evidence and lineage.

**FRs covered:** FR4, FR5, FR6, FR7, FR8, FR9

### Epic 3: External Contributor Confirmation

External contributors can submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.

**FRs covered:** FR13, FR14

### Epic 4: Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

**FRs covered:** FR16, FR17, FR18, FR19, FR20

## Epic 1: Trusted Actor-Neutral Time Capture & Activity Governance

Users can record auditable time against Project or Work references for internal, external, and AI contributors, with Activity Type catalogs, Party attribution, reference validation, EventStore persistence, and module boundary enforcement in place.

### Story 1.1: Scaffold Trusted Timesheets Module Shell

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

**Given** the scaffold is complete
**When** restore, build, and the initial architecture/unit test lane are run
**Then** the solution builds with warnings as errors
**And** the test baseline passes or any infrastructure-dependent tests are clearly isolated from the fast baseline.

### Story 1.2: Manage Tenant Activity Types

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

### Story 1.3: Manage Project Activity Types

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

### Story 1.4: Record Draft Time Entry Against Project or Work

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

### Story 1.5: Preserve and Display Time Entry Evidence

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

### Story 1.6: Capture AI Effort Metrics

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

### Story 1.7: Publish Capture Contracts and FrontComposer Metadata

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
**And** contract evolution remains additive and serialization-tolerant.

**Given** FrontComposer metadata is generated or registered for capture and catalog workflows
**When** internal UI surfaces are composed
**Then** they use FrontComposer command/projection patterns and Fluent UI V5-compatible metadata
**And** no bespoke parallel portal or raw HTML-first component model is required.

**Given** contract and metadata tests run
**When** package boundary and UI conformance checks execute
**Then** they prove Contracts remains infrastructure-free, no inline package versions are added, Fluent UI V4 components are not introduced, and generated surfaces expose required validation/status/freshness fields.

**Given** API documentation or OpenAPI artifacts are produced
**When** consumers inspect the public surface
**Then** docs describe Timesheets commands, queries, states, and validation outcomes
**And** they do not expose EventStore internals or imply Timesheets owns Party, Project, Work, Tenant, invoice, payroll, rate, or revenue state.

## Epic 2: Submission, Approval, Period Review & Corrections

Contributors can submit entries and periods; approvers can approve or reject entries and periods; approved entries are locked from direct edit; corrections preserve evidence and lineage.

### Story 2.1: Submit Draft Time Entries for Approval

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

### Story 2.2: Approve or Reject Submitted Time Entries

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

### Story 2.3: Lock Approved Entries and Add Corrections

As a contributor or reviewer,
I want approved entries to be locked from direct edits and changed only through additive corrections,
So that approved evidence remains trustworthy while mistakes can still be fixed.

**Acceptance Criteria:**

**Given** a Time Entry is Approved
**When** a user attempts to directly edit date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or Approval State
**Then** the command is rejected as a domain outcome
**And** no approved event history is rewritten.

**Given** an Approved Time Entry needs a correction
**When** an authorized user adds a correction with corrected values and a reason where required
**Then** a compensating correction event is persisted
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

### Story 2.4: Submit Timesheet Periods

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

### Story 2.5: Approve or Reject Timesheet Periods

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

### Story 2.6: Correct Rejected Entries in Submitted Periods

As a contributor,
I want to correct rejected entries from a submitted period without rebuilding the whole period,
So that approval evidence remains intact while specific problems are resolved.

**Acceptance Criteria:**

**Given** an entry in a submitted Timesheet Period is Rejected
**When** the contributor opens the correction flow
**Then** the prior values and rejection reason are visible where authorized
**And** the correction is saved as an additive event, not a direct edit.

**Given** a corrected entry belongs to a submitted or approved period
**When** the correction is submitted
**Then** the period shows a pending correction or mixed state as appropriate
**And** the period history is not silently rewritten.

**Given** the correction changes date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or AI metrics
**When** the command is accepted
**Then** original and corrected values are linked in lineage
**And** downstream projections can include or exclude superseded values through query options.

**Given** a correction would cross tenant boundaries, use an inactive/disallowed Activity Type, or reference unverifiable Project/Work/Party data for a trust-bearing change
**When** the command is handled
**Then** it fails closed
**And** no partial correction is persisted.

**Given** the correction UI is displayed
**When** users inspect the rejected entry and correction form
**Then** FrontComposer/Fluent UI V5 components show rejection reason, correction lineage, field validation, consequence-aware copy, and accessible focus order
**And** no copy implies approved evidence can be directly edited.

## Epic 3: External Contributor Confirmation

External contributors can submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.

### Story 3.1: Submit External Contributor Time Through API

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

**Given** external API requests are retried or duplicated
**When** idempotency context matches a prior accepted command
**Then** the system avoids duplicate Time Entries or duplicate submitted evidence
**And** projection replay remains idempotent.

**Given** telemetry is emitted for external submission
**When** requests succeed or fail
**Then** logs contain correlation-safe outcome metadata only
**And** comments, command bodies, personal data, token values, target names, and protected identifiers are not logged.

### Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities

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

### Story 3.3: Confirm or Adjust Time Through Magic Link

As an external contributor,
I want to confirm or adjust a scoped proposed Time Entry from a magic link,
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

**Given** the external contributor adjusts allowed fields
**When** they submit adjusted time
**Then** only allowed fields are accepted
**And** the adjustment follows the same validation, approval, correction, target-reference, and tenant-isolation rules as other Time Entry capture.

**Given** the confirmation has been accepted
**When** the same token is used again
**Then** reuse is rejected with the generic no-disclosure invalid-link response
**And** no previous confirmation details are shown.

**Given** the external confirmation page is used on a phone viewport
**When** the contributor reviews, confirms, or adjusts the proposed entry
**Then** the page remains fully usable with Fluent UI V5 components, clear duration units, accessible focus order, keyboard/touch reachable controls, and no internal shell navigation.

### Story 3.4: Protect Invalid Magic-Link States From Disclosure

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
**When** test cases cover expired, used, revoked, unauthorized, malformed, unknown, cross-tenant, and repeated tokens
**Then** all cases produce equivalent external disclosure behavior
**And** only authorized internal audit views can distinguish failure categories.

## Epic 4: Operational Reporting, AI Effort Insight, Approved Ledger & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

### Story 4.1: Query Time Entries by Operational Dimensions

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

### Story 4.2: Produce Project and Work Actual-Time Reports

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

**Given** report users interact with filter-heavy surfaces
**When** they change filters or switch related report views
**Then** FrontComposer/Fluent UI V5 components provide FilterBar, FluentDataGrid, status badges, related tabs where appropriate, and accessible keyboard/focus behavior.

**Given** common report filters are run against realistic tenant/project/period data
**When** performance tests execute
**Then** common report queries target 2 seconds p95 according to launch sizing assumptions
**And** deviations are visible as quality evidence rather than hidden implementation details.

### Story 4.3: Maintain Approved-Time Ledger Projection

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
**And** finance/export actions cannot treat stale data as fresh unless explicitly allowed by policy.

**Given** unauthorized or cross-tenant ledger access is attempted
**When** the ledger is queried
**Then** access fails closed or filters according to policy
**And** no protected contributor, target, comment, or ledger details are disclosed.

**Given** the Approved-Time Ledger UI is displayed
**When** users review approved evidence
**Then** FrontComposer/Fluent UI V5 surfaces show stable IDs, approval metadata, billable status, comments where allowed, correction lineage, filters, and projection freshness.

### Story 4.4: Surface AI Effort Reporting

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

### Story 4.5: Export Approved Billable Evidence

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

**Given** the export is generated
**When** contract and golden-file tests run
**Then** output schema, stable field names, time-zone handling, correction lineage, billable filtering, and deterministic regeneration are verified
**And** no rates, invoice totals, taxes, payroll values, revenue-recognition data, raw EventStore envelopes, or copied sibling-owned state are included.

**Given** export UI is displayed
**When** users review scope and confirm
**Then** a FluentDialog summarizes filters, output scope, included evidence fields, freshness state, and what Timesheets will not calculate
**And** copy avoids invoice, payroll, rate, tax, and revenue-recognition ownership language.
