---
baseline_commit: 1a4ad11
---

# Story 4.6: Verify Finance Export Evidence and Audit Trail

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a finance or audit reviewer,
I want export evidence and audit records to be deterministic and verifiable,
so that downstream reconciliation can trust the exported approved-time evidence.

## Acceptance Criteria

1. Given a finance export is requested, when the export is accepted, then requester, filters, timestamp, correlation ID, freshness state, output scope, and export format/version are recorded as audit evidence, and the audit evidence does not include secrets, raw tokens, command bodies, or copied sibling-owned state.
2. Given the export is generated, when contract and golden-file tests run, then output schema, stable field names, time-zone handling, correction lineage, billable filtering, and deterministic regeneration are verified, and no rates, invoice totals, taxes, payroll values, revenue-recognition data, raw EventStore envelopes, or copied sibling-owned state are included.
3. Given comments are included or excluded by export policy, when export redaction checks run, then comment visibility follows the evidence retention and comment sensitivity policy, and unauthorized comment fields are absent from export output and diagnostics.
4. Given export filters include tenant-local dates or period boundaries, when time-zone boundary tests run, then UTC audit instants and tenant-local period keys are handled consistently, and edge cases around period boundaries are covered by golden files.

## Tasks / Subtasks

- [x] Add EventStore-backed export audit evidence contracts without creating export CRUD storage (AC: 1, 2)
  - [x] Add an export audit event contract under `src/Hexalith.Timesheets.Contracts/Events/Exports/`, likely `ApprovedTimeExported`, matching existing event style: `public sealed record`, immutable positional data, no infrastructure references.
  - [x] Include only safe audit facts: requester `PartyReference`, tenant `TenantReference`, `QueryApprovedTimeLedger` filter snapshot, requested/generated UTC instants, correlation ID, `ApprovedTimeExportScope`, `ApprovedTimeExportFormat`, format version, `ProjectionFreshnessState`, row count, and an output fingerprint or deterministic content hash if needed for reconciliation.
  - [x] Exclude raw CSV content, row payload dumps, comments, token values, raw claims, command bodies, EventStore envelopes, Project/Work/Party display labels, rates, invoices, payroll, taxes, and revenue fields from the audit event.
  - [x] Preserve additive/serialization-tolerant contract evolution; do not rename or remove any existing Story 4.5 export fields.
- [x] Wire accepted export audit recording through the existing export service boundary (AC: 1)
  - [x] Extend `ApprovedTimeExportService` or add a narrow collaborator under `src/Hexalith.Timesheets.Server/Exports/` so successful accepted exports produce audit evidence through the EventStore/domain-event path.
  - [x] Keep `TimesheetsOperation.Export` authorization before ledger lookup and before audit recording; do not rely only on `ProjectionRead`.
  - [x] Record audit evidence only for accepted/generated exports. Blocked, denied, stale, non-billable, empty, unsupported-format, or policy-gap attempts may return safe result metadata but must not create misleading `ApprovedTimeExported` evidence.
  - [x] Do not persist export records with direct SQL, Redis, Dapr state, local files, mutable projection writes, or broker-backed CRUD. EventStore remains the authoritative persistence path.
- [x] Harden deterministic CSV v1 output and golden fixtures (AC: 2, 4)
  - [x] Add golden-file fixtures under the existing integration-test structure, for example `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/`.
  - [x] Pin the CSV v1 header, stable field order, row ordering, invariant date/time/number formatting, correction lineage formatting, AI metric availability fields, billable filtering, and deterministic regeneration across repeated exports.
  - [x] Decide and pin the line-ending policy for CSV v1. Story 4.5 currently writes `\n`; either keep it as the versioned v1 contract or deliberately change to a documented versioned policy with updated golden files.
  - [x] Cover Project-target and Work-target rows independently, current and superseded rows, approved-entry correction lineage, rejected-entry correction later approved, and multi-page export accumulation.
  - [x] Cover tenant-local period/date filters including period-boundary and DST-adjacent cases using the Story 2.7 tenant-time-zone policy: UTC audit instants remain separate from tenant-local period keys.
- [x] Add export redaction, CSV safety, and diagnostics proof (AC: 2, 3)
  - [x] Verify comment inclusion follows `TimeEntryComment.Policy.ExportInclusion` and `TimesheetsEvidencePolicyOptions`; comments excluded by policy must not appear in CSV, audit events, diagnostics, blocked results, or logs.
  - [x] Address the Story 4.5 review follow-up on CSV formula injection. If spreadsheet neutralization would mutate evidence, document and test the chosen policy explicitly, such as consumer-side neutralization guidance or a versioned escaping strategy.
  - [x] Test CSV quoting/escaping for commas, quotes, CR/LF, Unicode-safe text, empty fields, and policy-allowed comments without leaking excluded comments.
  - [x] Extend diagnostics/privacy fitness tests so export output, audit metadata, metadata descriptors, logs, and test fixtures contain no forbidden finance ownership or sensitive-data vocabulary beyond explicit negative-test strings.
- [x] Strengthen export contract and metadata tests (AC: 1, 2, 3)
  - [x] Update `ApprovedTimeExportContractTests` to round-trip the new audit event and assert server-controlled authority is not accepted from caller input.
  - [x] Verify `TimesheetsMetadataCatalog` surfaces audit metadata, format/version, freshness, output scope, comment policy, and persistent block state without invoice, payroll, rate, tax, or revenue-recognition language.
  - [x] Keep `PreviewApprovedTimeExport` honest: either implement the server preview path or document/test that preview readiness is served by the approved-ledger query until a later story.
  - [x] Add contract tests that unsupported export format/version and non-billable requests fail closed without audit-event creation.
- [x] Verify affected build and test lanes (AC: 1-4)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts, Server, Projections if ledger/export helpers change, Integration, and Architecture tests.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `.agents/skills/bmad-create-story/SKILL.md`, `discover-inputs.md`, `template.md`, and `checklist.md` completely.
- Loaded `Hexalith.AI.Tools/hexalith-llm-instructions.md`.
- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-6-verify-finance-export-evidence-and-audit-trail` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md`, especially Epic 4, Story 4.6, FR18, FR19, NFR5, NFR14, NFR15, UX-DR29, and UX-DR30.
- Loaded `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, export consistency contracts, data exchange formats, project structure, query/API patterns, frontend rules, and test organization.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `addendum.md`, and `.decision-log.md`, especially FR18, FR19, export accountability, retention/comment policy, tenant time-zone policy, and candidate `ApprovedTimeExported`.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `DESIGN.md`, and `.decision-log.md`, especially Approved-Time Ledger, Export Review Dialog, CSV v1 assumption, no-results behavior, and anti-invoice/payroll/rate/tax/revenue copy rules.
- Loaded persistent facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/4-5-generate-finance-export-from-approved-ledger.md`.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 3 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `addendum.md`, and `.decision-log.md`.
- `{ux_content}` loaded from 3 sharded files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `DESIGN.md`, and `.decision-log.md`.
- Persistent facts loaded from 6 sibling `project-context.md` files.

### Epic And Story Context

- Epic 4 covers operational time queries, Project/Work actual-time reports, AI effort reporting, Approved-Time Ledger, finance export, export audit, and dashboard composition.
- Story 4.6 owns NFR5 export auditability and hardens Story 4.5's approved-ledger export into deterministic, verifiable evidence.
- Story 4.6 is also the explicit coverage point for NFR15 tenant time-zone/period policy in exports: export filters using tenant-local dates or period keys must be proven against UTC audit instants and boundary cases.
- Story 4.6 must not broaden Timesheets into invoicing, payroll, rate-card, tax, or revenue-recognition ownership. Exports remain evidence files/contracts only.
- The PRD candidate event catalog names `ApprovedTimeExported`; use this as the likely audit event name unless local naming review finds a better Timesheets-domain name.

### Current Code State To Extend

- Story 4.5 added `GenerateApprovedTimeExport`, `PreviewApprovedTimeExport`, `ApprovedTimeExportReadModel`, `ApprovedTimeExportRowReadModel`, `ApprovedTimeExportScope`, `ApprovedTimeExportAuditMetadata`, `ApprovedTimeExportFormat`, and `ApprovedTimeExportReadinessState`.
- `ApprovedTimeExportService.GenerateAsync` currently authorizes `TimesheetsOperation.Export`, validates CSV v1 and billable filters, loads all approved-ledger pages through `ApprovedTimeLedgerQueryService`, blocks non-fresh/empty/non-billable results, and returns audit metadata in the read model.
- Story 4.5 audit metadata is a result shape only. It is not yet persisted as EventStore-backed audit evidence; that is the core gap for Story 4.6.
- There is no `src/Hexalith.Timesheets.Contracts/Events/Exports/` folder and no export aggregate/audit event contract yet.
- `ApprovedTimeExportCsvWriter` writes a deterministic CSV string with a fixed header and `\n` row separators. It quotes commas, quotes, CR, and LF, but does not neutralize spreadsheet formula-leading values.
- `PreviewApprovedTimeExport` exists as a contract but has no dedicated server handler. Today preview/readiness is served by the approved-ledger query and export generation result.
- `ApprovedTimeLedgerQueryService` is the disclosure boundary for ledger rows. It performs tenant-first `ProjectionRead` authorization, projection lookup, per-row authorization, insufficient-role filtering, and display hydration only after row authorization.
- `TimesheetsEvidencePolicyEvaluator` treats export as trust-bearing and fails closed when legal-hold, tenant retention override, or comment sensitivity policy is unresolved.
- `ApprovedTimeLedgerRowReadModel.CurrentFromEvidence` and `SupersededFromApprovedCorrection` sanitize comments through projection policy. Do not bypass this by exporting raw `TimeEntryEvidenceReadModel` comments.

### Files Being Modified Or Extended

- Likely NEW: `src/Hexalith.Timesheets.Contracts/Events/Exports/ApprovedTimeExported.cs`.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/Commands/Exports/GenerateApprovedTimeExport.cs` only if a request-level export audit identifier or format constraints must be added; do not accept tenant/user/correlation authority from callers.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportAuditMetadata.cs` if persistent audit metadata needs a content fingerprint or additional version field.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportReadModel.cs` if accepted audit event identity/fingerprint must be surfaced to consumers.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportRowReadModel.cs` only for additive fields required by golden-file verification; avoid duplicate ledger row semantics.
- Likely UPDATE: `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs` to emit/return persistent audit evidence after successful generation.
- Likely UPDATE: `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportCsvWriter.cs` to pin line endings and formula-safety policy if the chosen v1 evidence policy requires code changes.
- Likely UPDATE: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` if a new export audit recorder abstraction is introduced.
- Likely UPDATE: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` to keep audit/fingerprint/format/version metadata visible and no finance-ownership copy in place.
- Likely UPDATE: `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`, `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs`, `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`, and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`.
- Likely NEW: golden fixtures under `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/`.

For every UPDATE file above, preserve existing behavior:

- `ApprovedTimeExportService` must still export the full cursor-paged scope, not only the first ledger page.
- Export still fails closed on denied export authorization, policy gaps, unsupported format/version, non-billable filters, stale projection, no rows, non-billable rows, and unsafe per-row authorization.
- Insufficient-role rows may be filtered before output; unsafe row denials must fail closed.
- `ApprovedTimeExportCsvWriter.Header` order is the current CSV v1 contract unless this story deliberately versions the change.
- `ApprovedTimeLedgerQueryService` remains the row disclosure/hydration boundary; do not re-query raw projections or raw EventStore state from export code.

### Architecture Constraints

- EventStore remains the only authoritative persistence boundary. Export audit records must be events/domain evidence, not mutable storage.
- Projections are rebuildable and non-authoritative for writes; do not treat the approved ledger as a write authority.
- Export output is generated from approved-ledger rows after export authorization and disclosure filtering, not from raw EventStore envelopes, aggregate state, operational report rows, or display-hydrated sibling labels.
- Public contracts hide EventStore envelope mechanics, aggregate internals, projection rebuild mechanics, raw stream names, sequence internals, and debug payloads.
- Tenant/resource gates run before projection reads, export preview, export generation, audit recording, or UI action disclosure. JWT tenant/user claims are request evidence, not authority.
- Sibling modules own Project, Work, Party, and Tenant data. Export rows and audit metadata use stable references only and must not copy Project hierarchy, Work lifecycle/planned effort, Party personal data, or tenant membership data.
- Dates use ISO-8601; audit timestamps use UTC instants; period filters use tenant-local period keys/date ranges from the tenant-time-zone policy.
- Contract and event evolution must be additive and serialization-tolerant. Do not rename or remove existing ledger, Time Entry, approval, correction, AI metrics, policy, or export fields.
- Logs/traces must use structured metadata and correlation IDs without comments, event payloads, command bodies, personal data, token values, secrets, full export rows, raw claims, or protected identifiers.
- Use the existing pinned stack: .NET 10, `.slnx`, Central Package Management, nullable, implicit usings, warnings as errors, xUnit v3, Shouldly, NSubstitute, FrontComposer metadata, and Blazor Fluent UI V5 semantics. No dependency upgrades or new package families are needed.

### UX And UI Constraints

- Internal export starts from `Approved-Time Ledger` in `FrontComposerShell`; do not add a parallel internal navigation shell.
- Export review is a focused decision and must use a generated/metadata-backed command form and `FluentDialog` semantics, with only one dialog layer.
- Ledger and export surfaces must show projection freshness. Stale, rebuilding, degraded, or unavailable projections must use persistent explanation and block export unless architecture explicitly changes the policy.
- No-result export state must show the filters and persistent text such as `No approved entries match these filters.` It must not produce an empty file as if export succeeded.
- Use `FluentMessageBar` for stale projection, permission denied, comments policy, export scope warning, or policy block. Use `FluentToast` only for transient feedback after a successful export request.
- Export Review Dialog must summarize filters, output scope, included evidence fields, freshness state, comment policy, audit metadata, and what Timesheets will not calculate.
- Approved UX copy: `Export approved ledger`. Rejected copy examples: `Create invoice`, payroll, rate-card, tax, revenue-recognition, or language implying Timesheets owns downstream finance decisions.

### Previous Story Intelligence

- Story 4.5 implemented finance export generation from the Approved-Time Ledger and finished senior review as done.
- Story 4.5 review fixed a HIGH issue where export silently truncated to the first page. Do not regress the `LoadDisclosedLedgerAsync` full-scope paging behavior.
- Story 4.5 review fixed a MEDIUM issue where descending sort used a brittle string inversion helper. Preserve the explicit `OrderByDescending`/tie-breaker approach.
- Story 4.5 intentionally deferred three low-priority hardening items to Story 4.6:
  - CSV formula injection policy for free-text comments.
  - Row separator policy (`\n` today, not RFC 4180 `\r\n`).
  - `PreviewApprovedTimeExport` contract has no server handler; readiness currently comes from approved-ledger query output.
- Story 4.4 added AI metric fields to approved-ledger rows. Export hardening must keep AI runtime/token units separate and must never convert them into human duration or finance values.
- Stories 4.1-4.3 established tenant-first query authorization, per-row authorization, projection freshness, deterministic report/ledger ordering, and fail-closed disclosure boundaries. Export audit must build on those patterns.

### Git Intelligence Summary

- Last 5 commit titles:
  - `1a4ad11 feat(story-4.5): Generate Finance Export from Approved Ledger`
  - `13b4435 feat(story-4.4): Surface AI Effort Reporting`
  - `d0bc176 feat(story-4.3): Produce Project and Work Actual-Time Reports`
  - `f17c2bb feat(story-4.2): Project Approved Time Ledger from Domain Events`
  - `da204ec feat(story-4.1): Query Time Entries from Rebuildable Read Models`
- Story 4.5 changed export contracts, `TimesheetsMetadataCatalog`, `ApprovedTimeExportService`, `ApprovedTimeExportCsvWriter`, runtime registration, contract tests, server tests, and integration tests.
- The current working tree before creating this story already had unrelated modifications in `Hexalith.FrontComposer`, `Hexalith.Tenants`, and `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, file-scoped namespaces, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a Timesheets UI project, or a parallel shell for this story.
- Use `ConfigureAwait(false)` on every awaited production call.
- No external latest-version research is needed for this story because no new framework, library, external API, or dependency selection is required; the architecture and repo pin the implementation stack.

### Testing Standards

- Every claimed audit field, export field, filter, readiness state, freshness state, authorization decision, policy decision, comment inclusion/exclusion, lineage behavior, deterministic ordering rule, time-zone boundary, and no-finance-boundary rule needs executable proof.
- Test that denied, blocked, stale, no-results, unsupported-format, non-billable, unsafe-row, and policy-gap exports do not create accepted export audit evidence.
- Test Project-target and Work-target export rows independently; Project rows authorize Project only and Work rows authorize Work only.
- Test filters for Project, Work, Contributor, Activity Type, tenant-local period, date range, Billable State, current-only rows, and included superseded rows.
- Test DST/period-boundary golden fixtures using tenant-local period keys and UTC audit instants. Do not conflate local service dates with UTC timestamps.
- Test CSV output with stable header order, ISO dates, invariant numeric formatting, chosen line endings, quoting/escaping, and deterministic row order across repeated exports.
- Test correction lineage for approved-entry correction and rejected-entry correction later approved; current and superseded rows must reconcile.
- Test comments with export allowed, excluded, redacted, policy required, and unresolved policy states. Excluded comments must not appear in output, audit, logs, fixtures, or diagnostics.
- Test AI metrics presence on ledger rows does not collapse token/runtime values into duration or finance fields.
- Test metadata vocabulary contains text-bearing export readiness/freshness/comment policy/status states and no EventStore stream, invoice, payroll, rate, tax, revenue-recognition, Project ownership, Work ownership, or Party personal-data language.
- Test logs/diagnostics do not include full export rows, comments, personal data, raw claims, command bodies, token values, raw EventStore envelopes, or denied row labels.

### Anti-Patterns To Prevent

- Do not record export audit by writing mutable rows/files outside EventStore.
- Do not audit blocked/denied/no-results/stale export attempts as accepted exports.
- Do not generate export output from raw EventStore envelopes, aggregate state, or operational report rows when approved-ledger rows already exist.
- Do not skip `TimesheetsOperation.Export` authorization because the approved ledger query already performed `ProjectionRead` authorization.
- Do not treat stale, rebuilding, degraded, or unavailable projections as exportable evidence.
- Do not produce an empty CSV/file for no-results filters as if it were successful finance evidence.
- Do not include display labels, copied Project hierarchy, copied Work lifecycle/planned effort, copied Party personal data, tenant membership data, raw claims, command bodies, raw EventStore sequence/envelope fields, or diagnostics in the export or audit event.
- Do not add rates, invoice totals, invoice IDs, payroll values, tax values, revenue-recognition fields, currency math, or downstream finance workflow state.
- Do not broaden comment export beyond explicit policy. Comments are sensitive by default.
- Do not silently change CSV field order, line endings, formula handling, or timestamp formatting without versioned tests and golden fixtures.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-6-Verify-Finance-Export-Evidence-and-Audit-Trail`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR18`]
- [Source: `_bmad-output/planning-artifacts/epics.md#FR19`]
- [Source: `_bmad-output/planning-artifacts/epics.md#NFR5`]
- [Source: `_bmad-output/planning-artifacts/epics.md#NFR14`]
- [Source: `_bmad-output/planning-artifacts/epics.md#NFR15`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR29`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-DR30`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-18-Maintain-the-Approved-Time-Ledger`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-19-Export-approved-billable-evidence`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Data-Governance-and-Audit-Requirements`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Cross-Cutting-NFRs`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-for-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-5-Marc-exports-approved-billable-evidence`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Component-Usage`]
- [Source: `_bmad-output/implementation-artifacts/4-5-generate-finance-export-from-approved-ledger.md#Senior-Developer-Review-AI`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/Exports/GenerateApprovedTimeExport.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportAuditMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportRowReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportCsvWriter.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyEvaluator.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` via VSTest was attempted for Contracts/Server/Integration and blocked by local socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`; switched to README direct xUnit v3 executable fallback.
- Required restore passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
- Required build passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
- Direct xUnit v3 fallback passed after build: ArchitectureTests 21/21, Contracts.Tests 83/83, Server.Tests 371/371, Projections.Tests 77/77, IntegrationTests 49/51 passed with 2 expected infrastructure/performance skips.

### Completion Notes List

- Added `ApprovedTimeExported` as the accepted-export audit event carrying safe references, filter snapshot, UTC request/generation instants, correlation ID, output scope, CSV format/version, projection freshness, row count, and SHA-256 output fingerprint.
- Added a domain-event audit recorder seam and wired `ApprovedTimeExportService` so only generated accepted exports produce audit evidence; denied, blocked, stale, empty, unsupported-format, non-billable, and unsafe-row paths leave `AuditResult` null.
- Kept CSV v1 line endings as `\n`, added spreadsheet formula neutralization for exported text fields, and pinned the policy with server and golden-file tests.
- Extended contract, server, integration, and architecture/privacy tests for audit-event safety, deterministic regeneration, comment redaction, tenant-local period/date filters, correction lineage, AI metric fields, metadata vocabulary, and no finance-ownership leakage.

### File List

- `src/Hexalith.Timesheets.Contracts/Events/Exports/ApprovedTimeExported.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportAuditMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportCsvWriter.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportResult.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`
- `src/Hexalith.Timesheets.Server/Exports/DomainEventApprovedTimeExportAuditRecorder.cs`
- `src/Hexalith.Timesheets.Server/Exports/IApprovedTimeExportAuditRecorder.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/approved-time-export-v1-project-work-boundaries.csv`
- `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs`
- `_bmad-output/implementation-artifacts/4-6-verify-finance-export-evidence-and-audit-trail.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-06-19: Implemented Story 4.6 export audit evidence, deterministic CSV v1 hardening, golden fixtures, privacy/metadata proof, and validation lanes. Status set to review.
- 2026-06-19: Senior Developer Review (auto-fix). No CRITICAL/HIGH issues. Strengthened AC4 period-key proof and CSV-escaping/formula-policy coverage; documented CSV v1 formula-neutralization policy. Build clean; Server 376, Integration 50 (+2 expected skips), Contracts 83, Architecture 21 passing. Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-19 · **Mode:** Adversarial, auto-fix · **Outcome:** Approved (done)

### Verification

- Build: `dotnet build Hexalith.Timesheets.slnx -warnaserror` → 0 warnings, 0 errors.
- Tests (direct xUnit v3 executables; VSTest socket fallback per README): Contracts 83/83, Server 376/376, Integration 50/50 (+2 expected infrastructure/performance skips), Architecture 21/21.
- Git vs File List: every changed source file is listed in the Dev Agent Record File List. No undocumented or phantom changes. Pre-existing unrelated working-tree edits (`Hexalith.FrontComposer`, `Hexalith.Tenants`, story-automator artifacts) were left untouched as instructed.

### AC and task audit

- **AC1** (accepted-export audit evidence, no secrets/tokens/bodies/sibling state) — IMPLEMENTED. `ApprovedTimeExported` carries only safe references + SHA-256 fingerprint; contract test proves the event JSON omits csv/comment/display/claims/body/envelope and finance-ownership vocabulary. Audit is recorded only on the accepted path; all denied/blocked/stale/empty/unsupported/non-billable/unsafe-row paths leave `AuditResult` null (proven by server tests).
- **AC2** (contract + golden-file determinism, no finance values/raw envelopes) — IMPLEMENTED. Golden fixture + deterministic-regeneration + round-trip + privacy fitness tests in place. Audit-metadata evolution is purely additive (two `init` properties; no positional rename/removal).
- **AC3** (comment redaction in output and diagnostics) — IMPLEMENTED. CSV emits comment text only when `ExportInclusion == Allowed`; excluded/redacted/policy-required comments are absent from CSV, audit event, and metadata.
- **AC4** (tenant-local period keys vs UTC audit instants) — IMPLEMENTED; proof strengthened (see fixes). Period matching keys off the tenant-local `ServiceDate`, structurally independent of the UTC approval instant.

### Findings and fixes applied (auto-fix)

- **MEDIUM — AC4 test confound (fixed):** `Export_scope_respects_...` set both `TenantLocalPeriodKey` and an overlapping `ServiceDateFrom/To`, so the date range excluded the only out-of-period row regardless of the period key — the period-key filter was never independently exercised. Added `Export_tenant_local_period_key_filters_independently_of_utc_audit_instants`, which filters by period key alone (no date range) across two rows approved at the same June UTC instant but with March/April service dates, proving the tenant-local period key is the sole discriminator and stays separate from the UTC audit instant.
- **LOW — CSV escaping coverage (fixed):** the task lists CR/LF and Unicode-safe text, but tests only exercised comma/quote/LF. Added `Csv_v1_quotes_carriage_returns_commas_quotes_and_unicode_without_mutating_plain_text`.
- **LOW — formula-neutralization policy not documented/scoped (fixed):** neutralization protects every column, not only comments, but this was undocumented. Added an XML-doc `<remarks>` policy block on `ApprovedTimeExportCsvWriter` and `Csv_v1_neutralizes_formula_leading_values_in_any_field_not_only_comments` pinning the all-field scope.

### Observations (no change required)

- `ApprovedTimeExported.GeneratedAtUtc` is set equal to `RequestedAtUtc`. The export is generated synchronously from the caller-supplied instant and no clock is injected; keeping them equal preserves deterministic golden output. Acceptable as designed.
- `DomainEventApprovedTimeExportAuditRecorder` returns `TimesheetsDomainResult.Success([evidence])` rather than persisting directly — consistent with every other Timesheets command service, where the host commits returned events to EventStore. No mutable/CRUD persistence is introduced.
