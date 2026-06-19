---
baseline_commit: 35b1119
---

# Story 2.5: Enforce Approved Entry Locking

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor or reviewer,
I want approved entries to be locked from direct edits,
so that approved evidence remains trustworthy once it has become review evidence.

## Acceptance Criteria

1. Given a Time Entry is Approved, when a user attempts to directly edit date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or Approval State, then the command is rejected as a typed domain outcome, and no approved event history is rewritten.
2. Given an approved entry is included in a period approval or ledger-eligible state, when lock state is evaluated, then the lock decision comes from EventStore-backed domain state and approved entry events, and the Timesheet Period aggregate does not duplicate entry state as authority.
3. Given lock enforcement tests run, when they cover Draft, Submitted, Rejected, Approved, Corrected, Superseded, duplicate command, concurrent command, and cross-tenant attempts, then only allowed transitions succeed, and invalid transitions produce typed domain rejections.
4. Given lock state is displayed in Time Entry Detail, period review, reports, or ledger reads, when projections are stale, rebuilding, degraded, or unavailable, then lock state is shown with projection freshness, and stale read models are not used as write authority.

## Tasks / Subtasks

- [x] Add explicit approved-entry lock contract/read-model evidence without adding a mutable edit workflow (AC: 1, 2, 4)
  - [x] Add an additive `TimeEntryLockState` or equivalent contract enum under `src/Hexalith.Timesheets.Contracts/ValueObjects`, preserving `Unknown = 0` and string-enum JSON behavior. Expected values should distinguish at least `Unlocked`, `LockedFromDirectEdit`, and a future-compatible superseded/locked state if needed.
  - [x] Add a compact read-model/evidence type under `src/Hexalith.Timesheets.Contracts/Models`, for example `TimeEntryLockEvidence`, carrying lock state, source approval decision id where applicable, source approval scope, locked-by/locked-at evidence where available, and safe explanatory text. Do not include caller roles, raw claims, EventStore envelopes, Project/Work display data, Party personal data, comments, or command bodies.
  - [x] Extend `TimeEntryEvidenceReadModel` with lock state/evidence so Time Entry Detail, period review, reports, and future ledger projections can show whether direct mutation is locked.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively for lock state/evidence schemas. Assert OpenAPI remains free of caller authority fields, raw roles/claims, EventStore envelope fields, and copied sibling display data.
  - [x] Do not add a general "edit approved entry" command. If implementation discovers a direct edit command is already present or must be represented for tests, it must be scoped to non-approved direct facts only and must reject Approved state through the same lock guard. Approved-entry correction belongs to Story 2.6.
- [x] Centralize lock evaluation in the existing Time Entry lifecycle (AC: 1, 2, 3)
  - [x] Extend `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs` with a derived lock property or method that evaluates from replayed EventStore-backed state only. Approved entries are locked from direct mutation. Corrected rejected entries remain correction-state evidence, not approved-entry locks. Superseded entries remain locked unless a later story explicitly defines otherwise.
  - [x] Extend `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` with a focused lock guard used by any command path that could mutate entry facts or approval state. The guard must reject direct mutation of Approved entries before producing any success event.
  - [x] Add a typed rejection code such as `TimeEntryLocked` to `TimesheetsRejectionCode` if existing codes cannot distinguish lock enforcement from generic validation. Add it additively at the end of the enum.
  - [x] Preserve existing idempotency behavior: same approval decision id on already Approved remains `NoOp`; a different approval/rejection decision after Approved remains a typed rejection. Do not rewrite, remove, or replace `TimeEntryApproved` evidence.
  - [x] Preserve Story 2.4 behavior: `CorrectRejectedTimeEntry` remains valid only for Rejected entries, produces additive correction evidence, and must reject Approved entries as locked/invalid without dispatching a correction event.
  - [x] Do not make `TimesheetPeriod` authoritative for entry lock state. Period stories may reference included entry ids and grouped review evidence, but the entry lock decision comes from Time Entry replay state/projections.
- [x] Enforce fail-closed authorization and stale projection boundaries for lock-sensitive actions (AC: 1, 3, 4)
  - [x] Reuse `ITimesheetsAccessGuard` ordering from `TimeEntryApprovalCommandService` and `TimeEntryCorrectionCommandService`: tenant/resource/contributor gates run before aggregate dispatch.
  - [x] Ensure cross-tenant, stale, ambiguous, unavailable, unauthorized, or invalid Project/Work/Party references fail closed before any lock-sensitive fact mutation or approval-state mutation.
  - [x] If a service result shape is extended for lock denial, keep denial copy safe: do not include tenant, Project, Work, Party, role, comment, rejection reason, command body, raw claim, upstream problem detail, or EventStore envelope values.
  - [x] Treat projection freshness as display and query trust metadata only. A fresh projection may display lock state, but write authority must come from the aggregate state loaded from events.
- [x] Project lock state through evidence read models and FrontComposer metadata (AC: 2, 4)
  - [x] Extend `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` so `TimeEntryApproved` sets `LockedFromDirectEdit` lock evidence while preserving approval decision evidence and event lineage.
  - [x] Ensure replay of recorded -> submitted -> approved remains deterministic and idempotent, including duplicate `TimeEntryApproved` deliveries by `MessageId`.
  - [x] Ensure rejected -> corrected -> submitted remains not falsely marked as approved-locked until a later approval event occurs.
  - [x] Ensure stale, rebuilding, or unavailable `TimesheetsProjectionCheckpoint` values flow into the read model alongside lock state. Do not present stale projections as write authority.
  - [x] Extend `TimesheetsMetadataCatalog` descriptors for Time Entry Evidence, My Timesheet Period, Approvals Queue/Period Review, reports, and future ledger surfaces with lock state/evidence fields, status badge vocabulary, persistent freshness message state, and a safe action label such as `Add correction` only as future/disabled metadata if needed. Do not expose `Edit approved entry`.
- [x] Add focused tests for contracts, aggregate transitions, service gates, projections, metadata, OpenAPI, and privacy (AC: 1-4)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` for lock enum sentinel/string JSON behavior, lock evidence serialization, read-model fields, metadata descriptors/state badges, OpenAPI schema coverage, and absence of caller authority/envelope fields.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` for Draft, Submitted, Rejected, Approved, Corrected, Superseded, duplicate, and concurrent/stale-state attempts. Required assertions: Approved direct fact mutation rejects with typed lock/domain rejection; same approved decision id remains `NoOp`; different terminal decision rejects; Rejected correction remains allowed; Approved correction/direct edit never emits correction or replacement events.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` for cross-tenant and unavailable authority attempts before aggregate dispatch, using existing fake guard/resolver patterns and safe denial copy assertions.
  - [x] Extend `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` for approved lock evidence, duplicate approval idempotency, rejected/corrected entries not marked as approved-locked, superseded lock display if represented, unrelated entry filtering, and non-fresh checkpoint states.
  - [x] Extend `tests/Hexalith.Timesheets.IntegrationTests` only if the lock-sensitive path crosses command service + projection + metadata in-process behavior. Keep infrastructure skips explicit and do not require Dapr/Aspire/EventStore server for fast lanes.
  - [x] Extend architecture/privacy tests if static metadata, OpenAPI, diagnostics, logs, or source evidence are touched. Assert no comments, command bodies, event payloads, personal data, token values, sibling display names, raw roles, raw claims, upstream problem details, or EventStore envelopes are logged or exposed.
- [x] Verify build and affected lanes (AC: 1-4)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only when in-process workflow/static metadata endpoint behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.5.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR3, FR6, FR8, NFR2, NFR4, and the candidate event catalog.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, aggregate boundaries, correction model, projection model, API patterns, validation patterns, and project structure.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially approved-entry state, correction flow copy, projection freshness, FrontComposer/Fluent UI component rules, and evidence/audit semantics.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-4-correct-rejected-entries-for-resubmission.md`.
- Read current Timesheets contracts, aggregate, state, command services, projection, metadata, package, README, and test files listed in References.
- Reviewed recent git history through `35b1119 feat(story-2.4): Correct Rejected Entries for Resubmission`, `8e43aaa feat: Implement time entry approval and rejection commands, events, and service`, and `31ea687 feat(story-2.2): Enforce Timesheet Approval Authority Policy`.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.5 owns approved-entry direct-mutation locking. It must not implement approved-entry corrections (Story 2.6), period submission (Story 2.7), period approval (Story 2.8), Approved-Time Ledger generation (Epic 4), reports, finance export, invoice, payroll, rates, or revenue-recognition behavior.
- FR6 requires approved Time Entries to reject direct mutation and route future changes through additive corrections. Story 2.6 will add the approved-entry correction path; Story 2.5 must leave that path ready without implementing it.
- FR8 requires entry approval state and period approval state to remain distinct. Period state must not flatten or duplicate Time Entry lock authority.
- NFR2 requires approval, rejection, correction, and export-relevant changes to be append-only events. Approved evidence must never be silently overwritten.
- UX-DR23 and UX-DR24 require Time Entry Detail and correction surfaces to expose evidence and lineage without implying approved evidence can be directly edited.

### Current Code State To Extend

- `RecordTimeEntry`, `SubmitTimeEntriesForApproval`, `ApproveTimeEntry`, `RejectTimeEntry`, and `CorrectRejectedTimeEntry` already exist under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`. Public command bodies intentionally omit server-controlled tenant/user/correlation/authorization fields. Match this pattern.
- `TimeEntryRecorded`, `TimeEntrySubmitted`, `TimeEntryApproved`, `TimeEntryRejected`, and `TimeEntryCorrected` already exist under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries`. Do not change or remove existing event fields. Add new fields/types only additively.
- `TimeEntryApprovalState` currently contains `Unknown`, `Draft`, `Submitted`, `Approved`, and `Rejected`. Do not renumber these values. `TimeEntryCorrectionState` currently contains `Unknown`, `None`, `Corrected`, and `Superseded`.
- `TimeEntryState` stores recorded facts, submission evidence, approval/rejection decision evidence, rejection reason, correction state, correction id, previous values, and corrected values. Lock state should be derived from this replayed state rather than persisted separately through a projection-only flag.
- `TimeEntry.Handle(ApproveTimeEntry/RejectTimeEntry, ...)` already treats `Approved` and `Rejected` as terminal for additional approval decisions. Same approval decision id against Approved is `NoOp`; different decision id is a typed rejection.
- `TimeEntry.Handle(CorrectRejectedTimeEntry, ...)` currently allows only Rejected entries, emits `TimeEntryCorrected`, moves approval state back to Draft, and preserves rejection evidence. Approved entries must remain rejected for this path until Story 2.6 defines approved corrections.
- `TimeEntrySubmissionCommandService`, `TimeEntryApprovalCommandService`, and `TimeEntryCorrectionCommandService` show the fail-closed service composition pattern. Reuse their guard order and safe denial copy discipline.
- `TimeEntryEvidenceProjection` handles recorded/submitted/approved/rejected/corrected events, orders by sequence number, dedupes by `MessageId`, appends lineage, preserves display hydration, and flows projection freshness from `TimesheetsProjectionCheckpoint`.
- `TimesheetsMetadataCatalog` already exposes Time Entry Evidence, My Timesheet Period, Approvals Queue, approval command, correction command, and export policy descriptors. Add lock metadata here rather than creating a custom UI project.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for lock state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- `TimeEntry` is the baseline aggregate boundary for lifecycle, submission, approval/rejection state, lock state, correction lineage, target, contributor, billable flag, Activity Type, comments, and AI effort evidence. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Projections are rebuildable, idempotent, duplicate-tolerant, and non-authoritative for writes; read models expose stale/degraded/rebuilding states. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Public command/query contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes. Approved-entry locking is trust-bearing and must fail closed on missing authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Commands/events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add/adjust contracts metadata and read models, not a custom UI shell. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Approved Locking Semantics

- Direct mutation means any command path that would replace approved entry facts or approval state without an additive, approved correction event. Fields include date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, contributor category, AI metrics, and Approval State.
- Lock state is an invariant of replayed Time Entry domain state: once `TimeEntryApproved` is applied, the entry is locked from direct mutation. Future approved corrections must be additive and are Story 2.6.
- A lock rejection must not erase or modify `TimeEntryRecorded`, `TimeEntrySubmitted`, `TimeEntryApproved`, `TimeEntryRejected`, or `TimeEntryCorrected` evidence.
- Do not treat projection lock state as command authority. Projections may display lock state and freshness; aggregate state loaded from EventStore decides writes.
- Duplicate/idempotent command behavior remains explicit: exact duplicate approval decision is `NoOp`; non-idempotent retries or stale concurrent commands after Approved must reject without emitting replacement events.
- `Corrected` rejected entries are not approved-locked until a new `TimeEntryApproved` event occurs after resubmission. `Superseded` entries are not directly editable.
- Period approval may include approved entries later, but `TimesheetPeriod` must reference entry ids and grouped review evidence. It must not copy or own the entry lock decision.

### FrontComposer / UX Guardrails

- Time Entry Detail, My Timesheet Period, Approvals Queue/Period Review, reports, and future ledger surfaces should show lock state with text status, not color alone.
- Approved-entry direct-edit affordances must be disabled/hidden. If a mutation path is shown, copy should say `Add correction` only when the user is being routed to additive correction semantics; do not use `Edit approved entry`.
- Projection freshness must remain visible. Use persistent message-bar state for stale/rebuilding/unavailable evidence. Do not use a toast for audit-critical lock status.
- Keyboard users must be able to discover lock state, reach allowed actions, and understand blocked actions without hover-only controls.
- Copy must be factual and safe. Avoid protected tenant, Project, Work, Party, role, token, raw upstream, invoice, payroll, rate, revenue, gamification, or timer-app language.

### Previous Story Intelligence

- Story 2.4 implemented rejected-entry correction and resubmission by extending the existing `TimeEntry` aggregate and projection rather than creating a correction aggregate. Reuse that pattern.
- Story 2.4 established `TimeEntryCorrectionCommandService` fail-closed current/corrected reference authorization, `CorrectionAuthorization` authority resolution, safe denial copy, and fresh Activity Type catalog validation. Do not weaken these gates for locking.
- Story 2.4 review added a missing raw Rejected -> Submitted aggregate test and explicitly documented that correction is provider-authority-resolved, not contributor-self-owned.
- Story 2.4 verification passed direct xUnit v3 fallback after local VSTest socket permissions blocked `dotnet test`. Keep verification reporting precise and do not claim IntegrationTests were run unless they were.
- Story 2.3 established approval/rejection commands, terminal-state handling, approval authority source attribution, projection ordering/dedupe, and safe denial copy. Approved-entry locking should build on those decisions.
- Story 2.2 established authority source precedence, fail-closed resolver behavior, and `ApprovalAuthorityAction.CorrectionAuthorization`. Do not change resolver precedence fall-through behavior without explicit tests and policy rationale.

### Git Intelligence Summary

- `35b1119` implemented Story 2.4 correction contracts, aggregate transition, correction service, projection lineage, metadata/OpenAPI, focused tests, and review fixes.
- `8e43aaa` implemented Story 2.3 approval/rejection commands, events, command service, projection, metadata, OpenAPI, and tests.
- `31ea687` implemented Story 2.2 approval authority policy with resolver/provider seams and fail-closed defaults.
- `8a73645` implemented Story 2.1 submission with batch handling, partial blocked entries, submission service, and projection updates.
- `9c00c00` implemented AI effort metrics with unit-preserving contracts, aggregate validation, projection tests, and privacy guardrails.

### Latest Technical Information

- No package upgrade is required for Story 2.5. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- Current repo pins include `xunit.v3` `3.2.2`, `Shouldly` `4.3.0`, `NSubstitute` `6.0.0-rc.1`, Aspire `13.4.5`, and OpenTelemetry package lines already declared in `Directory.Packages.props`.
- Use .NET 10, `.slnx`, nullable, implicit usings, warnings as errors, and current README test fallback. Do not add inline package versions, `.sln` files, Dockerfiles, new UI package references, or dependency upgrades for this story.

### Project Context Reference

- EventStore context: aggregates are pure command/state to events, EventStore owns persistence and envelope metadata, projections must be idempotent, and payloads/personal data must not be logged.
- Tenants context: tenant access fails closed, tenant/member/role projections are eventually consistent, and authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred internal UI path.
- No Works project-context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.5 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Locking tests must include negative paths for Approved direct mutation, cross-tenant attempts, stale/unavailable authority, duplicate command delivery, non-idempotent concurrent/stale state, projection duplicate delivery, and safe denial copy.
- Projection tests must prove lock evidence is derived from approval events, idempotent under duplicate delivery, absent for non-approved corrected rejected entries, and paired with freshness metadata.

### Anti-Patterns To Prevent

- Do not implement Story 2.5 as approved-entry corrections, period submission/approval, Approved-Time Ledger projection, report generation, export generation, payroll, invoice, rate, or revenue-recognition behavior.
- Do not create a parallel lock aggregate or make `TimesheetPeriod` authoritative for entry lock state.
- Do not mutate projections directly as the source of truth for locking.
- Do not add an `Edit approved entry` command/action/copy path. Approved changes must be additive corrections in Story 2.6.
- Do not rewrite or remove prior `TimeEntryApproved` evidence.
- Do not trust command-body tenant, user, role, claims, authority source, policy source, correlation, timestamp, or other server-controlled context.
- Do not use stale, unavailable, ambiguous, disabled, contradictory, inactive, disallowed, or cross-tenant authority/reference data for successful lock-sensitive writes.
- Do not leak protected IDs, comments, rejection reasons, corrected comments, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, raw claims, or raw upstream problem details in denial messages or logs.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server updates belong under `src/Hexalith.Timesheets.Server/TimeEntries` and possibly `Events/Rejections` if adding a typed lock rejection code.
- Expected projection updates belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use `IntegrationTests` only for in-process workflow or host/static metadata behavior that genuinely crosses project boundaries.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-5-Enforce-Approved-Entry-Locking`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-6-Lock-Approved-Entries-Against-Direct-Edits`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-8-Reconcile-Entry-Level-And-Period-Level-Approval`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-For-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Evidence-And-Audit-Semantics`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-4-correct-rejected-entries-for-resubmission.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/SubmitTimeEntriesForApproval.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/ApproveTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RejectTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectRejectedTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejectionCode.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApproved.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryCorrected.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryApprovalCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/CorrectRejectedTimeEntriesForResubmissionE2ETests.cs`]
- [Source: `Hexalith.EventStore/_bmad-output/project-context.md`]
- [Source: `Hexalith.Tenants/_bmad-output/project-context.md`]
- [Source: `Hexalith.Parties/_bmad-output/project-context.md`]
- [Source: `Hexalith.Projects/_bmad-output/project-context.md`]
- [Source: `Hexalith.FrontComposer/_bmad-output/project-context.md`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Red-phase `dotnet test` for Contracts.Tests, Server.Tests, and Projections.Tests failed as expected because `TimeEntryLockState`, `TimeEntryLockEvidence`, read-model lock evidence, state lock properties, and `TimeEntryLocked` did not exist yet.
- `dotnet test` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- Direct xUnit v3 fallback passed (re-verified during review, 2026-06-19): Contracts.Tests 47/47, Server.Tests 243/243, Projections.Tests 30/30 (1 review test added for superseded lock evidence), ArchitectureTests 19/19, IntegrationTests 19/21 passed with 2 explicit infrastructure/performance skips.

### Completion Notes List

- Added `TimeEntryLockState` and `TimeEntryLockEvidence`, extended `TimeEntryEvidenceReadModel`, and documented the lock evidence surface in the OpenAPI artifact without adding any mutable approved-entry edit command.
- Derived lock state from replayed `TimeEntryState` and added a typed `TimeEntryLocked` rejection guard for approved-entry direct mutation paths while preserving duplicate approval `NoOp` behavior and rejected-entry correction behavior.
- Projected approved-entry lock evidence from `TimeEntryApproved` events, preserved unlocked state for rejected/corrected/resubmitted paths, and extended FrontComposer metadata/status badges for evidence, period, approvals, and report/ledger-facing surfaces.
- Added contract, aggregate, projection, architecture/privacy, metadata, and OpenAPI assertions for lock behavior and safe evidence exposure.

### File List

- `_bmad-output/implementation-artifacts/2-5-enforce-approved-entry-locking.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejectionCode.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryLockEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApproveOrRejectSubmittedTimeEntriesE2ETests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (automated adversarial review) on 2026-06-19
**Outcome:** Approved (auto-fixed) — Status set to `done`. No critical issues remain.

### Verification

- `dotnet restore` and `dotnet build Hexalith.Timesheets.slnx -warnaserror` both passed with 0 warnings / 0 errors.
- Re-ran affected lanes via the README direct xUnit v3 fallback: Contracts.Tests 47/47, Server.Tests 243/243, Projections.Tests 30/30, ArchitectureTests 19/19, IntegrationTests 19/21 (2 explicit infrastructure/performance skips).

### Acceptance Criteria Assessment

- AC1 (Approved entries reject direct edits as typed outcomes, no history rewrite): IMPLEMENTED. `TimeEntry` lock guard rejects Record/Submit/Approve/Reject/Correct on locked state with `TimesheetsRejectionCode.TimeEntryLocked`; rejections emit `TimesheetsRejection` only, never rewriting approved event evidence.
- AC2 (Lock derived from EventStore-backed state, period aggregate not authoritative): IMPLEMENTED. `TimeEntryState.LockState` is derived from replayed approval/correction state; `TimesheetPeriod` untouched.
- AC3 (Transition coverage incl. Draft/Submitted/Rejected/Approved/Corrected/Superseded, duplicate, concurrent, cross-tenant): IMPLEMENTED. Aggregate and authorization tests cover terminal-state, duplicate `NoOp`, superseded lock, and fail-closed cross-tenant correction.
- AC4 (Lock state shown with projection freshness, stale not used as write authority): IMPLEMENTED. Projection sets `LockEvidence` alongside `ProjectionFreshness`; write authority stays in the aggregate.

### Findings and Resolutions

- **[Medium][Fixed] Incomplete File List.** `tests/Hexalith.Timesheets.IntegrationTests/ApproveOrRejectSubmittedTimeEntriesE2ETests.cs` was modified (new approved-entry lock E2E test + helpers) but omitted from the Dev Agent Record File List. Added to the File List.
- **[Low][Fixed] Projection/aggregate lock inconsistency for superseded entries.** `TimeEntryEvidenceProjection.Apply(TimeEntryCorrected, ...)` unconditionally emitted `TimeEntryLockEvidence.Unlocked`, while the aggregate derives `SupersededLocked` and rejects edits with `superseded-locked`. Corrected handler now emits `TimeEntryLockEvidence.Superseded()` when `CorrectionState == Superseded`; added `Projection_marks_superseded_correction_as_superseded_locked_evidence` projection test.
- **[Low][Fixed] Inaccurate Debug Log counts.** Recorded Server.Tests 242 and IntegrationTests 18/20 were stale; updated to re-verified 243 and 19/21.

### Notes (no change required)

- The `&& errors.Count > 0` qualifier on the lock guard inside the Approve/Reject handlers is intentional and correct: it sits after the idempotent `NoOp` early-return, and Approved/Superseded states always produce a terminal-state/invalid-transition error before reaching it, so legitimate idempotent retries still return `NoOp` while non-idempotent retries surface `TimeEntryLocked`.
- `TimeEntryLockEvidence` exposes only stable references (decision id, scope, approver Party id, lock time, safe static explanation); architecture privacy test asserts the closed schema. No caller authority, claims, roles, comments, or envelope fields leak.

## Change Log

| Date       | Version | Description | Author |
|------------|---------|-------------|--------|
| 2026-06-19 | 1.2 | Adversarial review: fixed File List omission, projection superseded-lock evidence consistency (+test), and Debug Log counts. Build/tests re-verified; status set to done. | Automated Review |
| 2026-06-19 | 1.1 | Implemented approved-entry lock contracts, aggregate guard, projection evidence, metadata/OpenAPI, tests, and validation. | Codex GPT-5 |
| 2026-06-19 | 1.0 | Story 2.5 context created for approved-entry direct-mutation locking. | Codex GPT-5 |
