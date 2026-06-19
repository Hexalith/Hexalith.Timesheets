---
baseline_commit: 8a7364568c7921a3fef30d765978be87e8936dda
---

# Story 2.2: Enforce Timesheet Approval Authority Policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant or project governance owner,
I want Timesheets to resolve who can approve which entries and periods before review actions occur,
so that approval decisions fail closed and follow a clear policy rather than ad hoc role checks.

## Acceptance Criteria

1. Given tenant admins, project managers, work owners, finance reviewers, and contributors may overlap, when approver authority is evaluated, then Timesheets applies an explicit authority policy with precedence rules for entry approval, period approval, rejection, correction, and export eligibility, and the authority source used for a decision is recorded where approval evidence requires it.
2. Given self-approval is denied by default, when a contributor attempts to approve their own entry or period, then the command is rejected unless policy explicitly allows self-approval for that context, and the rejection is auditable without leaking protected cross-tenant detail.
3. Given Tenants, Project, or Work authority projections are stale, unavailable, ambiguous, or contradictory, when a trust-bearing approval decision is attempted, then the decision fails closed, and the UI exposes unresolved authority or stale evidence as a blocking state.
4. Given approver authority policy changes, when the policy is configured or updated, then future approval decisions use the new policy, and previously recorded approval evidence remains attributable to the policy/source in effect at decision time.
5. Given approval surfaces display available actions, when the current user lacks authority, then approve/reject controls are disabled or hidden according to policy, and copy is specific enough to guide action without exposing protected identifiers or sibling-owned state.
6. Given authority resolution tests run, when they cover tenant admin, project approver, work owner, finance reviewer, contributor self-approval, missing user, disabled tenant, stale projection, and cross-tenant attempts, then all unauthorized, stale, or ambiguous cases fail closed.

## Tasks / Subtasks

- [x] Add infrastructure-free approval authority vocabulary without accepting caller authority (AC: 1, 2, 4)
  - [x] Add stable contracts/value objects under `src/Hexalith.Timesheets.Contracts` for approval authority action, authority source, decision freshness/state, and evidence/source attribution. Include `Unknown = 0` on enums and JSON string enum behavior consistent with `TimesheetsEnums`.
  - [x] Cover at least these actions: entry approval, entry rejection, period approval, period rejection, correction authorization, and approved-time export eligibility.
  - [x] Represent source attribution separately from authorization input. Public commands and future approval events may carry resolved source/policy evidence, but public commands must not accept tenant/user/role/JWT/correlation authority fields.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` only for additive public contract/metadata surfaces introduced by this story.
- [x] Add a fail-closed server approval authority resolver (AC: 1-4, 6)
  - [x] Add a focused `ApprovalAuthority` server area, for example `src/Hexalith.Timesheets.Server/ApprovalAuthority`, with an `ITimesheetsApprovalAuthorityResolver`, policy/options model, source-result model, and default fail-closed source providers.
  - [x] Resolve base tenant/resource access through the existing `ITimesheetsAccessGuard` before evaluating approval-specific precedence. Preserve current ordering: tenant -> Project/Work -> contributor Party -> policy.
  - [x] Apply explicit precedence across Tenants, Project, Work, finance, and self-approval policy sources. If two sources are contradictory at the same precedence, return `AmbiguousAuthority`.
  - [x] Deny self-approval by default when `TimesheetsRequestContext.Actor` matches the Time Entry or period Contributor. Allow it only through an explicit policy option for the requested action/scope, and include the self-approval source in the resolved evidence.
  - [x] Return a result that includes allow/deny, safe denial category, source used, policy version/key, action, and freshness. Do not include Party display names, Project/Work display names, protected IDs, claims, roles, tokens, or raw sibling payloads in denial copy.
- [x] Integrate authority policy with existing authorization and UI action surfaces (AC: 3, 5)
  - [x] Extend `TimesheetsAuthorizationRequest`, `TimesheetsOperation`, or a server-only companion request only as needed to carry approval-action intent. Keep `TimesheetsRequestContext` server-only.
  - [x] Extend `TimesheetsUiAction`/`TimesheetsUiActionPolicyOutcome` or add approval-specific UI outcome metadata so approve/reject controls can be `Hidden` or `Disabled` with safe copy such as `Authority cannot be resolved.` or `Access denied for this action.`
  - [x] Update `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` with FrontComposer-compatible approval authority metadata for Approvals Queue and Time Entry/Period approval command surfaces. This story should add metadata only, not a bespoke UI project.
  - [x] Surface stale/rebuilding/unavailable authority as blocking state. Do not allow optimistic approval based on stale projection data.
- [x] Register defaults without weakening fail-closed behavior (AC: 3, 6)
  - [x] Register the approval authority resolver and fail-closed default source providers in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Keep `DenyAllTimesheetsTenantAccessValidator`, deny-all reference validators, and fail-closed evidence policy defaults intact.
  - [x] Do not replace `TimesheetsEvidencePolicyEvaluator`; compose approval authority policy with it or extend policy evaluation in a way that preserves retention/comment launch-readiness gates.
- [x] Add focused tests for contracts, resolver behavior, metadata, registration, and privacy (AC: 1-6)
  - [x] Extend `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` or add a focused contract test file for authority enum JSON, `Unknown = 0`, source evidence round-trip, OpenAPI coverage, metadata descriptors, and absence of server authority fields.
  - [x] Add `tests/Hexalith.Timesheets.Server.Tests/ApprovalAuthorityPolicyTests.cs` covering tenant admin, project approver, work owner, finance reviewer, precedence ordering, same-precedence ambiguity, self-approval denied by default, explicit self-approval allow, missing actor, disabled tenant, stale Tenants projection, stale Project/Work projection, unavailable sibling authority, invalid reference, cross-tenant target, and safe denial copy.
  - [x] Extend `AuthorizationServiceTests` where operation/action vocabulary changes, keeping existing fail-closed ordering tests green.
  - [x] Extend `RuntimeRegistrationTests` so the composed server kernel registers the resolver while default source providers still deny/unavailable.
  - [x] Extend architecture/privacy tests if new files introduce diagnostics, metadata, source evidence, or safe copy. Assert no comments, command bodies, event payloads, personal data, token values, sibling display names, raw roles, or EventStore envelopes are logged or exposed.
- [x] Verify build and affected lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected test projects individually: Contracts.Tests, Server.Tests, ArchitectureTests, and IntegrationTests only if host/static metadata endpoint behavior changes.
  - [x] If `dotnet test` is blocked by VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

### Review Follow-ups (AI)

- [ ] [AI-Review][Low] When real (non-stub) authority source providers land, decide whether a higher-precedence source returning `Unavailable` should fall through to lower-precedence governance/finance sources. Current resolver treats the highest-precedence provider group as authoritative even when it is `Unavailable`, so in the composed kernel `TenantAdministrator`/`FinanceReviewer` authority cannot be reached behind an available higher tier. This is intentional and fail-closed-safe for Story 2.2 (codified by `ApprovalAuthorityPolicyTests.Resolver_applies_source_precedence_before_tenant_governance` and `ApprovalAuthorityPolicyE2ETests.Default_kernel_authority_workflow_fails_closed_when_authority_sources_are_unavailable`), but revisit for export-eligibility precedence in a later story. [`src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs:118`]
- [ ] [AI-Review][Low] Self-approval guard reads the contributor from the top-level `ApprovalAuthorityResolutionRequest.Contributor` instead of `AuthorizationRequest.Contributor`. A caller that populates one but not the other would bypass the self-approval check. Current callers/tests always set both consistently; consider collapsing to a single source of truth when the approve/reject command services consume this resolver. [`src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs:175`]

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 2 and Story 2.2.
- Loaded `{prd_content}` from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and `addendum.md`, especially FR5, FR7, FR9, the self-approval assumption, and the open approver-precedence question.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially Authentication & Security, Validation Patterns, API/Communication Patterns, Data Architecture, Frontend Architecture, and Enforcement Guidelines.
- Loaded `{ux_content}` from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Approvals Queue, unresolved authority state, `FluentMessageBar`, disabled/hidden controls, and FrontComposer/Fluent UI rules.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/2-1-submit-draft-time-entries-for-approval.md`.
- Read current Timesheets authorization, policy, Time Entry, submission, projection, metadata, package, and test files listed in References.
- Reviewed recent git history through `8a73645 feat(story-2.1): Submit Draft Time Entries for Approval`.

### Epic And Story Context

- Epic 2 covers submission, approval, period review, locking, and corrections. Story 2.2 owns only reusable approval-authority policy resolution. Actual `TimeEntryApproved`, `TimeEntryRejected`, period submission, period approval, approved-entry locking, corrections, ledger/export generation, and finance behavior belong to later stories.
- FR5 requires approve/reject decisions to record approver, timestamp, reason where required, and deny self-approval by default. This story creates the policy/authority decision shape those later events must use.
- FR7 and FR8 require period approval to remain distinct from entry state. Authority policy must therefore support period approval/rejection without flattening entry approval state.
- FR9 requires fail-closed approver authority using Tenants membership/roles and Project/Work approver projections. Exact role names and precedence were an open PRD assumption; this story must make precedence explicit and testable.

### Current Code State To Extend

- `ITimesheetsAccessGuard` and `TimesheetsAccessGuard` already centralize tenant, Project, Work, Contributor Party, policy, and UI action visibility checks. Extend this path; do not create approve/reject-specific bypasses.
- `TimesheetsAuthorizationRequest` currently carries `Context`, `Operation`, optional `Project`, optional `Work`, optional `Contributor`, and optional `UiAction`. It has no approval-action or authority-source vocabulary yet.
- `TimesheetsOperation` currently contains generic `Command`, `Query`, `ProjectionRead`, `Export`, `Confirmation`, and `UiActionVisibility`. If this story adds operations, update `AuthorizationServiceTests.Operation_vocabulary_covers_trust_boundaries_and_ui_visibility`.
- `TimesheetsUiAction` already includes `Approval`, `Correction`, and `Export`, but not separate approve/reject or entry/period actions. Add only the vocabulary needed for authority display and keep action copy safe.
- `TimesheetsEvidencePolicyEvaluator` gates trust-bearing operations on retention/comment policy readiness. Approval authority policy must compose with this, not remove or weaken it.
- `TimeEntrySubmissionCommandService` shows the current trust-bearing command pattern: authorize through `ITimesheetsAccessGuard`, resolve fresh catalog state, then dispatch aggregate logic. Later approve/reject services should follow the same pattern using the authority resolver created here.
- `TimeEntryEvidenceProjection` already projects Draft and Submitted Time Entry evidence with source authority and lineage. Story 2.2 may add authority metadata/descriptors, but it must not use read projections as write authority.
- `TimesheetsMetadataCatalog` currently has record, submit, activity catalog, evidence, and export-policy metadata. Add approval authority descriptors here instead of creating a Timesheets UI project.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Story 2.2 should add policy/resolution code and metadata; it must not introduce SQL, Redis, Dapr state, local JSON, direct projection mutation, or broker-backed CRUD as authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. Approval authority resolution must preserve this ordering. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- JWT tenant/user claims are request evidence, not authority. Use `TimesheetsRequestContext` and server-side adapters/policy sources, never public command fields, to resolve authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Commands and events live in `Contracts`; aggregate decisions, command services, policies, validation orchestration, and authority resolution live in `Server`; read-model handlers live in `Projections`; tests live under focused `tests/Hexalith.Timesheets.*` projects. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Public contracts hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Denied, unknown, stale, ambiguous, or unavailable authorization outcomes fail closed and avoid existence disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- Internal UI uses FrontComposer first and Blazor Fluent UI V5 where explicit composition is needed. This story should add metadata and safe state outcomes, not bespoke UI. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]

### Approval Authority Semantics

- Treat approval authority as a policy decision, not as an aggregate state transition. Story 2.3 and Story 2.8 will consume this decision when emitting approve/reject events.
- The resolver input should be server-owned context plus target evidence needed to check authority: action, target kind, target reference, contributor Party, approval scope, and optional period scope. Do not accept caller-supplied roles, membership, source names, or "is approver" booleans.
- Recommended source precedence for v1, unless implementation discovers a stronger existing policy hook: explicit self-approval override for the action/scope, Project/Work approver authority for target-scoped review, tenant administrator/governance authority, finance reviewer for export eligibility only. Same-precedence contradictions must deny as ambiguous.
- Self-approval is denied by default for entry and period approval when actor and contributor Party match. Rejection and correction authority should be explicit per policy; do not accidentally allow self-approval because the actor is a tenant admin.
- Authority source evidence should be stable and later event-friendly: source enum/key, policy version/key, action, resolved freshness, and evaluation timestamp if needed. Do not store display names or copied sibling state.
- Stale, rebuilding, unavailable, ambiguous, disabled, invalid, and cross-tenant authority source states are blockers for trust-bearing approval decisions.

### FrontComposer / UX Guardrails

- Approval surfaces include Approvals Queue, Time Entry Detail, and future Period Approval Detail. This story should expose metadata/state needed by those surfaces, not implement pages.
- When authority is unresolved or stale, use persistent state (`FluentMessageBar` through FrontComposer metadata) and block approval. Do not use transient toasts for audit-critical authority failures.
- Approve/reject actions should be disabled or hidden according to policy. Copy must be factual and safe: avoid protected tenant, Project, Work, Party, entry, role, token, or raw projection details.
- Status and authority states must use text, not color alone. Metadata should support projection freshness, authority freshness, approval state, and source authority display.
- Keep button/action labels verb-based and specific, for example `Approve entry`, `Reject entry`, `Approve period`, `Reject period`. Avoid invoice, payroll, rate, revenue, or celebratory language.

### Previous Story Intelligence

- Story 2.1 established `TimeEntrySubmitted`, `TimeEntrySubmissionCommandService`, submitted projection lineage, submit metadata, and fail-closed submission tests.
- Story 2.1 review fixed a duplicate-id idempotency gap and File List omissions. For Story 2.2, ensure authority-resolution tests include duplicate/contradictory source cases and ensure every changed file appears in the final File List.
- Existing adapter seams are intentionally fail-closed/unavailable by default. Positive authority tests must configure explicit fake Tenants, Project, Work, finance, policy, and self-approval source results rather than weakening defaults.
- `dotnet test` may fail in this sandbox due to VSTest socket permissions. Previous stories used direct xUnit v3 executable fallback after restore/build.

### Git Intelligence Summary

- `8a73645` implemented Story 2.1 with submit contracts, aggregate transition, submission command service, projection updates, metadata/OpenAPI updates, integration tests, and review fixes for duplicate IDs/File List completeness.
- `9c00c00` implemented AI effort metrics with contract-safe metadata, aggregate validation, projection tests, privacy fitness coverage, and no package upgrades.
- `7f8d474` implemented evidence read models, source authority, event lineage, fail-closed evidence query service, and display hydration seams.
- `a4aa9fc` implemented the existing `TimeEntry`, `TimeEntryCommandService`, `TimeEntryState`, `TimeEntryEvidenceProjection`, and aggregate/authorization/projection tests.
- `2eaf794` established project Activity Type governance, freshness checks, project-scope catalog behavior, and authorization tests.

### Latest Technical Information

- No package upgrade is required for Story 2.2. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- NuGet shows repo-pinned `xunit.v3` `3.2.2` targets .NET 8.0 and is computed compatible with `net10.0`; newer 4.x prereleases exist, but this story must not upgrade the test framework. [Source: `https://www.nuget.org/packages/xunit.v3/3.2.2`]
- NuGet shows repo-pinned `Shouldly` `4.3.0` targets .NET 8.0 and .NET Standard 2.0. [Source: `https://www.nuget.org/packages/Shouldly/4.3.0`]
- NuGet shows repo-pinned `NSubstitute` `6.0.0-rc.1` is prerelease and should remain the existing test double library for this repository. [Source: `https://www.nuget.org/packages/NSubstitute/6.0.0-rc.1`]
- Microsoft Learn documents current `dotnet test` behavior with Microsoft Testing Platform and VSTest modes. This repository already documents direct xUnit executable fallback for local socket failures; Story 2.2 should not change global test-runner mode. [Source: `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test`]

### Project Context Reference

- EventStore context: aggregates are pure command/state -> events, EventStore owns persistence and envelope metadata, projections must be idempotent, and payloads/personal data must not be logged.
- Tenants context: tenant access fails closed, tenant/member/role projections are eventually consistent, and authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, and persistent message bars are the preferred internal UI path.
- No Works project context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep resolver and contract tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 2.2 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Positive authority tests must configure explicit fake authority source results. Do not make default providers permissive.
- Negative coverage is mandatory for stale/unavailable/ambiguous authority, self-approval denied, disabled tenant, missing actor, invalid reference, cross-tenant target, same-precedence contradiction, and unsafe denial-copy prevention.

### Anti-Patterns To Prevent

- Do not implement Story 2.2 as actual Time Entry approval/rejection events, period approval, entry locking, corrections, ledger/export generation, payroll, invoice, rate, or revenue-recognition behavior.
- Do not add approve/reject role checks directly inside future command handlers when a reusable authority resolver should decide.
- Do not trust public command fields for tenant, user, role, claims, authority source, policy source, correlation, or server-controlled context.
- Do not treat projections as approval write authority; projections can inform UI state and freshness only.
- Do not copy Tenants membership, Project manager, Work owner, finance reviewer, Party display, or sibling-owned state into Timesheets contracts/events.
- Do not allow stale, unavailable, ambiguous, disabled, or contradictory authority to become a successful approval decision.
- Do not leak protected IDs, comments, command bodies, event payloads, token values, role names, personal data, sibling-owned display names, or raw upstream problem details in denial messages or logs.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

## Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/ValueObjects`, possibly `Models` for stable source/evidence DTOs, `TimesheetsMetadataCatalog.cs`, and OpenAPI artifacts if public schema changes.
- Expected server additions belong under a focused `src/Hexalith.Timesheets.Server/ApprovalAuthority` area plus `Authorization` if operation/action vocabulary changes and `Runtime/ServiceCollectionExtensions.cs` for DI.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, and `tests/Hexalith.Timesheets.ArchitectureTests`. Use IntegrationTests only if host/static metadata endpoint behavior changes.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-2-2-Enforce-Timesheet-Approval-Authority-Policy`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-2-Submission-Approval-Period-Review--Corrections`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-5-Approve-Or-Reject-Individual-Time-Entries`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-7-Submit-And-Approve-Timesheet-Periods`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-9-Resolve-Approver-Authority`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Candidate-Event-Catalog-For-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-1-submit-draft-time-entries-for-approval.md#Previous-Story-Intelligence`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/ITimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAuthorizationRequest.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsUiAction.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyEvaluator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/AuthorizationServiceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/FailClosedDefaultsTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
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

GPT-5 Codex

### Debug Log References

- 2026-06-19: Loaded AGENTS, Hexalith shared LLM instructions, Hexalith UX instructions, BMAD dev-story workflow, checklist, sprint status, story 2.2, and project context files.
- 2026-06-19: Preserved existing baseline commit `8a7364568c7921a3fef30d765978be87e8936dda` and moved story/sprint status to `in-progress`.
- 2026-06-19: `dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore --no-build` was blocked by VSTest `SocketException (13): Permission denied`; used README direct xUnit v3 executable fallback for test execution.
- 2026-06-19: Ran `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` successfully.
- 2026-06-19: Ran `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` successfully with 0 warnings and 0 errors.
- 2026-06-19: Direct xUnit fallback passed: Contracts.Tests 39/39, Server.Tests 213/213, ArchitectureTests 17/17, Projections.Tests 23/23, IntegrationTests 8 passed with 2 existing explicit skips.
- 2026-06-19: Senior Developer Review (AI) re-validated build (`-warnaserror`, 0/0) and re-ran direct xUnit fallback: Server.Tests 213/213, Contracts.Tests 39/39, IntegrationTests 11 passed + 2 existing skips, ArchitectureTests 17/17.

### Completion Notes List

- Added approval authority contract vocabulary and source attribution evidence with string-enum JSON, `Unknown = 0` sentinels, policy key/version, action, source, decision state, and freshness.
- Added a fail-closed `ApprovalAuthority` server area with resolver, source provider contract, policy options, source results, resolution results, and default unavailable providers for Project, Work, Tenant administrator, and finance authority.
- Composed approval resolution with the existing `ITimesheetsAccessGuard` in DI so tenant/resource/contributor/policy gates run before approval-source precedence, while keeping default providers deny/unavailable.
- Implemented explicit precedence, same-precedence ambiguity handling, self-approval denial by default, explicit self-approval allow-list support, and safe denial copy.
- Added FrontComposer-compatible metadata for Approvals Queue, Time Entry approval, and Period approval surfaces with blocking authority/freshness state and verb-specific approve/reject actions.
- Added contract, resolver, metadata, registration, fail-closed, architecture, and integration validation coverage. No approval/rejection domain events or bespoke UI project were added.

### File List

- `_bmad-output/implementation-artifacts/2-2-enforce-timesheet-approval-authority-policy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Models/ApprovalAuthoritySourceAttribution.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/ApprovalAuthorityResolutionRequest.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/ApprovalAuthorityResolutionResult.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/ApprovalAuthoritySourceResult.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/DefaultApprovalAuthoritySourceProviders.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/IApprovalAuthoritySourceProvider.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/ITimesheetsApprovalAuthorityResolver.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityPolicyOptions.cs`
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs`
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAuthorizationRequest.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovalAuthorityContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovalAuthorityPolicyE2ETests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj`
- `tests/Hexalith.Timesheets.Server.Tests/ApprovalAuthorityPolicyTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/FailClosedDefaultsTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 2.2 approval authority contracts, fail-closed resolver, metadata, registration, and test coverage.
- 2026-06-19: Senior Developer Review (AI) completed. Outcome: Approve. Fixed File List omissions (added integration test + csproj). Recorded 2 low-severity forward-looking follow-ups. Status set to done.

## Senior Developer Review (AI)

**Reviewer:** Jerome (adversarial automated review)
**Date:** 2026-06-19
**Outcome:** Approve — no Critical/High issues; 1 Medium fixed, 2 Low recorded as follow-ups.

### Validation performed

- Verified all 6 Acceptance Criteria against implementation:
  - AC1 — `ApprovalAuthorityAction` covers entry/period approval, entry/period rejection, correction, and export eligibility; resolver applies precedence; `ApprovalAuthoritySourceAttribution` records source/policy/action/state/freshness. IMPLEMENTED.
  - AC2 — Self-approval denied by default for entry/period approval; allowed only via explicit `SelfApprovalAllowedActions`; denial copy is safe and auditable. IMPLEMENTED.
  - AC3 — `Stale`/`Unavailable`/`Ambiguous`/`CrossTenantTarget`/`InvalidReference`/`DisabledTenant` decision states fail closed; Approvals Queue + approval command metadata expose `blockingState`/`persistentMessageBarState`/`authorityFreshness`. IMPLEMENTED.
  - AC4 — `PolicyKey`/`PolicyVersion` carried on options and stamped into attribution at decision time. IMPLEMENTED.
  - AC5 — Approval surface metadata provides verb-specific actions and safe blocking copy ("Authority cannot be resolved." / "Access denied for this action.") with no protected identifiers. IMPLEMENTED (metadata-only, per story scope).
  - AC6 — `ApprovalAuthorityPolicyTests` covers all four configured sources, precedence, same-precedence ambiguity, self-approval denied/allowed, missing actor, disabled tenant, stale projection, unavailable sibling, invalid reference, cross-tenant target, base-guard-first ordering, and safe denial copy. IMPLEMENTED.
- Audited every `[x]` task — all genuinely done; no false completions.
- Build: `dotnet build Hexalith.Timesheets.slnx -warnaserror` → 0 warnings, 0 errors.
- Tests (direct xUnit v3 executable, VSTest sockets unavailable in sandbox): Server.Tests 213/213, Contracts.Tests 39/39, IntegrationTests 11 passed + 2 pre-existing infrastructure skips, ArchitectureTests 17/17.
- OpenAPI change confirmed additive only (4 new schemas; no existing schema removed/altered); `additionalProperties: false`; no caller-authority fields. Added `Microsoft.Extensions.DependencyInjection` csproj reference is version-less (centrally pinned in `Directory.Packages.props`) — compliant with the no-package-version anti-pattern.

### Findings

- **[Medium][Fixed] File List omission.** `tests/Hexalith.Timesheets.IntegrationTests/ApprovalAuthorityPolicyE2ETests.cs` (new) and `tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj` (modified) were changed in git but missing from the Dev Agent Record File List — the same class of omission flagged in the Story 2.1 review. File List corrected.
- **[Low][Follow-up] Precedence short-circuit on `Unavailable`.** See Review Follow-ups (AI). Intentional and fail-closed-safe for this story; not changed to avoid weakening the fail-closed posture and contradicting deliberate tests.
- **[Low][Follow-up] Self-approval contributor source.** See Review Follow-ups (AI). Latent footgun only under inconsistent caller population; current callers are consistent.
