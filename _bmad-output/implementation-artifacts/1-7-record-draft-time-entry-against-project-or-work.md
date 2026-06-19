---
baseline_commit: 2eaf794
---

# Story 1.7: Record Draft Time Entry Against Project or Work

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor,
I want to record draft time against exactly one Project or Work reference,
so that my work evidence is captured early with the right contributor, activity, duration, and billable context.

## Acceptance Criteria

1. Given a contributor is authorized in a tenant context, when they record a draft Time Entry with date, positive whole-minute duration, Contributor Party ID, Activity Type, comment, Billable Flag, and exactly one Target Reference, then Timesheets persists the change through EventStore as a durable domain event, and the resulting projection exposes the draft Time Entry with projection freshness metadata.
2. Given a draft Time Entry command contains both a Project Reference and a Work Reference, or neither, when the command is handled, then the command is rejected as a domain outcome, and no successful Time Entry event is emitted.
3. Given a draft Time Entry command has a non-positive human/external duration or missing required capture fields, when the command is handled, then validation rejects the command with field-specific errors, and no partial Time Entry state is persisted.
4. Given a Contributor Party ID is supplied, when Timesheets records the entry, then the Party reference is validated at the boundary according to the configured policy, and Timesheets stores the Party ID only, not Party display name, contact details, or profile data.
5. Given a Project or Work target cannot be verified for a trust-bearing write, when the Time Entry is submitted for a trust-bearing state, then the command fails closed, and draft capture behavior only tolerates stale display hydration where policy explicitly allows it.
6. Given a user records time from a Project/Work context, dashboard, or command surface, when the capture UI is shown, then it uses FrontComposerGeneratedForm or equivalent Fluent UI V5 components, states duration units clearly, validates fields beside inputs, and preserves entered values after interrupted commands.
7. Given logs and telemetry are emitted during capture, when the command succeeds or fails, then logs include correlation-safe metadata only, and comments, command bodies, event payloads, personal data, target names, and secrets are not logged.

## Tasks / Subtasks

- [x] Implement the Time Entry draft aggregate and command decision path (AC: 1, 2, 3)
  - [x] Add a `TimeEntry` aggregate/state boundary under `src/Hexalith.Timesheets.Server/TimeEntries` that follows the existing pure `Handle(command, state?) -> TimesheetsDomainResult` pattern from Activity Types.
  - [x] Handle `RecordTimeEntry` by emitting `TimeEntryRecorded` with `ApprovalState = TimeEntryApprovalState.Draft`, stable `TimeEntryId`, `TimeEntryTargetReference`, `PartyReference`, `ActivityTypeId`, `ActivityTypeScope`, service date, positive `DurationMinutes`, `BillableState`, contributor category, optional AI metrics, and optional policy-aware comment.
  - [x] Reject duplicate `TimeEntryId` when prior state already contains a recorded entry; idempotent same-command behavior must be explicit if chosen, covered by tests, and must not emit duplicate events.
  - [x] Reject `TimeEntryTargetKind.Unknown`, blank target IDs, missing target data, missing contributor, missing Activity Type, `BillableState.Unknown`, `ContributorCategory.Unknown`, `TimeEntryApprovalState.Unknown`, non-positive duration, invalid AI metric units, and invalid comment values as typed Timesheets domain outcomes.
  - [x] Do not put tenant/user/correlation/JWT/EventStore envelope fields into `RecordTimeEntry`, `TimeEntryRecorded`, aggregate state, or public read models. Tenant and actor context stays in `TimesheetsRequestContext` and EventStore infrastructure.
- [x] Add a Time Entry command service and fail-closed authorization boundary (AC: 1, 4, 5)
  - [x] Add a `TimeEntryCommandService` under `src/Hexalith.Timesheets.Server/TimeEntries`.
  - [x] Reuse `TimesheetsAccessGuard` with `TimesheetsOperation.Command`; set exactly one of `TimesheetsAuthorizationRequest.Project` or `TimesheetsAuthorizationRequest.Work` from `RecordTimeEntry.Target`, and always set `Contributor`.
  - [x] Preserve authorization order: tenant access first, then Project or Work validation, then Contributor Party validation, then policy evaluation, then aggregate decision.
  - [x] Preserve fail-closed defaults: `DenyAllProjectReferenceValidator`, `DenyAllWorkReferenceValidator`, and `DenyAllContributorPartyValidator` must still deny composed-kernel draft capture until trust adapters are supplied.
  - [x] Return authorization denial without aggregate dispatch when tenant authority, target authority, contributor authority, stale projection, ambiguous authority, unavailable sibling authority, cross-tenant target, disabled/archived target, invalid reference, or policy cannot be resolved.
  - [x] Keep draft capture distinct from later submission. Draft display may carry stale hydration warnings by policy, but this story must not weaken submission/approval/export/magic-link fail-closed requirements.
- [x] Validate Activity Type selection without reimplementing the catalog (AC: 1, 3, 5)
  - [x] Reuse the Activity Type catalog read/selection semantics from Stories 1.5 and 1.6 rather than duplicating label or scope logic in Time Entry capture.
  - [x] Require the selected Activity Type to be available for capture for the target context: tenant types are valid unless inactive/restricted; project types require the matching Project Reference; Work-targeted capture must resolve the governing Project context through a policy/adapter or reject until that adapter exists.
  - [x] Persist stable `ActivityTypeId` and `ActivityTypeScope` only. Do not persist Activity Type display labels as identity, and do not rewrite Time Entry facts when labels, active state, billable defaults, or project restrictions change.
  - [x] If current projection freshness is required for capture selection, reject stale/rebuilding/unavailable catalog authority instead of treating it as fresh.
- [x] Add the Time Entry projection/read path (AC: 1)
  - [x] Add a replay-safe projection under `src/Hexalith.Timesheets.Projections/TimeEntries`, for example `TimeEntryEvidenceProjection`, that derives `TimeEntryEvidenceReadModel` from `TimeEntryRecorded` and future Time Entry events.
  - [x] Include `ProjectionFreshnessMetadata` using the existing `TimesheetsProjectionCheckpoint` and `ProjectionFreshness` mapping style.
  - [x] Make projection application idempotent by `MessageId`, ordered by `SequenceNumber`, and replay-equivalent for duplicate delivery.
  - [x] Preserve stable references only: `TimeEntryId`, `TimeEntryTargetReference`, `PartyReference`, `ActivityTypeId`, `ActivityTypeScope`, service date, duration, billable state, approval state, contributor category, AI metrics, correction state, and optional comment policy data.
  - [x] Do not make projections write authority. Projections are derived read models only.
- [x] Update contracts, metadata, and static API artifact only where gaps remain (AC: 1, 6)
  - [x] Prefer the existing `RecordTimeEntry`, `TimeEntryRecorded`, `TimeEntryTargetReference`, `TimeEntryEvidenceReadModel`, `ReadTimeEntryEvidence`, `AiEffortMetrics`, and metadata descriptors from Story 1.3.
  - [x] Extend `TimesheetsMetadataCatalog` in place if the record-time descriptor is missing fields now required by the command surface, especially `serviceDate`, `contributorCategory`, optional AI metrics, approval state badges, and target-context action labels.
  - [x] Keep `timesheets.command.record-time` as `FrontComposerGeneratedForm`; do not add a dedicated Timesheets UI project or hand-built raw HTML/CSS form for this story.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` if schema/metadata shape changes. It remains a contract artifact with no product endpoints and no EventStore envelope mechanics.
- [x] Add focused tests for domain, authorization, projections, contracts, metadata, and privacy (AC: 1-7)
  - [x] Add server aggregate tests for successful draft record, duplicate ID, exactly-one-target invariant, missing fields, non-positive duration, unknown enum sentinels, invalid AI metrics, invalid comment, and draft approval state.
  - [x] Add authorization tests proving Time Entry capture fails closed before domain dispatch for missing tenant, cross-tenant Project/Work, stale target projection, ambiguous authority, unavailable sibling authority, invalid target, disabled/archived target, unresolved Contributor Party, and policy denial.
  - [x] Add tests proving Project-targeted capture calls only Project plus Contributor validators, Work-targeted capture calls only Work plus Contributor validators, and neither path calls both target validators.
  - [x] Add projection tests proving duplicate/replayed `TimeEntryRecorded` events do not duplicate entries, out-of-order delivery is ordered by `SequenceNumber`, freshness metadata is surfaced, and stale/rebuilding/unavailable checkpoints are not presented as fresh.
  - [x] Add contract tests for any changed `RecordTimeEntry`, `TimeEntryRecorded`, `TimeEntryEvidenceReadModel`, metadata, and OpenAPI artifact shape; preserve existing authority-field exclusions.
  - [x] Extend diagnostics/privacy tests if new source can log or serialize comments, command bodies, event payloads, target IDs, target names, Party data, tokens, or secrets.
- [x] Verify build and focused test lanes (AC: 1-7)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests if host metadata or static artifact behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions in this sandbox, use the direct xUnit v3 execution pattern from Stories 1.1-1.6 and document the reason.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded PRD content from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`.
- Loaded UX content from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-6-configure-project-activity-types.md`.
- Read current Timesheets Time Entry contracts, value objects, authorization/reference validators, metadata catalog, OpenAPI artifact, projection patterns, Activity Type selection projection, and focused tests listed in References.
- Reviewed recent git history through commit `2eaf794 feat(story-1.6): Configure Project Activity Types`.

### Epic And Story Context

- Epic 1 establishes trusted time capture and activity governance with Project/Work references, Activity Type catalogs, Party attribution, EventStore persistence, and module boundary enforcement.
- Story 1.7 realizes FR1, FR2, FR12, and FR21: contributors can create draft Time Entries; entries target exactly one Project or Work; Contributor Party references are stored as stable IDs only; and durable state changes persist through EventStore-backed events.
- This story is draft capture only. Do not implement submission, approval, rejection, approved-entry locking, corrections, timesheet periods, magic-link confirmation, external contributor confirmation, finance export, or report rollups beyond the read model needed to expose recorded draft evidence.
- Activity Type selection depends on the tenant/project catalog foundation from Stories 1.5 and 1.6. Do not reimplement catalogs, copy labels as identity, or bypass project restriction semantics.

### Current Code State To Extend

- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs` already exists and carries `TimeEntryId`, a single `TimeEntryTargetReference`, `PartyReference`, `ActivityTypeId`, service date, duration minutes, billable state, contributor category, optional AI metrics, and optional comment.
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs` already exists and adds `ActivityTypeScope` and `TimeEntryApprovalState`; successful draft capture should emit this with `Draft`.
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryTargetReference.cs` enforces Project-or-Work target kind and nonblank target ID. Keep this single-target contract; do not add parallel `ProjectReference` and `WorkReference` command fields.
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs` already defines the evidence projection shape with stable references, approval state, correction state, optional AI metrics/comment, and projection freshness.
- `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/ReadTimeEntryEvidence.cs` currently accepts only `TimeEntryId`; query authorization must derive tenant/user context from request context, not from the query payload.
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` already has `timesheets.command.record-time` and `timesheets.projection.time-entry-evidence` descriptors, but the command descriptor should be checked against the current command fields before implementation is considered complete.
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs` already validates tenant access first, then Project, Work, and Contributor references when present, then policy.
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAuthorizationRequest.cs` already supports optional `Project`, `Work`, and `Contributor` reference slots. Story 1.7 should populate those slots for capture instead of adding new guard APIs.
- `src/Hexalith.Timesheets.Server/References/DenyAllContributorPartyValidator.cs`, `DenyAllProjectReferenceValidator.cs`, and `DenyAllWorkReferenceValidator.cs` are the correct fail-closed defaults.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` registers the server kernel and should register the new `TimeEntryCommandService` with `TryAddSingleton`, matching the Activity Type services.
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs` already supports tenant catalog reads, project catalog reads, restriction filtering, freshness metadata, `MessageId` deduplication, and `SequenceNumber` ordering. Reuse this selection contract for Activity Type availability.
- There is no current `src/Hexalith.Timesheets.Server/TimeEntries` or `src/Hexalith.Timesheets.Projections/TimeEntries` implementation. This story creates those paths.

### Architecture Constraints

- Timesheets durable domain state changes persist only through Hexalith.EventStore. Do not introduce SQL, Redis, Dapr state-store, local JSON, broker-backed CRUD, or projection mutation as authoritative storage. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- `TimeEntry` is the aggregate boundary for individual entry lifecycle, target reference, Contributor Party ID, billable flag, Activity Type, comments, approval state, correction lineage, and AI effort evidence attached to that entry. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Draft creation may tolerate stale display hydration, but submission, approval, export, and magic-link confirmation fail closed when required references or authority cannot be resolved. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. JWT claims and caller payloads are evidence only, not authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Public contracts evolve additively and remain infrastructure-free. Do not remove or rename existing contract/event/read-model fields from Stories 1.3-1.6. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Events are additive and immutable. Event handlers and projections tolerate at-least-once delivery, duplicate messages, rebuild, and replay. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Logs and traces must not contain command bodies, event payloads, comments, personal data, tokens, secrets, target names, or copied sibling display state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]

### Time Entry Domain Guidance

- A draft Time Entry is an attributable evidence fact, not a mutable spreadsheet row.
- The success event should capture the initial fact and `Draft` state. Later edits/corrections/submission/approval are separate events in later stories.
- Duration for human and external contributor entries is positive whole minutes. AI runtime and token counts live in `AiEffortMetrics` and must not be normalized into human minutes.
- Missing provider token metrics are represented as unavailable/unknown or null, not zero unless a provider reports zero.
- `BillableState.Unknown`, `ContributorCategory.Unknown`, `TimeEntryTargetKind.Unknown`, `ActivityTypeScope.Unknown`, and `TimeEntryApprovalState.Unknown` are invalid for successful persisted evidence.
- Comments are sensitive evidence. Use existing `TimeEntryComment` and evidence policy objects; do not log comments or emit them into diagnostics.
- Business failures should be typed Timesheets domain outcomes/rejections with field-specific errors where possible, not thrown infrastructure exceptions.
- Aggregates remain pure: no tenant lookup, Project/Work/Party API calls, HTTP, logging, clocks, filesystem, Dapr, EventStore envelope inspection, or UI shaping inside aggregate decisions.

### Authorization And Boundary Guidance

- The service layer owns authorization and reference validation before domain dispatch; the aggregate owns invariant consistency.
- For Project targets, set `TimesheetsAuthorizationRequest.Project` from `TimeEntryTargetReference.ForProject(...)` semantics and leave `Work` null.
- For Work targets, set `TimesheetsAuthorizationRequest.Work` from `TimeEntryTargetReference.ForWork(...)` semantics and leave `Project` null unless an explicit Work-to-Project validation adapter is later introduced.
- Always set `TimesheetsAuthorizationRequest.Contributor` for `RecordTimeEntry`.
- The command service should not trust a caller-supplied target ID, contributor ID, tenant ID, JWT claim, or correlation ID as authority. These are inputs to validation or request metadata only.
- Do not copy Party display names, contact details, profile fields, Project names, Project hierarchy, Project lifecycle, Work titles, Work lifecycle, planned effort, owners, managers, or approver lists into Timesheets events, state, projections, OpenAPI, logs, or metadata.

### Projection And Read Model Guidance

- `TimeEntryEvidenceReadModel` should surface draft evidence with `ProjectionFreshnessMetadata.Fresh` only when the checkpoint is fresh.
- Projection tests should mirror Activity Type projection patterns: sort by `SequenceNumber`, dedupe by `MessageId`, and prove duplicate delivery/replay equivalence.
- The projection can start with `TimeEntryRecorded` only, but its shape must leave room for later submission, approval, rejection, correction, AI metric, and comment policy events.
- Reads must not imply stale/rebuilding/unavailable projection data is fresh authority for trust-bearing decisions.

### FrontComposer / UX Guardrails

- Record Time Entry is reachable from Project/Work context actions, dashboard actions, and command surfaces.
- Use `FrontComposerGeneratedForm` by default; validation messages stay beside fields.
- Required capture fields are date, duration, Target Reference, Activity Type, comment where policy requires it, Billable Flag, and Contributor Party reference.
- Duration inputs state units clearly. AI metric fields distinguish minutes, runtime, billable effort, and token counts.
- Status badges include text, not color alone, for approval state, billable state, contributor category, AI metric availability, correction state, and projection freshness.
- Preserve entered values after interrupted commands where the command surface supports it.
- Do not create a parallel internal navigation shell, Timesheets-specific visual theme, marketing page, gamified timer UI, raw HTML/CSS form, or decorative card-heavy capture surface.

### Previous Story Intelligence

- Story 1.6 implemented project-scoped Activity Type governance with `ProjectActivityTypeAggregate`, `ProjectActivityTypeCommandService`, project catalog restriction contracts/events, Activity Type projection selection semantics, metadata/OpenAPI updates, and focused tests.
- Story 1.6 established the command-service pattern for resource-scoped writes: use `TimesheetsAccessGuard`, populate the relevant reference slot, return authorization denial without domain dispatch, and register services through `TryAddSingleton`.
- Story 1.6 projection work extended `TenantActivityTypeCatalogProjection` with `ProjectForProject`, restriction filtering, active/inactive capture availability, `MessageId` deduplication, and `SequenceNumber` ordering. Time Entry projections should reuse those replay/idempotency habits.
- Story 1.6 preserved tenant catalog behavior while adding project scope. Story 1.7 must preserve both tenant-level and project-level Activity Type behavior while validating capture selection.
- Story 1.6 proved the current codebase uses xUnit v3, Shouldly, NSubstitute, centralized package versions, `.slnx`, warnings as errors, and targeted test projects.
- The worktree had an unrelated modified `_bmad-output/story-automator/orchestration-1-20260618-221411.md` during story creation. Leave unrelated story-automator output alone unless the user explicitly asks to touch it.

### Latest Technical Information

- No package upgrade is required for Story 1.7. Use the repository pins in `Directory.Packages.props`: Aspire `13.4.5`, CommunityToolkit Aspire Dapr preview `13.4.0-preview.1.260602-0230`, Microsoft.Extensions `10.0.9` / `10.7.0`, OpenTelemetry `1.16.0` / instrumentation `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- Dapr remains architecture-pinned to SDK `1.18.4`, but this story should not add or upgrade Dapr packages. Domain and projection work should remain pure unless EventStore integration requires host-level wiring.
- Fluent UI V5 is a UX rule for rendering. Contracts and metadata remain runtime-neutral and must not take Fluent UI, Blazor, FrontComposer runtime, ASP.NET Core, Dapr, Redis, Entity Framework, or EventStore server dependencies.

### Testing Standards

- Use xUnit v3 and Shouldly. Use NSubstitute only where fakes/mocks are needed.
- Run test projects individually; use `.slnx` for restore/build only.
- Keep fast tests deterministic and infrastructure-free. Time Entry aggregate, authorization, projection, contract, and metadata tests should not require Dapr, Aspire, EventStore server, containers, browser, network, or a Timesheets UI project.
- Negative-path coverage is mandatory:
  - unauthorized or unresolved tenant/target/contributor authority creates no event and no projection change;
  - duplicate Time Entry IDs do not emit duplicate successful events;
  - both-targets/neither-targets cannot be represented in public command contracts and invalid target kinds reject safely;
  - non-positive duration and missing fields produce field-specific errors;
  - inactive/restricted Activity Types are unavailable for future capture;
  - stale/rebuilding/unavailable catalog or evidence projections are not treated as fresh;
  - Contracts remain infrastructure-free and do not accept server-controlled authority fields;
  - stable Party/Project/Work references are stored without copied sibling-owned state;
  - comments, command bodies, event payloads, target names, personal data, tokens, and secrets stay out of logs/traces.

### Anti-Patterns To Prevent

- Do not implement Time Entries as direct CRUD rows, mutable projection writes, local JSON files, Dapr state-store authority, Redis, SQL, or broker-backed storage.
- Do not bypass EventStore command/event patterns or persist authoritative state from API host code.
- Do not call Projects, Works, Parties, Tenants, Dapr, HTTP, logging, clocks, or the filesystem from aggregate decision logic.
- Do not trust JWT claims, caller-supplied tenant/user IDs, target IDs, contributor IDs, or correlation IDs as authorization authority.
- Do not copy Party personal data, Project state, Work state, display labels, target names, ownership, lifecycle, planned effort, managers, approvers, invoices, rates, payroll, tax, or revenue data into Timesheets.
- Do not use Activity Type display labels as identity or reporting grouping keys.
- Do not rewrite Time Entry facts when Activity Type labels/defaults/restrictions change.
- Do not conflate draft capture with submission or approval fail-closed semantics.
- Do not log comments, command bodies, event payloads, personal data, target names, tokens, secrets, or full request bodies.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract updates, if any, belong under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, `Events/TimeEntries`, `Queries/TimeEntries`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server work belongs under a new `src/Hexalith.Timesheets.Server/TimeEntries` folder and `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
- Expected authorization reuse belongs under existing `src/Hexalith.Timesheets.Server/Authorization` and `src/Hexalith.Timesheets.Server/References`; only add new adapters if a proven gap exists.
- Expected projection work belongs under a new `src/Hexalith.Timesheets.Projections/TimeEntries` folder and may reuse existing freshness/checkpoint helpers.
- Expected tests are under `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests` only if host metadata or static artifact behavior changes.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1-7-Record-Draft-Time-Entry-Against-Project-Or-Work`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Functional-Requirements`]
- [Source: `_bmad-output/planning-artifacts/epics.md#UX-Design-Requirements`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#5-1-Time-Entry-Ledger`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#5-4-Actor-Neutral-Contributors-External-Parties-And-AI-Agents`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#8-Boundaries-And-Integrations`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Component-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Camille-Records-Her-Project-Time-Before-Submitting-The-Week`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/1-6-configure-project-activity-types.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Queries/TimeEntries/ReadTimeEntryEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryTargetReference.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryComment.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAuthorizationRequest.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IContributorPartyValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IProjectReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IWorkReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimesheetsProjectionCheckpoint.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/ActivityTypeCatalogProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`
- `dotnet test ... --no-build` attempted for Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests; VSTest aborted with `System.Net.Sockets.SocketException (13): Permission denied` from `TcpListener`.
- Direct xUnit v3 executable runs completed:
  - `./tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` - 30 passed.
  - `./tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` - 139 passed.
  - `./tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` - 19 passed.
  - `./tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` - 16 passed.
  - `./tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` - 3 passed, 2 skipped by existing infrastructure/performance placeholders.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented `TimeEntry` aggregate/state handling for draft `RecordTimeEntry` decisions with typed validation rejections, duplicate ID protection, positive duration enforcement, enum sentinel checks, AI metric unit checks, policy-aware comments, and `TimeEntryRecorded` emission with `Draft` approval state.
- Added `TimeEntryCommandService` fail-closed boundary using `TimesheetsAccessGuard` with tenant, exact Project-or-Work target, Contributor Party, policy, Activity Type catalog freshness/availability, and aggregate dispatch ordering.
- Added replay-safe `TimeEntryEvidenceProjection` with `MessageId` deduplication, `SequenceNumber` ordering, stable-reference read model projection, correction placeholder state, and checkpoint-derived freshness metadata.
- Extended record-time metadata and static OpenAPI artifact for required command/read fields while keeping the FrontComposer-generated form contract and avoiding UI/runtime dependencies.
- Added focused aggregate, authorization, projection, metadata/contract, runtime registration, architecture, and integration validation coverage.

### File List

- `_bmad-output/implementation-artifacts/1-7-record-draft-time-entry-against-project-or-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryProjectionEvent.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandResult.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`

### Senior Developer Review (AI)

- Reviewer: Jerome on 2026-06-19.
- Outcome: Approved after auto-fixes. Build is clean under `-warnaserror` (0 warnings); all seven acceptance criteria verified against implementation.
- Verification: `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1` succeeded; direct xUnit v3 runs — Contracts.Tests 31, Server.Tests 157, Projections.Tests 19, ArchitectureTests 16 — all passed.
- Findings fixed:
  - [Medium] `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` was changed during implementation but was missing from the File List; added it for accurate change documentation.
  - [Medium] `TimeEntryAggregateTests.Record_rejects_invalid_ai_metric_units_and_invalid_comment_values` set `Comment = null`, so the invalid-comment subtask marked complete was not actually exercised. Split into `Record_rejects_invalid_ai_metric_units` and added `Record_rejects_invalid_comment_policy_decisions`, which constructs a comment with an `Unknown` policy decision and asserts the `comment.policy`/`unknown` field error from `TimeEntry.ValidateComment`.
- Findings noted (no change): [Low] `TimeEntry.ValidateComment` keeps unreachable blank-text/too-long/null-policy guards already enforced by the `TimeEntryComment` constructor; harmless defensive code, left in place.

### Change Log

- 2026-06-19: Implemented Story 1.7 draft Time Entry capture domain, authorization, Activity Type selection, projection, metadata/OpenAPI, and focused test coverage; status moved to review.
- 2026-06-19: Adversarial review (auto-fix). Documented the previously-omitted contract test in the File List and replaced a misnamed aggregate test with genuine invalid-comment-policy coverage (Server.Tests 156 → 157, all green). Status moved to done.
