---
baseline_commit: d0bc1766323a3d4ecf62beb44e45a127c4d9e34b
---

# Story 4.4: Surface AI Effort Reporting

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an AI agent operator or project reviewer,
I want AI effort reported separately from human and external effort,
so that automation cost and runtime are visible without implying all units are interchangeable.

## Acceptance Criteria

1. Given Time Entries include AI effort metrics, when AI effort reports are queried, then reports include wall-clock time, model/tool runtime, billable effort, and provider-reported token counts where available, and each metric preserves its explicit unit and source metadata.
2. Given AI token metrics are unavailable or not reported by a provider, when AI reports or Time Entry Detail display those fields, then the UI shows `Unavailable` or `Not reported by provider`, and missing metrics are never displayed or exported as zero.
3. Given combined human, external, and AI effort reports are displayed, when totals are shown, then human/external duration totals and AI runtime/token metrics remain visually and semantically separated, and no default token-to-hours conversion is performed.
4. Given an AI report is filtered by AI agent Party or Work Reference, when results are returned, then tenant isolation, result-level authorization, projection freshness, and reference-state metadata are enforced, and Timesheets stores stable references only.
5. Given AI effort reporting UI is displayed, when users inspect metrics and drill into entries, then FrontComposer/Fluent UI V5 surfaces use clear labels, status badges, accessible tables, and explicit unavailable states, and AI insight is not presented as approval, payroll, invoicing, or finance authority.

## Tasks / Subtasks

- [x] Add AI effort report contract/read-model fields without breaking existing actual-time reports (AC: 1, 2, 3)
  - [x] Extend `ActualTimeReportRowReadModel` or add a focused AI-effort report read model under `src/Hexalith.Timesheets.Contracts/Models` that carries AI wall-clock milliseconds, model/tool runtime milliseconds, AI billable effort minutes, provider input/output/total token counts, token availability, metric availability, and `AiEffortMetricSourceMetadata`.
  - [x] Preserve `ActualMinutes` as human/external whole-minute duration only; do not reuse it for AI runtime, token counts, or AI billable effort semantics.
  - [x] Keep all unavailable/not-reported AI metric values nullable with explicit `AiMetricAvailability` and `AiTokenMetricAvailability`; do not serialize missing token metrics as `0`.
  - [x] If a dedicated query is needed, add it under `src/Hexalith.Timesheets.Contracts/Queries/Reporting` with filters for Work Reference, AI agent Party, Activity Type, period/date range, Billable Flag, Approval State, current-only/include-superseded, sort, page size, and opaque cursor.
  - [x] Do not accept caller-supplied `TenantId`, `UserId`, claims, roles, correlation authority, server-side freshness overrides, message IDs, display labels, or converted token/hour values in any query body.
- [x] Extend report projections to roll up AI metrics by explicit units (AC: 1, 2, 3, 4)
  - [x] Update `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs` and/or add a focused AI report projection in the same area.
  - [x] Source AI values from existing `TimeEntryEvidenceReadModel.AiMetrics`, `TimeEntryRecorded.AiMetrics`, and `TimeEntryCorrectionValues.AiMetrics`; do not invent a second AI metrics store.
  - [x] Filter AI report rows to `ContributorCategory.AutomatedAgent` or entries with non-null/non-unavailable `AiMetrics` as appropriate; human/external rows may remain in combined views but must not be merged into AI unit totals.
  - [x] Preserve approved evidence semantics by reusing `ApprovedTimeLedgerProjection` and/or `TimeEntryEvidenceListProjection`; do not re-create approval authority from raw entry state.
  - [x] Preserve correction lineage: current values are default, superseded evidence appears only when requested, and AI metric corrections use corrected/prior `AiEffortMetrics` consistently.
  - [x] Keep projection behavior idempotent and deterministic: dedupe by `MessageId`, process by `SequenceNumber`, tolerate duplicate/replayed events, and keep stable sorting/cursor paging.
  - [x] Use `ProjectionFreshnessMetadataMapper` for freshness; do not add another freshness mapper.
- [x] Preserve server-side authorization, hydration, and fail-closed reporting behavior (AC: 4)
  - [x] Extend `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs` only where needed, preserving tenant-first `TimesheetsOperation.ProjectionRead` authorization before projection lookup.
  - [x] Re-authorize each disclosed AI report row through `ITimesheetsAccessGuard` using exact Contributor Party and Project/Work target.
  - [x] Keep ordinary insufficient-role row filtering only where the existing policy allows; cross-tenant, stale authority, ambiguous authority, unavailable sibling authority, invalid reference, and unconfigured policy must fail closed.
  - [x] Hydrate Party, Project, Work, and Activity Type labels only after row authorization succeeds. Denied rows must not be hydrated and must not leak labels, protected identifiers, AI source metadata, token counts, or target details through errors, logs, or diagnostics.
  - [x] Keep Works planned-effort lookup separate from AI effort reporting. Planned/estimated Works values remain Works-sourced; AI runtime/token metrics remain Timesheets entry evidence.
- [x] Publish FrontComposer metadata for AI effort reporting surfaces (AC: 2, 3, 5)
  - [x] Add or extend descriptors in `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`, for example `timesheets.projection.ai-effort-report` or AI fields on the Project/Work actual-time report descriptors.
  - [x] Use `TimesheetsCompositionPattern.FrontComposerProjectionView`; do not add a Timesheets-specific UI shell, raw HTML/CSS UI, or raw EventStore browsing path.
  - [x] Include FilterBar fields for Work Reference, AI agent Party/Contributor, period/date range, Activity Type, Billable Flag, Approval State, current-only/include-superseded, AI metric availability, token availability, source category, and projection freshness.
  - [x] Include grid/detail fields for human/external actual minutes, AI wall-clock execution, model/tool runtime, AI billable effort, provider input/output/total tokens, token availability, AI metric source category/provider/tool/work execution ID, source row count, correction/superseded counts, reference state, display hydration, and projection freshness.
  - [x] Include text-bearing status badge vocabularies for contributor category, AI metric availability, AI token availability, AI source category, approval state, billable state, report row state, reference/hydration state, and projection freshness.
  - [x] UI metadata/copy must show unavailable tokens as `Unavailable` or `Not reported by provider`; never silently display `0` unless a provider reported zero.
  - [x] AI effort must use factual labels and normal status treatment; do not use special promotional colors or imply AI insight is approval, payroll, invoicing, export, rate, tax, or revenue authority.
- [x] Add focused tests and quality evidence (AC: 1-5)
  - [x] Contracts tests: query/read models round-trip AI units, token availability, source metadata, null unavailable metrics, filters, cursor, and freshness; JSON omits server-controlled authority fields and converted token/hour fields.
  - [x] Projection tests: AI rows roll up wall-clock/model runtime/billable effort/tokens by Work/AI agent/period/activity/billable/approval state; human/external duration remains separate; unavailable token metrics stay null and explicit; duplicate/replayed events do not duplicate totals; correction/superseded include/exclude behavior is deterministic.
  - [x] Server tests: tenant denial prevents projection lookup; row denial prevents hydration and planned-effort/AI detail disclosure; Work and AI agent filters use exact `ITimesheetsAccessGuard` requests; stale/unavailable authority fails closed.
  - [x] Metadata tests: AI effort descriptor/fields/status vocabularies exist, use `FrontComposerProjectionView`, contain explicit unavailable-token fields, and contain no EventStore stream, invoice, payroll, rate, tax, revenue-recognition, Project ownership, Work lifecycle ownership, or promotional AI language.
  - [x] Integration tests: seeded AI agent Work scenarios query through services with freshness metadata, deterministic paging, authorization, drill-in compatibility with `ReadTimeEntryEvidence`, and unavailable provider-token behavior.
  - [x] Privacy tests: reports, metadata, logs, and diagnostics do not expose prompts, responses, raw provider payloads, command bodies, secrets, token values beyond provider-reported counts, denied row labels, copied Party/Project/Work display data, raw EventStore envelopes, comments when policy excludes them, rates, invoice totals, payroll values, taxes, or revenue-recognition data.
- [x] Verify affected build and test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: Contracts, Projections, Server, Integration, and Architecture test projects.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `.agents/skills/bmad-create-story/SKILL.md`, `discover-inputs.md`, `template.md`, and `checklist.md` completely.
- Loaded `Hexalith.AI.Tools/hexalith-llm-instructions.md`.
- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-4-surface-ai-effort-reporting` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md` sections for Epic 4 and Story 4.4, plus relevant FR/UX traceability context.
- Loaded `_bmad-output/planning-artifacts/architecture.md` sections covering AI multi-unit evidence, projections, query APIs, FrontComposer surfaces, project structure, and data exchange formats.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR15, FR20, SM-4, SM-6, SM-7, and the AI metrics risk.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially AI Effort Report, Time Entry Detail, unavailable token metrics, and non-promotional AI metric styling.
- Loaded persistent facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/4-3-produce-project-and-work-actual-time-reports.md`.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md`.
- `{ux_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md`.
- Persistent facts loaded from 6 sibling `project-context.md` files.

### Epic And Story Context

- Epic 4 covers operational time queries, Project/Work actual-time reports, AI effort reporting, Approved-Time Ledger, finance export, export audit, and dashboard composition.
- Story 4.4 implements FR15 and FR20. It depends on existing Time Entry AI metrics and Story 4.3 actual-time report infrastructure.
- Story 4.4 must report AI effort beside human/external effort without treating units as interchangeable. Human/external `DurationMinutes`, AI wall-clock milliseconds, AI model/tool runtime milliseconds, AI billable effort minutes, and token counts are separate facts.
- Provider token counts are reportable only where provider-reported. Missing/unavailable/not-reported token metrics are explicit states, not zeros.
- AI report filters must support AI agent Party and Work Reference and preserve tenant isolation, result-level authorization, projection freshness, and reference-state metadata.
- Story 4.4 must not implement Story 4.5 finance export generation, Story 4.6 export audit trail, Story 4.7 dashboard overview, rates, invoices, payroll, taxes, or revenue recognition.

### Current Code State To Extend

- `AiEffortMetrics` already exists at `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs` with `AiMetricAvailability`, wall-clock milliseconds, model runtime milliseconds, billable effort minutes, provider input/output/total token counts, source metadata, and `AiTokenMetricAvailability`.
- `AiEffortMetricSourceMetadata` already exists with source category, provider name, tool name, and Work execution ID. It must remain compact metadata; do not add prompts, responses, secrets, request bodies, or personal data.
- `RecordTimeEntry`, `TimeEntryRecorded`, `TimeEntryEvidenceReadModel`, and `TimeEntryCorrectionValues` already carry optional `AiEffortMetrics`.
- `TimeEntryQueryRowReadModel` currently derives `TimeEntrySourceType.AutomatedAgent` when contributor category is `AutomatedAgent` and AI metrics are present, but it does not expose the AI metrics themselves.
- `ActualTimeReportRowReadModel` currently carries `ActualMinutes`, source row count, correction/superseded counts, reference state, freshness, display hydration, and optional Works planned effort. It does not yet carry AI metric totals or availability/source metadata.
- `ActualTimeReportProjection` currently groups by target, monthly period, contributor, activity type scope, billable state, approval state, and contributor category. It sums only `DurationMinutes`.
- `ActualTimeReportProjection` reuses `ApprovedTimeLedgerProjection` for approved rows and `TimeEntryEvidenceListProjection` for selected/non-approved rows. Preserve that reuse so Story 4.4 does not duplicate approval or correction semantics.
- `ActualTimeReportQueryService` already performs tenant-first authorization, per-row authorization, post-authorization hydration, fail-closed denials, and Works planned-effort lookup only for authorized Work rows. Preserve this disclosure order.
- `TimesheetsMetadataCatalog` already exposes AI metric fields on Record Time Entry and Time Entry Detail, and Project/Work actual-time report descriptors without AI report fields. Story 4.4 should add AI report metadata or extend report descriptors with clear AI fields.

### Architecture Constraints

- EventStore remains the only authoritative persistence boundary. Do not add SQL, Redis, local files, mutable caches, direct projection mutation, or broker-backed CRUD as AI report authority.
- Reports and AI effort reports are rebuildable projections. They must be idempotent, replay-safe, duplicate-tolerant, and expose freshness/degraded/rebuilding/unavailable states.
- Query contracts return typed DTOs/read models and `ProjectionFreshnessMetadata`, not raw EventStore envelopes, aggregate state, or debug stream views.
- Tenant/resource gates run before projection reads and before any row disclosure. JWT tenant/user claims are request evidence, not authority.
- Timesheets stores stable Tenant, Party, Project, and Work references only. Display labels are read-time hydration after authorization, not copied sibling state.
- AI effort evidence needs provenance and correction path. AI input must not bypass tenant, reference, approval, lock, audit, or no-disclosure checks.
- Dates and periods follow v1 policy: UTC audit instants plus tenant-local dates/period keys where business rules require them.
- Logs/traces must use structured metadata and correlation IDs without event payloads, comments, personal data, prompts, responses, provider request/response bodies, tokens/secrets, raw claims, protected identifiers, command bodies, or report row payload dumps.
- Public contract evolution must be additive and serialization-tolerant. Do not rename existing Time Entry events, evidence read models, ledger contracts, or actual-time report query contracts.

### UX And UI Constraints

- AI Effort Report must use `FrontComposerProjectionView` and dense `FluentDataGrid` semantics through metadata, not a hand-built UI package in this story.
- Filters belong above the grid and must be preserved during drill-in/back navigation by the generated/FrontComposer surface.
- Row click should open Time Entry Evidence/Detail, not EventStore streams or projection internals.
- Time Entry Detail and AI Effort Report must separate human/external duration from AI wall-clock, model/tool runtime, billable effort, and token metrics.
- Token unavailable states must display as `Unavailable` or `Not reported by provider`; zero is valid only when provider reported zero.
- AI metrics must not use special branded/promotional color. Use factual labels and text-bearing status badges.
- UI copy must not imply Timesheets owns Party, Project, Work, invoice, payroll, rate, tax, revenue, approval authority, or finance authority.
- Related tabs may be used only for close report partitions such as Project / Work or Human / External / AI views.

### Previous Story Intelligence

- Story 4.3 added Project and Work actual-time report contracts, projection, query service, fail-closed reader, Works planned-effort adapter, FrontComposer metadata, and tests.
- Story 4.3 review fixed activity-type scope grouping in `ActualTimeReportProjection`; Story 4.4 must preserve `ActivityTypeScope` in every AI grouping and ordering path.
- Story 4.3 review added missing Works source reference-state metadata; Story 4.4 must expose AI source/availability states with the same clarity.
- Story 4.3 established `ActualTimeReportProjection` as the report projection area and `ActualTimeReportQueryService` as the disclosure boundary. Extend these rather than creating parallel authorization or hydration flows.
- Story 4.2 consolidated freshness mapping into `ProjectionFreshnessMetadataMapper`; reuse it.
- Story 4.1 established operational query contracts, degraded projection freshness, result-level authorization, and fail-closed list readers. Keep the same non-disclosure posture for AI reporting.
- Build/test lanes after Stories 4.1-4.3 used direct xUnit executable fallback where VSTest socket permissions were blocked.

### Git Intelligence Summary

- Last 5 commit titles:
  - `d0bc176 feat(story-4.3): Produce Project and Work Actual-Time Reports`
  - `f17c2bb feat(story-4.2): Project Approved Time Ledger from Domain Events`
  - `da204ec feat(story-4.1): Query Time Entries from Rebuildable Read Models`
  - `735f7e1 docs(retro): Capture Epic 3 Retrospective`
  - `21890e2 feat(story-3.5): Reject Invalid Confirmation Links Without Resource Disclosure`
- Story 4.3 changed `ActualTimeReportReadModel`, `ActualTimeReportRowReadModel`, `QueryProjectActualTimeReport`, `QueryWorkActualTimeReport`, `TimesheetsMetadataCatalog`, `ActualTimeReportProjection`, `ActualTimeReportQueryService`, and matching tests. Story 4.4 should build on those files.
- Story 3.5 and later magic-link work established strict no-disclosure as a project norm. AI reports must not disclose unauthorized Project/Work/Party/Time Entry details or AI metadata through missing-reference states.
- Current working tree before creating this story already had unrelated changes for `Hexalith.FrontComposer`, `Hexalith.Tenants`, and `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, file-scoped namespaces, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a Timesheets UI project, or a parallel shell for this story.
- Use `ConfigureAwait(false)` on every awaited production call.
- No external latest-version research is needed for this story because no new framework, library, or external API selection is required; the architecture and repo pin the implementation stack.

### Project Structure Notes

- Expected contract/model files to update or add:
  - `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportReadModel.cs` if page-level AI totals are added.
  - `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetricSourceMetadata.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`
  - Optional: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryAiEffortReport.cs`
  - Optional: `src/Hexalith.Timesheets.Contracts/Models/AiEffortReportReadModel.cs`
  - Optional: `src/Hexalith.Timesheets.Contracts/Models/AiEffortReportRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
  - `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs` only if new AI report state/sort enums are required.
- Expected projection files to update or add:
  - `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`
  - Optional focused helper under `src/Hexalith.Timesheets.Projections/OperationalReports` for AI metric accumulation if it avoids duplicating unit-handling logic.
- Expected server files to update only if query/service shape changes:
  - `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/IActualTimeReportProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/UnavailableActualTimeReportProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryResult.cs`
  - `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- Expected tests:
  - `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs`
  - `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
  - `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs`
  - `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` if metadata endpoint output changes.
  - `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` or nearby architecture tests if new AI report surfaces need privacy-boundary coverage.
- Do not use `_bmad-output/`, `docs/`, or submodules as implementation scratch space.

### Testing Standards

- Every claimed AI unit, authorization, freshness, reference-state, source metadata, unavailable-token, rollup, correction, and lineage behavior needs executable proof.
- Test tenant-first denial before projection lookup and per-row denial after projection lookup.
- Test Work-target AI rows and Project-target AI rows independently; Work filters must not validate Project and Project filters must not validate Work.
- Test AI agent Party filtering by exact `PartyReference`.
- Test stale, rebuilding, degraded, and unavailable freshness states in projection/read models and service outputs.
- Test token availability states: provider reported counts, provider reported zero, not reported, unavailable, and unknown.
- Test corrections and superseded rows with include/exclude behavior for AI metric values and deterministic totals.
- Test duplicate events and replay from sequence zero produce identical AI rollups and no duplicate counts/minutes/tokens/runtime.
- Test stable ordering and cursor paging with deterministic IDs/dates.
- Test period boundary behavior for tenant-local period key and explicit date range filters.
- Test denied rows do not get display hydration, planned-effort lookup, or leaked Party/Project/Work/Time Entry/AI metric details through labels, errors, telemetry, metadata, or quality evidence.
- Test metadata vocabulary contains text-bearing AI availability/source statuses and does not include ownership/scope-creep language.

### Anti-Patterns To Prevent

- Do not collapse human/external duration, AI wall-clock runtime, model/tool runtime, AI billable effort, token counts, and Works effort units into a single untyped duration/hours field.
- Do not display unavailable/not-reported token metrics as zero.
- Do not treat provider token counts as billable minutes, approval evidence, payroll input, invoice value, rate basis, tax basis, or revenue-recognition evidence.
- Do not treat operational or AI reports as authoritative write-side state.
- Do not re-create approval authority from raw Time Entry state when approved-ledger evidence exists.
- Do not copy Project hierarchy, Project lifecycle state, Work lifecycle state, Work planning state, Party personal data, AI prompts, AI responses, provider payloads, or tenant membership into report rows.
- Do not accept planned/estimated effort, AI converted-hour values, display labels, tenant/user authority, source authority, or freshness override values from query bodies.
- Do not bypass `ITimesheetsAccessGuard` because a row came from a tenant-scoped projection.
- Do not hydrate display labels or query Works planned effort for rows that will be filtered or denied.
- Do not include CSV/API/webhook export, export audit records, export command handling, rates, invoice totals, taxes, payroll values, or revenue-recognition language in this story.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-4-Surface-AI-Effort-Reporting`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR15`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR20`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR28`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-15-Record-AI-Effort-Metrics`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#12-Success-Metrics`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#AI-Effort-Metrics`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#AI-effort-evidence`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Query-API-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Information-Architecture`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#AI-agent-capture-and-reporting`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Color`]
- [Source: `_bmad-output/implementation-artifacts/4-3-produce-project-and-work-actual-time-reports.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetricSourceMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportRowReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionValues.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ProjectionFreshnessMetadataMapper.cs`]
- [Source: `src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~ActualTimeReportContractTests -v minimal` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- Used README direct xUnit v3 executable fallback after building test assemblies.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed; all projects up-to-date.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- Direct xUnit: Contracts `Total: 79, Errors: 0, Failed: 0, Skipped: 0`.
- Direct xUnit: Projections `Total: 75, Errors: 0, Failed: 0, Skipped: 0`.
- Direct xUnit: Server `Total: 358, Errors: 0, Failed: 0, Skipped: 0`.
- Direct xUnit: Integration `Total: 48, Errors: 0, Failed: 0, Skipped: 2`; skips are existing infrastructure/performance evidence placeholders.
- Direct xUnit: Architecture `Total: 20, Errors: 0, Failed: 0, Skipped: 0`.
- `git diff --check` passed.

### Completion Notes List

- Extended existing Project/Work actual-time report contracts with nullable AI wall-clock, model/runtime, billable effort, provider token count, availability, and source metadata fields without introducing a parallel report stack.
- Carried `AiEffortMetrics` through evidence and approved-ledger source rows so the report projection reuses existing evidence and approval/correction semantics.
- Updated actual-time report projection rollups so human/external `ActualMinutes` remain separate from AI runtime, token, and AI billable effort units; provider-not-reported tokens remain null with explicit availability.
- Added AI agent, AI metric availability, token availability, and source-category filters to Project/Work actual-time report queries while keeping tenant/user/correlation/freshness authority out of query bodies.
- Extended Project/Work actual-time report FrontComposer metadata with AI filters, grid/detail fields, text-bearing status vocabularies, and unavailable-token copy.
- Added focused Contracts, Projections, Server, Integration, and Architecture coverage for AI units, token availability, correction lineage, authorization, hydration, metadata vocabulary, and provider-token privacy semantics.

### File List

- `_bmad-output/implementation-artifacts/4-4-surface-ai-effort-reporting.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Models/ActualTimeReportRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryProjectActualTimeReport.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryWorkActualTimeReport.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 4.4 AI effort reporting across actual-time report contracts, projections, metadata, and tests; moved story and sprint status to review.
- 2026-06-19: Senior Developer Review (AI) — auto-fixed order-dependent AI token-availability rollup, documented the contracts "Token" fitness-test carve-out, corrected stale baseline_commit; all affected lanes green; status → done.

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-06-19 · **Outcome:** Approved (auto-fix mode)

### Scope verified

- Cross-referenced the story File List against `git diff HEAD` — all 13 changed source/test files match; no undocumented or phantom changes.
- Independently re-ran the build (`-warnaserror`, 0 warnings / 0 errors) and every affected suite via the README direct xUnit fallback: Contracts 79, Projections 77, Server 358, Integration 48 (2 pre-existing infrastructure skips), Architecture 20 — all green.
- Validated all five Acceptance Criteria against the implementation: explicit AI units + source metadata (AC1), nullable unavailable/not-reported tokens never serialized as zero (AC2), human/external minutes kept separate from AI runtime/token rollups with no token-to-hours conversion (AC3), tenant-first + per-row authorization reused unchanged with an added AI-agent row-auth test (AC4), and FrontComposer metadata with factual labels, text-bearing status badges, and no promotional/finance-authority language (AC5). All implemented.

### Findings and resolutions

- **[MEDIUM — fixed] Order-dependent AI token-availability rollup.** `ReportGroupAccumulator.MergeTokenAvailability` only handled `ProviderReported`/`NotReported` in the `next` position, so a later `ProviderReported` row could silently upgrade a group that contained a `NotReported` sibling — making the aggregate token-availability badge depend on event replay order (its sibling `MergeMetricAvailability` is already order-independent). Proven with a new failing projection test (`Mixed_provider_reported_and_not_reported_token_rows_merge_to_not_reported_regardless_of_order`), then fixed by adding the symmetric `NotReported` case so precedence is a deterministic, conservative `Unknown > NotReported > ProviderReported`. Test now passes both event orderings. File: `src/Hexalith.Timesheets.Projections/OperationalReports/ActualTimeReportProjection.cs`.
- **[LOW — fixed] Undocumented fitness-test weakening.** `DependencyDirectionTests` stripped `AiTokenAvailability`/`AiTokenMetricAvailability` from contract sources before the forbidden-"Token" scan with no explanation, reading like a guard being quietly defeated. Replaced the inline `string.Replace` chain with a documented, explicitly-scoped allow-list (longest-identifier-first) clarifying that provider token-metric vocabulary is exempt while any other `...Token...` member (AuthToken, AccessToken, …) still trips the scan. File: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`.
- **[LOW — fixed] Stale `baseline_commit` frontmatter.** Recorded hash `d0bc17643ad1…` did not resolve; corrected to the real HEAD `d0bc1766323a…`.

### Notes (no change required)

- `ActualTimeReportQueryService` was correctly left untouched: AI rows are the row contributor, so existing per-row `ITimesheetsAccessGuard` re-authorization already covers them (confirmed by the added server test). The task's "extend only where needed" was honored.
- `ActualMinutes` deliberately excludes `AutomatedAgent` rows (AI effort flows only through `AiMetrics`), matching the story's explicit unit-separation requirement.
- Mixed-availability groups can still surface a partial provider-token sum alongside a `NotReported` badge; left as-is because nulling the partial total would discard genuinely reported counts. The badge now correctly signals incompleteness.
