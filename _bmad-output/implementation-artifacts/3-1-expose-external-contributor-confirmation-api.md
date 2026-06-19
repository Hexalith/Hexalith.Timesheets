---
baseline_commit: 5200265db2a47ec7372cb8598c26da1341f4e5c6
---

# Story 3.1: Expose External Contributor Confirmation API

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an external contributor integration,
I want to submit or confirm Time Entries through an API-only surface,
so that external effort can enter the same approval workflow without granting full internal access.

## Acceptance Criteria

1. Given an external contributor API caller has tenant-scoped authorization and a valid Contributor Party reference, when they submit or confirm a Time Entry with required capture fields, then the entry is persisted through the same EventStore-backed Time Entry flow as internal entries, and it enters the configured Draft or Submitted workflow state according to policy.
2. Given an external API request lacks tenant authority, uses an invalid Party reference, or references unverifiable Project/Work data for a trust-bearing write, when the command is handled, then it fails closed, and no entry, projection update, or protected target/contributor detail is disclosed.
3. Given an external API-created entry enters review, when approvers inspect it, then the entry shows external contributor category, source metadata, Party ID, target reference, Activity Type, billable flag, and approval state, and it follows the same approval, rejection, correction, locking, and audit rules as internal entries.
4. Given an external contributor confirms time through the API, when the confirmation is accepted, then confirmation is recorded as contributor evidence, not approval, and approval still requires the configured Timesheets approval workflow.
5. Given external API requests are retried or duplicated, when idempotency context matches a prior accepted command, then the system avoids duplicate Time Entries or duplicate submitted evidence, and projection replay remains idempotent.
6. Given telemetry is emitted for external submission, when requests succeed or fail, then logs contain correlation-safe outcome metadata only, and comments, command bodies, personal data, token values, target names, and protected identifiers are not logged.

## Tasks / Subtasks

- [x] Add external contribution command contracts and evidence vocabulary (AC: 1, 3, 4, 5, 6)
  - [x] Add external API command contracts under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries` or a focused `Commands/ExternalContributions` folder if that keeps the capability clearer. Prefer names that separate contributor evidence from approval, for example `SubmitExternalTimeEntry` and `ConfirmExternalTimeEntry`; do not use approval wording for confirmation.
  - [x] Reuse existing value objects where possible: `TimeEntryId`, `TimeEntryTargetReference`, `PartyReference`, `ActivityTypeId`, `TimeEntryComment`, `BillableState`, `ContributorCategory.ExternalContributor`, `TimeEntrySubmissionId`, and `TimeEntrySubmissionScope.SelectedEntries`.
  - [x] Add only the minimum new model/value object needed for source metadata and idempotency, such as an external request id/source reference. It must not store personal data, raw provider payloads, bearer tokens, decoded magic-link material, comments outside existing comment policy, or sibling-owned display names.
  - [x] If a new event is needed for confirmation evidence, add it under `src/Hexalith.Timesheets.Contracts/Events/TimeEntries` with past-tense Timesheets naming. It must make clear that confirmation is contributor evidence and not approval.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively for the new API commands/read-model fields. Do not expose raw EventStore envelopes or server-controlled authority fields.
- [x] Implement external command orchestration by reusing the existing Time Entry flow (AC: 1, 2, 4, 5)
  - [x] Add a service in `src/Hexalith.Timesheets.Server/TimeEntries` or `src/Hexalith.Timesheets.Server/MagicLinks` only if scoped confirmation logic needs its own boundary; Story 3.1 is API-only and must not implement magic-link issuance from Story 3.2.
  - [x] Reuse `TimeEntryCommandService.RecordAsync` for capture validation and EventStore-backed `TimeEntryRecorded` behavior rather than copying aggregate validation into the new API path.
  - [x] When policy selects immediate submission, reuse `TimeEntrySubmissionCommandService.SubmitAsync` after a successful record transition; do not create a parallel submitted state or mutate a projection directly.
  - [x] Force or validate `ContributorCategory.ExternalContributor` for these commands. External submissions must not be accepted as `Employee` or `AutomatedAgent`, and AI metrics must remain unavailable/null for external-human contribution unless a later story explicitly adds agent evidence to this path.
  - [x] Preserve idempotency using existing aggregate behavior where the same `TimeEntryId`/submission id repeats, plus any new external request id. A duplicate retry must return an accepted/no-op style outcome without emitting duplicate Time Entries or duplicate submitted evidence.
  - [x] Keep confirmation separate from approval. Accepted confirmation may create draft/submitted contributor evidence, but `TimeEntryApproved`, `TimesheetPeriodApproved`, lock state, and Approved-Time Ledger eligibility still require the existing approval workflow.
- [x] Expose narrow host API endpoints without a generic external portal (AC: 1, 2, 4, 6)
  - [x] Add endpoint mapping under `src/Hexalith.Timesheets/Endpoints` using lowercase Timesheets-owned routes, for example `/api/timesheets/external-contributions` and `/api/timesheets/external-contributions/{timeEntryId}/confirm`; exact names may follow local endpoint conventions if one is introduced in this story.
  - [x] Build `TimesheetsRequestContext` only from trusted server-side authentication/tenant/correlation sources. The request body must not accept tenant authority, actor authority, raw claims, correlation ids, timestamps that should be server-controlled, EventStore message ids, approval authority, or policy decisions.
  - [x] Return typed command outcomes or ProblemDetails-style transport errors without leaking protected tenant, Project, Work, Party, Time Entry, Activity Type, comment, or target display details on denial.
  - [x] Register the endpoint/service in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` or host composition as appropriate, while keeping fail-closed defaults until real adapters are configured.
  - [x] Do not add a full external-party portal, internal shell navigation, magic-link token inspection endpoint, or broad browse/query API in this story.
- [x] Project and disclose external contributor evidence safely (AC: 3, 4, 5)
  - [x] Extend `TimeEntryEvidenceReadModel`, `TimeEntryEvidenceProjection`, and related models only as needed to expose external contributor category and safe source/confirmation metadata to authorized approvers.
  - [x] Keep `PartyReference`, target reference, Activity Type ID, billable flag, approval state, projection freshness, event lineage, correction state, and lock evidence aligned with existing `TimeEntryEvidenceReadModel` semantics.
  - [x] Do not hydrate or persist Party personal data, target names, Project/Work-owned lifecycle state, or raw external provider data in Timesheets events.
  - [x] Ensure duplicate event delivery and replay keep external source/confirmation metadata idempotent and ordered by the existing projection sequence/message-id rules.
- [x] Update metadata and client surface for API consumers without creating UI scope (AC: 1, 3, 4)
  - [x] Extend `TimesheetsMetadataCatalog` with external contribution command/evidence descriptors so FrontComposer/API consumers can discover the capability later.
  - [x] Extend `src/Hexalith.Timesheets.Client` only if the current client abstraction needs methods/options for external contribution API calls; keep contracts infrastructure-free and do not add package versions.
  - [x] Use factual labels such as `Submit external time`, `Confirm time`, `External contributor`, and `Confirmation recorded`. Do not use copy that implies approval, invoice, payroll, rates, or broader tenant access.
- [x] Add focused tests across contracts, server, projections, endpoint integration, metadata, and privacy (AC: 1-6)
  - [x] Add contract tests for JSON round trips, enum/value vocabulary, OpenAPI schemas, idempotency/source metadata, and absence of server-controlled authority/envelope/personal-data fields.
  - [x] Add server tests proving tenant gate, Project/Work reference validation, Contributor Party validation, policy evaluation, Activity Type freshness, and domain dispatch order match the existing internal capture/submission path.
  - [x] Add tests that invalid tenant, invalid Party, stale/unavailable Project/Work, inactive Activity Type, and policy denial all fail closed before aggregate dispatch or projection disclosure.
  - [x] Add projection tests for external contributor category/source metadata, duplicate delivery, replay, and preservation of approval/rejection/correction/lock semantics.
  - [x] Add in-process integration tests showing external API record-only and record-plus-submit policy paths produce the same `TimeEntryRecorded`/`TimeEntrySubmitted` evidence and remain reviewable through authorized evidence reads.
  - [x] Add diagnostics/privacy tests or extend `DiagnosticsPrivacyTests` to assert no comments, command bodies, event payloads, token values, personal data, target names, protected identifiers, raw claims, upstream details, or EventStore envelopes leak through logs, denial copy, metadata, or endpoint responses.
- [x] Verify affected build and test lanes (AC: 1-6)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests when endpoint/static metadata behavior changes.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 3 and Story 3.1.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, authentication/security, API communication patterns, external submission API, magic-link communication boundaries, validation patterns, project structure, and testing expectations.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and addendum, especially FR12, FR13, FR14 adjacency, NFR1, NFR3, NFR6, NFR8, NFR9, NFR12, and the external contributor journey.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially Magic-Link Confirmation boundaries, no-disclosure states, factual copy, evidence/audit semantics, and the rule that v1 has no full external portal.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous implementation intelligence from `_bmad-output/implementation-artifacts/2-8-approve-or-reject-timesheet-periods.md` and `_bmad-output/implementation-artifacts/epic-2-retro-2026-06-19.md`.
- Read current Timesheets record/submission contracts, Time Entry aggregate/state/services, authorization/reference validation, projection/read model, host `Program.cs`, client abstraction, metadata catalog, package pins, README test instructions, and representative tests listed in References.

### Epic And Story Context

- Epic 3 lets external contributors submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.
- Story 3.1 owns the API-only external contributor submission/confirmation surface. Story 3.2 owns issuing scoped magic-link capabilities; Story 3.3 and Story 3.4 own magic-link confirm/adjust flows; Story 3.5 owns invalid-link no-disclosure equivalence.
- FR12 requires every Time Entry to be attributed to a Party and to distinguish internal, external, and AI-agent contribution without storing Party personal data.
- FR13 requires external API submission to use tenant-scoped authorization, valid Party references, and the same approval, correction, Target Reference validation, tenant isolation, and audit workflow as internal entries.
- FR14 is adjacent but not implemented here: magic links are single-use, scoped, expiring capabilities with no-disclosure invalid states. Story 3.1 should not create token issuance/storage unless the implementation needs a harmless placeholder type for later stories.
- Confirmation is contributor evidence, not approval. Do not emit approval events, lock approved entries, or make entries ledger-eligible from confirmation alone.

### Current Code State To Extend

- `RecordTimeEntry` and `TimeEntryRecorded` already carry target, contributor, activity type, service date, duration, billable state, contributor category, optional AI metrics, and optional comment.
- `ContributorCategory` already has `ExternalContributor`. Use it for external API-created entries instead of adding a parallel source enum unless source metadata needs a separate safe field.
- `TimeEntryCommandService.RecordAsync` already performs fail-closed access checks through `ITimesheetsAccessGuard`, validates Activity Type catalog freshness/scope, and dispatches `TimeEntry.Handle`.
- `TimeEntrySubmissionCommandService.SubmitAsync` already deduplicates repeated entry ids in a submission command, validates access/activity type freshness, and dispatches `TimeEntry.Handle(SubmitTimeEntriesForApproval, ...)`.
- `TimeEntry.Handle(RecordTimeEntry, ...)` rejects duplicate Time Entry IDs, invalid target/contributor/activity/billable/category fields, invalid AI metrics, invalid comments, and locked states. Reuse this logic.
- `TimesheetsAccessGuard` validates tenant access first, then Project/Work references, then Contributor Party references, then policy. Preserve this order for external API paths.
- Default reference validators and tenant access validators are fail-closed. Positive external API tests need configured fakes; production behavior must not silently become allow-all.
- `TimeEntryEvidenceProjection` deduplicates by message id, orders by sequence number, and projects recorded/submitted/approved/rejected/corrected evidence into `TimeEntryEvidenceReadModel`.
- `TimeEntryEvidenceQueryService` performs tenant-level read authorization, reads projection evidence, authorizes evidence-specific Project/Work/Contributor disclosure, and hydrates display labels only after authorization.
- `src/Hexalith.Timesheets/Program.cs` currently exposes only service defaults, the fail-closed kernel, default endpoints, and `/metadata/timesheets`. Story 3.1 is likely the first public Timesheets command endpoint slice.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or direct projection mutation for external submission/confirmation state. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- External-party API submission uses the same command/query boundary as internal flows. It cannot bypass tenant gates, reference validation, approval, correction, locking, or audit rules. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Public/internal command contracts live in `Contracts`; host/API endpoints call server application services; server dispatches authoritative changes through EventStore. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Public command/query contracts must hide EventStore envelope mechanics and must not accept server-controlled tenant/user/correlation/authorization context from request bodies. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Magic-Link Confirmation uses a narrow web/API surface for a single scoped capability and must not expose generic Timesheets browsing APIs or internal navigation. [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-link-communication`]
- Query/read APIs return typed DTOs/read models with projection freshness/trust metadata, not raw EventStore envelopes. [Source: `_bmad-output/planning-artifacts/architecture.md#API-Response-Formats`]
- Comments, token values, personal data, command payloads, event payloads, target names, and protected identifiers must not be logged or included in diagnostics. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]

### External API Semantics

- Treat external submission as a narrow command surface over existing capture/submission behavior, not a separate external ledger.
- External caller authorization is still tenant-scoped authorization. JWT tenant claims or caller-supplied tenant/user fields are evidence only and do not replace `ITimesheetsAccessGuard`.
- External API-created entries must have a valid Contributor Party reference and target exactly one Project or Work reference. Timesheets stores references only.
- Trust-bearing writes fail closed when tenant, Party, Project/Work, Activity Type catalog, or policy authority is stale, unavailable, ambiguous, invalid, disabled, cross-tenant, or unresolved.
- Policy may choose Draft or Submitted initial workflow state. If no explicit policy vocabulary exists yet, implement a conservative default and document it in tests; do not silently submit when policy is unresolved.
- Confirmation means the external contributor supplied or accepted the time evidence. Approval remains a separate approver workflow using existing `ApproveTimeEntry`/period approval behavior.
- Retried requests must not create duplicates. Existing duplicate Time Entry ID and submission id behavior can be part of idempotency, but external request/source id should be explicit if required for client retries.

### Previous Story Intelligence

- Epic 2 completed internal submission, approval, rejection, rejected-entry correction, approved-entry correction, period submission, and period approval/rejection.
- Story 2.8 established the pattern of reusing existing entry approval paths instead of duplicating approval policy in period services. Story 3.1 should similarly reuse `TimeEntryCommandService` and `TimeEntrySubmissionCommandService`.
- Epic 2 review synthesis says reviews repeatedly found missing branch tests for stale, unavailable, duplicate, cross-tenant, and self-approval paths. Story 3.1 should include these failure branches from the start.
- Epic 2 retrospective explicitly warns: external API paths must reuse existing approval, correction, locking, and safe-denial services instead of creating bypasses.
- Epic 2 also warns that required-comment and partial-submission policy vocabulary is not fully modeled. Do not assume permissive policy for external submission; make policy behavior explicit in story implementation and tests.
- In-process integration tests are useful but do not replace runtime EventStore/Dapr/Aspire evidence. Keep infrastructure-dependent tests explicitly skipped until real runtime fixtures exist.

### Git Intelligence Summary

- `5200265 feat(story-3.1): update orchestration state for story 3.1 progression` updated story automator/orchestration state only; do not treat it as an implementation baseline.
- `68af2da docs(epic-2): add retrospective updates` added Epic 2 retrospective guidance now relevant to external API safety.
- `ce2d07d feat(story-2.8): Approve or Reject Timesheet Periods` added period approval/rejection contracts, services, projections, metadata, OpenAPI, and tests. It is the latest implementation pattern for cross-project Timesheets stories.
- `e4c79eb feat(story-2.7): Submit Timesheet Periods`, `0d66912 feat(story-2.6): Add Approved Entry Corrections`, and `ce2d07d` show the expected implementation shape: contracts, server service/aggregate changes, projections/read models, metadata/OpenAPI, focused tests, and exact story File List.

### Latest Technical Information

- No package upgrade or new external dependency is required for Story 3.1. Use repository pins in `Directory.Packages.props` and keep versions centralized.
- Current repo pins include .NET 10 package lines, Aspire `13.4.5`, `Microsoft.Extensions.*` `10.0.9`/`10.7.0`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.
- Do not add inline package versions, `.sln` files, Dockerfiles, new UI framework packages, direct infrastructure packages, or dependency upgrades for this story.

### Project Context Reference

- EventStore context: aggregates are pure command/state to events; EventStore owns persistence and envelope metadata; projections must be idempotent; payloads, comments, and personal data must not be logged.
- Tenants context: tenant access fails closed; tenant/member/role projections are eventually consistent; authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred internal UI path.
- No Works project-context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 3.1 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- API/endpoint tests may be in-process integration tests if they can run without real external infrastructure; keep runtime infrastructure tests explicitly skipped with clear skip reasons.
- Every claimed fail-closed path needs executable test proof: missing tenant, invalid Party, invalid/stale/unavailable Project or Work, inactive/missing Activity Type, unresolved policy, duplicate retry, and privacy-safe denial copy.

### Anti-Patterns To Prevent

- Do not implement external submissions as direct SQL/Redis/Dapr state, local files, mutable projection writes, or a separate external table.
- Do not bypass `ITimesheetsAccessGuard`, reference validators, Activity Type catalog freshness, `TimeEntry.Handle`, `TimeEntryCommandService`, or `TimeEntrySubmissionCommandService`.
- Do not accept tenant/user/actor/correlation/authority/source-of-truth fields from the request body as trusted authority.
- Do not treat confirmation as approval, lock approved evidence, or make entries ledger/export eligible from external confirmation alone.
- Do not add magic-link issuance, token hash storage, revocation state, invalid-link no-disclosure matrix, or adjustment UI beyond what is required for API-only confirmation. Those belong to later Epic 3 stories.
- Do not expose generic browse/query APIs, a token inspection endpoint, a full external portal, internal shell navigation, or a parallel UI framework.
- Do not store or log Party personal data, Project/Work display names or lifecycle state, comments outside existing policy, command bodies, event payloads, bearer tokens, decoded capability material, raw claims, upstream problem details, EventStore envelopes, or protected identifiers.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries` or `Commands/ExternalContributions`, `Events/TimeEntries` if confirmation evidence needs a new event, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/TimeEntries` for command orchestration, with a `MagicLinks` folder reserved only for capability-specific boundaries needed by later stories.
- Expected host additions belong under `src/Hexalith.Timesheets/Endpoints` or an equivalent local endpoint mapping pattern introduced by this story.
- Expected projection additions belong under `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests` for in-process endpoint/workflow coverage.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-3-1-Expose-External-Contributor-Confirmation-API`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-3-External-Contributor-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-12-Attribute-every-Time-Entry-to-a-Party`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-13-Support-external-party-API-submission`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-14-Support-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-3-Simon-confirms-supplier-time-through-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/2-8-approve-or-reject-timesheet-periods.md#Previous-Story-Intelligence`]
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-06-19.md#Epic-3-Preview`]
- [Source: `Directory.Packages.props`]
- [Source: `README.md#Build-and-Test`]
- [Source: `src/Hexalith.Timesheets/Program.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/SubmitTimeEntriesForApproval.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntrySubmitted.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsRequestContext.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IContributorPartyValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IProjectReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/References/IWorkReferenceValidator.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntrySubmissionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryEvidenceQueryService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`]
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

- `dotnet test` via VSTest was attempted for Contracts/Projections/Server and blocked by local socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`; switched to README direct xUnit v3 executable fallback.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- Direct xUnit fallback passed: Contracts.Tests 59/59, Server.Tests 289/289, Projections.Tests 40/40, ArchitectureTests 19/19, IntegrationTests 30 passed / 2 skipped infrastructure-performance reservations.
- 2026-06-19 review re-run (direct xUnit executables): Contracts.Tests 59/59, Server.Tests 293/293, Projections.Tests 40/40, ArchitectureTests 19/19, IntegrationTests 31 passed / 2 skipped. Build `dotnet build Hexalith.Timesheets.slnx -warnaserror` clean (0 warnings, 0 errors).

### Completion Notes List

- Added API-only external contribution command contracts (`SubmitExternalTimeEntry`, `ConfirmExternalTimeEntry`) plus safe source/idempotency metadata.
- Added contributor confirmation as evidence (`TimeEntryContributorConfirmed`) and kept it separate from approval, lock, and ledger eligibility.
- Implemented external contribution orchestration in the server layer by reusing `TimeEntryCommandService.RecordAsync` and `TimeEntrySubmissionCommandService.SubmitAsync`; default external policy records Draft and supports explicit Submitted policy.
- Added narrow host routes for external contribution submit/confirm with trusted server-derived request context and opaque ProblemDetails denial copy.
- Extended evidence read model/projection, metadata catalog, client interface, and OpenAPI artifact for source and confirmation evidence.
- Added contract, server, projection, integration, metadata, and privacy/boundary coverage for the new external contribution surface.

### File List

- `_bmad-output/implementation-artifacts/3-1-expose-external-contributor-confirmation-api.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Client/ITimesheetsClient.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions/ConfirmExternalTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions/SubmitExternalTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/RecordTimeEntry.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryContributorConfirmed.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryRecorded.cs`
- `src/Hexalith.Timesheets.Contracts/Models/ExternalContributionSource.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryContributorConfirmationEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsServerRequestContext.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandResult.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandService.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionPolicyOptions.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`
- `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs`
- `src/Hexalith.Timesheets/Program.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ExternalContributionContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionWorkflowE2ETests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/ExternalContributionCommandServiceTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs`

### Change Log

- 2026-06-19: Implemented Story 3.1 external contributor confirmation API contracts, orchestration, endpoint mapping, projection evidence, metadata/client surface, OpenAPI schemas, and focused tests.
- 2026-06-19: Adversarial code review (story-automator). Auto-fixed File List omission (`ExternalContributionWorkflowE2ETests.cs`), simplified an unreachable state-fabricating fallback in `ExternalContributionCommandService.ApplyRecordedEvent`, and refreshed verified build/test evidence. Status → done.

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot
**Date:** 2026-06-19
**Outcome:** Approved (auto-fix applied)

### Verification performed

- Read every file in the File List plus the reused capture/submission/authorization infrastructure.
- Cross-referenced git reality (`git status --porcelain`) against the story File List.
- Built `Hexalith.Timesheets.slnx` with `-warnaserror`: 0 warnings, 0 errors.
- Ran affected suites via the README direct xUnit v3 executable fallback: Contracts.Tests 59/59, Server.Tests 293/293, Projections.Tests 40/40, ArchitectureTests 19/19, IntegrationTests 31 passed / 2 skipped.

### Acceptance Criteria validation

- AC1 — IMPLEMENTED. `ExternalContributionCommandService.SubmitAsync` reuses `TimeEntryCommandService.RecordAsync` (EventStore-backed `TimeEntryRecorded`) and, under Submitted policy, `TimeEntrySubmissionCommandService.SubmitAsync`. Default policy records Draft.
- AC2 — IMPLEMENTED. Tenant → reference → policy fail-closed order preserved through `TimesheetsAccessGuard`; denial copy is opaque ProblemDetails. Branch tests cover missing tenant, invalid Party, stale/unavailable Project/Work, inactive Activity Type, and policy denial before dispatch.
- AC3 — IMPLEMENTED. Read model/projection expose external contributor category, source metadata, Party ID, target, Activity Type, billable flag, and approval state; same approval/rejection/correction/lock semantics retained.
- AC4 — IMPLEMENTED. Confirmation emits only `TimeEntryContributorConfirmed`; it never mutates approval state, lock, or ledger eligibility. Verified by service and projection tests.
- AC5 — IMPLEMENTED. Idempotency via existing duplicate-id behavior plus external request id; same-source retries return no-op without duplicate record/confirmation events; projection dedupes by message id and orders by sequence number.
- AC6 — SATISFIED. No telemetry is emitted in this slice, and contract/E2E privacy assertions confirm commands, events, and read models carry no tokens, command bodies, approver authority, or personal data; denial copy is opaque.

### Findings (all auto-fixed or accepted)

- MEDIUM (documentation) — `ExternalContributionWorkflowE2ETests.cs` was implemented but missing from the File List. FIXED: added to File List.
- LOW (maintainability) — `ApplyRecordedEvent` carried an unreachable fallback fabricating a `TimeEntryRecorded` with a hardcoded `ActivityTypeScope.Tenant`. FIXED: simplified to reuse the dispatched event (the only reachable case).
- LOW (transparency) — Debug Log References cited stale counts (Server 289, Integration 30 passed). FIXED: appended verified re-run counts.
- OBSERVATION (no change) — Host endpoints intentionally pass `null` aggregate state and an unavailable Activity Type catalog, keeping the public surface fail-closed until EventStore/projection adapters are wired (consistent with the rest of the module skeleton). Functional happy-path coverage lives at the service/projection layer. Revisit when adapters are introduced (Epic 3 follow-on).

### CRITICAL/HIGH issues

None. No task marked `[x]` was found incomplete; no AC was missing or only partially implemented.
