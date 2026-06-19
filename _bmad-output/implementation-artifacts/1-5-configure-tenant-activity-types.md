---
baseline_commit: 5bb8616
---

# Story 1.5: Configure Tenant Activity Types

Status: done

## Story

As an authorized tenant operator,
I want to define and maintain tenant-level Activity Types,
so that contributors can classify time consistently without rewriting historical evidence.

## Acceptance Criteria

1. Given an authorized tenant operator is in a valid tenant context, when they create a tenant-level Activity Type with a stable ID, display label, active state, and optional billable-default metadata, then the change is persisted through Hexalith.EventStore as an auditable domain event, and the Activity Type is available for future tenant-scoped Time Entry capture.
2. Given an existing tenant-level Activity Type, when an authorized tenant operator updates its display label or billable-default metadata, then a new event records the change, and historical Time Entry facts continue to reference the stable Activity Type ID rather than being rewritten.
3. Given an existing tenant-level Activity Type, when an authorized tenant operator deactivates it, then the Activity Type is unavailable for new Time Entries, and it remains visible and reportable for historical entries.
4. Given a caller lacks tenant authority or supplies a tenant context they do not control, when they attempt to create, update, or deactivate a tenant Activity Type, then the command fails closed, and no Activity Type event or projection change is produced.
5. Given the tenant Activity Type catalog projection is queried, when read models are returned, then they include projection freshness metadata and active/inactive status text, and stale, rebuilding, or unavailable projections are not presented as fresh catalog authority.
6. Given Activity Type catalog UI is generated or composed, when the catalog is displayed, then it uses FrontComposer/Fluent UI V5 surfaces with a dense grid or projection view, visible status badges, keyboard reachable commands, and no color-only state.

## Tasks / Subtasks

- [x] Implement tenant Activity Type domain state and pure command decisions (AC: 1, 2, 3)
  - [x] Add a focused Activity Type aggregate/state boundary under `src/Hexalith.Timesheets.Server/ActivityTypes` or `Server/Aggregates`.
  - [x] Handle `CreateTenantActivityType`, `RenameActivityType`, `UpdateActivityTypeMetadata`, `DeactivateActivityType`, and `ReactivateActivityType` for tenant scope only.
  - [x] Emit existing contract events `ActivityTypeCreated`, `ActivityTypeRenamed`, `ActivityTypeMetadataUpdated`, `ActivityTypeDeactivated`, and `ActivityTypeReactivated` without adding EventStore envelope fields to public contracts.
  - [x] Preserve the stable `ActivityTypeId` for all rename, metadata, deactivate, and reactivate changes.
  - [x] Reject duplicate create, missing/blank labels, `ActivityTypeScope.Unknown`, tenant/project scope mismatches, same-label no-op or explicit no-op behavior, and invalid billable metadata with typed domain outcomes.
- [x] Wire tenant authorization before any Activity Type write or read authority is used (AC: 1, 4)
  - [x] Reuse `TimesheetsAccessGuard`; keep ordering as tenant access, optional sibling references, then policy.
  - [x] Ensure tenant Activity Type commands do not rely on caller-supplied tenant, user, role, correlation, JWT, token, EventStore stream, sequence, or authorization body fields as authority.
  - [x] Do not validate Project or Work references for tenant-only commands. Project-scoped Activity Type behavior remains Story 1.6.
  - [x] Keep aggregates pure: no tenant lookup, HTTP calls, logging, clocks, filesystem, Dapr, EventStore envelope inspection, or UI shaping inside aggregate decisions.
- [x] Implement a replay-safe tenant Activity Type catalog projection (AC: 1, 2, 3, 5)
  - [x] Add projection code under `src/Hexalith.Timesheets.Projections/ActivityTypes`.
  - [x] Project tenant-scope events into `ActivityTypeCatalogReadModel` / `ActivityTypeCatalogItem` with `ProjectionFreshnessMetadata`.
  - [x] Include active/inactive status text or metadata that a UI can render without relying on color alone; do not remove the existing `IsActive` contract field unless replaced additively.
  - [x] Keep inactive Activity Types visible for historical/reporting reads while excluding them from future capture selection semantics.
  - [x] Make projection handlers idempotent and replay-safe using message identity/checkpoint concepts; duplicate events must not create duplicate catalog rows.
  - [x] Do not mutate projections as write authority; projections are derived from EventStore events only.
- [x] Refine contracts and metadata only where additive gaps are proven (AC: 1, 5, 6)
  - [x] Reuse existing contract files under `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes`, `Events/ActivityTypes`, `Models/ActivityTypeCatalog*.cs`, `Queries/ActivityTypes/ListActivityTypes.cs`, and `ValueObjects/ActivityTypeId.cs`.
  - [x] If status text, rejection codes, active-state enum, or catalog-selection metadata is missing, add it additively with `Unknown = 0` sentinels and string JSON converters where applicable.
  - [x] Extend `TimesheetsMetadataCatalog` rather than creating a second UI metadata vocabulary.
  - [x] Preserve Contracts as infrastructure-free: no Dapr, Aspire, ASP.NET Core, Fluent UI runtime, EventStore server, OpenTelemetry, Redis, Entity Framework, or direct persistence references.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` only if the tenant Activity Type catalog contract changes; keep it dependency-free and static.
- [x] Add focused tests for domain, projection, metadata, and boundaries (AC: 1-6)
  - [x] Add server tests for create, rename, metadata update, deactivate, reactivate, duplicate create, inactive transition, no-op/same-state handling, and typed rejection outcomes.
  - [x] Add authorization tests proving unauthorized, missing tenant, cross-tenant, stale, ambiguous, or unavailable tenant authority fails before any Activity Type event/projection change.
  - [x] Add projection tests proving fresh catalog read models, active/inactive visibility, inactive exclusion from future-capture selection metadata, replay equivalence, and duplicate-delivery idempotency.
  - [x] Add contract tests for JSON round trips, `Unknown = 0` enum sentinels, no server-controlled authority fields, and no copied sibling state.
  - [x] Add metadata tests proving the Activity Type Catalog descriptor uses FrontComposer projection/form patterns, status badges with text, keyboard-reachable command descriptors, and no Fluent UI runtime dependency.
  - [x] Extend architecture/privacy tests only where new source can log or expose sensitive material.
- [x] Verify build and focused test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only if a host/static metadata endpoint changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 execution pattern from Stories 1.1-1.4 and document the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded PRD shards from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`.
- Loaded UX shards from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-4-establish-evidence-retention-and-comment-sensitivity-policy.md`.
- Read current Timesheets Activity Type contracts, metadata, authorization, server registration, projection freshness, architecture tests, and test files listed in References.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance. Story 1.5 realizes FR10: tenant-level Activity Types with stable IDs, display labels, active/inactive state, optional billable-default metadata, historical reportability, and no historical fact rewrite.
- Story 1.5 is the tenant catalog foundation before Story 1.6 adds project-scoped Activity Types and before Story 1.7 records draft Time Entries against active Activity Types.
- This story is not a project Activity Type story. It must not introduce Project Reference validation or project catalog restriction behavior except to preserve contracts for later work.
- This story is not a full Time Entry capture story. It may expose tenant Activity Types for future capture, but it should not implement draft entry recording, approval, correction, Magic-Link Confirmation, reporting, ledger export, or AI metrics behavior.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateTenantActivityType.cs` already exposes `ActivityTypeId`, `Label`, and `DefaultBillableState`. It does not carry tenant/user/authority fields.
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateProjectActivityType.cs` already exists for later Story 1.6. Do not wire it into tenant-scope behavior in this story.
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/RenameActivityType.cs`, `UpdateActivityTypeMetadata.cs`, `DeactivateActivityType.cs`, and `ReactivateActivityType.cs` already exist as thin public contracts.
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeCreated.cs` already carries `ActivityTypeId`, `ActivityTypeScope`, optional `Project`, `Label`, and `DefaultBillableState`. Tenant-created events must use `ActivityTypeScope.Tenant` and `Project = null`.
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs` already carries `ActivityTypeId`, `Scope`, optional `Project`, `Label`, `IsActive`, and `DefaultBillableState`.
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogReadModel.cs` already carries `Items` plus `ProjectionFreshnessMetadata`.
- `src/Hexalith.Timesheets.Contracts/Queries/ActivityTypes/ListActivityTypes.cs` accepts `Scope` and optional `Project`; tenant catalog queries should require `Scope = ActivityTypeScope.Tenant` and `Project = null`.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` already declares `timesheets.command.activity-type-catalog` with create/rename/deactivate/reactivate actions. Extend it in place if active-state text, projection view metadata, or help text is missing.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` currently registers fail-closed tenant/resource/policy defaults with `TryAddSingleton`. New Activity Type domain services or handlers should follow replaceable registration patterns.
- `src/Hexalith.Timesheets.Projections` currently has only checkpoint/freshness primitives. Tenant Activity Type catalog projection code is expected new work.

### Architecture Constraints

- Timesheets durable domain state changes persist only through `Hexalith.EventStore`; no SQL, Redis, Dapr state-store, broker-backed CRUD, local JSON authoritative storage, or projection mutation as write authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Activity Type is an explicit baseline aggregate/catalog boundary for tenant-scoped and project-scoped activity type governance. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Contracts contain commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only. They must remain infrastructure-free. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Public contracts evolve additively and serialization-tolerantly. Do not remove or rename existing contract/event fields from Story 1.3/1.4. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. JWT claims and caller payloads are evidence only, not authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Projections must tolerate at-least-once delivery, duplicate events, replay, rebuild, and freshness/degraded/unavailable states. [Source: `_bmad-output/planning-artifacts/architecture.md#State-Management-Patterns`]
- Logs and traces must not contain command bodies, event payloads, comments, personal data, token values, secrets, or copied sibling display state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]

### Activity Type Domain Guidance

- Treat `ActivityTypeId` as the stable historical reference. Labels and default billable metadata can change; Time Entry facts continue to point at the ID.
- Tenant Activity Types are tenant-scope facts. Do not store or infer Project names, Project hierarchy, Work lifecycle, Party personal data, Tenant membership, rates, invoices, payroll, taxes, or revenue-recognition data.
- Create should reject duplicate IDs for the tenant catalog. Rename and metadata update should reject unknown IDs and inactive-state mismatches only if the selected domain semantics require that; deactivation must preserve historical visibility.
- Deactivation means unavailable for new Time Entry selection, not deleted and not hidden from reports.
- Reactivation is already present in public contracts and metadata from Story 1.3. If implemented now, it must be event-backed and tested; if deferred, the UI/action metadata must not imply reactivation works end to end.
- Business failures should be typed Timesheets domain outcomes/rejections, not infrastructure exceptions. Existing `TimesheetsRejectionCode` may need additive values such as duplicate/unknown Activity Type if the current vocabulary is too coarse.
- Do not add a second generic catalog framework. Implement the minimum Activity Type boundary needed for tenant catalog governance and later project catalog extension.

### Projection And Read Model Guidance

- The tenant catalog projection should derive from Activity Type events and expose `ActivityTypeCatalogReadModel`.
- Fresh projections can serve catalog reads. `Rebuilding`, `Stale`, `Unavailable`, and `Unknown` states must not be presented as fresh authority for trust-bearing selection.
- Duplicate delivery must not duplicate catalog items. Replay must produce the same catalog state as a single ordered event stream.
- Inactive items remain in `Items` for historical reportability and can be marked unavailable for new capture through metadata or a future selection-specific view.
- If the projection needs a checkpoint, use or extend `TimesheetsProjectionCheckpoint` rather than inventing ad hoc freshness or sequence state.

### FrontComposer / UX Guardrails

- Activity Type Catalog is an admin/settings surface reached from internal navigation; it uses FrontComposer and Fluent UI V5 patterns, not a custom portal.
- Use `FrontComposerProjectionView` and/or `FrontComposerGeneratedForm` metadata; use `FluentDataGrid` semantics for the catalog list.
- Status must include text. Active/inactive state cannot be color-only.
- Commands must be keyboard reachable and exposed as explicit verbs: `Create tenant Activity Type`, `Rename Activity Type`, `Update billable default`, `Deactivate Activity Type`, and only `Reactivate Activity Type` if supported.
- UI copy must be factual and bounded. Avoid invoice, payroll, rate, tax, revenue-recognition, productivity coaching, timer-app, or celebratory language.
- Do not add `Hexalith.Timesheets.UI` unless metadata-only support cannot meet AC6; expected implementation is contract metadata and projection/form descriptors.

### Previous Story Intelligence

- Story 1.4 added comment/evidence policy vocabulary, fail-closed policy defaults, safe copy, and diagnostics/privacy tests. Do not weaken those defaults while adding catalog behavior.
- Story 1.4 replaced the old deny-all policy evaluator registration with `TimesheetsEvidencePolicyEvaluator` and `TimesheetsEvidencePolicyOptions.FailClosedDefault`. Catalog command authorization should compose with this seam rather than bypass it.
- Story 1.4 added additive comment fields to `RecordTimeEntry`, `TimeEntryRecorded`, and `TimeEntryEvidenceReadModel`; do not remove or reshape those fields.
- Story 1.3 added the Activity Type contracts and metadata shell. Build on those files instead of recreating command/event/read-model types under another namespace.
- Story 1.3 removed an orphan duplicate enum. New Activity Type status or rejection vocabulary must be consumed by code/tests, not left as unused catalog decoration.
- Stories 1.1-1.4 consistently use `.slnx`, Central Package Management, xUnit v3, Shouldly, NSubstitute, warnings as errors, and focused per-project test runs.
- `dotnet test` has been blocked by VSTest socket permissions in this sandbox before; direct xUnit v3 execution with `dotnet run --project <test>.csproj --no-build` was the accepted fallback and must be documented if used.
- The worktree currently has an unrelated modified `_bmad-output/story-automator/orchestration-1-20260618-221411.md`; leave unrelated story-automator output alone.

### Latest Technical Information

- No package upgrade is required for Story 1.5. Use local pins in `Directory.Packages.props`: Aspire `13.4.5`, CommunityToolkit Aspire Dapr preview `13.4.0-preview.1.260602-0230`, Microsoft.Extensions `10.0.9` / `10.7.0`, OpenTelemetry `1.16.0` / instrumentation `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- NuGet Gallery confirms `xunit.v3` `3.2.2` is a stable package updated on 2026-01-14 and supports .NET 8.0 or later; newer `4.0.0` builds are prerelease. Keep the repository pin and do not upgrade test infrastructure as part of this story. [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]
- Dapr remains architecture-pinned to SDK `1.18.4`, but Story 1.5 should not add or upgrade Dapr packages. Domain and projection work should remain pure/fast unless later EventStore integration requires host-level wiring.
- Fluent UI V5 is a UX rule for future rendering. Contracts and metadata must remain runtime-neutral and must not take Fluent UI or FrontComposer runtime dependencies.

### Testing Standards

- Use xUnit v3 and Shouldly. Use NSubstitute only where mocks/fakes are needed.
- Run test projects individually; use `.slnx` for restore/build only.
- Keep fast tests deterministic and infrastructure-free. Activity Type aggregate/state/projection tests should not require Dapr, Aspire, EventStore server, containers, browser, network, or a Timesheets UI project.
- Negative-path coverage is mandatory:
  - unauthorized or unresolved tenant authority creates no event and no projection change;
  - duplicate create and unknown update/deactivate reject safely;
  - inactive Activity Types remain reportable but unavailable for future selection;
  - stale/rebuilding/unavailable catalog projections are not treated as fresh;
  - Contracts remain infrastructure-free and do not accept server-controlled authority fields;
  - logs/diagnostics do not include command bodies, event payloads, comments, personal data, token values, secrets, or sibling display data.

### Anti-Patterns To Prevent

- Do not implement Activity Types as direct CRUD rows, mutable projection writes, local JSON files, Dapr state-store authority, Redis, SQL, or broker-backed storage.
- Do not delete or hide inactive Activity Types from historical/reporting reads.
- Do not rewrite Time Entry facts when an Activity Type label or billable default changes.
- Do not make display labels unique across all scopes in a way that will block Story 1.6. Stable ID plus scope is the durable identity.
- Do not implement project-scoped Activity Type restrictions in this story.
- Do not trust caller-supplied tenant, user, authorization, role, JWT, token, correlation, EventStore stream, or sequence fields as authority.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.
- Do not add a dedicated Timesheets UI project or raw HTML/CSS admin page for this story.

## Project Structure Notes

- Expected new server files are under `src/Hexalith.Timesheets.Server/ActivityTypes` or `src/Hexalith.Timesheets.Server/Aggregates`, plus DI registration in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` only if needed.
- Expected projection files are under `src/Hexalith.Timesheets.Projections/ActivityTypes`.
- Expected contract changes, if any, are additive and stay under existing Activity Type command/event/model/query/value-object folders.
- Expected tests are under `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Contracts.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`.
- Expected public guidance updates, if any, belong in `src/Hexalith.Timesheets.Contracts/openapi/`. Do not use `docs/` as scratch space.
- No Timesheets UI project is expected for this story.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-5-Configure-Tenant-Activity-Types`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Functional-Requirements`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-Design-Requirements`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns--Consistency-Rules`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#5-3-Activity-Type-Catalogs`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#9-Data-Governance-and-Audit-Requirements`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/1-4-establish-evidence-retention-and-comment-sensitivity-policy.md#Previous-Story-Intelligence`]
- [Source: `README.md`]
- [Source: `docs/boundary-decision-record.md`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateTenantActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateProjectActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/RenameActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/UpdateActivityTypeMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/DeactivateActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/ReactivateActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeCreated.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/ActivityTypes/ListActivityTypes.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimesheetsProjectionCheckpoint.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/AuthorizationServiceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/ProjectionPlaceholderTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]
- [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19 03:02 CEST - Resolved `bmad-create-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 03:02 CEST - Confirmed user-requested Story 1.5 maps to sprint status key `1-5-configure-tenant-activity-types`, currently `backlog`.
- 2026-06-19 03:02 CEST - Loaded epics, architecture, PRD, UX, persistent project-context facts, previous Story 1.4, current source/test files, recent git history, and package metadata for pinned xUnit v3.
- 2026-06-19 03:02 CEST - Created story file and marked sprint status entry `1-5-configure-tenant-activity-types` as `ready-for-dev`.
- 2026-06-19 - Resolved `bmad-dev-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 - Marked Story 1.5 and sprint status `in-progress`; preserved existing `baseline_commit: 5bb8616`.
- 2026-06-19 - Implemented tenant Activity Type aggregate/state, tenant authorization-backed command service, replay-safe catalog projection, additive contract/status metadata, static OpenAPI catalog schema, and focused test coverage.
- 2026-06-19 - `dotnet test` was blocked by VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`); used direct xUnit v3 `dotnet run --project ... --no-build` pattern for affected test projects.
- 2026-06-19 - Validation passed: exact restore, warning-as-error build, direct xUnit Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story scope is tenant Activity Type governance only: tenant catalog domain state, EventStore-backed events, fail-closed tenant authorization, replay-safe projection, FrontComposer metadata, and focused tests.
- Existing Activity Type command/event/read-model contracts from Story 1.3 should be reused and extended only additively where implementation gaps are proven.
- Project-scoped Activity Types, project catalog restrictions, draft Time Entry capture, approval/correction behavior, Magic-Link Confirmation, reporting, exports, and UI package work remain outside this story.
- The story preserves EventStore-only authority, infrastructure-free Contracts, no copied sibling-owned state, fail-closed tenant gates, and projection freshness semantics.
- Added pure tenant Activity Type decision handling for create, rename, metadata update, deactivate, and reactivate with typed Timesheets rejection outcomes and no EventStore envelope fields in public contracts.
- Added fail-closed tenant Activity Type command/read authorization through `TimesheetsAccessGuard`, with tenant-only commands avoiding Project, Work, and Contributor reference validation.
- Added replay-safe tenant catalog projection with message-id deduplication, active/inactive status text, capture-selection availability metadata, and freshness mapping.
- Added additive catalog contract metadata, active-state enum, rejection codes, FrontComposer projection descriptor, and static OpenAPI schema updates.
- Validation passed with direct xUnit fallback after VSTest socket permissions blocked `dotnet test`.

### File List

- `_bmad-output/implementation-artifacts/1-5-configure-tenant-activity-types.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejectionCode.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/ActivityTypes/ActivityTypeProjectionEvent.cs`
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/ActivityTypeCatalogState.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/ActivityTypeCommandResult.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/ActivityTypeState.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeAggregate.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeCommandService.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/TimesheetsDomainResult.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/ActivityTypeCatalogProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAuthorizationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`

### Change Log

- 2026-06-19 - Implemented Story 1.5 tenant Activity Type governance and marked ready for review.
- 2026-06-19 - Senior Developer Review (AI): auto-fixed 4 MEDIUM findings (unused rejection vocabulary, dead state method, replay ordering, File List gaps); build `-warnaserror` clean and all focused test lanes green; status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot
**Date:** 2026-06-19
**Outcome:** Approve (after auto-fixes)

### Verification

- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` → 0 warnings, 0 errors.
- Focused test lanes (direct xUnit v3 fallback, VSTest sockets blocked): Contracts.Tests 30, Server.Tests 91, Projections.Tests 8, IntegrationTests 5 (2 intentional infrastructure skips), ArchitectureTests 16 — all passing.
- All six Acceptance Criteria verified as implemented; every `[x]` task cross-checked against source. No CRITICAL findings (no falsely-completed tasks, no missing ACs).

### Findings and Resolutions (all auto-fixed)

- **[MEDIUM] Unused rejection vocabulary** — `TimesheetsRejectionCode.ActivityTypeNoChange` was declared but never emitted (no-op paths use `TimesheetsDomainResult.NoOp()`), violating the story guardrail against unused catalog decoration. Removed the enum value.
- **[MEDIUM] Dead aggregate-state method** — `ActivityTypeCatalogState.Apply(TimesheetsRejection)` was a no-op with no callers that misleadingly implied rejections mutate state. Removed the method and its now-unused `using`.
- **[MEDIUM] Unenforced replay ordering** — `ActivityTypeProjectionEvent.SequenceNumber` was never read; the projection assumed input order. Now orders events by `SequenceNumber` before dedup, making replay deterministic per the architecture's replay-equivalence requirement and consuming the previously dead field.
- **[MEDIUM] File List gaps** — `ActivityTypeCatalogE2ETests.cs`, `HostMetadataEndpointTests.cs`, and `Hexalith.Timesheets.IntegrationTests.csproj` carried AC5/AC6 coverage but were undocumented. Added to the File List.

### Notes

- OpenAPI is a static, hand-maintained documentation artifact; references (`ProjectReference`, etc.) are simplified to strings following the existing pre-1.5 convention — not introduced as a new defect by this story.
- Projection attaches honest freshness metadata for non-fresh states and gating is delegated to `TimesheetsProjectionCheckpoint.CanServeReads`; this satisfies AC5 by design.
