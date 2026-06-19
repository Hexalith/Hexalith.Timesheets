---
baseline_commit: f7ca2bd
---

# Story 1.6: Configure Project Activity Types

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized project operator,
I want to define project-scoped Activity Types,
so that project-specific work can be categorized without copying Project state into Timesheets.

## Acceptance Criteria

1. Given an authorized project operator has access to a Project Reference, when they create a project-level Activity Type, then the Activity Type is scoped to the tenant and Project Reference, and Timesheets stores the stable Project ID only, not copied project name, hierarchy, lifecycle, or ownership state.
2. Given a project-level Activity Type exists, when its display label, active state, or billable-default metadata changes, then the change is persisted through EventStore as an auditable domain event, and historical Time Entry references remain tied to the stable Activity Type ID.
3. Given a project explicitly restricts its catalog, when a contributor selects an Activity Type for that project, then only allowed tenant-level and project-level Activity Types are available, and restricted or inactive Activity Types cannot be selected for new entries.
4. Given a Project Reference cannot be verified through the Projects boundary for a trust-bearing catalog change, when the command is submitted, then the command fails closed, and no project Activity Type is created or changed.
5. Given reports group by Activity Type, when tenant-level and project-level Activity Types share a display label, then reports group by stable Activity Type IDs and scopes, and display labels do not merge distinct catalog concepts.
6. Given the project Activity Type catalog UI is displayed, when users filter or inspect project-scoped entries, then the surface uses FrontComposer/Fluent UI V5 patterns, shows scope and active state text, preserves filters during drill-in/back navigation, and remains keyboard accessible.

## Tasks / Subtasks

- [x] Implement project-scoped Activity Type domain decisions without breaking tenant catalog behavior (AC: 1, 2, 5)
  - [x] Extend the existing Activity Type boundary under `src/Hexalith.Timesheets.Server/ActivityTypes`; prefer a focused `ProjectActivityTypeAggregate` or a carefully generalized Activity Type aggregate over duplicating the tenant catalog model.
  - [x] Handle `CreateProjectActivityType` by emitting `ActivityTypeCreated` with `ActivityTypeScope.Project`, the supplied stable `ProjectReference`, normalized label, active state, and default billable metadata.
  - [x] Reject blank labels, `BillableState.Unknown`, `ActivityTypeScope.Unknown`, duplicate IDs in the same tenant catalog state, tenant/project scope mismatches, missing Project references, and unknown project-scoped Activity Types with typed Timesheets domain outcomes.
  - [x] Preserve the stable `ActivityTypeId` for rename, billable-default update, deactivate, reactivate, and any project catalog restriction changes.
  - [x] Keep labels as display text only. Do not make display labels globally unique or use labels as reporting identity; stable ID plus scope and Project reference are the durable grouping keys.
  - [x] Do not copy Project name, hierarchy, lifecycle, ownership, planning state, manager names, or approver lists into Timesheets events, state, projections, OpenAPI, logs, or metadata.
- [x] Add a safe contract path for project-scoped mutations and catalog restrictions (AC: 2, 3, 4)
  - [x] Reuse `CreateProjectActivityType`; it already exists under `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes`.
  - [x] Before implementing project rename/update/deactivate/reactivate, resolve the current contract gap: existing `RenameActivityType`, `UpdateActivityTypeMetadata`, `DeactivateActivityType`, and `ReactivateActivityType` carry only `ActivityTypeId`.
  - [x] Do not authorize a project-scoped mutation from `ActivityTypeId` alone. Either add explicit project-scoped mutation commands, or add serialization-tolerant optional project/scope metadata to existing command contracts without breaking tenant callers.
  - [x] If adding contract fields or command types, keep them additive, infrastructure-free, and free of server-controlled tenant, actor, correlation, JWT, token, EventStore stream, sequence, message, causation, or authorization fields.
  - [x] Add the minimal project catalog restriction contract/event/read-model support needed for AC3. A safe shape is a project-scoped policy that can express unrestricted default behavior and a restricted list of allowed tenant Activity Type IDs plus project Activity Types.
  - [x] Ensure default selection behavior remains tenant plus project Activity Types unless the project restriction policy explicitly narrows the catalog.
- [x] Wire fail-closed project authorization and reference validation before project catalog writes are trusted (AC: 1, 4)
  - [x] Reuse `TimesheetsAccessGuard` and set `TimesheetsAuthorizationRequest.Project` for project-scoped catalog commands so `IProjectReferenceValidator` runs after tenant access and before policy.
  - [x] Keep ordering as tenant access, Project reference validation, then policy. Work and Contributor validators must not run for project Activity Type catalog commands unless the command truly carries those references.
  - [x] Register project Activity Type command service(s) with replaceable `TryAddSingleton` patterns in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Preserve fail-closed defaults: `DenyAllProjectReferenceValidator` must still deny unconfigured project authority in the composed server kernel.
  - [x] Return authorization denial without domain dispatch when tenant authority, project authority, stale projection, ambiguous authority, unavailable sibling authority, cross-tenant reference, disabled/archived project, or policy cannot be resolved.
- [x] Extend replay-safe catalog projection and selection semantics for project scopes (AC: 3, 5)
  - [x] Extend `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs` or add a `ProjectActivityTypeCatalogProjection` that derives from Activity Type events and any project restriction event.
  - [x] Preserve the current tenant-only projection semantics and tests: tenant catalog reads must not accidentally include project-scoped rows unless the query asks for a project-scoped catalog.
  - [x] For project catalog selection, return active allowed tenant-level rows plus active project-level rows for the requested `ProjectReference`; inactive or restricted rows remain visible for historical/reporting reads but must be marked unavailable for future capture.
  - [x] Include `ActivityTypeScope`, optional `ProjectReference`, `ActivityTypeId`, `StatusText`, `ActiveState`, `IsAvailableForCapture`, and `ProjectionFreshnessMetadata` in read models.
  - [x] Keep projection handlers idempotent and replay-safe using `ActivityTypeProjectionEvent.MessageId` deduplication and `SequenceNumber` ordering. Duplicate delivery must not duplicate rows or restriction entries.
  - [x] Do not mutate projections as write authority. Projections are derived from EventStore events only.
- [x] Update FrontComposer metadata, static contract documentation, and catalog UI descriptors (AC: 3, 6)
  - [x] Extend `TimesheetsMetadataCatalog` in place; do not create a second UI metadata vocabulary.
  - [x] Ensure the Activity Type Catalog projection descriptor exposes project filtering, scope text, Project reference where applicable, active/inactive text, projection freshness, and keyboard-reachable project catalog commands.
  - [x] The projection actions currently omit `create-project-activity-type`; add project actions where supported and keep command labels factual: `Create project Activity Type`, `Rename Activity Type`, `Update billable default`, `Deactivate Activity Type`, `Reactivate Activity Type`, and any restriction action with consequence-aware wording.
  - [x] Keep metadata runtime-neutral: no Fluent UI runtime, Blazor component, Dapr, Aspire, ASP.NET Core, EventStore server, Redis, Entity Framework, or direct persistence dependencies in Contracts.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` if contract/read-model shape changes. It should continue to document contracts only; no product endpoints are required.
- [x] Add focused tests for domain, authorization, projection, contracts, metadata, and boundaries (AC: 1-6)
  - [x] Add server aggregate tests for project create, rename/update/deactivate/reactivate or selected mutation strategy, duplicate IDs, scope mismatch, missing Project reference, stable ID preservation, and label collision across tenant/project scopes.
  - [x] Add authorization tests proving project catalog commands fail closed before domain dispatch for missing tenant, cross-tenant project, stale Project projection, ambiguous authority, unavailable Projects authority, invalid Project reference, disabled/archived Project, and policy denial.
  - [x] Add tests proving project commands call the Project validator and do not call Work or Contributor validators for project catalog-only changes.
  - [x] Add projection tests proving tenant-only reads remain tenant-only, project catalog reads include only the requested project rows plus allowed tenant rows, inactive/restricted rows are unavailable for capture, duplicate delivery is idempotent, and replay order uses `SequenceNumber`.
  - [x] Add contract tests for `CreateProjectActivityType` and any new project mutation/restriction contracts. Adjust authority-field assertions so `ProjectReference.ProjectId` is allowed where it is the actual stable Project reference, while copied Project display/lifecycle/ownership fields and server-controlled authority fields remain forbidden.
  - [x] Add metadata/OpenAPI tests proving project catalog fields/actions are exposed and Contracts remains infrastructure-free.
  - [x] Extend architecture/privacy tests only where new source can log, expose, or serialize copied sibling-owned state.
- [x] Verify build and focused test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests if host metadata or OpenAPI/static endpoint behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 execution pattern from Stories 1.1-1.5 and document the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded PRD content from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`.
- Loaded UX content from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-5-configure-tenant-activity-types.md`.
- Read current Timesheets Activity Type contracts, aggregate/service/projection code, authorization/reference validators, metadata catalog, OpenAPI artifact, and focused tests listed in References.
- Reviewed recent git history through commit `f7ca2bd feat(story-1.5): Configure Tenant Activity Types`.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance with Project/Work references, Activity Type catalogs, Party attribution, EventStore persistence, and module boundary enforcement.
- Story 1.6 realizes FR2 and FR11: project-level Activity Types are scoped to a tenant and Project Reference, Project References are validated through the Projects boundary, tenant-level types remain available unless restricted, and reports group by stable IDs/scopes rather than labels.
- Story 1.6 builds directly on Story 1.5's tenant Activity Type catalog foundation. Do not reimplement tenant catalog behavior or replace the existing Activity Type contracts when an additive extension is enough.
- This story is not draft Time Entry capture. It may expose catalog selection semantics for Story 1.7, but it should not implement `RecordTimeEntry`, approvals, corrections, Magic-Link Confirmation, reporting rollups, exports, or AI metric behavior.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateProjectActivityType.cs` already exists and carries `ActivityTypeId`, `ProjectReference`, label, and default billable state.
- `src/Hexalith.Timesheets.Contracts/References/ProjectReference.cs` exposes only `ProjectId`; this is the allowed stable Project identifier. Do not add Project name, hierarchy, lifecycle, owner, manager, or approver display data.
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeCreated.cs` already carries `ActivityTypeScope` and optional `ProjectReference`. Project creates should set `Scope = ActivityTypeScope.Project` and `Project` to the stable reference.
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs` already carries scope, optional project, active state text, capture availability, and billable default. Reuse it unless a proven additive gap exists.
- `src/Hexalith.Timesheets.Contracts/Queries/ActivityTypes/ListActivityTypes.cs` accepts `Scope` and optional `Project`; project catalog reads should require a Project when querying project scope or project selection context.
- `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeAggregate.cs` only permits tenant-scoped decisions and rejects project-scoped state as a scope mismatch.
- `src/Hexalith.Timesheets.Server/ActivityTypes/ActivityTypeCatalogState.cs` can already apply project-scoped `ActivityTypeCreated` events because it stores `Scope` and `Project`.
- `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeCommandService.cs` authorizes tenant writes and catalog reads; it does not set `TimesheetsAuthorizationRequest.Project`, so it must not be reused blindly for project catalog writes.
- `src/Hexalith.Timesheets.Server/References/IProjectReferenceValidator.cs` and `DenyAllProjectReferenceValidator.cs` already exist. Story 1.6 should use this adapter boundary rather than calling Projects directly from aggregate code.
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs` currently ignores project-scoped `ActivityTypeCreated` events. Preserve that for tenant-only reads and add explicit project catalog projection/selection behavior.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` already includes `create-project-activity-type` in the command descriptor, but the projection descriptor currently exposes only tenant-oriented workflow actions. Update deliberately.
- `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs` has an authority-field helper that forbids `ProjectReference.ProjectId`; this was acceptable for tenant-only catalog tests but must be corrected for project commands so stable Project references are allowed and copied Project state remains forbidden.

### Architecture Constraints

- Timesheets durable domain state changes persist only through Hexalith.EventStore. Do not introduce SQL, Redis, Dapr state-store, local JSON, broker-backed CRUD, or projection mutation as authoritative storage. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Activity Type is a baseline aggregate/catalog boundary for both tenant-scoped and project-scoped governance. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Timesheets stores stable Tenant, Party, Project, and Work references only. Project validation fails closed for trust-bearing writes when authority cannot be resolved. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Tenant/resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. JWT claims and caller payloads are evidence only, not authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Public contracts evolve additively and remain infrastructure-free. Do not remove or rename existing fields from Stories 1.3-1.5. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Projections must tolerate at-least-once delivery, duplicate events, replay, rebuild, stale, degraded, and unavailable states. [Source: `_bmad-output/planning-artifacts/architecture.md#State-Management-Patterns`]
- Logs and traces must not contain command bodies, event payloads, comments, personal data, tokens, secrets, or copied sibling display state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]

### Project Activity Type Domain Guidance

- Project Activity Types are tenant-scoped facts with a stable Project Reference, not a copy of Projects-owned state.
- `ActivityTypeId` remains the stable historical reference. Labels and default billable metadata can change; historical Time Entry facts continue to point at the ID and scope.
- Inactive means unavailable for new Time Entry selection, not deleted and not hidden from reports.
- Project restrictions affect future selection semantics only. They must not rewrite tenant Activity Types, project Activity Types, or historical Time Entry facts.
- If a project restricts its catalog, the allowed selection set must be explicit and replayable from events or contract state. Do not infer restrictions from labels or UI filters.
- Business failures should be typed Timesheets domain outcomes/rejections, not infrastructure exceptions.
- Aggregates remain pure: no tenant lookup, Project API calls, HTTP, logging, clocks, filesystem, Dapr, EventStore envelope inspection, or UI shaping inside aggregate decisions.

### Authorization And Boundary Guidance

- `TimesheetsAccessGuard` already supports `ProjectReference` validation through `TimesheetsAuthorizationRequest.Project`.
- `DenyAllProjectReferenceValidator` is the correct unconfigured default. Tests should prove the composed kernel fails closed until a Projects adapter is supplied.
- Project catalog writes require both tenant authority and Project authority. A caller's stable `ProjectId` is input to validation, not proof of authority.
- Do not treat stale, unavailable, ambiguous, disabled/archived, invalid, or tenant-mismatched Projects authority as usable for project catalog writes.
- Do not validate Work or Contributor references for project Activity Type catalog-only commands.

### Projection And Read Model Guidance

- Tenant-only catalog reads must remain usable for tenant governance and must not include project rows by accident.
- Project catalog reads should support future Time Entry selection by returning available active tenant rows plus available active project rows, with restriction policy applied when present.
- Historical/reporting visibility differs from capture availability. Inactive or restricted rows can remain visible in read models while `IsAvailableForCapture` is false.
- Reports and future ledgers must be able to group by stable `ActivityTypeId`, `ActivityTypeScope`, and `ProjectReference` where scope is project. Display labels are presentation only.
- Freshness metadata is part of the trust contract. Stale/rebuilding/unavailable projections must not be treated as fresh catalog authority for trust-bearing selection.

### FrontComposer / UX Guardrails

- Activity Type Catalog is an internal admin/settings surface inside `FrontComposerShell`; do not create a parallel Timesheets UI shell.
- Use FrontComposer projection/form metadata and Fluent UI V5-compatible descriptors. A dedicated `Hexalith.Timesheets.UI` project is not expected for this story.
- Project catalog UI must show scope and active/inactive state as text, not color alone.
- Preserve filters during drill-in/back navigation. Project filter state is part of the expected catalog workflow.
- Keep copy factual and bounded. Avoid invoice, payroll, rate, tax, revenue-recognition, productivity, timer-app, or celebratory language.

### Previous Story Intelligence

- Story 1.5 implemented tenant Activity Type governance with `TenantActivityTypeAggregate`, `TenantActivityTypeCommandService`, `ActivityTypeCatalogState`, `TenantActivityTypeCatalogProjection`, active-state metadata, OpenAPI schema updates, and focused tests.
- Story 1.5 deliberately left project-scoped Activity Type behavior for Story 1.6. Do not force project behavior into tenant-only methods without explicit scope checks.
- Story 1.5's review fixed unused rejection vocabulary and dead state methods. New rejection codes, state methods, metadata fields, or restriction concepts must be consumed by tests and implementation.
- Story 1.5's projection now orders by `SequenceNumber` and deduplicates by `MessageId`; keep that replay/idempotency pattern.
- Story 1.5 proved `dotnet test` may be blocked by VSTest socket permissions in this sandbox; direct xUnit v3 execution with `dotnet run --project <test>.csproj --no-build` is the accepted fallback when documented.
- The worktree had an unrelated modified `_bmad-output/story-automator/orchestration-1-20260618-221411.md` during story creation. Leave unrelated story-automator output alone unless the user explicitly asks to touch it.

### Latest Technical Information

- No package upgrade is required for Story 1.6. Use the repository pins in `Directory.Packages.props`: Aspire `13.4.5`, CommunityToolkit Aspire Dapr preview `13.4.0-preview.1.260602-0230`, Microsoft.Extensions `10.0.9` / `10.7.0`, OpenTelemetry `1.16.0` / instrumentation `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- Dapr remains architecture-pinned to SDK `1.18.4`, but this story should not add or upgrade Dapr packages. Domain and projection work should remain pure unless host-level EventStore integration proves otherwise.
- Fluent UI V5 is a UX rule for future rendering. Contracts and metadata remain runtime-neutral and must not take Fluent UI or FrontComposer runtime dependencies.

### Testing Standards

- Use xUnit v3 and Shouldly. Use NSubstitute only where fakes/mocks are needed.
- Run test projects individually; use `.slnx` for restore/build only.
- Keep fast tests deterministic and infrastructure-free. Activity Type aggregate, authorization, projection, contract, and metadata tests should not require Dapr, Aspire, EventStore server, containers, browser, network, or a Timesheets UI project.
- Negative-path coverage is mandatory:
  - unauthorized or unresolved tenant/project authority creates no event and no projection change;
  - duplicate create and unknown update/deactivate reject safely;
  - inactive Activity Types remain reportable but unavailable for future selection;
  - restricted Activity Types are not available for capture when project policy excludes them;
  - tenant/project labels can collide without merging report identities;
  - stale/rebuilding/unavailable catalog projections are not treated as fresh;
  - Contracts remain infrastructure-free and do not accept server-controlled authority fields;
  - Project references remain stable IDs only and do not copy Projects-owned state.

### Anti-Patterns To Prevent

- Do not implement project Activity Types as direct CRUD rows, mutable projection writes, local JSON files, Dapr state-store authority, Redis, SQL, or broker-backed storage.
- Do not copy Project display names, hierarchy, lifecycle, ownership, manager/approver data, planning state, or Work lifecycle state into Timesheets.
- Do not authorize project-scoped mutation from an `ActivityTypeId` alone.
- Do not use Activity Type display labels as unique identity or reporting grouping keys.
- Do not delete or hide inactive Activity Types from historical/reporting reads.
- Do not rewrite Time Entry facts when an Activity Type label, billable default, active state, or project restriction changes.
- Do not weaken tenant Activity Type behavior or tests while adding project scope.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.
- Do not add a dedicated Timesheets UI project or raw HTML/CSS admin page for this story.

## Project Structure Notes

- Expected server work belongs under `src/Hexalith.Timesheets.Server/ActivityTypes`, `src/Hexalith.Timesheets.Server/Authorization`, and `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` only where needed.
- Expected projection work belongs under `src/Hexalith.Timesheets.Projections/ActivityTypes`.
- Expected contract work belongs under existing Activity Type command/event/model/query/value-object folders and remains additive.
- Expected metadata work belongs in `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`.
- Expected static API documentation updates, if any, belong in `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`.
- Expected tests are under `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests` only if metadata endpoint/static contract behavior changes.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-6-Configure-Project-Activity-Types`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Functional-Requirements`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-Design-Requirements`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Implementation-Patterns--Consistency-Rules`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#5-3-Activity-Type-Catalogs`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#8-Boundaries-and-Integrations`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/1-5-configure-tenant-activity-types.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateProjectActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateTenantActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/RenameActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/UpdateActivityTypeMetadata.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/DeactivateActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/ReactivateActivityType.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeCreated.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/ActivityTypes/ListActivityTypes.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/References/ProjectReference.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeAggregate.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ActivityTypes/TenantActivityTypeCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ActivityTypes/ActivityTypeCatalogState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAuthorizationRequest.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IProjectReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/DenyAllProjectReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ActivityTypes/ActivityTypeProjectionEvent.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/ActivityTypeCatalogProjectionTests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19 - Resolved `bmad-create-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 - Confirmed user-requested Story 1.6 maps to sprint status key `1-6-configure-project-activity-types`, currently `backlog`.
- 2026-06-19 - Loaded epics, architecture, PRD, UX, persistent project-context facts, previous Story 1.5, current Activity Type source/test files, recent git history, and local package pins.
- 2026-06-19 - Created story file and marked sprint status entry `1-6-configure-project-activity-types` as `ready-for-dev`.
- 2026-06-19 - Resolved `bmad-dev-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 - Preserved existing `baseline_commit: f7ca2bd`; marked story and sprint status `in-progress`.
- 2026-06-19 - Confirmed red phase with focused Server.Tests compile failure for missing project aggregate/service/contract types.
- 2026-06-19 - Implemented explicit project Activity Type command contracts, project restriction event, `ProjectActivityTypeAggregate`, and `ProjectActivityTypeCommandService`.
- 2026-06-19 - Extended project catalog projection selection with replay-safe restriction handling while preserving tenant-only projection semantics.
- 2026-06-19 - Updated FrontComposer metadata and static OpenAPI artifact for project catalog filters/actions and stable `ProjectReference.ProjectId`.
- 2026-06-19 - `dotnet test` is blocked by VSTest socket permissions in this sandbox; used direct xUnit v3 execution after compile/build.
- 2026-06-19 - Validation passed: restore, full solution build with `-warnaserror`, Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story scope is project Activity Type governance only: project-scoped catalog domain decisions, Project reference validation, fail-closed project authorization, catalog restriction/selection semantics, projection/read-model updates, FrontComposer metadata, and focused tests.
- Tenant Activity Type behavior from Story 1.5 must remain intact and covered.
- Draft Time Entry capture, approval/correction behavior, Magic-Link Confirmation, reporting rollups, exports, AI metrics, and dedicated UI package work remain outside this story.
- Added project-scoped Activity Type domain decisions that emit existing Activity Type events with `ActivityTypeScope.Project` and stable `ProjectReference`, normalize labels, preserve stable IDs, reject duplicates/scope mismatches/unknown project rows, and keep tenant catalog behavior unchanged.
- Added explicit project-scoped mutation contracts plus a replayable project catalog restriction contract/event so project mutations are never authorized from `ActivityTypeId` alone.
- Added project command service authorization that sets `TimesheetsAuthorizationRequest.Project`, preserving tenant -> Project reference validation -> policy ordering and avoiding Work/Contributor validation for catalog-only commands.
- Extended catalog projection behavior with `ProjectForProject(...)` to include tenant rows plus requested project rows, apply explicit restrictions, keep inactive/restricted rows visible but unavailable for capture, and retain MessageId/SequenceNumber replay safety.
- Updated metadata/OpenAPI documentation for project filters, Project reference, project actions, restriction action, and contract schemas without adding runtime UI or infrastructure dependencies.
- Added focused domain, authorization, projection, contract, metadata/OpenAPI, runtime registration, architecture, and integration validation coverage.

### File List

- `_bmad-output/implementation-artifacts/1-6-configure-project-activity-types.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/ConfigureProjectActivityTypeCatalogRestriction.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/DeactivateProjectActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/ReactivateProjectActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/RenameProjectActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/UpdateProjectActivityTypeMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ProjectActivityTypeCatalogRestrictionConfigured.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/ProjectActivityTypeAggregate.cs`
- `src/Hexalith.Timesheets.Server/ActivityTypes/ProjectActivityTypeCommandService.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/ActivityTypeCatalogProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAuthorizationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`

### Change Log

- 2026-06-19 - Implemented Story 1.6 project Activity Type governance, project catalog restrictions, fail-closed project authorization, projection selection semantics, metadata/OpenAPI updates, and focused validation tests.
- 2026-06-19 - Senior Developer Review (AI) by Jerome: auto-fixed review findings (added AC5 label-collision projection coverage, SequenceNumber-ordering replay coverage, project missing/null reference rejection coverage, project-command tenant fail-closed-before-project-validation coverage; de-duplicated projection lifecycle application). Build green with `-warnaserror`; Server.Tests 124, Projections.Tests 13, Contracts.Tests 30, ArchitectureTests 16, IntegrationTests 5 (2 reserved skips) all pass. Story → done.

## Senior Developer Review (AI)

**Reviewer:** Jerome
**Date:** 2026-06-19
**Outcome:** Approved (auto-fix mode) — Story → done

### Summary

Adversarial review of Story 1.6 against the six Acceptance Criteria, all `[x]` task claims, and the full File List versus git reality. The implementation is sound: all six ACs are genuinely implemented (project-scoped domain decisions emit existing Activity Type events with `ActivityTypeScope.Project` and a stable `ProjectReference`; rename/metadata/deactivate/reactivate preserve the stable `ActivityTypeId`; project authorization is fail-closed with tenant → Project reference → policy ordering and no Work/Contributor validation; the catalog projection applies replayable restrictions and keeps inactive/restricted rows visible-but-unavailable; FrontComposer metadata and the OpenAPI artifact were extended additively and remain infrastructure-free). The File List matches `git status` exactly (no false claims, no undocumented source changes), the solution builds with `-warnaserror` at 0 warnings, and every affected test lane passes. No CRITICAL or HIGH issues were found.

Findings were limited to test-coverage gaps for explicitly-claimed `[x]` subtasks plus one maintainability issue, all auto-fixed.

### Findings (all auto-fixed)

| Sev | Finding | Resolution |
| --- | --- | --- |
| MEDIUM | AC5 subtask "label collision across tenant/project scopes" marked `[x]` but no test proved tenant and project Activity Types with identical labels stay distinct (grouped by stable ID + scope, not merged). | Added `Project_catalog_keeps_tenant_and_project_rows_distinct_when_display_labels_collide` to `ActivityTypeCatalogProjectionTests`. |
| MEDIUM | Test subtask claimed "missing Project reference" coverage and a test was even named for it, but no assertion exercised the `ValidateProject(null)` rejection path. | Added `Project_commands_reject_missing_project_reference_with_typed_validation_outcome` to `ActivityTypeAggregateTests` (create/rename/deactivate/restriction). |
| MEDIUM | Authorization subtask claimed project commands fail closed for "missing tenant", but no test fed the `ProjectActivityTypeCommandService` a failing tenant validator (proving tenant-before-project ordering and that the Project validator is not even called). | Added theory `Project_activity_type_write_fails_closed_on_tenant_authority_before_project_validation` to `ActivityTypeAuthorizationTests`. |
| LOW | Claimed "replay order uses `SequenceNumber`" was not directly proven with out-of-order delivery. | Added `Projection_orders_events_by_sequence_number_regardless_of_delivery_order`. |
| LOW | `Apply` and `ApplyProjectSelection` in `TenantActivityTypeCatalogProjection` duplicated the four lifecycle-transition switch arms and the create-item construction (drift risk). | Extracted `CreateItem(...)` and `ApplyLifecycleTransition(...)` shared helpers; behavior unchanged, confirmed by the full projection suite. |

### Verification

- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` → Build succeeded, 0 warnings, 0 errors.
- Direct xUnit v3 execution (VSTest socket blocked in sandbox, per Story 1.1-1.5 pattern): Server.Tests 124/0, Projections.Tests 13/0, Contracts.Tests 30/0, ArchitectureTests 16/0, IntegrationTests 5/0 (2 reserved skips).
