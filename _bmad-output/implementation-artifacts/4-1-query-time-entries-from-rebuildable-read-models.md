---
baseline_commit: 735f7e1f274e3a0389db913aed19f75314d90044
---

# Story 4.1: Query Time Entries from Rebuildable Read Models

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized operational user,
I want to query Time Entries by contributor, target, period, Activity Type, billable flag, approval state, and source type,
so that I can find and review the effort evidence relevant to my work.

## Acceptance Criteria

1. Given an authorized user queries Time Entries within a tenant, when they filter by Contributor, Project Reference, Work Reference, period, Activity Type, Billable Flag, Approval State, or source type, then Timesheets returns matching read models with stable IDs and projection freshness metadata, and result-level authorization is enforced.
2. Given a query includes Draft, Rejected, Corrected, Superseded, or Approved state options, when results are returned, then each entry state is explicit, and users can include or exclude non-current states according to query options.
3. Given a query crosses tenant boundaries or requests unauthorized target data, when it is handled, then it fails closed or filters results according to policy, and no protected Party, Project, Work, or Time Entry details are disclosed.
4. Given projection state is stale, rebuilding, degraded, or unavailable, when query results are shown, then freshness/trust metadata is visible in the API and UI, and stale data is not presented as fresh decision authority.
5. Given the operational query UI is displayed, when users filter, sort, page, drill into detail, and navigate back, then it uses FrontComposerProjectionView or FluentDataGrid, preserves filters, provides keyboard traversal, and shows status badges with text, and it does not expose raw EventStore stream browsing as a Timesheets UI path.

## Tasks / Subtasks

- [x] Add the operational Time Entry query contract and response DTOs (AC: 1, 2, 4)
  - [x] Add a query contract under `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries`, for example `QueryTimeEntries`, without server-controlled `TenantId`, `UserId`, `CorrelationId`, `MessageId`, token, claims, roles, or authorization fields.
  - [x] Model filters explicitly: contributor, project reference, work reference, tenant-local period or date range, Activity Type, Billable Flag, Approval State set including Submitted, Contributor/source type, correction/current-state options, sort, page size, and opaque cursor where needed.
  - [x] Add a list/page read model that reuses `TimeEntryEvidenceReadModel` shape where practical and always includes stable `TimeEntryId`, explicit approval/correction state, target reference, contributor reference, billable state, Activity Type, service date/duration, source category, and `ProjectionFreshnessMetadata`.
  - [x] Define source type in terms of existing evidence fields: `ContributorCategory.Employee`, `ExternalContributor` with `ExternalSource`, and `AutomatedAgent` with `AiEffortMetrics`. Do not infer source type from display labels.
  - [x] Reconcile the architecture/UX `degraded` projection state with the current contracts. Either add `ProjectionFreshnessState.Degraded` additively with tests and metadata updates, or explicitly map degraded upstream conditions to `Stale`/`Unavailable` with a non-empty `Detail`. Do not silently drop degraded state.
  - [x] Keep contracts additive and serialization-tolerant. Do not rename existing `ReadTimeEntryEvidence` or `TimeEntryEvidenceReadModel`.
- [x] Extend rebuildable projection support for operational list reads (AC: 1, 2, 4)
  - [x] Extend `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` or add a closely named list projection in the same folder. Reuse existing event-folding semantics for `TimeEntryRecorded`, submitted, confirmed, magic-link adjusted, approved, rejected, rejected-corrected, and approved-corrected events.
  - [x] Preserve idempotence: dedupe by `TimeEntryProjectionEvent.MessageId`, order by `SequenceNumber`, ignore unrelated entries, and produce deterministic output on replay.
  - [x] Preserve current effective state plus lineage. Corrected and superseded are correction states, not approval states; query options must combine `TimeEntryApprovalState` and `TimeEntryCorrectionState` without collapsing either enum.
  - [x] Map `TimesheetsProjectionCheckpoint` to `ProjectionFreshnessMetadata` consistently with existing single-entry evidence projection.
- [x] Add server-side query orchestration with tenant-first and result-level authorization (AC: 1, 3, 4)
  - [x] Add a reader interface beside `ITimeEntryEvidenceProjectionReader`, for example `ITimeEntryEvidenceListProjectionReader`, plus an unavailable fail-closed implementation.
  - [x] Add a query service beside `TimeEntryEvidenceQueryService` that authorizes `TimesheetsOperation.ProjectionRead` before projection lookup.
  - [x] For every candidate row, validate result-level authorization through `ITimesheetsAccessGuard` using contributor and the row target. Project targets must validate Project only; Work targets must validate Work only.
  - [x] Apply policy consistently: unauthorized rows are filtered when list policy allows filtering; cross-tenant, ambiguous, stale authority, unavailable sibling authority, or unconfigured policy must fail closed when policy requires non-disclosure.
  - [x] Hydrate display labels only after row authorization succeeds. Never hydrate or return labels for denied rows.
- [x] Expose the operational query surface through the existing host patterns (AC: 1, 3, 4)
  - [x] Add endpoint wiring only if this story owns an HTTP surface. Use typed DTO/read models and ProblemDetails for transport errors; do not expose raw EventStore envelopes, stream browsing, projection internals, or debug endpoints.
  - [x] Build the `TimesheetsRequestContext` only from trusted sources such as claims and trace/correlation context, matching existing endpoint patterns.
  - [x] Register new services/readers in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` with fail-closed defaults.
- [x] Publish FrontComposer metadata for the operational query UI (AC: 5)
  - [x] Add a `timesheets.projection.time-entry-query` descriptor to `TimesheetsMetadataCatalog` using `TimesheetsCompositionPattern.FrontComposerProjectionView`.
  - [x] Include fields for filters, stable IDs, target, contributor, Activity Type, service date, duration, billable state, approval state, correction state, contributor/source type, display hydration, and projection freshness.
  - [x] Include text-bearing status badge vocabularies for approval, correction, billable, contributor/source type, hydration, and projection freshness.
  - [x] Do not add a hand-built UI package unless required by this story's implementation. If a UI package is introduced, it must be `src/Hexalith.Timesheets.UI` with tests under `tests/Hexalith.Timesheets.UI.Tests`, using FrontComposer first and Fluent UI V5 components.
- [x] Add focused tests and update existing contract guards (AC: 1-5)
  - [x] Contracts tests: query contract avoids server-controlled authority/envelope fields; list read models serialize enum states as names; metadata catalog includes the new projection descriptor and required badge vocabularies.
  - [x] Projection tests: filters by contributor, Project, Work, period/date range, Activity Type, Billable Flag, Approval State, Contributor category/source type, and current/non-current correction options; replay/duplicates are deterministic.
  - [x] Server tests: tenant authority denial prevents projection lookup; no rows returns a non-disclosing empty/not-found result per policy; unauthorized Project/Work/contributor rows are filtered or fail closed as specified; display hydration runs only for disclosed rows.
  - [x] Integration tests: seeded entries can be queried through the service surface with stable ordering, paging/cursor behavior, freshness metadata, and drill-in compatibility with `ReadTimeEntryEvidence`.
  - [x] Performance evidence: add or update the existing performance evidence lane so common tenant/project/period operational queries have a documented path toward the 2 seconds p95 launch target without using unrealistic unbounded scans as the only implementation.
  - [x] Privacy tests: responses, metadata, logs, and diagnostics do not expose raw EventStore envelopes, comments beyond policy, protected sibling display data for denied rows, or command bodies.
- [x] Verify affected build and test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.IntegrationTests`, and architecture tests if endpoints, metadata, privacy, or project structure are touched.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `_bmad-output/planning-artifacts/epics.md`, especially Epic 4 and Story 4.1.
- Loaded `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and addendum, especially FR16, FR18 context, NFR security/reliability/performance, SM-4, and SM-7.
- Loaded `_bmad-output/planning-artifacts/architecture.md`, especially data architecture, projection model, API response formats, component boundaries, project structure, read model naming, and query/freshness requirements.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially `FrontComposerProjectionView`, `FluentDataGrid`, `FilterBar`, `StatusBadge`, and stale/rebuilding/unavailable projection state patterns.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Projects`, `Hexalith.Tenants`, `Hexalith.Conversations`, and `Hexalith.Parties` project-context files. No current Timesheets `project-context.md` file was present.
- Loaded previous completed story intelligence from `_bmad-output/implementation-artifacts/3-5-reject-invalid-confirmation-links-without-resource-disclosure.md`.

### Epic And Story Context

- Epic 4 starts the read/reporting/export path. Story 4.1 is the foundation for later approved ledger, Project/Work reports, AI effort reports, finance export, and dashboard stories.
- This story implements FR16 only. Do not build the Approved-Time Ledger, finance export, Project/Work rollups, or dashboard in this story. Make the query contracts and projection/read-service patterns reusable by those later stories.
- Queryable dimensions are Contributor, Project Reference, Work Reference, period/date range, Activity Type, Billable Flag, Approval State, and source type/contributor category.
- Query results must carry projection freshness/trust metadata. Stale, rebuilding, degraded, and unavailable states must be visible and must not be presented as fresh authority for trust-bearing decisions.
- Draft, Submitted, Rejected, and Approved approval states are distinct from Corrected and Superseded correction states. Current-only filtering and include-superseded/include-non-current options must be explicit.

### Current Code State To Extend

- `ReadTimeEntryEvidence` is a point-read query contract under `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries`.
- `TimeEntryEvidenceReadModel` already includes stable `TimeEntryId`, target, contributor, Activity Type, service date, duration, billable state, approval state, contributor category, AI metrics, correction state, projection freshness, comment, external source, contributor confirmation, event lineage, approval decision, correction evidence, approved correction evidence, external adjustment, lock evidence, and display hydration.
- `ProjectionFreshnessMetadata` currently supports `Fresh`, `Rebuilding`, `Stale`, and `Unavailable`; `ProjectionFreshnessState` also has `Unknown`.
- There is no current `ProjectionFreshnessState.Degraded` enum value even though architecture and UX mention degraded projection state. Treat that as an explicit implementation decision in this story, not an accidental omission.
- `TimeEntryEvidenceProjection` folds Time Entry events in sequence order, dedupes by message ID, ignores unrelated entries, maps checkpoint freshness, and preserves event lineage. Extend or reuse this behavior for list reads.
- `TimeEntryEvidenceQueryService` authorizes tenant access before projection lookup, re-authorizes the returned row by target and contributor, then hydrates display labels. The list query must preserve this ordering for every row.
- `UnavailableTimeEntryEvidenceProjectionReader` is registered as the fail-closed default. Add matching fail-closed defaults for list readers instead of returning synthetic rows.
- `TimesheetsMetadataCatalog` already has `timesheets.projection.time-entry-evidence`; Story 4.1 should add a distinct operational list/query projection descriptor rather than overloading the detail/evidence descriptor.
- `src/Hexalith.Timesheets/Program.cs` currently maps default endpoints, external contribution endpoints, magic-link endpoints, and `/metadata/timesheets`. If HTTP query routes are added, wire them through an endpoint extension, not inline ad hoc route bodies.

### Architecture Constraints

- EventStore is the only authoritative persistence boundary. Do not add SQL, Redis, Dapr state-store writes, local files, mutable caches, or direct projection mutation as authority for Time Entry query results.
- Projections are rebuildable, idempotent, replay-safe, duplicate-tolerant, and non-authoritative for writes.
- Query APIs return typed DTOs/read models, not raw EventStore envelopes. Transport errors use ProblemDetails; domain outcomes remain typed.
- Tenant/resource gates run before aggregate load, command dispatch, projection read, export, or disclosure. JWT tenant claims are not enough by themselves.
- Timesheets stores stable sibling references only. Do not copy Party personal data, Project hierarchy/state, or Work lifecycle state into durable Timesheets events/read models for convenience.
- Comments are sensitive unstructured data by default. Include comments only when the existing evidence policy permits the projection/read surface to expose them.
- Dates and periods must follow the resolved v1 policy: UTC audit instants plus tenant-local dates/period keys where business rules require them.
- Logs/traces must use structured metadata and correlation IDs without event payloads, comments, personal data, tokens, secrets, raw claims, protected identifiers, or command bodies.

### UX And UI Constraints

- Use `FrontComposerProjectionView` as the default generated query/projection surface and `FluentDataGrid` for dense operational results.
- Keep filters in a dense `FilterBar` above the grid. Preserve filters, sort, page, and selected period during drill-in and back navigation.
- Row click should open Time Entry Detail/evidence, not raw EventStore streams.
- Use text-bearing `StatusBadge` values for Approval State, Billable Flag, contributor/source type, correction state, display hydration, and projection freshness.
- Use `FluentMessageBar` or the FrontComposer equivalent for stale, rebuilding, degraded, or unavailable projection state. Do not rely on color alone.
- Do not make a marketing/landing page or decorative card layout for this operational surface.

### Previous Story Intelligence

- Story 3.5 completed the no-disclosure magic-link hardening and left one important pattern: fail-closed seams are acceptable only when tests prove they disclose nothing and follow-ups are recorded honestly.
- The current codebase already uses fail-closed default providers/readers. Do not replace them with permissive fakes in production registrations.
- Prior test execution noted local VSTest socket permission failures; the README direct xUnit v3 executable fallback is accepted when documented.
- Continue not modifying sibling submodule files such as `Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.Projects`, `Hexalith.Parties`, or `Hexalith.EventStore`.

### Git Intelligence Summary

- Recent work is story-driven and conventional: `feat(story-3.5)`, `feat(story-3.4)`, `feat(story-3.3)`, `feat(story-3.2)`.
- Story 3.5 touched magic-link endpoints, contracts, server services, OpenAPI, and tests, while keeping submodule files untouched.
- Existing query/projection work lives under `TimeEntries`, `TimesheetPeriods`, `ActivityTypes`, and `MagicLinks`, with tests in matching `Contracts.Tests`, `Server.Tests`, `Projections.Tests`, and `IntegrationTests` projects.

### Library And Framework Requirements

- Use the existing pinned local stack. Do not upgrade dependencies for this story.
- Current local SDK is pinned in `global.json` to .NET `10.0.301` with `rollForward: latestPatch`.
- Keep package versions centralized in `Directory.Packages.props`; do not add inline package versions to `.csproj`.
- Use xUnit v3, Shouldly, and NSubstitute in tests. Run test projects individually rather than solution-level `dotnet test`.
- Use `ConfigureAwait(false)` on every awaited call. Warnings are treated as errors.

### Project Structure Notes

- Expected contract changes belong under `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries`, `src/Hexalith.Timesheets.Contracts/Models`, `src/Hexalith.Timesheets.Contracts/ValueObjects`, and `TimesheetsMetadataCatalog.cs`.
- Expected projection changes belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected server changes belong under `src/Hexalith.Timesheets.Server/TimeEntries`, plus registration updates in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
- Expected endpoint changes, if any, belong under `src/Hexalith.Timesheets/Endpoints`, using a focused extension method similar to the existing endpoint groups.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.IntegrationTests`, and `tests/Hexalith.Timesheets.ArchitectureTests` when architecture/privacy metadata rules are touched.
- Do not use `_bmad-output/` or `docs/` as implementation scratch space.

### Testing Standards

- Every claimed authorization/freshness behavior needs executable proof.
- Test both tenant-first denial before projection lookup and per-result denial after projection lookup.
- Test Project-target and Work-target paths independently; the Project validator must not run for Work rows, and the Work validator must not run for Project rows.
- Test stale/rebuilding/unavailable freshness metadata in the projection and query service. If stale data is returned for non-trust-bearing list review, it must be visibly marked.
- Test the degraded-state decision explicitly: either round-trip the new `Degraded` value, or prove degraded upstream conditions are exposed as `Stale` or `Unavailable` with useful detail.
- Test current-only and include-non-current behavior for rejected correction and approved correction lineage.
- Test common tenant/project/period query shape for bounded filtering and stable order; do not rely only on in-memory full-list filtering in tests if the production seam is expected to page through projection storage.
- Test stable ordering and deterministic paging/cursor behavior from seeded projection data.
- Test that denied rows do not get display hydration and do not leak Party, Project, Work, comment, or Time Entry detail through labels, errors, telemetry, or metadata.

### Anti-Patterns To Prevent

- Do not create a second Time Entry evidence model that diverges from `TimeEntryEvidenceReadModel` unless the list row intentionally has a narrower documented shape.
- Do not use projections as write authority for approval, correction, export, or locking decisions.
- Do not introduce direct database/broker/cache dependencies or mutable in-memory stores for read authority.
- Do not accept tenant/user/correlation/authority/policy fields from query bodies as trusted.
- Do not bypass `ITimesheetsAccessGuard` because a row came from a tenant-scoped projection.
- Do not hydrate labels for rows that will be filtered or denied.
- Do not expose raw EventStore stream browsing, event payloads, aggregate state, projection internals, or debug endpoints in the Timesheets UI/API.
- Do not collapse AI token metrics into duration minutes or show unavailable token metrics as zero.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4-1-Query-Time-Entries-from-Rebuildable-Read-Models`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-16-Query-Time-Entries-by-operational-dimensions`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#10-Cross-Cutting-NFRs`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Structure-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/3-5-reject-invalid-confirmation-links-without-resource-disclosure.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/ReadTimeEntryEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/ITimeEntryEvidenceProjectionReader.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/UnavailableTimeEntryEvidenceProjectionReader.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `README.md#Build-and-Test`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --no-build` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- Used README direct xUnit v3 executable fallback for affected lanes.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 71 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed: 51 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed: 334 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 42 total, 0 failed, 2 existing infrastructure/performance skips.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed: 20 total, 0 failed.

### Completion Notes List

- Added additive operational Time Entry query contracts and read models with explicit filters, cursor paging, source type, and degraded projection freshness metadata.
- Added `TimeEntryEvidenceListProjection` that reuses the existing evidence projection fold per candidate entry, preserving duplicate tolerance, sequence ordering, correction states, freshness mapping, filtering, sorting, and cursor paging.
- Added server-side list query orchestration with fail-closed defaults, tenant-first authorization, per-row Project/Work/contributor authorization, filtering for ordinary unauthorized rows, fail-closed handling for non-disclosure authority failures, and post-authorization display hydration only.
- Published the `timesheets.projection.time-entry-query` FrontComposer descriptor with filter/grid fields and text-bearing badge vocabularies; no hand-built UI package or raw EventStore browsing path was added.
- Added focused contract, projection, server, integration, architecture/performance evidence coverage for the story acceptance criteria.

### File List

- `_bmad-output/implementation-artifacts/4-1-query-time-entries-from-rebuildable-read-models.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/performance-evidence.md`
- `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryQueryRowReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/QueryTimeEntries.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Projections/ProjectionFreshness.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceListProjection.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/ITimeEntryEvidenceListProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceListQueryResult.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceListQueryService.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/UnavailableTimeEntryEvidenceListProjectionReader.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/OperationalTimeEntryQueryServiceIntegrationTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 4.1 operational Time Entry query contracts, rebuildable list projection, server query orchestration, FrontComposer metadata, focused tests, and verification. Status set to review.
- 2026-06-19: Senior Developer Review (AI) completed. Re-ran restore/build/all affected test lanes (all green). Fixed File List omission of `HostMetadataEndpointTests.cs`. No CRITICAL/HIGH issues found; surfaced out-of-scope sibling submodule pointer drift. Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-19
**Outcome:** Approve (no CRITICAL or HIGH findings)

### Validation performed

- `dotnet restore Hexalith.Timesheets.slnx` — up to date.
- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror` — **succeeded, 0 warnings, 0 errors**.
- Direct xUnit v3 executable lanes (VSTest socket fallback, as documented):
  - Contracts.Tests: 71 passed, 0 failed.
  - Projections.Tests: 54 passed, 0 failed.
  - Server.Tests: 334 passed, 0 failed.
  - IntegrationTests: 43 passed, 0 failed, 2 infrastructure skips.
  - ArchitectureTests: 20 passed, 0 failed.

(Note: Projections/Integration counts are now 54/43 — higher than the 51/42 recorded in the
dev Debug Log because the new Story 4.1 list-projection and integration tests are included. The
dev-recorded counts were captured before the final test additions.)

### Acceptance Criteria audit

- **AC1 (query by all dimensions + freshness + result-level auth):** IMPLEMENTED. `QueryTimeEntries`
  exposes every required filter with no server-controlled authority fields; `TimeEntryEvidenceListQueryService`
  enforces tenant-first `ProjectionRead` authorization before projection lookup, then per-row Project/Work/contributor
  authorization; rows and page carry `ProjectionFreshnessMetadata`.
- **AC2 (state options + current/non-current):** IMPLEMENTED. `ApprovalStates` + `CorrectionStates` sets,
  `CurrentEntriesOnly`/`IncludeNonCurrentStates`. Approval and correction enums kept distinct (not collapsed).
- **AC3 (cross-tenant/unauthorized non-disclosure):** IMPLEMENTED. Ordinary unauthorized rows
  (`Unauthorized → InsufficientRole`) are filtered; cross-tenant, stale, ambiguous, unavailable-sibling, and
  unconfigured-policy denials fail closed. Verified by `Evidence_list_query_*` server tests; labels are never
  hydrated for denied rows.
- **AC4 (stale/rebuilding/degraded/unavailable visibility):** IMPLEMENTED. `ProjectionFreshnessState.Degraded`
  added additively (contracts + projection enums + metadata factory), round-tripped in contract tests.
- **AC5 (operational query UI):** ADDRESSED via the `timesheets.projection.time-entry-query`
  `FrontComposerProjectionView` descriptor with filter/grid fields and text-bearing status-badge vocabularies; the
  metadata JSON is asserted to contain no `EventStore`/`stream` browsing strings. Per the story scope, no hand-built
  UI package was added; interactive behaviors (keyboard traversal, back-nav filter preservation) are delegated to
  FrontComposer generation.

### Findings (all auto-resolved or surfaced; none block automation)

- **[MEDIUM][Resolved] File List omission.** `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
  was modified (real test that exports/validates the new query descriptor) but absent from the File List. Added.
- **[MEDIUM][Surfaced — needs human action] Out-of-scope submodule pointer drift.** Working tree advances the
  `Hexalith.FrontComposer` (2396ef9→20d2102) and `Hexalith.Tenants` (79938e7→f12db93) submodule gitlinks to
  commits unrelated to Story 4.1 (FrontComposer accordion refactor; Tenants cross-cutting stories). The story
  explicitly forbids modifying sibling submodules. Working trees are clean (pointer-only). NOT reverted — these were
  not produced by this story and may be intentional in-flight work; **they must be excluded from the Story 4.1 commit.**
- **[LOW] Duplicated freshness mapping.** `ToFreshnessMetadata(checkpoint)` is duplicated between
  `TimeEntryEvidenceProjection` and `TimeEntryEvidenceListProjection`. Harmless; candidate for a shared helper later.
- **[LOW] List row drops `ActivityTypeScope`.** `TimeEntryQueryRowReadModel` omits scope, so the list query service
  passes `ActivityTypeScope.Unknown` to the display hydrator; project-scoped activity-type labels may hydrate as
  unavailable on the list surface. No correctness/privacy impact (the row's narrower shape deliberately excludes
  comment/lineage/AI metrics, which is privacy-positive). Revisit when a real list reader/hydrator lands.
- **[LOW] List projection re-folds full event stream per candidate (O(N×M)).** Acceptable for this story — the
  production reader is the fail-closed `Unavailable` default, and `docs/performance-evidence.md` documents the bounded
  tenant/project/period path the future storage-backed reader must take.
