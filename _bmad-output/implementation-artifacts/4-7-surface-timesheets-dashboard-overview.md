---
baseline_commit: d55750e
---

# Story 4.7: Surface Timesheets Dashboard Overview

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an internal Timesheets user,
I want a dashboard that summarizes my current period, pending actions, approval workload, and reporting shortcuts,
so that I can start common capture, review, and finance workflows from one operational surface.

## Acceptance Criteria

1. Given an internal user opens the Timesheets module, when the dashboard loads inside `FrontComposerShell`, then it shows the user's current period status, pending corrections or submissions, approval workload where authorized, and shortcuts to operational reports and the Approved-Time Ledger, and it does not introduce a parallel shell, marketing hero, decorative card-heavy landing page, or custom portal.
2. Given the dashboard depends on projections for counts, period state, approval workload, or ledger freshness, when any projection is stale, rebuilding, degraded, or unavailable, then the dashboard shows explicit freshness/status messaging, and it does not present stale data as fresh decision authority.
3. Given the dashboard composes status from existing projections, when it renders current period, approval workload, report shortcuts, or ledger shortcuts, then it remains read-only aggregation over existing read models, and it introduces no new approval logic, finance math, export calculations, or write authority.
4. Given a user lacks approver or finance authority, when the dashboard is rendered, then approval workload, ledger, and export actions are hidden or disabled according to policy, and no protected tenant, contributor, Project, Work, or Time Entry details are disclosed.
5. Given the dashboard has no current entries, approvals, or exportable ledger data, when empty states are shown, then the page provides the single most relevant action, such as `Record time`, and avoids misleading finance/export affordances.
6. Given users navigate from dashboard shortcuts to capture, period review, approvals, reports, or ledger, when they drill in and return, then filters, period context, and projection status are preserved where applicable, and navigation remains keyboard accessible.
7. Given dashboard UI is tested, when accessibility and conformance checks run, then FrontComposer/Fluent UI V5 components, status badges with text, message bars, focus order, and WCAG 2.2 AA behavior are verified, and hover-only controls or color-only statuses are not introduced.

## Tasks / Subtasks

- [x] Add dashboard contracts and metadata without creating new write authority (AC: 1, 3, 5, 6)
  - [x] Add a read-only dashboard query contract under `src/Hexalith.Timesheets.Contracts/Queries/Reporting/`, for example `QueryTimesheetsDashboardOverview`, carrying only filter/context inputs such as current tenant-local period key/date range and optional Project/Work context. Do not accept caller-supplied tenant, user, authorization, correlation, or server-controlled freshness values.
  - [x] Add dashboard read models under `src/Hexalith.Timesheets.Contracts/Models/`, likely `TimesheetsDashboardOverviewReadModel` plus small summary records for current period, pending actions, approval workload, reporting shortcuts, ledger/export readiness, and projection status.
  - [x] Add a `timesheets.dashboard.overview` or equivalent descriptor in `TimesheetsMetadataCatalog` using `TimesheetsSurfaceKind.Projection` and `TimesheetsCompositionPattern.FrontComposerProjectionView`.
  - [x] Include descriptor fields for current period state, pending correction/submission counts, approval workload status, report shortcuts, Approved-Time Ledger shortcut/readiness, freshness/status messaging, preserved filters, and the single relevant empty-state action.
- [x] Compose the dashboard from existing projections and policy outcomes (AC: 2, 3, 4)
  - [x] Add a server query service under `src/Hexalith.Timesheets.Server/OperationalReports/` or a focused `Dashboard/` capability folder that calls existing query/projection boundaries instead of reading raw event streams or aggregate state.
  - [x] Reuse existing patterns from `TimeEntryEvidenceListQueryService`, `TimesheetPeriodSummaryQueryService`, `ActualTimeReportQueryService`, and `ApprovedTimeLedgerQueryService` for tenant-first `ProjectionRead` authorization, per-row/project/work filtering, display hydration after authorization, and fail-closed denied outcomes.
  - [x] Register the dashboard service and unavailable/fail-closed projection reader in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] If a dashboard projection reader abstraction is added, provide an unavailable default that returns no dashboard data or an unavailable freshness state; do not make the default leak identifiers or fabricate live counts.
- [x] Enforce dashboard authority and disclosure rules (AC: 4, 5)
  - [x] Use existing `TimesheetsOperation.ProjectionRead` for dashboard data access and evaluate action affordances through existing UI/action policy outcomes where available. Approval/export shortcuts must be hidden or disabled based on policy, not shown optimistically.
  - [x] Show approval workload only when approver authority can be resolved. If authority is denied, ambiguous, stale, or unavailable, show a safe hidden/disabled state with persistent message-bar guidance and no protected row labels.
  - [x] Show ledger/export shortcuts only when ledger access and export readiness allow them. Do not show `Export approved ledger` when the user lacks finance/export authority, when projection freshness is not fresh enough, or when no exportable approved rows exist.
  - [x] Empty dashboard states must prefer one safe action such as `Record time`; they must not advertise export, approval, or finance shortcuts without data and authority.
- [x] Keep UI implementation inside FrontComposer/Fluent UI V5 conventions (AC: 1, 6, 7)
  - [x] Prefer metadata-driven FrontComposer composition. If `Hexalith.Timesheets.UI` is scaffolded in this story, follow the architecture UI project structure and add the paired UI test project; otherwise keep the surface contract/metadata-only.
  - [x] Do not build a custom landing page, marketing hero, custom shell, decorative cards, raw HTML/CSS component system, JavaScript, Fluent UI V4 components, custom palette, custom typography, or Timesheets-specific theme.
  - [x] Use `FluentMessageBar` for stale/rebuilding/degraded/unavailable projections, permission-denied states, policy blocks, or unavailable authority; use text-bearing status badges for period, approval, billable, correction, freshness, and export readiness states.
  - [x] Preserve period/filter context in shortcut intents so dashboard drill-in to `Record time`, `My Timesheet Period`, `Approvals Queue`, Project/Work reports, AI effort report, and Approved-Time Ledger can return without losing context.
- [x] Add focused contract, server, integration, accessibility/conformance, and metadata tests (AC: 1-7)
  - [x] Update metadata catalog tests in `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`, `TimeCaptureContractTests.cs`, and a focused dashboard contract test to include the new descriptor count/name/order, fields, actions, status vocabularies, and no runtime dependency leakage.
  - [x] Add dashboard query/read-model serialization tests proving no caller authority fields, no raw EventStore terms, no copied sibling-owned state, and no invoice/payroll/rate/tax/revenue-recognition language.
  - [x] Add server tests proving denied tenant access fails closed before projection reads; insufficient-role rows are filtered; unsafe denials fail closed; stale/degraded/unavailable freshness becomes persistent status; and hidden/disabled shortcuts never disclose protected IDs.
  - [x] Add integration tests proving the dashboard composes current period, pending corrections/submissions, approval workload, report shortcuts, ledger readiness, and empty states from existing projection fixtures.
  - [x] If UI components are added, add UI/accessibility/conformance tests for keyboard focus order, message bars, text-bearing status badges, no hover-only controls, and Fluent UI V5/FrontComposer usage.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts, Server, Integration, Architecture, and UI tests if a UI project is added. If VSTest socket permissions block `dotnet test`, use the repository's direct xUnit v3 executable fallback and record the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `.agents/skills/bmad-create-story/SKILL.md`, `discover-inputs.md`, `template.md`, and `checklist.md` completely.
- Loaded `AGENTS.md`, `Hexalith.AI.Tools/hexalith-llm-instructions.md`, and `Hexalith.AI.Tools/hexalith-ux-instructions.md`.
- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-7-surface-timesheets-dashboard-overview` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md`, especially Epic 4 and Story 4.7 plus FR16, FR18, FR20, NFR13, UX-DR1, UX-DR4, UX-DR11, UX-DR20, UX-DR33, and UX-DR34.
- Loaded `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, query/API patterns, frontend architecture, project structure, UI project timing, projection freshness, and test organization.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `addendum.md`, and `.decision-log.md`, especially FR16, FR18, FR20, module boundaries, data governance, NFRs, and non-goals.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `DESIGN.md`, and `.decision-log.md`, especially the Timesheets dashboard surface, FrontComposerShell rule, component patterns, state patterns, voice/tone, and accessibility floor.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/4-6-verify-finance-export-evidence-and-audit-trail.md`.
- Loaded relevant current code around `TimesheetsMetadataCatalog`, UI metadata descriptors, query contracts/read models, query services, runtime registration, and metadata/test patterns.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 3 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `addendum.md`, and `.decision-log.md`.
- `{ux_content}` loaded from 3 sharded files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `DESIGN.md`, and `.decision-log.md`.
- Persistent facts discovered 6 sibling `project-context.md` files under `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer`; only story-relevant boundary facts are carried here.

### Epic And Story Context

- Epic 4 covers operational time queries, Approved-Time Ledger, Project/Work actual-time reports, AI effort reporting, finance export, export audit, and dashboard composition.
- Story 4.7 is the final Epic 4 composition story. It must consume existing read-side capabilities and metadata from Stories 4.1-4.6 instead of inventing a new source of authority.
- FR16 requires operational Time Entry query by contributor, target, period, Activity Type, billable flag, approval state, and source type with projection freshness and result-level authorization.
- FR18 requires the Approved-Time Ledger projection to remain rebuildable evidence, not authoritative storage.
- FR20 requires AI effort reporting to keep wall-clock time, model/tool runtime, billable effort, and token counts separate from human/external duration.
- UX requires the Timesheets dashboard to be reached from module navigation/shell landing and to summarize My current period, pending actions, approval workload, and report shortcuts.

### Current Code State To Extend

- There is no dashboard contract, dashboard read model, dashboard service, dashboard projection reader, dashboard metadata descriptor, or Timesheets UI project in the current source tree.
- `TimesheetsMetadataCatalog.Descriptors` currently exposes 22 metadata descriptors for record time, submission, external contribution, magic-link adjustment, period submission, corrections, Activity Type catalog, Time Entry evidence/query, Approved-Time Ledger/export, Project/Work actual-time reports, My Timesheet Period, Period Approval Detail, Approvals Queue, approval commands, export policy review, and magic-link capabilities.
- `TimesheetsMetadataCatalog` is contract-only metadata. Existing tests require descriptor order/count stability, no runtime dependency strings such as `Microsoft.FluentUI`, no EventStore leakage, and no finance-ownership vocabulary.
- `TimesheetsSurfaceKind` currently supports `Command` and `Projection`; dashboard should use `Projection` unless a separate enum value is deliberately added with tests.
- `TimesheetsCompositionPattern` currently supports `FrontComposerGeneratedForm` and `FrontComposerProjectionView`; dashboard should use `FrontComposerProjectionView`.
- `QueryTimeEntries`, `QueryApprovedTimeLedger`, `QueryProjectActualTimeReport`, and `QueryWorkActualTimeReport` already model the filter dimensions the dashboard needs for shortcuts and counts. Reuse their shape where possible instead of creating divergent filters.
- `TimeEntryQueryReadModel`, `TimesheetPeriodSummaryReadModel`, `ApprovedTimeLedgerReadModel`, and `ActualTimeReportReadModel` already carry projection freshness metadata.
- `TimeEntryEvidenceListQueryService`, `ApprovedTimeLedgerQueryService`, and `ActualTimeReportQueryService` are the current disclosure-boundary examples: tenant-first authorization, projection reader call, row-level authorization, insufficient-role filtering, fail-closed unsafe denials, and display hydration after row authorization.
- `TimesheetPeriodSummaryQueryService` reads a single period summary through `ITimesheetPeriodSummaryProjectionReader`; the current unavailable default returns null and therefore fails closed.
- `ServiceCollectionExtensions.AddTimesheetsServerKernel` registers query services, unavailable projection readers, display hydration providers, fail-closed authorization defaults, and export services. Dashboard services/readers should follow this registration pattern.

### Files Being Modified Or Extended

- Likely NEW: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryTimesheetsDashboardOverview.cs`.
- Likely NEW: `src/Hexalith.Timesheets.Contracts/Models/TimesheetsDashboardOverviewReadModel.cs` and focused sub-records/enums as needed for current period, pending actions, approval workload, shortcut, and freshness/status summaries.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` to add a dashboard descriptor and FrontComposer shortcut/action metadata.
- Likely UPDATE: `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`, `TimeCaptureContractTests.cs`, and a focused dashboard contract test file for descriptor order/count, serialization, vocabularies, and vocabulary/privacy checks.
- Likely NEW: `src/Hexalith.Timesheets.Server/OperationalReports/TimesheetsDashboardOverviewQueryService.cs` or `src/Hexalith.Timesheets.Server/Dashboard/TimesheetsDashboardOverviewQueryService.cs`.
- Likely NEW: `src/Hexalith.Timesheets.Server/OperationalReports/ITimesheetsDashboardOverviewProjectionReader.cs` and `UnavailableTimesheetsDashboardOverviewProjectionReader.cs`, if dashboard composition needs an explicit read boundary.
- Likely UPDATE: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` to register dashboard service/readers.
- Likely NEW: `tests/Hexalith.Timesheets.Server.Tests/TimesheetsDashboardOverviewQueryServiceTests.cs`.
- Likely NEW or UPDATE: `tests/Hexalith.Timesheets.IntegrationTests/TimesheetsDashboardOverviewIntegrationTests.cs` and `HostMetadataEndpointTests.cs`.
- Possible NEW: `src/Hexalith.Timesheets.UI/` and `tests/Hexalith.Timesheets.UI.Tests/` only if this story implements hand-authored UI beyond metadata. If added, follow architecture UI project timing and Fluent UI V5-only rules.

For every UPDATE file above, preserve existing behavior:

- Do not reorder existing metadata descriptors unless tests are deliberately updated and consumers remain compatible.
- Do not rename or remove existing query/read-model fields from previous stories.
- Do not change Approved-Time Ledger export readiness semantics from Story 4.6.
- Do not weaken row-level authorization/hydration sequencing in existing query services.
- Do not turn unavailable default readers into permissive sample data providers.

### Architecture Constraints

- EventStore remains the only authoritative persistence boundary. A dashboard overview is read-only composition and must not create EventStore audit, approval, export, or dashboard state unless an existing command is invoked elsewhere by user action.
- Projections are rebuildable and non-authoritative for writes. Dashboard counts and statuses must expose freshness/trust metadata and cannot authorize approval/export/correction decisions by themselves.
- Tenant and resource gates run before dashboard projection reads, row disclosure, display hydration, shortcut disclosure, or action enablement.
- JWT tenant/user claims are request evidence, not authority. Do not accept caller-supplied tenant/user/authorization/correlation fields in dashboard query contracts.
- Sibling modules own Project, Work, Party, and Tenant data. Dashboard contracts and metadata use stable references and hydrated labels only after authorization; they must not persist or copy sibling-owned state.
- Logs/traces and metadata must not include comments, command bodies, raw claims, personal data, token values, magic-link tokens, raw EventStore envelopes, stream names, sequence internals, full rows, or protected identifiers.
- Contract evolution is additive and serialization-tolerant. New dashboard contracts should be optional-friendly and avoid renaming existing contract members.
- Use the existing pinned stack: .NET 10, `.slnx`, Central Package Management, nullable, implicit usings, warnings as errors, xUnit v3, Shouldly, NSubstitute, FrontComposer metadata, and Blazor Fluent UI V5 semantics.

### UX And UI Constraints

- The dashboard must live inside `FrontComposerShell`; it must not add a parallel shell, standalone portal, marketing landing page, or bespoke dashboard frame.
- The first viewport must be operational and dense enough for repeated use: current period status, pending actions, approval workload where authorized, report shortcuts, ledger readiness/status, and projection freshness.
- Use FrontComposer and Fluent UI V5 first. Do not use raw HTML/CSS/JavaScript or third-party components where FrontComposer/Fluent UI components exist.
- Do not redefine theme, palette, typography, or spacing. Use FrontComposer/Fluent defaults and Fluent UI V5 component parameters/tokens only where needed.
- Use `FluentMessageBar` for stale, rebuilding, degraded, unavailable, permission-denied, policy-blocked, or authority-unresolved states.
- Use text-bearing status badges for period, approval, correction, billable, contributor category, projection freshness, and export readiness. Color-only status is forbidden.
- Use `FluentToast` only for transient feedback after successful user-triggered actions. Dashboard freshness/policy/authority state must remain persistent.
- Empty current period state should show period boundary and a single safe action such as `Record time`.
- Empty export/ledger state should not produce or advertise export as successful evidence; use the established text pattern `No approved entries match these filters.` where relevant.
- Avoid invoice, payroll, rate-card, tax, revenue-recognition, Project ownership, Work ownership, Party profile, productivity coaching, gamification, timer-app, and celebratory language.

### Previous Story Intelligence

- Story 4.6 implemented persistent accepted-export audit evidence and deterministic export verification. Dashboard work must preserve export readiness and audit semantics rather than recomputing them.
- Story 4.6 added/changed `ApprovedTimeExported`, export audit metadata, `TimesheetsMetadataCatalog`, `ApprovedTimeExportCsvWriter`, export service/result classes, runtime registration, and export tests.
- Story 4.6 direct xUnit fallback passed after VSTest socket permission issues: Contracts 83/83, Server 374/374, Integration 49 passed with 2 existing infrastructure/performance skips, Projections 77/77, Architecture 21/21.
- Story 4.6 confirms metadata descriptors may mention `FluentDialog`, `FluentMessageBar`, and `FluentToast` as semantic UI component guidance, but should not depend on runtime UI packages from contracts.
- Epic 3 retrospective warns that any dashboard shortcut referencing confirmation state must not rely on the currently registered unavailable magic-link loader as live persisted truth.

### Git Intelligence Summary

- Last 5 commit titles:
  - `d55750e feat(story-4.6): Verify Finance Export Evidence and Audit Trail`
  - `1a4ad11 feat(story-4.5): Generate Finance Export from Approved Ledger`
  - `13b4435 feat(story-4.4): Surface AI Effort Reporting`
  - `d0bc176 feat(story-4.3): Produce Project and Work Actual-Time Reports`
  - `f17c2bb feat(story-4.2): Project Approved Time Ledger from Domain Events`
- Recent Epic 4 work consistently added contract read models, metadata descriptors, server query/composition services, fail-closed unavailable readers, and focused contract/server/integration tests.
- The current working tree before creating this story already had unrelated modifications in `Hexalith.FrontComposer`, `Hexalith.Tenants`, and `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, file-scoped namespaces, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a parallel shell, or a custom Timesheets UI theme.
- Use `ConfigureAwait(false)` on every awaited production call.
- No external latest-version research is needed for this story because no new framework, library, external API, or dependency selection is required; the architecture and repo pin the implementation stack.

### Testing Standards

- Every claimed dashboard count, status, shortcut, freshness state, authority decision, empty state, and disclosure decision needs executable proof.
- Test that denied tenant access fails closed before projection reads and does not disclose tenant, contributor, Project, Work, Time Entry, period, ledger, or export details.
- Test insufficient-role rows are filtered where existing query services allow filtering; unsafe row denials fail closed and do not hydrate labels.
- Test stale, rebuilding, degraded, and unavailable projection states are surfaced in dashboard read models/metadata and prevent stale data from being described as fresh decision authority.
- Test approval workload is hidden or disabled when approver authority is denied, ambiguous, stale, or unavailable.
- Test ledger/export shortcuts are hidden or disabled when finance/export authority is absent, projection freshness is not acceptable, or no exportable approved rows exist.
- Test empty current period and empty dashboard states show one safe primary action such as `Record time` and do not advertise export/finance actions.
- Test shortcut intents preserve period/filter context for capture, period review, approvals, reports, AI effort report, and ledger drill-in/back navigation.
- Test metadata JSON contains dashboard descriptor fields/actions/status vocabularies but omits server-controlled authority, raw EventStore terms, finance ownership vocabulary, copied sibling-owned state, and runtime dependency leakage.
- If UI is implemented, test keyboard access, focus order, text-bearing status badges, persistent message bars, WCAG 2.2 AA expectations, and absence of hover-only controls/color-only states.

### Anti-Patterns To Prevent

- Do not create a dashboard table, cache, event, or state store outside EventStore/projection architecture.
- Do not create new approval, export, finance, or period authority inside dashboard code.
- Do not compute finance values, rates, invoices, taxes, payroll, revenue recognition, or export totals beyond existing approved-ledger/export readiness contracts.
- Do not present stale/rebuilding/degraded/unavailable projection counts as fresh.
- Do not show approval workload, ledger rows, export actions, Project/Work labels, Party labels, or Time Entry identifiers before authorization succeeds.
- Do not hydrate labels for denied rows.
- Do not use raw EventStore envelopes, stream names, aggregate state, export audit events, or operational report rows as shortcut authority when existing query services already define disclosure boundaries.
- Do not include comments, raw claims, command bodies, tokens, magic-link tokens, personal data, full rows, or protected identifiers in dashboard metadata, diagnostics, logs, or no-access responses.
- Do not add decorative card-heavy layouts, marketing hero copy, custom shell, custom portal, bespoke brand, custom palette, custom typography, gamification, timer-app positioning, or hover-only controls.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-7-Surface-Timesheets-Dashboard-Overview`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-4-Approved-Time-Ledger-Reporting--Finance-Export`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR16`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR18`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR20`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR20`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-16-Query-Time-Entries-by-operational-dimensions`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-18-Maintain-the-Approved-Time-Ledger`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-20-Surface-AI-agent-effort-reporting`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Non-Goals`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Gap-Analysis-Results`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Information-Architecture`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Brand--Style`]
- [Source: `Hexalith.AI.Tools/hexalith-ux-instructions.md#Component-sources`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataDescriptor.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/QueryTimeEntries.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceListQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- 2026-06-19: `dotnet test` for Contracts, Server, Integration, and Architecture was blocked by VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`. Used repository direct xUnit v3 executable fallback.
- 2026-06-19: Direct xUnit fallback passed: Contracts.Tests 86/86, Server.Tests 379/379, IntegrationTests 51 passed / 2 existing explicit skips, ArchitectureTests 21/21.

### Completion Notes List

- Added read-only dashboard query and overview read models carrying period/filter context, current period status, pending actions, approval workload, report shortcuts, ledger readiness, projection status, and one safe empty-state action.
- Added `timesheets.dashboard.overview` FrontComposer projection metadata with status vocabularies, preserved shortcut context, and persistent `FluentMessageBar` guidance for stale, degraded, unavailable, and policy-blocked states.
- Added dashboard server composition service that performs tenant-first `ProjectionRead` authorization, composes through existing time-entry, actual-time report, and approved-ledger query services, and evaluates capture/approval/report/ledger/export affordances through existing UI action policy outcomes.
- Kept the implementation metadata-driven; no `Hexalith.Timesheets.UI` project, custom shell, raw UI, or dashboard write authority was added.
- Added contract, server, integration, runtime registration, and host metadata coverage for dashboard serialization, descriptor shape, hidden/disabled policy states, freshness status, preserved filters, and empty-state behavior.

### File List

- `_bmad-output/implementation-artifacts/4-7-surface-timesheets-dashboard-overview.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Models/TimesheetsDashboardOverviewReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryTimesheetsDashboardOverview.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Server/Dashboard/TimesheetsDashboardOverviewQueryResult.cs`
- `src/Hexalith.Timesheets.Server/Dashboard/TimesheetsDashboardOverviewQueryService.cs`
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsUiActionPolicyOutcome.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/DashboardContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/TimesheetsDashboardOverviewIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimesheetsDashboardOverviewQueryServiceTests.cs`

## Change Log

- 2026-06-19: Implemented Story 4.7 dashboard contracts, metadata descriptor, server composition service, runtime registration, and focused contract/server/integration tests.
- 2026-06-19: Adversarial code review (auto-fix). Fixed 3 issues (1 High, 2 Medium); rebuilt clean and reran affected test projects. Status set to done (0 critical).

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-19
**Outcome:** Approved after auto-fixes. Acceptance Criteria 1-7 are implemented as read-only, metadata-driven composition over existing projections with policy/freshness-gated shortcuts; no new write/finance authority was introduced. Build is clean (`-warnaserror`, 0 warnings). Tests: Contracts 86/86, Server 379/379, Integration 52 passed/2 explicit skips, Architecture 21/21.

### Issues found and fixed

1. **[High][AC5] Record time wrongly gated by read-projection freshness.** The capture/`record-time` shortcut state was derived from `currentPage.ProjectionFreshness` (`Fresh ? Ready : BlockedByFreshness`). Record time is a write action and the designated single safe empty-state action; in the empty/`Unavailable` projection case it became `BlockedByFreshness`, contradicting AC5. The empty-state integration test even claimed "ready" in its name without asserting it. Fixed so the capture shortcut is `Ready` whenever capture authority is `Allowed`, independent of read freshness. Updated the stale-projection server test and added an explicit `State == Ready` assertion to the empty-projection integration test. [`TimesheetsDashboardOverviewQueryService.cs`]
2. **[Medium][Correctness] Pending-correction count over-counted already-handled rows.** The count included `TimeEntryCorrectionState.Corrected`/`Superseded` rows in addition to `Rejected`. `Corrected`/`Superseded` are completed/replaced states (and non-current rows are excluded by `CurrentEntriesOnly`), so they are not "pending correction". This overstated the badge and conflicted with the period-state logic that treats only `Rejected` as `NeedsCorrection`. Fixed to count `Rejected` entries awaiting correction. [`TimesheetsDashboardOverviewQueryService.cs`]
3. **[Medium][Maintainability] Brittle magic-string coupling for unresolved authority.** `MapAuthorityState` detected the "authority unresolved" outcome by matching the literal string `"Authority cannot be resolved."`, duplicated from `TimesheetsUiActionPolicyOutcome.AuthorityUnresolved`. A wording change would have silently downgraded `Unresolved` to `Denied`. Promoted the text to a shared `TimesheetsUiActionPolicyOutcome.AuthorityUnresolvedMessage` constant referenced by both producer and consumer. [`TimesheetsUiActionPolicyOutcome.cs`, `TimesheetsDashboardOverviewQueryService.cs`]

### Observations (non-blocking, not changed)

- **[Low] Counts are computed from a single page (`PageSize` 200 for entries, 50 for reports).** Summary counts (`EntryCount`, `SubmittedEntryCount`, `DisclosedApprovedRowCount`) silently cap at the page size; underlying read models expose no total. Acceptable for a per-operator current-period summary, but a busy approval workload could undercount. Worth a follow-up if a total-count boundary becomes available.
- The five projection reads run sequentially; they could be parallelized, but the sequential pattern matches sibling query services and is not a correctness issue.
