---
baseline_commit: f4365ea9d72c6fbcc48eac555a4d2258421c41b3
---

# Story 1.3: Publish Time Capture API Contracts and FrontComposer Metadata

Status: done

## Story

As an API, SDK, or UI consumer,
I want stable Timesheets capture and catalog contracts with generated UI metadata,
so that integrations can record and manage time evidence without learning EventStore internals.

## Acceptance Criteria

1. Given capture and catalog commands, events, queries, value objects, and read models are published, when consumers reference `Hexalith.Timesheets.Contracts`, then contracts expose Timesheets concepts only, and EventStore envelope mechanics, aggregate internals, projection rebuild mechanics, and infrastructure types are hidden.
2. Given a command or query accepts tenant, user, correlation, or authorization context, when the public contract is reviewed, then server-controlled context is not accepted as caller authority, and tenant/resource authority is resolved by host/server policy instead.
3. Given API or SDK consumers use Timesheets capture contracts, when they submit commands or queries, then stable DTOs support Time Entry capture, Activity Type catalog management, AI metrics, and Time Entry evidence reads, and feature-specific metadata can be extended by later stories without breaking additive contract evolution.
4. Given FrontComposer metadata foundations are generated or registered for capture and catalog workflows, when internal UI surfaces are composed, then they use FrontComposer command/projection patterns and Fluent UI V5-compatible metadata, and feature stories remain responsible for their own detailed UI fields, validation states, and workflow-specific metadata.
5. Given contract and metadata tests run, when package boundary, consumer-driven contract, and UI conformance checks execute, then they prove Contracts remains infrastructure-free, no inline package versions are added, Fluent UI V4 components are not introduced, and public metadata smoke tests pass.
6. Given API documentation or OpenAPI artifacts are produced, when consumers inspect the public surface, then docs describe Timesheets commands, queries, states, and validation outcomes, and they do not expose EventStore internals or imply Timesheets owns Party, Project, Work, Tenant, invoice, payroll, rate, or revenue state.

## Tasks / Subtasks

- [x] Define infrastructure-free capture and catalog contract primitives in `Hexalith.Timesheets.Contracts` (AC: 1, 2, 3)
  - [x] Add capability folders under `src/Hexalith.Timesheets.Contracts`: `Commands/TimeEntries`, `Commands/ActivityTypes`, `Events/TimeEntries`, `Events/ActivityTypes`, `Events/Rejections`, `Queries/TimeEntries`, `Models`, `State`, `ValueObjects`, and `Ui` as needed.
  - [x] Add stable value objects/enums for Time Entry ID, Activity Type ID, target kind/reference, contributor category, approval state, billable state, activity type scope, projection freshness/trust state, and AI metric availability.
  - [x] Use explicit units in type/property names for durations and AI metrics: whole minutes for human/external duration, separate wall-clock/runtime/billable effort fields, and provider-reported input/output/total token counts.
  - [x] Include `Unknown = 0` enum sentinels and serialization-tolerant defaults where enum contracts are exposed.
  - [x] Keep stable references as IDs only; do not add Party display name/contact/profile fields, Project/Work names, hierarchy/lifecycle fields, Tenant membership fields, rates, invoice totals, payroll, taxes, or revenue-recognition fields.
- [x] Publish foundational command/query/event DTOs without implementing domain behavior yet (AC: 1, 2, 3)
  - [x] Add a `RecordTimeEntry` command contract that represents exactly one target reference by construction, not two optional `ProjectReference`/`WorkReference` properties that could both be set.
  - [x] Add foundational Activity Type catalog command contracts for tenant-scoped and project-scoped catalog management: create, rename/update metadata, deactivate/reactivate where required for the published surface.
  - [x] Add event contracts implied by this story's public surface, such as `TimeEntryRecorded`, `ActivityTypeCreated`, `ActivityTypeRenamed` or updated, `ActivityTypeDeactivated`, and typed rejection payloads/outcomes for validation/authority failures.
  - [x] Add query/read contracts for Time Entry evidence reads and Activity Type catalog reads with projection freshness metadata, stable IDs, and correction/approval placeholders that later stories can extend additively.
  - [x] Do not add EventStore envelope fields (`messageId`, `causationId`, sequence, stream, aggregate internals), host authorization context, `ClaimsPrincipal`, JWT/token fields, or caller-supplied tenant/user authority fields to public command/query payloads.
- [x] Replace the generic metadata placeholder with extensible FrontComposer-compatible descriptors (AC: 3, 4, 5)
  - [x] Extend or replace `TimesheetsMetadataCatalog` and `TimesheetsMetadataDescriptor` with typed descriptors for command surfaces, projection surfaces, fields, actions, and state badges using Timesheets-owned enums/strings only.
  - [x] Register baseline descriptors for `Record time`, `Activity Type Catalog`, and `Time Entry Evidence` surfaces.
  - [x] Metadata must declare FrontComposer/Fluent UI intent without referencing Fluent UI, FrontComposer runtime, ASP.NET Core, Dapr, EventStore server, or generated UI implementation packages from Contracts.
  - [x] Keep feature details minimal and additive: later stories own detailed field validation, workflow-specific metadata, generated components, and any UI project creation.
- [x] Produce public contract documentation or OpenAPI-ready artifacts (AC: 1, 6)
  - [x] Add `src/Hexalith.Timesheets.Contracts/openapi/` and/or `docs/api/` artifacts that describe command/query names, DTO fields, state vocabularies, validation/rejection outcomes, metadata descriptors, and explicit non-ownership boundaries.
  - [x] Documentation must state that tenant/user/correlation/authorization context is server-derived and not accepted as caller authority.
  - [x] Documentation must avoid EventStore envelope mechanics, aggregate internals, projection rebuild mechanics, raw stream browsing, invoice/payroll/rate/revenue language, and copied sibling state.
  - [x] If a machine-readable artifact is added, keep it static or generated from Contracts without adding Swashbuckle/OpenAPI package dependencies unless an implementation need is proven and architecture is updated.
- [x] Update the host metadata smoke surface without turning it into a product API (AC: 4, 5, 6)
  - [x] Extend `/metadata/timesheets` only with correlation-safe module, contract version, capability, and metadata descriptor names.
  - [x] Do not expose tenant, Party, Project, Work, Time Entry, token, secret, command body, event payload, or sibling-owned display data through the metadata endpoint.
  - [x] Keep `Program.cs` thin; no domain rules, command dispatch, aggregate loading, or real capture endpoints belong in this story.
- [x] Add contract, architecture, and metadata tests (AC: 1-6)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs` or add focused contract tests verifying command/read model shape, exactly-one target modeling, AI metric units/unavailable semantics, enum `Unknown` sentinels, metadata descriptors, and additive-friendly DTOs.
  - [x] Extend architecture tests to prove Contracts remains infrastructure-free, no inline package versions are introduced, no Fluent UI V4 components/packages appear, and public contracts do not expose server-controlled authority fields or EventStore envelope mechanics.
  - [x] Extend `HostMetadataEndpointTests` to cover the metadata endpoint's new correlation-safe contract/descriptor information.
  - [x] Use xUnit v3 and Shouldly. Do not introduce Moq, FluentAssertions, raw `Assert.*`, or broad reflection helpers that make failures hard to diagnose.
- [x] Verify build and focused test lanes (AC: 5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected fast tests individually: Contracts.Tests, ArchitectureTests, and IntegrationTests if the metadata endpoint smoke test remains static/no-infrastructure.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 execution pattern from Story 1.2 and document the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded nested PRD sources: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`.
- Loaded nested UX sources: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-2-enforce-tenant-and-resource-authorization-gates.md`.
- Read current contract, host metadata, architecture test, contract test, README, and boundary-decision files listed in the references.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance. Story 1.3 is the public-surface foundation that lets later feature stories implement capture, catalog governance, evidence reads, and FrontComposer UI without inventing contract shapes ad hoc.
- This story realizes FR1, FR10, FR11, FR15, FR21, FR22, and FR23: Time Entry capture, Activity Type catalogs, AI effort metrics, EventStore-backed domain evidence, command/query contracts, and bounded references to sibling modules.
- The story is intentionally contract-first. It should not implement aggregate behavior, persistence, real command handlers, real projection stores, or a Timesheets UI project. Later stories 1.5-1.9 own catalog behavior, capture behavior, read models, and AI projection behavior.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` currently contains only two generic descriptors: `timesheets.frontcomposer.entry-points` and `timesheets.eventstore.domain-service`. Story 1.3 should turn this into a useful infrastructure-free catalog for specific capture/catalog/evidence surfaces.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataDescriptor.cs` is a small sealed record with `Name`, `Description`, and `Scope`. Extend it only if the new metadata surface needs typed fields/actions/states; keep it free of UI runtime types.
- `src/Hexalith.Timesheets.Contracts/References/*Reference.cs` already models stable Tenant, Party, Project, and Work IDs. Reuse these rather than creating duplicate raw ID types for sibling references.
- `src/Hexalith.Timesheets/Program.cs` exposes `/metadata/timesheets` with module, EventStore domain, and registration assembly metadata. It is safe for a smoke endpoint; do not turn it into a command/query implementation.
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs` currently verifies stable references and the two metadata descriptors. Extend this style for new contract shapes.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs` and `ScaffoldGovernanceTests.cs` already enforce dependency direction, no inline package versions, no direct persistence patterns, and no Fluent UI V4 component package. Add targeted checks instead of creating duplicate governance suites.
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` statically verifies host metadata endpoint shape. Extend it if `/metadata/timesheets` grows.

### Architecture Constraints

- Contracts live in `src/Hexalith.Timesheets.Contracts` and must not reference infrastructure packages. They may contain commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only. [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- Public command/query contracts hide EventStore envelope mechanics. Domain business failures are typed domain outcomes/rejections; transport errors use ProblemDetails only where HTTP surfaces exist. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Event and contract evolution must be additive and serialization-tolerant. Do not remove or rename previously emitted event fields once published. [Source: `_bmad-output/planning-artifacts/epics.md#NonFunctional-Requirements`]
- Store stable references only. Timesheets must not copy Party personal data, Project state, Work lifecycle/planning state, Tenant membership state, rates, invoice data, payroll data, taxes, or revenue-recognition decisions. [Source: `docs/boundary-decision-record.md`]
- Timesheets durable domain changes persist through Hexalith.EventStore only. This story should define contracts and docs, not direct SQL/Redis/Dapr state/broker-backed CRUD, not local JSON authoritative storage, and not projection mutation as write authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- FrontComposer metadata must support generated command/projection surfaces and Fluent UI V5-compatible composition, but Contracts must not take a Fluent UI, FrontComposer runtime, ASP.NET Core, Dapr, or EventStore server dependency. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`; `Hexalith.FrontComposer/_bmad-output/project-context.md#Critical-Implementation-Rules`]

### Contract Modeling Guidance

- Model exactly one Time Entry target by construction. Prefer a single `TimeEntryTargetReference`/target-kind value object over separate optional project/work properties that allow both or neither.
- Human/external duration is whole minutes. AI metrics are separate multi-unit evidence: wall-clock, model/tool runtime, billable effort, and provider-reported input/output/total tokens. Missing provider token metrics must be represented as unavailable/unknown/null, never `0`.
- Activity Types need stable IDs, display labels, active/inactive state, optional billable-default metadata, tenant/project scope, and historical reportability semantics. Renames do not rewrite historical Time Entry facts.
- Time Entry evidence reads should expose stable ID, target reference, Contributor Party reference, Activity Type ID/scope, date, duration, billable state, approval state, contributor category, optional AI metrics, correction/lineage placeholders, and projection freshness metadata.
- Validation/rejection contracts should be specific enough for consumers to handle field errors and policy failures, but safe enough not to disclose protected tenant, contributor, target, period, or entry details.
- If a property name looks like server authority (`TenantId`, `UserId`, `CorrelationId`, `Authorization`, `Claims`, `Roles`, `Jwt`, `Token`), stop and justify it. In this story, public caller payloads should not accept those fields as authority.

### FrontComposer / UX Guardrails

- Internal surfaces run in `FrontComposerShell`. This story should publish metadata foundations only; it should not add a parallel shell, custom portal, custom theme, raw HTML-first UI, JavaScript, custom CSS, or a Timesheets UI project.
- Metadata descriptors should declare the intended patterns: `FrontComposerGeneratedForm` for `Record time` and Activity Type commands, `FrontComposerProjectionView` or grid-friendly projection descriptors for catalog/evidence reads, and text-bearing status badge metadata for approval, billable, contributor category, correction, and projection freshness states.
- UI copy in descriptors/docs must be factual and consequence-aware: examples include `Record time`, `Activity Type Catalog`, `Time Entry Evidence`, `Projection is rebuilding`, and `Authority cannot be resolved.` Avoid timer-app, invoice, payroll, rate, tax, or revenue language.
- Later UI stories own detailed field ordering, validation states, generated components, dialogs, grids, accessibility screenshots, and any dedicated UI project.

### Previous Story Intelligence

- Story 1.2 is done and added `TimesheetsAccessGuard`, fail-closed tenant/resource/policy adapters, expanded reference validation states, UI action policy outcomes, and tests. Do not regress server-only `TimesheetsRequestContext` or move authorization context into Contracts.
- Story 1.2 review found the UI-action authorization path was initially orphaned and fixed it with `EvaluateUiActionAsync`. If this story adds metadata for UI actions, align it with `TimesheetsUiAction`/policy outcomes instead of inventing a separate permission vocabulary.
- Story 1.2 kept fail-closed defaults replaceable through DI. This story should not add real sibling lookups or weaken default denial behavior.
- Story 1.2 confirmed direct xUnit v3 execution can be needed when `dotnet test` is blocked by VSTest socket permissions in this sandbox.
- Story 1.1 established the scaffold and a user-owned `Hexalith.Works` submodule pointer change. Do not modify sibling submodules or initialize nested submodules.

### Latest Technical Information

- Local package pins remain authoritative for this story: Aspire `13.4.5`, CommunityToolkit Aspire Dapr preview `13.4.0-preview.1.260602-0230`, Microsoft.Extensions `10.0.9` / `10.7.0`, OpenTelemetry `1.16.0` / instrumentation `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- Dapr .NET SDK latest release check shows `v1.18.4` on 2026-06-15. This story should not add or upgrade Dapr packages because Contracts must stay infrastructure-free and capture behavior is not being wired yet. [External source: `https://github.com/dapr/dotnet-sdk/releases`]
- Microsoft .NET 10 download page lists the May 12, 2026 security patch line for .NET Runtime `10.0.8`; the repo uses `global.json` roll-forward to latest patch and Story 1.2 had already observed local `dotnet --version` as `10.0.301`. Do not change `global.json` in this contract story. [External source: `https://dotnet.microsoft.com/en-us/download/dotnet/10.0`]
- Fluent UI Blazor release notes show the public latest line around v4, while Hexalith.FrontComposer pins Fluent UI V5 RC for component usage. Story 1.3 should not add Fluent UI dependencies; metadata must remain runtime-neutral and V5-compatible by convention. [External source: `https://github.com/microsoft/fluentui-blazor/releases`; Source: `Hexalith.FrontComposer/_bmad-output/project-context.md#Technology-Stack--Versions`]

### Testing Standards

- Use xUnit v3 and Shouldly; NSubstitute only if mocks are required. Avoid raw `Assert.*`, Moq, and FluentAssertions.
- Run tests by project, not solution-level `dotnet test`, for this Timesheets repo. Use `.slnx` for restore/build.
- Keep Story 1.3 tests fast and deterministic. Do not require Dapr, Aspire, EventStore server, containers, browser, or network to validate contracts and metadata.
- Negative-path tests are mandatory: no server-controlled authority fields in public payloads, no EventStore envelope fields, no sibling display/personal data, no Fluent UI V4 package, no inline package versions, and no infrastructure references from Contracts.

### Anti-Patterns To Prevent

- Do not expose raw EventStore envelopes, stream names, aggregate internals, sequence/checkpoint/rebuild mechanics, or command status substrate details as the public Timesheets contract.
- Do not add tenant/user/correlation/authorization/JWT/claims fields to public caller payloads as authority.
- Do not duplicate sibling module DTOs or persist/display copied Party personal data, Project/Work names, hierarchy/lifecycle, or Tenant membership state.
- Do not add direct persistence, Dapr runtime, ASP.NET hosting, Fluent UI, FrontComposer runtime, OpenAPI generator, OpenAI/SemanticKernel, or MCP dependencies to Contracts.
- Do not create a UI project, generated Razor components, or detailed workflow UI in this story.
- Do not use Fluent UI V4 components or add a Fluent UI icons package for metadata.
- Do not change package versions, initialize/update submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected production changes are primarily under `src/Hexalith.Timesheets.Contracts`.
- Expected host smoke changes, if needed, are limited to `src/Hexalith.Timesheets/Program.cs`.
- Expected documentation/artifacts should live under `src/Hexalith.Timesheets.Contracts/openapi/` or `docs/api/`; avoid using `docs/` as scratch content and keep any docs intentional.
- Expected tests are under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests`.
- Do not create `src/Hexalith.Timesheets.UI` for this story. The architecture allows it later, but Story 1.3 is metadata foundation only.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-3-Publish-Time-Capture-API-Contracts-and-FrontComposer-Metadata`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-1-Record-a-Time-Entry`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-10-Manage-tenant-level-Activity-Types`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-11-Manage-project-level-Activity-Types`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-15-Record-AI-Effort-Metrics`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-22-Expose-commandquery-contracts-for-UI-and-integration`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Architecture-Handoff-Notes`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/1-2-enforce-tenant-and-resource-authorization-gates.md#Previous-Story-Intelligence`]
- [Source: `docs/boundary-decision-record.md`]
- [Source: `README.md`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataDescriptor.cs`]
- [Source: `src/Hexalith.Timesheets/Program.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]
- [External source: `https://github.com/dapr/dotnet-sdk/releases`]
- [External source: `https://dotnet.microsoft.com/en-us/download/dotnet/10.0`]
- [External source: `https://github.com/microsoft/fluentui-blazor/releases`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19 - Resolved `bmad-create-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 - Loaded sprint status and selected user-requested Story 1.3; Epic 1 was already `in-progress`.
- 2026-06-19 - Loaded epics, architecture, nested PRD/UX artifacts, persistent project-context facts, previous Story 1.2, current source/test files, and recent git history.
- 2026-06-19 - Checked latest Dapr .NET SDK, .NET 10, and Fluent UI Blazor release sources; no version or dependency changes recommended for this contract story.
- 2026-06-19 - Created story file and marked sprint status entry `1-3-publish-time-capture-api-contracts-and-frontcomposer-metadata` as `ready-for-dev`.
- 2026-06-19 - Resolved `bmad-dev-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 - Captured baseline commit `f4365ea9d72c6fbcc48eac555a4d2258421c41b3` and moved Story 1.3/sprint status to `in-progress`.
- 2026-06-19 - Added red contract tests for exactly-one target modeling, server authority/envelope exclusion, enum sentinels, AI metric units, evidence read models, and metadata descriptors; initial compile failed because the new contract namespaces were absent.
- 2026-06-19 - Implemented infrastructure-free Timesheets contract primitives, command/query/event DTOs, rejection payloads, read models, typed metadata descriptors, static OpenAPI-ready artifact, and correlation-safe host metadata response.
- 2026-06-19 - `dotnet test` was blocked by VSTest socket permissions; used direct xUnit v3 project execution (`dotnet run --project <test>.csproj --no-build`) per Story 1.2 guidance.
- 2026-06-19 - Restored and built `Hexalith.Timesheets.slnx` with warnings as errors using the local NuGet package cache; all direct xUnit v3 test lanes passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story scope is intentionally contract/metadata/documentation/test foundation only; aggregate behavior, persistence, real command handlers, projections, and a Timesheets UI project remain for later stories.
- Current code files to extend and anti-patterns to avoid are explicitly listed so the dev agent can build on Story 1.1/1.2 instead of duplicating scaffolding or weakening authorization boundaries.
- Published infrastructure-free capture/catalog/evidence contract primitives and DTOs without adding domain behavior, persistence, command dispatch, generated UI, or runtime dependencies.
- Replaced the generic metadata placeholder with typed FrontComposer-compatible descriptors for `Record time`, `Activity Type Catalog`, and `Time Entry Evidence`.
- Added a static OpenAPI-ready contract artifact documenting server-derived context, non-ownership boundaries, DTO fields, state vocabularies, and validation/rejection outcomes without adding OpenAPI package dependencies.
- Updated `/metadata/timesheets` to return only module, contract version, capability names, and metadata descriptor names.
- Validation completed: restore/build passed; direct xUnit v3 lanes passed for Contracts.Tests, ArchitectureTests, IntegrationTests, Server.Tests, and Projections.Tests.

### File List

- `_bmad-output/implementation-artifacts/1-3-publish-time-capture-api-contracts-and-frontcomposer-metadata.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Client/ITimesheetsClient.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateProjectActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/CreateTenantActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/DeactivateActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/ReactivateActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/RenameActivityType.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ActivityTypes/UpdateActivityTypeMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeCreated.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeDeactivated.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeMetadataUpdated.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeReactivated.cs`
- `src/Hexalith.Timesheets.Contracts/Events/ActivityTypes/ActivityTypeRenamed.cs`
- `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsFieldError.cs`
- `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejection.cs`
- `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejectionCode.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogItem.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ProjectionFreshnessMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/ActivityTypes/ListActivityTypes.cs`
- `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/ReadTimeEntryEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataDescriptor.cs` (deleted)
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsCompositionPattern.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataActionDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataFieldDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataStateBadgeDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsSurfaceKind.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/ActivityTypeId.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryId.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryTargetReference.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets/Program.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`

## Senior Developer Review (AI)

Reviewer: Jérôme Piquot on 2026-06-19. Outcome: Approve (with auto-applied fixes).

### Verification

- Build: `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` succeeded with 0 warnings / 0 errors.
- Tests (direct xUnit v3 lanes): Contracts.Tests 14/14, ArchitectureTests 15/15, IntegrationTests 1 passed + 2 intentionally deferred (infrastructure/performance lanes).
- Acceptance Criteria 1-6: all implemented. Negative tests confirm public command/query payloads carry no server-controlled authority or EventStore envelope fields, Contracts has no infrastructure/UI dependencies, no inline package versions, and no Fluent UI V4 package.
- File List vs git: all source changes are documented; no false "changed" claims and no undocumented source files (only excluded `_bmad-output/` orchestration artifacts differ).

### Findings and Resolutions

- [Medium][Fixed] Orphaned duplicate enum: `State/TimeEntryLifecycleState.cs` was referenced nowhere and duplicated the canonical, fully-wired `TimeEntryApprovalState` (its only extra value `Corrected` is already covered by `TimeEntryCorrectionState`). Shipping a second approval/lifecycle vocabulary wired to nothing creates additive-evolution ambiguity in a contracts package. Removed the file (and the now-empty `State/` folder). Build and tests remain green.
- [Medium][Fixed] AC4 coverage gap: no test asserted that the metadata catalog declares the required status-badge vocabularies (approval, billable, contributor category, correction, projection freshness). Added `Metadata_catalog_declares_required_status_badge_vocabularies` to `TimeCaptureContractTests`.
- [Low][Fixed] Stale test name: `ReferenceContractTests.Metadata_catalog_exposes_frontcomposer_and_eventstore_entry_points` referenced "eventstore entry points" that no longer exist in the catalog. Renamed to `Metadata_catalog_exposes_capture_and_evidence_surface_descriptors`.

No CRITICAL issues found; status advanced to `done`.

## Change Log

- 2026-06-19 - Created Story 1.3 developer context for time capture API contracts and FrontComposer metadata; status set to ready-for-dev.
- 2026-06-19 - Implemented Story 1.3 contract, metadata, documentation, host smoke, and test foundation; status set to review.
- 2026-06-19 - Senior Developer Review (AI): removed orphaned duplicate `TimeEntryLifecycleState` enum, added required status-badge metadata test, renamed a stale metadata test; build/tests green; status set to done.
