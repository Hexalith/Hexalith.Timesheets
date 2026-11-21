---
title: "Hexalith.Timesheets — Product Requirements Document"
status: final
created: 2026-06-18
updated: 2026-11-21
---

# PRD: Hexalith.Timesheets

## 0. Document Purpose

This PRD specifies v1 of **Hexalith.Timesheets**, the Hexalith module for recording, approving, correcting, and reporting time spent against **Project** and **Work** references. It builds on the product brief and its addendum. The glossary is in §4, the numbered functional requirements are in §5, and §16 records current acceptance. [Product brief](../../briefs/brief-timesheets-2026-06-18/brief.md) · [Brief addendum](../../briefs/brief-timesheets-2026-06-18/addendum.md)

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

- **UJ-1. Camille records her project time before submitting the week.** Camille is an internal consultant working across two client projects. From a Project or Work context, she records a Time Entry with its date, duration, Activity Type, comment, and Billable Flag. On Friday she submits her weekly Timesheet Period. If an included Activity Type has been deactivated, the whole period stays unsubmitted. Camille sees which entry to correct, keeps her valid selections, and retries. The submitted period lists the entries she reviewed.

- **UJ-2. Nadia approves the week and rejects one entry.** Nadia is a project manager reviewing submitted time for a client project. She filters pending Time Entries by Project, opens Camille's weekly Timesheet Period, approves valid entries, and rejects one vague billable entry with a reason. The value lands when approved entries are locked into the Approved-Time Ledger and the rejected entry returns to Camille for correction. **Edge case:** if Camille corrects the rejected entry after period submission, the period shows a pending correction instead of silently changing the approved record.

- **UJ-3. Simon confirms supplier time through Magic-Link Confirmation.** Simon receives a scoped link for a Work item. He reviews the proposed date, duration, recorded tenant Activity Type, Billable Flag, and minimal target context. He sees a comment only when external-confirmation policy permits it. He can confirm the recorded type even if it was later deactivated. If he adjusts the entry, the selected type must be eligible now. The tenant receives Party-attributed evidence for approval. An expired or used link reveals no entry details.

- **UJ-4. Ada the AI agent records execution evidence.** Ada is an AI agent assigned through Hexalith.Works. After completing a Work item step, Ada records wall-clock execution time, model/tool runtime, billable effort, and token consumption against the Work reference. The value lands when the Work item report shows AI effort beside human effort without treating tokens as human hours. **Edge case:** if token metrics are unavailable from the provider, the entry records the missing metrics explicitly rather than fabricating values.

- **UJ-5. Marc exports approved billable evidence.** Marc filters the Approved-Time Ledger by tenant, Project, period, and Activity Type. Before requesting the CSV, he checks the preview for readiness, row count, and the comment policy for his selected scope. After Timesheets verifies the complete ledger snapshot and its matching audit receipt, Marc can download the file and reconcile entry IDs, Contributor Party references, approvals, and corrections in downstream billing. If verification is uncertain, the download stays unavailable and Marc can retry the same request.

## 4. Glossary

- **Time Entry** — the core fact recorded by Timesheets: date, duration, Target Reference, Activity Type, comment, Billable Flag, Contributor, Approval State, and optional AI Effort Metrics.
- **Contributor** — the Party that performed or confirmed the time. A Contributor may be an internal user, an external party, or an AI agent.
- **Party** — the stable identity from `Hexalith.Parties`, referenced by Party ID. Timesheets does not store Party personal data.
- **Target Reference** — the object the Time Entry is recorded against: either a Project Reference or a Work Reference.
- **Project Reference** — a stable ID from `Hexalith.Projects`; Timesheets references it but does not own project structure.
- **Work Reference** — a stable ID from `Hexalith.Works`; Timesheets references it but does not own work lifecycle.
- **Activity Type** — tenant- or project-scoped category describing the nature of recorded time.
- **Billable Flag** — the Timesheets-owned classification of whether a Time Entry is billable evidence. It is not a rate, invoice line, or revenue-recognition decision.
- **Approval State** — a Time Entry can be Draft, Submitted, Approved, or Rejected. A Timesheet Period has its own approval record with values Submitted, Approved, or Rejected.
- **Correction State** — None, Corrected, or Superseded. An entry can remain Approved as an additive correction supersedes its earlier values.
- **Lock State** — Unlocked, LockedFromDirectEdit, or SupersededLocked. This determines whether an entry accepts a direct edit.
- **Ledger row state** — Current or Superseded. The row state identifies which version of the approved evidence a reader is viewing. [Published state vocabulary](../../../../src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs)
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
- Human and external entries record a positive duration in whole minutes. AI Effort Metrics can also capture finer-grained runtime.
- A Time Entry must target exactly one Project Reference or one Work Reference.
- Comment requirements can be policy-driven; when required, missing comments block submission rather than creation.

#### FR-2: Validate Target References without owning them

Timesheets verifies the Project or Work reference before recording a Draft and again at submission and approval.

**Consequences (testable):**
- Project Reference validation uses a Projects-owned API/projection/adapter; Work Reference validation uses a Works-owned API/projection/adapter.
- Timesheets stores stable IDs and minimal correlation metadata only; it does not copy Project, Work, or Party display state.
- A protected write, including Draft capture, requires fresh evidence that the target is valid and authorized for this tenant. Missing, stale, or denied target authority blocks the write before an entry event is recorded. Tenant policy may permit showing a stale display label after the target check succeeds. [Draft capture criteria](../../../implementation-artifacts/1-7-record-draft-time-entry-against-project-or-work.md)

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
- When a contributor submits selected entries and policy permits partial submission, valid entries can proceed. Each blocked entry returns its ID and field-specific guidance. Submitting a whole Timesheet Period follows FR-7. [Selected-entry submission](../../../implementation-artifacts/2-1-submit-draft-time-entries-for-approval.md)
- A rejected entry can be corrected and resubmitted without losing rejection reason history.

#### FR-5: Approve or reject individual Time Entries

An Approver can approve a submitted Time Entry without giving a reason. Rejection requires a nonblank reason.

**Consequences (testable):**
- Approval changes the entry to Approved and makes it eligible for the Approved-Time Ledger.
- Rejection changes the entry to Rejected and records the approver, timestamp, and reason. A missing or blank rejection reason leaves the entry Submitted and emits no rejection event. [Entry approval and rejection](../../../implementation-artifacts/2-3-approve-or-reject-submitted-time-entries.md)
- An Approver cannot approve their own Time Entry unless policy explicitly allows it (§14.2).

#### FR-6: Lock approved entries against direct edits

Approved Time Entries are locked from direct mutation and can only be corrected additively.

**Consequences (testable):**
- Direct edit commands against Approved entries are rejected with a domain rejection.
- Additive corrections supersede the affected values and keep the original approval evidence visible.
- Reporting can include or exclude superseded entries according to query options.

#### FR-7: Submit and approve Timesheet Periods

A Contributor can submit a weekly or monthly Timesheet Period containing their Time Entries, and an Approver can approve or reject that period.

**Consequences (testable):**
- A Timesheet Period is scoped to one Contributor, one Tenant, and one weekly or monthly period.
- Period submission records the included Time Entry IDs and the period boundary.
- On whole-period submission, if any included entry fails validation, Timesheets emits neither a period-submission event nor an entry-submission event for that request. It returns the valid entry IDs and identifies the fields that need correction on each blocked entry so the contributor can retry the whole period. [Period submission criteria](../../../implementation-artifacts/2-7-submit-timesheet-periods.md)
- An interrupted whole-period operation stays pending until the period decision and each required entry transition can be verified. An authorized retry after restart completes outstanding effects once. [Recovery criteria](../../epics.md#story-610-recover-interrupted-period-submissions-and-reviews)
- Period approval does not erase entry-level states; it records grouped review evidence and locks approved included entries from direct edit.

#### FR-8: Reconcile entry-level and period-level approval

Timesheets must distinguish entry Approval State from period Approval State.

**Consequences (testable):**
- Period detail shows its own decision and each entry’s Approval State, Correction State, and Lock State. One period can contain both Approved and Rejected entries, with corrections visible against the affected entries.
- Period approval locks included approved entries against direct edits. Additive corrections retain the original approval evidence.
- A rejected entry inside a submitted period can be corrected and resubmitted without reconstructing the whole period from scratch.

#### FR-9: Resolve approver authority

Timesheets authorizes Approvers using tenant and project/work context.

**Consequences (testable):**
- Entry and period approval fail closed when approver authority cannot be resolved.
- The approval check can use Tenants membership/roles and Project/Work-specific approver projections.
- Tenant admins and Project or Work approvers act under server-resolved policy. Providers have explicit precedence, and contradictory decisions at the same precedence deny approval. The provider owns the role names (§14.2).
- If the highest-precedence applicable provider group is unavailable, approval is denied even when a lower-priority tenant or finance provider would allow it. [Authority policy and follow-up](../../../implementation-artifacts/2-2-enforce-timesheet-approval-authority-policy.md)

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
- The default catalog includes active tenant types and active types for the selected Project. An explicit Project restriction can narrow that selection. Changing it requires both tenant and Project authority (§14.6).
- Reports can group by tenant-level and project-level Activity Types without merging IDs by display label.
- Magic-link selection is limited to tenant-owned Activity Types under FR-14.

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
- Workload-authenticated confirmation verifies the tenant, Contributor, and target of the entry recorded through the API. Acknowledgement requires a matching committed confirmation. Retrying the same request, including after restart, leaves exactly one confirmation and preserves the Approval State. A different tenant or Contributor receives an opaque refusal and the entry keeps its prior state.
- External recording respects caller cancellation and a configured deadline for verifying the write. If verification times out after commit, retrying the same request identity yields one recording. Operators can distinguish an uncertain outcome from a policy refusal while public responses and telemetry protect sensitive entry and identity details.
- External API calls cannot bypass approval, correction, Target Reference validation, tenant isolation, or audit requirements.

#### FR-14: Support Magic-Link Confirmation

External contributors can confirm or adjust a specific Time Entry through Magic-Link Confirmation.

**Consequences (testable):**
- Magic links are single-use, apply to one contribution confirmation, expire by policy, and require no full account login (§14.4).
- Issuance, display, confirmation, and adjustment support tenant-owned Activity Types for both Project and Work targets. New issuance and adjustment require an active, selectable tenant type. A Project target applies its catalog restrictions. A Work target uses its own scope without inferring a Project.
- A valid link may display and confirm the entry’s recorded tenant type after deactivation or exclusion by its Project. The usual capability, scope, and authorization checks still apply. [Magic-link selection and historical confirmation](../../../implementation-artifacts/tests/five-magic-link-consistency-deferrals.md)
- Expired, invalid, or already-used links reveal no tenant, Project, Work, Party, or Time Entry details.
- Valid-link details include comment text only when explicit external-confirmation policy allows it with the required redaction; otherwise it is omitted, including when policy is missing.
- Confirmed Magic-Link Confirmation entries are attributed to the external Contributor Party and retain link-use audit metadata.

#### FR-15: Record AI Effort Metrics

AI-agent Contributors can attach AI Effort Metrics to Time Entries.

**Consequences (testable):**
- AI Effort Metrics support wall-clock execution time, model/tool runtime, billable effort, and token consumption as distinct fields.
- Record provider-reported input, output, and total token counts when available. If the provider omits a count, keep its availability status and leave the numeric value absent. A reported zero is a measured value. The Contributor can record the Time Entry even when counts are missing.
- AI Effort Metrics are reportable beside human/external durations without converting tokens into hours by default.

### 5.5 Reporting, Approved-Time Ledger, and Exports

**Description:** Timesheets provides operational and finance-grade read models over recorded and approved effort. Reports must show actual time by Project, Work item, Contributor, Activity Type, Billable Flag, Approval State, and period. Realizes UJ-2, UJ-4, UJ-5.

#### FR-16: Query Time Entries by operational dimensions

Authorized users can query Time Entries by Contributor, Project Reference, Work Reference, period, Activity Type, Billable Flag, Approval State, and source type.

**Consequences (testable):**
- Queries enforce tenant isolation and result-level authorization.
- Queries can filter by entry Approval State and independently include or exclude corrected or superseded evidence. An Approved entry still matches the approval filter after a correction.
- Queries expose projection freshness so consumers can distinguish fresh, stale, rebuilding, or unavailable read models.

#### FR-17: Produce Project and Work actual-time reports

Timesheets reports actual time by Project and Work item, including planned-vs-actual comparison when Works provides planned/estimated effort.

**Consequences (testable):**
- Project reports roll up Time Entries by Project Reference and period.
- Work reports roll up Time Entries by Work Reference. The default host reports planned effort as unavailable. An activated Works adapter can supply an estimate for comparison (§8).
- If the planned value is missing or cannot be trusted or disclosed, show authorized actuals with their own freshness and mark the plan unavailable, or deny the read when policy requires it. Calculate a variance only when Works supplies an authorized estimate with a compatible unit. [Planned-effort reporting criteria](../../../implementation-artifacts/4-8-implement-works-planned-effort-reporting-adapter.md)
- Reports do not require Timesheets to copy Project hierarchy or Work lifecycle state.

#### FR-18: Maintain the Approved-Time Ledger

Timesheets maintains a projection of approved time suitable for downstream finance, billing, and audit consumers.

**Consequences (testable):**
- An authorized ledger row includes Time Entry ID, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, and correction lineage. A direct ledger read includes comment text only when the row’s projection policy explicitly permits it. A denied or missing inclusion decision omits text from the row and its original and corrected values while preserving authorized identifiers and lineage. [Ledger disclosure criteria](../../../implementation-artifacts/4-2-project-approved-time-ledger-from-domain-events.md)
- Superseded or corrected approved entries remain visible with lineage.
- The ledger can be rebuilt from events and does not become authoritative storage.

#### FR-19: Export approved billable evidence

Authorized finance/accounting consumers can export approved billable time.

**Consequences (testable):**
- Export filters include tenant, Project Reference, Work Reference, Contributor, Activity Type, period, and Billable Flag.
- Export output includes stable IDs and correction lineage sufficient for downstream reconciliation.
- Preview and generation evaluate the same trusted tenant export policy and each row’s export permission. Both must allow a comment before it can appear in generated CSV or returned rows, including original and corrected values. Redaction preserves IDs and correction lineage. Missing required export policy blocks the action.
- The v1 preview service returns readiness, freshness, row count, and effective disclosure policy for the authorized scope. Its response contains no evidence rows or comment text. Preview creates neither a file nor an export audit event. [Preview contract](../../../../src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportPreviewReadModel.cs)
- If a caller requests a full export while browsing a later page, Timesheets starts at the first page of the authorized scope or explicitly rejects the request. Each matching row appears once in deterministic order at one ledger version, including requested superseded rows. If the service cannot verify completeness or a single version, it withholds both the CSV and returned rows.
- Before delivery, Timesheets verifies a durable receipt for the request identity, requester, filter scope, row count, format version, and hash of the CSV bytes. Rejection or uncertainty withholds both bytes and rows. Retrying after a lost acknowledgement, including after restart, yields one matching receipt. Reusing the identity for different scope or content cannot authorize delivery.
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
- v1 ships REST/SDK command/query contracts plus FrontComposer-compatible metadata for admin/internal surfaces. This repository does not ship or add a Timesheets UI project; rendered internal and external experiences are composed and verified by a consuming FrontComposer host using Fluent UI V5. It does not ship a bespoke mobile app.
- API contracts do not accept server-controlled tenant/user/authorization context from callers.
- Public contracts hide EventStore envelope mechanics, aggregate internals, and projection rebuild mechanics.
- A consumer that installs only the published Projections package can enumerate its supported projections using its declared dependencies. A composed host can discover every installed command and projection exactly once.

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
- Approved-Time Ledger and deterministic CSV v1 approved billable-time export.
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
- A dedicated HTTP endpoint for export preview. V1 exposes the side-effect-free preview through the service query described in FR-19.
- Structured API/webhook export and richer tenant comment classification/redaction schemes.
- Timesheets-owned rendering projects; consuming FrontComposer hosts own rendered UI/accessibility.
- Replacing Project or Work ownership of their own state.

## 8. Integration and Dependencies

- **Hexalith.EventStore** — authoritative persistence path, aggregate/event mechanics, command status, replayable projections.
- **Hexalith.Tenants** — tenant lifecycle, membership, roles, access projections, and fail-closed tenant isolation.
- **Hexalith.Parties** — Contributor identity and display hydration. Timesheets stores Party IDs, not personal data.
- **Hexalith.Projects** — Project Reference validation, project-level Activity Type scope, project manager/approver context, and project reporting roll-up context.
- **Hexalith.Works** owns Work Reference validation, planned effort, and AI-agent execution context. In the default host, Work capture is denied and planned effort is unavailable. Live activation is waived until Platform supplies a live Works query channel and enables the relevant validation or reporting adapter (Stories 1.10 and 4.8). A separate waiver covers detection of stale Works state until Works supplies sufficient freshness evidence. [Launch-scope classification](../../../../docs/launch-readiness.md#launch-scope-classification)
- **Hexalith.FrontComposer** — generated/admin UI metadata and internal operational surfaces where suitable.

## 9. Data Governance and Audit Requirements

- **Tenant isolation.** Every Time Entry, Timesheet Period, Activity Type, projection, export, and command/query path is tenant-scoped. Cross-tenant reads and writes must fail closed and be tested adversarially.
- **Append-only evidence.** Approval, rejection, correction, and export-relevant changes are events. Approved evidence is never silently overwritten.
- **Personal data minimization.** Durable Timesheets events store Party IDs and contribution evidence, not Party display names, contact details, profile fields, or personal-data objects.
- **Correction provenance.** Corrections preserve original values, corrected values, actor, timestamp, reason where required, and lineage to the affected entry.
- **Export accountability.** Every delivered approved-time CSV has the durable receipt required by FR-19. It records the request and generation times and identifies the delivered contents by hash. Comments and exported row data stay out of the receipt.
- **Magic-link secrecy.** Invalid or expired magic links reveal no tenant, Project, Work, Party, or Time Entry details.
- **Retention policy.** **[RESOLVED 2026-06-19]** v1 default: Time Entry and approval/correction events are retained as indefinite audit evidence; export records and Magic-Link Confirmation audit metadata follow a documented tenant default. A **legal-hold override remains an explicit launch-readiness gate** requiring tenant/legal sign-off (owned by Story 1.4).

## 10. Cross-Cutting NFRs

- **Security and authorization.** All command, query, API, Magic-Link Confirmation, export, and admin paths enforce tenant and resource gates. JWT tenant claims are not sufficient by themselves; local tenant/resource authorization decides access.
- **Reliability.** Projections tolerate at-least-once delivery, duplicate events, replay, and rebuild. Reads expose stale/rebuilding/unavailable states instead of pretending data is fresh.
- **Performance.** NFR10 targets 500 ms p95 for common command acknowledgements in a warmed local service. NFR11 targets 2 seconds p95 for common Project and Work reports filtered by tenant, Project, and period. Historical in-process measurements passed for the command pipeline and reads over seeded projections. The release owner tracks the waiver for the EventStore/Dapr persistence and transport path with the owners of Stories 1.11 and 4.10. Revisit it when representative persisted fixtures can measure end-to-end latency. [Measured scenarios and waiver](../../../../docs/performance-evidence.md)
- **Ledger performance qualification.** The reporting owner agrees on a representative tenant volume and budgets for elapsed time and memory allocation. At that volume, compare the existing and revised ledger reads under the same load and verify identical effective rows, superseded lineage, and CSV bytes. [Story 6.9](../../epics.md#story-69-reduce-repeated-ledger-folds-while-preserving-evidence)
- **Observability.** Logs and traces use structured metadata and correlation IDs without logging event payloads, comments, personal data, tokens, secrets, or full command bodies.
- **Accessibility.** Any rendered Timesheets surface in a consuming FrontComposer host follows Hexalith/FrontComposer/Fluent UI V5 rules and targets WCAG 2.2 AA (NFR13). Timesheets contracts and metadata express accessible semantics; component and browser conformance require consumer-host evidence.
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

- **SM-1 — Trusted capture coverage.** Automated tests and demo data must cover internal, external-party, and AI-agent capture through the same domain model (FR-1 and FR-12–FR-15). `[ASSUMPTION: starting from the existing Project or Work context lets the contributor record time without reconstructing the target.]` In product UAT, the product owner observes a contributor record and submit time from that context, checks the Target Reference on the submitted entry, and records any re-entry or other friction in the launch register.
- **SM-2 — Approval evidence completeness.** Every approved Time Entry has approver, timestamp, Approval State lineage, Target Reference, Contributor, Billable Flag, and correction lineage where applicable. Target: 100% of approved ledger records satisfy evidence schema. Validates FR-3, FR-5, FR-6, FR-18.
- **SM-3 — Period approval readiness.** Weekly and monthly Timesheet Periods can be submitted, approved/rejected, and reconciled with entry states. Target: end-to-end tests for clean, rejected, corrected, and mixed-state periods. Validates FR-7, FR-8, FR-9.
- **SM-4 — Operational reporting usefulness.** Project and Work reports show actual time by Contributor, period, Activity Type, Billable Flag, and Approval State. Target: seeded report scenarios answer the brief's questions: "where did the time go?", "what can we bill?", and "what did this work cost in effort?" Validates FR-16, FR-17, FR-20.
- **SM-5 — Billing evidence export.** Every delivered file reconciles to one complete authorized ledger version and one durable receipt. Finance acceptance covers comment disclosure in every representation, retry after a lost audit acknowledgement, and a correction between pages. A failed verification releases zero rows or bytes. Finance delivery requires passing evidence for every acceptance criterion in Stories 6.1–6.3 (FR-18 and FR-19).

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

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, a documented owns-vs-references boundary decision, concrete launch-scope integration adapters implemented in their owning feature phases, and performance evidence for the stated command/report targets. Story-complete evidence is not the same as launch-complete evidence: unresolved host wiring, runtime fixtures, consumer-host rendering/accessibility, deployment, stakeholder acceptance, legal-hold sign-off, or formally accepted waivers must remain visible in the launch-readiness record.

Story 6.7 requires a Project approver and contributor to be able to complete capture, submission, approval, additive correction, and ledger readback through supported release entry points. To qualify that workflow under Story 6.8, the release owner must repeat it with production-equivalent providers in an authorized tenant. After restart and again after rebuilding projections, the ledger must return the same row identities, effective values, and correction lineage. A caller from a second tenant must be denied access to read or change the first tenant’s entry. The release owner records the deployed evidence in the launch register. [Epic 6](../../epics.md#epic-6-qualify-project-time-and-finance-evidence) · [Launch register](../../../../docs/launch-readiness.md)

## 14. Open Questions

1. **Time-zone and period policy** — **[RESOLVED 2026-06-19]** v1 uses UTC audit instants and tenant-local period keys (Stories 2.7 and 4.6).
2. **Approver precedence** — **[RESOLVED 2026-06-19]** FR-9 defines the current rule for an unavailable deciding group or a contradiction within that group. Self-approval of an entry or period is denied unless policy explicitly allows it. The authority-policy owner must decide whether to allow lower-priority fallback when real providers or finance-review workflows are integrated. Any such change requires a separate policy decision.
3. **Correction model detail** — **[RESOLVED 2026-06-19]** v1 uses additive correction events with superseding correction lineage for rejected-entry and approved-entry correction. Offset-entry correction remains deferred unless finance/export decisions require it at launch.
4. **Magic-Link Confirmation issuance** — **[RESOLVED 2026-06-19]** Story 3.2 owns issuance authority for the single-use, scoped, expiring links defined in FR-14. Secondary identity verification for high-value or billable entries is deferred to post-v1.
5. **Retention and legal hold** — **[RESOLVED 2026-06-19]** v1 default retention per §9; **legal-hold override is a launch-readiness gate** pending tenant/legal sign-off (owned by Story 1.4).
6. **Activity Type governance (Q6)** — **[RESOLVED 2026-09-12]** Tenant Activity Types require tenant-authorized command access. Project Activity Types and catalog restrictions require both tenant and Project authority resolved through server-side gates; role names remain provider policy, not caller input. Projects may explicitly restrict selectable tenant/project IDs. The default is unrestricted active tenant types plus active types for the selected Project. Implemented by Stories 1.5/1.6.
7. **Export format (Q7)** — **[RESOLVED 2026-09-12]** Deterministic approved-time CSV v1 is the launch export. Stories 4.5/4.6 supplied the original CSV and reconciliation evidence. Stories 6.1–6.3 own the outstanding finance delivery criteria in FR-19. Structured API/webhook export is post-v1 and needs a named downstream consumer and contract story.
8. **Comment sensitivity (Q8)** — **[RESOLVED 2026-09-12]** Comments are sensitive unstructured evidence. Authorized internal display is policy-controlled; external confirmation display, diagnostics, and export are excluded by default. External disclosure requires explicit policy/redaction; missing comment policy fails trust-bearing actions closed. Richer tenant classification/redaction schemes are post-v1. Story 1.4 implements the baseline; legal-hold and tenant-policy configuration remain separate launch concerns.

9. **Carrying previously approved entries into another period** — Deferred to post-v1. The product owner will decide whether a later period may include previously approved entries when revisiting period approval policy. [Launch register](../../../../docs/launch-readiness.md#launch-scope-classification)

## 15. Assumptions Index

- **SM-1, in-context capture.** The product owner will test the assumption that a contributor can use the existing Project or Work context without reconstructing its target. The observation and acceptance evidence are due at product UAT (§12).

Section 14 records settled policy and deferred decisions. The reporting owner still needs to agree on the representative tenant volume and ledger performance budgets in §10. Section 16 records the outstanding release gates.

## 16. Decision History and Current Acceptance — 2026-11-20

**Release decision: FAIL.** The November 20 audit of v2.1.1 found gaps in finance delivery, workload-authenticated confirmation, and the production Project approval-to-ledger path. The nine audit follow-ups entered the backlog on November 20. Story 6.10 tracks the subsequent period recovery decision. The onboarding instructions were corrected. Finance delivery requires Stories 6.1–6.3, external API confirmation and recording reliability are tracked in 6.4–6.5, and production composition and deployed Project qualification are tracked separately in 6.7 and 6.8. [November 20 change proposal](../../sprint-change-proposal-2026-11-20.md) · [Sprint status](../../../implementation-artifacts/sprint-status.yaml)

The October 26 isolated magic-link journey demonstrated persistence and restart locally. Deployed authority, legacy inventory and continuity, shared rebuild, and ingress privacy remain open under Stories 3.6 and 3.7 (FR-14). Tenant policy configuration, legal-hold sign-off, and product UAT also await their owners. Stories 5.1 and 5.2 closed their scoped reconciliations. Package currency remains **CONCERNS**. The release owner maintains the dated evidence and waivers in the [launch register](../../../../docs/launch-readiness.md).

The September 12 decisions on catalog governance, CSV v1, comment sensitivity, and consumer-host UI ownership still apply. Richer comment classification and structured API/webhook export remain post-v1. [September 12 change proposal, §4.6](../../sprint-change-proposal-2026-09-12.md)
