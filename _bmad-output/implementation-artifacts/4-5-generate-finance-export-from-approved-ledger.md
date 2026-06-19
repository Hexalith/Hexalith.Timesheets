---
baseline_commit: 13b4435
---

# Story 4.5: Generate Finance Export from Approved Ledger

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a finance or accounting consumer,
I want to export approved billable time with stable IDs and correction lineage,
so that downstream billing workflows receive trustworthy evidence without Timesheets becoming an invoicing system.

## Acceptance Criteria

1. Given a finance consumer has export authority in a tenant, when they filter the Approved-Time Ledger by tenant, Project Reference, Work Reference, Contributor, Activity Type, period, and Billable Flag, then the export scope is previewed from approved ledger data, and projection freshness and export readiness are visible before export.
2. Given approved billable ledger rows match the requested filters, when the user confirms export, then the export output includes stable IDs, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comments where policy allows, and requester, filters, timestamp, correlation ID, and output scope are auditable.
3. Given corrected or superseded approved entries exist, when export output is generated, then lineage is sufficient for downstream reconciliation, and included/excluded superseded values follow the selected query options.
4. Given no approved entries match the filters or projection freshness is not acceptable for export, when the user opens the export action, then export is disabled or blocked with persistent explanation, and no empty or misleading finance evidence file is produced.
5. Given export output is generated, when rows are written, then ordering is deterministic across repeated exports with the same filters and source events, and no raw EventStore envelopes, copied sibling-owned state, rates, invoice totals, taxes, payroll values, or revenue-recognition data are included.
6. Given export UI is displayed, when users review scope and confirm, then a FluentDialog summarizes filters, output scope, included evidence fields, freshness state, and what Timesheets will not calculate, and copy avoids invoice, payroll, rate, tax, and revenue-recognition ownership language.

## Tasks / Subtasks

- [x] Add finance export contracts and read/output models under the existing export capability boundary (AC: 1, 2, 3, 4, 5)
  - [x] Add export request/command/query contracts under `src/Hexalith.Timesheets.Contracts/Commands/Exports` and/or `src/Hexalith.Timesheets.Contracts/Queries/Exports`, reusing `QueryApprovedTimeLedger` filter semantics rather than inventing a second filter vocabulary.
  - [x] Add export preview/output models under `src/Hexalith.Timesheets.Contracts/Models` for requested filters, output scope, row count, format/version, projection freshness, export readiness detail, and deterministic generated rows.
  - [x] Include stable IDs only: Time Entry ID, Contributor Party ID, Target Reference, Service Date, Duration Minutes, Activity Type ID/scope, Billable Flag, approval metadata, row state, correction/approved-correction lineage, Event Lineage, and comment fields only when export policy allows.
  - [x] Add a v1 export format indicator. Use CSV as the v1 UI affordance unless architecture is explicitly changed, but keep contracts format/versioned so Story 4.6 can verify output deterministically.
  - [x] Do not accept caller-supplied `TenantId`, `UserId`, roles, claims, correlation authority, message IDs, server-side freshness overrides, display labels, rate values, invoice IDs, payroll fields, tax fields, or revenue fields in public export contracts.
- [x] Implement export orchestration by extending the existing approved-ledger server path (AC: 1, 2, 3, 4, 5)
  - [x] Add `src/Hexalith.Timesheets.Server/Exports/*` service(s) that call `ApprovedTimeLedgerQueryService` or its projection reader after `TimesheetsOperation.Export` authorization succeeds.
  - [x] Preserve tenant-first authorization before projection lookup, then per-row authorization/hydration behavior already implemented by `ApprovedTimeLedgerQueryService`; denied rows must not be exported or hydrated.
  - [x] Require `ApprovedTimeLedgerReadModel.CanUseForExport == true`, `ProjectionFreshnessMetadata.State == Fresh`, at least one disclosed row, and billable evidence unless a future policy explicitly allows another state.
  - [x] Reuse `TimesheetsEvidencePolicyEvaluator` and `TimesheetsEvidencePolicyOptions`; fail closed when legal-hold, tenant retention override, or comment sensitivity policy is unresolved for export.
  - [x] Apply comment export policy from `TimeEntryComment.Policy.ExportInclusion` / effective policy. Projection comments currently omit sensitive comments; export must not reintroduce excluded comment text.
  - [x] Add audit metadata to the result shape: requester Party reference, filter snapshot, requested/generated UTC timestamp, correlation ID from `TimesheetsRequestContext`, output scope, format/version, freshness state, row count, and blocked reason where applicable.
  - [x] Do not create an authoritative export database, direct SQL/Redis/Dapr state store, mutable local files, or broker-backed CRUD path. Story 4.6 will harden persistent audit evidence; Story 4.5 should expose the metadata needed for that audit.
- [x] Generate deterministic export output from approved ledger rows (AC: 2, 3, 5)
  - [x] Generate rows from `ApprovedTimeLedgerRowReadModel`, not raw Time Entry aggregate state or EventStore envelopes.
  - [x] Use the ledger's existing deterministic ordering rules as the default: primary query sort, then `TimeEntryId`, then row state. For CSV, keep a stable header order and invariant formatting.
  - [x] Preserve current vs superseded row behavior according to `CurrentRowsOnly` and `IncludeSupersededRows`; include enough correction and event lineage for downstream reconciliation.
  - [x] Include AI effort metrics only as evidence fields if already present on the ledger row, with explicit units and availability; never convert tokens/runtime into human hours or financial values.
  - [x] Keep display hydration labels out of the export output unless a later explicit export policy allows display labels. Stable references are the reconciliation contract.
  - [x] Do not include rates, invoice totals, payroll values, tax values, revenue-recognition fields, Project hierarchy/lifecycle data, Work lifecycle/planned effort data, Party personal data, raw claims, command bodies, comments excluded by policy, or diagnostics.
- [x] Publish FrontComposer metadata for export preview and confirmation (AC: 1, 4, 6)
  - [x] Extend `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` so `timesheets.projection.approved-time-ledger` exposes a concrete export action such as `Timesheets.GenerateApprovedLedgerExport` only when export readiness can be reviewed.
  - [x] Add a focused export review descriptor if needed, using `TimesheetsCompositionPattern.FrontComposerGeneratedForm` or the established metadata pattern for generated command forms; do not add a Timesheets-specific UI shell or raw Blazor UI project in this story.
  - [x] Metadata must describe FilterBar fields, selected filters, output scope, included evidence fields, row count, freshness state, export readiness, comment policy, and audit metadata.
  - [x] Represent the Export Review Dialog requirements in metadata/copy: one bounded `FluentDialog`, persistent `FluentMessageBar` for stale/no-results/policy blocks, and `FluentToast` only after a successful export request.
  - [x] Use factual command labels such as `Export approved ledger`; do not use `Create invoice`, `Run payroll`, rate-card, tax, or revenue-recognition language.
- [x] Add focused contract, server, metadata, projection/export, and integration tests (AC: 1-6)
  - [x] Contracts tests: export request/output round-trip; format/version, filters, audit metadata, lineage, comment policy, freshness/readiness, deterministic row fields; JSON omits server-controlled authority and finance ownership fields.
  - [x] Server tests: tenant/export denial prevents ledger lookup; policy gaps fail closed; non-fresh or empty ledger blocks output; insufficient-role rows are filtered before output; unsafe row denials fail closed; comments excluded by policy remain absent.
  - [x] Output/golden tests: CSV/header/order/escaping/date and duration formatting are deterministic across repeated exports with the same source events and filters, including corrected/superseded rows.
  - [x] Metadata tests: approved-ledger/export descriptors include export action, review dialog fields, export readiness, freshness, comment policy, audit metadata, and no EventStore stream, invoice, payroll, rate, tax, or revenue-recognition ownership language.
  - [x] Integration tests: seeded approved billable Project and Work rows export through the service with authorization, freshness, paging or full-scope behavior, correction lineage, no empty file on no results, and drill-in compatibility with existing ledger/evidence reads.
  - [x] Privacy/security tests: export output, metadata, logs, diagnostics, and blocked results do not disclose denied Party/Project/Work/Time Entry details, copied sibling display data, raw EventStore envelopes, comments excluded by policy, AI prompts/responses, token secrets, command bodies, rates, invoices, payroll, taxes, or revenue data.
- [x] Verify affected build and test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts, Server, Projections if output uses projection helpers, Integration, and Architecture tests.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `.agents/skills/bmad-create-story/SKILL.md`, `discover-inputs.md`, `template.md`, and `checklist.md` completely.
- Loaded `Hexalith.AI.Tools/hexalith-llm-instructions.md`.
- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-5-generate-finance-export-from-approved-ledger` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md`, especially Epic 4 and Story 4.5 plus FR18, FR19, NFR5, UX-DR29, and UX-DR30 traceability.
- Loaded `_bmad-output/planning-artifacts/architecture.md` sections covering query APIs, exports, FrontComposer surfaces, project structure, data exchange formats, validation, and test organization.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR18, FR19, SM-5, export accountability, non-goals, candidate `ApprovedTimeExported`, and export-format open question.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `DESIGN.md`, and `.decision-log.md`, especially Approved-Time Ledger, Export Review Dialog, CSV v1 assumption, no-results behavior, and anti-invoice/payroll/rate/tax/revenue copy rules.
- Loaded persistent facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/4-4-surface-ai-effort-reporting.md`.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md`.
- `{ux_content}` loaded from 3 files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/.decision-log.md`.
- Persistent facts loaded from 6 sibling `project-context.md` files.

### Epic And Story Context

- Epic 4 covers operational time queries, Project/Work actual-time reports, AI effort reporting, Approved-Time Ledger, finance export, export audit, and dashboard composition.
- Story 4.5 implements FR18 and FR19 by generating approved billable evidence from the already-built Approved-Time Ledger. It depends on Stories 4.1 and 4.2 for query/freshness/ledger behavior and on Story 4.4 for preserving AI metrics when present.
- Story 4.6 follows this story and owns deeper audit verification, export evidence hardening, and tenant-time-zone boundary golden files. Story 4.5 must still include requester, filters, timestamp, correlation ID, output scope, format/version, and freshness in the result so Story 4.6 has a stable surface to verify.
- The UX assumption accepted on 2026-06-18 is that CSV export is sufficient as a v1 UI affordance unless architecture selects API/webhook export at launch. No later architecture selection overrode that assumption.
- Exports are evidence exports, not finance calculations. Timesheets owns the billable flag and approved-time ledger; rates, invoice generation, payroll, taxes, and revenue recognition remain outside the module.

### Current Code State To Extend

- `ApprovedTimeLedgerReadModel` already exists with `Items`, `NextCursor`, `ProjectionFreshness`, `CanUseForExport`, and `ExportReadinessDetail`.
- `ApprovedTimeLedgerRowReadModel` already carries the export-relevant evidence base: `TimeEntryId`, `Contributor`, `Target`, `ServiceDate`, `DurationMinutes`, `ActivityTypeId`, `ActivityTypeScope`, `BillableState`, `ContributorCategory`, `ApprovalDecision`, `LockEvidence`, row state, freshness, AI metrics, approved correction, rejected correction, event lineage, display hydration, comment projection state, and comment.
- `ApprovedTimeLedgerRowReadModel.CurrentFromEvidence` and `SupersededFromApprovedCorrection` sanitize comments through projection policy. Do not bypass this by exporting raw `TimeEntryEvidenceReadModel` comments.
- `QueryApprovedTimeLedger` already supports Project, Work, Contributor, Activity Type, tenant-local period key, service date range, Billable State, current-only/include-superseded behavior, sorting, page size, and cursor. Export should reuse these filters.
- `ApprovedTimeLedgerProjection` projects from `TimeEntryEvidenceProjection`, filters by ledger query, sorts deterministically, pages with opaque cursors, maps freshness through `ProjectionFreshnessMetadataMapper`, and blocks export readiness when checkpoint freshness cannot serve reads.
- `ApprovedTimeLedgerQueryService` performs tenant-first `TimesheetsOperation.ProjectionRead` authorization, projection lookup, per-row authorization, insufficient-role filtering, non-filterable fail-closed denials, and display hydration only after row authorization. Reuse or wrap this behavior; do not duplicate it loosely.
- `TimesheetsOperation.Export` already exists and `TimesheetsEvidencePolicyEvaluator` treats export as trust-bearing. Export services must authorize with `TimesheetsOperation.Export`, not only `ProjectionRead`.
- `TimesheetsEvidencePolicyOptions` and `TimesheetsEvidencePolicyDescriptor` already model unresolved legal-hold, tenant retention override, comment sensitivity, export comment allowance, and export-record retention gaps. Use these instead of creating a separate export policy shape.
- `ApprovalAuthorityAction.ApprovedTimeExportEligibility` and `ApprovalAuthoritySource.FinanceReviewer` already exist in `TimesheetsEnums.cs`. Export eligibility should align with this authority vocabulary where approval-authority evidence is surfaced.
- `TimesheetsMetadataCatalog` currently marks approved-ledger export readiness as "Preview only; export generation is implemented by later finance-export workflows." Story 4.5 should replace that placeholder with concrete export action/review metadata.
- No `Contracts/Commands/Exports`, `Contracts/Queries/Exports`, `Server/Exports`, `ApprovedTimeExported`, or export golden tests exist yet.

### Architecture Constraints

- EventStore remains the only authoritative persistence boundary. Do not add SQL, Redis, Dapr state, local files, mutable caches, projection mutation, or broker-backed CRUD as export authority.
- Exports are generated from approved-ledger projection rows plus policy/authorization/audit metadata. The approved ledger remains rebuildable and non-authoritative for writes.
- Public contracts hide EventStore envelopes, aggregate internals, projection rebuild mechanics, raw stream names, sequence internals, and debug payloads.
- Tenant/resource gates run before projection reads, export preview, export generation, or UI action disclosure. JWT tenant/user claims are request evidence, not authority.
- Sibling modules own Project, Work, Party, and Tenant data. Export rows use stable references only and must not copy Project hierarchy, Work lifecycle/planned effort, Party personal data, or tenant membership data.
- Dates use ISO-8601; audit timestamps use UTC instants; period filters use tenant-local period keys/date ranges already established by prior stories.
- Contract evolution must be additive and serialization-tolerant. Do not rename existing ledger, Time Entry, approval, correction, AI metrics, or policy fields.
- Logs/traces must use structured metadata and correlation IDs without comments, event payloads, command bodies, personal data, token values, secrets, full export rows, raw claims, or protected identifiers.
- Use the existing pinned stack: .NET 10, `.slnx`, Central Package Management, nullable, implicit usings, warnings as errors, xUnit v3, Shouldly, NSubstitute, FrontComposer metadata, and Blazor Fluent UI V5 semantics. No dependency upgrades or new package families are needed.

### UX And UI Constraints

- Internal export starts from `Approved-Time Ledger` in `FrontComposerShell`; do not add a parallel internal navigation shell.
- Export review is a focused decision and must use a generated/metadata-backed command form and `FluentDialog` semantics, with only one dialog layer.
- Ledger and export surfaces must show projection freshness. Stale, rebuilding, degraded, or unavailable projections must use persistent explanation and block export unless architecture explicitly changes the policy.
- No-result export state must show the filters and persistent text such as `No approved entries match these filters.` It must not produce an empty file as if export succeeded.
- Use dense filter-heavy operational UI: FilterBar above the grid/review surface, `FluentDataGrid` semantics for ledger rows, status badges with text, and keyboard-reachable actions.
- Use `FluentMessageBar` for stale projection, permission denied, comments policy, export scope warning, or policy block. Use `FluentToast` only for transient feedback after a successful export request.
- Export Review Dialog must summarize filters, output scope, included evidence fields, freshness state, comment policy, and what Timesheets will not calculate.
- Approved UX copy: `Export approved ledger`. Rejected copy examples: `Create invoice`, payroll, rate-card, tax, revenue-recognition, or language implying Timesheets owns downstream finance decisions.

### Previous Story Intelligence

- Story 4.4 extended actual-time reports with AI metrics and explicitly kept human/external minutes, AI wall-clock runtime, model/tool runtime, AI billable effort, and token counts as separate units. Export must preserve those units if AI metrics are included and must never convert them to finance values.
- Story 4.4 added AI metric fields to `ApprovedTimeLedgerRowReadModel`; Story 4.5 should not remove or flatten those fields.
- Story 4.4 review fixed order-dependent AI token availability aggregation. Any export row generation over AI metrics must be order-deterministic and conservative about unavailable/not-reported values.
- Story 4.3 established actual-time report services as disclosure boundaries with tenant-first authorization and per-row authorization. Story 4.5 should follow the existing `ApprovedTimeLedgerQueryService` disclosure boundary rather than creating a shortcut.
- Story 4.2 established `ApprovedTimeLedgerProjection` and `ProjectionFreshnessMetadataMapper`; reuse the ledger projection and freshness mapper rather than creating export-specific freshness logic.
- Story 4.1 established operational query contracts, degraded projection freshness, result-level authorization, and fail-closed list readers. Export must keep the same non-disclosure posture.
- Build/test lanes after Stories 4.1-4.4 used direct xUnit executable fallback where VSTest socket permissions were blocked.

### Git Intelligence Summary

- Last 5 commit titles:
  - `13b4435 feat(story-4.4): Surface AI Effort Reporting`
  - `d0bc176 feat(story-4.3): Produce Project and Work Actual-Time Reports`
  - `f17c2bb feat(story-4.2): Project Approved Time Ledger from Domain Events`
  - `da204ec feat(story-4.1): Query Time Entries from Rebuildable Read Models`
  - `735f7e1 docs(retro): Capture Epic 3 Retrospective`
- Story 4.4 changed ledger/report contracts, metadata, projection, and tests. Export work should expect current ledger rows to include optional AI metrics and to be covered by privacy/fitness tests.
- Story 4.2 and 4.4 both rely on deterministic projection ordering and replay safety. Export generation must add golden-file style tests instead of trusting incidental row order.
- Current working tree before creating this story already had unrelated changes for `Hexalith.FrontComposer`, `Hexalith.Tenants`, and `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, file-scoped namespaces, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a Timesheets UI project, or a parallel shell for this story.
- Use `ConfigureAwait(false)` on every awaited production call.
- No external latest-version research is needed for this story because no new framework, library, external API, or dependency selection is required; the architecture and repo pin the implementation stack.

### Project Structure Notes

- Expected contract/model files to add or update:
  - `src/Hexalith.Timesheets.Contracts/Commands/Exports/GenerateApprovedTimeExport.cs` or similarly named command.
  - `src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs` if preview is separated from generation.
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportAuditMetadata.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportScope.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportFormat.cs` or enum in `ValueObjects/TimesheetsEnums.cs`
  - `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- Expected server files to add or update:
  - `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`
  - `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportResult.cs`
  - Optional `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportCsvWriter.cs` if CSV formatting is not kept inside the service.
  - `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- Expected projection/ledger files to update only if export needs additional helper data:
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`
  - `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`
- Expected tests:
  - `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`
  - `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeLedgerContractTests.cs`
  - `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs`
  - `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeLedgerAuthorizationTests.cs`
  - `tests/Hexalith.Timesheets.Projections.Tests/ApprovedTimeLedgerProjectionTests.cs` if ledger query/output ordering changes.
  - `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/` or equivalent golden fixtures if the repo's test project accepts fixture folders.
  - `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` or nearby fitness tests for export privacy and finance-boundary language.
- Do not use `_bmad-output/`, `docs/`, or submodules as implementation scratch space.

### Testing Standards

- Every claimed export field, filter, readiness state, freshness state, authorization decision, policy decision, comment inclusion/exclusion, lineage behavior, deterministic ordering rule, and no-finance-boundary rule needs executable proof.
- Test tenant/export denial before ledger lookup and row-level denial before hydration/output.
- Test Project-target and Work-target export rows independently; Project rows authorize Project only and Work rows authorize Work only.
- Test filters for Project, Work, Contributor, Activity Type, tenant-local period, date range, Billable State, current-only rows, and included superseded rows.
- Test no matching rows and non-fresh projection states block output and return persistent, user-actionable explanation.
- Test CSV output with stable header order, ISO dates, invariant numeric formatting, escaping of commas/quotes/newlines if comments are allowed, and deterministic row order across repeated runs.
- Test correction lineage for approved-entry correction and rejected-entry correction later approved; current and superseded rows must reconcile.
- Test comments with export allowed, excluded, redacted, policy required, and unresolved policy states. Excluded comments must not appear in output or diagnostics.
- Test AI metrics presence on ledger rows does not collapse token/runtime values into duration or finance fields.
- Test metadata vocabulary contains text-bearing export readiness/freshness/comment policy/status states and no EventStore stream, invoice, payroll, rate, tax, revenue-recognition, Project ownership, Work ownership, or Party personal-data language.
- Test logs/diagnostics do not include full export rows, comments, personal data, raw claims, command bodies, token values, raw EventStore envelopes, or denied row labels.

### Anti-Patterns To Prevent

- Do not generate export output from raw EventStore envelopes, aggregate state, or operational report rows when approved ledger rows already exist.
- Do not skip `TimesheetsOperation.Export` authorization because the approved ledger query already performed `ProjectionRead` authorization.
- Do not treat stale, rebuilding, degraded, or unavailable projections as exportable evidence.
- Do not produce an empty CSV/file for no-results filters as if it were successful finance evidence.
- Do not include display labels, copied Project hierarchy, copied Work lifecycle/planned effort, copied Party personal data, tenant membership data, raw claims, command bodies, raw EventStore sequence/envelope fields, or diagnostics in the export.
- Do not add rates, invoice totals, invoice IDs, payroll values, tax values, revenue-recognition fields, currency math, or downstream finance workflow state.
- Do not persist export records through direct SQL, Redis, Dapr state, local files, or mutable projection writes. Use EventStore-backed events if persistence is added; otherwise return audit metadata for Story 4.6 hardening.
- Do not add a new UI shell, raw HTML/CSS UI, JavaScript export widget, new package family, `.sln`, inline package versions, or submodule changes.
- Do not broaden comment export beyond explicit policy. Comments are sensitive by default.
- Do not mark Story 4.6 audit verification complete in this story; only create the generation/audit metadata surface it will verify.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-5-Generate-Finance-Export-from-Approved-Ledger`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR18`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR19`]
- [Source: `_bmad-output/planning-artifacts/epics.md#NFR5`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR29`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR30`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-18-Maintain-the-Approved-Time-Ledger`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-19-Export-approved-billable-evidence`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Data-Governance-and-Audit-Requirements`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Success-Metrics`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-for-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Query-API-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-5-Marc-exports-approved-billable-evidence`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Component-Usage`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/.decision-log.md#2026-06-18`]
- [Source: `_bmad-output/implementation-artifacts/4-4-surface-ai-effort-reporting.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyEvaluator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyOptions.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeLedgerContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/ApprovedTimeLedgerProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeLedgerAuthorizationTests.cs`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` via VSTest was attempted for Contracts and Server and aborted with `System.Net.Sockets.SocketException (13): Permission denied`; used the README direct xUnit v3 executable fallback.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 82 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed: 366 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed: 77 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 49 total, 0 failed, 2 skipped for pre-existing infrastructure/performance lanes.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed: 20 total, 0 failed.

### Completion Notes List

- Added CSV v1 approved-time export command/query contracts and read models using existing `QueryApprovedTimeLedger` filters and server-owned audit metadata.
- Implemented `ApprovedTimeExportService` with `TimesheetsOperation.Export` authorization before ledger lookup, reuse of `ApprovedTimeLedgerQueryService`, freshness/readiness/billable gates, policy-aware comment output, deterministic CSV rows, and blocked results with audit metadata but no file.
- Published FrontComposer metadata for `Timesheets.GenerateApprovedLedgerExport`, export review dialog fields, persistent block message metadata, success toast metadata, export readiness/status vocabularies, and no new UI shell.
- Added contract, server, metadata, integration, projection, architecture, and full solution validation coverage for the export surface.

### File List

- `_bmad-output/implementation-artifacts/4-5-generate-finance-export-from-approved-ledger.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/Exports/GenerateApprovedTimeExport.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportAuditMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportScope.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportCsvWriter.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportResult.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeLedgerContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs`

### Change Log

- 2026-06-19: Implemented Story 4.5 approved-ledger finance export contracts, orchestration, deterministic CSV generation, FrontComposer metadata, tests, and validation. Status set to review.
- 2026-06-19: Senior Developer Review (AI) — auto-fixed export full-scope paging (HIGH) and descending-sort ordering (MEDIUM); added regression tests. All affected lanes pass. Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-19
**Outcome:** Changes Requested → auto-fixed → Approved (0 critical issues remaining)

### Findings and resolutions

- **[HIGH — fixed] Export silently truncated to a single page.** `ApprovedTimeExportService.GenerateAsync`
  issued one `ApprovedTimeLedgerQueryService.QueryAsync` call and exported only `ledger.Items`, ignoring
  `NextCursor`. With `QueryApprovedTimeLedger.PageSize` defaulting to 50, any filter matching more than 50
  approved billable rows produced an incomplete finance-evidence file, and `Scope.RowCount` / `Audit.RowCount`
  understated the true total with no truncation signal. This contradicted AC2 (export includes all matching
  rows), AC5 (deterministic *and complete* output), AC4 and the "no misleading finance evidence" anti-pattern,
  and the task "paging or full-scope behavior" was checked but unimplemented. **Fix:** added
  `LoadDisclosedLedgerAsync`, which loops the ledger cursor until `NextCursor` is empty, accumulates every
  disclosed row, fails closed on any non-fresh page, and aggregates freshness/readiness before generation.
  Per-page tenant + per-row authorization is preserved. Covered by
  `Export_accumulates_all_pages_when_ledger_is_cursor_paged`.
- **[MEDIUM — fixed] Descending sort produced wrong order for variable-length keys.** The previous
  `InvertOrdinal` helper subtracted each char from `char.MaxValue` without length normalization, so
  prefix-related keys sorted incorrectly under descending order (e.g. `time-entry-2` before `time-entry-20`).
  Output stayed deterministic but the ordering was wrong. **Fix:** replaced the string-inversion hack with
  `ApplyExportOrdering` using `OrderByDescending`/`OrderBy` on the primary key plus ascending `TimeEntryId`
  then `RowState` tie-breakers. Covered by
  `Export_orders_descending_time_entry_ids_with_prefix_relationship`.

### Low-priority follow-ups (non-blocking, deferred to Story 4.6 export hardening)

- **[LOW] CSV formula injection.** The free-text `comment` column is not guarded against leading
  `=`/`+`/`-`/`@` characters, which spreadsheet apps may interpret as formulas. Intentionally *not* auto-fixed
  here because mutating exported comment text would corrupt the reconciliation evidence; the right mitigation
  (consumer-side neutralization or an explicit, versioned escaping policy) belongs to Story 4.6, which owns
  export evidence hardening and golden files.
- **[LOW] Row separator is `\n`, not RFC 4180 `\r\n`.** Deterministic and acceptable for v1; flagged so
  Story 4.6 golden fixtures pin the intended line ending.
- **[LOW] `PreviewApprovedTimeExport` contract has no server handler.** Preview/readiness is served today by
  the existing ledger query (`CanUseForExport` + `ExportReadinessDetail` + freshness). The contract is
  forward-looking for Story 4.6 and is covered by a serialization round-trip test; left in place by design.

### Verification

- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` — 0 warnings, 0 errors.
- Direct xUnit v3 executables (VSTest socket blocked, per README fallback): Contracts 82/0, Server 370/0,
  Projections 77/0, Integration 50/0 (2 pre-existing skips), Architecture 20/0.
