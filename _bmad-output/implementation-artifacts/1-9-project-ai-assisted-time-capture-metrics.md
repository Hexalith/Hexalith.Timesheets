---
baseline_commit: 7f8d474
---

# Story 1.9: Project AI-Assisted Time Capture Metrics

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an AI agent operator,
I want AI-agent Time Entries to capture wall-clock, runtime, billable effort, and token metrics separately,
so that automation effort is visible without converting tokens or runtime into human hours.

## Acceptance Criteria

1. Given an AI-agent contributor is represented by a valid Party ID, when an AI Time Entry is recorded, then the entry can include wall-clock execution time, model/tool runtime, billable effort, and provider-reported input/output/total token counts, and each metric carries explicit units and source metadata.
2. Given provider token metrics are unavailable, when the AI Time Entry is recorded or displayed, then token fields are represented as unavailable or not reported, and the system never stores or displays missing provider metrics as zero.
3. Given AI effort metrics are included on a Time Entry, when the domain event is persisted, then the event remains additive and serialization-tolerant, and AI metrics do not change the authoritative human/external duration semantics.
4. Given an AI-agent command attempts to bypass tenant, Party, Project, Work, Activity Type, approval, or audit rules, when the command is handled, then the command fails closed, and AI-agent capture follows the same reference-validation and tenant-isolation rules as human/external capture.
5. Given AI metrics are shown in Time Entry Detail or AI effort surfaces, when users inspect the entry, then AI wall-clock, model/tool runtime, billable effort, and token metrics are visually separated from human/external duration, and unavailable metrics are shown with explicit text, not silence or color-only signals.
6. Given telemetry is emitted for AI capture, when metrics are accepted or rejected, then logs do not include token values, prompts, responses, command bodies, comments, secrets, or personal data, and only correlation-safe operational metadata is recorded.

## Tasks / Subtasks

- [x] Tighten the AI effort metric contract additively without replacing existing capture contracts (AC: 1, 2, 3)
  - [x] Extend `AiEffortMetrics` additively instead of renaming existing fields. Preserve `WallClockDurationMilliseconds`, `ModelRuntimeMilliseconds`, `BillableEffortMinutes`, `ProviderInputTokenCount`, `ProviderOutputTokenCount`, and `ProviderTotalTokenCount` so current consumers keep working.
  - [x] Add compact source metadata for the metric bundle, for example a new `AiEffortMetricSource`/`AiEffortMetricSourceMetadata` contract under `src/Hexalith.Timesheets.Contracts/Models` or `ValueObjects`. It must identify source category/provider/tool/work execution context enough for audit, but it must not store prompts, responses, provider secrets, bearer tokens, raw request/response bodies, or Party personal data.
  - [x] Add explicit token availability metadata so "runtime was reported but token metrics were not reported" is representable without setting token counts to zero. Use an enum with `Unknown = 0` and states such as provider-reported and not-reported/unavailable; keep JSON string enum behavior consistent with existing value objects.
  - [x] Keep units explicit in property names and OpenAPI schema descriptions: wall-clock and model/tool runtime are milliseconds, billable effort is minutes, provider token fields are counts. Do not introduce a generic `Duration`, `Runtime`, `Tokens`, `Cost`, or token-to-hours conversion field.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` so the AI metric schema documents units, source metadata, nullable unavailable token counts, and no caller-supplied tenant/user/authorization context.
- [x] Enforce AI-agent metric semantics in the Time Entry aggregate and command path (AC: 1, 2, 3, 4)
  - [x] Extend `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` validation rather than adding a parallel AI command handler.
  - [x] Require supplied AI metrics to have known availability/source metadata and non-negative numeric values. Unknown source or unknown token availability must produce field-specific domain rejections.
  - [x] Treat `ContributorCategory.AutomatedAgent` as the category allowed to carry provider-reported or estimated AI metrics. Human/external entries must not carry provider-reported AI effort data; they may omit metrics or use an explicitly unavailable/null placeholder only if needed for read-model compatibility.
  - [x] Preserve the existing Time Entry authorization order in `TimeEntryCommandService`: tenant access, target reference, contributor Party, policy, fresh Activity Type catalog, then aggregate validation. AI-agent capture must not bypass or weaken these gates.
  - [x] Keep `DurationMinutes` as the authoritative human/external effort field. AI wall-clock/runtime/token metrics are separate evidence and must not mutate or reinterpret `DurationMinutes`.
- [x] Project and display AI metrics safely through existing read models (AC: 2, 3, 5)
  - [x] Extend `TimeEntryEvidenceProjection` to preserve the enhanced `AiEffortMetrics` value from `TimeEntryRecorded` exactly, including source metadata and token availability, while preserving message-id dedupe, sequence ordering, unrelated-entry filtering, and projection freshness behavior.
  - [x] Ensure unavailable provider token counts remain `null` in `TimeEntryEvidenceReadModel`; never synthesize `0` unless the provider explicitly reported zero.
  - [x] Extend `TimesheetsMetadataCatalog` for `timesheets.command.record-time` and `timesheets.projection.time-entry-evidence` so AI metrics have distinct field metadata/status vocabulary for wall-clock runtime, model/tool runtime, billable effort, token metric availability, and source metadata.
  - [x] Keep Time Entry Detail as `FrontComposerProjectionView` metadata. If a UI project is eventually required, it must use FrontComposer/Fluent UI V5 and group AI metrics in a titled AI Metrics section when multiple sections are present; do not create a custom AI dashboard or special AI color palette in this story.
- [x] Add focused contract, aggregate, authorization, projection, metadata, and privacy tests (AC: 1-6)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` to prove AI metrics round-trip units, source metadata, token availability, nullable missing tokens, explicit provider-reported zero versus unavailable/null, and no authority/envelope fields.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` with AI-specific validation cases: automated-agent metrics accepted, human/external provider-reported metrics rejected, unknown source/availability rejected, negative values rejected, missing token metrics stored as unavailable/null, and `DurationMinutes` unchanged by AI metrics.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` to prove AI-agent capture still fails closed on tenant, Work/Project, Contributor Party, policy, and Activity Type catalog failures before domain dispatch.
  - [x] Extend `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` to prove AI metrics survive projection replay/deduplication and unavailable token metrics remain null.
  - [x] Extend metadata/OpenAPI tests to prove AI metric field descriptors and schema include units, source metadata, token availability, and text status badge vocabulary without Fluent UI runtime, Dapr, ASP.NET, EventStore envelope, or caller-authority dependencies in Contracts.
  - [x] Extend `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` so logging/source/artifact checks catch token values, prompts, responses, raw command bodies, comments, personal data, token secrets, and provider request/response payloads in diagnostic paths.
- [x] Verify build and affected test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected tests individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only if host/static artifact behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 executable pattern from Stories 1.7 and 1.8 and record the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 1 and Story 1.9.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, especially UJ-4, FR-12, FR-15, FR-20, FR-21, FR-22, FR-23, NFR observability/privacy, and the assumptions index.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially data architecture, API/query patterns, UI architecture, Dapr/Aspire guidance, implementation sequence, naming/format patterns, validation patterns, project structure, and enforcement guidance.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially UJ-4, AI metric display, component rules, status badges, explicit unavailable text, and no token-to-hours conversion.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-8-display-time-entry-evidence-from-read-models.md`.
- Read current Timesheets contracts, server, projection, metadata, OpenAPI, and test files listed in References.
- Reviewed recent git history through commit `7f8d474 feat(story-1.8): Display Time Entry Evidence from Read Models`.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance for human, external, and AI contributors with Party attribution, Project/Work references, Activity Types, EventStore persistence, and module boundary enforcement.
- Story 1.9 realizes FR12 and FR15. It is the capture/detail foundation for AI effort evidence, not the full reporting story. Story 4.4 later owns AI Effort Report rollups and broader reporting/filtering.
- AI metrics must remain separate evidence beside `DurationMinutes`. Do not convert token counts or runtime into human hours, do not calculate cost/rates, and do not add finance/invoice/payroll behavior.
- Existing Story 1.7 and 1.8 code already introduced the Time Entry command/event/projection/read-model path. Extend it. Do not create a parallel AI Time Entry aggregate, separate AI persistence store, or separate AI-specific authorization path.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs` already exists with `Availability`, `WallClockDurationMilliseconds`, `ModelRuntimeMilliseconds`, `BillableEffortMinutes`, and nullable provider token count fields. It lacks explicit source metadata and token-specific availability semantics.
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs` already contains `ContributorCategory.AutomatedAgent` and `AiMetricAvailability` with `Unknown = 0`. Add new enums here only when they are stable public vocabulary and keep zero as `Unknown`.
- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs` and `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs` already carry optional `AiEffortMetrics`. Preserve that additive shape; do not introduce a separate AI-record command unless later architecture explicitly requires it.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` already validates required capture fields, duplicate IDs, comment policy, AI metric unknown availability, and non-negative metric values. Extend this validation for automated-agent/source/token semantics.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs` already applies fail-closed authorization before activity-type selection and aggregate dispatch. Reuse this path for AI-agent entries.
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` already maps `TimeEntryRecorded.AiMetrics` into `TimeEntryEvidenceReadModel` and preserves projection freshness, source authority, event lineage, and unavailable display hydration. Extend it only where new AI metadata needs projection coverage.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` already has `aiMetrics` on record-time and Time Entry Evidence descriptors plus an AI metric availability status vocabulary. Split or enrich this metadata enough for the dev agent to satisfy visible unit/source/token availability requirements without adding UI runtime dependencies to Contracts.
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` already defines `AiEffortMetrics`; update that static artifact whenever public contract shape changes.
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` already covers AI metric round-trip and nullable unavailable counts, but it currently allows an Employee command with provider-reported AI metrics. Story 1.9 should adjust tests to enforce AI-agent semantics.
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` currently records Employee entries with `AiEffortMetrics.Unavailable`. Add automated-agent/provider-reported and unavailable-token projection cases.
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` has the fail-closed authorization pattern to reuse for AI-agent capture.

### Architecture Constraints

- Timesheets durable domain state changes persist only through Hexalith.EventStore. Do not introduce SQL, Redis, Dapr state-store authority, local JSON, broker-backed CRUD, or direct projection mutation for AI metrics. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Public command/query contracts hide EventStore envelope mechanics, aggregate internals, projection rebuild mechanics, tenant/user/correlation authority, JWT claims, roles, tokens, and raw infrastructure types. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Events are additive and immutable. Prefer optional fields or new additive value objects over renaming/removing previously emitted event fields. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- AI effort metrics must carry explicit units and source metadata; unavailable metrics are null/absent, never `0`. [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. AI-agent capture uses the same gates as other contributors. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Projection handlers must tolerate at-least-once delivery, duplicate messages, replay, and rebuild. Projection reads expose freshness/degradation and remain non-authoritative for writes. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 only where generated surfaces need explicit composition. V4 components are not allowed; V4 icons are an icons-only fallback. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### AI Metric Semantics

- `DurationMinutes` remains a positive whole-minute Time Entry duration. For AI-agent entries it may represent billable/captured effort according to policy, but wall-clock and model/tool runtime are separate AI metric fields and must not rewrite duration semantics.
- Wall-clock execution time and model/tool runtime use milliseconds. Billable effort uses minutes. Provider input/output/total token fields use token counts.
- Missing provider token metrics are represented by explicit token availability metadata plus null token count fields. `0` is valid only when the provider explicitly reported zero tokens.
- Source metadata should be concise and stable: enough to identify provider/tool/work-execution source for audit and later reporting, not enough to persist prompts, responses, secrets, request bodies, raw provider payloads, or sibling-owned Work details.
- Do not add OpenAI, Azure OpenAI, Anthropic, or other provider SDK dependencies for this story. The contract should model provider-reported metrics generically; provider adapters can translate into it later.

### FrontComposer / UX Guardrails

- Time Entry Detail already uses `FrontComposerProjectionView` metadata. Keep it there.
- AI metrics shown in Time Entry Detail or future AI effort surfaces must be separated from human/external duration and must display unit labels or unit-aware field metadata.
- Use text status for AI metric availability and token availability. `Unavailable` or `Not reported by provider` is acceptable copy; silence, blank cells, hidden fields, or color-only states are not.
- Do not create a special AI visual brand, AI-only palette, decorative card grid, marketing-style hero, gamified timer, or stopwatch/live activity capture.
- If multiple detail sections are present, AI Metrics belongs in a titled `FluentAccordionItem` alongside Evidence, Approval, Correction Lineage, Audit Metadata, or Policy.

### Previous Story Intelligence

- Story 1.8 added source authority, safe event lineage, read-time display hydration, fail-closed evidence query service, and metadata/OpenAPI coverage. Reuse these patterns; do not expose raw EventStore envelopes or sibling labels in AI metric details.
- Story 1.8 review found checked tasks that were not fully reflected in File List/tests. For Story 1.9, keep checked tasks honest and ensure every listed test/fitness change exists.
- Story 1.8 used direct xUnit v3 executable runs because `dotnet test` hit VSTest socket permission failures in this sandbox. Use the same fallback if needed.
- The worktree currently has an unrelated modified `_bmad-output/story-automator/orchestration-1-20260618-221411.md`. Leave it untouched.

### Git Intelligence Summary

- `7f8d474` implemented Story 1.8 by extending `TimeEntryEvidenceReadModel`, `TimeEntryEvidenceProjection`, Time Entry evidence query services, hydration seams, metadata/OpenAPI, and contract/server/projection/privacy tests.
- `a4aa9fc` implemented Story 1.7 by creating `TimeEntry`, `TimeEntryCommandService`, `TimeEntryEvidenceProjection`, `TimeEntryProjectionEvent`, `TimeEntryState`, and aggregate/authorization/projection tests.
- `2eaf794` implemented project Activity Type governance with project catalog commands/events, projection behavior, command services, metadata/OpenAPI updates, and authorization tests.
- `f7ca2bd` implemented tenant Activity Type governance and established projection freshness/catalog availability patterns.
- `5bb8616` implemented evidence retention/comment sensitivity policy and strengthened diagnostics privacy fitness tests.

### Latest Technical Information

- No package upgrade is required for Story 1.9. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- Official NuGet confirms `xunit.v3` `3.2.2` targets .NET 8.0 and is computed compatible with `net10.0`; it remains the repo-pinned test framework for this story. A newer prerelease exists, but do not change test packages for AI metric capture.
- Dapr remains architecture-pinned to SDK `1.18.4`; this story should not add Dapr package dependencies.
- Fluent UI V5 remains a UX/component requirement. Contracts and metadata must not reference Fluent UI, Blazor, FrontComposer runtime packages, ASP.NET Core, Dapr, Redis, Entity Framework, OpenAI SDKs, or EventStore server packages.

### Project Context Reference

- EventStore project context reinforces pure aggregate/event handling, EventStore-owned persistence/envelopes, idempotent projections, `.slnx`, xUnit v3, Shouldly, and no payload/personal-data logging.
- Tenants project context reinforces fail-closed tenant access, idempotent event handling, no recursive submodules, and no sensitive detail in logs or docs.
- Parties project context reinforces Party-owned identity/display data and read-side projection freshness; Timesheets must store Party references only.
- Projects project context reinforces stable Project references and no copied Project hierarchy, lifecycle, owners, managers, or approver state.
- FrontComposer project context reinforces generated projection surfaces, Fluent UI V5 component usage, accordions for multi-section details, text status badges, and no raw HTML-first internal surface.
- No `Hexalith.Works/_bmad-output/project-context.md` file was found; use Timesheets architecture/PRD guidance for Work reference boundaries.

### Testing Standards

- Use xUnit v3 and Shouldly. Use NSubstitute only where fakes/mocks are needed.
- Run test projects individually; use `.slnx` for restore/build only.
- Keep fast tests deterministic and infrastructure-free. Contract, aggregate, authorization, projection, metadata, and privacy tests should not require Dapr, Aspire, EventStore server, containers, browser, network, external AI providers, or a Timesheets UI project.
- Negative-path coverage is mandatory:
  - AI metrics with unknown source/token availability are rejected;
  - negative runtime/effort/token values are rejected;
  - human/external provider-reported AI metrics are rejected or explicitly unavailable only;
  - provider-unreported token counts remain null and display as unavailable/not reported, never zero;
  - AI-agent capture fails closed on tenant, target, contributor, policy, and Activity Type catalog failures before dispatch;
  - projection replay/duplicate delivery preserves a single deterministic AI metric result;
  - logs, metadata, artifacts, and denial results omit prompts, responses, token values, comments, command bodies, personal data, secrets, target names, sibling labels on denial, and raw EventStore envelopes.

### Anti-Patterns To Prevent

- Do not implement a second AI Time Entry aggregate, an AI-only command pipeline, or AI-specific authorization bypass.
- Do not persist AI metrics outside EventStore-backed Time Entry events or treat projections as write authority.
- Do not store prompts, responses, raw provider payloads, request bodies, API keys, bearer tokens, decoded capability material, or provider secrets in commands, events, projections, logs, metadata, comments, or OpenAPI examples.
- Do not convert tokens, runtime, or wall-clock time into human hours by default.
- Do not use `0` to mean missing provider token metrics.
- Do not use `Estimated` AI metrics as provider-reported facts unless the source explicitly says estimated.
- Do not copy Work execution state, Work lifecycle state, Project names, Party display names, or Tenant membership into AI metric events.
- Do not add provider-specific SDK dependencies or external network calls to the domain model.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract updates belong under `src/Hexalith.Timesheets.Contracts/Models`, `ValueObjects`, `Commands/TimeEntries`, `Events/TimeEntries`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server updates belong under `src/Hexalith.Timesheets.Server/TimeEntries` and should reuse `Authorization` and `References` services, not duplicate them.
- Expected projection updates belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests are under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use IntegrationTests only if host/static artifact behavior changes.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-9-Project-AI-Assisted-Time-Capture-Metrics`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-Design-Requirements`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#5-4-Actor-Neutral-Contributors-External-Parties-And-AI-Agents`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#10-Cross-Cutting-NFRs`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Format-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-4-Ada-The-AI-Agent-Records-Execution-Evidence`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/1-8-display-time-entry-evidence-from-read-models.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`]
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

- Resolved `bmad-dev-story` workflow customization; no prepend/append steps configured.
- Read shared Hexalith LLM instructions and `hexalith-state-instructions.md`; no alternate persistence was introduced.
- Attempted `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore`; VSTest aborted with `System.Net.Sockets.SocketException (13): Permission denied`, so direct xUnit v3 executable runs were used.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` succeeded.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` succeeded with 0 warnings and 0 errors.
- Direct xUnit v3 runs passed: Contracts.Tests 33, Server.Tests 180, Projections.Tests 21, ArchitectureTests 17, IntegrationTests 5 total with 2 expected infrastructure-reserved skips.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added additive AI metric source and token availability contract metadata while preserving existing `AiEffortMetrics` unit/count properties.
- Enforced known AI metric source/token availability, non-negative values, null-not-zero unavailable token semantics, and automated-agent-only provider/estimated AI metrics in the existing Time Entry aggregate path.
- Reused the existing Time Entry command service authorization order; no parallel AI command handler or AI-specific authorization bypass was added.
- Verified the existing evidence projection already preserves the enhanced `AiEffortMetrics` value exactly; added replay/deduplication tests for source metadata and token availability.
- Enriched Timesheets metadata/OpenAPI with distinct AI wall-clock, runtime, billable effort, token availability, and source metadata descriptors without UI/runtime dependencies.
- Strengthened privacy fitness coverage for diagnostic paths involving token values, prompts, responses, raw command/provider payloads, comments, personal data, bearer tokens, and secrets.

### File List

- `_bmad-output/implementation-artifacts/1-9-project-ai-assisted-time-capture-metrics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetricSourceMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/AiEffortMetrics.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/AiAssistedTimeCaptureMetricsE2ETests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 1.9 AI-assisted time capture metrics contract, aggregate validation, metadata/OpenAPI updates, projection coverage, privacy tests, and validation gates.
- 2026-06-19: Adversarial code review (auto-fix) by Jerome. Added the two undocumented IntegrationTests files to the File List, hardened `TimeEntry` unavailable-metric validation so the "unavailable values must be null" rule always runs, and re-verified the full build and all five test lanes. Status moved review -> done.

## Senior Developer Review (AI)

- **Reviewer:** Jerome
- **Date:** 2026-06-19
- **Outcome:** Approve (auto-fix applied)
- **Scope:** Story File List + git changes since baseline `7f8d474`, all six Acceptance Criteria, every task marked `[x]`, and the changed contract/server/projection/test surface.

### Verification performed

- `dotnet restore`, `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` -> 0 warnings, 0 errors.
- Direct xUnit v3 executables (VSTest socket blocked in sandbox, per Stories 1.7/1.8): Contracts.Tests 33, Server.Tests 180, Projections.Tests 21, ArchitectureTests 17, IntegrationTests 7 (5 passed + 2 expected infrastructure skips). All green after fixes.
- Confirmed each AC is implemented: AC1 source metadata + explicit units (contract/OpenAPI); AC2 null-not-zero token availability; AC3 additive event fields with defaults and `DurationMinutes` left authoritative; AC4 reused fail-closed `TimeEntryCommandService` gate order (authorization test added); AC5 distinct unit/source/token field descriptors in `TimesheetsMetadataCatalog`; AC6 broadened `DiagnosticsPrivacyTests` sensitive-term coverage.
- Verified the "no projection change required" claim: `TimeEntryEvidenceProjection.Apply` passes `recorded.AiMetrics` through by reference, so the new `Source`/`TokenAvailability` fields are preserved automatically.

### Findings and resolution

- **[MEDIUM][Fixed] File List incomplete.** Git showed `tests/Hexalith.Timesheets.IntegrationTests/AiAssistedTimeCaptureMetricsE2ETests.cs` (new) and `tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj` (added Server + Projections project references) were changed but absent from the Dev Agent Record File List. Both added.
- **[LOW][Fixed] Misplaced unavailable-metric check in `TimeEntry.cs`.** The "unavailable AI metrics must not carry numeric values" rule (an overall-`Availability` concern) lived inside `ValidateAiTokenAvailability` after an early `return` on unknown token availability, so it was skipped whenever `TokenAvailability == Unknown`. No accept/reject escape (such entries are rejected anyway), but the precise error was masked. Extracted into `ValidateUnavailableMetricsCarryNoValues` and invoked unconditionally.
- **[LOW][Accepted as-is] Redundant sensitive-term substrings** in `DiagnosticsPrivacyTests.SensitiveLoggingTerms` (`token` already subsumes `tokens`/`token value`/`token count`, etc.). Left intentionally because the explicit list documents the AC6 enumeration; behavior is unaffected.
- **[LOW][Accepted as-is] `AiEffortMetricSourceMetadata` factory asymmetry** — only `Provider(...)` and `Unavailable` helpers exist while validation/OpenAPI also support `Tool`/`WorkExecution` categories. Callers can use the primary constructor; no defect.

No CRITICAL findings: no task marked `[x]` was unimplemented, no AC was missing, and no File List entry claimed a change without a matching git diff.
