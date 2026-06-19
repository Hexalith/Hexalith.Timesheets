---
baseline_commit: 31ea687abebf5bb93521bff2fc614a6831bf709e
---

# Story 2.3: Approve or Reject Submitted Time Entries

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an approver,
I want to approve or reject submitted Time Entries with policy-governed reasons,
so that reviewed effort becomes trusted evidence or returns to the contributor for correction.

## Acceptance Criteria

1. Given a Submitted Time Entry and an approver with resolved tenant and Project/Work authority, when the approver approves the entry, then the entry transitions to Approved through an EventStore-backed domain event, and approver, timestamp, authority source, and approval scope are recorded.
2. Given a Submitted Time Entry requires rejection or clarification, when the approver rejects the entry with a required reason, then the entry transitions to Rejected through an EventStore-backed domain event, and the rejection reason, approver, timestamp, and affected entry ID are preserved for later correction.
3. Given self-approval is not explicitly allowed by policy, when a contributor attempts to approve their own submitted entry, then the approval command is rejected, and no Approved event or ledger-eligible projection state is produced.
4. Given approver authority cannot be resolved from Tenants, Project, or Work context, when an approval or rejection command is handled, then the command fails closed, and the UI shows unresolved authority without disclosing protected cross-tenant details.
5. Given approval and rejection states are displayed in an Approvals Queue or Time Entry Detail, when users review the entry, then status badges include text, projection freshness is visible, dialogs focus required fields, and keyboard users can approve or reject without hover-only controls.

## Tasks / Subtasks

- [x] Add infrastructure-free approve/reject contracts and evidence events (AC: 1, 2, 3)
  - [x] Add `ApproveTimeEntry` and `RejectTimeEntry` under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`. Commands identify the target entry and a stable domain idempotency value such as `TimeEntryApprovalDecisionId`; reject command also carries a required reason. Do not add `TenantId`, `UserId`, `MessageId`, `CorrelationId`, role, JWT, approval-source, approver, policy-source, or authority booleans to public command bodies.
  - [x] Add `TimeEntryApproved` and `TimeEntryRejected` under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries`. Events must record `TimeEntryId`, approver `PartyReference`, tenant `TenantReference`, UTC decision timestamp, approval decision id, resulting `TimeEntryApprovalState`, `ApprovalAuthoritySourceAttribution`, and approval scope. Rejection event must also carry the required reason.
  - [x] Add only stable value-object vocabulary needed by both commands/events, for example `TimeEntryApprovalDecisionId`, `TimeEntryApprovalScope`, and a rejection reason value object with max length and blank validation. Keep new enums with `Unknown = 0` and string-enum JSON behavior consistent with `TimesheetsEnums`.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively for the new command/event schemas. Assert schemas omit caller authority and raw EventStore envelope fields.
- [x] Extend the existing Time Entry lifecycle instead of creating a parallel approval aggregate (AC: 1, 2, 3)
  - [x] Add approve and reject handling to `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs` and `TimeEntryState.cs`. Valid transition: `Submitted` -> `Approved` or `Rejected`.
  - [x] Preserve recorded and submitted evidence. Approval/rejection must not mutate date, duration, target, contributor, Activity Type, billable flag, comment, contributor category, AI metrics, submission id, submitter, submitted timestamp, or submission scope.
  - [x] Use the stable approval decision id for idempotency. A retry with the same decision id against the same resulting state is `NoOp`; a different decision id against `Approved` or `Rejected` is a typed domain rejection unless a later story defines correction/resubmission.
  - [x] Reject approval/rejection for unknown/unrecorded state, non-Submitted state, missing approver, missing tenant, non-UTC timestamp, missing/unknown authority attribution, missing approval scope, and missing/blank/too-long rejection reason.
  - [x] Keep self-approval and external authority checks out of the aggregate; the aggregate validates supplied server-owned evidence and transition invariants. The command service owns authority resolution.
- [x] Add an approve/reject command service that composes submission gates with Story 2.2 authority policy (AC: 1, 2, 3, 4)
  - [x] Add a focused service under `src/Hexalith.Timesheets.Server/TimeEntries`, for example `TimeEntryApprovalCommandService`, and register it in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Authorize base tenant/resource/contributor access through `ITimesheetsAccessGuard` before approval authority source evaluation. Build the authorization request from existing `TimeEntryState.Target` and `TimeEntryState.Contributor`; fail closed on missing tenant, missing actor, disabled tenant, stale/ambiguous/unavailable tenant, Project, Work, Party, or policy authority.
  - [x] Resolve approval authority with `ITimesheetsApprovalAuthorityResolver` using `ApprovalAuthorityAction.EntryApproval` for approve and `ApprovalAuthorityAction.EntryRejection` for reject. Pass one consistent contributor source; do not populate `ApprovalAuthorityResolutionRequest.Contributor` differently from `AuthorizationRequest.Contributor`.
  - [x] Deny self-approval by default for entry approval through the resolver. Do not special-case tenant administrators or finance reviewers around the resolver.
  - [x] Return a result shape that distinguishes authorized domain events, domain rejections, no-ops, and authority denials. Denial copy must use safe messages such as `Authority cannot be resolved.` or `Access denied for this action.` and must not include protected IDs, role names, display names, comments, command bodies, raw claims, or upstream problem details.
  - [x] Keep authority source providers fail-closed by default. Positive tests may use explicit fake providers; do not make default providers permissive.
- [x] Project approved/rejected state through evidence read models and future ledger-friendly metadata (AC: 1, 2, 3, 5)
  - [x] Extend `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs` to apply `TimeEntryApproved` and `TimeEntryRejected` after `TimeEntrySubmitted`, order by sequence number, dedupe by message id, append lineage, and preserve projection freshness, source authority, display hydration, correction state, and recorded/submitted evidence.
  - [x] Extend `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs` only as needed to expose approval/rejection evidence without copying sibling-owned state. Prefer stable IDs and `ApprovalAuthoritySourceAttribution`; do not store Party display names, Project/Work names, or raw authority payloads.
  - [x] Rejected evidence must preserve reason, approver, timestamp, affected entry id, and authority source so Story 2.4 can show the reason and correction path without reconstructing it from logs.
  - [x] Approved evidence must be explicit enough for Story 2.5 locking and Story 4.2 Approved-Time Ledger projection. Do not implement locking or ledger generation in this story.
  - [x] Extend `TimesheetsMetadataCatalog` approval descriptors if needed so Approvals Queue and Time Entry Detail can show Approved/Rejected state, text status badges, rejection reason, projection freshness, authority freshness, blocking message-bar state, and verb-specific approve/reject commands.
- [x] Add focused tests for contracts, aggregate behavior, service gates, projection replay, metadata, and privacy (AC: 1-5)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` or add focused contract tests for approve/reject command JSON, approval/rejection event JSON, `Unknown = 0` enum behavior, OpenAPI coverage, no caller authority fields, and metadata descriptors.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` for Submitted -> Approved, Submitted -> Rejected, same-decision retry NoOp, different-decision rejection, Draft/non-recorded invalid transitions, missing approver/tenant/timestamp/authority/reason validation, UTC timestamp enforcement, and evidence preservation.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` or add a focused approval service test file for fail-closed tenant, Project, Work, contributor Party, policy, approval authority stale/unavailable/ambiguous, self-approval denied by default, explicit self-approval allow if policy enables it, positive approval, positive rejection, and safe denial copy.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/ApprovalAuthorityPolicyTests.cs` if command-service consumption exposes the Story 2.2 follow-up around inconsistent contributor sources.
  - [x] Extend `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` for ordered recorded/submitted/approved and recorded/submitted/rejected replay, duplicate approval/rejection message id idempotency, unrelated entry filtering, rejected reason projection, approved evidence projection, and non-fresh checkpoint states.
  - [x] Extend `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs` so the composed kernel registers the approval command service while fail-closed defaults remain intact.
  - [x] Extend architecture/privacy tests if new logs, diagnostics, metadata, source evidence, or static artifacts are introduced. Assert no comments, command bodies, event payloads, personal data, token values, sibling display names, raw roles, raw claims, upstream problem details, or EventStore envelopes are logged or exposed.
- [x] Verify build and affected lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests only if host/static metadata endpoint behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.3.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR5, FR6-FR9 context, the candidate event catalog, success metrics SM-2/SM-3, and the self-approval assumption.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, approval/correction aggregate boundaries, API/query patterns, frontend architecture, validation patterns, and project structure.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Approvals Queue, Time Entry Detail, rejection dialogs, status badges, projection freshness, and no hover-only controls.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-2-enforce-timesheet-approval-authority-policy.md` and `_bmad-output/implementation-artifacts/2-1-submit-draft-time-entries-for-approval.md`.
- Read current Timesheets contracts, server lifecycle, approval authority, projection, metadata, package, README, and test files listed in References.
- Reviewed recent git history through `31ea687 feat(story-2.2): Enforce Timesheet Approval Authority Policy`.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.3 owns individual Time Entry approval/rejection only. It must not implement period approval, approved-entry locking, correction/resubmission, Approved-Time Ledger generation, finance export, payroll, invoice, rates, or a bespoke UI project.
- FR5 requires approval to make an entry ledger-eligible and rejection to preserve approver, timestamp, and reason. Ledger projection itself belongs to a later story; this story must emit evidence that later ledger and locking stories can consume.
- FR9 requires authority to fail closed using Tenants plus Project/Work authority context. Story 2.2 created the reusable authority resolver; this story must consume it rather than adding ad hoc role checks.
- Rejection evidence is prerequisite for Story 2.4. The reason and affected entry id must be durable event evidence, not only field errors, logs, or UI copy.
- Entry approval state and period approval state remain distinct. This story changes only entry state.

### Current Code State To Extend

- `SubmitTimeEntriesForApproval` and `TimeEntrySubmitted` already exist. Public commands intentionally omit server-controlled tenant/user/correlation/authority fields. Match this contract pattern for approve/reject commands.
- `TimeEntryApprovalState` currently contains `Draft`, `Submitted`, `Approved`, and `Rejected`. The aggregate currently handles `RecordTimeEntry` and `SubmitTimeEntriesForApproval`; there are no approve/reject commands, events, state fields, command service, or projection handling yet.
- `TimeEntryState` stores recorded evidence and submission evidence: submission id, submitter, tenant, submitted timestamp, and submission scope. Approval/rejection should add analogous decision evidence without losing existing fields.
- `TimeEntrySubmissionCommandService` shows the current trust-bearing command pattern: authorize through `ITimesheetsAccessGuard`, resolve fresh supporting state, then dispatch aggregate logic. Approval/rejection should follow the same pattern and add authority resolver consumption.
- `TimesheetsApprovalAuthorityResolver` currently runs the optional base access guard, checks actor, denies self-approval by default for `EntryApproval` and `PeriodApproval`, then evaluates ordered authority source providers. Existing review follow-up: avoid inconsistent population between `ApprovalAuthorityResolutionRequest.Contributor` and `AuthorizationRequest.Contributor`.
- `TimesheetsApprovalAuthorityResolver` treats the first provider precedence group as decisive, including unavailable/denied results. This is intentionally fail-closed in Story 2.2; do not fall through to lower-precedence authority without an explicit policy change.
- `TimeEntryEvidenceProjection` currently handles `TimeEntryRecorded` and `TimeEntrySubmitted`, dedupes by `MessageId`, orders by sequence number, appends safe lineage, and preserves display hydration/freshness. Extend it for approval/rejection; do not use projections as write authority.
- `TimesheetsMetadataCatalog` already has Approvals Queue and Time Entry Approval descriptors with approve/reject actions, authority decision/source/freshness, and blocking state. Update metadata only where new durable fields require it.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for approval/rejection state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Commands and events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Public command/query contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. Approval/rejection must preserve this ordering. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- JWT tenant/user claims are request evidence, not authority. Use `TimesheetsRequestContext` and server-side adapters/resolver output for authority, never caller-provided command fields. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Denied, unknown, stale, ambiguous, or unavailable authorization outcomes fail closed and avoid existence disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Projection handlers must tolerate at-least-once delivery, duplicate messages, replay, and rebuild. Projection reads expose freshness/degradation and remain non-authoritative for writes. [Source: `_bmad-output/planning-artifacts/architecture.md#Communication-Patterns`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add/adjust metadata and read models, not a bespoke UI. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Approval And Rejection Semantics

- Treat approval/rejection as Time Entry lifecycle transitions after submission. They are not corrections, locks, period decisions, exports, or finance decisions.
- Approver identity comes from `TimesheetsRequestContext.Actor`. Tenant scope comes from `TimesheetsRequestContext.Tenant`. Authority source comes from `ITimesheetsApprovalAuthorityResolver`. The command body should only state the target entry, the decision id, and the reject reason where applicable.
- Approval scope should be explicit event evidence, at minimum distinguishing individual entry approval from later period-scoped approval. Do not infer scope from UI labels.
- Rejection reason is durable evidence. Validate it for presence and length, but do not log or include it in diagnostics if it may contain sensitive material.
- Approval must not be allowed from stale/unavailable/ambiguous authority or projection evidence. If authority cannot be resolved, no `TimeEntryApproved`, no `TimeEntryRejected`, and no ledger-eligible state should be produced.
- Rejection also requires authority. Do not let a contributor self-reject or bypass approval authority unless policy explicitly permits it for `EntryRejection`.
- Approved entries become eligible for the future Approved-Time Ledger, but direct edit locking is Story 2.5. In this story, the durable state/evidence should make locking and ledger projection possible later.

### FrontComposer / UX Guardrails

- Approval surfaces include Approvals Queue and Time Entry Detail. This story may enrich metadata and read models, but should not create a full UI project.
- Use generated command/projection metadata. Approval/rejection controls must be hidden or disabled according to policy; unresolved authority uses persistent message-bar state, not transient toast.
- `Reject entry` requires a focused decision surface with a required reason. If UI metadata models this, it must support field-level validation and focus on the required field/control.
- Status badges must include text for Submitted, Approved, Rejected, projection freshness, and authority freshness. Do not depend on color alone.
- Keyboard users must be able to filter, select, open details, and run approve/reject actions. Hover-only actions are not acceptable.
- Copy must be factual and safe. Avoid protected tenant, Project, Work, Party, role, token, comment, and upstream details. Avoid invoice, payroll, rate, revenue, gamification, or timer-app language.

### Previous Story Intelligence

- Story 2.2 established `ApprovalAuthoritySourceAttribution`, `ApprovalAuthorityAction`, fail-closed resolver behavior, approval metadata descriptors, source precedence, and self-approval denial by default.
- Story 2.2 review left two low follow-ups. For this story, avoid inconsistent contributor sources when calling the resolver, and do not change precedence fall-through behavior without explicit tests and policy rationale.
- Story 2.1 established submission contracts, `TimeEntrySubmitted`, `TimeEntrySubmissionCommandService`, submitted projection lineage, submit metadata, and fail-closed submission tests.
- Story 2.1 review fixed a duplicate-id idempotency gap and File List omissions. For this story, test duplicate approval decision ids and ensure every changed file appears in the final File List.
- Existing adapter seams are intentionally fail-closed/unavailable by default. Positive authority tests must configure explicit fake source results rather than weakening defaults.
- `dotnet test` may fail in this sandbox due to VSTest socket permissions. Previous stories used direct xUnit v3 executable fallback after restore/build.

### Git Intelligence Summary

- `31ea687` implemented Story 2.2 with approval authority contracts, fail-closed resolver, metadata, DI registration, integration coverage, and review follow-ups.
- `8a73645` implemented Story 2.1 with submission contracts, aggregate transition, submission command service, projection updates, metadata/OpenAPI updates, integration tests, and review fixes for duplicate IDs/File List completeness.
- `9c00c00` implemented AI effort metrics with contract-safe metadata, aggregate validation, projection tests, privacy fitness coverage, and no package upgrades.
- `7f8d474` implemented evidence read models, source authority, event lineage, fail-closed evidence query service, and display hydration seams.
- `a4aa9fc` implemented the existing `TimeEntry`, `TimeEntryCommandService`, `TimeEntryState`, `TimeEntryEvidenceProjection`, and aggregate/authorization/projection tests.

### Latest Technical Information

- No package upgrade is required for Story 2.3. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- NuGet shows repo-pinned `xunit.v3` `3.2.2` targets .NET 8.0 and is computed compatible with `net10.0`; newer 4.x prereleases exist, but this story must not upgrade the test framework. [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]
- NuGet shows repo-pinned `Shouldly` `4.3.0` targets .NET 8.0 and .NET Standard 2.0. [Source: `https://www.nuget.org/packages/Shouldly/4.3.0`]
- NuGet shows repo-pinned `NSubstitute` `6.0.0-rc.1` is prerelease and should remain the existing test double library for this repository. [Source: `https://www.nuget.org/packages/NSubstitute/6.0.0-rc.1`]
- Microsoft Learn documents current `dotnet test` behavior with Microsoft Testing Platform and VSTest modes. This repository already documents direct xUnit executable fallback for local socket failures; Story 2.3 should not change global test-runner mode. [Source: `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test`]

### Project Context Reference

- EventStore context: aggregates are pure command/state to events, EventStore owns persistence and envelope metadata, projections must be idempotent, and payloads/personal data must not be logged.
- Tenants context: tenant access fails closed, tenant/member/role projections are eventually consistent, and authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, and persistent message bars are the preferred internal UI path.
- No Works project context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.3 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Positive approval/rejection tests must configure explicit fake tenant, Project/Work, Party, policy, and approval authority success states.
- Negative coverage is mandatory for stale/unavailable/ambiguous authority, self-approval denied, wrong entry state, missing rejection reason, duplicate decision id, cross-tenant target, stale projection, safe denial copy, and duplicate projection delivery.

### Anti-Patterns To Prevent

- Do not implement Story 2.3 as period approval, correction/resubmission, approved-entry locking, Approved-Time Ledger projection, export generation, payroll, invoice, rate, or revenue-recognition behavior.
- Do not add approve/reject role checks directly inside command services when the reusable authority resolver should decide.
- Do not trust command-body tenant, user, role, claims, authority source, policy source, correlation, or server-controlled context.
- Do not treat projections as approval write authority.
- Do not mutate recorded or submitted evidence when approving or rejecting. Approval/rejection changes approval state and records decision evidence only.
- Do not allow stale, unavailable, ambiguous, disabled, contradictory, or cross-tenant authority to become a successful approval/rejection decision.
- Do not leak protected IDs, comments, rejection reasons, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, raw claims, or raw upstream problem details in denial messages or logs.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries`, `Events/TimeEntries`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/TimeEntries` and `Runtime/ServiceCollectionExtensions.cs`, reusing `Authorization`, `ApprovalAuthority`, `References`, and `Policies`.
- Expected projection updates belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use IntegrationTests only for host/static artifact behavior.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-3-Approve-Or-Reject-Submitted-Time-Entries`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-5-Approve-Or-Reject-Individual-Time-Entries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-9-Resolve-Approver-Authority`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-For-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-2-enforce-timesheet-approval-authority-policy.md#Previous-Story-Intelligence`]
- [Source: `_bmad-output/implementation-artifacts/2-1-submit-draft-time-entries-for-approval.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntrySubmitted.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ApprovalAuthoritySourceAttribution.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovalAuthority/ApprovalAuthorityResolutionRequest.cs`]
- [Source: `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ApprovalAuthorityPolicyTests.cs`]
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

- 2026-06-19: `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --no-build` was blocked by VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`); used the README xUnit v3 executable fallback.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore -m:1 /nr:false` passed after adding approval contracts.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 43 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --no-restore -m:1 /nr:false` passed after aggregate approval/rejection changes.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed after aggregate lifecycle changes: 219 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --no-restore -m:1 /nr:false` passed after approval command service changes.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed after approval command service changes: 225 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore -m:1 /nr:false` passed after approval read-model/metadata changes.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed after approval read-model/metadata changes: 43 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --no-restore -m:1 /nr:false` passed after approval projection changes.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed after approval projection changes: 26 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-restore -m:1 /nr:false` passed after approval evidence privacy guard changes.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed after approval evidence privacy guard changes: 18 tests, 0 failed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- 2026-06-19: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed: 0 warnings, 0 errors.
- 2026-06-19: Final affected executable test pass completed: Contracts.Tests 43/0 failed, Server.Tests 225/0 failed, Projections.Tests 26/0 failed, ArchitectureTests 18/0 failed.
- 2026-06-19 (review): Re-verified restore + full `Hexalith.Timesheets.slnx` build with `-warnaserror` (0 warnings, 0 errors) and re-ran affected executables: Contracts.Tests 43/0, Server.Tests 225/0, Projections.Tests 26/0, ArchitectureTests 18/0, and `Hexalith.Timesheets.IntegrationTests` 16 total / 2 skipped / 0 failed (the three new approve/reject E2E facts passed).

### Completion Notes List

- Added public approve/reject command contracts without caller authority or EventStore envelope fields.
- Added approval/rejection domain event payloads with approver, tenant, UTC decision time, decision id, authority attribution, approval scope, resulting state, and durable rejection reason.
- Added approval decision id, approval scope enum, and bounded rejection reason value objects, plus additive OpenAPI schema coverage.
- Extended the existing Time Entry aggregate and state fold for Submitted -> Approved/Rejected with decision id idempotency, terminal-state rejection, UTC/server-owned evidence validation, and preservation of recorded/submitted evidence.
- Added `TimeEntryApprovalCommandService` and result shape to compose access guard checks, approval authority resolution, safe denial copy, self-approval policy behavior, and aggregate dispatch.
- Added compact approval decision evidence to the Time Entry evidence read model, projected approval/rejection events with message-id dedupe and lineage, and enriched metadata/OpenAPI descriptors for approval evidence, authority freshness, and rejection reasons.
- Extended contract, aggregate, service, projection, runtime registration, and architecture/privacy tests for approval/rejection behavior and safe static artifact exposure.
- Verified restore, full solution build with warnings as errors, and affected test executables. An infrastructure-free approve/reject end-to-end composition test (`ApproveOrRejectSubmittedTimeEntriesE2ETests`) was added under `Hexalith.Timesheets.IntegrationTests` and run; it uses fakes only (no EventStore/Dapr/Aspire) and passes. The infrastructure-dependent host/static IntegrationTests lanes remain reserved and were not exercised because host/static metadata endpoint behavior did not change.

### File List

- _bmad-output/implementation-artifacts/2-3-approve-or-reject-submitted-time-entries.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/ApproveTimeEntry.cs
- src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RejectTimeEntry.cs
- src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryApproved.cs
- src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRejected.cs
- src/Hexalith.Timesheets.Contracts/Models/TimeEntryApprovalDecisionEvidence.cs
- src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs
- src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs
- src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryApprovalDecisionId.cs
- src/Hexalith.Timesheets.Contracts/ValueObjects/TimeEntryRejectionReason.cs
- src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs
- src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json
- src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryApprovalCommandResult.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryApprovalCommandService.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs
- src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs
- src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs
- tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs
- tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs
- tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs
- tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs
- tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs
- tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs
- tests/Hexalith.Timesheets.IntegrationTests/ApproveOrRejectSubmittedTimeEntriesE2ETests.cs

## Senior Developer Review (AI)

- Reviewer: Jerome
- Date: 2026-06-19
- Outcome: **Approve** (status set to `done`; 0 critical issues remain after fixes)
- Mode: Autonomous story-automator review with automatic fixes.

### Scope Verified

- Cross-referenced the story File List against `git status` reality; reviewed every changed source/test file (commands, events, value objects, models, aggregate, state fold, command service/result, projection, metadata, OpenAPI, DI, and all test projects). Excluded `_bmad-output/` and tooling artifacts per review policy.
- Re-ran the full build and affected test executables to validate the dev record (see review Debug Log entry).

### Acceptance Criteria

- AC1 (approve → Approved, EventStore-backed, records approver/timestamp/authority source/scope): **Implemented** — `TimeEntryApproved` carries approver, tenant, UTC decision time, decision id, resulting state, authority attribution, and `IndividualEntry` scope; aggregate `TimeEntry.Handle(ApproveTimeEntry, …)` emits it only from `Submitted`.
- AC2 (reject → Rejected with required reason preserved): **Implemented** — `TimeEntryRejected` carries the durable `Reason`; `TimeEntryRejectionReason` enforces non-blank + max length; projection preserves it.
- AC3 (self-approval denied by default, no Approved/ledger state produced): **Implemented** — resolver denies self-approval unless policy opts in; service returns no domain result on denial (covered by `Approval_service_denies_self_approval_by_default_through_resolver`).
- AC4 (authority cannot be resolved → fail closed, no protected disclosure): **Implemented** — base access guard runs before resolver and domain dispatch; `SafeAuthority`/`SafeAuthorization` collapse denial copy to `Authority cannot be resolved.` / `Access denied for this action.` even when an upstream provider leaks detail (defensive, tested).
- AC5 (queue/detail badges, freshness, focused reject dialog, keyboard): **Implemented at metadata/read-model scope** — added `approvalDecision`, `rejectionReason`, `authoritySource`, `authorityFreshness` evidence fields and `authority-decision`/`authority-source` text badges; no bespoke UI added, consistent with story scope.

### Findings (all auto-fixed in this review)

- **[MEDIUM][Resolved] File List omission** — `tests/Hexalith.Timesheets.IntegrationTests/ApproveOrRejectSubmittedTimeEntriesE2ETests.cs` was created for this story but absent from the File List (the exact recurrence flagged in Previous Story Intelligence). Added to File List.
- **[MEDIUM][Resolved] Inaccurate dev record** — Completion Notes stated "IntegrationTests were not run," and the Debug Log had no integration-test entry, yet a new integration test was added and passes. Completion Notes corrected and a review Debug Log entry added recording the verified run (16 total / 2 skipped / 0 failed).
- **[LOW][Acknowledged] Test placement** — the new test is infrastructure-free (fakes only) and is closer in character to `Server.Tests` composition tests than to the reserved EventStore/Dapr/Aspire IntegrationTests lanes. Left in place (the IntegrationTests project already hosts reserved-lane skip tests and the test passes); noted for future test-organization consistency.

### Notes

- No code defects found. Aggregate idempotency (same decision id + same resulting state → NoOp; different decision id against a terminal state → typed `terminal-state` rejection), UTC/server-owned evidence validation, recorded/submitted evidence preservation, projection ordering/dedupe by message id, and consistent contributor sourcing between `AuthorizationRequest.Contributor` and `ApprovalAuthorityResolutionRequest.Contributor` are all correctly implemented and tested.

## Change Log

| Date       | Version | Description                                                                 | Author |
|------------|---------|-----------------------------------------------------------------------------|--------|
| 2026-06-19 | 1.0     | Story 2.3 implemented: approve/reject contracts, events, aggregate, command service, projection, metadata, OpenAPI, and tests. | Codex GPT-5 |
| 2026-06-19 | 1.1     | Senior Developer Review (AI): verified build/tests, fixed File List omission and inaccurate IntegrationTests record; status → done. | Jerome |
