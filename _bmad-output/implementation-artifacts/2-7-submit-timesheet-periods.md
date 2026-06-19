---
baseline_commit: 0d66912
---

# Story 2.7: Submit Timesheet Periods

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor,
I want to submit a weekly or monthly Timesheet Period containing my entries,
so that my work can be reviewed as a coherent period without losing entry-level evidence.

## Acceptance Criteria

1. Given a contributor has Draft or already Submitted entries within a tenant-policy period boundary, when they submit a Timesheet Period, then the period is scoped to one Contributor, one Tenant, and one weekly or monthly period, and the included Time Entry IDs and period boundary are recorded through EventStore-backed events.
2. Given period boundaries are calculated, when a Timesheet Period is created or submitted, then the calculation uses the configured tenant time-zone/period policy, and UTC audit instants remain separate from tenant-local period keys.
3. Given one or more included entries are invalid for submission, when the contributor submits the period, then blocking entries are identified with correction guidance, and valid entries plus review context are returned without silently discarding them.
4. Given a contributor attempts to submit a period for another contributor or tenant without authority, when the command is handled, then it fails closed, and no period submission event, entry submission event, or cross-tenant details are produced.
5. Given the contributor reviews My Timesheet Period in the UI, when the period contains Draft, Submitted, Rejected, Corrected, or Superseded entries, then period state is shown separately from entry states, and status badges, filters, required-field markers, and keyboard navigation remain accessible.

## Tasks / Subtasks

- [x] Add Timesheet Period contracts and period-policy value objects (AC: 1, 2, 5)
  - [x] Add `SubmitTimesheetPeriod` under `src/Hexalith.Timesheets.Contracts/Commands/TimesheetPeriods`. The command should carry a period submission id, contributor Party reference, weekly/monthly period request, included Time Entry IDs, and no server-controlled tenant/user/authority/correlation fields.
  - [x] Add `TimesheetPeriodSubmitted` under `src/Hexalith.Timesheets.Contracts/Events/TimesheetPeriods` with `TimesheetPeriodId`, `Tenant`, `Contributor`, `Submitter`, `SubmittedAtUtc`, `PeriodKind`, tenant-local period key, local start/end dates, tenant time-zone id, included Time Entry IDs, and period state.
  - [x] Add value objects/models for period identity, period kind, period state, tenant-local period boundary, period submission evidence, blocking entry guidance, and `TimesheetPeriodSummaryReadModel`.
  - [x] Keep enum evolution additive with `Unknown = 0`. Do not renumber existing `TimeEntryApprovalState`, `TimeEntrySubmissionScope`, `TimeEntryApprovalScope`, `TimeEntryCorrectionState`, or `TimeEntryLockState`.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively. Do not expose EventStore envelopes, caller roles, raw claims, Project/Work display data, Party personal data, comments beyond policy, or command bodies.
- [x] Implement the `TimesheetPeriod` aggregate/state boundary (AC: 1, 2, 4)
  - [x] Add `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriod.cs` and `TimesheetPeriodState.cs` following the pure `TimeEntry.Handle(...)/Apply(...)` pattern.
  - [x] Validate one tenant, one contributor, one weekly/monthly period, non-empty distinct included entry IDs, UTC `SubmittedAtUtc`, and a resolved tenant time-zone/period policy before producing events.
  - [x] Calculate tenant-local period keys from the configured tenant time zone while preserving UTC audit instants. Add DST and period-boundary golden-file fixtures for ambiguous, skipped, and cross-midnight cases.
  - [x] Make idempotent retries return `NoOp` when the same period submission id, contributor, boundary, and included entry IDs already produced the same submitted period. Reject same-id attempts with different boundary or entry membership.
  - [x] Keep `TimesheetPeriod` as grouped review evidence only. It must not duplicate entry Approval State as write authority and must not decide entry correction/lock state.
- [x] Add a period submission command service that reuses existing entry validation seams (AC: 1, 3, 4)
  - [x] Add `TimesheetPeriodSubmissionCommandService` beside existing `TimeEntrySubmissionCommandService`.
  - [x] Reuse the current fail-closed gate order: tenant/resource/contributor authorization, fresh Activity Type catalog validation, entry state validation, then aggregate dispatch. Do not trust command-body tenant, user, role, claims, or authority context.
  - [x] For Draft entries in the period, dispatch/return `TimeEntrySubmitted` transitions with `TimeEntrySubmissionScope.TimesheetPeriod` through the existing `TimeEntry.Handle(SubmitTimeEntriesForApproval, ...)` path or a shared helper. Already Submitted entries may be included without duplicate entry events.
  - [x] Block the period submission when any included entry is missing, belongs to a different tenant/contributor, falls outside the tenant-local boundary, has invalid/inactive Activity Type, has stale/unavailable trust-bearing reference data, is Rejected without valid correction/resubmission, or is Superseded/locked in a state that should not enter a new period submission.
  - [x] Return structured blocking guidance keyed by Time Entry ID and field. Do not silently submit a subset as the period unless a later explicit policy enables partial period submission.
  - [x] Keep denial copy safe: no protected tenant, Project, Work, Party names, role names, raw claims, comments, command bodies, upstream problem details, EventStore envelope data, or token values.
- [x] Project and query My Timesheet Period read models (AC: 1, 3, 5)
  - [x] Add `src/Hexalith.Timesheets.Projections/TimesheetPeriods/TimesheetPeriodSummaryProjection.cs` and supporting projection event wrapper(s), using the same `MessageId` dedupe and `TimesheetsProjectionCheckpoint` freshness conversion pattern as `TimeEntryEvidenceProjection`.
  - [x] Project period submission evidence, included entry IDs, period boundary, tenant time-zone id, period state, blocking entry guidance, and entry-level evidence summaries without making the projection write authority.
  - [x] Define supported out-of-order behavior. A period event before referenced entry evidence must not create false fresh authority; the read model should surface incomplete, rebuilding, stale, or degraded freshness as appropriate.
  - [x] Add a query service/reader under `src/Hexalith.Timesheets.Server/TimesheetPeriods` that fails closed before disclosure and returns projection freshness/trust metadata.
- [x] Update FrontComposer metadata and UX vocabulary (AC: 3, 5)
  - [x] Extend `TimesheetsMetadataCatalog` for `timesheets.projection.my-timesheet-period` with period id, period kind, period state, tenant time-zone id, local boundary, included entry count, blocking-entry guidance, and separate entry state badges.
  - [x] Add a first-class `submit-period` action bound to `Timesheets.SubmitTimesheetPeriod`. Keep the existing `submit-time-entries` action for selected-entry submission.
  - [x] Use factual copy: `Submit period`, `Record time`, `Correct entry`, `Projection is rebuilding`, `Entry needs correction`. Do not use invoice, payroll, rate, revenue, timer-app, or celebratory language.
  - [x] Preserve UX rules: FrontComposer first, Fluent UI V5 when explicit composition is needed, `FluentDataGrid` for period rows, text status badges, persistent `FluentMessageBar` for stale/blocking states, keyboard-accessible filters/actions, and no hover-only controls.
- [x] Add focused tests across contracts, aggregate, service, projections, metadata, OpenAPI, and privacy (AC: 1-5)
  - [x] Extend `TimeCaptureContractTests` or add `TimesheetPeriodContractTests` for command/event serialization, enum vocabulary, period boundary DTOs, read model shape, metadata descriptors, OpenAPI schemas, and absence of authority/envelope/personal-data fields.
  - [x] Add `TimesheetPeriodAggregateTests` for success, UTC enforcement, tenant-local boundary calculation, weekly/monthly keys, duplicate `NoOp`, same-id-different-membership rejection, empty membership rejection, and no entry-state authority in period state.
  - [x] Add `TimesheetPeriodAuthorizationTests` for cross-tenant contributor attempts, another contributor's period, stale/unavailable tenant or target authority, inactive Activity Type, missing/cross-boundary entries, and safe denial copy.
  - [x] Add `TimesheetPeriodSummaryProjectionTests` for replay, duplicate delivery, incomplete entry evidence, mixed Draft/Submitted/Rejected/Corrected/Superseded entry display, freshness states, and status badge vocabularies.
  - [x] Add or extend in-process IntegrationTests for a full period submission with two Draft entries, one already Submitted entry, and one blocked Rejected/Superseded entry. Keep infrastructure-dependent tests explicitly skipped.
  - [x] Extend architecture/privacy tests if new diagnostics, metadata, OpenAPI, logs, or service results are touched. Assert no comments, command bodies, event payloads, personal data, raw roles, raw claims, upstream details, or EventStore envelopes leak through logs or denial copy.
- [x] Verify affected build and test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only when in-process workflow/static metadata behavior changes.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.7.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, `TimesheetPeriod` aggregate boundary, projection model, API patterns, FrontComposer/Fluent UI patterns, validation patterns, and project structure.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`, especially FR4, FR7, FR8, NFR1, NFR8, NFR9, and NFR15.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially My Timesheet Period, submitted period with mixed entries, invalid entry submission, projection freshness, and evidence/audit semantics.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-6-add-approved-entry-corrections.md`.
- Read current Timesheets contracts, aggregate, submission/approval services, state, metadata, README, package pins, and tests listed in References.
- Reviewed recent git history through `0d66912 feat(story-2.6): Add Approved Entry Corrections`, `4e175ec feat(story-2.5): Enforce Approved Entry Locking`, `35b1119 feat(story-2.4): Correct Rejected Entries for Resubmission`, `8e43aaa feat: Implement time entry approval and rejection commands, events, and service`, and `31ea687 feat(story-2.2): Enforce Timesheet Approval Authority Policy`.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.7 is the first executable Timesheet Period write slice.
- FR4 requires submission to record submitter, timestamp, scope, and validation of required fields/reference/activity/comment policy.
- FR7 requires weekly/monthly Timesheet Periods scoped to one Contributor, one Tenant, and one period. Period submission records included Time Entry IDs and the period boundary.
- FR8 requires period state and entry Approval State to stay distinct. Period work must not flatten entry history or make period state authoritative for entry correction/lock decisions.
- NFR15 requires tenant-policy-aware dates, periods, and approval cutoffs. The ratified policy for this story is tenant time zone: UTC audit instants plus tenant-local period keys.
- UX requires My Timesheet Period to show Draft, Submitted, and Rejected entries with entry Approval State and required-field correction status before submission. Mixed entry states must not collapse into a single period badge.

### Current Code State To Extend

- `SubmitTimeEntriesForApproval` exists at `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/SubmitTimeEntriesForApproval.cs`. It already supports `TimeEntrySubmissionScope.TimesheetPeriod`.
- `TimeEntrySubmitted` exists at `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntrySubmitted.cs` and records submitter, tenant, UTC timestamp, submission id, submission scope, and resulting entry approval state.
- `TimeEntrySubmissionCommandService` already handles per-entry submission, dedupes repeated ids, validates Activity Type catalog freshness, blocks inactive/mismatched Activity Types, and returns partial entry results. Period submission should reuse its validation/domain path but should not silently create a partial period.
- `TimeEntry.Handle(SubmitTimeEntriesForApproval, ...)` already returns `NoOp` for duplicate submission id on an already Submitted entry, rejects locked direct-edit states, and emits `TimeEntrySubmitted` for valid Draft entries.
- `TimeEntryState` already exposes `ServiceDate`, `Contributor`, `ApprovalState`, `SubmissionScope`, `CorrectionState`, `LockState`, correction evidence, and approval evidence. Use this state as input evidence for period validation; do not copy its state into `TimesheetPeriod` as authority.
- `TimesheetsMetadataCatalog` already contains `timesheets.projection.my-timesheet-period`, but the current action is `Submit entries`. Story 2.7 should add `Submit period` and richer period boundary/state metadata.
- `ApprovalAuthorityAction` already includes `PeriodApproval` and `PeriodRejection` for Story 2.8. Story 2.7 should not implement period approval/rejection, but must avoid blocking those future actions.
- There is currently no `src/Hexalith.Timesheets.Server/TimesheetPeriods`, `src/Hexalith.Timesheets.Projections/TimesheetPeriods`, or Timesheet Period contract folder. This story establishes those seams.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for period state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- `TimesheetPeriod` is the aggregate boundary for contributor/tenant/period submission and grouped review evidence. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Operational views, Timesheet Period views, approval queues, reports, and ledgers are rebuildable projections with freshness/degraded/rebuilding states. [Source: `_bmad-output/planning-artifacts/architecture.md#Projection-model`]
- Draft creation may tolerate stale display hydration only where policy allows; submission is trust-bearing and fails closed when required references or authority cannot be resolved. [Source: `_bmad-output/planning-artifacts/architecture.md#Reference-validation-model`]
- Public command/query contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Commands/events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add/adjust contracts metadata and read models, not build a parallel UI shell. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Timesheet Period Semantics

- A Timesheet Period submission is grouped evidence over entry IDs and a tenant-local boundary. It is not an edit to entries and not an approval decision.
- Period submission should include Draft entries by producing `TimeEntrySubmitted` with `TimeEntrySubmissionScope.TimesheetPeriod`; already Submitted entries can be included without duplicate entry events.
- Rejected entries are blockers until corrected/resubmitted. Corrected entries are eligible only according to their current entry Approval State. Superseded or locked/non-current states must be displayed but should not enter a new period submission unless a later policy explicitly allows it.
- The period event should record included Time Entry IDs and boundary evidence, not entry field snapshots as authority. Projections may display entry summaries by replaying Time Entry evidence.
- Period approval/rejection belongs to Story 2.8. Do not implement `ApprovePeriod`, `RejectPeriod`, ledger, export, payroll, invoice, rate, or revenue behavior in this story.

### Previous Story Intelligence

- Story 2.6 added `CorrectApprovedTimeEntry`, `TimeEntryApprovedCorrected`, approved correction evidence, correction reason, aggregate approved-correction handling, projection replay, metadata/OpenAPI, and tests.
- Story 2.6 preserved the Story 2.5 invariant that approved/superseded entries are locked from direct edit. Period submission must not weaken that lock by routing locked entries through a generic submit/edit path.
- Story 2.6 explicitly noted that `TimesheetPeriod` must not become authoritative for correction state. Carry that forward: period projections can display correction state, but entry replay remains write authority.
- Story 2.5 fixed a lock-bypass class involving Superseded entries. Period submission must include tests for Superseded/locked entries so grouped submission cannot reintroduce that bypass.
- Story 2.4 and Story 2.6 established correction services that fail closed on current-context authorization, corrected-value authorization, authority resolution, fresh Activity Type catalog, then aggregate dispatch. Reuse this gate discipline.
- Story 2.1 established per-entry submission, partial batch reporting, and `TimeEntrySubmissionScope`. Period submission should reuse these primitives while enforcing coherent period-level behavior.

### Git Intelligence Summary

- `0d66912` implemented Story 2.6 approved-entry corrections across contracts, aggregate, command service, projections, metadata/OpenAPI, and integration tests.
- `4e175ec` implemented Story 2.5 approved-entry locking, lock evidence, metadata/OpenAPI, and regression fixes for superseded lock bypass.
- `35b1119` implemented Story 2.4 rejected correction and resubmission by extending the existing `TimeEntry` aggregate/projection rather than creating a correction aggregate.
- `8e43aaa` implemented Story 2.3 approval/rejection commands, events, command service, projection, metadata, OpenAPI, and tests.
- `31ea687` implemented Story 2.2 approval authority policy with resolver/provider seams and fail-closed defaults.

### Latest Technical Information

- No package upgrade is required for Story 2.7. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- Use .NET 10, `.slnx`, nullable, implicit usings, warnings as errors, xUnit v3, Shouldly, NSubstitute, and current README test fallback.
- Do not add inline package versions, `.sln` files, Dockerfiles, new UI package references, direct infrastructure packages, or dependency upgrades for this story.

### Project Context Reference

- EventStore context: aggregates are pure command/state to events; EventStore owns persistence and envelope metadata; projections must be idempotent; payloads, comments, and personal data must not be logged.
- Tenants context: tenant access fails closed; tenant/member/role projections are eventually consistent; authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred internal UI path.
- No Works project-context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.7 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Period tests must cover tenant time-zone policy, DST and boundary cases, one contributor/tenant constraints, blocked entries, cross-tenant attempts, duplicate command delivery, and safe denial copy.
- Projection tests must prove period evidence derives from additive period and entry events, dedupes duplicate deliveries, exposes separate period and entry states, and carries projection freshness metadata.

### Anti-Patterns To Prevent

- Do not implement Timesheet Periods as direct SQL/Redis/Dapr state or mutable projection records.
- Do not make `TimesheetPeriod` authoritative for Time Entry approval, correction, lock, or current effective values.
- Do not flatten mixed entry states into one period badge or rewrite entry history when submitting a period.
- Do not silently submit only the valid subset of entries while presenting the period as submitted.
- Do not use server-controlled tenant, user, role, claims, correlation, submitted timestamp, authority source, or period policy from the command body as authority.
- Do not use stale, unavailable, ambiguous, disabled, contradictory, inactive, disallowed, or cross-tenant authority/reference data for successful period submission.
- Do not leak protected IDs, comments, correction reasons, rejected comments, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, raw claims, or raw upstream problem details in denial messages or logs.
- Do not implement period approval/rejection, Approved-Time Ledger, report generation, finance export, invoice, payroll, rates, or revenue-recognition behavior in this story.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/TimesheetPeriods`, `Events/TimesheetPeriods`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/TimesheetPeriods`, with reuse of `TimeEntries` services/helpers where appropriate.
- Expected projection additions belong under `src/Hexalith.Timesheets.Projections/TimesheetPeriods`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use `IntegrationTests` only for in-process workflow/static metadata behavior that genuinely crosses project boundaries.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-7-Submit-Timesheet-Periods`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-4-Submit-Time-Entries-for-approval`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-7-Submit-and-approve-Timesheet-Periods`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-8-Reconcile-entry-level-and-period-level-approval`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Key-Flows`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-6-add-approved-entry-corrections.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/SubmitTimeEntriesForApproval.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntrySubmitted.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/SubmitTimeEntriesForApprovalE2ETests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-19: `dotnet test` for Contracts.Tests, Server.Tests, and Projections.Tests was blocked by local VSTest socket listener permissions (`System.Net.Sockets.SocketException (13): Permission denied`). Used the README xUnit v3 direct executable fallback.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- 2026-06-19: Direct xUnit fallback passed: ArchitectureTests 19/19, Contracts.Tests 51/51, Server.Tests 258/258, Projections.Tests 35/35, IntegrationTests 23/25 passed with 2 explicit infrastructure/performance skips.
- 2026-06-19 (review): Re-verified after review fixes. `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx -warnaserror -m:1 /nr:false` passed (0 warnings, 0 errors). Direct xUnit fallback re-run: Contracts.Tests 51/51, Server.Tests 265/265 (3 new DST/fail-closed boundary tests added), Projections.Tests 35/35, IntegrationTests 23/25 with 2 explicit infrastructure/performance skips.

### Completion Notes List

- Added Timesheet Period command/event contracts, value objects, read models, OpenAPI schemas, and FrontComposer metadata for `submit-period` and richer `my-timesheet-period` projection vocabulary.
- Implemented pure `TimesheetPeriod.Handle(...)/TimesheetPeriodState.Apply(...)` boundary with tenant-local weekly/monthly boundary evidence, UTC audit instant validation, distinct included entries, idempotent retry `NoOp`, and same-id conflict rejection.
- Added `TimesheetPeriodSubmissionCommandService` as an all-or-nothing period coordinator that validates authorization, Activity Type freshness, entry state, contributor, boundary membership, and safe denial copy before emitting draft-entry `TimeEntrySubmitted` transitions and the period event.
- Added Timesheet Period summary projection/query seams with message-id dedupe, freshness conversion, separate period and entry states, and rebuilding freshness when period evidence arrives before referenced entry evidence.
- Added focused contract, aggregate, service, projection, metadata/OpenAPI/privacy, and in-process integration tests; infrastructure-dependent integration tests remain explicitly skipped.

### File List

- `_bmad-output/implementation-artifacts/2-7-submit-timesheet-periods.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/TimesheetPeriods/SubmitTimesheetPeriod.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimesheetPeriods/TimesheetPeriodSubmitted.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimesheetPeriodBlockingEntryGuidance.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimesheetPeriodEntrySummary.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimesheetPeriodSubmissionEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimesheetPeriodSummaryReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TenantLocalPeriodBoundary.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetPeriodId.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetPeriodRequest.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/TimesheetPeriods/TimesheetPeriodProjectionEvent.cs`
- `src/Hexalith.Timesheets.Projections/TimesheetPeriods/TimesheetPeriodSummaryProjection.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/ITimesheetPeriodSummaryProjectionReader.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TenantLocalPeriodBoundaryCalculator.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TenantTimesheetPeriodPolicy.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriod.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodState.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSubmissionCommandResult.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSubmissionCommandService.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSummaryQueryResult.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSummaryQueryService.cs`
- `src/Hexalith.Timesheets.Server/TimesheetPeriods/UnavailableTimesheetPeriodSummaryProjectionReader.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/SubmitTimesheetPeriodE2ETests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimesheetPeriodSummaryProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimesheetPeriodAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimesheetPeriodAuthorizationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 2.7 Submit Timesheet Periods and moved status to review.
- 2026-06-19: Senior Developer Review (AI) completed. Auto-fixed 4 verified issues, added 3 tests, re-verified build/tests, and moved status to done.

## Senior Developer Review (AI)

**Reviewer:** Jerome — 2026-06-19
**Outcome:** Approved (auto-fix applied)
**Mode:** Adversarial review with automatic fixes (no prompting)

### Scope verified

- All application source files in the File List were read and cross-checked against the story File List and `git status`. The story File List exactly matches the actual git changes (no undocumented or phantom files). `_bmad-output/` artifacts were excluded from code review per policy.
- Build re-verified with `-warnaserror` (0 warnings, 0 errors). All fast lanes pass: Contracts 51, Server 265, Projections 35, Integration 23 (+2 explicit skips).

### Acceptance Criteria validation

- **AC1 (one contributor/tenant/period, included IDs + boundary via events):** IMPLEMENTED. `TimesheetPeriodSubmitted` records contributor, server-derived tenant, included IDs, and tenant-local boundary; aggregate enforces single contributor/tenant and weekly/monthly kind.
- **AC2 (tenant time-zone policy; UTC audit separate from local keys):** IMPLEMENTED. `submittedAtUtc` is validated as a UTC instant and stored separately from date-only tenant-local period keys.
- **AC3 (blocking entries with guidance; valid entries returned, none silently dropped):** IMPLEMENTED. All-or-nothing coordinator returns keyed `TimesheetPeriodBlockingEntryGuidance` plus `ValidTimeEntryIds`; no partial period is emitted.
- **AC4 (cross-contributor/tenant fails closed; no leakage):** IMPLEMENTED. Tenant/contributor gates run before dispatch; denial copy is sanitized; cross-tenant entries surface as missing/blocked.
- **AC5 (period state separate from entry state; accessible badges):** IMPLEMENTED at the metadata/read-model layer (no UI project in scope). Period and entry badges are distinct vocabularies; persistent message-bar and keyboard-accessible action metadata present.

### Findings and resolutions

- **[MEDIUM] Fail-open boundary calculation — FIXED.** `TenantLocalPeriodBoundaryCalculator.Calculate` silently returned an `Unknown`/`"unknown"` boundary for an unrecognized `PeriodKind` instead of failing closed. Now throws `ArgumentOutOfRangeException`; `TimesheetPeriod.ValidateSubmission` only invokes the calculator for validated Weekly/Monthly kinds, so the rejection path for an `Unknown` kind is preserved (defense in depth).
- **[MEDIUM] Boundary recomputed independently in the command service — FIXED.** The service recomputed the boundary via a second `TenantLocalPeriodBoundaryCalculator.Calculate` (and a second time-zone lookup) that could in principle diverge from the boundary recorded in the event. It now derives the boundary directly from the accepted `TimesheetPeriodSubmitted` event — single source of truth, one time-zone resolution.
- **[MEDIUM] Test-claim gap for DST/ambiguous/skipped boundary fixtures — FIXED.** The subtask claimed DST/ambiguous/skipped/cross-midnight fixtures, but ambiguous (fall-back) and skipped (spring-forward) instants were not exercised. Added aggregate tests proving date-only period keys stay stable across the Europe/Paris spring-forward (2026-03-29) and fall-back (2026-10-25) days while the audit instant remains a UTC offset, plus a fail-closed calculator test.
- **[LOW] Non-deterministic entry-dispatch order — FIXED.** Entry validation/dispatch iterated a `HashSet<TimeEntryId>`, whose enumeration order is unspecified (the integration test relied on insertion order). Replaced with a deterministic first-occurrence-ordered list (`DistinctInOrder`) used for validation, the entry command, dispatch, and `ValidTimeEntryIds`.

### Non-blocking observations (no change required)

- The entry-dispatch path passes the entry's stored `ActivityTypeScope` rather than the freshly catalog-resolved scope used by `TimeEntrySubmissionCommandService`. This is inert today (the submitted event does not carry scope and `TimeEntry.Handle` only checks for `Unknown`), so it was left as-is to avoid churn; worth aligning if the per-entry path's scope handling evolves.
- `TryResolveActivityTypeScope` uses `SingleOrDefault` over catalog items, which would throw on a duplicated `ActivityTypeId`. This mirrors the pre-existing `TimeEntrySubmissionCommandService` pattern and is not introduced by this story.
