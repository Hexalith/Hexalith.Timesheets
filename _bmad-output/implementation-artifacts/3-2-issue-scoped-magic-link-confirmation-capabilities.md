---
baseline_commit: 1876378ebdefb3b6c8777991203318ad102058dc
---

# Story 3.2: Issue Scoped Magic-Link Confirmation Capabilities

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized internal user,
I want to issue a scoped Magic-Link Confirmation capability for one external contribution,
so that an external contributor can confirm or adjust proposed time without broader tenant access.

## Acceptance Criteria

1. Given an authorized user has permission to request external confirmation for a specific contribution, when they issue a magic link, then Timesheets creates a server-generated opaque capability scoped to tenant, Contributor Party, proposed entry or entry, allowed action, expiry, and single-use state, and only a token hash and capability metadata are stored.
2. Given a magic link is issued, when audit evidence is persisted, then issue metadata is recorded through EventStore-backed events, and token values or decoded capability material are not logged, projected, exported, or stored in comments.
3. Given the issuer lacks tenant/resource authority or the proposed confirmation references invalid Project, Work, Party, or Activity Type data, when link issuance is attempted, then the command fails closed, and no usable capability is produced.
4. Given a magic-link capability exists, when it is revoked or expires by policy, then the state change is auditable, and future use of the link follows the same no-disclosure invalid-state response.
5. Given operators view issued confirmation requests, when the internal UI displays link state, then it uses FrontComposer/Fluent UI V5 status badges with text, expiry state, audit metadata, and no raw token values.

## Tasks / Subtasks

- [x] Add magic-link capability contracts and value vocabulary (AC: 1, 2, 4, 5)
  - [x] Add command contracts under `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks`, for example `IssueMagicLinkConfirmationCapability`, `RevokeMagicLinkConfirmationCapability`, and an explicit expire command or service operation if expiry is modeled as a policy-triggered command.
  - [x] Add value objects/models under `src/Hexalith.Timesheets.Contracts/ValueObjects` or `Models`: capability ID, allowed action (`Confirm`, `Adjust`, or both if policy allows), capability state (`Issued`, `Revoked`, `Expired`, later `Used`), token hash, expiry UTC instant, target scope, and safe audit metadata.
  - [x] Scope capability metadata to tenant, Contributor Party, target Project or Work reference, Activity Type, proposed or existing Time Entry ID, allowed action, expiry, single-use state, issuer, issued-at UTC, and correlation-safe source metadata.
  - [x] Do not add raw token, decoded token payload, bearer token, login/session data, personal data, target display names, comments, EventStore envelope fields, or caller-controlled authority fields to persisted events or read models.
  - [x] If the issuer must receive a usable URL/token once, return it only from the immediate server/transport result for the successful issuance call; do not persist, project, log, export, or expose it through metadata/read APIs.
- [x] Implement EventStore-backed magic-link aggregate/service logic (AC: 1-4)
  - [x] Add a `src/Hexalith.Timesheets.Server/MagicLinks` capability area with pure aggregate/state handling for issue, revoke, and expire transitions. Keep authoritative state in emitted events, not projections, SQL, Redis, Dapr state, local files, or mutable in-memory stores.
  - [x] Generate opaque capability tokens with cryptographically strong random bytes. Store only a deterministic server-side hash plus metadata. Do not use ASP.NET Core Data Protection as the only source of revocation or single-use truth.
  - [x] Enforce expiry with an injected time source where possible for deterministic tests; if a local `TimeProvider` dependency is introduced, keep package versions centralized and avoid inline versions.
  - [x] Reject duplicate issuance IDs, missing/invalid scope, invalid allowed action, non-UTC expiry, expired-at-issue tokens, missing token hash, and revoke/expire transitions for unknown or terminal capabilities.
  - [x] Record revocation and expiry as auditable events. `Used` can remain a later story concern unless needed for single-use state representation, but the state model must leave a clear additive path for Story 3.3.
- [x] Reuse fail-closed authority, reference, and policy checks before issuance (AC: 1, 3)
  - [x] Extend `TimesheetsOperation` only if a distinct `MagicLinkIssuance`/`MagicLinkDisclosure` operation is clearer than the existing `Confirmation` operation; update tests and policy evaluation accordingly.
  - [x] Use `ITimesheetsAccessGuard` before aggregate dispatch. Tenant authority must run first, then Project or Work validation, then Contributor Party validation, then policy evaluation.
  - [x] Validate Activity Type catalog freshness/scope for proposed confirmation details before issuing a usable capability. Stale, rebuilding, unavailable, missing, inactive, cross-tenant, ambiguous, or policy-denied authority fails closed.
  - [x] Build `TimesheetsRequestContext` from trusted server-side sources only. Request bodies must not accept tenant authority, actor authority, raw claims, correlation IDs, EventStore metadata, issuer role, or policy decisions as trusted input.
- [x] Add narrow host endpoints and client surface without a token-inspection API (AC: 1-4)
  - [x] Add endpoint mapping under `src/Hexalith.Timesheets/Endpoints/MagicLinks`, for example `/api/timesheets/magic-links/confirmation-capabilities` for issuance and scoped management routes for revoke/expire if exposed.
  - [x] Register the endpoint in `src/Hexalith.Timesheets/Program.cs` and service registrations in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
  - [x] Return `Accepted` or ProblemDetails-style transport results with opaque denial copy. Denials must not reveal tenant, Party, Project, Work, Time Entry, Activity Type, expiry reason, token existence, or policy details.
  - [x] Do not add a generic token inspection endpoint, broad browse/query API, full external portal, internal shell navigation, confirmation-use endpoint, or adjustment UI in this story.
  - [x] Extend `src/Hexalith.Timesheets.Client/ITimesheetsClient.cs` only if the existing client abstraction needs issuance/revoke calls; keep `Contracts` infrastructure-free.
- [x] Project safe operator-visible capability state and metadata (AC: 2, 4, 5)
  - [x] Add projection/read-model support under `src/Hexalith.Timesheets.Projections/MagicLinks` or a focused server read model if projections are not yet wired. It must be replay-safe, duplicate-tolerant, and non-authoritative.
  - [x] Surface capability state, allowed action, expiry state, issue/revoke/expire audit metadata, scope IDs, projection freshness, and text status badge vocabulary.
  - [x] Exclude raw token values, decoded capability material, token hash if not strictly needed for internal audit, comments, Party personal data, target names, sibling-owned state, EventStore envelopes, and command bodies from projections and metadata.
  - [x] Extend `TimesheetsMetadataCatalog` with a FrontComposer-compatible descriptor for issued confirmation requests, including text state badges for magic-link status and expiry state. Do not create a Timesheets UI project in this story.
- [x] Update OpenAPI/contract artifacts additively (AC: 1, 2, 4, 5)
  - [x] Extend `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` with magic-link issuance, state, and audit schemas. Keep all schemas closed where local OpenAPI patterns require it.
  - [x] Keep public artifacts free of raw token fields except for a narrowly documented one-time issuance response if the endpoint returns link material. If such a response exists, tests must prove it is not persisted, projected, logged, or exposed by metadata/read models.
  - [x] Preserve the existing evidence policy category `MagicLinkConfirmationAuditMetadata` and do not weaken `timesheets-evidence-policy.v1.md` privacy guidance.
- [x] Add focused tests for contracts, server, projections, endpoints, metadata, and privacy (AC: 1-5)
  - [x] Add contract tests for JSON round trips, enum unknown sentinels where applicable, additive OpenAPI schemas, authority-field absence, raw-token absence in persisted/audit/read schemas, and safe one-time issuance response shape if present.
  - [x] Add server tests proving successful issue stores hash+metadata only, issue/revoke/expire events are emitted, duplicate/terminal transitions are idempotent or rejected as designed, and expiry uses UTC/time-provider semantics.
  - [x] Add fail-closed tests for missing tenant, invalid Party, invalid/stale/unavailable Project or Work, inactive/missing Activity Type, unconfigured policy, cross-tenant target, non-UTC expiry, expired-at-issue request, and malformed scope.
  - [x] Add endpoint/integration tests similar to `ExternalContributionEndpointTests` proving routes are narrow, request bodies cannot supply authority fields, denial copy is opaque, no token-inspection route exists, and raw token material is absent from logs/metadata/read responses.
  - [x] Add projection tests for issued/revoked/expired state, duplicate delivery, replay, out-of-order-safe behavior where applicable, projection freshness, and text status badge metadata.
  - [x] Extend diagnostics/privacy and architecture tests so new magic-link code does not log raw tokens, token hashes where disallowed, decoded material, command bodies, comments, personal data, target names, EventStore envelopes, or protected identifiers.
- [x] Verify affected build and test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests when endpoint/static metadata behavior changes.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 3 and Story 3.2.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially authentication/security, magic-link model, API naming, project structure, validation patterns, API boundaries, and testing expectations.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and addendum, especially FR14, UJ-3, NFR6, NFR7, NFR8, NFR12, and the resolved v1 policy that secondary identity verification for high-value/billable entries is deferred post-v1.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially magic-link no-disclosure copy, status badges with text, minimal external confirmation surface, and FrontComposer/Fluent UI rules.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, and `Hexalith.FrontComposer` project-context files. No `Hexalith.Works/_bmad-output/project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/3-1-expose-external-contributor-confirmation-api.md`.
- Read current Timesheets external contribution contracts, endpoint mapping, access guard, service registration, metadata catalog, privacy tests, package pins, README test instructions, and representative tests listed in References.

### Epic And Story Context

- Epic 3 lets external contributors submit or confirm scoped time without becoming full internal users, using API-only submission and no-disclosure Magic-Link Confirmation.
- Story 3.1 owns API-only external contribution submission/confirmation and is complete. Story 3.2 owns issuing scoped magic-link capabilities. Story 3.3 owns token validation and confirmation use. Story 3.4 owns adjustment through a magic link. Story 3.5 owns invalid-link no-disclosure equivalence across all invalid states.
- FR14 requires single-use, scoped, expiring magic links that require no full account login. Invalid, expired, already-used, revoked, unauthorized, malformed, or unknown links must reveal no tenant, Project, Work, Party, Time Entry, duration, Activity Type, comment, or approval detail.
- The v1 policy is explicit: single-use scoped expiring links are the baseline; secondary identity verification for high-value or billable entries is deferred post-v1 and must not be silently implemented or implied.
- Confirmation is contributor evidence, not approval. Issuing a link must not approve, submit, correct, lock, export, or make a Time Entry ledger-eligible.

### Current Code State To Extend

- There is no dedicated `MagicLinks` source folder yet. Story 3.2 should create the capability area instead of placing token issuance inside `ExternalContributionCommandService`.
- Story 3.1 added external contribution contracts under `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions`, source metadata in `ExternalContributionSource`, confirmation evidence via `TimeEntryContributorConfirmed`, endpoint mapping in `ExternalContributionEndpoints`, and service registration in `ServiceCollectionExtensions`.
- `ExternalContributionCommandService` reuses `TimeEntryCommandService`, `TimeEntrySubmissionCommandService`, `ITimesheetsAccessGuard`, and fail-closed reference validation. Use the same pattern for magic-link issuance authority.
- `TimesheetsAccessGuard` validates tenant access first, then Project or Work, then Contributor Party, then policy. Preserve this order for capability issuance and management.
- Default reference validators and tenant access validators are fail-closed. Positive magic-link tests need configured fakes; production defaults must not become allow-all.
- `Program.cs` currently maps default endpoints, external contribution endpoints, and `/metadata/timesheets`. Add a distinct magic-link endpoint mapping rather than broadening external contribution routes.
- `DiagnosticsPrivacyTests` scans logging lines for sensitive terms including token-related strings. New logging must remain correlation-safe and avoid raw token/capability material.
- `Directory.Packages.props` currently pins Aspire `13.4.5`, `Microsoft.Extensions.*`, OpenTelemetry, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`. Do not add inline package versions.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add direct SQL, Redis, Dapr state, local JSON, broker-backed CRUD, or mutable projection state for magic-link authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Magic links are scoped capabilities, not login sessions. They are server-generated opaque tokens with only token hash and metadata stored. [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- Capability metadata binds tenant, Contributor Party, target entry/proposed entry, allowed action, expiry, and single-use state. [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- Issue, use, revoke, and expire outcomes persist through EventStore-backed audit events. Story 3.2 owns issue/revoke/expire; Story 3.3 owns use. [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- ASP.NET Core Data Protection may be used only for purpose-bound protection where useful; it is not the single-use or revocation source of truth. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-protection-and-secrets`]
- Tenant and resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Magic-link routes are capability-specific and must not expose browse/query surfaces or a general token inspection endpoint. [Source: `_bmad-output/planning-artifacts/architecture.md#API-Naming-Conventions`]
- Contracts contains commands, events, queries, IDs, value objects, UI descriptors, and OpenAPI artifacts only. Server owns aggregate decisions, validation orchestration, and magic-link capability logic. [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- Logs and traces must not include comments, tokens, secrets, command bodies, event payloads, magic-link values, personal data, target names, or protected identifiers. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]

### UX And Metadata Constraints

- Story 3.2 does not implement the external confirmation page, but it must expose safe metadata for internal operators to see issued confirmation request state.
- Internal operator state must use FrontComposer-compatible metadata and Fluent UI V5 status badge vocabulary with text, not color-only state.
- Operator-visible metadata may include link state, expiry state, allowed action, safe scope IDs, issuer/audit metadata, and projection freshness. It must not include raw tokens, decoded material, token hashes unless explicitly restricted to internal audit, comments, Party personal data, Project/Work names, or sibling-owned state.
- Invalid-link display behavior is mainly Story 3.5, but Story 3.2 revoke/expire outcomes must leave enough state for later token use to map to the same no-disclosure invalid-state response.

### Previous Story Intelligence

- Story 3.1 established the pattern for a narrow external-facing endpoint that derives `TimesheetsRequestContext` from trusted claims and trace identifiers, not request-body authority.
- Story 3.1 tests verify external contribution routes do not expose bearer/raw token concepts, EventStore internals, authority fields, or detailed denial copy. Story 3.2 should mirror and extend this route/static verification pattern.
- Story 3.1 reused existing Time Entry capture/submission/confirmation services instead of duplicating aggregate validation. Story 3.2 should reuse shared access guard, reference validators, Activity Type freshness models, metadata descriptors, and privacy tests rather than creating a parallel authorization or diagnostics path.
- Story 3.1 kept confirmation separate from approval. Magic-link issuance must also stay separate from approval, locking, ledger eligibility, export, and correction flows.
- Story 3.1 added OpenAPI and metadata additively. Story 3.2 must preserve additive contract evolution and avoid removing or renaming existing schemas.
- Story 3.1 implementation and review used direct xUnit v3 executable fallback when VSTest socket permissions were blocked. Keep the same verification fallback documented.

### Git Intelligence Summary

- `1876378 feat(story-3.1): Expose External Contributor Confirmation API` added external contribution commands/events, endpoint mapping, service orchestration, projection/read-model fields, metadata/OpenAPI, and contract/server/projection/integration/privacy tests.
- `5200265 feat(story-3.1): update orchestration state for story 3.1 progression` updated orchestration state only.
- `68af2da docs(epic-2): add retrospective updates` added guidance that external API paths must reuse existing approval, correction, locking, and safe-denial services.
- `ce2d07d feat(story-2.8): Approve or Reject Timesheet Periods` remains the latest broad pattern for cross-cutting Timesheets stories: contracts, server service/aggregate changes, projections/read models, metadata/OpenAPI, focused tests, and exact File List updates.

### Latest Technical Information

- No package upgrade or new external dependency is required for Story 3.2. Use repository pins in `Directory.Packages.props`.
- If cryptographic token generation/hash code requires APIs, use .NET in-box cryptography (`System.Security.Cryptography`) before adding dependencies.
- Do not add inline package versions, `.sln` files, Dockerfiles, direct infrastructure packages, UI framework packages, or dependency upgrades for this story.

### Project Context Reference

- EventStore context: aggregates are pure command/state to events; EventStore owns persistence and envelope metadata; projections must be idempotent; payloads, comments, personal data, and tokens must not be logged.
- Tenants context: tenant access fails closed; tenant/member/role projections are eventually consistent; authorization must not be weakened for tests.
- Parties context: Party owns identity/personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred internal UI path.
- No Works project-context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a Timesheets UI project for Story 3.2 fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Every claimed fail-closed path needs executable test proof: missing tenant, invalid Party, invalid/stale/unavailable Project or Work, inactive/missing Activity Type, unconfigured policy, cross-tenant target, invalid expiry, revoked/expired state, and privacy-safe denial copy.

### Anti-Patterns To Prevent

- Do not persist raw magic-link tokens, decoded capability material, bearer tokens, comments, command bodies, event payloads, personal data, target names, raw claims, upstream details, EventStore envelopes, or protected identifiers.
- Do not implement token authority with direct SQL/Redis/Dapr state, local files, mutable projections, static dictionaries, cache-only state, or token self-validation alone.
- Do not use Data Protection-only tokens as the revocation or single-use authority.
- Do not bypass `ITimesheetsAccessGuard`, reference validators, Activity Type catalog freshness, policy evaluation, or EventStore-backed domain events.
- Do not accept tenant/user/actor/correlation/authority/policy fields from request bodies as trusted.
- Do not add confirmation use, adjustment flow, invalid-link no-disclosure UI, a token inspection endpoint, a generic browse API, a full external portal, or internal shell navigation.
- Do not treat issuance, confirmation, or capability state as approval, locking, ledger eligibility, export eligibility, or correction.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks`, `Events/MagicLinks`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/MagicLinks`, with registrations in `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`.
- Expected host additions belong under `src/Hexalith.Timesheets/Endpoints/MagicLinks` or a similarly focused endpoint mapping file, registered from `src/Hexalith.Timesheets/Program.cs`.
- Expected projection additions belong under `src/Hexalith.Timesheets.Projections/MagicLinks` unless implementation proves a projection is deferred; if deferred, metadata/read behavior must still be explicit and tested.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests`.
- Do not use `docs/` as scratch space. BMAD/generated artifacts belong under `_bmad-output/`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-3-2-Issue-Scoped-Magic-Link-Confirmation-Capabilities`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-3-External-Contributor-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-14-Support-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#UJ-3-Simon-confirms-supplier-time-through-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#Open-Questions`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Implications-For-Timesheets`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-protection-and-secrets`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API-Naming-Conventions`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-3-Simon-confirms-supplier-time-through-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/implementation-artifacts/3-1-expose-external-contributor-confirmation-api.md#Previous-Story-Intelligence`]
- [Source: `README.md#Build-and-Test`]
- [Source: `Directory.Packages.props`]
- [Source: `src/Hexalith.Timesheets/Program.cs`]
- [Source: `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions/SubmitExternalTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions/ConfirmExternalTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryContributorConfirmed.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/ExternalContributionSource.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/TimeEntryContributorConfirmationEvidence.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsAccessGuard.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs`]
- [Source: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandService.cs`]
- [Source: `tests/Hexalith.Timesheets.Contracts.Tests/ExternalContributionContractTests.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/ExternalContributionCommandServiceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionEndpointTests.cs`]
- [Source: `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`]
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

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- `dotnet test` for Contracts.Tests, Server.Tests, Projections.Tests, ArchitectureTests, and IntegrationTests was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- Used README direct xUnit v3 executable fallback:
  - `tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 63 total, 0 failed.
  - `tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed: 309 total, 0 failed (306 at dev time; +3 added during review).
  - `tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed: 44 total, 0 failed.
  - `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed: 20 total, 0 failed.
  - `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 37 total, 0 failed, 2 skipped infrastructure/performance placeholders.

### Completion Notes List

- Added magic-link confirmation capability contracts, value objects, commands, events, one-time issue response, safe read model, and OpenAPI schemas.
- Implemented pure EventStore-style issue/revoke/expire aggregate logic and state folding, plus a command service that authorizes and validates references/activity catalog before generating an opaque one-time token.
- Added cryptographic token generation with persisted hash-only event metadata; no raw token is stored, projected, logged, or exposed by metadata/read models.
- Added narrow host routes for issue/revoke/expire, registered the server service and endpoint mapping, and kept request authority derived from trusted server sources.
- Added replay-safe projection support and FrontComposer-compatible metadata with text status badge vocabulary for capability state and expiry state.
- Added focused contract, server, projection, architecture/privacy, and endpoint tests covering success, fail-closed paths, privacy, metadata, and no token-inspection API.

### File List

- `_bmad-output/implementation-artifacts/3-2-issue-scoped-magic-link-confirmation-capabilities.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/ExpireMagicLinkConfirmationCapability.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/IssueMagicLinkConfirmationCapability.cs`
- `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/RevokeMagicLinkConfirmationCapability.cs`
- `src/Hexalith.Timesheets.Contracts/Events/MagicLinks/MagicLinkConfirmationCapabilityExpired.cs`
- `src/Hexalith.Timesheets.Contracts/Events/MagicLinks/MagicLinkConfirmationCapabilityIssued.cs`
- `src/Hexalith.Timesheets.Contracts/Events/MagicLinks/MagicLinkConfirmationCapabilityRevoked.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkAuditMetadata.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkConfirmationCapabilityReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkConfirmationScope.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkIssueResponse.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/MagicLinkCapabilityId.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/MagicLinkEnums.cs`
- `src/Hexalith.Timesheets.Contracts/ValueObjects/MagicLinkTokenHash.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs`
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkProjectionEvent.cs`
- `src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/CryptographicMagicLinkTokenGenerator.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/IMagicLinkTokenGenerator.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkCapabilityCommandResult.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkCapabilityState.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapability.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkTokenMaterial.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`
- `src/Hexalith.Timesheets/Program.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/MagicLinkContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/AuthorizationServiceTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs`

### Change Log

- 2026-06-19: Implemented Story 3.2 issue-scoped magic-link confirmation capabilities with contracts, server logic, endpoint mapping, projection/read metadata, OpenAPI artifacts, and focused tests.
- 2026-06-19: Adversarial code review (auto-fix). Closed a fail-closed gap where a malformed scope with a null target threw a `NullReferenceException` in `MagicLinkConfirmationCapabilityCommandService.TryResolveActivityTypeScope` instead of producing an opaque rejection; added a guard that fails closed on a missing/unknown target. Added server tests for malformed-scope, unknown allowed action, and premature-expiry rejection paths. Recorded the previously undocumented `MagicLinkConfirmationCapabilityWorkflowE2ETests.cs` in the File List and refreshed test counts. Re-verified build (0 warnings, warnings-as-errors) and all affected lanes (473 total, 0 failed, 2 skipped).

## Senior Developer Review (AI)

**Reviewer:** Jerome
**Date:** 2026-06-19
**Outcome:** Approved (auto-fix applied)

### Summary

Implementation faithfully delivers all five acceptance criteria. Tasks marked complete are genuinely implemented: contracts/value objects/events, a pure issue/revoke/expire aggregate, a fail-closed command service ordered tenant → reference → contributor → policy → activity-catalog before token generation, in-box cryptographic opaque token generation with hash-only persistence, a replay-/duplicate-tolerant projection with text status and expiry badges, additive closed OpenAPI schemas, FrontComposer-compatible metadata, and strong privacy/architecture fitness tests. Build is clean under `-warnaserror`; all affected suites pass. The narrow host endpoints mirror the Story 3.1 fail-closed "registration seam" pattern (null state + unavailable catalog) and defer real EventStore load/persist to a later data-bearing story, which is consistent with the established codebase convention.

### Findings and Resolution

- **[Medium — Fixed] Fail-closed gap on malformed scope.** `TryResolveActivityTypeScope` dereferenced `command.Scope.Target` without a null check, throwing a `NullReferenceException` (HTTP 500) instead of an opaque fail-closed rejection when the scope carried a null/unknown target. Reachable via the dispatcher/service path once a fresh catalog is supplied. Added a guard that rejects a missing or `Unknown` target with `ValidationFailed`. Covered by a new test (`Issue_magic_link_fails_closed_on_malformed_scope_without_target`).
- **[Medium — Fixed] Incomplete File List / stale Debug Log.** `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs` existed on disk but was absent from the File List, and Debug Log counts were stale (IntegrationTests 35→37). Both corrected.
- **[Low — Fixed] Missing coverage on two rejection branches.** Added tests for `AllowedAction.Unknown` rejection and premature-expiry rejection (`Expire` before the stored expiry instant), exercising previously untested branches.
- **[Low — Noted, not changed] Sibling submodule pointer drift.** `Hexalith.FrontComposer` and `Hexalith.Tenants` submodule pointers are modified in the working tree but are unrelated to Story 3.2 and pre-date this change set. Per repository guidance (do not modify/initialize sibling/nested submodules), these were intentionally left untouched rather than reverted blindly; they should be reviewed/handled separately before commit.
- **[Low — Noted, not changed] Unused `MagicLinkDisclosure` operation.** `TimesheetsOperation.MagicLinkDisclosure` (value 8) is declared and asserted in the enum-name test but not yet consumed by any authorization path. Left in place as intentional additive groundwork for Story 3.3 (token validation/use disclosure).

### Verification

- `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`: succeeded, 0 warnings, 0 errors.
- Direct xUnit v3 executables (VSTest socket fallback): Contracts 63, Server 309, Projections 44, Architecture 20, Integration 37 (2 skipped) — 0 failed.
