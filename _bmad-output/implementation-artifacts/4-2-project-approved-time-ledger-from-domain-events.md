---
baseline_commit: da204eceb57d600297eab6abf99d1476b191e76a
---

# Story 4.2: Project Approved Time Ledger from Domain Events

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a finance or audit consumer,
I want an Approved-Time Ledger projection with approval metadata and correction lineage,
so that approved effort can be used as trusted downstream evidence without becoming separate authoritative storage.

## Acceptance Criteria

1. Given Time Entries are approved, when approval events are projected, then the Approved-Time Ledger includes Time Entry ID, Contributor Party ID, Target Reference, date, duration, Activity Type, Billable Flag, approval metadata, correction lineage, and comment where policy allows, and EventStore remains the source of authority.
2. Given an approved entry is corrected or superseded, when ledger projections rebuild, then original and corrected/superseded evidence remains visible with lineage, and query options can include or exclude superseded entries.
3. Given duplicate, replayed, or rebuilt event streams are processed, when the Approved-Time Ledger projection is regenerated, then the ledger output is deterministic and idempotent, and it does not accumulate duplicate rows.
4. Given ledger reads depend on projection state, when the projection is stale, rebuilding, degraded, or unavailable, then the API and UI expose freshness/trust metadata, and finance export actions cannot treat stale data as fresh unless explicitly allowed by policy.
5. Given unauthorized or cross-tenant ledger access is attempted, when the ledger is queried, then access fails closed or filters according to policy, and no protected contributor, target, comment, or ledger details are disclosed.
6. Given the Approved-Time Ledger UI is displayed, when users review approved evidence, then FrontComposer/Fluent UI V5 surfaces show stable IDs, approval metadata, billable status, comments where allowed, correction lineage, filters, projection freshness, keyboard flow, labels, and focus management.

## Tasks / Subtasks

- [x] Add dedicated Approved-Time Ledger contracts and read models (AC: 1, 2, 4, 6)
  - [x] Add a query contract under `src/Hexalith.Timesheets.Contracts/Queries/Reporting`, for example `QueryApprovedTimeLedger`, without caller-supplied `TenantId`, `UserId`, claims, roles, correlation authority, message IDs, tokens, or export-authority fields.
  - [x] Model filters explicitly: Project Reference, Work Reference, Contributor Party, Activity Type, tenant-local period key or date range, Billable Flag, current-only/include-superseded option, sort, page size, and opaque cursor.
  - [x] Add `ApprovedTimeLedgerReadModel` and `ApprovedTimeLedgerRowReadModel` under `src/Hexalith.Timesheets.Contracts/Models`. Rows must include stable `TimeEntryId`, `Contributor`, `Target`, `ServiceDate`, `DurationMinutes`, `ActivityTypeId`, `BillableState`, approval metadata, correction lineage, lock state, row state/current-vs-superseded signal, display hydration, comment policy state, and `ProjectionFreshnessMetadata`.
  - [x] Reuse existing value objects and evidence records where practical: `TimeEntryApprovalDecisionEvidence`, `TimeEntryApprovedCorrectionEvidence`, `TimeEntryCorrectionValues`, `TimeEntryLockEvidence`, `TimeEntryDisplayHydration`, and `ProjectionFreshnessMetadata`.
  - [x] Add a narrow ledger-specific enum only if needed, for example `ApprovedTimeLedgerRowState` with `Unknown`, `Current`, and `Superseded`. Keep it additive and JSON string-converted like existing enums.
  - [x] Comments must be nullable and policy-gated. If projection/comment policy is unresolved or disallows projection inclusion, the ledger row must omit comment text and expose enough policy state for the UI/export-preview surface to explain the omission without leaking the text.
- [x] Implement the rebuildable Approved-Time Ledger projection (AC: 1, 2, 3, 4)
  - [x] Add `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`. Use a ledger-specific `ProjectionName`, for example `approved-time-ledger`.
  - [x] Consume Time Entry domain events through existing projection event shapes where possible. `TimeEntryRecorded`, `TimeEntrySubmitted`, `TimeEntryContributorConfirmed`, `TimeEntryAdjustedThroughMagicLink`, `TimeEntryApproved`, `TimeEntryRejected`, `TimeEntryCorrected`, and `TimeEntryApprovedCorrected` are already modeled by `TimeEntryProjectionEvent`.
  - [x] Reuse `TimeEntryEvidenceProjection` folding semantics instead of duplicating approval/correction event interpretation. The ledger may derive rows from evidence models, but it must return a ledger-shaped read model and must not overload `TimeEntryQueryReadModel` as the ledger contract.
  - [x] Include only ledger-eligible approved evidence by default. Draft, Submitted, and Rejected entries are not ledger rows.
  - [x] Preserve approved correction lineage. For an approved correction, expose the effective current approved row plus previous/superseded evidence through a clear lineage shape; include superseded rows only when the query option requests them.
  - [x] Preserve rejected-entry correction lineage once a corrected entry is later approved. Do not treat a rejected correction as finance evidence before an approval event makes it ledger-eligible.
  - [x] Preserve idempotence: dedupe by `MessageId`, order by `SequenceNumber`, ignore unrelated entries, and produce deterministic rows across replay/rebuild.
  - [x] Map `TimesheetsProjectionCheckpoint` to `ProjectionFreshnessMetadata`, including the existing `Degraded` state added in Story 4.1.
- [x] Add server-side ledger query orchestration with fail-closed authorization (AC: 4, 5)
  - [x] Add `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/IApprovedTimeLedgerProjectionReader.cs`, `UnavailableApprovedTimeLedgerProjectionReader.cs`, `ApprovedTimeLedgerQueryResult.cs`, and `ApprovedTimeLedgerQueryService.cs`.
  - [x] Register the service and fail-closed reader in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Authorize `TimesheetsOperation.ProjectionRead` before projection lookup. Ledger reads are projection reads; do not use `TimesheetsOperation.Export` until an actual export request is implemented in Story 4.5.
  - [x] For every candidate row, re-authorize result disclosure through `ITimesheetsAccessGuard` with the row contributor and exact target. Project rows validate Project only; Work rows validate Work only.
  - [x] Match Story 4.1's non-disclosure policy: ordinary insufficient-role rows may be filtered when policy allows; cross-tenant, stale authority, ambiguous authority, unavailable sibling authority, invalid reference, and unconfigured policy must fail closed.
  - [x] Hydrate display labels only after row authorization succeeds. Never hydrate or return labels/comments for denied rows.
  - [x] Expose freshness/export-readiness information for later export workflows, but do not generate export files or audit export records in this story.
- [x] Publish FrontComposer metadata for the Approved-Time Ledger surface (AC: 4, 6)
  - [x] Add a `timesheets.projection.approved-time-ledger` descriptor to `TimesheetsMetadataCatalog` using `TimesheetsCompositionPattern.FrontComposerProjectionView`.
  - [x] Include filter fields for Project, Work, Contributor, Activity Type, period/date range, Billable Flag, current-only/include-superseded, and projection freshness.
  - [x] Include grid/detail fields for stable IDs, Target Reference, Contributor Party reference, Activity Type, service date, duration, Billable Flag, approval decision metadata, correction lineage, lock state, comment policy/comment value where allowed, display hydration, and projection freshness.
  - [x] Include text-bearing status-badge vocabularies for billable state, correction/ledger row state, lock state, display hydration, approval authority/source where surfaced, comment policy, and projection freshness.
  - [x] If metadata includes an export action, it must be a non-generating preview/review intent for later Story 4.5 and must be disabled or blocked when the ledger has no approved rows or projection freshness is not acceptable. Do not create CSV/API/webhook export output here.
- [x] Add focused tests and update existing guards (AC: 1-6)
  - [x] Contracts tests: ledger query contract omits server-controlled authority fields; read models round-trip enum state names, approval metadata, correction lineage, comment policy state, cursor paging, and degraded freshness.
  - [x] Projection tests: approved entries appear; Draft/Submitted/Rejected entries do not; approved corrections expose current and superseded lineage; include/exclude superseded options work; duplicate/replayed events do not duplicate rows; stable sort and cursor paging are deterministic.
  - [x] Server tests: tenant denial prevents projection lookup; fail-closed default reader discloses nothing; unauthorized rows are filtered or fail closed by denial category; display hydration and comment disclosure happen only after authorization.
  - [x] Metadata tests: `timesheets.projection.approved-time-ledger` exists with required filters, fields, actions if any, and text-bearing status-badge vocabularies; metadata does not expose EventStore stream browsing or finance ownership language.
  - [x] Integration tests: seeded approved and approved-corrected entries can be queried through the ledger service with freshness metadata, deterministic paging, per-row authorization, and drill-in compatibility with `ReadTimeEntryEvidence`.
  - [x] Privacy tests: ledger responses, metadata, logs, and diagnostics do not expose raw EventStore envelopes, copied Party/Project/Work display data for denied rows, event payload dumps, comments when policy excludes them, command bodies, tokens, secrets, rates, invoice totals, payroll values, taxes, or revenue-recognition data.
- [x] Verify affected build and test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.IntegrationTests`, and `tests/Hexalith.Timesheets.ArchitectureTests` when metadata/privacy/project-structure rules are touched.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `_bmad-output/implementation-artifacts/sprint-status.yaml` completely. Story key `4-2-project-approved-time-ledger-from-domain-events` was backlog under Epic 4.
- Loaded `_bmad-output/planning-artifacts/epics.md` completely, especially Epic 4 and Story 4.2.
- Loaded `_bmad-output/planning-artifacts/architecture.md` completely, especially EventStore-first persistence, projection model, API/query patterns, approved ledger/export boundaries, project structure, and FrontComposer/Fluent UI rules.
- Loaded `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR18, FR19, governance, NFRs, success metrics SM-2/SM-5/SM-7, and the finance evidence scope.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Approved-Time Ledger, projection freshness, grid/filter/status-badge, export no-results, and evidence-copy rules.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Conversations`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files. No Timesheets-local `project-context.md` file was present.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/4-1-query-time-entries-from-rebuildable-read-models.md`.

### Discovery Results

- `{epics_content}` loaded from 1 file: `_bmad-output/planning-artifacts/epics.md`.
- `{architecture_content}` loaded from 1 file: `_bmad-output/planning-artifacts/architecture.md`.
- `{prd_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md`.
- `{ux_content}` loaded from 2 sharded files: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md`, `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md`.
- Persistent facts loaded from 6 sibling `project-context.md` files.

### Epic And Story Context

- Epic 4 covers querying, reporting, Approved-Time Ledger, finance export, AI effort reporting, and dashboard surfaces.
- Story 4.2 implements FR18 and provides the ledger foundation for later report/export stories. It must not implement Story 4.3 Project/Work rollups, Story 4.4 AI effort reports, Story 4.5 export generation, Story 4.6 export audit trail, or Story 4.7 dashboard composition.
- The Approved-Time Ledger is a rebuildable projection derived from Timesheets domain events. It is not a source of authority and must not become mutable storage.
- Ledger rows must represent approved evidence only. Approval makes the entry ledger-eligible; approved corrections and superseding lineage remain visible for audit/reconciliation.
- Export remains evidence-only and downstream-facing. No rates, invoice totals, payroll values, taxes, revenue-recognition calculations, invoice language, or project/work ownership should appear.

### Current Code State To Extend

- `TimeEntryEvidenceReadModel` already contains stable entry fields, `ApprovalDecision`, `Correction`, `ApprovedCorrection`, `LockEvidence`, `EventLineage`, `DisplayHydration`, `Comment`, `SourceAuthority`, and `ProjectionFreshness`.
- `TimeEntryEvidenceProjection` folds Time Entry events in sequence order, dedupes by `MessageId`, ignores unrelated entries, maps checkpoint freshness including `Degraded`, and preserves approval/correction evidence. Reuse or wrap this fold for ledger semantics instead of duplicating event interpretation.
- `TimeEntryEvidenceListProjection` from Story 4.1 is an operational list projection. It is useful as a pattern for sorting, filtering, cursor paging, and freshness, but it is not the Approved-Time Ledger contract.
- `QueryTimeEntries`, `TimeEntryQueryReadModel`, and `TimeEntryQueryRowReadModel` are FR16 operational query contracts. Add separate ledger contracts rather than widening these until they become finance-specific.
- `TimeEntryEvidenceListQueryService` already shows the required authorization sequence: tenant-first projection read authorization, projection lookup, per-row Project/Work/contributor authorization, post-authorization display hydration, and fail-closed handling for non-disclosure categories.
- `TimesheetsOperation` already includes `ProjectionRead` and `Export`; use `ProjectionRead` for ledger reads in this story. Reserve `Export` for actual export generation and export audit in Stories 4.5/4.6.
- `TimesheetsMetadataCatalog` already has `timesheets.projection.time-entry-evidence` and `timesheets.projection.time-entry-query`. Add a distinct `timesheets.projection.approved-time-ledger` descriptor.
- `TimesheetsEvidencePolicyDescriptor` and `TimeEntryCommentPolicy.SensitiveDefault` make comment projection/export policy explicit. Ledger comment fields must obey policy; do not expose comments just because they exist on `TimeEntryEvidenceReadModel`.
- `ProjectionPlaceholderTests` already references `approved-time-ledger` as a checkpoint example. Replace or extend placeholder coverage with real ledger projection tests.

### Architecture Constraints

- EventStore is the only authoritative persistence boundary. Do not add SQL, Redis, Dapr state-store writes, local files, mutable caches, direct projection mutation, or broker-backed CRUD as ledger authority.
- Projections are rebuildable, idempotent, replay-safe, duplicate-tolerant, and non-authoritative for writes.
- Query APIs return typed DTOs/read models and `ProjectionFreshnessMetadata`, not raw EventStore envelopes or projection internals.
- Tenant/resource gates run before aggregate load, command dispatch, projection read, export, or disclosure. JWT tenant/user claims are request evidence, not authority.
- Timesheets stores stable Tenant, Party, Project, and Work references only. Do not copy Party personal data, Project hierarchy/state, Work lifecycle/planning state, or tenant membership data into ledger rows.
- Dates and periods follow the v1 policy: UTC audit instants plus tenant-local dates/period keys where business rules require them.
- Logs/traces must use structured metadata and correlation IDs without event payloads, comments, personal data, tokens, secrets, raw claims, protected identifiers, or command bodies.
- Public contract evolution must be additive and serialization-tolerant. Do not rename existing Time Entry events, evidence read models, or query contracts.

### UX And UI Constraints

- Approved-Time Ledger uses `FrontComposerProjectionView` and dense `FluentDataGrid` semantics through metadata, not a hand-built UI package in this story.
- Filters belong above the grid and must be preserved during drill-in/back navigation by the generated/FrontComposer surface.
- Row click should open Time Entry Evidence/Detail, not EventStore streams or projection internals.
- Use text-bearing status badge vocabularies for Billable Flag, correction/ledger row state, lock state, display hydration, comment policy, and projection freshness.
- Show stale/rebuilding/degraded/unavailable projection state through persistent message-bar style metadata; stale data must not appear as fresh export evidence.
- Empty ledger state should support the later UX copy `No approved entries match these filters.` and must not offer a misleading export action.
- UI copy must remain factual and must not imply Timesheets owns Party, Project, Work, invoice, payroll, rate, tax, or revenue decisions.

### Previous Story Intelligence

- Story 4.1 added additive operational query contracts, degraded projection freshness, `TimeEntryEvidenceListProjection`, `TimeEntryEvidenceListQueryService`, fail-closed list readers, metadata descriptor `timesheets.projection.time-entry-query`, and focused tests.
- Build/test lanes after Story 4.1 passed through direct xUnit executable fallback where VSTest socket permissions were blocked.
- Story 4.1 review noted low-risk duplication of freshness mapping between evidence projections. If this story touches freshness mapping again, prefer a small shared helper only if it reduces meaningful duplication without widening scope.
- Story 4.1 review also noted `TimeEntryQueryRowReadModel` drops `ActivityTypeScope`, causing list display hydration to pass `ActivityTypeScope.Unknown`. For ledger rows, include `ActivityTypeScope` if comment/display hydration or project-scoped Activity Type labels require it.
- Story 4.1 deliberately did not build the Approved-Time Ledger, finance export, Project/Work rollups, or dashboard. Do not backfill those as incidental changes beyond the ledger projection/query surface.

### Git Intelligence Summary

- Last 5 commit titles:
  - `da204ec feat(story-4.1): Query Time Entries from Rebuildable Read Models`
  - `735f7e1 docs(retro): Capture Epic 3 Retrospective`
  - `21890e2 feat(story-3.5): Reject Invalid Confirmation Links Without Resource Disclosure`
  - `6349709 feat(story-3.4): Adjust Time Through Magic Link`
  - `2dd4a2f feat(story-3.3): Confirm Time Through Magic Link`
- Story 4.1 changed contracts, projection, server query services, metadata catalog, performance evidence, integration tests, and sprint artifacts. Story 4.2 should follow that same pattern with ledger-specific names and fail-closed defaults.
- Story 3.5 and later magic-link work established a strict no-disclosure posture: fail-closed defaults are acceptable only when tests prove they disclose nothing.
- Current working tree already has unrelated submodule pointer drift for `Hexalith.FrontComposer` and `Hexalith.Tenants`, plus `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Do not revert or include those changes unless explicitly instructed.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current repo uses .NET 10 with `.slnx`, Central Package Management, nullable, implicit usings, and warnings as errors.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Do not add Moq, FluentAssertions, solution-level `.sln`, or inline package versions.
- Use FrontComposer metadata and Blazor Fluent UI V5 semantics. Do not add raw HTML/CSS UI, a Timesheets UI project, or a parallel shell for this story.
- Use `ConfigureAwait(false)` on every awaited call.
- No external latest-version research is needed for this story because no new framework or library choice is required; the architecture and repo pin the implementation stack.

### Project Structure Notes

- Expected contract additions:
  - `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerReadModel.cs`
  - `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`
  - Optional focused ledger lineage/status models only if they prevent ambiguity.
  - `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
  - `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs` only if a new ledger row state enum is required.
- Expected projection additions:
  - `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`
  - Optional small helper for shared freshness mapping if it keeps `TimeEntryEvidenceProjection`, `TimeEntryEvidenceListProjection`, and the ledger projection consistent.
- Expected server additions:
  - `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/IApprovedTimeLedgerProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/UnavailableApprovedTimeLedgerProjectionReader.cs`
  - `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryResult.cs`
  - `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`
  - `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- Expected tests:
  - `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` or a focused ledger contract test file.
  - `tests/Hexalith.Timesheets.Projections.Tests/ApprovedTimeLedgerProjectionTests.cs`
  - `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeLedgerAuthorizationTests.cs` or matching existing authorization test placement.
  - `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeLedgerQueryServiceIntegrationTests.cs`
  - `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` if metadata endpoint output changes.
  - `tests/Hexalith.Timesheets.ArchitectureTests` if privacy/contract boundary rules need to include the new ledger models.
- Do not use `_bmad-output/`, `docs/`, or submodules as implementation scratch space.

### Testing Standards

- Every claimed authorization, freshness, comment policy, and lineage behavior needs executable proof.
- Test tenant-first denial before projection lookup and per-row denial after projection lookup.
- Test Project-target and Work-target rows independently; the Project validator must not run for Work rows, and the Work validator must not run for Project rows.
- Test stale, rebuilding, degraded, and unavailable freshness states in projection/read models and service outputs.
- Test that non-fresh ledger data blocks or clearly marks export readiness, without implementing export generation.
- Test approved corrections with previous/current evidence and include/exclude superseded behavior.
- Test rejected corrections only become ledger-eligible after approval.
- Test duplicate events and replay from sequence zero produce identical ledger rows and no duplicate rows.
- Test stable ordering and cursor paging with deterministic IDs/dates.
- Test denied rows do not get display hydration and do not leak Party, Project, Work, comment, or Time Entry detail through labels, errors, telemetry, or metadata.

### Anti-Patterns To Prevent

- Do not treat the operational `TimeEntryQueryReadModel` as the Approved-Time Ledger contract.
- Do not use projections as write authority for approval, correction, export, or locking decisions.
- Do not introduce direct database/broker/cache dependencies or mutable in-memory stores as ledger authority.
- Do not accept tenant/user/correlation/authority/policy fields from query bodies as trusted.
- Do not bypass `ITimesheetsAccessGuard` because a row came from a tenant-scoped projection.
- Do not hydrate display labels or expose comments for rows that will be filtered or denied.
- Do not expose raw EventStore stream browsing, event payloads, aggregate state, projection internals, or debug endpoints in the Timesheets UI/API.
- Do not include rates, invoice totals, taxes, payroll values, revenue-recognition language, or copied sibling-owned state in ledger contracts or metadata.
- Do not implement CSV/API/webhook export, export audit records, or export command handling in this story.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-2-Project-Approved-Time-Ledger-from-Domain-Events`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-18-Maintain-the-Approved-Time-Ledger`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-19-Export-approved-billable-evidence`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#9-Data-Governance-and-Audit-Requirements`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#12-Success-Metrics`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-for-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Reporting-Approved-Time-Ledger-And-Exports`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Approved-Time-Ledger`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/4-1-query-time-entries-from-rebuildable-read-models.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/QueryTimeEntries.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryApprovalDecisionEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryApprovedCorrectionEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionValues.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryLockEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApproved.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApprovedCorrected.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceListProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimesheetsProjectionCheckpoint.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceListQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Policies/TimesheetsEvidencePolicyDescriptor.cs`]
- [Source: `README.md#Build-and-Test`]
- [Source: `docs/boundary-decision-record.md`]
- [Source: `docs/performance-evidence.md`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- 2026-06-19: `dotnet test` was blocked by local VSTest socket permissions (`SocketException (13): Permission denied`), so the README direct xUnit v3 executable fallback was used.
- 2026-06-19: Direct xUnit v3 executable fallback passed for Contracts, Projections, Server, Integration, and Architecture test projects. Integration retained 2 existing infrastructure/performance skips.

### Completion Notes List

- Implemented dedicated Approved-Time Ledger query/read model contracts with explicit filters, cursor paging, export-readiness metadata, ledger row state, approval/correction lineage, lock evidence, display hydration, freshness metadata, and policy-gated comments.
- Added rebuildable `approved-time-ledger` projection derived from `TimeEntryEvidenceProjection`, preserving approved-only eligibility, approved correction current/superseded rows, rejected-correction lineage after later approval, deterministic sorting/paging, duplicate replay idempotence, and degraded freshness mapping.
- Added server-side ledger query orchestration and fail-closed default projection reader using `TimesheetsOperation.ProjectionRead`, tenant-first authorization, per-row Project/Work/contributor disclosure checks, and post-authorization display hydration.
- Published `timesheets.projection.approved-time-ledger` FrontComposer metadata with required filters, grid/detail fields, text-bearing status vocabularies, and non-generating export-readiness review action.
- Added focused contract, projection, server, integration, and architecture/metadata guard coverage. No finance export files, export audit records, reports, dashboard, rates, invoice totals, payroll values, taxes, or revenue-recognition logic were implemented.

### File List

- `_bmad-output/implementation-artifacts/4-2-project-approved-time-ledger-from-domain-events.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeLedgerRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/Reporting/QueryApprovedTimeLedger.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs`
- `src/Hexalith.Timesheets.Projections/ProjectionFreshnessMetadataMapper.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceListProjection.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryResult.cs`
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs`
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/IApprovedTimeLedgerProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/UnavailableApprovedTimeLedgerProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeLedgerContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeLedgerQueryServiceIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/ApprovedTimeLedgerProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeLedgerAuthorizationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 4.2 Approved-Time Ledger contracts, projection, query service, FrontComposer metadata, policy-gated lineage/comment handling, tests, and validation; moved story to review.
- 2026-06-19: Senior Developer Review (AI) completed. Auto-fixed export-readiness detail/flag coherence in `ApprovedTimeLedgerQueryService` (MEDIUM) and removed dead duplicated sort computation in `ApprovedTimeLedgerProjection` (LOW); added server test for the all-rows-filtered export-readiness case. Re-ran restore/build (`-warnaserror`, 0 warnings) and all affected test lanes (green). No CRITICAL findings; status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jerome — 2026-06-19
**Outcome:** Approve (no CRITICAL or HIGH findings; MEDIUM/LOW auto-fixed)

### Validation performed

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx` — up to date.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror` — **succeeded, 0 warnings, 0 errors** (after fixes).
- Direct xUnit v3 executable lanes (VSTest socket fallback, as documented in README):
  - Contracts.Tests: 74 passed, 0 failed.
  - Projections.Tests: 63 passed, 0 failed.
  - Server.Tests: 345 passed, 0 failed (includes new export-readiness filter test).
  - IntegrationTests: 44 passed, 0 failed, 2 infrastructure/performance skips.
  - ArchitectureTests: 20 passed, 0 failed.

### Git vs File List

- Story File List matches the git working tree for all in-scope source/test additions and modifications. No discrepancies.
- Out-of-scope drift (pre-existing, NOT part of this story, must be excluded from the commit): `Hexalith.FrontComposer` / `Hexalith.Tenants` submodule pointer drift and `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Carried over from the Story 4.1 review and confirmed unrelated.

### Acceptance Criteria audit

- **AC1 (ledger fields + EventStore stays authoritative):** IMPLEMENTED. `ApprovedTimeLedgerRowReadModel` carries stable `TimeEntryId`, contributor/target, service date, duration, activity type/scope, billable state, `ApprovalDecision`, `LockEvidence`, lineage, display hydration, comment policy, and freshness. The projection derives rows from `TimeEntryEvidenceProjection` rather than mutating storage.
- **AC2 (correction/superseded lineage + include/exclude option):** IMPLEMENTED. `CurrentFromEvidence` + `SupersededFromApprovedCorrection` expose current and prior approved evidence; `CurrentRowsOnly`/`IncludeSupersededRows` gate visibility. Verified by projection tests.
- **AC3 (deterministic + idempotent, no duplicate rows):** IMPLEMENTED. `CandidateIds` dedupes by `MessageId`; the evidence fold dedupes again; ordering uses stable ascending tie-breakers (`TimeEntryId`, then `RowState`). Replayed duplicate events produce identical rows (tested).
- **AC4 (freshness/trust + stale data cannot be treated as fresh):** IMPLEMENTED. Freshness mapped via the new shared `ProjectionFreshnessMetadataMapper`; export readiness requires `Fresh`. **Fixed (MEDIUM):** the query service now recomputes `ExportReadinessDetail` together with `CanUseForExport` so a page whose rows are all authorization-filtered no longer reports a contradictory "fresh enough for export preview" message.
- **AC5 (cross-tenant/unauthorized non-disclosure):** IMPLEMENTED. Tenant-first `ProjectionRead` authorization precedes lookup; per-row Project/Work/contributor re-authorization filters only `InsufficientRole` and fails closed on cross-tenant, stale, ambiguous, unavailable-sibling, invalid-reference, and unconfigured-policy categories; denied rows are never hydrated. Verified by server tests.
- **AC6 (FrontComposer/Fluent UI surface metadata):** ADDRESSED via the `timesheets.projection.approved-time-ledger` `FrontComposerProjectionView` descriptor with filters, grid/detail fields, and text-bearing status-badge vocabularies; metadata JSON asserted to contain no `EventStore`/finance-ownership language. Per story scope, interactive keyboard/focus behavior is delegated to FrontComposer generation (no hand-built UI).

### Findings

- **[MEDIUM][Resolved] Export-readiness detail could contradict the flag.** `ApprovedTimeLedgerQueryService` recomputed `CanUseForExport` after per-row filtering but kept the projection's `ExportReadinessDetail`; when all rows were filtered on a fresh page the result reported `CanUseForExport=false` with detail "Approved ledger rows are fresh enough for export preview." Fixed by recomputing the detail coherently and added a regression test (`Ledger_query_blocks_export_readiness_when_all_rows_are_filtered`).
- **[LOW][Resolved] Dead/duplicated sort computation.** `ApprovedTimeLedgerProjection.Sort` built the ascending ordering and then discarded/recomputed it for the descending case. Refactored to apply sort direction once via an `OrderByPrimary` helper; behavior unchanged (paging/sort tests stay green).
- **[LOW][Acknowledged] Per-candidate full-stream re-fold (O(N×M)).** The projection re-folds the full event list for each candidate entry, matching the accepted Story 4.1 list-projection pattern; the production reader is the fail-closed `Unavailable` default. No change in scope.
- **[Positive] Freshness-mapping duplication from Story 4.1 retired.** The previously duplicated `ToFreshnessMetadata` was consolidated into `ProjectionFreshnessMetadataMapper` and reused by both evidence projections and the ledger, resolving the Story 4.1 LOW note.
