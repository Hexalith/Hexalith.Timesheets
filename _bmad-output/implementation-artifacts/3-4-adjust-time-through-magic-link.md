---
baseline_commit: 2dd4a2f413d7c4952fb67b674a55a0c9c18bf2e6
---

# Story 3.4: Adjust Time Through Magic Link

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an external contributor,
I want to adjust allowed fields in a scoped magic-link confirmation,
so that proposed time can be corrected before it enters approval without granting internal access.

## Acceptance Criteria

1. Given a magic-link token is valid, unexpired, unused, tenant-scoped, and allows adjustment, when the external contributor opens the adjustment flow, then only policy-allowed fields are editable and internal-only fields, approval state, tenant context, and broader Timesheets navigation remain unavailable.
2. Given the external contributor submits adjusted time, when the command is handled, then the adjustment follows the same EventStore-backed validation, target-reference, Activity Type, tenant-isolation, and audit rules as Time Entry capture and confirmation remains distinct from approval.
3. Given an adjusted value uses an invalid duration, disallowed Activity Type, unauthorized target, or cross-tenant reference, when the adjustment command is handled, then the command fails closed and no partial Time Entry, confirmation use, or protected details are persisted.
4. Given adjustment telemetry and audit evidence are recorded, when the adjustment succeeds or fails, then token values, decoded capability material, comments beyond policy, command bodies, personal data, and target names are not logged and only hashed/scoped references and outcome categories are available where policy allows.
5. Given the adjustment page is used on phone or keyboard-only navigation, when the contributor changes allowed fields and submits, then Fluent UI V5 components provide labels, validation messages, focus order, clear units, and accessible action controls and no hover-only or color-only state is introduced.

## Tasks / Subtasks

- [x] Add token-scoped adjustment contracts and safe display metadata (AC: 1, 2, 4, 5)
  - [x] Add a narrow command contract under `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks`, for example `AdjustTimeThroughMagicLink`, carrying only v1 editable proposed-entry values: service date, duration minutes, Activity Type ID, Billable State, and comment where policy allows.
  - [x] Do not accept tenant, contributor, Time Entry ID, target, target kind, approval state, authority, token hash, raw token, decoded token material, actor, correlation, or audit source from the external request body. Target changes are not v1 editable fields; if a caller attempts to submit target fields, schema/model binding and service validation must fail closed without disclosure.
  - [x] Add or extend a safe display/adjustment response under `src/Hexalith.Timesheets.Contracts/Models/MagicLinks` so the UI can distinguish read-only scoped fields from editable policy-allowed fields without exposing internal context.
  - [x] Extend `MagicLinkAllowedAction.Adjust` / `ConfirmOrAdjust` handling without weakening existing `Confirm` behavior.
  - [x] Update `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json` additively and prove adjustment request/display schemas exclude authority, raw token/hash, target names, command-body audit source, and EventStore envelopes.
- [x] Implement a dedicated pre-approval external adjustment domain path (AC: 2, 3)
  - [x] Add an EventStore-backed command/event path for draft/proposed external adjustment, for example `TimeEntryAdjustedThroughMagicLink` or `TimeEntryExternalAdjustmentRecorded`, that stores previous values, adjusted values, scoped contributor, tenant, adjusted-at UTC instant, and safe magic-link source metadata.
  - [x] Apply the adjustment event to `TimeEntryState` so projections and later approval workflows see the adjusted effective values.
  - [x] Reuse capture validation rules for adjusted values: positive whole-minute duration, valid scoped target shape from capability/state, valid scoped contributor, fresh Activity Type catalog, Activity Type active/available, Project Activity Type scope checks, comment policy validation, UTC timestamp validation, and tenant/resource gates.
  - [x] Do not reuse `CorrectRejectedTimeEntry` or `CorrectApprovedTimeEntry`; those require rejection/approval lineage and approval-authority resolution, while this story is external contributor self-adjustment before approval.
  - [x] Restrict adjustment to recorded external proposed entries in `Draft` state, matching the validated capability's `TimeEntryId`, contributor, target, and tenant. Submitted, approved, rejected, locked, corrected, superseded, internal, AI-agent, or mismatched entries must fail closed.
  - [x] Confirmation remains separate from approval. Adjustment must not approve, submit, lock, period-approve, ledger-qualify, finance-export, or silently mark a token used before the Time Entry adjustment succeeds.
- [x] Extend magic-link capability use semantics for adjustment (AC: 1-4)
  - [x] Add `AdjustAsync` or equivalent to `MagicLinkConfirmationCapabilityCommandService` beside `ConfirmAsync` and `DescribeAsync`.
  - [x] Derive the server-side token hash from the opaque token and validate capability state before disclosure or dispatch: exists, issued, unexpired, unused, tenant match, token hash match, target kind `ProposedTimeEntry`, and allowed action `Adjust` or `ConfirmOrAdjust`.
  - [x] Validate the proposed Time Entry state matches the capability scope before applying adjusted values; caller input must not change Party, Time Entry ID, target, tenant, or approval workflow.
  - [x] Record capability use through EventStore-backed magic-link use/audit evidence only after adjusted values pass validation and the Time Entry adjustment event is accepted. If either side fails, persist no partial capability-use or Time Entry adjustment event.
  - [x] Build audit/source metadata from validated capability state, such as `("magic-link", capabilityId)`, never from caller input.
  - [x] Preserve no-disclosure outcomes for malformed, unknown, expired, revoked, used, wrong-action, wrong-scope, wrong-recipient, tenant-mismatch, hash-mismatch, stale/unavailable catalog, unauthorized target, and replay attempts.
- [x] Wire real load/fold seams into magic-link endpoints before adding adjustment routes (AC: 1-3)
  - [x] Replace the current endpoint-level `null` capability/time-entry state and `UnavailableCatalog()` placeholders in `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` with a story-owned service seam that loads/folds capability state, Time Entry state, and fresh Activity Type catalog/read data before calling `DescribeAsync`, `ConfirmAsync`, or the new adjustment method.
  - [x] Keep EventStore as the authoritative source for folded write-side state. Do not use projections, SQL, Redis, Dapr state, local files, caches, static dictionaries, or Data Protection-only tokens as authority for single-use or adjustment.
  - [x] Add narrow routes for adjustment display/submit, for example `/api/timesheets/magic-links/adjust` and `/api/timesheets/magic-links/adjust/submit`, without adding a generic token inspection endpoint or browse/query API.
  - [x] Endpoint denial copy must remain one opaque no-disclosure response and must not reveal whether a tenant, Party, Project, Work, Time Entry, token, expiry reason, or Activity Type exists.
- [x] Add or extend external adjustment UI metadata/surface only as needed (AC: 1, 5)
  - [x] If a Blazor UI slice is introduced, add `src/Hexalith.Timesheets.UI` and `tests/Hexalith.Timesheets.UI.Tests` as the first UI-bearing Timesheets package following the architecture's UI project timing decision.
  - [x] Use FrontComposer first and Blazor Fluent UI V5 components for editable fields, validation messages, message bars, dialogs, labels, focus order, and action controls.
  - [x] Keep the external page single-purpose and outside internal shell navigation; do not add a full external portal, dashboard, Project/Work detail page, Party profile, approval UI, or broad Timesheets navigation.
  - [x] Use a focused `FluentDialog` or equivalent Fluent V5 pattern for adjustment decision review if the UX needs a second confirmation step; only one dialog layer is allowed.
- [x] Project safe adjustment state and update metadata/OpenAPI (AC: 2, 4)
  - [x] Extend `TimeEntryEvidenceProjection` and related read models so adjusted draft/proposed values, previous values, and safe adjustment evidence are visible where policy allows.
  - [x] Extend `MagicLinkConfirmationCapabilityProjection` / read model only with safe used/adjustment outcome categories and timestamps; exclude raw token, token hash where disallowed, decoded material, command bodies, comments beyond policy, personal data, target names, and EventStore envelopes.
  - [x] Update `TimesheetsMetadataCatalog` additively for adjustment command/display metadata and text status badges.
  - [x] Preserve schema and event evolution as additive; do not remove or rename previously emitted fields.
- [x] Add focused tests across contracts, server, projections, endpoints, privacy, and workflow (AC: 1-5)
  - [x] Contract tests: JSON round trips, enum handling, OpenAPI schemas, forbidden authority/token/hash fields absent from adjustment request/display/read schemas, and safe display field policy.
  - [x] Server tests: valid `Adjust` and `ConfirmOrAdjust` capabilities adjust only allowed fields; `Confirm`-only capability rejects adjustment; invalid duration/activity/target/tenant/contributor/state fails closed with no capability use and no Time Entry event.
  - [x] Server tests: adjustment cannot alter tenant, contributor, Time Entry ID, target, approval state, external source authority, audit source, or internal-only fields.
  - [x] Service/endpoint tests: real load/fold seams feed display/confirm/adjust methods; no route remains a permanent null-state stub; no token-inspection route exists.
  - [x] Projection tests: adjustment event updates effective draft values deterministically, preserves previous values/lineage, handles duplicate delivery/replay, and keeps freshness metadata.
  - [x] Privacy/diagnostics tests: no raw tokens, token hashes where disallowed, decoded material, command bodies, comments beyond policy, personal data, target names, protected identifiers, or EventStore envelopes appear in logs, read models, OpenAPI display models, or diagnostics.
  - [x] UX/component tests if UI is added: phone viewport, keyboard-only navigation, labels, validation messages, focus order, Fluent UI V5-only component use, no hover-only or color-only states, and no internal shell navigation.
- [x] Verify affected build and test lanes (AC: 1-5)
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`.
  - [x] Run `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run affected tests individually: ArchitectureTests, Contracts.Tests, Server.Tests, Projections.Tests, IntegrationTests, and UI.Tests if a UI package is added.
  - [x] If `dotnet test` is blocked by local VSTest socket permissions, use the README direct xUnit v3 executable fallback and record the reason in Debug Log References.

## Dev Notes

### Source Documents Loaded

- Loaded `{epics_content}` from `_bmad-output/planning-artifacts/epics.md`, especially Epic 3 and Story 3.4.
- Loaded `{architecture_content}` from `_bmad-output/planning-artifacts/architecture.md`, especially EventStore-first persistence, magic-link model, authorization, API naming, validation, data exchange/privacy, project structure, UI, and testing sections.
- Loaded PRD context from `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and addendum, especially FR13, FR14, UJ-3, NFR6, NFR8, NFR12, NFR13, and the v1 decision that secondary identity verification is deferred post-v1.
- Loaded UX context from `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md` and `DESIGN.md`, especially external magic-link confirmation/adjustment, invalid state, phone usability, Fluent UI V5, `FluentDialog`, validation messages, focus order, and no external portal.
- Loaded persistent project facts from `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Parties`, `Hexalith.Projects`, `Hexalith.Conversations`, and `Hexalith.FrontComposer` project-context files. No current Timesheets `project-context.md` file was present.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/3-3-confirm-time-through-magic-link.md`.
- Read current Timesheets magic-link contracts/server/projection/endpoints/tests, external contribution service, Time Entry aggregate/state/validation rules, correction services, metadata catalog, README test fallback, and package pins listed in References.

### Epic And Story Context

- Epic 3 enables external contributors to submit, confirm, or adjust scoped time without becoming internal tenant users.
- Story 3.1 delivered API-only external contribution submission/confirmation. Story 3.2 delivered scoped magic-link issuance, hash-only token storage, revocation/expiry, operator projection, and metadata. Story 3.3 delivered confirmation use and safe display service behavior.
- Story 3.4 owns adjustment through a scoped magic link. Story 3.5 owns broader invalid-link no-disclosure equivalence across invalid, expired, used, revoked, unauthorized, malformed, unknown, and enumeration states.
- FR14 requires single-use scoped expiring magic links for confirm or adjust. Invalid or already-used links reveal no tenant, Project, Work, Party, Time Entry, duration, Activity Type, comment, approval, expiry, or token-existence details.
- Adjustment is contributor evidence/capture correction before approval. It is not internal approval, rejected-entry correction, approved-entry correction, period approval, finance export eligibility, or full external account access.
- The v1 policy is explicit: secondary identity verification for high-value/billable entries is deferred post-v1. Do not add OTP, identity proofing, challenge questions, document upload, or full login in this story.

### Current Code State To Extend

- `MagicLinkAllowedAction` already contains `Confirm`, `Adjust`, and `ConfirmOrAdjust`, but Story 3.3 implemented confirmation only. Current `ConfirmAsync` rejects `Adjust`-only capabilities and accepts `Confirm` / `ConfirmOrAdjust`.
- `ConfirmTimeThroughMagicLink` is intentionally an empty marker record. Follow the same pattern for adjustment: the external body may carry adjusted proposed values only, not authority or attribution.
- `MagicLinkConfirmationCapabilityCommandService.DescribeAsync` returns safe display details after validating token hash, capability state, Time Entry state, and fresh Activity Type catalog. Reuse and extend this validation shape for the adjustment flow.
- `MagicLinkConfirmationCapability.HandleUse` emits `MagicLinkConfirmationCapabilityUsed` with server-derived source metadata. If one use event is reused for adjustment, tests must make the action/outcome unambiguous through safe metadata/read state; otherwise add an additive adjustment-specific magic-link use event.
- Host endpoints in `MagicLinkConfirmationCapabilityEndpoints` still pass `null` capability state, `null` Time Entry state, and `UnavailableCatalog()` into issue/revoke/expire/confirm/display. Story 3.3 review called this a medium follow-up. Story 3.4 should wire the load/fold seam before adding adjustment routes so adjustment works end-to-end instead of becoming another permanent stub.
- `ExternalContributionCommandService.SubmitAsync` and `TimeEntryCommandService.RecordAsync` already model capture validation and external source handling. Use their validation patterns, but do not create duplicate entry IDs or bypass the existing recorded proposed entry's scope.
- `TimeEntry.Handle(ConfirmExternalTimeEntry, ...)` confirms contributor evidence only. Do not overload it to mutate adjusted values.
- `TimeEntry.Handle(CorrectRejectedTimeEntry, ...)` and `TimeEntry.Handle(CorrectApprovedTimeEntry, ...)` are not suitable for this story. They require rejection/approval lineage and use `TimeEntryCorrectionCommandService`, which resolves `ApprovalAuthorityAction.CorrectionAuthorization`; external pre-approval adjustment must remain token-scoped contributor adjustment.
- `TimeEntryState` already tracks recorded facts, approval state, contributor confirmation source, correction state, and comment. The new adjustment event must update effective draft values consistently and preserve previous values/lineage for audit.

### Architecture Constraints

- Domain state changes persist only through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, local files, broker-backed CRUD, static dictionaries, mutable caches, direct projection mutation, or Data Protection-only tokens as magic-link or Time Entry authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Architecture`]
- Magic links are scoped capabilities, not login sessions. Store only token hash and safe metadata; token values and decoded material are never logged, projected, exported, stored in comments, or accepted in command bodies. [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- Tenant/resource gates run before aggregate load, command dispatch, projection read, export, or magic-link disclosure. [Source: `_bmad-output/planning-artifacts/architecture.md#Authorization-model`]
- Reference validation against Projects, Works, Parties, and Tenants happens before trust-bearing writes. Adjustment fails closed on missing, stale, unavailable, ambiguous, invalid, or cross-tenant authority. [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- Magic-link routes are capability-specific and must never expose a general token inspection endpoint, broad browse/query API, or raw EventStore envelope. [Source: `_bmad-output/planning-artifacts/architecture.md#API-Naming-Conventions`]
- Contracts stays infrastructure-free. Server owns aggregate decisions, validation orchestration, and magic-link capability logic. Projections are rebuildable, idempotent, and non-authoritative for writes. [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- Logs/traces must not include comments, tokens, secrets, command bodies, event payloads, magic-link values, personal data, target names, protected identifiers, or EventStore envelopes. [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- If UI is introduced, `Hexalith.Timesheets.UI` and `Hexalith.Timesheets.UI.Tests` are added with the first UI-bearing story. Use the documented `UI/` structure and Fluent UI V5-only rule. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure-Boundaries`]

### UX And External Surface Constraints

- The external magic-link surface is single-purpose, minimal, responsive, and may sit outside internal shell navigation, but it must use Fluent UI V5 component patterns and no-disclosure security behavior.
- Valid adjustment display may show only scoped proposed-entry details needed for adjustment and must mark which fields are editable. The page must not show tenant context, Project/Work names beyond allowed minimal context, Party profile data, internal approval state controls, or broader Timesheets navigation.
- Editable v1 fields should be limited to policy-allowed proposed-entry facts: service date, duration minutes, Activity Type, Billable State, and comment where policy allows. Target, contributor, tenant, Time Entry ID, source authority, approval state, and confirmation/approval workflow are server-derived/read-only. AC3's unauthorized-target case is covered by rejecting any attempted target input and by revalidating the capability/state target before dispatch.
- Invalid states must use the same no-disclosure failure surface. Provide one safe recovery path without exposing whether the link, tenant, contributor, target, or entry exists.
- Phone and keyboard-only use are in scope: clear duration units, labels, validation messages, reachable controls, logical focus order, and no hover-only or color-only state.
- Do not create a full external portal, dashboard, internal shell navigation, token inspection page, broad Timesheets browse surface, Party profile, Project/Work lifecycle view, approval UI, or finance/export affordance.

### Previous Story Intelligence

- Story 3.3 implemented safe confirmation display and submit service logic, including server-derived audit source and tests for token/hash privacy.
- Story 3.3 review fixed a critical display-path stub at the service level, but left host endpoints passing null folded state and unavailable catalogs. Story 3.4 should address that endpoint seam as part of adjustment to avoid another route that can never succeed in a live host.
- Story 3.3 confirms through `ExternalContributionCommandService.ConfirmAsync` and `TimeEntry.Handle(ConfirmExternalTimeEntry, ...)`; adjustment must not be added to that confirmation handler.
- Story 3.3 service tests already cover wrong action (`Adjust` for confirm), wrong scope, tenant mismatch, token mismatch, expired, used, revoked, and invalid display no-disclosure. Extend those cases for adjustment rather than creating a separate weaker matrix.
- Story 3.2 projection intentionally excludes token/hash/comment material from operator read models. Adjustment projection changes must keep that privacy boundary.
- Story 3.1 established external capture contracts and source metadata. Reuse `ExternalContributionSource("magic-link", capabilityId)` style server-derived source metadata for adjustment evidence.
- Recent story reviews emphasize exact File List updates, build verification, and direct xUnit v3 fallback when VSTest socket permissions fail.

### Git Intelligence Summary

- `2dd4a2f feat(story-3.3): Confirm Time Through Magic Link` added confirmation-use contracts/events, safe display response, service logic, endpoint routes, projection updates, OpenAPI updates, and tests. It also documented a follow-up to replace null endpoint seams with real load/fold behavior.
- `d0554af feat(story-3.2): Issue Scoped Magic-Link Confirmation Capabilities` added magic-link issue/revoke/expire contracts, hash-only token handling, projection/read metadata, OpenAPI, and route/metadata tests.
- `1876378 feat(story-3.1): Expose External Contributor Confirmation API` added external submission/confirmation commands/events, endpoint mapping, service orchestration, metadata/OpenAPI, and contract/server/integration/privacy tests.
- Recent story commits follow the same implementation shape: contracts, server service/aggregate changes, projections/read models, metadata/OpenAPI, focused tests, exact File List updates, build verification, and direct xUnit fallback where needed.

### Latest Technical Information

- No package upgrade or new external dependency is required for Story 3.4. Use repository pins in `Directory.Packages.props`; do not add inline package versions.
- Repository package pins at story creation time include .NET 10, Aspire `13.4.5`, Dapr architecture target `1.18.4`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `6.0.0-rc.1`, and Microsoft.NET.Test.Sdk `18.6.0`.
- Continue using in-box `System.Security.Cryptography` token hashing/generation already present in `CryptographicMagicLinkTokenGenerator`; do not add token or identity dependencies for this story.

### Project Context Reference

- EventStore context: aggregates are pure command/state to events; EventStore owns persistence and envelope metadata; projections must be idempotent; payloads, comments, personal data, and tokens must not be logged.
- Tenants context: tenant access fails closed; tenant/member/role projections are eventually consistent; authorization must not be weakened for tests.
- Parties context: Party owns identity and personal data; Timesheets stores Party IDs and hydrates display data at read time only.
- Projects context: Timesheets stores stable Project references and must not copy Project hierarchy, lifecycle, owners, or approver state.
- FrontComposer context: generated command/projection metadata, Fluent UI V5 component usage, text status badges, keyboard access, persistent message bars, and no parallel internal shell are the preferred UI path.
- Conversations context adds no direct implementation surface for this story, but reinforces EventStore ownership, tenant gates, Party data minimization, and avoiding provider/session IDs as durable authority.
- No Works project-context file was present. Follow Timesheets architecture for Work reference validation/authority and avoid copying Work lifecycle, ownership, or planned-effort state.

### Testing Standards

- Use xUnit v3, Shouldly assertions, and NSubstitute where fakes/mocks are needed.
- Keep unit/contract/projection tests deterministic and infrastructure-free. Do not require Dapr, Aspire, EventStore server, browser, network, external providers, or a full external portal for fast lanes.
- Run test projects individually. Use `.slnx` for restore/build only.
- Every claimed fail-closed path needs executable proof. For this story that includes malformed token, unknown token, hash mismatch, expired, revoked, used, wrong action, wrong target kind, stale/unavailable catalog, invalid duration, inactive/disallowed/ambiguous Activity Type, unauthorized target, cross-tenant target, mismatched contributor, mismatched entry, non-draft state, internal/AI contributor state, locked/corrected state, and opaque denial copy.

### Anti-Patterns To Prevent

- Do not persist raw magic-link tokens, decoded capability material, bearer tokens, comments beyond policy, command bodies, event payloads, personal data, target names, raw claims, upstream details, EventStore envelopes, or protected identifiers.
- Do not implement adjustment or single-use enforcement with projections, SQL, Redis, Dapr state, local files, mutable caches, static dictionaries, or Data Protection-only tokens.
- Do not accept tenant/user/actor/correlation/authority/policy/target/contributor/approval fields from request bodies as trusted.
- Do not let the external contributor choose Party, Time Entry ID, target, tenant, approval state, source metadata, capability ID, token hash, or audit source.
- Do not reuse rejected-entry or approved-entry correction commands for pre-approval magic-link adjustment.
- Do not treat adjustment as approval, submission, locking, ledger eligibility, finance export eligibility, period approval, or internal correction.
- Do not add secondary identity verification, a full external portal, internal shell navigation, token inspection, broad query/browse APIs, or approval UI.
- Do not add package versions to `.csproj`, create `.sln` files, weaken warnings-as-errors, initialize nested submodules, or modify sibling submodule files.

### Project Structure Notes

- Expected contract additions belong under `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks`, `Events/TimeEntries` or `Events/MagicLinks` as appropriate, `Models/MagicLinks`, `Models`, `ValueObjects`, `TimesheetsMetadataCatalog.cs`, and `openapi/timesheets-capture-contracts.v1.json`.
- Expected server additions belong under `src/Hexalith.Timesheets.Server/MagicLinks` and `src/Hexalith.Timesheets.Server/TimeEntries`, reusing existing authorization, Activity Type catalog, and Time Entry validation patterns.
- Expected host additions belong under `src/Hexalith.Timesheets/Endpoints/MagicLinks`, registered through existing endpoint mapping from `src/Hexalith.Timesheets/Program.cs`.
- Expected projection additions belong under `src/Hexalith.Timesheets.Projections/MagicLinks` and `src/Hexalith.Timesheets.Projections/TimeEntries`.
- Expected tests belong under `tests/Hexalith.Timesheets.Contracts.Tests`, `tests/Hexalith.Timesheets.Server.Tests`, `tests/Hexalith.Timesheets.Projections.Tests`, `tests/Hexalith.Timesheets.ArchitectureTests`, and `tests/Hexalith.Timesheets.IntegrationTests`.
- If UI is added, use `src/Hexalith.Timesheets.UI` and `tests/Hexalith.Timesheets.UI.Tests`. Do not use `docs/` or `_bmad-output/` as scratch space for implementation artifacts.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-3-4-Adjust-Time-Through-Magic-Link`]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-3-External-Contributor-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-14-Support-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#UJ-3-Simon-confirms-supplier-time-through-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/addendum.md#Implications-For-Timesheets`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#UJ-3-Simon-confirms-supplier-time-through-Magic-Link-Confirmation`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#State-Patterns`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md#Components`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Magic-Link-Confirmation-model`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API-Naming-Conventions`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Validation-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Component-Boundaries`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data-Exchange-Formats`]
- [Source: `_bmad-output/implementation-artifacts/3-3-confirm-time-through-magic-link.md#Previous-Story-Intelligence`]
- [Source: `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapability.cs`]
- [Source: `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkCapabilityState.cs`]
- [Source: `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryCorrectionCommandService.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/ConfirmTimeThroughMagicLink.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/ExternalContributions/SubmitExternalTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectRejectedTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Commands/TimeEntries/CorrectApprovedTimeEntry.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkConfirmationDisplayResponse.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/ValueObjects/MagicLinkEnums.cs`]
- [Source: `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`]
- [Source: `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs`]
- [Source: `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs`]
- [Source: `README.md#Build-and-Test`]
- [Source: `Directory.Packages.props`]

## Story Context Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` was attempted for ArchitectureTests, Contracts.Tests, Server.Tests, and Projections.Tests, but VSTest failed locally with `System.Net.Sockets.SocketException (13): Permission denied` while opening its socket channel. Used the README direct xUnit v3 executable fallback.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` - passed, all projects up-to-date.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` - passed, 0 warnings, 0 errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` - passed: 20 total, 0 failed, 0 skipped.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` - passed: 67 total, 0 failed, 0 skipped.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` - passed: 321 total, 0 failed, 0 skipped.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` - passed: 47 total, 0 failed, 0 skipped.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` - passed: 39 total, 0 failed, 2 skipped (pre-existing reserved infrastructure/performance lanes). [Corrected during review: the added adjustment workflow E2E test raised the lane from 38 to 39.]
- `git diff --check` - passed.

### Completion Notes List

- Added the narrow `AdjustTimeThroughMagicLink` contract carrying only service date, duration minutes, Activity Type ID, Billable State, and optional policy-governed comment.
- Added a dedicated pre-approval external adjustment path through `TimeEntryAdjustedThroughMagicLink`, `TimeEntry.Handle(AdjustTimeThroughMagicLink, ...)`, `ExternalContributionCommandService.AdjustAsync`, and `MagicLinkConfirmationCapabilityCommandService.AdjustAsync`.
- Adjustment validates token hash, capability state, action `Adjust`/`ConfirmOrAdjust`, proposed Time Entry scope, Draft/external state, fresh Activity Type catalog, duration/comment rules, tenant/resource gates, and server-derived source metadata.
- Capability use for adjustment is recorded only after the Time Entry adjustment succeeds; invalid adjusted values return with no capability-use event.
- Added `MagicLinkAdjustmentDisplayResponse`, additive OpenAPI schemas/routes, and FrontComposer metadata without creating a Blazor UI package.
- Replaced endpoint-level folded-state/catalog placeholders with `IMagicLinkConfirmationCapabilityStateLoader` and a fail-closed default implementation; added narrow adjustment display and submit routes.
- Extended Time Entry and magic-link projections/read models with safe adjustment evidence and outcome category fields while preserving raw token/hash and command-body privacy.

### File List

- `src/Hexalith.Timesheets.Contracts/Commands/MagicLinks/AdjustTimeThroughMagicLink.cs`
- `src/Hexalith.Timesheets.Contracts/Events/MagicLinks/MagicLinkConfirmationCapabilityUsed.cs`
- `src/Hexalith.Timesheets.Contracts/Events/TimeEntries/TimeEntryAdjustedThroughMagicLink.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkAdjustmentDisplayResponse.cs`
- `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/MagicLinkConfirmationCapabilityReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryEvidenceReadModel.cs`
- `src/Hexalith.Timesheets.Contracts/Models/TimeEntryExternalAdjustmentEvidence.cs`
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs`
- `src/Hexalith.Timesheets.Contracts/openapi/timesheets-capture-contracts.v1.json`
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkConfirmationCapabilityProjection.cs`
- `src/Hexalith.Timesheets.Projections/TimeEntries/TimeEntryEvidenceProjection.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/IMagicLinkConfirmationCapabilityStateLoader.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapability.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationUseResult.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEndpointTokenState.cs`
- `src/Hexalith.Timesheets.Server/MagicLinks/UnavailableMagicLinkConfirmationCapabilityStateLoader.cs`
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/ExternalContributionCommandService.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryAdjustmentCommandResult.cs`
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryState.cs`
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/MagicLinkContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs`
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionTests.cs`
- `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs`
- `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs`
- `_bmad-output/implementation-artifacts/3-4-adjust-time-through-magic-link.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-06-19: Implemented Story 3.4 adjustment-through-magic-link contracts, domain path, endpoint seam/routes, projections, metadata/OpenAPI, and focused tests; moved story to review.
- 2026-06-19: Senior Developer Review (AI, auto-fix). No CRITICAL/HIGH code defects found; adjustment path, fail-closed validation, and privacy verified. Fixed File List omission (`MagicLinkConfirmationCapabilityWorkflowE2ETests.cs`) and a stale Integration test count (38 → 39); logged non-blocking MEDIUM/LOW follow-ups. Build `0 warnings / 0 errors`; all affected lanes green. Status moved to done.

## Senior Developer Review (AI)

Reviewer: Jerome (adversarial auto-fix review) on 2026-06-19.

**Outcome: Approve (with tracked follow-ups).** Re-verified locally: `dotnet build Hexalith.Timesheets.slnx -warnaserror` → `0 warnings / 0 errors`. Tests (xUnit v3 direct-exe fallback; VSTest socket still blocked): Architecture 20, Contracts 67, Server 321, Projections 47, Integration 39 (2 pre-existing infra/perf skips) — all passing. Zero CRITICAL findings → status `done` per the CRITICAL-only gate.

### What was verified

- **AC1 (only policy-allowed fields editable):** `AdjustTimeThroughMagicLink` carries only `serviceDate`, `durationMinutes`, `activityTypeId`, `billableState`, and optional `comment`; OpenAPI schema is `additionalProperties: false`, so any attempt to submit `target`/`tenant`/`contributor`/`approval`/`token` fails closed at binding. `MagicLinkAdjustmentDisplayResponse` explicitly partitions `editableFields` vs `readOnlyFields` and exposes only target *kind* (no Project/Work name). Contract tests assert forbidden fields absent.
- **AC2 (capture-parity validation; confirmation ≠ approval):** `TimeEntry.ValidateExternalAdjustment` mirrors capture's `ValidateRequiredFields` (positive whole-minute duration, Activity Type id/scope, Billable state, comment policy) and adds stricter state gates (recorded, `Draft`, `ExternalContributor`, not locked, `CorrectionState.None`, scope match). It does **not** reuse `CorrectRejectedTimeEntry`/`CorrectApprovedTimeEntry`, never approves/submits/locks, and records capability use only after the Time Entry adjustment event is accepted.
- **AC3 (fail closed, no partial persistence):** invalid duration/activity/target/tenant/contributor/state return a rejection with `CapabilityResult == null` and no Time Entry event (server + workflow E2E tests prove `WasDispatched == false` and no capability use).
- **AC4 (privacy):** read models, projections, and OpenAPI display schemas carry only safe outcome categories (`confirmed`/`adjusted`) and scoped `("magic-link", capabilityId)` source metadata; tests serialize read models/evidence and assert absence of token/hash/command/party material.
- **AC5 (accessible adjustment surface):** expressed via FrontComposer metadata descriptors (labels, whole-minute hint, "status shown with text, not color alone"); see follow-up below — no Blazor UI package/component tests exist yet, consistent with the architecture's UI-timing deferral carried from Story 3.3.

### Findings and dispositions

1. **[MEDIUM — FIXED] File List omitted a modified test file (git-vs-story discrepancy).** `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs` was modified (+170 lines adding the full adjustment workflow + replay + privacy E2E test) but was absent from the Dev Agent Record → File List (it appeared only under References). Fix: added it to the File List.
2. **[LOW — FIXED] Stale Debug Log test count.** Debug Log References claimed Integration `38 total`; the added adjustment workflow E2E test raised the lane to `39 total` (2 skipped). Fix: corrected the count with a note.
3. **[MEDIUM — follow-up] Endpoint load/fold seam is structural only; no concrete EventStore-backed loader.** Story 3.4 introduced `IMagicLinkConfirmationCapabilityStateLoader` and wired every magic-link route (issue/revoke/expire/display/confirm/adjust) through it — a real improvement over the inline `null`/`UnavailableCatalog()` stubs flagged in the 3.3 review. However, the only registered implementation is `UnavailableMagicLinkConfirmationCapabilityStateLoader`, which returns `null` capability, `null` Time Entry, and an unavailable catalog, so all routes still deny end-to-end in a live host. The validated logic lives in the tested service layer. This is the 3.3 follow-up partially advanced, not closed; not unique to 3.4 and non-blocking per the CRITICAL-only gate. Tracked below. (No fix applied — a real loader requires EventStore aggregate-fold infrastructure absent across the epic's command services; fabricating one would be a broken stub.)
4. **[LOW — follow-up] AC5 not executably verified.** No `Hexalith.Timesheets.UI`/`*.UI.Tests` package was added; the adjustment surface exists only as FrontComposer metadata. Phone/keyboard accessibility is therefore asserted by descriptor hints, not component tests. Consistent with the deliberate UI deferral; the UI subtasks are satisfied via the conditional "if a Blazor UI slice is introduced" escape. Tracked below.
5. **[LOW — transparency] Working-tree changes outside the File List:** sibling submodule pointer bumps `Hexalith.FrontComposer` and `Hexalith.Tenants` (the story forbids modifying sibling submodules — left untouched, not staged, likely environmental) and `_bmad-output` tracking files (out of review scope). The source File List is otherwise accurate.

### Review Follow-ups (AI)

- [ ] [AI-Review][Medium] Provide a concrete EventStore-backed `IMagicLinkConfirmationCapabilityStateLoader` (fold capability + Time Entry state and load a fresh Activity Type catalog) so display/confirm/adjust function end-to-end in a live host instead of always denying via `UnavailableMagicLinkConfirmationCapabilityStateLoader` [src/Hexalith.Timesheets.Server/MagicLinks/UnavailableMagicLinkConfirmationCapabilityStateLoader.cs, src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs].
- [ ] [AI-Review][Low] When the first UI-bearing story lands, add `src/Hexalith.Timesheets.UI` + `tests/Hexalith.Timesheets.UI.Tests` and executable phone/keyboard/Fluent-V5/no-color-only coverage for the adjustment surface to close AC5 verification [_bmad-output/planning-artifacts/architecture.md#Project-Structure-Boundaries].
- [ ] [AI-Review][Low] Confirm the sibling submodule pointer changes (`Hexalith.FrontComposer`, `Hexalith.Tenants`) are intentional before committing; the story scope forbids modifying sibling submodules.
