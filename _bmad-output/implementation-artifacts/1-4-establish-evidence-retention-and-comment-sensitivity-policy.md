---
baseline_commit: 01ae7f3
---

# Story 1.4: Establish Evidence Retention and Comment Sensitivity Policy

Status: done

## Story

As a compliance-minded tenant operator,
I want explicit retention and comment sensitivity policy for time evidence,
so that Time Entries, comments, exports, and confirmation metadata are handled consistently before trusted capture expands.

## Acceptance Criteria

1. Given Timesheets stores Time Entry evidence, comments, export records, and magic-link confirmation metadata, when policy defaults are configured, then retention categories and default retention behavior are documented in configuration and public guidance, and unresolved legal-hold or tenant-specific overrides are visible as launch-readiness gaps rather than hidden assumptions.
2. Given comments may contain customer/private data, when a Time Entry, correction, rejection, or export includes comments, then comment sensitivity rules define where comments may be displayed, exported, retained, redacted, or excluded, and the rules do not copy Party personal data or sibling-owned Project/Work state into Timesheets.
3. Given logs, traces, support diagnostics, projections, and exports are produced, when they include operational metadata, then comments, event payloads, command bodies, personal data, token values, and secrets remain excluded unless an explicitly authorized export policy allows comment fields, and redaction/logging tests prove the exclusion.
4. Given retention or comment policy is missing for a trust-bearing action, when approval, correction, magic-link confirmation, or export would depend on that policy, then the action is blocked or marked not launch-ready according to configured policy, and users receive consequence-aware copy without protected detail leakage.
5. Given policy information appears in UI, when contributors, approvers, or finance users encounter comments or exports, then FrontComposer/Fluent UI V5 surfaces use message bars, field help, or export review text to explain the relevant policy, and the copy avoids invoice, payroll, rate, tax, and revenue-recognition ownership language.

## Tasks / Subtasks

- [x] Model infrastructure-free policy vocabulary in `Hexalith.Timesheets.Contracts` (AC: 1, 2, 3, 5)
  - [x] Add policy/value-object types under `src/Hexalith.Timesheets.Contracts/Policies` or `ValueObjects`, grouped by capability rather than generic compliance names.
  - [x] Include `Unknown = 0` enum sentinels and string JSON converters for policy-facing enums, matching the existing pattern in `TimesheetsEnums`.
  - [x] Cover retention categories for at least Time Entry evidence, comments, export records, and magic-link confirmation audit metadata.
  - [x] Cover comment sensitivity decisions: internal display, external confirmation display, projection inclusion, export inclusion, diagnostic/support exclusion, redaction requirement, and retention classification.
  - [x] Do not add Party display names, contact/profile fields, Project/Work names, hierarchy, lifecycle, Tenant membership, rates, invoice totals, payroll, tax, or revenue-recognition fields.
- [x] Add default policy configuration and launch-readiness reporting (AC: 1, 4)
  - [x] Add a server-side policy model such as `TimesheetsEvidencePolicyOptions` and evaluator under `src/Hexalith.Timesheets.Server/Policies` or a focused `Server/EvidencePolicy` folder.
  - [x] Register fail-closed defaults from `AddTimesheetsServerKernel()` using `TryAdd*` so future host wiring can replace them without weakening the default posture.
  - [x] Make unresolved legal-hold and tenant-specific retention overrides explicit launch-readiness gaps, not assumed allowed behavior.
  - [x] Keep policy evaluation server-side. Public caller payloads must not supply tenant/user/authorization/legal-hold context as authority.
  - [x] Do not persist policy configuration through SQL, Redis, Dapr state, local JSON files, broker CRUD, or projection mutation. If durable tenant policy is later required, it must flow through EventStore-backed domain events.
- [x] Wire policy decisions into trust-bearing operation vocabulary without implementing full capture/approval behavior early (AC: 2, 4)
  - [x] Extend existing server policy seams (`ITimesheetsPolicyEvaluator`, `TimesheetsOperation`, denial categories, or a focused evidence-policy evaluator) so approval, correction, export, and confirmation can fail closed when policy is missing.
  - [x] Preserve `TimesheetsAccessGuard` ordering: tenant/user authority first, resource validation second, Timesheets policy last.
  - [x] Add safe denial or not-launch-ready outcomes such as `CommentPolicyMissing`, `RetentionPolicyMissing`, or equivalent names only if they are consumed by tests and user-facing safe copy.
  - [x] Avoid aggregate-side policy lookups, clocks, logging, filesystem access, HTTP calls, or sibling lookups.
- [x] Add comment-sensitive contract support additively (AC: 2, 3)
  - [x] Current `RecordTimeEntry` and `TimeEntryEvidenceReadModel` do not model comments yet. Add comment support only as an additive, serialization-tolerant contract change; do not remove or rename existing constructor parameters/properties.
  - [x] Prefer a small value object such as `TimeEntryComment` or `CommentText` with boundary validation and policy metadata over raw strings scattered across commands, events, read models, and exports.
  - [x] If the existing positional record shape forces a breaking constructor change, document the rationale in the story completion notes and keep the JSON contract additive where possible.
  - [x] Ensure comment metadata never becomes a place to store token material, Party personal data, or Project/Work display state.
- [x] Update metadata and public guidance for policy-aware surfaces (AC: 1, 5)
  - [x] Extend `TimesheetsMetadataCatalog` with policy-related field/help descriptors for `Record time`, `Time Entry Evidence`, and future export review surfaces without taking Fluent UI, FrontComposer runtime, ASP.NET Core, Dapr, or EventStore server dependencies in Contracts.
  - [x] Add or update static public guidance under `src/Hexalith.Timesheets.Contracts/openapi/` and/or `docs/api/` describing retention categories, unresolved launch-readiness gaps, comment visibility/export rules, and non-ownership boundaries.
  - [x] UI copy must be short, factual, and consequence-aware. Use language such as `Comments may be excluded by policy.` or `Export comments only when policy allows it.`
  - [x] Do not use invoice, payroll, rate, tax, revenue-recognition, productivity coaching, or timer-app language.
- [x] Add privacy, contract, server, and architecture tests (AC: 1-5)
  - [x] Add contract tests for enum sentinels, JSON round-trip behavior, safe comment policy metadata, and no server-controlled authority fields.
  - [x] Add server tests proving missing policy blocks or marks trust-bearing approval/correction/export/confirmation paths not launch-ready, with safe copy and no protected detail leakage.
  - [x] Extend `DiagnosticsPrivacyTests` so logging/source scans continue to reject comments, command bodies, event payloads, personal data, token values, secrets, and sibling display data.
  - [x] Add tests proving policy docs/artifacts omit EventStore envelopes, copied sibling state, invoice/payroll/rate/tax/revenue wording, and unauthorized comment fields.
  - [x] Use xUnit v3, Shouldly, and NSubstitute. Do not introduce Moq, FluentAssertions, broad reflection helpers that hide failures, new analyzers, or package versions in `.csproj` files.
- [x] Verify build and focused test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected fast test projects individually: Contracts.Tests, Server.Tests, ArchitectureTests, and IntegrationTests only if the metadata endpoint/static artifact changes need coverage there.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 execution pattern from Stories 1.1-1.3 and document the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded nested PRD sources from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`.
- Loaded nested UX sources from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-3-publish-time-capture-api-contracts-and-frontcomposer-metadata.md`.
- Read current Timesheets contract, server policy/authorization, metadata, OpenAPI artifact, diagnostics, and test files listed in References.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance. Story 1.4 is a policy foundation story placed before tenant/project Activity Type governance and draft Time Entry capture expand.
- This story realizes FR3, FR21, and FR23: append-only evidence/correction lineage, EventStore persistence authority, and sibling boundary integrity.
- PRD NFRs make retention and legal hold an unresolved launch-readiness decision for Time Entry events, comments, export records, and Magic-Link Confirmation audit metadata. This story should surface that gap explicitly instead of inventing final tenant/legal retention periods.
- Comments are useful evidence but sensitive unstructured data by default. The architecture requires visibility, retention, logging, tracing, redaction, export exposure, and external-confirmation treatment to be defined before trusted capture expands.
- This story is not the full approval, correction, magic-link, export, or UI implementation. It creates the policy vocabulary, defaults, guardrails, documentation, and tests that later stories must consume.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs` currently contains Time Entry identity, target, contributor, Activity Type, date, duration, billable state, contributor category, and AI metrics. It does not yet include comment text or comment policy metadata.
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs` currently exposes stable references, Activity Type, date, duration, billable state, approval state, contributor category, AI metrics, correction state, and projection freshness. It does not yet expose comments or comment visibility decisions.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` currently defines `Record time`, `Activity Type Catalog`, and `Time Entry Evidence` descriptors. Extend this catalog rather than creating a parallel UI metadata vocabulary.
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` currently documents capture/catalog/evidence contracts and non-ownership boundaries with no product endpoints. Extend the static artifact only if it remains dependency-free and does not imply a live API endpoint.
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs` already includes `Command`, `Query`, `ProjectionRead`, `Export`, `Confirmation`, and `UiActionVisibility`. Reuse these operation categories for policy decisions before adding new ones.
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs` already runs tenant access, reference validation, then policy evaluation. Preserve that order and add evidence/comment policy through policy seams, not by bypassing the guard.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` registers fail-closed defaults with `TryAddSingleton`. New policy defaults should follow the same replaceable registration pattern.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` already scans logging lines for sensitive payload/identifier material. Extend it for any new policy, comment, export, or diagnostic terms introduced by this story.

### Architecture Constraints

- Timesheets durable domain changes must persist only through `Hexalith.EventStore`; no direct SQL, Redis, Dapr state, broker-backed CRUD, local JSON authoritative storage, or projection mutation as write authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Contracts contain commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only. They must remain infrastructure-free. [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- Events and public contracts evolve additively and serialization-tolerantly. Do not remove or rename published event fields or existing public contract properties. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Comments, token values, personal data, and command payloads are never logged, exported by default, or included in diagnostic metadata. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. Stale data cannot authorize approval, export, or confirmation decisions. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Public user-facing errors and policy copy must guide action without disclosing unauthorized tenant, entry, token, Project, Work, or Party existence. [Source: `_bmad-output/planning-artifacts/architecture.md#Error-Handling-Patterns`]
- UI surfaces must use FrontComposer-compatible metadata and Fluent UI V5 component conventions. This story should publish metadata/guidance only unless a later implementation proves a UI package is required. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]

### Policy Modeling Guidance

- Treat comments as sensitive unstructured evidence by default, not harmless notes.
- Policy vocabulary should distinguish at least:
  - retention category: Time Entry evidence, comment text, export record, magic-link confirmation audit metadata;
  - retention posture: retained by default, excluded, redacted, unresolved/legal-hold-required, tenant-override-required;
  - comment visibility: internal contributor/approver view, external confirmation view, projection/read model view, export output, support diagnostics;
  - trust-bearing action behavior when policy is missing: block, not launch-ready, or allowed only for non-trust-bearing draft display if explicitly configured.
- Store stable IDs and policy states only. Do not use comment policy as a shortcut for copying Party/Project/Work/Tenant display data.
- Do not make projections the authority for policy. Projections can surface policy/freshness state, but server policy evaluation gates trust-bearing writes and exports.
- If export policy allows comments, that permission must be explicit, test-covered, and documented. Default behavior should exclude comments from exports and diagnostics until policy says otherwise.

### FrontComposer / UX Guardrails

- Internal policy communication should be expressed through metadata that future FrontComposer surfaces can render as `FluentMessageBar`, field help, export review text, or status badges.
- Policy copy examples:
  - `Comments may contain sensitive information.`
  - `Comments are excluded from diagnostics.`
  - `Comment export is disabled until policy allows it.`
  - `Retention policy is unresolved for this action.`
- Copy must avoid protected detail leakage. Do not say which tenant, contributor, Project, Work, Time Entry, or token caused a denial if authority or policy cannot be resolved.
- Do not use invoice, payroll, rate, tax, or revenue-recognition language. Export is approved evidence only.
- Do not create a `Hexalith.Timesheets.UI` project for this story unless metadata-only support cannot meet AC5; the expected path is contract metadata and docs.

### Previous Story Intelligence

- Story 1.3 published infrastructure-free capture/catalog/evidence contracts and replaced generic metadata with typed FrontComposer-compatible descriptors. Build on those descriptors instead of reintroducing the deleted generic `TimesheetsMetadataDescriptor`.
- Story 1.3 review removed an orphaned duplicate enum and added a status-badge metadata test. Any new retention/comment enum must be wired to real contract/server/tests and not exist as unused vocabulary.
- Story 1.3 added a static OpenAPI-ready artifact under `src/Hexalith.Timesheets.Contracts/openapi/` without adding OpenAPI package dependencies. Keep this story's public guidance static/dependency-free unless an implementation need is proven.
- Story 1.2 added `TimesheetsAccessGuard`, `ITimesheetsPolicyEvaluator`, fail-closed tenant/resource/policy defaults, and UI action policy outcomes. Policy behavior for this story should extend those seams rather than creating a second authorization path.
- Story 1.2 review warned that adapter denial reasons are currently propagated verbatim; new policy evaluators should use centrally tested safe copy.
- Stories 1.1-1.3 confirmed `dotnet test` can be blocked by VSTest socket permissions in this sandbox; direct xUnit v3 execution with `dotnet run --project <test>.csproj --no-build` has been the reliable fallback.
- Do not modify sibling submodules or initialize nested submodules. The current worktree already has an unrelated modified story-automator orchestration file; leave it alone.

### Latest Technical Information

- No new external runtime/library version is required for this story. Use local package pins from `Directory.Packages.props`: Aspire `13.4.5`, CommunityToolkit Aspire Dapr preview `13.4.0-preview.1.260602-0230`, Microsoft.Extensions `10.0.9` / `10.7.0`, OpenTelemetry `1.16.0` / instrumentation `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- Dapr remains architecture-pinned to SDK `1.18.4`, but Story 1.4 should not add or upgrade Dapr packages. Policy code should be contract/server/test code and should not require Dapr/Aspire/EventStore infrastructure fixtures.
- Fluent UI V5 is a Hexalith FrontComposer rule for future rendering. Contracts must remain runtime-neutral and must not take Fluent UI or FrontComposer runtime dependencies.

### Testing Standards

- Use xUnit v3 and Shouldly. Use NSubstitute only where mocks/fakes are needed.
- Run test projects individually; use `.slnx` for restore/build.
- Keep Story 1.4 tests fast and deterministic. Do not require Dapr, Aspire, EventStore server, containers, browser, network, or a Timesheets UI project to validate policy vocabulary/defaults.
- Negative-path coverage is mandatory:
  - missing retention policy blocks or marks approval/correction/export/confirmation as not launch-ready;
  - default export policy excludes comments unless explicitly allowed;
  - logs/diagnostics/source scans reject comments, command bodies, event payloads, personal data, token values, secrets, and copied sibling display data;
  - policy copy does not reveal protected tenant, contributor, target, period, entry, or token details;
  - Contracts remain infrastructure-free and do not accept server-controlled authority fields.

### Anti-Patterns To Prevent

- Do not silently choose a legal retention period, legal-hold behavior, or tenant override behavior. Surface unresolved decisions as launch-readiness gaps.
- Do not log comments for troubleshooting.
- Do not include comments in support diagnostics by default.
- Do not include comments in export output unless explicit export policy allows it and tests prove unauthorized comment fields are absent.
- Do not model comments as Party personal data, Project/Work display names, or Tenant membership snapshots.
- Do not trust caller-supplied policy, tenant, user, authorization, legal-hold, correlation, token, or export-scope fields as authority.
- Do not add direct persistence or configuration storage outside established .NET configuration and EventStore-backed future domain events.
- Do not implement approval, correction, magic-link token issuance/use, export generation, or full UI workflows in this story.
- Do not create a second metadata/action vocabulary beside `TimesheetsMetadataCatalog` and `TimesheetsUiActionPolicyOutcome`.
- Do not modify package versions, create `.sln` files, weaken warnings-as-errors, or change sibling submodule files.

## Project Structure Notes

- Expected contract changes are under `src/Hexalith.Timesheets.Contracts/Policies`, `ValueObjects`, `Models`, `Events/Rejections`, `TimesheetsMetadataCatalog.cs`, and `openapi/` if guidance is extended.
- Expected server changes are under `src/Hexalith.Timesheets.Server/Policies` or another explicit evidence-policy folder, plus `Authorization` only where policy evaluation needs new safe denial categories.
- Expected tests are under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`.
- Expected documentation should live under `src/Hexalith.Timesheets.Contracts/openapi/` or an intentional `docs/api/` artifact. Do not use `docs/` as scratch space.
- No Timesheets UI project is expected for this story.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-4-Establish-Evidence-Retention-and-Comment-Sensitivity-Policy`]
- [Source: `_bmad-output/planning-artifacts/epics.md#NonFunctional-Requirements`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Pressure-Points`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#10-Cross-Cutting-NFRs`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#14-Open-Questions`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Microcopy-Guidelines`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Component-System`]
- [Source: `_bmad-output/implementation-artifacts/1-3-publish-time-capture-api-contracts-and-frontcomposer-metadata.md#Previous-Story-Intelligence`]
- [Source: `docs/boundary-decision-record.md`]
- [Source: `README.md`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19 02:23 CEST - Resolved `bmad-create-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 02:23 CEST - Confirmed user-requested Story 1.4 maps to sprint status key `1-4-establish-evidence-retention-and-comment-sensitivity-policy`, currently `backlog`.
- 2026-06-19 02:23 CEST - Loaded epics, architecture, PRD, UX, persistent project-context facts, previous Story 1.3, current source/test files, and recent git history.
- 2026-06-19 02:23 CEST - Created story file and marked sprint status entry `1-4-establish-evidence-retention-and-comment-sensitivity-policy` as `ready-for-dev`.
- 2026-06-19 CEST - Resolved `bmad-dev-story` workflow customization: no prepend/append steps, persistent facts from `**/project-context.md`, no `on_complete` action.
- 2026-06-19 CEST - Confirmed Story 1.4 baseline commit already matched current `HEAD` (`01ae7f306a1426f91468a4f0dfd4e2e2769a8616`) and preserved it.
- 2026-06-19 CEST - Red phase: added policy/comment contract, server evaluator, and architecture tests; initial Contracts test compile failed because `Hexalith.Timesheets.Contracts.Policies` did not exist.
- 2026-06-19 CEST - `dotnet test` for Contracts.Tests, Server.Tests, and ArchitectureTests compiled but was blocked by VSTest socket permission (`System.Net.Sockets.SocketException (13): Permission denied`); used direct xUnit v3 execution per story guidance.
- 2026-06-19 CEST - Initial network-restricted restore could not reach NuGet; created a temporary local package source at `/tmp/timesheets-local-nuget` from existing `.nupkg` cache, restored once from it, then the required plain restore command passed.
- 2026-06-19 CEST - Validation passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
- 2026-06-19 CEST - Validation passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
- 2026-06-19 CEST - Direct xUnit v3 passed: Contracts.Tests 22/22, Server.Tests 66/66, ArchitectureTests 16/16, IntegrationTests 3 total with 1 passed and 2 existing infrastructure/performance skips.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story scope is policy foundation only: retention/comment vocabulary, fail-closed policy defaults, launch-readiness gaps, safe copy, docs/metadata, and tests.
- The story explicitly calls out that current capture/evidence contracts do not yet model comments, so implementation must add comment support additively and policy-aware.
- The story preserves EventStore-only authority, infrastructure-free Contracts, fail-closed server policy seams, FrontComposer-compatible metadata, and no copied sibling-owned state.
- Added infrastructure-free retention/comment policy vocabulary with Unknown enum sentinels, JSON string converters, default retention/comment rules, and explicit launch-readiness gaps for legal hold, tenant overrides, and comment sensitivity.
- Added `TimeEntryComment` and `TimeEntryCommentPolicy` as additive `init` properties on `RecordTimeEntry`, `TimeEntryRecorded`, and `TimeEntryEvidenceReadModel`; existing positional constructors and JSON fields remain compatible.
- Replaced the default server policy evaluator registration with `TimesheetsEvidencePolicyEvaluator` plus fail-closed `TimesheetsEvidencePolicyOptions`; trust-bearing command/export/confirmation/UI operations now return safe `RetentionPolicyMissing` or `CommentPolicyMissing` denials until policy is configured.
- Extended metadata/OpenAPI/static guidance for comment sensitivity, export policy review, diagnostics exclusion, retention categories, and launch-readiness gaps without adding runtime UI or infrastructure dependencies.
- Added contract, server, architecture, and integration validation for enum/string JSON behavior, no authority fields, safe copy, diagnostics/privacy exclusions, metadata guidance, and policy blocking.

### File List

- `_bmad-output/implementation-artifacts/1-4-establish-evidence-retention-and-comment-sensitivity-policy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Policies/CommentSensitivityRule.cs`
- `src/Hexalith.Timesheets.Contracts/Policies/EvidenceRetentionRule.cs`
- `src/Hexalith.Timesheets.Contracts/Policies/TimeEntryCommentPolicy.cs`
- `src/Hexalith.Timesheets.Contracts/Policies/TimesheetsEvidencePolicyDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/Policies/TimesheetsPolicyEnums.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsMetadataFieldDescriptor.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryComment.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-evidence-policy.v1.md`
- `src/Hexalith.Timesheets.Server/Authorization/DenyAllTimesheetsPolicyEvaluator.cs` (removed during review — orphaned after evaluator swap)
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsDenialCategory.cs`
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsUiActionPolicyOutcome.cs`
- `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyEvaluator.cs`
- `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyOptions.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/EvidencePolicyContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/AuthorizationServiceTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/EvidencePolicyEvaluatorTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/FailClosedDefaultsTests.cs` (updated during review)
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`

## Senior Developer Review (AI)

**Reviewer:** Jerome — 2026-06-19
**Outcome:** Approved (auto-fix mode). All 5 ACs verified implemented; every `[x]` task confirmed against code/tests. 0 CRITICAL findings. Fixes applied automatically.

### Git vs Story File List

- Source changes matched the story File List. Only `_bmad-output/*` artifacts were changed outside the listed source — excluded from review per workflow rules.
- Review work added one modified test file (`FailClosedDefaultsTests.cs`) and removed one orphaned source file (`DenyAllTimesheetsPolicyEvaluator.cs`); File List updated accordingly.

### Findings and resolutions

- 🟡 **MEDIUM — Orphaned dead code + misleading fail-closed test.** Story 1.2's `DenyAllTimesheetsPolicyEvaluator` was left unregistered after `AddTimesheetsServerKernel()` swapped the default policy adapter to `TimesheetsEvidencePolicyEvaluator`, yet `FailClosedDefaultsTests.Default_tenant_and_policy_adapters_reject_unconfigured_authority` still source-scanned the dead class — implying the *active* default is a blanket deny-all (it is not; it allows non-trust-bearing reads). This contradicts the story's anti-pattern against orphaned vocabulary not wired to the real server. **Fixed:** deleted `DenyAllTimesheetsPolicyEvaluator.cs`; refit `FailClosedDefaultsTests` to (a) scan only the genuine deny-all tenant validator and (b) assert the real registered default (`TimesheetsEvidencePolicyEvaluator` + `FailClosedDefault`) denies trust-bearing operations with `RetentionPolicyMissing`.
- 🟡 **MEDIUM — `TimeEntryComment` lacked an upper-length boundary** despite Task 4 requiring "boundary validation" for this sensitive free-text value object (only the lower bound was enforced). **Fixed:** added a documented `TimeEntryComment.MaxLength = 4096` guardrail with `ArgumentOutOfRangeException.ThrowIfGreaterThan`, matched the OpenAPI `text` schema with `maxLength: 4096`, and added a boundary contract test. The constant is a bounded-payload guardrail until tenant policy specifies a final limit.
- 🟢 **LOW — Using-directive order** in `TimesheetsMetadataCatalog.cs` (`Policies` placed after `ValueObjects`). **Fixed:** reordered alphabetically.

### Validation

- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` → Build succeeded, 0 Warning(s), 0 Error(s).
- Direct xUnit v3 execution (`dotnet run --no-build`; `dotnet test` blocked by VSTest socket permissions in this sandbox per Stories 1.1–1.3): Contracts.Tests 25/25, Server.Tests 75/75, ArchitectureTests 16/16 — all passed.

## Change Log

- 2026-06-19 - Created Story 1.4 developer context for evidence retention and comment sensitivity policy; status set to ready-for-dev.
- 2026-06-19 - Implemented Story 1.4 evidence retention and comment sensitivity policy foundation; status set to review.
- 2026-06-19 - Senior Developer Review (auto-fix): removed orphaned `DenyAllTimesheetsPolicyEvaluator` and refit fail-closed test, added `TimeEntryComment` max-length boundary + test + OpenAPI bound, fixed using-directive order. Build + focused test lanes green; status set to done.
