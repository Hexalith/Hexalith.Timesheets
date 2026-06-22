---
title: "Hexalith.Timesheets — Product Requirements Document"
status: draft
created: 2026-06-18
updated: 2026-06-19
---

# PRD: Hexalith.Timesheets

## 0. Document Purpose

This PRD specifies v1 of **Hexalith.Timesheets**, the launch-grade Hexalith module for recording, approving, correcting, and reporting time spent against **Project** and **Work** references. It is written for product stakeholders, the architect who will turn it into a solution design, the developers who will implement the module, and the owners of dependent Hexalith modules. It builds on the product brief (`../../briefs/brief-timesheets-2026-06-18/brief.md`) and brief addendum (`../../briefs/brief-timesheets-2026-06-18/addendum.md`). Vocabulary is anchored in §3 Glossary, Functional Requirements are numbered globally, and inferred choices are tagged inline with `[ASSUMPTION]` and collected in §15.

## 1. Vision

Hexalith.Timesheets is the trusted effort ledger of the Hexalith ecosystem. It records who spent time, when, for how long, against which Project or Work item, why, whether that time is billable, and whether it has been approved. A Time Entry is not a mutable spreadsheet row; it is an attributable, tenant-scoped fact with an approval and correction history.

The module matters because Hexalith work is performed by more than internal employees. Internal users, external parties, and AI agents all contribute effort, and all three need a common time-evidence model. Timesheets treats every contributor as a **Party** and every recorded duration as an auditable event, whether it came from a staff member, a supplier, a customer, or an AI agent executing work through Hexalith.Works.

v1 proves the core module boundary: Projects and Works own their own state; Parties owns identity and personal data; Tenants owns access and isolation; EventStore owns event-sourced persistence. Timesheets owns **Time Entries**, **Timesheet Periods**, **Approval State**, **Corrections**, **Activity Types**, **Billable Flags**, **AI Effort Metrics**, and the **Approved-Time Ledger** used by operational and finance consumers.

## 2. Why Now and Market Position

Generic time-tracking products already cover the familiar market baseline. Harvest positions around time tracking, reporting, invoicing, budgets, team management, and profitability. Asana now offers centralized timesheet management, approvals, billable/non-billable rates, budgets, cost reporting, and exports. Clockify supports weekly/monthly approvals, locking approved time, audit trails, and billing/payroll handoff. Tempo lets Jira users log time directly inside work items and close approval periods after timesheets are approved.

Hexalith.Timesheets should not compete as a generic timer app. The product wedge is **actor-neutral, event-sourced time evidence inside the Hexalith domain model**. It must make human, external-party, and AI-agent effort comparable without collapsing them into the same simplistic `duration` field. Microsoft Work Trend Index framing around AI agents taking on work execution reinforces the need for accountable agent effort beside human effort.

Primary sources used for the landscape check are captured in `addendum.md`.

## 3. Target Users

### 3.1 Jobs To Be Done

- **As an internal contributor,** I need to record time against the Project or Work item I actually worked on, so I do not reconstruct effort from memory at the end of the week.
- **As an external contributor,** I need to submit or confirm time without becoming a full internal user, so my effort can be approved and billed with minimal access.
- **As an AI agent operator,** I need AI execution time, model/tool runtime, billable effort, and token consumption recorded against the work that caused it, so automation has cost and effort visibility.
- **As an approver or project manager,** I need to review, approve, reject, and lock time at entry and period levels, so reported effort becomes trusted evidence.
- **As a finance/accounting consumer,** I need approved billable time that can be exported or consumed downstream, so billing and cost workflows do not rely on informal notes.
- **As a Hexalith builder,** I need a thin module that references Projects, Works, Parties, Tenants, and EventStore without copying their state, so the ecosystem remains bounded and maintainable.

### 3.2 Non-Users in v1

- **Payroll administrators** — payroll processing and payroll compliance are out of scope.
- **Invoice owners** — Timesheets supplies approved billable evidence, but invoice generation, payment tracking, rates, and revenue recognition are downstream.
- **Desktop activity-monitoring operators** — automatic desktop/app monitoring, screenshots, idle detection, and surveillance-style tracking are out of scope.
- **External users needing a full portal** — v1 supports API-only integration and Magic-Link Confirmation, not a complete external-party portal UX.

### 3.3 Key User Journeys

- **UJ-1. Camille records her project time before submitting the week.** Camille is an internal consultant working across two client projects. From a Project or Work context, she records a Time Entry with date, duration, Activity Type, comment, and Billable Flag. On Friday, she reviews her weekly Timesheet Period and submits it. The value lands when the period shows submitted with all entries traceable back to the Project or Work references. **Edge case:** if one entry is missing a required Activity Type, submission blocks only that entry and shows the correction needed.

- **UJ-2. Nadia approves the week and rejects one entry.** Nadia is a project manager reviewing submitted time for a client project. She filters pending Time Entries by Project, opens Camille's weekly Timesheet Period, approves valid entries, and rejects one vague billable entry with a reason. The value lands when approved entries are locked into the Approved-Time Ledger and the rejected entry returns to Camille for correction. **Edge case:** if Camille corrects the rejected entry after period submission, the period shows a pending correction instead of silently changing the approved record.

- **UJ-3. Simon confirms supplier time through Magic-Link Confirmation.** Simon is an external supplier who is not a tenant user. He receives a scoped magic link for a Work item, reviews the proposed date, duration, Activity Type, comment, and Billable Flag, then confirms or adjusts the entry. The value lands when the tenant sees a Party-attributed Time Entry that can enter approval without granting Simon broader access. **Edge case:** if the link expired or was already used, Simon sees no data and must request a new confirmation link.

- **UJ-4. Ada the AI agent records execution evidence.** Ada is an AI agent assigned through Hexalith.Works. After completing a Work item step, Ada records wall-clock execution time, model/tool runtime, billable effort, and token consumption against the Work reference. The value lands when the Work item report shows AI effort beside human effort without treating tokens as human hours. **Edge case:** if token metrics are unavailable from the provider, the entry records the missing metrics explicitly rather than fabricating values.

- **UJ-5. Marc exports approved billable evidence.** Marc is a finance consumer preparing billing evidence. He filters approved billable time by tenant, Project, period, and Activity Type, then exports the Approved-Time Ledger with entry IDs, Contributor Party references, Approval State, correction lineage, and comments. The value lands when downstream billing can consume trusted time without needing Timesheets to generate invoices.

## 4. Glossary

- **Time Entry** — the core fact recorded by Timesheets: date, duration, Target Reference, Activity Type, comment, Billable Flag, Contributor, Approval State, and optional AI Effort Metrics.
- **Contributor** — the Party that performed or confirmed the time. A Contributor may be an internal user, an external party, or an AI agent.
- **Party** — the stable identity from `Hexalith.Parties`, referenced by Party ID. Timesheets does not store Party personal data.
- **Target Reference** — the object the Time Entry is recorded against: either a Project Reference or a Work Reference.
- **Project Reference** — a stable ID from `Hexalith.Projects`; Timesheets references it but does not own project structure.
- **Work Reference** — a stable ID from `Hexalith.Works`; Timesheets references it but does not own work lifecycle.
- **Activity Type** — tenant- or project-scoped category describing the nature of recorded time.
- **Billable Flag** — the Timesheets-owned classification of whether a Time Entry is billable evidence. It is not a rate, invoice line, or revenue-recognition decision.
- **Approval State** — the current review state of a Time Entry or Timesheet Period: Draft, Submitted, Approved, Rejected, Corrected, or Superseded. `[ASSUMPTION: these state names are the v1 vocabulary; architecture may map them to exact event names.]`
- **Timesheet Period** — a weekly or monthly review artifact grouping a Contributor's Time Entries for submission and approval.
- **Approver** — an authorized Party that can approve or reject Time Entries or Timesheet Periods.
- **Correction** — an additive, auditable change that fixes or supersedes a prior Time Entry without silently rewriting history.
- **Approved-Time Ledger** — the projection of approved, locked, billable and non-billable Time Entries used for reporting and downstream finance/export consumers.
- **AI Effort Metrics** — AI-specific measurements attached to a Time Entry: wall-clock execution time, model/tool runtime, billable effort, and token consumption.
- **Magic-Link Confirmation** — a scoped external-party flow that allows a Contributor to confirm or adjust a specific Time Entry without full tenant access.
- **Projection** — a recomputable read model derived from Timesheets domain events.
- **Tenant** — the isolation and access boundary owned by `Hexalith.Tenants`.

## 5. Features

### 5.1 Time Entry Ledger

**Description:** Timesheets records Time Entries against Project or Work references with enough structure to support personal capture, approval, reporting, and downstream billing evidence. The ledger is event-sourced and append-only; edits become events and corrections, not silent row mutations. Realizes UJ-1, UJ-3, UJ-4.

#### FR-1: Record a Time Entry

A Contributor can create a Time Entry with date, positive duration, Target Reference, Activity Type, comment, Billable Flag, Contributor Party ID, and initial Approval State.

**Consequences (testable):**
- Creating a Time Entry emits a durable domain event and produces a queryable Time Entry projection.
- Duration must be positive and recorded in whole minutes for human/external entries. `[ASSUMPTION: v1 records human and external durations in whole minutes; finer-grained AI runtime can be captured in AI Effort Metrics.]`
- A Time Entry must target exactly one Project Reference or one Work Reference.
- Comment requirements can be policy-driven; when required, missing comments block submission rather than creation.

#### FR-2: Validate Target References without owning them

Timesheets verifies Target References through module boundaries before accepting submitted or approved time.

**Consequences (testable):**
- Project Reference validation uses a Projects-owned API/projection/adapter; Work Reference validation uses a Works-owned API/projection/adapter.
- Timesheets stores stable IDs and minimal correlation metadata only; it does not copy Project, Work, or Party display state.
- Writes fail closed when a required Target Reference cannot be verified. `[ASSUMPTION: Project/Work reference validation fails closed for submitted/approved writes when reference status cannot be verified; Draft creation may be allowed with a stale-warning state.]`

#### FR-3: Preserve entry history and correction lineage

Timesheets must expose how each current Time Entry state was reached.

**Consequences (testable):**
- Any change to date, duration, Target Reference, Activity Type, comment, Billable Flag, Contributor, or Approval State emits an event.
- Corrected entries retain links to the original entry and correcting entry or correction event.
- Approved entries cannot be silently overwritten; correction is the only allowed mutation path after approval.

### 5.2 Submission, Approval, Rejection, and Locking

**Description:** Time becomes trusted evidence only after review. v1 supports both entry-level and period-level submission/approval, rejection with reason, and additive correction. Realizes UJ-1, UJ-2, UJ-5.

#### FR-4: Submit Time Entries for approval

A Contributor can submit Draft Time Entries for review individually or as part of a Timesheet Period.

**Consequences (testable):**
- Submission changes Approval State from Draft to Submitted and records submitter, timestamp, and submission scope.
- Submission validates required fields, Target Reference status, Activity Type validity, and policy-required comments.
- A rejected entry can be corrected and resubmitted without losing rejection reason history.

#### FR-5: Approve or reject individual Time Entries

An Approver can approve or reject submitted Time Entries with an optional or required reason according to tenant/project policy.

**Consequences (testable):**
- Approval changes the entry to Approved and makes it eligible for the Approved-Time Ledger.
- Rejection changes the entry to Rejected and records approver, timestamp, and reason.
- An Approver cannot approve their own Time Entry unless policy explicitly allows it. `[ASSUMPTION: self-approval is denied by default.]`

#### FR-6: Lock approved entries against direct edits

Approved Time Entries are locked from direct mutation and can only be corrected additively.

**Consequences (testable):**
- Direct edit commands against Approved entries are rejected with a domain rejection.
- Corrections can supersede or offset approved entries while keeping original approval evidence visible.
- Reporting can include or exclude superseded entries according to query options.

#### FR-7: Submit and approve Timesheet Periods

A Contributor can submit a weekly or monthly Timesheet Period containing their Time Entries, and an Approver can approve or reject that period.

**Consequences (testable):**
- A Timesheet Period is scoped to one Contributor, one Tenant, and one weekly or monthly period. `[ASSUMPTION: v1 does not support multi-contributor period submissions.]`
- Period submission records the included Time Entry IDs and the period boundary.
- Period approval does not erase entry-level states; it records grouped review evidence and locks approved included entries from direct edit.

#### FR-8: Reconcile entry-level and period-level approval

Timesheets must distinguish entry Approval State from period Approval State.

**Consequences (testable):**
- A Timesheet Period can show mixed entry states (Approved, Rejected, Corrected, Superseded) without losing period history.
- Period approval locks included approved entries from direct edit but does not prevent additive corrections. `[ASSUMPTION: period approval is not an irreversible freeze; corrections remain possible with audit evidence.]`
- A rejected entry inside a submitted period can be corrected and resubmitted without reconstructing the whole period from scratch.

#### FR-9: Resolve approver authority

Timesheets authorizes Approvers using tenant and project/work context.

**Consequences (testable):**
- Entry and period approval fail closed when approver authority cannot be resolved.
- The approval check can use Tenants membership/roles and Project/Work-specific approver projections.
- Default approvers are tenant admins and project managers/project approvers from Projects/Works projections. `[ASSUMPTION: exact role names and precedence are architecture decisions.]`

### 5.3 Activity Type Catalogs

**Description:** Activity Types provide the controlled vocabulary for reporting and billing evidence. v1 supports tenant-level and project-level catalogs. Realizes UJ-1, UJ-2, UJ-5.

#### FR-10: Manage tenant-level Activity Types

Authorized users can define tenant-level Activity Types that apply across projects.

**Consequences (testable):**
- Activity Types have stable IDs, display labels, active/inactive state, and optional billable-default metadata.
- Inactive Activity Types cannot be selected for new Time Entries but remain reportable for historical entries.
- Renaming an Activity Type does not rewrite historical Time Entry facts.

#### FR-11: Manage project-level Activity Types

Authorized users can define project-level Activity Types for project-specific categorization.

**Consequences (testable):**
- Project-level Activity Types are scoped to a Project Reference and tenant.
- Tenant-level Activity Types remain available unless the project explicitly restricts the catalog. `[ASSUMPTION: project-level catalog restrictions are opt-in; the default is tenant plus project types.]`
- Reports can group by tenant-level and project-level Activity Types without merging IDs by display label.

### 5.4 Actor-Neutral Contributors, External Parties, and AI Agents

**Description:** Timesheets treats every Contributor as a Party. Human, external-party, and AI-agent entries use the same Time Entry model, with additional metrics where the contributor type needs them. Realizes UJ-3, UJ-4.

#### FR-12: Attribute every Time Entry to a Party

Every Time Entry has a Contributor Party ID and enough context to distinguish internal, external, and AI-agent contribution without storing personal data.

**Consequences (testable):**
- Timesheets validates Party IDs at write boundaries and stores references only.
- Party display names and personal data are hydrated at read time through Parties-owned contracts or adapters.
- Reports can filter by Contributor and contributor category without denormalizing Parties-owned data.

#### FR-13: Support external-party API submission

External contributors can submit or confirm Time Entries through an API surface without becoming full internal users.

**Consequences (testable):**
- External API submission requires tenant-scoped authorization and a valid Party reference.
- API-created entries enter the same Draft/Submitted/Approved workflow as internal entries.
- External API calls cannot bypass approval, correction, Target Reference validation, tenant isolation, or audit requirements.

#### FR-14: Support Magic-Link Confirmation

External contributors can confirm or adjust a specific Time Entry through Magic-Link Confirmation.

**Consequences (testable):**
- Magic links are single-use, scoped to one contribution confirmation, expire by policy, and require no full account login. `[ASSUMPTION: single-use scoped links are v1 security baseline, not a later hardening item.]`
- Expired, invalid, or already-used links reveal no tenant, Project, Work, Party, or Time Entry details.
- Confirmed Magic-Link Confirmation entries are attributed to the external Contributor Party and retain link-use audit metadata.

#### FR-15: Record AI Effort Metrics

AI-agent Contributors can attach AI Effort Metrics to Time Entries.

**Consequences (testable):**
- AI Effort Metrics support wall-clock execution time, model/tool runtime, billable effort, and token consumption as distinct fields.
- Token consumption is recorded as provider-reported input/output/total counts when available; missing provider metrics do not block Time Entry creation. `[ASSUMPTION: provider metric gaps are represented explicitly as unavailable/unknown, not zero.]`
- AI Effort Metrics are reportable beside human/external durations without converting tokens into hours by default.

### 5.5 Reporting, Approved-Time Ledger, and Exports

**Description:** Timesheets provides operational and finance-grade read models over recorded and approved effort. Reports must show actual time by Project, Work item, Contributor, Activity Type, Billable Flag, Approval State, and period. Realizes UJ-2, UJ-4, UJ-5.

#### FR-16: Query Time Entries by operational dimensions

Authorized users can query Time Entries by Contributor, Project Reference, Work Reference, period, Activity Type, Billable Flag, Approval State, and source type.

**Consequences (testable):**
- Queries enforce tenant isolation and result-level authorization.
- Queries can include or exclude Draft, Rejected, Corrected, Superseded, and Approved entries.
- Queries expose projection freshness so consumers can distinguish fresh, stale, rebuilding, or unavailable read models.

#### FR-17: Produce Project and Work actual-time reports

Timesheets reports actual time by Project and Work item, including planned-vs-actual comparison when Works provides planned/estimated effort.

**Consequences (testable):**
- Project reports roll up Time Entries by Project Reference and period.
- Work reports roll up Time Entries by Work Reference and can compare actual time with Works-provided planned/estimated effort.
- Reports do not require Timesheets to copy Project hierarchy or Work lifecycle state.

#### FR-18: Maintain the Approved-Time Ledger

Timesheets maintains a projection of approved time suitable for downstream finance, billing, and audit consumers.

**Consequences (testable):**
- The Approved-Time Ledger includes Time Entry ID, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comment.
- Superseded or corrected approved entries remain visible with lineage.
- The ledger can be rebuilt from events and does not become authoritative storage.

#### FR-19: Export approved billable evidence

Authorized finance/accounting consumers can export approved billable time.

**Consequences (testable):**
- Export filters include tenant, Project Reference, Work Reference, Contributor, Activity Type, period, and Billable Flag.
- Export output includes stable IDs and correction lineage sufficient for downstream reconciliation.
- Export does not calculate rates, invoice totals, taxes, payroll values, or revenue recognition.

#### FR-20: Surface AI-agent effort reporting

Reports show AI-agent effort separately from human/external effort while still allowing combined totals where meaningful.

**Consequences (testable):**
- AI reports include wall-clock time, model/tool runtime, billable effort, and token consumption.
- Combined reports can show human/external duration totals and AI duration/runtime/token metrics without implying that all units are interchangeable.
- Reports can filter by AI agent Party and Work Reference.

### 5.6 Module Boundary, Public Surface, and Platform Shape

**Description:** Timesheets is a Hexalith domain module with a contract-first surface. It persists through EventStore, references sibling modules by stable IDs, and exposes command/query surfaces for UI, API, automation, and downstream consumers. Realizes all journeys.

#### FR-21: Persist through Hexalith.EventStore

Timesheets persists all domain state changes through EventStore.

**Consequences (testable):**
- Time Entry, Timesheet Period, Activity Type, approval, rejection, and correction changes emit domain events.
- Aggregates and projections are replayable; no direct module-owned database writes are used for authoritative state.
- Domain rejections are represented as rejection events where consistent with Hexalith module patterns, not as mutable error rows.

#### FR-22: Expose command/query contracts for UI and integration

Timesheets exposes command and query contracts usable by internal UI, API clients, automation, and downstream finance consumers.

**Consequences (testable):**
- v1 ships REST/SDK command/query contracts plus FrontComposer-compatible metadata for admin/internal surfaces; it does not ship a bespoke mobile app. `[ASSUMPTION: FrontComposer-generated/admin UI is acceptable for v1 internal operations unless later UX work says otherwise.]`
- API contracts do not accept server-controlled tenant/user/authorization context from callers.
- Public contracts hide EventStore envelope mechanics, aggregate internals, and projection rebuild mechanics.

#### FR-23: Keep sibling module responsibilities out of Timesheets

Timesheets references Projects, Works, Parties, Tenants, and EventStore instead of duplicating them.

**Consequences (testable):**
- Timesheets stores Project IDs, Work IDs, Party IDs, tenant scope, and approval/correction facts, not copied Project/Work/Party content.
- Timesheets does not implement project planning, work-item lifecycle, identity/profile management, tenant membership, invoicing, payroll, or rate-card ownership.
- Architecture must include an owns-vs-references boundary decision record before implementation stories begin.

## 6. Non-Goals

- Timesheets is not a payroll system.
- Timesheets is not an invoicing, payment, rate-card, tax, or revenue-recognition system.
- Timesheets is not a desktop surveillance or automatic activity-tracking product.
- Timesheets is not a Project or Work lifecycle owner.
- Timesheets is not a Party profile store and must not persist Parties-owned personal data.
- Timesheets does not interpret natural-language time notes with AI in v1.
- Timesheets does not ship a full external-party portal in v1.
- Timesheets does not convert token consumption into billable hours by default.

## 7. MVP Scope

### 7.1 In Scope

- Time Entry recording against Project or Work references.
- Contributor model for internal users, external parties, and AI agents.
- Entry fields: date, duration, Target Reference, Activity Type, comment, Billable Flag, Contributor Party ID, Approval State, and optional AI Effort Metrics.
- Tenant-level and project-level Activity Type catalogs.
- Entry-level submit, approve, reject, lock, and additive correction flows.
- Weekly and monthly Timesheet Period submission and approval.
- Magic-link confirmation and API-only external-party submission/confirmation.
- Query/report read models by Contributor, Project, Work item, period, Activity Type, Billable Flag, Approval State, and contributor category.
- Approved-Time Ledger and approved billable-time export.
- AI-agent reporting for wall-clock execution time, model/tool runtime, billable effort, and token consumption.
- EventStore-backed persistence, replayable projections, tenant isolation, and Party-based actor references.

### 7.2 Out of Scope for v1

- Payroll processing.
- Invoice generation, payments, rates, taxes, and revenue recognition.
- Automatic desktop/app activity tracking.
- Calendar import, meeting inference, or automatic time suggestions.
- AI interpretation of natural-language time notes.
- Full external-party portal UX.
- Native mobile app.
- Replacing Project or Work ownership of their own state.

## 8. Integration and Dependencies

- **Hexalith.EventStore** — authoritative persistence path, aggregate/event mechanics, command status, replayable projections.
- **Hexalith.Tenants** — tenant lifecycle, membership, roles, access projections, and fail-closed tenant isolation.
- **Hexalith.Parties** — Contributor identity and display hydration. Timesheets stores Party IDs, not personal data.
- **Hexalith.Projects** — Project Reference validation, project-level Activity Type scope, project manager/approver context, and project reporting roll-up context.
- **Hexalith.Works** — Work Reference validation, planned/estimated effort for planned-vs-actual reporting, AI-agent/work execution context.
- **Hexalith.FrontComposer** — generated/admin UI metadata and internal operational surfaces where suitable.

## 9. Data Governance and Audit Requirements

- **Tenant isolation.** Every Time Entry, Timesheet Period, Activity Type, projection, export, and command/query path is tenant-scoped. Cross-tenant reads and writes must fail closed and be tested adversarially.
- **Append-only evidence.** Approval, rejection, correction, and export-relevant changes are events. Approved evidence is never silently overwritten.
- **Personal data minimization.** Durable Timesheets events store Party IDs and contribution evidence, not Party display names, contact details, profile fields, or personal-data objects.
- **Correction provenance.** Corrections preserve original values, corrected values, actor, timestamp, reason where required, and lineage to the affected entry.
- **Export accountability.** Approved-time exports are auditable actions with requester, filters, timestamp, and output scope recorded where required by policy.
- **Magic-link secrecy.** Invalid or expired magic links reveal no tenant, Project, Work, Party, or Time Entry details.
- **Retention policy.** **[RESOLVED 2026-06-19]** v1 default: Time Entry and approval/correction events are retained as indefinite audit evidence; export records and Magic-Link Confirmation audit metadata follow a documented tenant default. A **legal-hold override remains an explicit launch-readiness gate** requiring tenant/legal sign-off (owned by Story 1.4).

## 10. Cross-Cutting NFRs

- **Security and authorization.** All command, query, API, Magic-Link Confirmation, export, and admin paths enforce tenant and resource gates. JWT tenant claims are not sufficient by themselves; local tenant/resource authorization decides access.
- **Reliability.** Projections tolerate at-least-once delivery, duplicate events, replay, and rebuild. Reads expose stale/rebuilding/unavailable states instead of pretending data is fresh.
- **Performance.** Common command acknowledgements should complete within 500 ms p95 in a warmed local service, and common report queries should return within 2 seconds p95 for tenant/project/period filters. `[ASSUMPTION: these are v1 launch targets subject to architecture sizing.]`
- **Observability.** Logs and traces use structured metadata and correlation IDs without logging event payloads, comments, personal data, tokens, secrets, or full command bodies.
- **Accessibility.** Any internal UI surface follows the Hexalith/FrontComposer/Fluent UI rules and targets WCAG 2.2 AA where applicable.
- **Compatibility.** Event and contract evolution is additive and serialization-tolerant. Do not remove or rename previously emitted event fields.
- **Localization and time zones.** Dates, periods, and approval cutoffs must be tenant-policy aware. **[RESOLVED 2026-06-19]** Canonical v1 policy: **tenant time zone** — store UTC audit instants and tenant-local period keys; DST/period-boundary cases proven by golden-file tests (owned by Stories 2.7/4.6).

## 11. Risks and Mitigations

- **Risk: approval semantics become ambiguous between entry and period levels.** Mitigation: keep separate Approval State and period state, with explicit reconciliation rules in FR-7 and FR-8.
- **Risk: Timesheets drifts into invoicing/payroll.** Mitigation: enforce non-goals and keep exports evidence-only.
- **Risk: AI metrics get collapsed into human hours.** Mitigation: model AI Effort Metrics separately and avoid default token-to-hours conversion.
- **Risk: external-party access weakens tenant security.** Mitigation: single-use scoped magic links, no detail leakage on invalid links, same approval and audit path as internal entries.
- **Risk: sibling module data is copied for convenience.** Mitigation: architecture boundary decision record and tests for no persisted Party/Project/Work denormalized content beyond stable references.

## 12. Success Metrics

### Primary

- **SM-1 — Trusted capture coverage.** Internal, external-party, and AI-agent Contributors can create Time Entries through the same domain model. Target: all three contributor paths covered by automated tests and demo data. Validates FR-1, FR-12, FR-13, FR-14, FR-15.
- **SM-2 — Approval evidence completeness.** Every approved Time Entry has approver, timestamp, Approval State lineage, Target Reference, Contributor, Billable Flag, and correction lineage where applicable. Target: 100% of approved ledger records satisfy evidence schema. Validates FR-3, FR-5, FR-6, FR-18.
- **SM-3 — Period approval readiness.** Weekly and monthly Timesheet Periods can be submitted, approved/rejected, and reconciled with entry states. Target: end-to-end tests for clean, rejected, corrected, and mixed-state periods. Validates FR-7, FR-8, FR-9.
- **SM-4 — Operational reporting usefulness.** Project and Work reports show actual time by Contributor, period, Activity Type, Billable Flag, and Approval State. Target: seeded report scenarios answer the brief's questions: "where did the time go?", "what can we bill?", and "what did this work cost in effort?" Validates FR-16, FR-17, FR-20.
- **SM-5 — Billing evidence export.** Approved billable-time export includes stable IDs, filters, approval metadata, and correction lineage without rates or invoice totals. Target: finance export contract test passes. Validates FR-18, FR-19.

### Secondary

- **SM-6 — Boundary integrity.** Timesheets implementation persists no copied Party personal data and no copied Project/Work state. Target: architecture fitness tests and contract review confirm reference-only persistence. Validates FR-2, FR-12, FR-21, FR-23.
- **SM-7 — Projection resilience.** Approved-Time Ledger and operational reports rebuild correctly from events. Target: replay/rebuild tests produce identical read models for representative scenarios. Validates FR-16, FR-18, FR-21.

### Counter-Metrics

- **SM-C1 — Do not maximize captured hours.** More recorded time is not automatically better; optimizing for gross logged hours can incentivize noisy or inflated entries. Counterbalances SM-1.
- **SM-C2 — Do not optimize approval speed alone.** Fast approvals that increase corrections or disputes are worse than slower trusted approvals. Counterbalances SM-2 and SM-3.
- **SM-C3 — Do not broaden Timesheets into finance ownership.** Adding rates, invoices, payroll, or revenue recognition to hit export use cases is scope creep. Counterbalances SM-5.

## 13. Rollout and Launch Readiness

- **Phase 1 — Internal module foundation.** Implement Time Entry, Activity Type, submission/approval/correction, EventStore persistence, and basic reports for internal Contributors.
- **Phase 2 — External-party surface.** Add API-only external submission and Magic-Link Confirmation with security/audit gates.
- **Phase 3 — AI-agent evidence.** Add AI Effort Metrics capture and reporting tied to Works execution.
- **Phase 4 — Finance evidence.** Harden Approved-Time Ledger export and reconciliation flows for downstream billing/accounting consumers.
- **Phase 5 — Release-readiness verification.** Aggregate evidence, waivers, documentation sync, and launch decision records after feature behavior has already been implemented in the phase that introduced it. Phase 5 must not be the first implementation location for Magic-Link state loading, Work-reference validation, planned-effort reporting, export preview behavior, or performance evidence required by earlier phases.

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, a documented owns-vs-references boundary decision, concrete launch-scope integration adapters implemented in their owning feature phases, and performance evidence for the stated command/report targets. Story-complete evidence is not the same as launch-complete evidence: unresolved host wiring, runtime fixtures, UI, deployment, stakeholder acceptance, legal-hold sign-off, or formally accepted waivers must remain visible in the launch-readiness record.

## 14. Open Questions

1. **Time-zone and period policy** — **[RESOLVED 2026-06-19]** v1 uses **tenant time zone** as the canonical policy (UTC audit instants + tenant-local period keys). Owned by Stories 2.7/4.6.
2. **Approver precedence** — **[RESOLVED 2026-06-19]** v1 uses explicit authority-provider precedence. Same-precedence contradictions fail closed as ambiguous, self-approval is denied by default for entry and period approval unless explicitly allowed, and unavailable higher-precedence provider fall-through remains a policy follow-up for real provider/export stories.
3. **Correction model detail** — **[RESOLVED 2026-06-19]** v1 uses additive correction events with superseding correction lineage for rejected-entry and approved-entry correction. Offset-entry correction remains deferred unless finance/export decisions require it at launch.
4. **Magic-Link Confirmation issuance** — Story 3.2 owns issuance authority. **[RESOLVED 2026-06-19]** v1 baseline = single-use scoped expiring links (FR-14); **secondary identity verification for high-value/billable entries is deferred to post-v1** (explicit assumption, not silently closed).
5. **Retention and legal hold** — **[RESOLVED 2026-06-19]** v1 default retention per §9; **legal-hold override is a launch-readiness gate** pending tenant/legal sign-off (owned by Story 1.4).
6. **Activity Type governance** — Who can define project-level Activity Types, and can a project restrict tenant-level defaults?
7. **Export format** — Is CSV sufficient for v1, or does downstream billing need a structured API/webhook contract at launch?
8. **Comment sensitivity** — Are comments allowed to contain customer/private data, and do they need classification/redaction rules?

## 15. Assumptions Index

- §4 — Approval State vocabulary is Draft, Submitted, Approved, Rejected, Corrected, Superseded; architecture may map to exact event names.
- §5.1 FR-1 — Human and external durations are recorded in whole minutes; finer-grained AI runtime belongs in AI Effort Metrics.
- §5.1 FR-2 — Project/Work reference validation fails closed for submitted/approved writes; Draft creation may be allowed with stale-warning state.
- §5.2 FR-5 — Self-approval is denied by default.
- §5.2 FR-7 — A Timesheet Period is scoped to one Contributor, one Tenant, and one weekly or monthly period.
- §5.2 FR-8 — Period approval is not an irreversible freeze; corrections remain possible with audit evidence.
- §5.2 FR-9 — Exact approver role names remain provider/policy decisions; v1 precedence behavior is resolved in §14.
- §5.3 FR-11 — Project-level catalog restrictions are opt-in; default is tenant plus project Activity Types.
- §5.4 FR-14 — Single-use scoped magic links are v1 security baseline.
- §5.4 FR-15 — Provider metric gaps are represented as unavailable/unknown, not zero.
- §5.6 FR-22 — FrontComposer-generated/admin UI is acceptable for v1 internal operations unless later UX work says otherwise.
- §10 — Performance targets are v1 launch targets subject to architecture sizing.
