---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/.decision-log.md
  - _bmad-output/planning-artifacts/briefs/brief-timesheets-2026-06-18/brief.md
  - _bmad-output/planning-artifacts/briefs/brief-timesheets-2026-06-18/addendum.md
  - _bmad-output/planning-artifacts/briefs/brief-timesheets-2026-06-18/.decision-log.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/.decision-log.md
  - Hexalith.EventStore/_bmad-output/project-context.md
  - Hexalith.Parties/_bmad-output/project-context.md
  - Hexalith.Projects/_bmad-output/project-context.md
  - Hexalith.Conversations/_bmad-output/project-context.md
  - Hexalith.Tenants/_bmad-output/project-context.md
  - Hexalith.FrontComposer/_bmad-output/project-context.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-06-18'
project_name: 'timesheets'
user_name: 'Jerome'
date: '2026-06-18'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

Hexalith.Timesheets is a tenant-scoped, actor-neutral effort evidence module. The functional scope covers 23 requirements across six major areas:

- Time Entry Ledger: record auditable Time Entries against exactly one Project or Work reference; validate references through sibling module boundaries; preserve correction lineage.
- Submission, Approval, Rejection, and Locking: support entry-level and period-level submission/approval, rejection with reason, approved-entry locking, additive corrections, and distinct entry vs period state.
- Activity Type Catalogs: manage tenant-level and project-level Activity Types with active/inactive state, stable IDs, and historical reportability.
- Actor-Neutral Contributors: attribute every entry to a Party; support internal contributors, external-party API submission, scoped magic-link confirmation, and AI-agent effort evidence.
- Reporting, Ledger, and Exports: query by operational dimensions, report actual time by Project and Work, maintain an Approved-Time Ledger, export approved billable evidence, and surface AI effort separately from human/external duration.
- Platform Boundary: persist through Hexalith.EventStore; expose command/query contracts; integrate with FrontComposer/Fluent UI; reference Projects, Works, Parties, Tenants, and EventStore without copying their owned state.

Architecturally, the module must treat Time Entry evidence as append-only domain state, not mutable rows. Approval, rejection, correction, export-relevant changes, activity catalog changes, and magic-link confirmation all need explicit command/event/query semantics.

**Non-Functional Requirements:**

The main architecture-driving NFRs are:

- Security and authorization: every command, query, API, magic-link, export, and admin path must enforce tenant and resource gates and fail closed.
- Tenant isolation: all entries, periods, catalogs, projections, exports, and links are tenant-scoped; cross-tenant access must be impossible and tested.
- Auditability: approvals, rejections, corrections, exports, and magic-link usage must preserve provenance.
- Data minimization: Timesheets stores Party, Project, and Work references, not copied personal or owned domain data.
- Projection reliability: read models must tolerate at-least-once delivery, duplicates, replay, rebuild, stale, degraded, and unavailable states.
- Observability privacy: logs/traces must use structured metadata without payloads, comments, personal data, tokens, secrets, or command bodies.
- Compatibility: event and contract evolution must be additive and serialization-tolerant.
- Accessibility: internal and external web surfaces target WCAG 2.2 AA through FrontComposer/Fluent UI patterns.
- Performance targets: common command acknowledgements target 500 ms p95 in warmed local service; common report queries target 2 seconds p95 for tenant/project/period filters.

**Scale & Complexity:**

- Primary domain: full-stack Hexalith domain module with event-sourced backend, command/query contracts, projections, generated/admin UI, external confirmation surface, and export/reporting paths.
- Complexity level: enterprise.
- Estimated architectural components: 12 component families.

The component families implied by the requirements are: Time Entry write model, Timesheet Period write model, Activity Type catalog, approval policy/state model, correction lineage model, Project/Work reference validation adapters, Party contributor validation/display hydration adapters, Magic-Link Confirmation capability, AI effort metrics model, operational projections, Approved-Time Ledger/export projection, and FrontComposer/Fluent UI surfaces.

### Technical Constraints & Dependencies

Timesheets is constrained by existing Hexalith module rules:

- EventStore is the authoritative persistence path for domain state changes.
- Tenants owns tenant lifecycle, membership, roles, access projections, and tenant isolation.
- Parties owns Contributor identity and personal data; Timesheets persists Party IDs only.
- Projects owns Project state and project-level context; Timesheets stores stable Project references only.
- Works owns Work lifecycle and planned/estimated effort; Timesheets stores stable Work references only.
- FrontComposer and Blazor Fluent UI are the primary internal UI system.
- External contributor UX is limited to API-only integration and scoped Magic-Link Confirmation; no full external portal in v1.
- The module must follow Hexalith conventions: .NET 10, centralized package versions, warnings as errors, no recursive submodules, no copied sibling module state, no direct infrastructure persistence bypassing EventStore, and additive contract evolution.

No epics or stories were loaded, so this architecture is PRD/UX-driven. Implementation slicing should not be inferred yet; the architecture should first settle domain boundaries, event contracts, projection ownership, and cross-module adapters.

### Cross-Cutting Concerns Identified

- Tenant/resource authorization and fail-closed access checks across commands, queries, exports, magic links, and UI.
- Event-sourced correction and approval lineage across Time Entry, period, ledger, and reports.
- Distinct entry-level and period-level approval state without flattening mixed states.
- Projection freshness and rebuild semantics for approvals, reports, and ledger exports.
- Reference validation and display hydration through Projects, Works, and Parties without denormalizing owned state.
- AI effort metrics as multi-unit evidence, not a conversion into human hours.
- External magic-link no-disclosure behavior for invalid, expired, or used links.
- Activity Type governance across tenant and project scopes.
- Export accountability without crossing into invoicing, rates, payroll, taxes, or revenue recognition.
- Privacy-safe logging, tracing, comments, audit metadata, and support diagnostics.
- Responsive, accessible FrontComposer/Fluent UI surfaces with dense operational grids and explicit evidence-changing commands.

### Architectural Pressure Points

Several requirements are coupled and need explicit decisions before implementation stories begin:

- Aggregate boundaries: Time Entry, Timesheet Period, Activity Type, Magic-Link Confirmation, export audit, and approval/correction state could be modeled together or separately. The wrong split will create consistency and replay problems.
- Approval reconciliation: entry state and period state are intentionally distinct. Architecture must prevent period approval from silently flattening rejected, corrected, or superseded entry states.
- Correction semantics: the PRD allows additive correction after approval, but architecture must decide whether corrections are superseding events, linked replacement entries, offset entries, or a supported combination.
- Reference validation freshness: Project, Work, and Party validation must fail closed at approval/submission boundaries, but Draft creation may tolerate stale references. That distinction must be encoded in command policy.
- Magic-link security: Magic-Link Confirmation is not just an external UX. It needs a scoped capability model, single-use/expiry enforcement, no-disclosure failure behavior, and audit metadata.
- AI effort evidence: human/external duration, AI wall-clock time, model/tool runtime, billable effort, and tokens are different units. Projections and exports must preserve those units instead of normalizing them too early.
- Approved-Time Ledger authority: the ledger is a rebuildable projection, not source-of-truth storage. Export evidence must reference ledger projection state while preserving event lineage.
- Comment sensitivity: comments are useful evidence but may contain sensitive customer/private data. Architecture needs a policy for storage, logging, exports, and redaction posture.
- Period/time-zone policy: weekly/monthly boundaries depend on tenant policy unless superseded. This affects identity, validation, queries, and approval cutoffs.

### Risk-Bearing Architecture Implications

Timesheets is a Hexalith domain module, not a standalone CRUD time-entry product. The authoritative record is the append-only event stream; ledgers, reports, approval queues, exports, and UI lists are rebuildable projections over that event history.

The main unresolved architecture risk is aggregate and consistency boundary design. Time Entry, Timesheet Period, Activity Type policy, approval/locking, correction lineage, external confirmation, and AI effort evidence may not share the same transaction boundary. Architecture must decide which invariants require serialized aggregate handling and which can be handled through asynchronous projection-side reconciliation.

Approval and locking are not simple status transitions. The design must define how submission, approval, rejection, lock windows, late corrections, external confirmations, AI-assisted entries, and projection lag reconcile without mutating history or flattening mixed entry/period states.

Corrections must be first-class compensating facts with actor, reason, timestamp, affected entry/period references, and lineage. The UX and exports should expose current effective state plus history, not imply that approved evidence was edited in place.

Project, Work, Party, and Tenant data remain externally owned. Timesheets stores stable references and hydrates display metadata at read time or through approved projections. The architecture must expose stale, missing, deleted, unauthorized, or unavailable reference states to commands, UI, reports, and exports.

Magic-Link Confirmation is a security boundary, not only a UX flow. It must be scoped, expiring, tenant-bound, entry-bound, replay-resistant, revocable where needed, auditable, and unable to browse internal Timesheets context. Tokens and decoded claims must not appear in logs, projections, comments, or exports.

AI and external entries require provenance. Multi-unit AI effort evidence needs source attribution, unit basis, confidence or validation status, and correction path. AI input must not bypass tenant, reference, approval, lock, or audit checks.

Period and time-zone policy is architecture-critical. Tenant-local dates, UTC instants, DST behavior, weekly/monthly boundaries, lock windows, approval cutoffs, external confirmations, reports, and exports must use one canonical policy.

Comments are sensitive unstructured data by default. Architecture must define visibility, retention, logging, tracing, redaction, export exposure, and external-confirmation treatment.

Exports are consistency contracts. Export design must state tenant scope, filters, snapshot/projection freshness, correlation ID, included lineage, and whether exported totals are final, pending, stale, or rebuilt.

Testability is an architecture implication now: pure aggregate command/rejection tests, projection replay/idempotency tests, tenant isolation negative tests, period/time-zone matrix tests, magic-link abuse tests, export golden-file tests, and reference-staleness tests should be expected proof points.

Because epics and stories are not loaded, this analysis remains PRD/UX-derived and provisional. Implementation sequencing should not be inferred yet; architecture decisions must preserve room for clarification around period policy, aggregate boundaries, correction semantics, and external confirmation security.

## Starter Template Evaluation

### Primary Technology Domain

Hexalith.Timesheets is a .NET 10 Hexalith domain module with full-stack supporting surfaces: event-sourced backend, command/query contracts, projections, Aspire AppHost topology, generated/admin UI through FrontComposer, and a scoped external magic-link web surface.

This is not a generic web application starter problem. The right foundation is an internal Hexalith module scaffold aligned with the existing `Hexalith.Conversations`, `Hexalith.Works`, `Hexalith.Projects`, `Hexalith.Tenants`, and `Hexalith.Parties` module shapes.

### Version Research Notes

Current starter/tooling checks performed during this step:

- Local SDK observed during this architecture step: `dotnet --version` returned `10.0.301`; the current repository pin is `10.0.302`.
- Local templates include `aspire-apphost`, `aspire-servicedefaults`, `aspire-starter`, `webapi`, `blazor`, `classlib`, `xunit`, and solution templates.
- `dotnet new sln --name Hexalith.Timesheets` on the local .NET 10 SDK creates `Hexalith.Timesheets.slnx`.
- NuGet lists `Aspire.ProjectTemplates` `13.4.5` as the current package version on 2026-06-18.
- NuGet lists `xunit.v3` `3.2.2` as the current package version on 2026-06-18.
- Microsoft Learn documents `aspire-starter`, `aspire-apphost`, AppHost, and ServiceDefaults usage for Aspire starter projects and deployments.

### Starter Options Considered

| Option | Fit | Strength | Risk | Decision |
|---|---:|---|---|---|
| Official Aspire Starter Application | Low-Medium | Current, maintained, creates AppHost, ServiceDefaults, API, and web sample quickly | Imports generic sample structure and does not encode Hexalith EventStore/module boundaries | Do not use as primary starter |
| Official .NET SDK Templates | Medium | Correct primitives for `.slnx`, class libraries, Web API, Aspire AppHost, ServiceDefaults, and tests | Template defaults still require Hexalith cleanup and package centralization | Use as low-level scaffolding commands |
| Internal Hexalith Domain Module Scaffold | High | Preserves EventStore, tenant isolation, project/package boundaries, test style, and sibling-module conventions | Requires deliberate scaffold story because no single public command exists | Select |

The official Aspire Starter Application is rejected as the architectural template because Timesheets must follow Hexalith module boundaries, event-sourced domain conventions, tenant isolation rules, and sibling-module structure. Aspire primitives are approved only for empty project shells and local orchestration support.

### Selected Starter: Internal Hexalith Domain Module Scaffold

**Rationale for Selection:**

The project context requires Timesheets to behave like a Hexalith domain module: EventStore-backed, tenant-isolated, reference-based, projection-driven, centrally packaged, warnings-as-errors, and tested with Hexalith conventions. A public starter cannot safely encode those rules. The starter should therefore be the internal Hexalith module shape, closest to `Hexalith.Conversations` for greenfield domain-module structure and `Hexalith.Works` / `Hexalith.Projects` for Project/Work-adjacent integration patterns.

**Decision:** Timesheets will use an internal Hexalith domain-module scaffold as its starter template. Public .NET and Aspire templates may be used only to create empty project shells such as `.slnx`, class library, Web API, ServiceDefaults, AppHost, and xUnit projects. Architectural structure, package boundaries, dependency direction, EventStore integration, tenant isolation, and UI conventions must come from established Hexalith sibling modules, not from the official Aspire Starter Application.

### Initialization Command

There is no single public starter command selected. The first implementation story should scaffold the module using .NET 10 templates and sibling-module structure, roughly:

```powershell
dotnet new sln --name Hexalith.Timesheets

dotnet new classlib -n Hexalith.Timesheets.Contracts -o src/Hexalith.Timesheets.Contracts --framework net10.0 --no-restore
dotnet new classlib -n Hexalith.Timesheets.Client -o src/Hexalith.Timesheets.Client --framework net10.0 --no-restore
dotnet new classlib -n Hexalith.Timesheets.Server -o src/Hexalith.Timesheets.Server --framework net10.0 --no-restore
dotnet new classlib -n Hexalith.Timesheets.Projections -o src/Hexalith.Timesheets.Projections --framework net10.0 --no-restore
dotnet new webapi -n Hexalith.Timesheets -o src/Hexalith.Timesheets --framework net10.0 --no-restore
dotnet new aspire-servicedefaults -n Hexalith.Timesheets.ServiceDefaults -o src/Hexalith.Timesheets.ServiceDefaults --no-restore
dotnet new aspire-apphost -n Hexalith.Timesheets.AppHost -o src/Hexalith.Timesheets.AppHost --no-restore
dotnet new classlib -n Hexalith.Timesheets.Testing -o src/Hexalith.Timesheets.Testing --framework net10.0 --no-restore

dotnet new xunit -n Hexalith.Timesheets.Contracts.Tests -o tests/Hexalith.Timesheets.Contracts.Tests --framework net10.0 --no-restore
dotnet new xunit -n Hexalith.Timesheets.Server.Tests -o tests/Hexalith.Timesheets.Server.Tests --framework net10.0 --no-restore
dotnet new xunit -n Hexalith.Timesheets.Projections.Tests -o tests/Hexalith.Timesheets.Projections.Tests --framework net10.0 --no-restore
dotnet new xunit -n Hexalith.Timesheets.IntegrationTests -o tests/Hexalith.Timesheets.IntegrationTests --framework net10.0 --no-restore
```

The scaffold story must then apply Hexalith-specific files and conventions: `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`, project references, package metadata, test lane conventions, EventStore/Tenants/Parties/Projects/Works references, and `.slnx` membership.

The command sequence is illustrative and must be verified in the scaffold story before execution. Public templates create empty shells; they do not define the architecture.

### Architectural Decisions Provided by Starter

**Language & Runtime:**

C# on .NET 10, nullable enabled, implicit usings, warnings as errors, file-scoped namespaces, centralized package versions, `.slnx` solution format.

**Styling Solution:**

No custom Timesheets styling starter. Internal UI uses FrontComposer and Blazor Fluent UI through existing Hexalith patterns. External Magic-Link Confirmation should use the same component family and no-disclosure behavior. FrontComposer/Fluent UI and Magic-Link Confirmation are presentation concerns, not starter-template drivers.

**Build Tooling:**

MSBuild/.NET SDK projects, Central Package Management, module-local `.slnx`, no `.sln`, no inline package versions, .NET SDK container support where host packaging is needed.

**Testing Framework:**

xUnit v3, Shouldly, NSubstitute, bUnit for UI surfaces, Testcontainers/Aspire testing only for integration boundaries. The xUnit template creates only a shell until it is normalized to Hexalith test conventions. Expected proof points include aggregate command/rejection tests, projection replay/idempotency tests, tenant-isolation negative tests, time-zone matrix tests, magic-link abuse tests, export golden-file tests, and reference-staleness tests.

**Code Organization:**

Initial project families should include `Contracts`, `Client`, host/domain service, `Server`, `Projections`, `ServiceDefaults`, `AppHost`, `Testing`, and focused test projects. Optional `UI`, `Mcp`, or export-specific packages should be added only when the architecture decisions prove they are needed.

**Development Experience:**

Use Aspire AppHost for local topology and observability. Use root-level submodules only. Restore/build module `.slnx`; run tests by affected project or module lane according to the final Timesheets convention. AppHost changes require restarting `aspire run`.

### Starter Guardrails

The starter decision is not anti-Aspire. Aspire remains the local topology, orchestration, observability, AppHost, and ServiceDefaults foundation. The rejected part is using the official Aspire Starter Application as the application architecture.

Public .NET/Aspire templates are approved only for empty project shells: `.slnx`, `classlib`, `webapi`, `aspire-servicedefaults`, `aspire-apphost`, and `xunit`.

Architectural structure, package boundaries, dependency direction, EventStore integration, tenant isolation, testing shape, and UI conventions must come from established Hexalith sibling modules.

The scaffold defines structure only, not product behavior. No new Timesheets scope should be inferred from Aspire Starter Application sample features.

### Canonical Sibling References

Use sibling modules as architectural exemplars by concern, not as copy/paste sources:

- `Hexalith.EventStore`: event-sourced aggregate/write path, command status, persistence and replay expectations.
- `Hexalith.Tenants`: tenant isolation, fail-closed access, tenant projection behavior.
- `Hexalith.Parties`: stable Party references, personal-data minimization, external contributor identity patterns.
- `Hexalith.Projects` and `Hexalith.Works`: stable Project/Work references and Project/Work-adjacent integration patterns.
- `Hexalith.FrontComposer`: internal UI composition, generated command/projection surfaces, Fluent UI conventions.
- Aspire/AppHost patterns: local topology, service defaults, observability, and orchestration only.

### Assumptions To Verify During Scaffold Story

- `dotnet new sln` on the active .NET 10 SDK emits the expected `.slnx` format in local and CI environments.
- `dotnet new xunit` output must be normalized to Hexalith test conventions, including xUnit v3, Shouldly, NSubstitute, Central Package Management, and no inline package versions.
- Aspire template output must be aligned to the currently pinned Aspire version in `Directory.Packages.props` / AppHost SDK, not accepted blindly.
- The chosen sibling-module baseline should be explicit: `Hexalith.Conversations` for greenfield shape, with `Hexalith.Works` / `Hexalith.Projects` consulted for Project/Work integration patterns.
- UI packages should not be scaffolded automatically unless an architecture decision confirms whether Timesheets needs a dedicated `UI`, generated FrontComposer metadata only, or both.
- Root-level submodule rules are preserved; no recursive submodule initialization is introduced.
- EventStore remains the only authoritative persistence path for domain state.
- The API host does not become the place where domain logic or direct CRUD persistence lives.

### Failure Paths This Avoids

- Creating a generic Aspire sample and then fighting its API/frontend assumptions.
- Letting test template defaults drift from xUnit v3 and Hexalith assertion/mocking standards.
- Introducing inline package versions or `.sln` files.
- Scaffolding UI before deciding the FrontComposer contract surface.
- Treating AppHost topology as the domain architecture.
- Introducing direct database-backed CRUD or EventStore bypass.
- Treating tenant isolation as middleware only.
- Reimplementing sibling module contracts instead of referencing stable IDs and adapters.
- Hand-building an internal admin portal outside FrontComposer conventions.
- Inferring new Timesheets product behavior from starter-template sample features.

### Decision Record Summary

**Context:** Timesheets is an event-sourced Hexalith domain module with tenant isolation, external stable references, projection-heavy reporting, FrontComposer/Fluent UI internal surfaces, and a scoped external confirmation path.

**Decision:** Use an internal Hexalith domain-module scaffold assembled from .NET 10 SDK/Aspire primitives and sibling-module conventions. Reject the official Aspire Starter Application as the structural application template.

**Consequences:**

- The first implementation story must create the module shell and normalize all generated projects to Hexalith conventions.
- Public starter commands may create project files, but they do not define architecture.
- The architecture should treat `aspire-starter` as a reference sample only, not a codebase seed.
- Consistency improves and sample-code drift is reduced, but the scaffold source and validation checklist must be documented explicitly.

### Simplified Starter Rule

Use public templates only for empty project shells. Use Hexalith sibling modules for architecture.

**Note:** Project initialization using this scaffold should be the first implementation story. The implementation story should not run `aspire-starter` directly because it would introduce generic sample structure that conflicts with Hexalith module boundaries.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**

- EventStore-first persistence: Timesheets domain state persists through Hexalith.EventStore only.
- Aggregate boundaries: baseline `TimeEntry`, `TimesheetPeriod`, and `ActivityType` boundaries.
- Correction model: approved-entry changes use additive compensating events with superseding lineage by default.
- Projection model: operational views, approval queues, reports, and Approved-Time Ledger are rebuildable projections with freshness metadata.
- Reference validation: submission, approval, export, and magic-link confirmation fail closed when required references or authority cannot be resolved.
- Authentication/security: JWT/OIDC authentication with fail-closed tenant/resource authorization; JWT claims are evidence, not authority.
- Magic-Link Confirmation: scoped opaque capability, single-use/expiry-bound, audited, no-disclosure on invalid states.
- API model: EventStore command/query pipeline, typed domain outcomes, ProblemDetails for HTTP transport errors.
- Frontend component policy: Blazor Fluent UI V5 components only; V4 icons only if V5 icon source is unavailable.
- Infrastructure: Aspire AppHost for local topology/orchestration, Dapr SDK policy target `1.18.4`, no direct DB/broker bypass.

**Important Decisions (Shape Architecture):**

- `Contracts`, `Client`, host/domain service, `Server`, `Projections`, `ServiceDefaults`, `AppHost`, `Testing`, and focused tests form the initial module shape.
- FrontComposer is the primary internal UI composition model.
- External Magic-Link Confirmation is a minimal responsive web surface, not a full external portal.
- Exports are consistency contracts with tenant scope, filters, freshness/snapshot metadata, lineage, and audit metadata.
- Comments are sensitive unstructured data by default.
- OpenAPI/docs follow Hexalith conventions for public HTTP surfaces.
- Observability uses OpenTelemetry/Aspire conventions without payload/personal-data leakage.

**Deferred Decisions (Post-MVP or Later Architecture Category):**

- Offset-entry correction support beyond superseding lineage.
- `Mcp` or export-specific package creation. (The `UI`/`UI.Tests` projects are **not** deferred indefinitely — see "UI project timing (decided 2026-06-19)" under Gap Analysis — but are scaffolded with the first UI-bearing story, not in Story 1.1.)
- Full external-party portal.
- Invoice/rate/payroll/revenue integrations.
- Native mobile app.
- MCP Timesheets surface unless later requirements select it.

### Data Architecture

**Decision: EventStore-first event-sourced persistence**

Timesheets domain state changes will persist through `Hexalith.EventStore`. The module will not introduce a direct database-backed CRUD store for authoritative Time Entry, Timesheet Period, Activity Type, approval, correction, export, or magic-link state.

**Version notes:**

- .NET SDK: local target is .NET 10, current local SDK `10.0.302`.
- Dapr SDK packages: target latest verified package line `1.18.4` for Timesheets-owned direct pins, subject to scaffold compatibility validation. Current Timesheets root package files do not directly pin Dapr SDK packages; Dapr arrives through sibling EventStore project references, and the submodule-owned `Hexalith.Builds` package props still keep base `Dapr` at `1.17.9` while Dapr ASP.NET Core/Actors/Workflow pins are `1.18.4`.
- Aspire templates/packages: current `Aspire.ProjectTemplates` checked as `13.4.5`.

**Aggregate boundaries:**

Baseline aggregate boundaries are:

- `TimeEntry` for individual entry lifecycle, submission, approval/rejection state, lock state, correction lineage, target reference, Contributor Party ID, billable flag, activity type, comments, and AI effort evidence attached to that entry.
- `TimesheetPeriod` for contributor/tenant/period submission and grouped review evidence.
- `ActivityType` or Activity Type catalog boundary for tenant-scoped and project-scoped activity type governance.

Magic-Link Confirmation, export audit, reference validation, and read-side reporting are capabilities around these domain events unless later decisions prove they require separate aggregates.

**Correction model:**

Approved-entry changes are additive compensating events. The default v1 semantic is superseding correction lineage: reports and UI expose current effective state plus prior evidence. Offset entries are deferred unless finance/export decisions require them at launch.

**Projection model:**

Operational views, approval queues, Time Entry detail, Timesheet Period views, reports, AI effort reports, and the Approved-Time Ledger are rebuildable projections. Projections must be idempotent, replay-safe, duplicate-tolerant, and expose freshness/degraded/rebuilding states.

**Reference validation model:**

Timesheets stores stable references to Tenant, Party, Project, and Work identities. Draft creation may tolerate stale display hydration, but submission, approval, export, and magic-link confirmation fail closed when required references or authority cannot be resolved.

**Rationale:**

This matches Hexalith module constraints: EventStore owns persistence, sibling modules own their domain state, Timesheets owns time evidence and approval/correction facts, and read models remain rebuildable from append-only events.

### Authentication & Security

**Authentication model:**

Internal API/UI paths use ASP.NET Core authentication with JWT bearer/OIDC integration consistent with sibling Hexalith modules. Keycloak remains the production/local-topology identity provider when enabled, with the existing symmetric-key JWT development fallback preserved where the AppHost supports it.

**Version notes:**

- ASP.NET Core auth/data-protection package line should align with the local Hexalith/.NET 10 package line, currently observed as `10.0.9` in shared build props.
- Dapr SDK target remains latest verified `1.18.4`.

**Authorization model:**

JWT tenant/user claims are request evidence, not the authority. Timesheets must authorize through fail-closed tenant/resource gates before aggregate load, command dispatch, projection read, export, or magic-link disclosure.

Authorization decisions combine:

- Tenants projection for tenant lifecycle, membership, and role/access checks.
- Project/Work authority adapters for approver/project/work-specific permissions.
- Timesheets policy for self-approval, approval windows, correction rights, export permission, and magic-link issuance.

**Magic-Link Confirmation model:**

Magic links are scoped capabilities, not login sessions. Baseline design:

- Server-generated opaque token.
- Store only token hash and capability metadata.
- Bind capability to tenant, contributor Party, target entry/proposed entry, allowed action, expiry, and single-use state.
- Persist issue/use/revoke/expire outcomes through EventStore-backed audit events.
- Invalid, expired, used, or revoked links return the same no-disclosure response.
- Token values and decoded capability material are never logged, projected, exported, or stored in comments.

**Implementation status note (2026-06-19):** Epic 3 implemented the contracts, domain services, capability state, projection, endpoint routes, and service/workflow no-disclosure tests. The default registered `IMagicLinkConfirmationCapabilityStateLoader` is still `UnavailableMagicLinkConfirmationCapabilityStateLoader`, which fails closed by returning no capability, no Time Entry state, and an unavailable Activity Type catalog. Production-ready live host display/confirm/adjust requires a concrete EventStore-backed loader/token-hash lookup that folds capability state, folds the scoped Time Entry state, and loads a fresh Activity Type catalog without exposing token, tenant, contributor, target, or failure-reason material.

**Readiness repair (approved 2026-06-20):** the concrete `IMagicLinkConfirmationCapabilityStateLoader` is owned by Epic 3 follow-up Story 3.6, not by a final hardening bucket. The loader must resolve token hashes without token disclosure, fold capability state from EventStore-backed events, fold the scoped Time Entry state, load a fresh Activity Type catalog, and preserve the same no-disclosure response for malformed, unknown, expired, used, revoked, unauthorized, wrong-recipient, wrong-action, and replayed links. Epic 5 verifies the evidence only after the owning feature work is complete.

**Status update (2026-06-22):** Stories 3.6 and 3.7 closed the loader and HTTP-boundary gaps above. The concrete `EventStoreMagicLinkConfirmationCapabilityStateLoader` is now the registered (Scoped) implementation: it resolves the token hash, folds capability and scoped Time Entry state via `IEventStoreGatewayClient.ReadStreamAsync`, and loads a fresh Activity Type catalog with deterministic (clock-free) projection freshness. Token→capability resolution uses a new rebuildable, **non-authoritative** candidate index (`MagicLinkTokenHashCapabilityIndexProjection`, folded only from `MagicLinkConfirmationCapabilityIssued` events, storing only the token hash); single-use/revoked/expired truth still comes from folding the authoritative capability aggregate. A new `ITimesheetsTrustedContextAccessor` seam supplies tenant context (host wires `HttpContextTimesheetsTrustedContextAccessor`; kernel default stays fail-closed `Unavailable`). Story 3.7 added `MagicLinkConfirmationHttpBoundaryTests`, the module's first over-the-wire `WebApplicationFactory<Program>` proof (11 invalid cases × 4 external routes), and wired denial diagnostics (`LogExternalLinkDenial`, EventId 37001) carrying only correlation id, timestamp, and outcome category. **Remaining launch-readiness work (Epic 5):** the token-hash candidate index has no live projection-host wiring yet, so a valid magic link does not resolve end-to-end in the running topology (the index reads empty); invalid-link no-disclosure is fully proven regardless.

**Data protection and secrets:**

Use ASP.NET Core Data Protection only for purpose-bound protection where useful, not as the only source of revocation or single-use truth. Production deployments must use a shared persisted key ring where protected tokens/cursors need to survive restarts or multiple replicas.

**Security posture:**

- No browser-owned backend access tokens for internal UI; use server-side/BFF-style integration where FrontComposer patterns require it.
- No direct trust in caller-supplied tenant/user IDs, correlation IDs, or server-controlled fields.
- Comments are sensitive unstructured data by default and must be excluded from logs/traces.
- Exports require explicit authorization and audit metadata.
- Denied/unknown/stale/unavailable authorization outcomes fail closed and avoid existence disclosure.

**Rationale:**

This matches the PRD's security requirements and Hexalith module patterns: identity is external, tenant/resource authorization is local and fail-closed, magic links are narrow audited capabilities, and Timesheets does not weaken EventStore/Tenants/Parties boundaries.

### API & Communication Patterns

**Command API model:**

Timesheets writes use the Hexalith.EventStore command pipeline. Public/internal consumers submit Timesheets commands through the established command gateway shape rather than calling aggregate hosts directly.

Baseline:

- Command contracts live in `Hexalith.Timesheets.Contracts`.
- The domain host processes commands through EventStore domain-service integration.
- Command outcomes include accepted/completed/rejected status through EventStore command status patterns.
- Domain business failures are rejection events or typed domain outcomes, not transport exceptions.

**Query API model:**

Timesheets exposes query contracts and read models for operational views, approval queues, reports, AI effort reporting, Approved-Time Ledger reads, export readiness, and dashboard overview composition.

Baseline:

- Query contracts hide EventStore envelope mechanics.
- Query results include projection freshness/trust metadata.
- Stale, rebuilding, unavailable, forbidden, and degraded states are explicit outcomes.
- Trust-bearing decisions such as approval/export must require current/fresh enough projections or fall back to command-side validation.
- Epic 4 implementation note: `QueryTimeEntries`, `QueryApprovedTimeLedger`, `QueryProjectActualTimeReport`, `QueryWorkActualTimeReport`, and `QueryTimesheetsDashboardOverview` are typed contract surfaces. Dashboard overview is read-only composition over existing query services and action-policy outcomes; it must not become write authority or a dashboard-specific state store.

**External submission API:**

External-party API submission uses the same command/query boundary as internal flows. It cannot bypass tenant gates, reference validation, approval, correction, locking, or audit rules.

**Magic-link communication:**

Magic-Link Confirmation uses a narrow web/API surface for a single scoped capability. It must not expose generic Timesheets browsing APIs or internal module navigation.

**Service-to-service communication:**

Use Dapr service invocation/pub-sub through existing Hexalith/EventStore patterns. Timesheets should not call databases, brokers, or sibling infrastructure directly. Cross-module checks go through adapters/projections/clients for Tenants, Parties, Projects, and Works.

**API documentation:**

Use the existing Hexalith documentation/OpenAPI conventions for public HTTP surfaces. Do not create a separate OpenAPI contract for internal aggregate mechanics.

Epic 4 implementation note: `PreviewApprovedTimeExport` is a contract shape; the dedicated server preview handler was resolved in Story 4.9 (see the readiness-repair status note below) and is now served by `ApprovedTimeExportService.PreviewAsync` alongside Approved-Time Ledger query output and `GenerateApprovedTimeExport` results.

**Readiness repair (approved 2026-06-20; Story 4.9 completed 2026-06-22):** export preview behavior is owned by Epic 4 follow-up Story 4.9. _Status (2026-06-22):_ Story 4.9 is complete - `ApprovedTimeExportService.PreviewAsync` is a concrete, side-effect-free server preview path covered by service and integration tests; the resolved decision is a ledger/service-driven preview with **no dedicated HTTP route** - the `timesheets.query.approved-ledger-export-preview` capability is advertised in metadata only. Mapping a preview HTTP endpoint is recorded as **post-v1** in `docs/launch-readiness.md`. Epic 5 verifies this evidence and documentation only.

**Version notes:**

- `Microsoft.AspNetCore.OpenApi` package line observed at `10.0.9`.
- `Swashbuckle.AspNetCore` latest observed package line is `10.2.1`.
- `ModelContextProtocol.AspNetCore` latest observed package line is `1.4.0`; MCP is not selected for v1 unless a later architecture decision adds a Timesheets MCP surface.

**Error handling:**

Transport errors use ProblemDetails/RFC-style responses where HTTP surfaces are exposed. Domain rejections remain typed domain outcomes/events and should be visible through command status or query result shapes without leaking protected identifiers.

**Rationale:**

This keeps Timesheets consistent with Hexalith: command/query contracts are the integration surface, EventStore owns persistence and command routing, Dapr is the distributed-app abstraction, and public APIs do not expose aggregate internals.

### Frontend Architecture

**Internal UI model:**

Internal Timesheets surfaces use Hexalith.FrontComposer first, with Blazor Fluent UI V5 components where generated surfaces need explicit composition.

Baseline internal surfaces:

- Time Entry capture.
- My Timesheet Period.
- Approvals Queue.
- Time Entry Detail.
- Period Approval Detail.
- Activity Type Catalog.
- Operational Reports.
- AI Effort Report.
- Approved-Time Ledger.
- Export Review Dialog.

**Fluent UI decision:**

All Blazor UI components must use `Microsoft.FluentUI.AspNetCore.Components` V5 only. Do not use V4 components for Timesheets UI.

Icons may use the V4 Fluent UI icons package only when a V5 icon source is unavailable. This exception applies to icons only, not components.

**Version note:**

The intended Timesheets UI policy is Fluent UI V5 components only. Current Timesheets root package files do not directly pin Fluent UI because no Timesheets UI project exists yet. The submodule-owned `Hexalith.Builds` package props still carry `Microsoft.FluentUI.Components` `4.11.6`; this is retained as a platform waiver until a Timesheets-owned UI/package story adds direct V5 pins or the platform package policy is reconciled.

**Component architecture:**

Use generated command/projection surfaces for standard create, submit, approve, reject, correct, catalog, report, and export workflows. Hand-authored Blazor components are allowed only for behavior not expressible through FrontComposer metadata or existing Fluent UI V5 components.

**State and data freshness:**

Frontend state must represent projection freshness explicitly. Approval/export decisions must not present stale/rebuilding/unavailable projections as fresh evidence.

Required visible states include:

- Draft, Submitted, Approved, Rejected, Corrected, Superseded/Locked.
- Stale reference, missing reference, unauthorized reference.
- External confirmed/expired/used/revoked link.
- AI-assisted / AI metrics unavailable.
- Exportable / non-exportable / stale export scope.

**External Magic-Link Confirmation UI:**

The external magic-link surface is a minimal responsive web page, not a full portal. It may live outside full internal shell navigation but must use Fluent UI V5 components and no-disclosure security behavior.

Invalid/expired/used/revoked links must show the same safe failure state and no tenant, Project, Work, Party, Time Entry, duration, or comment details.

**Routing and navigation:**

Use FrontComposer shell/module navigation for internal surfaces. Project and Work modules may deep-link into record-time, detail, or filtered report surfaces through stable IDs. Timesheets must not expose EventStore, raw stream, Party profile, Project lifecycle, Work lifecycle, invoicing, payroll, or rate-card UI as its own surfaces.

**Accessibility and responsiveness:**

Target WCAG 2.2 AA for internal and external web surfaces. Use Fluent UI V5 keyboard/focus/dialog/grid patterns. The magic-link page must be fully usable on phone viewports. Internal surfaces are desktop/laptop-first but responsive.

**Rationale:**

This follows the accepted UX spine and Hexalith FrontComposer rules: Timesheets is an operational evidence UI, not a decorative dashboard or generic timer app. UI must make evidence state, projection freshness, approval/correction lineage, and no-disclosure behavior visible.

### Infrastructure & Deployment

**Local topology:**

Use Aspire AppHost as the local topology owner. Timesheets AppHost should compose Timesheets with EventStore, Tenants, Parties, Projects, Works, Dapr components, Redis/state/pubsub resources, Keycloak when enabled, and any required UI host.

Aspire owns orchestration and observability wiring only; it does not define domain architecture.

**Dapr runtime and SDK:**

Use Dapr for service invocation, pub/sub, actors/state abstractions, and EventStore integration according to Hexalith patterns.

Target Dapr SDK package line for Timesheets-owned direct pins: latest verified `1.18.4`, subject to scaffold compatibility checks. Current root package files do not directly pin Dapr SDK packages; see the launch-readiness package-currency waiver for the submodule-owned `Hexalith.Builds` base `Dapr` `1.17.9` divergence.

**Deployment shape:**

The Timesheets host is the deployable domain service. Library packages such as `Contracts`, `Client`, `Server`, `Projections`, `Testing`, and any future `UI` package ship as NuGet/package artifacts according to Hexalith conventions.

Use .NET SDK container support where containers are needed; do not introduce Dockerfiles unless a later deployment decision explicitly requires one.

**Environment configuration:**

Use `.NET` configuration conventions with `__` environment nesting. Secrets and identity provider settings come from environment/secret stores, not source-controlled config.

AppHost changes require restarting `aspire run`.

**CI/CD and validation:**

Use module-local `.slnx`, Central Package Management, warnings-as-errors, and targeted test lanes. CI should validate:

- restore/build
- package references and no inline versions
- architecture/fitness tests
- unit tests
- projection replay/idempotency tests
- security/tenant-isolation tests
- integration tests where Docker/Dapr/Aspire are required

**Observability:**

Use OpenTelemetry and Aspire dashboard conventions inherited from sibling modules. Logs and traces must never include comments, tokens, secrets, command bodies, event payloads, magic-link values, or personal data.

**Scaling and reliability:**

Design projections and event handlers for at-least-once delivery, duplicate events, replay, and rebuild. Trust-bearing reads must surface freshness and degradation rather than pretending data is current.

**Rationale:**

This keeps Timesheets aligned with Hexalith deployment patterns while preserving EventStore as the persistence authority and Dapr/Aspire as platform abstractions.

### Decision Impact Analysis

**Implementation Sequence:**

1. Scaffold Hexalith.Timesheets module shell and normalize package/build/test conventions.
2. Define contracts for Time Entry, Timesheet Period, Activity Type, correction, AI effort metrics, projection freshness, and typed rejections.
3. Implement pure aggregate/state logic and domain tests.
4. Wire EventStore domain-service command handling and command status integration.
5. Implement projections and replay/idempotency tests.
6. Add tenant/resource authorization gates and reference validation adapters.
7. Add Magic-Link Confirmation capability and abuse/no-disclosure tests.
8. Add FrontComposer metadata and Fluent UI V5 surfaces.
9. Add exports, audit metadata, and export golden-file tests.
10. Wire Aspire AppHost topology and integration tests.

**Cross-Component Dependencies:**

- Approval, export, and magic-link confirmation depend on tenant/resource authorization and reference validation.
- UI evidence states depend on projection freshness and typed reference-state outcomes.
- Export correctness depends on Approved-Time Ledger replay behavior and correction lineage.
- AI effort reporting depends on preserving multiple effort units in contracts and projections.
- Infrastructure validation depends on Dapr/Aspire compatibility and EventStore domain-service wiring.

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:** 34 areas where AI agents could otherwise make incompatible choices across naming, structure, formats, communication, process, and enforcement.

### Naming Patterns

**Persistence Naming Conventions:**

- Timesheets does not define module-owned database tables or direct persistence schemas.
- Domain state changes are persisted only through Hexalith.EventStore.
- Dapr state keys, stream names, checkpoints, and projection offsets must use existing Hexalith/EventStore naming helpers or conventions; agents must not invent ad hoc key formats.
- If a projection later requires a physical schema, table/container/index names must be documented in this architecture before implementation.

**API Naming Conventions:**

- Public HTTP routes, when needed, use lowercase plural resource names under a Timesheets-owned route prefix, for example `/api/timesheets/time-entries`.
- Route parameters use ASP.NET Core route-token syntax, for example `{tenantId}`, `{timeEntryId}`, `{periodId}`.
- Query parameters use camelCase, for example `projectId`, `workId`, `fromDate`, `toDate`, `includeFreshness`.
- Magic-link routes are capability-specific and must not expose browse/query surfaces. Use action-specific names such as `confirm`, `reject`, or `acknowledge`; never expose a general token inspection endpoint.
- Headers use standard headers where possible. Custom headers use the existing Hexalith convention or `X-Hexalith-*` only when no convention exists.

**Code Naming Conventions:**

- Namespaces start with `Hexalith.Timesheets`.
- Project names follow `Hexalith.Timesheets.{Area}`, for example `Hexalith.Timesheets.Contracts`, `Hexalith.Timesheets.Server`, `Hexalith.Timesheets.Projections`.
- Commands use imperative names, for example `RecordTimeEntry`, `SubmitTimesheetPeriod`, `ApproveTimeEntry`.
- Events use past-tense domain names, for example `TimeEntryRecorded`, `TimesheetPeriodSubmitted`, `TimeEntryCorrected`.
- Queries use result-oriented names, for example `GetTimeEntries`, `GetApprovedTimeLedger`, `GetTimesheetPeriodSummary`.
- Projection/read-model types end with `Projection`, `View`, `Summary`, or `ReadModel` according to the surrounding package convention.
- Private fields use `_camelCase`; public members use PascalCase; asynchronous methods use the `Async` suffix.

### Structure Patterns

**Project Organization:**

- Contracts live in `src/Hexalith.Timesheets.Contracts` and must not reference infrastructure packages.
- Client abstractions live in `src/Hexalith.Timesheets.Client`.
- Server command handling, policies, and application services live in `src/Hexalith.Timesheets.Server`.
- Projection handlers and read models live in `src/Hexalith.Timesheets.Projections`.
- Test helpers live in `src/Hexalith.Timesheets.Testing`.
- Aspire composition lives in `src/Hexalith.Timesheets.AppHost`.
- Internal UI, when implemented, lives in a Timesheets UI project and uses FrontComposer plus Blazor Fluent UI V5 components only.
- Tests live under `tests/Hexalith.Timesheets.*.Tests`; test files use `{Subject}Tests.cs`.

**File Structure Patterns:**

- Commands, events, queries, value objects, policies, projections, and test fixtures are grouped by domain capability first: Time Entries, Periods, Activity Types, Magic Links, Reporting, Exports.
- Shared helpers are allowed only when used by at least two capabilities and must live in an explicit `Common` or `Abstractions` area.
- Configuration uses .NET configuration binding with `__` environment variable nesting.
- Package versions are centralized only; no inline `PackageReference` versions.

### Format Patterns

**API Response Formats:**

- Query APIs return typed DTOs/read models, not raw EventStore envelopes.
- Query results that depend on projections include freshness or trust metadata.
- Transport errors use ASP.NET Core ProblemDetails.
- Domain rejections remain domain outcomes/events and must not be collapsed into ambiguous transport errors.

**Data Exchange Formats:**

- JSON uses camelCase through `System.Text.Json`.
- Dates and instants use ISO-8601 strings. Store UTC instants for audit and tenant-local dates/period keys where business rules require them.
- Durations are represented in whole minutes unless a specific AI metric requires a documented unit.
- AI effort metrics must carry explicit units and source metadata; unavailable metrics are null/absent, never `0`.
- IDs are stable strings at module boundaries. Do not parse sibling-owned IDs as GUIDs unless that owner's contract requires it.
- Comments, token values, personal data, and command payloads are never logged, exported by default, or included in diagnostic metadata.

### Communication Patterns

**Event System Patterns:**

- Events are additive and immutable. Do not rename or remove existing event fields once published.
- Prefer new event types or optional fields for evolution.
- Event handlers and projections must tolerate at-least-once delivery, duplicate messages, out-of-order rebuilds where applicable, and replay.
- Projection updates must be idempotent and checkpointed.
- Event payloads store sibling references, not copied Project, Work, Party, or Tenant state.

**State Management Patterns:**

- EventStore aggregate state is authority for commands.
- Projections are read-optimized views and must expose stale, degraded, rebuilding, or unavailable states rather than pretending to be authoritative.
- UI state must distinguish draft, validating, submitted, approved, rejected, locked, corrected, stale, and unavailable states where relevant.
- Blazor UI components use Fluent UI V5 only; V4 is permitted only for icons when a V5 icon source is unavailable.

### Process Patterns

**Validation Patterns:**

- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure.
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes.
- Aggregates enforce invariant consistency; adapters enforce external authority checks.
- Draft display may tolerate stale hydration; approval, export, submission, correction, and magic-link confirmation fail closed on missing authority.

**Error Handling Patterns:**

- User-facing errors are specific enough to guide action but must not disclose unauthorized tenant, entry, token, Project, Work, or Party existence.
- Magic-link invalid, expired, consumed, unauthorized, or unknown states use the same non-disclosing response pattern.
- Retries belong around infrastructure adapters and projection consumers, not inside aggregate decision logic.

**Loading State Patterns:**

- Loading states are explicit in UI and read models: loading, ready, stale, degraded, rebuilding, unavailable.
- Stale data cannot be used for approval, export, or confirmation decisions unless the architecture is explicitly amended.

### Enforcement Guidelines

**All AI Agents MUST:**

- Persist domain changes only through Hexalith.EventStore.
- Keep contracts infrastructure-free.
- Use Dapr SDK `1.18.4` unless a later architecture update replaces it.
- Use Blazor Fluent UI V5 components only; V4 icon package is an icons-only fallback.
- Keep package versions centralized.
- Treat projections as rebuildable, idempotent, and non-authoritative for writes.
- Avoid logging payloads, comments, personal data, secrets, magic-link tokens, or command bodies.
- Preserve additive event and contract evolution.

**Pattern Enforcement:**

- Add architecture/fitness tests for forbidden package references, inline package versions, `.sln` files, direct persistence bypass, and Contracts infrastructure references.
- Add projection replay/idempotency tests for every projection.
- Add security tests for tenant isolation, magic-link no-disclosure behavior, and fail-closed authorization.
- Add UI package checks preventing Fluent UI V4 components while allowing V4 icons only when needed.
- Document any pattern exception in this architecture before implementation.

### Pattern Examples

**Good Examples:**

- `RecordTimeEntry` command emits `TimeEntryRecorded`.
- `ApproveTimeEntry` fails closed when Project or Tenant authority cannot be verified.
- `GetApprovedTimeLedger` returns read data plus projection freshness metadata.
- A projection handler safely ignores a duplicate `TimeEntryRecorded` message.
- A magic-link failure returns a neutral response and logs only correlation ID, tenant scope hash/reference, and outcome category.

**Anti-Patterns:**

- Writing Time Entry rows directly to SQL, Redis, or Dapr state outside EventStore.
- Treating a projection as authority for approval or export.
- Copying Project, Work, Party, or Tenant owned data into Timesheets events.
- Logging comments, tokens, command payloads, or personal names.
- Using Fluent UI V4 components in Timesheets UI.
- Inventing custom event naming such as `time_entry.created` beside `TimeEntryRecorded`.
- Returning raw EventStore envelopes from query APIs.

## Project Structure & Boundaries

### Complete Project Directory Structure

```text
Hexalith.Timesheets/
|-- Hexalith.Timesheets.slnx
|-- global.json
|-- Directory.Packages.props
|-- Directory.Build.props
|-- .editorconfig
|-- README.md
|-- docs/
|   |-- architecture/
|   |-- api/
|   `-- operations/
|-- src/
|   |-- Hexalith.Timesheets/
|   |   |-- Program.cs
|   |   |-- appsettings.json
|   |   |-- appsettings.Development.json
|   |   |-- Authentication/
|   |   |-- Authorization/
|   |   |-- Configuration/
|   |   |-- Endpoints/
|   |   |   |-- Commands/
|   |   |   |-- Queries/
|   |   |   `-- MagicLinks/
|   |   |-- HealthChecks/
|   |   `-- OpenApi/
|   |-- Hexalith.Timesheets.Contracts/
|   |   |-- Commands/
|   |   |   |-- TimeEntries/
|   |   |   |-- TimesheetPeriods/
|   |   |   |-- ActivityTypes/
|   |   |   |-- MagicLinks/
|   |   |   `-- Exports/
|   |   |-- Events/
|   |   |   |-- TimeEntries/
|   |   |   |-- TimesheetPeriods/
|   |   |   |-- ActivityTypes/
|   |   |   |-- MagicLinks/
|   |   |   `-- Rejections/
|   |   |-- Queries/
|   |   |   |-- TimeEntries/
|   |   |   |-- TimesheetPeriods/
|   |   |   |-- Reporting/
|   |   |   `-- Exports/
|   |   |-- Models/
|   |   |-- Identifiers/
|   |   |-- State/
|   |   |-- Ui/
|   |   |-- ValueObjects/
|   |   `-- openapi/
|   |-- Hexalith.Timesheets.Client/
|   |   |-- Abstractions/
|   |   |-- Commands/
|   |   |-- Queries/
|   |   |-- MagicLinks/
|   |   `-- Extensions/
|   |-- Hexalith.Timesheets.Server/
|   |   |-- Aggregates/
|   |   |   |-- TimeEntries/
|   |   |   |-- TimesheetPeriods/
|   |   |   `-- ActivityTypes/
|   |   |-- Authorization/
|   |   |-- CommandHandlers/
|   |   |-- Exports/
|   |   |-- MagicLinks/
|   |   |-- Policies/
|   |   |-- ReferenceValidation/
|   |   `-- Registration/
|   |-- Hexalith.Timesheets.Projections/
|   |   |-- ApprovedTimeLedger/
|   |   |-- Freshness/
|   |   |-- Handlers/
|   |   |-- Models/
|   |   |-- OperationalReports/
|   |   |-- Replay/
|   |   `-- Strategies/
|   |-- Hexalith.Timesheets.Works/
|   |   |-- PlannedEffort/
|   |   |-- ReferenceValidation/
|   |   `-- Runtime/
|   |-- Hexalith.Timesheets.Testing/
|   |   |-- Builders/
|   |   |-- Fakes/
|   |   |-- MagicLinks/
|   |   |-- ReferenceValidation/
|   |   |-- Replay/
|   |   `-- TenantIsolation/
|   |-- Hexalith.Timesheets.ServiceDefaults/
|   `-- Hexalith.Timesheets.AppHost/
|       |-- Program.cs
|       |-- appsettings.json
|       |-- DaprComponents/
|       |-- KeycloakRealms/
|       `-- Properties/
`-- tests/
    |-- Hexalith.Timesheets.ArchitectureTests/
    |-- Hexalith.Timesheets.Contracts.Tests/
    |-- Hexalith.Timesheets.Server.Tests/
    |-- Hexalith.Timesheets.Projections.Tests/
    |-- Hexalith.Timesheets.IntegrationTests/
    |   |-- SchemaEvolution/Golden/
    |   `-- Exports/Golden/
    `-- Hexalith.Timesheets.Works.Tests/
```

Status note (2026-06-22): no `Hexalith.Timesheets.UI`, `UI.Tests`, `UnitTests`, `Security.Tests`, or `PropertyTests` project exists on disk. Security, tenant-isolation, property-like invariants, and architecture fitness coverage currently live in `ArchitectureTests`, `Server.Tests`, `IntegrationTests`, `Projections.Tests`, and `Works.Tests`. The UI projects remain deferred to the first UI-bearing story.

### Architectural Boundaries

**API Boundaries:**

- `Hexalith.Timesheets` is the deployable HTTP/service host.
- Command endpoints submit through the EventStore-backed command pipeline.
- Query endpoints return typed Timesheets read models with projection freshness metadata.
- Magic-link endpoints are isolated under `Endpoints/MagicLinks` and expose only token-scoped actions.
- No API endpoint may bypass tenant/resource gates, reference validation, approval state, locking, or audit behavior.

**Component Boundaries:**

- `Contracts` contains commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only.
- `Client` wraps command/query/magic-link interaction for consumers and UI.
- `Server` owns aggregate decisions, command handlers, policies, validation orchestration, export orchestration, and magic-link capability logic.
- `Projections` owns read-model handlers, replay behavior, freshness, Approved-Time Ledger, and operational report models.
- `UI` owns FrontComposer metadata and Blazor Fluent UI V5 component composition only.
- `AppHost` owns local topology and observability wiring, not domain rules.

**Service Boundaries:**

- Timesheets integrates with EventStore for persistence and command processing.
- Timesheets uses Tenants for tenant access authority, Parties for contributor identity references, Projects for Project authority, and Works for Work authority.
- Sibling modules are accessed through contracts/adapters, never through copied state or direct infrastructure calls.

**Data Boundaries:**

- EventStore is the only authoritative persistence boundary.
- Projections are rebuildable, idempotent, and non-authoritative for writes.
- Timesheets stores sibling references, not owned sibling data.
- Export data is produced from approved ledger projections plus authoritative audit state.

### Requirements To Structure Mapping

**Time Entry Ledger:**

- Contracts: `Contracts/Commands/TimeEntries`, `Contracts/Events/TimeEntries`, `Contracts/Queries/TimeEntries`
- Domain: `Server/Aggregates/TimeEntries`
- Reads: `Projections/OperationalReports`, `Projections/Models`
- Tests: `Server.Tests`, `Projections.Tests`, `IntegrationTests`

**Submission, Approval, Rejection, And Locking:**

- Contracts: `Contracts/Commands/TimesheetPeriods`, `Contracts/Events/TimesheetPeriods`, `Contracts/Events/Rejections`
- Domain: `Server/Aggregates/TimesheetPeriods`, `Server/Policies`
- Tests: `Server.Tests`, `ArchitectureTests`, `IntegrationTests`

**Activity Type Catalogs:**

- Contracts: `Contracts/Commands/ActivityTypes`, `Contracts/Events/ActivityTypes`
- Domain: `Server/Aggregates/ActivityTypes`
- Reads: `Projections/Models`
- Tests: `Server.Tests`, `IntegrationTests`

**Actor-Neutral Contributors And Magic Links:**

- Contracts: `Contracts/Commands/MagicLinks`, `Contracts/Events/MagicLinks`
- Domain: `Server/MagicLinks`, `Server/ReferenceValidation`
- Host: `Endpoints/MagicLinks`
- Tests: `Server.Tests`, `IntegrationTests`, `ArchitectureTests`

**Reporting, Approved-Time Ledger, And Exports:**

- Contracts: `Contracts/Queries/Reporting`, `Contracts/Queries/Exports`, `Contracts/Commands/Exports`
- Reads: `Projections/ApprovedTimeLedger`, `Projections/OperationalReports`
- Domain: `Server/Exports`
- Tests: `Projections.Tests`, `IntegrationTests/Exports/Golden`

**Platform Boundary:**

- Host: `src/Hexalith.Timesheets`
- App topology: `src/Hexalith.Timesheets.AppHost`
- Shared defaults: `src/Hexalith.Timesheets.ServiceDefaults`
- Enforcement: `tests/Hexalith.Timesheets.ArchitectureTests`

### Integration Points

**Internal Communication:**

- UI and clients call typed client abstractions.
- Client abstractions call Timesheets command/query/magic-link endpoints.
- Host endpoints call Server application services.
- Server submits authoritative changes through EventStore.
- Projections consume events and publish read models/freshness state.

**External Integrations:**

- EventStore: authoritative event persistence and command processing.
- Tenants: tenant membership, role, and access checks.
- Parties: contributor identity and display hydration.
- Projects: Project reference validation and authority checks.
- Works: Work reference validation and effort context.
- Dapr: service invocation, pub/sub, and state abstractions where required by established EventStore/Aspire patterns.
- Keycloak/JWT: production identity provider when enabled.

**Reference-validation adapter maturity (added 2026-06-19; corrected 2026-06-22):** `Hexalith.Projects` exposes a consumer query (`GetProjectAsync`) suitable for FR-2 Project validation. `Hexalith.Works` exposes a consumer-facing `get-work-item` query (`GetWorkItemQueryHandler`, domain `work`) returning a stable `WorkItemView`/`WorkItemStatus` from `Hexalith.Works.Contracts`, suitable for FR-2 Work validation; its `WorkItemEffort` (FR-17) and `ExecutorBinding` (FR-15/FR-20) Contracts are also stable. The Works reference-validation adapter therefore consumes that `get-work-item` query (or, as an alternative, a Works EventStore projection via a Timesheets adapter). Epic 4 implemented planned-effort access behind `IWorkPlannedEffortProvider` with `UnavailableWorkPlannedEffortProvider` as the fail-closed default, so Work reports can disclose actuals while marking planned-effort state unavailable until the Works adapter is made concrete. _Correction (2026-06-22, verified during Story 1.10):_ the original 2026-06-19 statement that "`Hexalith.Works` currently exposes no consumer-facing read/validate query" was stale — `GetWorkItemQueryHandler` (`get-work-item`) and `WorkItemView` ship today and are consumed by Story 1.10's `WorksQueryWorkReferenceValidator`.

**Readiness repair (approved 2026-06-20; Stories 1.10 and 4.8 completed 2026-06-22):** Work-reference validation is owned by Epic 1 follow-up Story 1.10, and planned-effort reporting is owned by Epic 4 follow-up Story 4.8. Launch claims that include Work validation or planned-vs-actual comparison require either a Works-owned consumer query or a Timesheets adapter over a Works EventStore projection, with fail-closed behavior when the adapter is unavailable, stale, cross-tenant, or unauthorized. Epic 5 verifies the evidence only after those owning stories are complete. _Status (2026-06-22):_ Story 1.10 is complete - the concrete `WorksQueryWorkReferenceValidator` (in `src/Hexalith.Timesheets.Works`) consumes the Works `get-work-item` query through the Timesheets-owned `IWorksQueryChannel` port, fails closed on unavailable/missing/cross-tenant/ambiguous/disabled states, and copies no Works-owned data. Story 4.8 is also complete - `WorksQueryWorkPlannedEffortProvider` reads Works effort context through the same port and keeps the host on `UnavailableWorkPlannedEffortProvider` unless the opt-in is called. (Note: the adapter cannot detect Works projection staleness from `WorkItemView` alone - see the Story 1.10 behavior note.)

**Export audit implementation note (added 2026-06-19):** accepted approved-time exports emit safe `ApprovedTimeExported` domain-event evidence through `IApprovedTimeExportAuditRecorder`. The audit evidence stores requester, tenant, filter snapshot, UTC request/generation instants, correlation ID, output scope, CSV format/version, projection freshness state, row count, and output content hash. It does not store CSV rows, comments, display labels, credential material, caller bodies, raw EventStore envelopes, or sibling-owned state.

**Readiness repair (approved 2026-06-20; Stories 1.11 and 4.10 completed 2026-06-22):** realistic end-to-end performance evidence remains reserved until EventStore-backed persisted fixtures exist, but ownership moves to the feature paths that create the measured behavior. Epic 1 follow-up Story 1.11 covers capture/governance command evidence, Epic 4 follow-up Story 4.10 covers report/export/dashboard evidence, and Epic 5 only aggregates the final evidence or waivers. _Status (2026-06-22):_ Story 1.11 is complete - the opt-in `TIMESHEETS_PERF=1` lane measures the in-process composed capture/governance command path (11 scenarios, worst-case p95 about 0.0056 ms vs the NFR10 500 ms target), **verdict pass** for the in-process command-decision path. Story 4.10 is complete - the same opt-in lane measures report/export/dashboard query paths (9 scenarios, worst-case p95 about 6.09 ms vs the NFR11 2s target), **verdict pass** for measured in-process reads. The full EventStore-backed wire path is recorded **waived/deferred** (needs runtime fixtures) so Epic 5 aggregates rather than re-measures. See `docs/performance-evidence.md`.

**Data Flow:**

1. Request enters host endpoint.
2. Tenant/resource authorization runs.
3. Reference validation adapters check sibling authority.
4. Command is dispatched to EventStore/domain handling.
5. Domain event is persisted.
6. Projection handlers update read models idempotently.
7. Query/UI/export surfaces read projection state with freshness metadata.

### File Organization Patterns

**Configuration Files:**

- Root build/package config stays at repository root.
- Host runtime config stays in `src/Hexalith.Timesheets`.
- Local orchestration config stays in `src/Hexalith.Timesheets.AppHost`.
- Dapr component files stay in `AppHost/DaprComponents`.
- Secrets are never committed.

**Source Organization:**

- Source is organized by package first, then capability.
- Capability folders use domain names: `TimeEntries`, `TimesheetPeriods`, `ActivityTypes`, `MagicLinks`, `Reporting`, `Exports`.
- Cross-cutting server behavior lives in `Authorization`, `Policies`, `ReferenceValidation`, and `Registration`.

**Test Organization:**

- Architecture tests enforce package boundaries and version/package rules.
- Unit/property tests cover aggregate invariants and correction/locking semantics.
- Projection tests cover replay, duplicates, idempotency, freshness, and rebuilds.
- Security tests cover tenant isolation, fail-closed behavior, and magic-link no-disclosure responses.
- Integration tests cover EventStore/Dapr/Aspire paths and export golden files.

**Asset Organization:**

- UI static assets stay under `src/Hexalith.Timesheets.UI/wwwroot`.
- FrontComposer descriptors and UI registration stay in `UI/Composition`.
- No static assets are required for backend-only packages.

### Development Workflow Integration

**Development Server Structure:**

- Run local topology from `src/Hexalith.Timesheets.AppHost`.
- Run the deployable Timesheets host from `src/Hexalith.Timesheets`.
- Restart `aspire run` after AppHost topology changes.

**Build Process Structure:**

- Build through `Hexalith.Timesheets.slnx`.
- Restore package versions from `Directory.Packages.props`.
- Treat warnings as errors through shared build props.
- Do not generate or maintain `.sln`.

**Deployment Structure:**

- `Hexalith.Timesheets` is the deployable service.
- Library packages ship from `Contracts`, `Client`, `Server`, `Projections`, `Testing`, and `UI` when enabled.
- Containers use .NET SDK container support unless a later decision explicitly requires Dockerfiles.

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:**
The technology choices work together: .NET 10, Hexalith.EventStore, Dapr SDK policy target `1.18.4`, Aspire AppHost orchestration, JWT/OIDC authentication, FrontComposer, and Blazor Fluent UI V5 each have clear boundaries. Aspire owns local topology; EventStore owns persistence; Dapr supports platform communication patterns. Actual package pins are tracked in the launch-readiness package-currency verdict so policy targets are not mistaken for every root/submodule package state.

**Pattern Consistency:**
Implementation patterns support the decisions. Commands, events, queries, projections, magic links, exports, and UI all have consistent naming, structure, error, freshness, and enforcement rules.

**Structure Alignment:**
The project structure supports all architecture decisions with distinct host, contracts, client, server, projections, UI, testing, service defaults, and AppHost packages.

### Requirements Coverage Validation ✅

**Feature Coverage:**
All six requirement areas are mapped to components: Time Entry Ledger, Submission/Approval/Rejection/Locking, Activity Types, Actor-Neutral Contributors, Reporting/Exports, and Platform Boundary.

**Functional Requirements Coverage:**
All 23 functional requirements are architecturally supported through EventStore-backed commands/events, projections, authorization gates, reference validation, magic-link capability boundaries, and export/reporting structures.

**Non-Functional Requirements Coverage:**
Security, tenant isolation, auditability, data minimization, projection reliability, observability privacy, compatibility, accessibility, and performance targets are addressed by explicit decisions and enforcement tests.

### Implementation Readiness Validation ✅

**Decision Completeness:**
Critical decisions are documented with versions where version-sensitive: Dapr SDK policy target `1.18.4`, Blazor Fluent UI V5 component policy, V4 icons-only fallback, .NET 10, Aspire, EventStore, OpenAPI, JWT/OIDC, and Keycloak usage. Current root/submodule package-state divergence is intentionally recorded as launch-readiness package-currency evidence.

**Structure Completeness:**
The project tree is specific enough for implementation agents to scaffold consistently and maps requirements to concrete projects, folders, and tests.

**Pattern Completeness:**
Naming, structure, API format, data format, event communication, projection behavior, validation, error handling, loading states, UI package rules, and enforcement checks are defined.

### Gap Analysis Results

**Critical Gaps:** None.

**Important Gaps:** None.

**Nice-To-Have Gaps:**

- Exact Dapr component manifest names can be refined during AppHost implementation.
- Optional MCP/CLI helper packages remain deferred until a concrete requirement appears.
- **UI project timing (decided 2026-06-19):** `Hexalith.Timesheets.UI` and `Hexalith.Timesheets.UI.Tests` are part of the target project tree but are scaffolded with the **first UI-bearing story, not in scaffold Story 1.1** (which creates host/Contracts/Client/Server/Projections/Testing/ServiceDefaults/AppHost only). When added, UI must follow the documented `UI/` structure and the Fluent UI V5-only rule.

### Validation Issues Addressed

- Dapr version policy ambiguity resolved by selecting latest verified SDK package line `1.18.4`; actual root/submodule package pins are separately tracked by launch-readiness package-currency evidence.
- Fluent UI generation ambiguity resolved as a Timesheets policy: V5 components only and V4 allowed only for icons when V5 icons are unavailable; current package pins remain separate until a Timesheets UI package exists or platform build props are reconciled.
- Persistence ambiguity resolved: EventStore is the sole authoritative persistence path.
- Projection trust ambiguity resolved: projections are rebuildable read models and cannot authorize trust-bearing writes.

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**

- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**

- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**

- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High

**Key Strengths:**

- Strong EventStore-first persistence boundary.
- Explicit tenant/resource fail-closed authorization model.
- Clear projection freshness and replay/idempotency rules.
- Concrete project structure aligned with sibling Hexalith modules.
- Strong AI-agent consistency rules for names, packages, boundaries, and tests.

**Areas for Future Enhancement:**

- Add MCP/CLI package architecture only if product requirements demand them.
- Expand export format details when the first export story is written.
- Add deployment-specific manifests after the target deployment environment is selected.

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all architectural decisions exactly as documented.
- Use implementation patterns consistently across all components.
- Respect project structure and package boundaries.
- Do not bypass EventStore, tenant/resource gates, or sibling-module contracts.
- Keep Fluent UI component usage on V5 only.

**First Implementation Priority:**
Scaffold the Hexalith.Timesheets module shell with `.slnx`, Central Package Management, Contracts, Client, Server, Projections, Testing, ServiceDefaults, AppHost, and the initial architecture tests enforcing package boundaries.
