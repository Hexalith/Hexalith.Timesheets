---
baseline_commit: f17c2bbc6285b245bd71dc59843acfe4aa43c414
---

# Story 4.3: Produce Project and Work Actual-Time Reports

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a project or work reviewer,
I want actual-time reports by Project and Work item,
so that I can compare effort against operational expectations without Timesheets owning Project or Work state.

## Acceptance Criteria

1. Given approved or selected Time Entries exist for Project References, when a Project actual-time report is queried, then Timesheets rolls up time by Project Reference, period, Contributor, Activity Type, Billable Flag, Approval State, and contributor category, and it does not require copied Project hierarchy or lifecycle state.
2. Given Time Entries exist for Work References and Works provides planned or estimated effort, when a Work actual-time report is queried, then Timesheets can compare actual time with Works-provided planned/estimated effort, and the report identifies Works as the source of planned/estimated values.
3. Given Works or Projects display hydration is stale, unavailable, or unauthorized, when reports are rendered, then the report shows reference-state and freshness information, and it does not guess or persist copied display state.
4. Given report projections receive duplicate or replayed events, when projections rebuild, then rollups remain idempotent and deterministic, and freshness states indicate rebuilding or unavailable data where appropriate.
5. Given approved ledger data is available, when actual-time reports need approved evidence, then reports consume rebuildable read models or approved-ledger projections, and they do not re-create approval authority from raw entry state.
6. Given report users interact with filter-heavy surfaces, when they change filters or switch related report views, then FrontComposer/Fluent UI V5 components provide FilterBar, FluentDataGrid, status badges, related tabs where appropriate, and accessible keyboard/focus behavior.
7. Given common report filters are run against realistic tenant/project/period data, when performance tests execute, then common report queries target 2 seconds p95 according to launch sizing assumptions, and deviations are visible as quality evidence rather than hidden implementation details.
8. Given report contract tests run, when seeded Project and Work report scenarios are compared, then stable ordering, period boundary behavior, redaction rules, and selected report fields are verified by deterministic fixtures or golden files.

## Tasks / Subtasks

- [x] Add actual-time report query contracts and read models (AC: 1, 2, 3, 5, 8)
  - [x] Add Project and Work report query contracts under `src/Hexalith.Timesheets.Contracts/Queries/Reporting`, for example `QueryProjectActualTimeReport` and `QueryWorkActualTimeReport`.
  - [x] Do not accept caller-supplied `TenantId`, `UserId`, claims, roles, correlation authority, server-side freshness overrides, message IDs, sibling display labels, or planned-effort values in query bodies.
  - [x] Model filters explicitly: Project Reference, Work Reference, Contributor Party, Activity Type, tenant-local period key or date range, Billable Flag, Approval State, contributor category, current-only/include-superseded option, sort, page size, and opaque cursor.
  - [x] Add report read models under `src/Hexalith.Timesheets.Contracts/Models`, including summary rows for Project, Work, period, Contributor, Activity Type, Billable Flag, Approval State, contributor category, actual minutes, source row count, correction/superseded counts, reference-state metadata, display hydration, and `ProjectionFreshnessMetadata`.
  - [x] Add a Work planned-effort shape that carries Works-sourced estimated/done/remaining/unit values, source module name, and source freshness/reference state. It must allow "not supplied", "unavailable", and "unauthorized" without fabricating planned values.
  - [x] Keep Project and Work reference values as stable IDs only. Display labels may appear only in hydration metadata after authorization succeeds and must never be persisted as Timesheets authority.
- [x] Implement rebuildable actual-time report projections (AC: 1, 4, 5, 8)
  - [x] Add projection code under `src/Hexalith.Timesheets.Projections/OperationalReports`, for example `ActualTimeReportProjection.cs`.
  - [x] Reuse `ApprovedTimeLedgerProjection` and/or `TimeEntryEvidenceProjection` folding semantics for approved evidence instead of re-implementing approval, correction, lock, or lineage rules.
  - [x] Support Project and Work rollups by stable target reference, period/date bucket, Contributor, Activity Type, Billable Flag, Approval State, and contributor category.
  - [x] Preserve correction lineage in report outputs: current values must be the default view, superseded evidence must be included only when query options request it, and rejected/unapproved entries must not become approved actuals.
  - [x] Preserve idempotence: dedupe by `MessageId`, process by `SequenceNumber`, tolerate duplicate/replayed events, and produce deterministic ordering and cursor paging across replay/rebuild.
  - [x] Map `TimesheetsProjectionCheckpoint` through `ProjectionFreshnessMetadataMapper`; do not introduce a second freshness mapper.
- [x] Add server-side report query orchestration with fail-closed authorization (AC: 1, 2, 3, 5)
  - [x] Add server services/readers under `src/Hexalith.Timesheets.Server/OperationalReports`, including fail-closed unavailable readers registered in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Authorize `TimesheetsOperation.ProjectionRead` before projection lookup. Do not use `TimesheetsOperation.Export`; export remains Stories 4.5 and 4.6.
  - [x] Re-authorize each disclosed report row through `ITimesheetsAccessGuard` with the row contributor and exact target. Project rows validate Project only; Work rows validate Work only.
  - [x] Match Story 4.1 and 4.2 non-disclosure policy: ordinary insufficient-role rows may be filtered when policy allows; cross-tenant, stale authority, ambiguous authority, unavailable sibling authority, invalid reference, and unconfigured policy must fail closed.
  - [x] Hydrate Project, Work, Party, and Activity Type display labels only after row authorization succeeds. Denied rows must not be hydrated and must not leak protected identifiers through errors, labels, telemetry, or metadata.
  - [x] Add a narrow Works planned-effort adapter abstraction, for example `IWorkPlannedEffortProvider`, with an unavailable fail-closed default. If the current Works `get-work-item` query is consumed, wrap it behind this adapter and keep Works infrastructure out of Timesheets contracts/projections.
  - [x] Surface planned-effort source state separately from actual-time projection freshness. Actual minutes may be fresh while Works planned effort is stale/unavailable; the response must represent that distinction.
- [x] Publish FrontComposer metadata for actual-time report surfaces (AC: 3, 6)
  - [x] Add descriptors to `TimesheetsMetadataCatalog`, for example `timesheets.projection.project-actual-time-report` and `timesheets.projection.work-actual-time-report`.
  - [x] Use `TimesheetsCompositionPattern.FrontComposerProjectionView`.
  - [x] Include FilterBar fields for Project, Work, Contributor, Activity Type, period/date range, Billable Flag, Approval State, contributor category, current-only/include-superseded, and projection freshness.
  - [x] Include grid/detail fields for target reference, period, Contributor, Activity Type, Billable Flag, Approval State, contributor category, actual minutes, source row count, correction/superseded counts, reference state, display hydration, projection freshness, and Works planned-effort values where relevant.
  - [x] Include text-bearing status badge vocabularies for approval state, billable state, contributor category, correction/report-row state, reference/hydration state, planned-effort availability, and projection freshness.
  - [x] Related tabs are allowed only for closely related report partitions, such as Project / Work or current / include-superseded views. Do not introduce a Timesheets-specific UI shell or raw EventStore browsing path.
- [x] Add focused tests and quality evidence (AC: 1-8)
  - [x] Contracts tests: report queries omit server-controlled authority fields; read models round-trip filters, planned-effort source states, reference-state metadata, contributor category, approval/billable/correction states, cursor paging, and degraded freshness.
  - [x] Projection tests: Project rows roll up actual minutes by required dimensions; Work rows roll up actual minutes and carry planned-effort placeholders/source states; duplicate/replayed events do not duplicate totals; correction/superseded include/exclude behavior works; deterministic sort and cursor paging are stable.
  - [x] Server tests: tenant denial prevents projection lookup; fail-closed default readers disclose nothing; unauthorized rows filter/fail closed by denial category; display hydration and Works planned-effort lookup occur only after authorization.
  - [x] Metadata tests: both report descriptors exist with required filters, fields, status vocabularies, and no EventStore stream, invoice, payroll, rate, tax, revenue-recognition, Project ownership, or Work lifecycle ownership language.
  - [x] Integration tests: seeded Project and Work report scenarios can be queried through services with freshness metadata, deterministic paging, per-row authorization, drill-in compatibility with `ReadTimeEntryEvidence`, and planned-vs-actual Works source attribution when a Works adapter fixture supplies effort.
  - [x] Performance evidence: add or update report performance evidence so common tenant/project/period filters target 2 seconds p95. If realistic persisted-state fixtures are unavailable, keep the test explicitly skipped and document the reason in `docs/performance-evidence.md` or the existing performance evidence lane.
  - [x] Privacy tests: reports, metadata, logs, and diagnostics do not expose raw EventStore envelopes, denied row labels, copied Party/Project/Work display data, event payload dumps, comments when policy excludes them, command bodies, tokens, secrets, rates, invoice totals, payroll values, taxes, or revenue-recognition data.
- [x] Verify affected build and test lanes (AC: 1-8)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: Contracts, Projections, Server, Integration, and Architecture test projects.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-3-produce-project-and-work-actual-time-reports` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md` sections for Epic 4 and Story 4.3, including the 2026-06-19 Works planned-effort dependency note.
- Loaded `_bmad-output/planning-artifacts/architecture.md` sections covering EventStore-first persistence, projection model, query patterns, reporting/export boundaries, project structure, FrontComposer/Fluent UI rules, and the "Reference-validation adapter maturity" note.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR16, FR17, FR18, FR20, NFR9, NFR11, SM-4, SM-6, and SM-7.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Operational Reports, projection freshness, FilterBar, FluentDataGrid, StatusBadge, and factual non-ownership copy.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files. No Timesheets-local `project-context.md` file was present.
- `Hexalith.Works` has no `_bmad-output/project-context.md` in this checkout, so Works context was verified from `Hexalith.Works/AGENTS.md`, `WorkItemEffort`, `WorkItemView`, `GetWorkItemQueryHandler`, and its integration tests without modifying the submodule.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/4-2-project-approved-time-ledger-from-domain-events.md`.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md`.
- `{ux_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md`.
- Persistent facts loaded from 6 sibling `project-context.md` files.

### Epic And Story Context

- Epic 4 covers operational time queries, Project/Work actual-time reporting, AI effort reporting, Approved-Time Ledger, finance export, export audit, and dashboard composition.
- Story 4.3 implements FR16 and FR17. It should build actual-time reporting over existing read-model/projection foundations, not create a new authoritative reporting store.
- Story 4.3 must not implement Story 4.4 AI effort reporting beyond preserving contributor category and not corrupting AI source data. It must not implement Story 4.5 finance export generation, Story 4.6 export audit trail, or Story 4.7 dashboard overview.
- Project reports roll up actual time by Project Reference and period. They must not copy Project hierarchy, lifecycle state, or owned display data into Timesheets.
- Work reports roll up actual time by Work Reference and can compare actual minutes with Works-provided planned/estimated effort. Planned values must be clearly sourced from Works and omitted/marked unavailable when Works cannot provide them.
- Reports should answer the PRD SM-4 questions: where did the time go, what can we bill, and what did this work cost in effort.

### Current Code State To Extend

- `ApprovedTimeLedgerReadModel` currently contains `Items`, `NextCursor`, `ProjectionFreshness`, `CanUseForExport`, and `ExportReadinessDetail`.
- `ApprovedTimeLedgerRowReadModel` already carries stable Time Entry ID, Contributor, Target, Service Date, Duration Minutes, Activity Type ID/scope, Billable State, Approval Decision, Lock Evidence, Row State, Projection Freshness, Approved Correction, Correction, Event Lineage, Display Hydration, comment policy state, and policy-gated Comment.
- `ApprovedTimeLedgerProjection` builds ledger rows from `TimeEntryEvidenceProjection`, dedupes candidate IDs by `MessageId`, orders by `SequenceNumber`, supports filters for contributor/project/work/period/date/activity/billable/current-vs-superseded, uses deterministic sorting/paging, and maps checkpoint freshness through `ProjectionFreshnessMetadataMapper`.
- `ApprovedTimeLedgerQueryService` demonstrates the server-side authorization pattern to reuse: tenant-first `ProjectionRead`, projection lookup, per-row Project/Work/contributor authorization, fail-closed denial handling, post-authorization display hydration, and coherent export-readiness detail.
- `UnavailableApprovedTimeLedgerProjectionReader` is the fail-closed default reader pattern. Actual-time report readers should follow the same null/no-disclosure behavior by default.
- `TimesheetsMetadataCatalog` already contains `timesheets.projection.time-entry-query` and `timesheets.projection.approved-time-ledger`; actual-time reports should add distinct descriptors instead of overloading either existing surface.
- `ServiceCollectionExtensions` currently registers `ApprovedTimeLedgerQueryService`, fail-closed projection readers, authorization, reference validators, and display hydration defaults. Add report services/readers here without replacing existing defaults.
- `TimeEntryEvidenceProjection` and `TimeEntryEvidenceListProjection` remain useful for operational selected-entry reporting. Use them for selected/non-approved views where the story requires selected Time Entries, but use ledger rows for approved evidence semantics.

### Architecture Constraints

- EventStore is the only authoritative persistence boundary. Do not add SQL, Redis, Dapr state-store writes, local files, mutable caches, direct projection mutation, or broker-backed CRUD as report authority.
- Projections are rebuildable, idempotent, replay-safe, duplicate-tolerant, and non-authoritative for writes.
- Query APIs return typed DTOs/read models and `ProjectionFreshnessMetadata`, not raw EventStore envelopes, projection internals, aggregate state, or debug stream views.
- Tenant/resource gates run before aggregate load, command dispatch, projection read, export, or disclosure. JWT tenant/user claims are request evidence, not authority.
- Timesheets stores stable Tenant, Party, Project, and Work references only. Do not copy Party personal data, Project hierarchy/state, Work lifecycle/planning state, or tenant membership data into report rows.
- Dates and periods follow the v1 policy: UTC audit instants plus tenant-local dates/period keys where business rules require them.
- Logs/traces must use structured metadata and correlation IDs without event payloads, comments, personal data, tokens, secrets, raw claims, protected identifiers, command bodies, or report row payload dumps.
- Public contract evolution must be additive and serialization-tolerant. Do not rename existing Time Entry events, evidence read models, ledger contracts, or query contracts.
- Works planned-effort access is a dependency boundary. Current checkout exposes `WorkItemView` through a `get-work-item` query handler with estimated/done/remaining/unit values, but Timesheets must consume it through a narrow adapter and fail closed when it is unavailable, unauthorized, stale, or not configured.

### UX And UI Constraints

- Operational Reports use `FrontComposerProjectionView` and dense `FluentDataGrid` semantics through metadata, not a hand-built UI package in this story.
- Filters belong above the grid and must be preserved during drill-in/back navigation by the generated/FrontComposer surface.
- Row click should open Time Entry Evidence/Detail, not EventStore streams or projection internals.
- Use text-bearing status badge vocabularies for Approval State, Billable Flag, contributor category, correction/report row state, reference/hydration state, planned-effort availability, and projection freshness.
- Show stale/rebuilding/degraded/unavailable projection state through persistent message-bar style metadata; stale data must never be presented as fresh report authority.
- UI copy must remain factual and must not imply Timesheets owns Party, Project, Work, invoice, payroll, rate, tax, or revenue decisions.
- Related tabs may be used only for close report partitions such as Project / Work or Human / External / AI views; do not use tabs for unrelated navigation.

### Previous Story Intelligence

- Story 4.2 implemented dedicated Approved-Time Ledger contracts, projection, query service, FrontComposer metadata, policy-gated comments, correction lineage, deterministic paging, and fail-closed authorization.
- Story 4.2 review found and fixed export-readiness detail coherence after row filtering. For report readiness/freshness messaging, compute explanatory details from the final disclosed rows, not only from the underlying projection page.
- Story 4.2 consolidated freshness mapping into `ProjectionFreshnessMetadataMapper`; reuse it.
- Story 4.2 accepted a simple full-stream fold pattern for projections. If Story 4.3 adds heavier grouping, preserve correctness first and record any performance limitation in quality evidence rather than hiding it.
- Story 4.1 established operational query contracts, degraded projection freshness, result-level authorization, and fail-closed list readers. Use the same non-disclosure posture for actual-time reports.
- Build/test lanes after Stories 4.1 and 4.2 used direct xUnit executable fallback where VSTest socket permissions were blocked.

### Git Intelligence Summary

- Last 5 commit titles:
  - `f17c2bb feat(story-4.2): Project Approved Time Ledger from Domain Events`
  - `da204ec feat(story-4.1): Query Time Entries from Rebuildable Read Models`
  - `735f7e1 docs(retro): Capture Epic 3 Retrospective`
  - `21890e2 feat(story-3.5): Reject Invalid Confirmation Links Without Resource Disclosure`
  - `6349709 feat(story-3.4): Adjust Time Through Magic Link`
- Story 4.2 changed contracts, projection, server query services, metadata catalog, service registration, integration tests, and sprint artifacts. Story 4.3 should follow the same pattern with report-specific names and fail-closed defaults.
- Story 3.5 and later magic-link work established strict no-disclosure as a project norm. Reports must not disclose unauthorized Project/Work/Party/Time Entry details through missing-reference states.
- Current working tree before creating this story already had unrelated submodule pointer drift for `Hexalith.FrontComposer` and `Hexalith.Tenants`, plus `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, file-scoped namespaces, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a Timesheets UI project, or a parallel shell for this story.
- Use `ConfigureAwait(false)` on every awaited production call.
- No external latest-version research is needed for this story because no new framework or library choice is required; the architecture and repo pin the implementation stack.

### Project Structure Notes

- Expected contract additions:
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/WorkPlannedEffortReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
  - `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs` only if a new report/reference/planned-effort state enum is required.
- Expected projection additions:
  - `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`
  - Optional focused helper for grouping/period buckets if it keeps Project and Work rollups consistent.
- Expected server additions:
  - `src/Hexalith.Timesheets.Server/OperationalReports/IActualTimeReportProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/UnavailableActualTimeReportProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryResult.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/IWorkPlannedEffortProvider.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/UnavailableWorkPlannedEffortProvider.cs`
  - `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- Expected tests:
  - `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs`
  - `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs`
  - `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` if metadata endpoint output changes.
  - `tests/Hexalith.Timesheets.ArchitectureTests` if privacy/contract boundary rules need to include the new report models.
- Do not use `_bmad-output/`, `docs/`, or submodules as implementation scratch space.

### Testing Standards

- Every claimed authorization, freshness, reference-state, planned-effort, rollup, and lineage behavior needs executable proof.
- Test tenant-first denial before projection lookup and per-row denial after projection lookup.
- Test Project-target and Work-target rows independently; the Project validator must not run for Work rows, and the Work validator must not run for Project rows.
- Test stale, rebuilding, degraded, and unavailable freshness states in projection/read models and service outputs.
- Test Works planned-effort states separately from actual report freshness: supplied, not supplied, unavailable, unauthorized, and stale if modeled.
- Test corrections and superseded rows with include/exclude behavior and deterministic totals.
- Test duplicate events and replay from sequence zero produce identical rollups and no duplicate counts/minutes.
- Test stable ordering and cursor paging with deterministic IDs/dates.
- Test period boundary behavior for tenant-local period key and explicit date range filters.
- Test denied rows do not get display hydration, planned-effort lookup, or leaked Party/Project/Work/Time Entry details through labels, errors, telemetry, metadata, or quality evidence.
- Test metadata vocabulary contains text-bearing statuses and does not include ownership/scope-creep language.

### Anti-Patterns To Prevent

- Do not treat operational reports as authoritative write-side state.
- Do not re-create approval authority from raw Time Entry state when approved-ledger evidence exists.
- Do not copy Project hierarchy, Project lifecycle state, Work lifecycle state, Work planning state, Party personal data, or tenant membership into Timesheets report rows.
- Do not accept planned/estimated effort, display labels, tenant/user authority, or freshness override values from query bodies.
- Do not bypass `ITimesheetsAccessGuard` because a row came from a tenant-scoped projection.
- Do not hydrate display labels or query Works planned effort for rows that will be filtered or denied.
- Do not collapse human/external duration, AI runtime, token metrics, and Works effort units into a single untyped "hours" field.
- Do not include rates, invoice totals, taxes, payroll values, revenue-recognition language, or finance export output in report contracts or metadata.
- Do not implement CSV/API/webhook export, export audit records, or export command handling in this story.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-3-Produce-Project-and-Work-Actual-Time-Reports`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-16-Query-Time-Entries-by-operational-dimensions`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-17-Produce-Project-and-Work-actual-time-reports`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#12-Success-Metrics`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Reporting-Approved-Time-Ledger-And-Exports`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Reference-validation-adapter-maturity-added-2026-06-19`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/4-2-project-approved-time-ledger-from-domain-events.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/IApprovedTimeLedgerProjectionReader.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/UnavailableApprovedTimeLedgerProjectionReader.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ProjectionFreshnessMetadataMapper.cs`]
- [Source: `Hexalith.Works/src/Hexalith.Works.Contracts/ValueObjects/WorkItemEffort.cs`]
- [Source: `Hexalith.Works/src/Hexalith.Works.Contracts/Models/WorkItemView.cs`]
- [Source: `Hexalith.Works/src/Hexalith.Works/Queries/GetWorkItemQueryHandler.cs`]
- [Source: `README.md#Build-and-Test`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19: `dotnet test` for Contracts, Projections, Server, Integration, and Architecture projects was blocked by local VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`). Used the README direct xUnit v3 executable fallback.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- 2026-06-19: Direct xUnit v3 fallback passed: Contracts 77/77, Projections 70/70, Server 355/355, Integration 43 passed / 2 skipped, Architecture 20/20.
- 2026-06-19 review: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors after review fixes.
- 2026-06-19 review: `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-build` was blocked by local VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`), so the README direct xUnit v3 fallback was used.
- 2026-06-19 review: Direct xUnit v3 fallback passed: Contracts 78/78, Projections 72/72, Server 357/357, Integration 45 passed / 2 skipped, Architecture 20/20.

### Completion Notes List

- Added Project and Work actual-time report query contracts and read models with explicit filters, stable target references, projection freshness, reference-state metadata, display hydration metadata, and Works planned-effort source attribution.
- Implemented `ActualTimeReportProjection` over existing evidence and approved-ledger projections so approved actuals reuse ledger semantics, selected/non-approved rows remain separate, replayed events stay deduplicated, and cursor paging is deterministic.
- Added fail-closed report query orchestration with tenant-first `ProjectionRead`, per-row Project/Work/contributor authorization, post-authorization hydration, and a narrow unavailable-by-default Works planned-effort adapter.
- Published FrontComposer projection-view descriptors for Project and Work actual-time reports and updated performance evidence for the 2s p95 report-query target.
- Added focused contract, projection, server, integration, and architecture-backed evidence for authorization, metadata privacy, freshness, planned-effort source state, correction/superseded handling, and deterministic ordering.

### File List

- `_bmad-output/implementation-artifacts/4-3-produce-project-and-work-actual-time-reports.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/performance-evidence.md`
- `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReferenceStateMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/WorkPlannedEffortReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryResult.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/IActualTimeReportProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/IWorkPlannedEffortProvider.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/UnavailableActualTimeReportProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/OperationalReports/UnavailableWorkPlannedEffortProvider.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeLedgerAuthorizationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`

### Senior Developer Review (AI)

Reviewer: Jerome on 2026-06-19

Outcome: Approved after automatic fixes.

Findings fixed:

- HIGH: Selected/non-approved actual-time report rows lost `ActivityTypeScope` by converting evidence rows to `ActivityTypeScope.Unknown`, which could merge tenant-scoped and project-scoped activity types with the same ID. Fixed by carrying `ActivityTypeScope` through `TimeEntryQueryRowReadModel` and using it in `ActualTimeReportProjection`.
- HIGH: Work report metadata exposed Works planned-effort source freshness but not Works source reference state, leaving stale/unavailable/unauthorized source-state visibility incomplete. Fixed by adding `plannedSourceReferenceState` metadata and contract coverage.
- MEDIUM: Git showed changed implementation test files that were not listed in the story File List. Fixed by adding the missing changed files.

Review notes:

- Acceptance Criteria 1-8 were re-checked against the implementation and focused tests.
- No MCP or web documentation search was needed for this review because the story used the existing pinned .NET/xUnit/FrontComposer stack and introduced no new library or external API selection.
- Story status is set to `done` because no critical issues remain after automatic fixes.

### Change Log

- 2026-06-19: Implemented Story 4.3 Project and Work actual-time reports and marked ready for review.
- 2026-06-19: Senior developer review fixed activity-scope grouping, Works source reference-state metadata, and File List discrepancies; marked story done.
