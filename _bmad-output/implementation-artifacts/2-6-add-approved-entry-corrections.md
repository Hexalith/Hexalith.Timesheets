---
baseline_commit: 4e175ec
---

# Story 2.6: Add Approved Entry Corrections

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized contributor or reviewer,
I want approved entries to be corrected through additive events,
so that mistakes can be fixed without editing approved evidence in place.

## Acceptance Criteria

1. Given an Approved Time Entry needs a correction, when an authorized user adds a correction with corrected values and a reason where required, then a compensating correction event is persisted through EventStore, and original values, corrected values, actor, timestamp, reason, and lineage to the affected entry are preserved.
2. Given a correction supersedes prior effective values, when Time Entry Detail or reports show the entry, then users can see current effective values and correction lineage, and the UI does not imply the approved record was edited in place.
3. Given a correction command is submitted by an unauthorized user or across tenants, when the command is handled, then the command fails closed, and no correction event, projection update, or protected details are disclosed.
4. Given projection handlers replay approval and correction events, when duplicate or out-of-order deliveries are encountered within supported replay rules, then the effective read model remains idempotent, and projection freshness reflects rebuilding or degraded states when trust is limited.

## Tasks / Subtasks

- [x] Add approved-entry correction contracts without weakening rejected-entry correction (AC: 1, 2)
  - [x] Add a new approved-entry correction command under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, for example `CorrectApprovedTimeEntry`. Do not overload `CorrectRejectedTimeEntry`; that command is rejected-entry-only and must continue rejecting Approved entries.
  - [x] Add an additive approved-entry correction event under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries`, for example `TimeEntryApprovedCorrected` or an equally explicit past-tense name. It must carry `TimeEntryId`, `TimeEntryCorrectionId`, tenant, actor, UTC timestamp, previous values, corrected values, correction reason, source approval decision/lock lineage, resulting approval state, and resulting correction state.
  - [x] Add a correction-reason value object distinct from `TimeEntryRejectionReason` if the current rejection reason type cannot accurately represent approved correction rationale. Keep comments and rationale policy-aware and privacy-safe.
  - [x] Reuse `TimeEntryCorrectionValues` for previous/corrected facts where the shape matches. Add fields only additively and do not remove or rename existing `TimeEntryCorrected` fields.
  - [x] Extend `TimeEntryCorrectionEvidence` or add an approved-specific evidence DTO so read models can distinguish rejected correction lineage from approved correction lineage without requiring consumers to infer it from event names.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively. The schema must not expose caller roles, raw claims, EventStore envelopes, Project/Work display data, Party personal data, comments beyond policy, or command bodies.
- [x] Extend Time Entry aggregate/state behavior for additive approved corrections (AC: 1, 2)
  - [x] Extend `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` with a dedicated handler for the approved correction command.
  - [x] Allow approved correction only when replayed `TimeEntryState.ApprovalState == Approved` and `TimeEntryState.IsLockedFromDirectEdit == true`; the lock blocks direct mutation commands, but this explicit correction command is the allowed additive path.
  - [x] Preserve the Story 2.5 invariant: `RecordTimeEntry`, `SubmitTimeEntriesForApproval`, `ApproveTimeEntry`, `RejectTimeEntry`, and `CorrectRejectedTimeEntry` must still reject locked Approved/Superseded entries unless they are legitimate duplicate `NoOp` cases already covered by tests.
  - [x] Apply approved correction events by updating current effective values for reads while preserving approval evidence, original values, correction values, source approval decision id, actor, timestamp, and reason in lineage.
  - [x] Decide the resulting state explicitly in code and tests. Default expectation: the effective entry remains `Approved`, direct-edit locked, and correction-aware after an approved correction; do not move it back to Draft or Submitted unless a later policy story requires reapproval.
  - [x] Preserve idempotency: same approved correction id with the same corrected values returns `NoOp`; same correction id with different values, a different correction id after a terminal superseded state, non-UTC timestamp, missing reason where required, or missing lineage rejects as a typed domain outcome.
  - [x] Do not make `TimesheetPeriod` authoritative for correction state. Period stories may display or reconcile entry correction state later, but Time Entry replay state remains the write authority.
- [x] Reuse fail-closed authorization and reference-validation gates (AC: 1, 3)
  - [x] Extend or add a command service beside `TimeEntryCorrectionCommandService`. Reuse the existing gate order: tenant/resource/contributor authorization, corrected target/contributor authorization, approval-authority resolution with `ApprovalAuthorityAction.CorrectionAuthorization`, fresh Activity Type catalog validation, then aggregate dispatch.
  - [x] Authorize both the current approved entry context and the corrected values context before dispatch. Cross-tenant, stale, unavailable, ambiguous, inactive/disallowed Activity Type, invalid Project/Work/Party, or unresolved authority must fail closed before any event is produced.
  - [x] Keep denial copy safe. Safe responses must not include tenant IDs beyond authorized context, Project/Work/Party names, role names, raw claims, comments, correction reasons, command bodies, upstream problem details, EventStore envelope data, or token values.
  - [x] Preserve current `TimeEntryCorrectionCommandService` behavior for rejected-entry corrections. If shared helpers are introduced, keep the rejected and approved paths behaviorally pinned by tests.
- [x] Project approved correction lineage and effective values (AC: 2, 4)
  - [x] Extend `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` to replay approved correction events after approval and update the read model's current effective values, correction evidence, event lineage, approval evidence, and lock evidence.
  - [x] Ensure approved -> approved-corrected replay remains deterministic under duplicate `MessageId` delivery. Duplicate correction deliveries must not duplicate lineage or reapply values.
  - [x] Define supported out-of-order behavior: correction before its approval or before its recorded entry must not create a partial read model; later correctly ordered replay should produce the effective model.
  - [x] Keep projection freshness from `TimesheetsProjectionCheckpoint` visible on corrected approved entries. Stale/rebuilding/unavailable projections are display/query trust metadata only and must not become write authority.
  - [x] Keep rejected correction replay behavior unchanged: rejected -> corrected -> submitted remains unlocked until a later approval event.
- [x] Update FrontComposer metadata and UX vocabulary (AC: 2)
  - [x] Extend `TimesheetsMetadataCatalog` so Time Entry Evidence, My Timesheet Period, Approvals Queue/Period Review, reports, and future ledger/export surfaces expose approved correction evidence, current effective values, prior values, reason, correction state, lock state, and projection freshness.
  - [x] Use safe action copy for approved entries: `Add correction`. Do not introduce `Edit approved entry` copy or a generic direct-edit action.
  - [x] Keep persistent message-bar state for stale projections, unresolved authority, and correction policy blockers. Toasts are acceptable only after a successful command acknowledgement.
  - [x] If a UI project or hand-authored component is touched, use FrontComposer/Fluent UI V5 only, keyboard-accessible actions, text status badges, and accordion sections for multi-section details.
- [x] Add focused tests across contracts, aggregate, services, projections, metadata, OpenAPI, and privacy (AC: 1-4)
  - [x] Extend `TimeCaptureContractTests` for approved correction command/event serialization, correction reason behavior, approved correction evidence DTOs, metadata descriptors, OpenAPI schemas, and absence of authority/envelope/personal-data fields.
  - [x] Extend `TimeEntryAggregateTests` for Approved correction success, prior/corrected value lineage, reason requirement, actor/timestamp evidence, UTC enforcement, duplicate `NoOp`, same-id-different-values rejection, Approved direct-edit commands still locked, `CorrectRejectedTimeEntry` still rejected for Approved, Superseded/terminal invalid paths, and no history rewrite.
  - [x] Extend `TimeEntryAuthorizationTests` for approved correction fail-closed current-context authorization, corrected-value authorization, stale Activity Type catalog, inactive Activity Type, Work project-scope unresolved behavior, authority denial safe copy, and successful dispatch only after all gates pass.
  - [x] Extend `TimeEntryEvidenceProjectionTests` for approved correction replay, duplicate correction dedupe, correction-before-approval ignored until valid replay order, corrected effective values, prior values retained, approval evidence retained, lock evidence retained, and freshness states.
  - [x] Extend architecture/privacy tests if new schema fields, diagnostics, logs, metadata, OpenAPI, or service results are touched. Assert no comments, correction reasons, command bodies, event payloads, token values, personal data, raw roles, raw claims, upstream details, or EventStore envelopes leak through logs or denial copy.
  - [x] Add or update an in-process IntegrationTests case only if the approved correction path crosses command service + projection + metadata/OpenAPI behavior in a way not covered by unit tests. Keep infrastructure skips explicit.
- [x] Verify affected build and test lanes (AC: 1-4)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only when in-process workflow/static metadata behavior changes.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.6.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, correction model, projection model, API patterns, FrontComposer/Fluent UI patterns, validation patterns, and project structure.
- Loaded additional PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR3, FR6, FR8, NFR2, NFR4, and the candidate event catalog.
- Loaded additional UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially approved-entry state, correction flow copy, projection freshness, FluentDialog/Accordion/DataGrid rules, and evidence/audit semantics.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-5-enforce-approved-entry-locking.md`.
- Read current Timesheets contracts, aggregate, state, command service, projection, metadata, and tests listed in References.
- Reviewed recent git history through `4e175ec feat(story-2.5): Enforce Approved Entry Locking`, `35b1119 feat(story-2.4): Correct Rejected Entries for Resubmission`, `8e43aaa feat: Implement time entry approval and rejection commands, events, and service`, `31ea687 feat(story-2.2): Enforce Timesheet Approval Authority Policy`, and `8a73645 feat(story-2.1): Submit Draft Time Entries for Approval`.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.6 owns the approved-entry correction path after Story 2.5 locked approved entries from direct mutation.
- FR3 requires Timesheets to expose how each current Time Entry state was reached. Any change to date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or Approval State emits an event.
- FR6 requires Approved Time Entries to reject direct edits and allow only additive corrections. Corrections can supersede approved entries while keeping original approval evidence visible.
- FR8 requires entry state and period state to remain distinct. Period approval can lock included approved entries from direct edit, but it must not prevent additive corrections with audit evidence.
- NFR2 requires approval, rejection, correction, and export-relevant changes to be append-only events. Approved evidence must never be silently overwritten.
- NFR4 requires correction provenance: original values, corrected values, actor, timestamp, reason where required, and lineage to the affected entry.
- UX requires approved entries to show `Add correction`, not `Edit approved entry`; corrected/superseded entries must show current value plus lineage, and reports/ledger can later include or exclude superseded entries through filters.

### Current Code State To Extend

- `CorrectRejectedTimeEntry` exists at `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectRejectedTimeEntry.cs`. It carries corrected values but no free correction reason because rejected correction uses the preserved rejection reason.
- `TimeEntryCorrected` exists at `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryCorrected.cs`. It is currently rejected-entry-specific: it carries `RejectionReason`, `RejectionDecisionId`, resulting `ApprovalState`, and `CorrectionState`.
- `TimeEntryCorrectionValues` and `TimeEntryCorrectionEvidence` exist and already model previous/corrected value pairs. Prefer reusing or extending these rather than inventing a parallel value shape.
- `TimeEntryApprovalState` currently has `Unknown`, `Draft`, `Submitted`, `Approved`, and `Rejected`. Do not renumber. `TimeEntryCorrectionState` has `Unknown`, `None`, `Corrected`, and `Superseded`. `TimeEntryLockState` has `Unknown`, `Unlocked`, `LockedFromDirectEdit`, and `SupersededLocked`.
- `TimeEntryState.LockState` is derived: `Superseded` correction state yields `SupersededLocked`; otherwise `Approved` yields `LockedFromDirectEdit`; other states are `Unlocked`.
- `TimeEntry.Handle(CorrectRejectedTimeEntry, ...)` currently returns a typed lock rejection for Approved entries. Keep that behavior; add a separate approved correction command path.
- `TimeEntryCorrectionCommandService` already composes fail-closed gates for rejected correction: current entry authorization, corrected values authorization, `CorrectionAuthorization` authority resolution, fresh Activity Type catalog validation, then aggregate dispatch. Approved correction should reuse this ordering and safe-copy discipline.
- `TimeEntryEvidenceProjection` currently replays recorded, submitted, approved, rejected, and rejected-corrected events in sequence order with `MessageId` dedupe. Approved correction replay must fit this deterministic model and preserve approval evidence plus lock evidence.
- `TimesheetsMetadataCatalog` already includes correction, lock, approval, display hydration, projection freshness, and report/ledger-facing metadata. Add approved correction fields/actions here before considering a custom UI project.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for corrections. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- `TimeEntry` is the baseline aggregate boundary for lifecycle, submission, approval/rejection state, lock state, correction lineage, target reference, contributor, billable flag, Activity Type, comments, and AI effort evidence. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Approved-entry changes use additive compensating events with superseding lineage by default. Offset entries are deferred unless later finance/export decisions require them. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Projections are rebuildable, idempotent, duplicate-tolerant, and non-authoritative for writes; read models expose stale/degraded/rebuilding states. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Public command/query contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes. Approved-entry correction is trust-bearing and must fail closed on missing authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Commands/events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add/adjust contracts metadata and read models, not build a parallel UI shell. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Approved Correction Semantics

- Approved correction is the only allowed mutation path after approval. It must be explicit and additive; it is not a bypass of the approved-entry lock guard.
- Direct mutation commands remain forbidden for Approved and Superseded entries. A successful approved correction event must preserve prior approval evidence and add correction evidence, not rewrite `TimeEntryApproved`.
- Current effective values may change in aggregate state and read models after an approved correction, but the event stream must retain prior values and the source approval decision lineage.
- The correction reason is audit evidence. Treat it like sensitive support/audit text: preserve it where authorized, but do not leak it through denial copy, logs, generic diagnostics, or unauthorized reads.
- Projection lock evidence should still show that the effective approved entry is locked from direct edit. If the implementation represents a prior version as `Superseded`, keep `SupersededLocked` behavior consistent with Story 2.5.
- Period and ledger stories will consume this lineage later. Do not implement period submission/approval, Approved-Time Ledger projection, finance export, invoice, payroll, rates, or revenue-recognition behavior in this story.

### FrontComposer / UX Guardrails

- Approved Time Entry surfaces should offer `Add correction` when authority allows. Do not use `Edit approved entry`.
- Time Entry Detail, reports, and future ledger surfaces must show current effective values and correction lineage with text status badges, not color alone.
- Projection freshness must remain visible. Use persistent message bars for stale/rebuilding/unavailable correction evidence; do not use a toast for audit-critical state.
- Approved correction should be a focused decision surface, preferably FrontComposer generated command metadata and FluentDialog semantics if/when UI composition is needed.
- Keyboard users must be able to discover correction state, reach allowed actions, and understand blocked actions without hover-only controls.
- Copy must be factual and safe. Avoid protected tenant, Project, Work, Party, role, token, raw upstream, invoice, payroll, rate, revenue, gamification, or timer-app language.

### Previous Story Intelligence

- Story 2.5 added `TimeEntryLockState`, `TimeEntryLockEvidence`, read-model lock evidence, lock metadata, aggregate lock guards, projection lock evidence, OpenAPI updates, and tests. Build/tests were re-verified after review.
- Story 2.5 review found a lock bypass for `Superseded` + `Submitted` entries because Approve/Reject lock guards were gated on validation errors. The fix made Approve/Reject lock guards unconditional after legitimate duplicate `NoOp`. Approved correction must not reintroduce conditional lock bypasses.
- Story 2.5 intentionally did not add approved correction behavior. Its test `Correct_approved_entry_rejects_with_typed_lock_rejection_without_correction_event` should remain valid for `CorrectRejectedTimeEntry`; add new tests for the new approved correction command.
- Story 2.4 implemented rejected-entry correction and resubmission by extending the existing `TimeEntry` aggregate and projection rather than creating a correction aggregate. Reuse that pattern where it fits.
- Story 2.4 established `TimeEntryCorrectionCommandService` fail-closed current/corrected reference authorization, `CorrectionAuthorization` authority resolution, safe denial copy, and fresh Activity Type catalog validation. Do not weaken these gates.
- Story 2.3 established approval/rejection commands, terminal-state handling, approval authority source attribution, projection ordering/dedupe, and safe denial copy.
- Story 2.2 established authority source precedence, fail-closed resolver behavior, and `ApprovalAuthorityAction.CorrectionAuthorization`. Do not change resolver precedence or self-approval behavior without explicit tests and policy rationale.

### Git Intelligence Summary

- `4e175ec` implemented Story 2.5 approved-entry lock contracts, aggregate guards, projection lock evidence, metadata/OpenAPI, and regression fixes for superseded lock bypass.
- `35b1119` implemented Story 2.4 rejected correction contracts, aggregate transition, correction service, projection lineage, metadata/OpenAPI, focused tests, and review fixes.
- `8e43aaa` implemented Story 2.3 approval/rejection commands, events, command service, projection, metadata, OpenAPI, and tests.
- `31ea687` implemented Story 2.2 approval authority policy with resolver/provider seams and fail-closed defaults.
- `8a73645` implemented Story 2.1 submission with batch handling, partial blocked entries, submission service, and projection updates.

### Latest Technical Information

- No package upgrade is required for Story 2.6. Use repository pins in `Directory.Packages.props` and keep versions centralized.
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
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.6 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Approved correction tests must include negative paths for cross-tenant attempts, stale/unavailable authority, stale Activity Type catalog, inactive/disallowed Activity Type, duplicate command delivery, same-id-different-values attempts, Superseded/terminal invalid state, projection duplicate delivery, and safe denial copy.
- Projection tests must prove approved correction evidence is derived from additive correction events, idempotent under duplicate delivery, paired with approval/lock evidence, and paired with freshness metadata.

### Anti-Patterns To Prevent

- Do not implement approved correction as a direct edit or by weakening the approved-entry lock guard.
- Do not repurpose `CorrectRejectedTimeEntry` for approved entries if doing so blurs rejected vs approved semantics or breaks existing tests.
- Do not remove, rename, rewrite, or mutate existing `TimeEntryApproved`, `TimeEntryCorrected`, `TimeEntryLockEvidence`, or correction-value fields.
- Do not create a parallel correction aggregate unless architecture is explicitly changed; current stories use `TimeEntry` as the correction lineage boundary.
- Do not make projections or `TimesheetPeriod` authoritative for correction writes.
- Do not implement period submission/approval, Approved-Time Ledger projection, report generation, export generation, payroll, invoice, rates, or revenue-recognition behavior.
- Do not trust command-body tenant, user, role, claims, authority source, policy source, correlation, timestamp, or other server-controlled context.
- Do not use stale, unavailable, ambiguous, disabled, contradictory, inactive, disallowed, or cross-tenant authority/reference data for successful corrections.
- Do not leak protected IDs, comments, correction reasons, rejected comments, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, raw claims, or raw upstream problem details in denial messages or logs.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, `Events/TimeEntries`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server updates belong under `src/Hexalith.Timesheets.Server/TimeEntries` and possibly `Events/Rejections` only if adding a typed approved-correction rejection code is necessary. Prefer existing `TimeEntryLocked`, `ValidationFailed`, and authority rejection codes when they are semantically precise.
- Expected projection updates belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use `IntegrationTests` only for in-process workflow/static metadata behavior that genuinely crosses project boundaries.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-6-Add-Approved-Entry-Corrections`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-3-Preserve-entry-history-and-correction-lineage`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-6-Lock-approved-entries-against-direct-edits`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-8-Reconcile-entry-level-and-period-level-approval`]
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
- [Source: `_bmad-output/implementation-artifacts/2-5-enforce-approved-entry-locking.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectRejectedTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryCorrected.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/Rejections/TimesheetsRejectionCode.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionValues.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryLockEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`]
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

- Red check: `dotnet build tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore -warnaserror -m:1 /nr:false` failed before implementation with missing `CorrectApprovedTimeEntry`, `TimeEntryCorrectionReason`, and `TimeEntryApprovedCorrected`.
- `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --no-build` was blocked by local VSTest socket permission (`SocketException (13): Permission denied`), so validation used the README direct xUnit v3 executable fallback.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- Direct xUnit v3 fallback passed: ArchitectureTests 19/19, Contracts.Tests 49/49, Server.Tests 249/249, Projections.Tests 32/32, IntegrationTests 22 passed and 2 explicit infrastructure/performance skips (the new `ApprovedEntryCorrectionsE2ETests` added 3 in-process workflow cases).

### Completion Notes List

- Added explicit approved-entry correction contracts: `CorrectApprovedTimeEntry`, `TimeEntryApprovedCorrected`, `TimeEntryCorrectionReason`, and approved-specific correction evidence.
- Added a dedicated aggregate handler that allows additive approved correction while keeping rejected correction and direct-edit commands locked for Approved/Superseded entries.
- Extended the correction command service with the same fail-closed gate order for approved corrections: current context authorization, corrected values authorization, correction authority, fresh Activity Type catalog, then aggregate dispatch.
- Extended projection replay so approved corrections update effective read values, preserve approval and lock evidence, dedupe duplicate deliveries, and ignore unsupported out-of-order correction events.
- Updated FrontComposer metadata and OpenAPI schemas with safe `Add correction` vocabulary, approved correction evidence, reason, current/prior values, lock state, and projection freshness.
- Added focused contract, aggregate, service, projection, metadata, OpenAPI, and privacy/safe-surface tests.

### File List

- `_bmad-output/implementation-artifacts/2-6-add-approved-entry-corrections.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectApprovedTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApprovedCorrected.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryApprovedCorrectionEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryCorrectionReason.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovedEntryCorrectionsE2ETests.cs`

### Change Log

- 2026-06-19: Implemented approved-entry additive correction contracts, aggregate/service/projection behavior, metadata/OpenAPI updates, and focused validation coverage. Story ready for review.
- 2026-06-19: Senior Developer Review (AI) — auto-fix pass. Hardened `TimeEntryCorrectionReason` with constructor validation to enforce its published OpenAPI contract (`minLength: 1`, `maxLength: 1024`), matching the sibling `TimeEntryRejectionReason`. Documented the previously missing `ApprovedEntryCorrectionsE2ETests` in the File List and corrected the integration test count in Debug Log References. Outcome: Approve. Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot · **Date:** 2026-06-19 · **Outcome:** Approve (auto-fix applied)

### Scope

Adversarial validation of all File List changes against the four Acceptance Criteria, all `[x]` tasks, git reality, code quality, security/privacy, and test quality.

### Verified

- **AC1** — `TimeEntryApprovedCorrected` is an additive compensating event carrying previous values, corrected values, actor, UTC timestamp, reason, and approval-decision/scope lineage; aggregate emits it only on a valid Approved + `LockedFromDirectEdit` state. No history rewrite.
- **AC2** — Projection updates current effective values while preserving `ApprovalDecision` and lock evidence (`LockEvidence.LockState == LockedFromDirectEdit` retained); metadata/OpenAPI expose `Add correction` vocabulary with no `Edit approved entry` copy.
- **AC3** — Command service reuses the fail-closed gate order (current-context auth → corrected-values auth → `CorrectionAuthorization` authority → fresh Activity Type catalog → dispatch). Denial copy is safe (`AccessDenied` / `AuthorityUnresolved`), confirmed by integration test asserting no tenant/project/reason leakage.
- **AC4** — Projection dedupes by `MessageId`, orders by sequence, and ignores correction-before-approval until valid replay order; freshness metadata preserved. Covered by dedicated projection tests.
- **Story 2.5 invariant** — Direct-mutation commands and `CorrectRejectedTimeEntry` still reject locked Approved/Superseded entries; the approved-correction lock guard rejects `SupersededLocked` and only permits `LockedFromDirectEdit`.
- **Build & tests (re-verified after fixes):** `dotnet build -warnaserror` clean (0/0); Contracts 49/49, Server 249/249, Projections 32/32, Architecture 19/19, Integration 22 passed + 2 explicit infrastructure skips.

### Findings & Resolutions

- **[Medium] Fixed** — `TimeEntryCorrectionReason` was a bare positional record with no validation despite the OpenAPI schema declaring `minLength: 1, maxLength: 1024` and the sibling `TimeEntryRejectionReason` enforcing both in its constructor. Added matching constructor validation.
- **[Medium] Fixed** — `ApprovedEntryCorrectionsE2ETests.cs` existed in git but was absent from the File List; added.
- **[Low] Fixed** — Stale integration-test count in Debug Log References corrected (19 → 22 passed).
- **[Low] Accepted** — Two near-identical `CorrectAsync` / `TryResolveActivityTypeScope` overloads in the command service. The story explicitly sanctions behaviorally-pinned duplication between the rejected and approved paths; left unchanged.
