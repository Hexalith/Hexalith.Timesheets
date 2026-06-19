---
baseline_commit: 328ff6c
---

# Story 2.4: Correct Rejected Entries for Resubmission

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor,
I want to correct rejected entries after review,
so that specific problems can be resolved without losing the original rejection evidence.

## Acceptance Criteria

1. Given a submitted Time Entry is Rejected, when the contributor opens the correction flow, then the prior values and rejection reason are visible where authorized, and the correction is saved as an additive EventStore-backed event, not a direct edit.
2. Given a rejected entry correction is submitted, when the correction changes date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, or AI metrics, then original and corrected values are linked in lineage, and the entry can be resubmitted according to policy without deleting the rejection reason.
3. Given a correction would cross tenant boundaries, use an inactive/disallowed Activity Type, or reference unverifiable Project/Work/Party data for a trust-bearing change, when the command is handled, then it fails closed, and no partial correction is persisted.
4. Given correction events replay into read models, when projections rebuild, then rejected, corrected, and resubmitted states remain deterministic and idempotent, and projection freshness is visible wherever correction state is displayed.
5. Given the correction UI is displayed, when users inspect the rejected entry and correction form, then FrontComposer/Fluent UI V5 components show rejection reason, correction lineage, field validation, consequence-aware copy, labels, focus order, and keyboard-accessible actions, and no copy implies approved evidence can be directly edited.

## Tasks / Subtasks

- [x] Add correction contracts and lineage value objects without caller authority fields (AC: 1, 2, 3)
  - [x] Add a contributor-facing command under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, for example `CorrectRejectedTimeEntry`. It should identify the rejected `TimeEntryId`, a stable idempotency value such as `TimeEntryCorrectionId`, and the corrected date, duration, target reference, Activity Type, comment, Billable Flag, Contributor, contributor category, and AI metrics. Do not add `TenantId`, `UserId`, `CorrelationId`, `MessageId`, role, JWT, policy-source, authorization booleans, or EventStore envelope fields to the public command body.
  - [x] Add `TimeEntryCorrected` under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries`. The event must preserve the original rejected facts, corrected facts, correcting actor `PartyReference`, tenant `TenantReference`, UTC correction timestamp, correction id, affected entry id, prior approval decision id, prior rejection reason, resulting `TimeEntryApprovalState`, resulting `TimeEntryCorrectionState`, and lineage to the affected entry.
  - [x] Add compact value objects/models needed by contracts and read models, for example `TimeEntryCorrectionId`, `TimeEntryCorrectionFacts`, and `TimeEntryCorrectionEvidence`. Use existing stable reference/value objects rather than sibling-owned display models.
  - [x] Extend `TimeEntryApprovalState` additively with `Corrected` if implementation needs a distinct resubmission-ready state; keep `Unknown = 0` and string-enum JSON behavior. Preserve existing enum numeric values for `Draft`, `Submitted`, `Approved`, and `Rejected`.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively for correction command/event/read-model schemas. Assert schemas omit caller authority, EventStore envelopes, raw roles, raw claims, and protected sibling display data.
- [x] Extend the existing `TimeEntry` aggregate and state fold instead of creating a correction aggregate (AC: 1, 2, 3)
  - [x] Add correction handling to `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` and `TimeEntryState.cs`. Valid transition for this story: `Rejected` -> `Corrected`; follow-up resubmission then uses the existing submission flow to move `Corrected` -> `Submitted`.
  - [x] Preserve the prior rejection evidence in state: rejected approver, rejection timestamp, approval decision id, authority attribution, scope, and reason remain available after correction. A corrected entry must not null out or overwrite the prior rejection reason.
  - [x] Store current effective facts from `TimeEntryCorrected` in `TimeEntryState` while keeping original recorded/submitted/rejected evidence available for lineage. Date, duration, target, contributor, Activity Type, billable flag, comment, contributor category, and AI metrics may change only through the correction event.
  - [x] Use the correction id for idempotency. A retry with the same correction id against the same corrected facts is `NoOp`; a different correction id against already-corrected facts is a typed domain rejection unless a later story defines multiple rejected-entry correction revisions.
  - [x] Reject correction for unknown/unrecorded state, non-Rejected state, missing corrector, missing tenant, non-UTC timestamp, missing correction id, missing prior rejection evidence, invalid corrected facts, blank or oversized comment/reason-bearing fields, unknown enum values, and invalid AI metric/unit metadata.
  - [x] Extend `SubmitTimeEntriesForApproval` handling so a `Corrected` entry with a new submission id can transition to `Submitted`. A duplicate resubmission id remains `NoOp`; a rejected entry without a correction remains blocked.
- [x] Add a correction command service that composes fail-closed access, reference, catalog, and correction-policy gates (AC: 1, 2, 3)
  - [x] Add a focused service under `src/Hexalith.Timesheets.Server/TimeEntries`, for example `TimeEntryCorrectionCommandService`, and register it in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Authorize base tenant/resource/contributor access through `ITimesheetsAccessGuard` before aggregate dispatch. Validate both the current rejected entry context and the corrected target/contributor context so cross-tenant, stale, ambiguous, unavailable, unauthorized, or invalid Project/Work/Party references fail closed.
  - [x] Resolve correction permission with existing policy seams. If using `ITimesheetsApprovalAuthorityResolver`, use `ApprovalAuthorityAction.CorrectionAuthorization`; if correction is contributor-owned through access policy, document that boundary in the service and tests. Do not add ad hoc role checks in the command service.
  - [x] Reuse the Activity Type catalog validation pattern from `TimeEntryCommandService` and `TimeEntrySubmissionCommandService`. The corrected Activity Type must come from a fresh projection, be active/available, match tenant/project scope, and preserve the existing Work + project-scoped Activity Type blocker until the governing Project adapter exists.
  - [x] Return a result shape that distinguishes authorized domain events, domain rejections, no-ops, authorization denials, correction-policy denials, and catalog/reference denials. Safe denial copy must not include protected tenant, Project, Work, Party, role, comment, rejection reason, command body, raw claim, or upstream problem detail values.
  - [x] Do not weaken fail-closed defaults. Positive tests should configure explicit fake tenant, target, contributor, catalog, and correction-policy success states.
- [x] Project correction and resubmission evidence through read models and metadata (AC: 1, 2, 4, 5)
  - [x] Extend `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` to apply `TimeEntryCorrected` after `TimeEntryRejected`, order by sequence number, dedupe by message id, append lineage, update current effective facts, and preserve projection freshness, display hydration, source authority, prior rejection evidence, and correction evidence.
  - [x] Extend `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs` with compact correction evidence and lineage fields. Keep stable IDs and safe event summaries; do not copy Party display names, Project/Work names, raw authority payloads, or EventStore envelopes.
  - [x] Ensure replay of recorded -> submitted -> rejected -> corrected -> submitted is deterministic and idempotent. Replayed duplicate correction or resubmission delivery must not duplicate lineage rows or produce contradictory effective values.
  - [x] Extend `TimesheetsMetadataCatalog` so Time Entry Evidence, My Timesheet Period, and correction command metadata expose rejection reason, prior values, corrected values, correction state text badge, field-level validation, persistent freshness/policy message-bar state, and a keyboard-reachable `Correct entry` action.
  - [x] Do not create a bespoke UI project unless implementation discovers a documented FrontComposer metadata gap. This story should prefer Contracts metadata/read-model changes and leave full page composition to the first dedicated UI-bearing story.
- [x] Add focused tests for contracts, aggregate behavior, service gates, projection replay, metadata, OpenAPI, and privacy (AC: 1-5)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` for correction command JSON, correction event JSON, correction value objects, enum sentinel behavior, read-model correction evidence, OpenAPI coverage, and absence of caller authority/envelope fields.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` for Rejected -> Corrected, Corrected -> Submitted, same-correction retry NoOp, different-correction terminal rejection, missing prior rejection evidence, invalid corrected fields, UTC timestamp enforcement, AI metric validation, and preservation of rejection evidence.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` or add a focused correction service test file for fail-closed tenant, Project, Work, contributor Party, corrected contributor, stale catalog, inactive Activity Type, Work/project Activity Type blocker, correction-policy denial, positive correction, and safe denial copy.
  - [x] Extend `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` for recorded/submitted/rejected/corrected replay, duplicate correction message id idempotency, corrected effective facts, preserved rejection reason, resubmission after correction, unrelated entry filtering, and non-fresh checkpoint states.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs` for correction command service DI registration with fail-closed defaults intact.
  - [x] Extend architecture/privacy tests if static metadata, OpenAPI, diagnostics, logs, or source evidence are touched. Assert no comments, command bodies, event payloads, personal data, token values, sibling display names, raw roles, raw claims, upstream problem details, or EventStore envelopes are logged or exposed.
- [x] Verify build and affected lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only if host/static metadata endpoint behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.4.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR3, FR4, FR8, Data Governance, success metrics SM-2/SM-3/SM-7, and the candidate event catalog.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, correction semantics, aggregate boundaries, projection model, validation patterns, API naming, and project structure.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Correction Flow, rejected-entry state, Time Entry Detail, FrontComposerGeneratedForm, StatusBadge, MessageBar, and keyboard/focus rules.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-3-approve-or-reject-submitted-time-entries.md`.
- Read current Timesheets contracts, value objects, aggregate, command services, approval authority resolver, projection, metadata, package, README, and test files listed in References.
- Reviewed recent git history through `8e43aaa feat: Implement time entry approval and rejection commands, events, and service` and the Story 2.3 review outcome.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.4 owns rejected-entry correction and resubmission only. It must not implement approved-entry locking, approved-entry corrections, period submission/approval, Approved-Time Ledger generation, reports, finance export, invoice, payroll, rates, or revenue-recognition behavior.
- FR3 requires any change to entry facts or approval state to emit events and preserve correction lineage. Corrections are not in-place edits.
- FR4 explicitly requires rejected entries to be corrected and resubmitted without losing rejection reason history.
- FR8 requires rejected entries inside a submitted period to be corrected/resubmitted without reconstructing the whole period. Period implementation is later, but this story must preserve the entry-level evidence period stories will need.
- UX-DR24 says the correction flow starts from prior entry values and saves an additive correction/resubmission, never a direct mutation of approved evidence.

### Current Code State To Extend

- `RecordTimeEntry`, `SubmitTimeEntriesForApproval`, `ApproveTimeEntry`, and `RejectTimeEntry` already exist under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`. Public command bodies intentionally omit server-controlled tenant/user/correlation/authorization fields. Match this pattern for correction commands.
- `TimeEntryRecorded`, `TimeEntrySubmitted`, `TimeEntryApproved`, and `TimeEntryRejected` already exist under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries`. `TimeEntryRejected` preserves `TimeEntryApprovalDecisionId`, approver, tenant, UTC decision time, `ApprovalAuthoritySourceAttribution`, scope, resulting `Rejected` state, and required reason.
- `TimeEntryApprovalState` currently contains `Unknown`, `Draft`, `Submitted`, `Approved`, and `Rejected`; `TimeEntryCorrectionState` already contains `Unknown`, `None`, `Corrected`, and `Superseded`. Adding `Corrected` to approval state is an additive contract change but must not renumber existing values.
- `TimeEntryState` stores recorded facts, submission evidence, approval/rejection decision evidence, and `RejectionReason`. It currently nulls `RejectionReason` only on approval; correction handling must preserve prior rejection evidence.
- `TimeEntry.Handle(SubmitTimeEntriesForApproval, ...)` currently allows only Draft -> Submitted, with duplicate same submission id as `NoOp`. Story 2.4 must deliberately extend this path for Corrected -> Submitted without allowing raw Rejected -> Submitted.
- `TimeEntry.Handle(ApproveTimeEntry/RejectTimeEntry, ...)` treats Approved and Rejected as terminal for additional approval decisions. This should remain true for approval/rejection; correction must be a distinct lifecycle command.
- `TimeEntryCommandService` and `TimeEntrySubmissionCommandService` show the catalog validation and fail-closed access pattern. Correction should reuse these checks rather than duplicating ad hoc validation.
- `TimeEntryApprovalCommandService` composes `ITimesheetsAccessGuard` with `ITimesheetsApprovalAuthorityResolver`, uses safe denial copy, and keeps contributor sourcing consistent between authorization and authority resolution. Reuse this discipline for correction authorization.
- `TimesheetsApprovalAuthorityResolver` already supports `ApprovalAuthorityAction.CorrectionAuthorization`, but self-approval shortcut logic currently applies only to `EntryApproval` and `PeriodApproval`. Do not silently assume correction is approver-owned; make the chosen correction-policy boundary explicit and tested.
- `TimeEntryEvidenceProjection` currently handles recorded/submitted/approved/rejected, orders by sequence number, dedupes by `MessageId`, appends lineage, preserves freshness and display hydration, and ignores approval events that do not follow Submitted. Extend this pattern for correction and resubmission.
- `TimesheetsMetadataCatalog` already exposes Time Entry Evidence fields for `approvalDecision`, `rejectionReason`, `correctionState`, `projectionFreshness`, and text badge vocabularies. Add correction command/action/evidence metadata without introducing runtime UI dependencies into Contracts.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for correction/resubmission state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- `TimeEntry` is the baseline aggregate boundary for lifecycle, submission, approval/rejection state, lock state, correction lineage, target, contributor, billable flag, Activity Type, comments, and AI effort evidence. Do not create a parallel correction aggregate for Story 2.4. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Correction model is additive with superseding lineage by default; offset-entry correction is deferred. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Commands/events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Public command/query contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. Correction must preserve this ordering. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes. Correction is trust-bearing and fails closed on missing authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Projections are rebuildable, idempotent, duplicate-tolerant, and non-authoritative for writes; read models expose stale/degraded/rebuilding states. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add/adjust contracts metadata and read models, not a custom UI shell. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Correction And Resubmission Semantics

- Treat rejected-entry correction as an additive lifecycle transition on the existing Time Entry. It changes the current effective facts and correction state while preserving the original recorded facts, submitted evidence, and rejection decision evidence.
- The command body states desired corrected facts and a correction id. Corrector identity, tenant, authorization, policy source, and timestamps come from server-side context/service inputs.
- Correction should produce enough event evidence for later Story 2.7 period reconciliation and Story 4.2 ledger/report projections to show original vs corrected values and prior rejection reason.
- Corrected entries should be resubmitted through the existing submission command/service after correction. Do not invent a separate resubmission aggregate or delete the original `TimeEntrySubmitted` / `TimeEntryRejected` lineage.
- A corrected entry may change target or contributor only if the corrected references validate through server-side gates. Cross-tenant target/contributor changes fail closed before aggregate dispatch.
- Activity Type validation must run against fresh catalog data. Stale, unavailable, inactive, disallowed, missing, or scope-mismatched Activity Types block correction.
- Comments remain sensitive unstructured data. Do not log correction comments, rejection reasons, command bodies, event payloads, or AI metric source details.
- AI metrics remain multi-unit evidence. Corrections must preserve wall-clock, model/tool runtime, billable effort, token availability, and provider counts without converting tokens/runtime into human duration.

### FrontComposer / UX Guardrails

- Correction Flow is reached from Time Entry Detail or a rejection notice. Metadata should expose `Correct entry` as a verb-phrase action and should not imply spreadsheet-style direct editing.
- The correction form starts from prior entry values but must submit a correction command. UI copy should communicate that the correction changes effective evidence through lineage.
- Rejected-entry surfaces must show rejection reason where authorized, original values, corrected values after save, correction state text badge, projection freshness, and field-level validation.
- Persistent `FluentMessageBar` state is appropriate for stale projections, permission denied, unresolved correction authority, comment policy, or reference/catalog blockers. `FluentToast` is only for transient success feedback after a correction or resubmission request is accepted.
- `StatusBadge` state must include text, not color alone, for Rejected, Corrected, Submitted, projection freshness, billable state, contributor category, and correction state.
- Keyboard users must be able to open detail, invoke `Correct entry`, move through fields in reading order, submit/close the dialog, and recover from validation errors without hover-only controls.
- Copy must be factual and safe. Avoid protected tenant, Project, Work, Party, role, token, raw upstream, invoice, payroll, rate, revenue, gamification, or timer-app language.

### Previous Story Intelligence

- Story 2.3 implemented approve/reject contracts, events, aggregate transitions, command service, projection evidence, metadata, OpenAPI, and tests. Build/test verification passed after review.
- Story 2.3 review fixed File List omissions and inaccurate IntegrationTests reporting. For Story 2.4, keep the File List complete and do not claim IntegrationTests were or were not run inaccurately.
- Story 2.3 established `TimeEntryApprovalCommandService` safe denial copy and consistent contributor sourcing between `AuthorizationRequest.Contributor` and `ApprovalAuthorityResolutionRequest.Contributor`. Reuse that pattern for correction authority.
- Story 2.3 left no critical code defects. Existing aggregate idempotency, UTC validation, evidence preservation, projection ordering/dedupe, and safe static artifact exposure should be preserved.
- Story 2.2 established approval authority source attribution, fail-closed resolver behavior, source precedence, and `ApprovalAuthorityAction.CorrectionAuthorization`. Do not change resolver precedence fall-through behavior without explicit tests and policy rationale.
- Story 2.1 established submission contracts, `TimeEntrySubmitted`, submission command service, partial-batch behavior, duplicate-id idempotency, submitted projection lineage, and fail-closed submission tests. Extend this rather than creating a separate resubmission path.

### Git Intelligence Summary

- `8e43aaa` implemented Story 2.3 approval/rejection commands, events, command service, projection, metadata, OpenAPI, and tests.
- `31ea687` implemented Story 2.2 approval authority policy with resolver/provider seams and fail-closed defaults.
- `8a73645` implemented Story 2.1 submission with batch handling, partial blocked entries, submission service, and projection updates.
- `9c00c00` implemented AI effort metrics with unit-preserving contracts, aggregate validation, projection tests, and privacy guardrails.
- `7f8d474` implemented evidence read models, source authority, event lineage, fail-closed evidence query service, and display hydration seams.

### Latest Technical Information

- No package upgrade is required for Story 2.4. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- NuGet shows repo-pinned `xunit.v3` `3.2.2` targets .NET 8.0 and is computed compatible with `net10.0`; newer 4.x prereleases exist, but this story must not upgrade the test framework. [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]
- NuGet shows repo-pinned `Shouldly` `4.3.0` targets .NET 8.0 and .NET Standard 2.0. [Source: `https://www.nuget.org/packages/Shouldly/4.3.0`]
- NuGet shows repo-pinned `NSubstitute` `6.0.0-rc.1` is a prerelease package targeting .NET 8.0 and .NET Standard 2.0; keep it as the existing test double library. [Source: `https://www.nuget.org/packages/NSubstitute/6.0.0-rc.1`]
- Microsoft Learn documents .NET 10 `dotnet test` behavior across VSTest and Microsoft Testing Platform modes. This repository already documents direct xUnit v3 executable fallback for local VSTest socket failures; Story 2.4 should not change global test-runner mode. [Source: `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test`]

### Project Context Reference

- EventStore context: aggregates are pure command/state to events, EventStore owns persistence and envelope metadata, projections must be idempotent, and payloads/personal data must not be logged.
- Tenants context: tenant access fails closed, tenant/member/role projections are eventually consistent, and authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred internal UI path.
- No Works project context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.4 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Positive correction tests must configure explicit fake tenant, current target, corrected target, contributor, corrected contributor, Activity Type catalog, and correction-policy success states.
- Negative coverage is mandatory for stale/unavailable/ambiguous authority, cross-tenant corrected target/contributor, stale catalog, inactive/disallowed Activity Type, raw Rejected -> Submitted, duplicate correction id, different correction id after correction, projection duplicate delivery, safe denial copy, and preservation of rejection reason.

### Anti-Patterns To Prevent

- Do not implement Story 2.4 as approved-entry correction, approved-entry locking, period submission/approval, Approved-Time Ledger projection, report generation, export generation, payroll, invoice, rate, or revenue-recognition behavior.
- Do not create a parallel correction aggregate or mutate projections directly as the correction source of truth.
- Do not make correction a destructive edit to `TimeEntryRecorded`, `TimeEntrySubmitted`, or `TimeEntryRejected` evidence.
- Do not let a rejected entry resubmit without an intervening additive correction event.
- Do not trust command-body tenant, user, role, claims, correction authority source, policy source, correlation, timestamp, or other server-controlled context.
- Do not use stale, unavailable, ambiguous, disabled, contradictory, inactive, disallowed, or cross-tenant authority/catalog/reference data for a successful correction.
- Do not leak protected IDs, comments, rejection reasons, corrected comments, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, raw claims, or raw upstream problem details in denial messages or logs.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, `Events/TimeEntries`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/TimeEntries` and `Runtime/ServiceCollectionExtensions.cs`, reusing `Authorization`, `ApprovalAuthority`, `References`, `Policies`, and Activity Type catalog validation seams.
- Expected projection updates belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use `IntegrationTests` only for in-process workflow or host/static metadata behavior that genuinely crosses project boundaries.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-4-Correct-Rejected-Entries-For-Resubmission`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-3-Preserve-Entry-History-And-Correction-Lineage`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-4-Submit-Time-Entries-For-Approval`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-8-Reconcile-Entry-Level-And-Period-Level-Approval`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-For-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#Interaction-Primitives`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-3-approve-or-reject-submitted-time-entries.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/SubmitTimeEntriesForApproval.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/ApproveTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RejectTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntrySubmitted.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApproved.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRejected.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryApprovalDecisionEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovalAuthority/ApprovalAuthorityResolutionRequest.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryApprovalCommandService.cs`]
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
- [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]
- [Source: `https://www.nuget.org/packages/Shouldly/4.3.0`]
- [Source: `https://www.nuget.org/packages/NSubstitute/6.0.0-rc.1`]
- [Source: `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-06-19: `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --no-build` was blocked by VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`); used README direct xUnit v3 executable fallback.
- 2026-06-19: Initial restore/build attempts were blocked by restricted network access to `https://api.nuget.org/v3/index.json`; restored once from the local NuGet package cache, then the required restore command passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- 2026-06-19: Direct xUnit v3 fallback passed after `dotnet test` socket failure: Contracts.Tests 45/45, Server.Tests 235/235, Projections.Tests 28/28, ArchitectureTests 19/19, IntegrationTests 15 passed / 2 existing explicit skips.
- 2026-06-19: QA Generate E2E Tests added Story 2.4 correction/resubmission integration coverage. `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore --no-build` was blocked by VSTest socket permissions; direct xUnit v3 fallback passed: IntegrationTests 18 passed / 2 existing explicit skips.

### Completion Notes List

- Added `CorrectRejectedTimeEntry`, `TimeEntryCorrected`, `TimeEntryCorrectionId`, correction value snapshots, and correction evidence contracts without caller authority or EventStore envelope fields.
- Extended `TimeEntry` and `TimeEntryState` so rejected entries can be corrected through additive events, preserve prior rejection evidence, become Draft for resubmission, and reject duplicate/different correction attempts according to idempotency policy.
- Added `TimeEntryCorrectionCommandService` with fail-closed current/corrected reference authorization, `CorrectionAuthorization` authority resolution, Activity Type catalog validation, safe denial copy, result shape, and DI registration.
- Extended evidence read models, projection replay, metadata descriptors, and OpenAPI schemas for correction lineage, current effective values, rejection preservation, correction state, and FrontComposer `Correct entry` action metadata.
- Added focused contract, aggregate, service, projection, runtime registration, architecture/privacy, and integration metadata endpoint coverage.

### File List

- _bmad-output/implementation-artifacts/2-4-correct-rejected-entries-for-resubmission.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md
- src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectRejectedTimeEntry.cs
- src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryCorrected.cs
- src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionEvidence.cs
- src/Hexalith.Timesheets.Contracts/Models/TimeEntryCorrectionValues.cs
- src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs
- src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs
- src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryCorrectionId.cs
- src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json
- src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs
- src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandResult.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs
- tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs
- tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs
- tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs
- tests/Hexalith.Timesheets.IntegrationTests/CorrectRejectedTimeEntriesForResubmissionE2ETests.cs
- tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs
- tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs
- tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs
- tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs
- tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (automated story-automator review)
**Date:** 2026-06-19
**Outcome:** Approved (auto-fix applied) — Status set to `done`

### Scope Verified

- Adversarial cross-check of all five Acceptance Criteria against implementation, every `[x]` task vs. code, the git changeset vs. the story File List, code quality, security/privacy, and test quality.
- Build: `dotnet build Hexalith.Timesheets.slnx -warnaserror` → 0 warnings, 0 errors.
- Tests (direct xUnit v3 executables, VSTest socket fallback per README): Contracts 45/45, Server 237/237 (was 235; +2 added in review), Projections 28/28, Architecture 19/19, Integration 18 passed / 2 existing explicit skips.

### Acceptance Criteria

- **AC1 (additive correction, prior values + rejection reason visible where authorized):** PASS. `CorrectRejectedTimeEntry` carries no caller-authority/envelope fields; `TimeEntryCorrected` is additive; `TimeEntryState`/projection preserve rejection evidence; metadata exposes prior + corrected values and rejection reason where authorized.
- **AC2 (lineage links original↔corrected; resubmit without deleting rejection reason):** PASS. `PreviousValues`/`CorrectedValues` linked in event, state, read model; resubmission (`Corrected`→Draft→Submitted) preserves `RejectionReason`/`RejectionDecisionId`.
- **AC3 (fail-closed on cross-tenant / inactive Activity Type / unverifiable references; no partial persist):** PASS. `TimeEntryCorrectionCommandService` gates current AND corrected target/contributor access before authority + fresh-catalog validation before aggregate dispatch; safe denial copy; no event on denial.
- **AC4 (deterministic, idempotent replay; freshness visible):** PASS. Projection orders by sequence, dedupes by message id, applies `Corrected` only after `Rejected` and resubmission only after correction; freshness metadata carried throughout.
- **AC5 (FrontComposer/Fluent metadata, no "approved evidence editable" copy):** PASS. Correction command, evidence, and My Timesheet Period descriptors expose rejection reason, lineage, field validation, correction state badge, persistent message-bar state, and a keyboard-reachable `Correct entry` verb action; copy never implies direct editing.

### Findings & Auto-Fixes Applied

- **[MEDIUM] Out-of-scope sibling submodule bump** — `Hexalith.FrontComposer` gitlink was modified (`f4910d7` → `c057a00`) in the working tree, not listed in the File List, and the story anti-patterns explicitly forbid modifying sibling submodule files. **Fixed:** restored the submodule to the recorded commit `f4910d7` (non-recursive `git submodule update`, no nested init per repo policy). Changeset now matches the File List.
- **[LOW] Missing story-mandated negative test** — Testing Standards require explicit `raw Rejected -> Submitted` coverage; the aggregate blocked it correctly but only the projection-level and generic non-Draft cases were asserted. **Fixed:** added `Submit_rejects_rejected_state_without_intervening_correction` to `TimeEntryAggregateTests` (asserts `invalid-transition` and that state stays Rejected/None).
- **[LOW] File List completeness** — generated `tests/test-summary.md` was modified but undocumented. **Fixed:** added to File List.
- **[MEDIUM] Correction-policy boundary not made explicit or tested** — the story requires the chosen correction-policy boundary to be "explicit and tested", but `TimeEntryCorrectionCommandService` carried no doc on the boundary and every service test mocked the resolver, so the real resolver's correction decision was unasserted. **Fixed:** added an XML-doc on the service documenting that correction is authority-provider-resolved via `CorrectionAuthorization` (not contributor-self-owned; no self-approval shortcut), and added `Correction_authority_is_provider_resolved_and_fails_closed_for_self_correction_without_a_granting_provider` to `TimeEntryAuthorizationTests` using the real `TimesheetsApprovalAuthorityResolver` (self-correction still fails closed to `UnavailableSiblingAuthority` with no granting provider).

### Notes / Non-blocking observations

- `ValidateCorrection` defensively rejects missing rejection-decision/reason lineage, but those branches are unreachable through the public API (a `Rejected` state always carries them). Left as defensive code; not separately testable without reflection.
- Design choice to model the resubmission-ready state as `ApprovalState=Draft` + `CorrectionState=Corrected` (rather than adding a `Corrected` approval enum value) is sound and explicitly permitted by the story; it reuses the existing Draft→Submitted path and is disambiguated by `CorrectionState`.

## Change Log

| Date       | Version | Description | Author |
|------------|---------|-------------|--------|
| 2026-06-19 | 1.0 | Story 2.4 context created for rejected-entry correction and resubmission. | Codex GPT-5 |
| 2026-06-19 | 1.1 | Implemented rejected-entry correction contracts, aggregate transition, service gates, projection lineage, metadata/OpenAPI, and focused tests. | Codex GPT-5 |
| 2026-06-19 | 1.2 | Senior Developer Review (AI): reverted out-of-scope FrontComposer submodule bump, added mandated raw Rejected→Submitted aggregate test, completed File List. Status → done. | Jérôme Piquot |
