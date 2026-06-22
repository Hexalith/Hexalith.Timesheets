---
baseline_commit: c4a9183249a22117e9eeeb6e6d92953255559b92
---

# Story 4.8: Implement Works Planned-Effort Reporting Adapter

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a work reviewer,
I want planned-vs-actual comparison backed by a concrete Works adapter or explicit unavailable launch policy,
so that Work reports do not imply planned-effort integration that does not exist.

## Acceptance Criteria

1. **Concrete, source-attributed planned effort (AC1 — FR17, FR20).**
   **Given** Work planned or estimated effort is available through a Works-owned query or approved adapter bridge
   **When** Work actual-time reports are queried
   **Then** `IWorkPlannedEffortProvider` returns source-attributed planned effort with freshness/reference-state metadata
   **And** planned-vs-actual comparisons identify Works as the source without converting AI token/runtime evidence into human duration.

2. **Fail closed, no leakage (AC2 — NFR1, NFR3, NFR8).**
   **Given** Works authority or planned-effort state is unavailable, stale, cross-tenant, ambiguous, disabled, unauthorized, or missing
   **When** a Work report is rendered
   **Then** planned effort is marked unavailable or the report fails closed according to policy
   **And** no Work lifecycle state, names, descriptions, ownership details, or protected identifiers are copied or leaked.

3. **Deterministic, freshness-aware, negative-path tested (AC3 — NFR9).**
   **Given** the adapter receives duplicate, replayed, or rebuilt Works projection data
   **When** report queries and validation paths run
   **Then** outputs are deterministic, freshness-aware, and covered by negative-path tests.

4. **Explicit launch policy (AC4 — FR23).**
   **Given** Work planned-effort reporting is not concrete at launch
   **When** documentation and UI are reviewed
   **Then** Work actuals remain available where authorized
   **And** planned-vs-actual claims are explicitly marked unavailable, waived, or post-v1.

## Tasks / Subtasks

- [x] **Task 1 — Implement the concrete provider (AC: 1, 2)**
  - [x] Add `src/Hexalith.Timesheets.Works/WorksQueryWorkPlannedEffortProvider.cs` implementing `IWorkPlannedEffortProvider` (clone the structure of the existing `WorksQueryWorkReferenceValidator`).
  - [x] Inject the existing `IWorksQueryChannel` port (do **not** add a new client or a new project reference — `Hexalith.Timesheets.Works.csproj` already references `.Server`, `Hexalith.Works.Contracts`, and `Hexalith.EventStore.Contracts`).
  - [x] Build the `QueryEnvelope` exactly as the validator does: `tenantId = context.Tenant!.TenantId`, `domain = "work"`, `aggregateId = work.WorkId`, `queryType = "get-work-item"`, `payload = []`, `correlationId = context.CorrelationId`, `userId = actor PartyId ?? correlationId`. Reuse the hardcoded `WorkDomainName`/`GetWorkItemQueryType` constants pattern.
  - [x] Deserialize the `QueryResult` payload to `WorkItemView` with `new JsonSerializerOptions(JsonSerializerDefaults.Web)`.
- [x] **Task 2 — State mapping & fail-closed (AC: 1, 2)**
  - [x] Map per the **Planned-Effort State Mapping** table in Dev Notes. Only return `Supplied(...)` when `view.Found` is true, the tenant matches, and **both** `Estimated` and `Unit` are present.
  - [x] Pass `view.Unit!.Value` (string) straight through — **never** convert the Works unit into minutes/hours or any Timesheets unit.
  - [x] On missing tenant context, non-success `QueryResult`, undefined payload, deserialize failure, or null view → `WorkPlannedEffortReadModel.Unavailable()`. Never fabricate planned values.
  - [x] Let `OperationCanceledException` propagate; fail closed to `Unavailable()` for every other exception (`#pragma warning disable CA1031`, mirroring the validator).
  - [x] Defensive cross-tenant guard: `view.TenantId.Value` vs `context.Tenant.TenantId.ToLowerInvariant()` → `Unauthorized()` on mismatch.
- [x] **Task 3 — DI composition extension (AC: 1, 4)**
  - [x] Add `src/Hexalith.Timesheets.Works/WorksPlannedEffortReportingServiceCollectionExtensions.cs` with `AddTimesheetsWorksPlannedEffortReporting()` using `services.Replace(ServiceDescriptor.Singleton<IWorkPlannedEffortProvider, WorksQueryWorkPlannedEffortProvider>())` (mirror `AddTimesheetsWorksReferenceValidation`). Must win in either call order relative to `AddTimesheetsServerKernel`.
  - [x] Do **not** change the kernel default registration `TryAddSingleton<IWorkPlannedEffortProvider, UnavailableWorkPlannedEffortProvider>()` in `Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs:72`.
  - [x] Apply the launch-policy decision (Open Question Q-D, default: **do not** wire into `src/Hexalith.Timesheets/Program.cs` for v1) and document it in the behavior note.
- [x] **Task 4 — Update the privacy fitness test (CRITICAL build blocker) (AC: 2)**
  - [x] Refactor `DiagnosticsPrivacyTests.Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state` so the `.Estimated`/`.Done`/`.Remaining`/`.Unit` token prohibition applies to the **validator file only** (`WorksQueryWorkReferenceValidator.cs`), not the whole `.Works` project. See **CRITICAL: Privacy Fitness-Test Blocker** in Dev Notes — without this the build fails.
  - [x] Keep `.OwnEffort` and `.Parent` forbidden across the **entire** `.Works` project (neither adapter touches Works internal roll-up structure or parent lineage).
  - [x] Keep `ILogger`/`logger`/`JsonSerializer.Serialize`/`SerializeToElement`/`SerializeToUtf8Bytes` forbidden across the entire `.Works` project (the provider must be log-free and must not serialize Works state outward).
  - [x] Confirm `DependencyDirectionTests.Contracts_take_no_works_or_eventstore_reference_and_server_takes_no_works_reference` stays green (all new code lives in `.Works`; do not add Works refs to `.Server`/`.Contracts`).
- [x] **Task 5 — Tests (AC: 1, 2, 3)**
  - [x] Add `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderTests.cs` + `...EdgeCaseTests.cs` (NSubstitute-faked `IWorksQueryChannel`). Extend `WorksQueryTestData.FoundView(...)` to populate `Estimated/Done/Remaining/Unit`.
  - [x] Cover the full state matrix: supplied-with-estimate, found-without-estimate → `NotSupplied`, not-found → `NotSupplied`, cross-tenant → `Unauthorized`, transport failure / `QueryResult.Failure` / undefined payload / deserialize failure / missing tenant → `Unavailable`, `OperationCanceledException` propagation, null-argument fast-fail.
  - [x] Determinism/replay test: two invocations for the same `WorkId` at the same `LatestAcceptedSourceSequence` produce equal results, no side effects.
  - [x] **Composed-service proof (mandatory):** resolve `ActualTimeReportQueryService` from a `ServiceCollection` with `AddTimesheetsServerKernel()` + `AddTimesheetsWorksPlannedEffortReporting()` + faked `IWorksQueryChannel`; run a Work report and assert the disclosed Work row's `WorkPlannedEffort.Availability == Supplied` and `SourceModuleName == "Works"`, and that the channel was invoked. Also assert that **without** the extension the row keeps the kernel default's `Unavailable` state. (Mirror `WorksReferenceValidationCompositionTests`.)
  - [x] Adapter-registration-wins test in both call orders relative to `AddTimesheetsServerKernel`.
- [x] **Task 6 — Behavior note & launch-policy doc (AC: 4)**
  - [x] Write `_bmad-output/implementation-artifacts/4-8-works-planned-effort-reporting-adapter-behavior.md` (mirror `1-10-works-reference-validation-adapter-behavior.md`): chosen path, state-mapping table, freshness limitation, tenant scoping, host-binding recipe, and the explicit launch posture (unavailable/waived/post-v1).
  - [x] Verify report metadata/UI copy for the Work actual-time report marks planned-vs-actual as unavailable/waived until the host composes both `IWorksQueryChannel` and the new extension; introduce no finance-ownership language.
- [x] **Task 7 — Build, test, verify, report (AC: 1, 2, 3)**
  - [x] Build `-warnaserror` (expect 0/0). Run the affected suites via the built xUnit v3 executables (VSTest socket is blocked in the sandbox).
  - [x] Generate the File List from the actual `git` diff and report exact test counts (no estimates).

## Dev Notes

### TL;DR — this is a "make the seam concrete" story, not a greenfield build

Every contract, type, and consumer this story plugs into **already exists and is tested**. Story 4.3 created the `IWorkPlannedEffortProvider` seam + `WorkPlannedEffortReadModel` contract + the `Unavailable` fail-closed default + the report consumer. Story 1.10 created the `IWorksQueryChannel` light-port + the `Hexalith.Timesheets.Works` adapter project + the `WorksQueryWorkReferenceValidator` template you will clone. Your deliverable is **one new provider class, one DI extension, one fitness-test edit, a test suite, and a behavior note.** Do **not** build a parallel Works client, a new project, or a new freshness type.

### The exact seam you are filling (from Story 4.3 — already in the repo)

`src/Hexalith.Timesheets.Server/OperationalReports/IWorkPlannedEffortProvider.cs`:

```csharp
public interface IWorkPlannedEffortProvider
{
    ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken);
}
```

`src/Hexalith.Timesheets.Contracts/Models/WorkPlannedEffortReadModel.cs` — the result contract (do not modify its shape; consume its factory methods):

```csharp
public sealed record WorkPlannedEffortReadModel(
    WorkPlannedEffortAvailability Availability,   // Unknown=0, Supplied=1, NotSupplied=2, Unavailable=3, Unauthorized=4, Stale=5
    string SourceModuleName,                       // always "Works" (factories hardcode it)
    decimal? Estimated, decimal? Done, decimal? Remaining, string? Unit,
    ActualTimeReferenceStateMetadata SourceReferenceState,
    ProjectionFreshnessMetadata SourceFreshness);
// Factories: Supplied(estimated, done, remaining, unit /*non-null*/, sourceFreshness)
//            NotSupplied(detail?) | Unavailable(detail?) | Unauthorized(detail?)
//            Stale(estimated, done, remaining, unit, sourceFreshness)
```

`Supplied(...)` validates `unit` is non-null/non-whitespace — only call it when both `Estimated` and `Unit` are present. The kernel default you keep is `UnavailableWorkPlannedEffortProvider` (returns `WorkPlannedEffortReadModel.Unavailable()`).

### How the consumer calls you (read this to write the composed-service test)

`ActualTimeReportQueryService.DiscloseRowsAsync` (`src/Hexalith.Timesheets.Server/OperationalReports/ActualTimeReportQueryService.cs`):

- Calls `GetPlannedEffortAsync` **only** for `row.Target.TargetKind == TimeEntryTargetKind.Work`, and **only after** per-row authorization passes. Denied/filtered rows short-circuit before any call — **never** query Works for a row that will be filtered or denied.
- Memoizes per `workId` in a `Dictionary<string, WorkPlannedEffortReadModel>` within one disclosure pass (so the same Work is queried once). Your provider must be safe to call once-per-distinct-work and return a stable result.
- Project rows keep `row.WorkPlannedEffort` as `null`; the projection seeds Work rows with `WorkPlannedEffortReadModel.NotSupplied()`, which your provider overwrites.
- `ActualTimeReportQueryService` is registered `TryAddSingleton<ActualTimeReportQueryService>()` (concrete) — resolvable directly in the composed test.

### The adapter template to clone (Story 1.10 — `WorksQueryWorkReferenceValidator.cs`)

Reuse verbatim: the `IWorksQueryChannel` injection, the hardcoded `WorkDomainName = "work"` / `GetWorkItemQueryType = "get-work-item"` constants, `BuildEnvelope(...)`, the `result.Success` / `payload.ValueKind == JsonValueKind.Undefined` / `payload.Deserialize<WorkItemView>(s_jsonOptions)` flow, the `catch (OperationCanceledException) throw;` + `CA1031` fail-closed catch, and the defensive cross-tenant re-check (`view.TenantId.Value` vs `context.Tenant.TenantId.ToLowerInvariant()`).

`IWorksQueryChannel` (`src/Hexalith.Timesheets.Works/IWorksQueryChannel.cs`) is the light port; the composed host binds it to `IDomainQueryInvoker` via a `DomainQueryInvokerWorksQueryChannel` pass-through. **Do not** inject `IDomainQueryInvoker` directly — it lives in the EventStore web host and drags an MSB3277 assembly-version conflict closure under `-warnaserror`. (This is why the port exists; it was built explicitly "reusable by Story 4.8's planned-effort provider".)

### The Works source contract (already shipping — `get-work-item` → `WorkItemView`)

`Hexalith.Works/src/Hexalith.Works.Contracts/Models/WorkItemView.cs` — returned by the `work` domain `get-work-item` query, documented as the contract `Hexalith.Timesheets` uses to "read its planned-vs-actual effort":

```csharp
public sealed record WorkItemView(
    TenantId TenantId, WorkItemId WorkItemId, bool Found, WorkItemStatus Status,
    decimal? Estimated, decimal? Done, decimal? Remaining, Unit? Unit,
    ParentWorkItemReference? Parent, long LatestAcceptedSourceSequence);
// WorkItemView.NotFound(tenantId, workItemId) => Found=false, all effort null.
```

`Unit` here is `Hexalith.Works.Contracts.ValueObjects.Unit { string Value }`. Map to the read-model `string?` via `view.Unit?.Value`. `Estimated/Done/Remaining/Unit` are all-null-or-all-present together (they come from a single `WorkItemEffort` whose constructor requires a `Unit`). **You read these effort fields here — that is the legitimate payload of this story** (unlike the validator, which is an authority gate and must not read effort — see the fitness-test blocker below).

### Planned-Effort State Mapping (recommended defaults — see Open Questions)

| `WorkItemView` / call condition | `WorkPlannedEffortReadModel` |
|---|---|
| `context.Tenant is null` | `Unavailable()` |
| non-success `QueryResult`, undefined payload, deserialize failure, `view is null` | `Unavailable()` (never throw except `OperationCanceledException`) |
| `view.TenantId` ≠ request tenant | `Unauthorized()` |
| `Found == false` | `NotSupplied()` *(Q-A default; row is already authorized, so an absent estimate is "not supplied", not an authority failure)* |
| `Found && Estimated is null` (or `Unit is null`) | `NotSupplied()` |
| `Found && Estimated is not null && Unit is not null` | `Supplied(Estimated, Done, Remaining, Unit.Value, freshness)` |
| `OperationCanceledException` | propagate (do not convert to a result) |

`freshness` for `Supplied`: build `new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, cursor: LatestAcceptedSourceSequence.ToString(InvariantCulture), asOfUtc: null, detail: null)` so the Works source sequence is recorded as the freshness cursor. **Do not** fabricate a `Stale` result — `WorkItemView` exposes no degraded/rebuilding flag or as-of timestamp, only the monotonic `LatestAcceptedSourceSequence`, so Works-side projection staleness is not positively detectable from the consumer view (see Open Question Q-C; the `Stale(...)` factory stays unused by this adapter).

Do **not** replicate the validator's lifecycle-status write-gate (which denies `Suspended/Cancelled/Rejected/Expired`). This is **read** reporting over already-authorized actuals — a completed or cancelled Work can still carry a planned estimate worth comparing against logged time. Supply the estimate for any `Found` work that has one, regardless of `Status` (Open Question Q-B).

### CRITICAL: Privacy Fitness-Test Blocker (will fail the build if ignored)

`tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` →
`Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state` currently scans **every** `*.cs` under `src/Hexalith.Timesheets.Works` and asserts none contains `.Estimated`, `.Done`, `.Remaining`, `.Unit`, `.OwnEffort`, `.Parent`, `ILogger`, `logger`, `JsonSerializer.Serialize`, `SerializeToElement`, or `SerializeToUtf8Bytes`.

Your new provider **must** contain `view.Estimated`, `view.Done`, `view.Remaining`, `view.Unit` to do its job → this test fails as written. Refactor it (Task 4):

- Scope the `.Estimated`/`.Done`/`.Remaining`/`.Unit` prohibition to the validator file `WorksQueryWorkReferenceValidator.cs` only (the authority gate must not read effort).
- Keep `.OwnEffort` + `.Parent` forbidden **everywhere** in `.Works` (neither adapter consumes Works internal roll-up structure or parent lineage).
- Keep `ILogger`/`logger`/`JsonSerializer.Serialize`/`SerializeToElement`/`SerializeToUtf8Bytes` forbidden **everywhere** in `.Works` (the provider must be log-free and must not serialize Works state outward — it only *deserializes the consumer view inbound* and returns a typed read model).
- Recommended: add a positive assertion that `WorksQueryWorkPlannedEffortProvider` exposes effort only via the typed `WorkPlannedEffortReadModel` return (no logging, no outward serialization).

### Source attribution & no-leak (AC2 — NFR3, FR23)

`SourceModuleName` is hardcoded to `"Works"` by the factories — keep it. Surface planned effort values **only** through the typed `WorkPlannedEffortReadModel`. Never copy, log, persist, or serialize Works `Status`, names, descriptions, ownership, `Parent`, or the raw `QueryResult` payload into Timesheets events, state, projections, contracts, metadata, or logs. Only the stable `WorkId` (already captured on the Time Entry) crosses the boundary inbound.

### Orthogonal freshness (AC1, AC2 — from Epic 4 retro)

Planned-effort source state (`WorkPlannedEffort.SourceFreshness` / `SourceReferenceState`) is **separate** from the report's actual-time projection freshness. An unavailable/stale Works call must **not** degrade the row's actual-time freshness, and vice-versa. The existing test `ActualTimeReportAuthorizationTests.Work_report_preserves_planned_effort_state_separately_from_actual_projection_freshness` locks this — keep it green.

### No unit conversion (AC1 — FR20)

Pass the Works `Unit.Value` string through unchanged. Never convert Works effort into minutes/hours, never collapse it with human/external duration, AI runtime, or token metrics into a single untyped field, and never derive a finance/duration value from it.

### Project Structure Notes

- New production code → `src/Hexalith.Timesheets.Works/` (namespace `Hexalith.Timesheets.Works`). `Hexalith.Timesheets.Works.csproj` already references `.Server` (seam + `TimesheetsRequestContext`), `Hexalith.Works.Contracts` (`WorkItemView`, `Unit`), and `Hexalith.EventStore.Contracts` (`QueryEnvelope`, `QueryResult`) — **no new project references required**.
- New tests → `tests/Hexalith.Timesheets.Works.Tests/` (already references `.Works`, transitively `.Server`/`Works.Contracts`/`EventStore.Contracts`). Baseline: 25 facts/theories today.
- Seam + fail-closed default stay in `src/Hexalith.Timesheets.Server/OperationalReports/` — unchanged.
- Boundary stays intact: contracts infrastructure-free; `.Server` may reference the EventStore SDK but **not** Works; Works isolation lives behind `.Works`. The new code stays in `.Works`, so no `DependencyDirectionTests` change is needed.

### Testing Requirements

- xUnit v3 · Shouldly · NSubstitute (per `Hexalith.Timesheets.Works.Tests.csproj`). Test method names PascalCase. Run projects individually.
- Mirror `WorksQueryTestData` / `WorksReferenceValidationCompositionTests`. Existing seam tests give you ready assertion targets: `ActualTimeReportContractTests` already round-trips `Supplied(160, 40, 120, "minutes", ProjectionFreshnessMetadata.Stale("12"))` and asserts `availability:"Supplied"`, `sourceModuleName:"Works"`; `ActualTimeReportAuthorizationTests` proves per-row authorization + memoization + the unavailable-keeps-null contract; `ActualTimeReportQueryServiceIntegrationTests` wires a `StaticPlannedEffortProvider` returning `Supplied(...)`.
- Sandbox: VSTest is blocked (`SocketException (13): Permission denied`). Build first, then run the built xUnit v3 executable directly, e.g. `./tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests`. Record the reason and exact counts.
- Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` then `... dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` (expect 0 warnings, 0 errors).

### Previous Story Intelligence

- **Story 1.10 (closest precedent)** established the entire pattern you are reusing: the `IWorksQueryChannel` port (built explicitly for 4.8 reuse), the `.Works` project placement, the fail-closed mapping discipline, the composed-service proof, and the `4-8` behavior-note expectation. The Epic-1 retro action item names 4.8 directly: "reuse it (do not re-invent) … Story 4.8 consumes the existing `Hexalith.Timesheets.Works` port rather than adding a parallel client."
- **Story 4.3 (consumer)** pre-described this exact task ("a narrow Works planned-effort adapter abstraction with an unavailable fail-closed default … wrap the `get-work-item` query behind this adapter and keep Works infrastructure out of Timesheets contracts/projections") and its review added `plannedSourceReferenceState` alongside `plannedSourceFreshness` — populate **both** on the read model (the factories already do; preserve that).
- **Epic 4 retro** flagged the freshness-vs-authority coupling bug (4.7) and the "typed units; never convert provider data" rule — both bind this story. It also names 4.8 as the action item closing the Works planned-effort gap.
- **File-List honesty + exact test counts** are now an enforced gate (1.10/1.11 both shipped stale counts and omitted test files and were caught in review). Generate the File List from `git diff`; report real counts. Budget for ≥1 review-found patch — it is the norm.

### Git Intelligence

Recent commits confirm the cadence and current state: `c4a9183 docs(epic-3): complete closure retrospective`, `6606239 feat(story-3.7): Prove Magic-Link No-Disclosure at the HTTP Boundary`, plus submodule-commit chores. Story 1.10 (`feat`) landed the `.Works` adapter + port; this story extends that same project. Use a `feat(story-4.8):` Conventional Commit. The `Hexalith.Works.Contracts` submodule already exposes `WorkItemView` with the effort fields (`WorkItemRollUp.OwnEffort` was exposed additively "so external consumers … can read the planned `Estimated` figure"); re-verify these files exist at dev time before relying on them (the architecture note that "Works has no consumer query" was stale).

### Latest Tech Information

- .NET 10 / C# 14, nullable + implicit usings, file-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix, XML docs on public members, `-warnaserror`.
- Cross-module dispatch is `IWorksQueryChannel.InvokeAsync(QueryEnvelope, CancellationToken) → Task<QueryResult>`, host-bound to the EventStore domain-service query channel (Dapr service invocation to the `work` domain `/query` endpoint). Target Dapr SDK `1.18.4`. No new packages required.
- Use `Ulid.TryParse` semantics for identifiers if you ever parse one; here `WorkId` is passed through as an opaque non-whitespace string (no parsing needed).

### Project Context Reference

Follow `Hexalith.AI.Tools/hexalith-llm-instructions.md` and the root `CLAUDE.md`. Domain-module rules: domain-centric, persist only via Hexalith.EventStore (this story is read-only — no persistence), reuse platform technical modules instead of duplicating. Sibling `project-context.md` files exist for EventStore/Parties/Tenants/Conversations/Projects/FrontComposer; `Hexalith.Works` is a root submodule (its contracts are referenced via `$(HexalithWorksRoot)`). Do not initialize nested submodules.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.8] — ACs, requirements FR17/FR20/FR23/NFR1/NFR3/NFR8/NFR9.
- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.3] — the consuming Work actual-time report; planned-effort ownership deferred to 4.8.
- [Source: src/Hexalith.Timesheets.Server/OperationalReports/IWorkPlannedEffortProvider.cs] · [UnavailableWorkPlannedEffortProvider.cs] · [ActualTimeReportQueryService.cs]
- [Source: src/Hexalith.Timesheets.Contracts/Models/WorkPlannedEffortReadModel.cs] · [ActualTimeReferenceStateMetadata.cs] · [ProjectionFreshnessMetadata.cs] · [ValueObjects/TimesheetsEnums.cs#WorkPlannedEffortAvailability]
- [Source: src/Hexalith.Timesheets.Works/WorksQueryWorkReferenceValidator.cs] · [IWorksQueryChannel.cs] · [WorksReferenceValidationServiceCollectionExtensions.cs] · [Hexalith.Timesheets.Works.csproj]
- [Source: src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs#L57,L71-72] — kernel default registrations.
- [Source: Hexalith.Works/src/Hexalith.Works.Contracts/Models/WorkItemView.cs] · [ValueObjects/WorkItemEffort.cs] · [ValueObjects/Unit.cs] · [Hexalith.Works/src/Hexalith.Works/Queries/GetWorkItemQueryHandler.cs]
- [Source: tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs#Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state] · [DependencyDirectionTests.cs#Contracts_take_no_works_or_eventstore_reference_and_server_takes_no_works_reference]
- [Source: tests/Hexalith.Timesheets.Works.Tests/WorksReferenceValidationCompositionTests.cs] · [WorksQueryTestData.cs]
- [Source: tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs] · [tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs] · [tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs]
- [Source: _bmad-output/implementation-artifacts/1-10-works-reference-validation-adapter-behavior.md] — behavior-note template to mirror.

### Open Questions (recommended defaults applied; confirm with policy owner — a change is a one-line switch arm + matching test)

- **Q-A — `Found == false` mapping.** Default: `NotSupplied()` (the Work row is already authorized; an absent Works estimate is "not supplied", not an authority failure). Alternative: `Unavailable()` if the launch owner wants projection-lag treated as an availability gap.
- **Q-B — lifecycle-status gating.** Default: **no** status gate for reporting (supply the estimate for any `Found` work with one, regardless of `Status`). Reporting reads over existing actuals; it is not a trust-bearing write like the validator.
- **Q-C — `Stale` detection.** Default: never fabricate `Stale`; return `Supplied` with `Fresh` freshness carrying `LatestAcceptedSourceSequence` as the cursor. The consumer view exposes no degraded flag/as-of timestamp. Positive staleness would require a Works-side consumer-view extension (out of scope, needs Works approval). Document as a known limitation (mirrors 1.10 Q3).
- **Q-D — host wiring / launch posture (AC4).** Default: ship the tested adapter + `AddTimesheetsWorksPlannedEffortReporting()` extension but keep `src/Hexalith.Timesheets/Program.cs` on the kernel fail-closed default for v1 (consistent with 1.10 leaving the validator unwired); document planned-vs-actual as "unavailable/post-v1 until the host composes `IWorksQueryChannel` + the extension." Alternative: wire it live for v1 if the launch owner approves composing the EventStore domain-service query channel in the Timesheets host (also requires the `DomainQueryInvokerWorksQueryChannel` host pass-through binding).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Opus 4.8, 1M context)

### Debug Log References

- Build (`dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`): **0 Warnings, 0 Errors**. A first `-m:1` pass surfaced transient pack/IDE0065 noise from the `Hexalith.PolymorphicSerializations` submodule plus the (then-real) missing-using errors in the new tests; after adding `using Hexalith.Works.Contracts.ValueObjects;` to the three new test files, the rebuild is clean 0/0.
- Tests run via the built xUnit v3 executables (VSTest socket is blocked in the sandbox: `SocketException (13): Permission denied`).

### Completion Notes List

- **Task 1–2 (provider).** Added `WorksQueryWorkPlannedEffortProvider` cloning the `WorksQueryWorkReferenceValidator` shape (same `IWorksQueryChannel` injection, hardcoded `work` / `get-work-item` constants, `BuildEnvelope`, `result.Success`/undefined-payload/`Deserialize<WorkItemView>` flow, `catch (OperationCanceledException) throw;` + `CA1031` fail-closed catch, defensive cross-tenant re-check). State mapping per the Dev Notes table: `Supplied` only when `Found` + tenant match + both `Estimated` and `Unit` present; cross-tenant → `Unauthorized`; missing tenant / non-success / undefined payload / deserialize failure / null view → `Unavailable`; `Found` without estimate-or-unit and not-found → `NotSupplied`. Works `Unit.Value` passes through verbatim (no conversion). `Supplied` freshness records `LatestAcceptedSourceSequence` as a `Fresh` cursor; `Stale` is never fabricated (Q-C).
- **Task 3 (DI).** `AddTimesheetsWorksPlannedEffortReporting()` uses `services.Replace(...)` (wins in either call order); kernel default `UnavailableWorkPlannedEffortProvider` left unchanged; host `Program.cs` left on the fail-closed default for v1 (Q-D).
- **Task 4 (fitness test).** Scoped the `.Estimated`/`.Done`/`.Remaining`/`.Unit` prohibition to `WorksQueryWorkReferenceValidator.cs` only; kept logging/`JsonSerializer.Serialize`/`SerializeToElement`/`SerializeToUtf8Bytes`/`.OwnEffort`/`.Parent` forbidden project-wide; added a positive assertion that the provider exposes effort only via its typed `ValueTask<WorkPlannedEffortReadModel>` return. `DependencyDirectionTests` stays green (no Works refs added to `.Server`/`.Contracts`).
- **Task 5 (tests).** 37 new test cases across 3 files (full state matrix, determinism/replay, edge cases, mandatory composed-service proof both with/without the extension, registration-wins both orders); `WorksQueryTestData.FoundView(...)` extended with optional effort params. (27 authored in dev-story; a subsequent QA-automation pass added 10 more — 6 facts + one 4-case theory — covering reference-state metadata on the Supplied/Unauthorized/Unavailable paths, zero-estimate and null Done/Remaining sub-cases, a parametrized rebuilt-sequence cursor, and caller-token forwarding.)
- **Task 6 (behavior note).** Wrote `4-8-works-planned-effort-reporting-adapter-behavior.md`. Verified the existing report metadata catalog already renders `plannedEffortAvailability` via the `WorkPlannedEffortAvailability` enum (so the kernel default renders `Unavailable` until the host composes the adapter) and contains no finance-ownership language — no copy change required.
- **Task 7 (verify).** Build 0/0. Suites: **Works.Tests 76** (39 baseline + 37 new), **ArchitectureTests 28**, **Server.Tests 403**, **Contracts.Tests 86**, **IntegrationTests 75** (0 failed, 3 pre-existing skips) — all green, no regressions. The orthogonal-freshness lock (`ActualTimeReportAuthorizationTests`) stays green.

### File List

**New — production (`src/Hexalith.Timesheets.Works/`):**

- `src/Hexalith.Timesheets.Works/WorksQueryWorkPlannedEffortProvider.cs`
- `src/Hexalith.Timesheets.Works/WorksPlannedEffortReportingServiceCollectionExtensions.cs`

**New — tests (`tests/Hexalith.Timesheets.Works.Tests/`):**

- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderTests.cs`
- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderEdgeCaseTests.cs`
- `tests/Hexalith.Timesheets.Works.Tests/WorksPlannedEffortReportingCompositionTests.cs`

**Modified — tests:**

- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` (privacy fitness-test refactor — Task 4)
- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryTestData.cs` (`FoundView(...)` extended with effort params)

**New — docs / planning:**

- `_bmad-output/implementation-artifacts/4-8-works-planned-effort-reporting-adapter-behavior.md`

**Modified — planning / tracking:**

- `_bmad-output/implementation-artifacts/4-8-implement-works-planned-effort-reporting-adapter.md` (this story file: frontmatter `baseline_commit`, task checkboxes, Dev Agent Record, Status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status → in-progress → review)

### Change Log

| Date | Change |
|---|---|
| 2026-06-22 | Implemented Story 4.8: concrete `WorksQueryWorkPlannedEffortProvider` + `AddTimesheetsWorksPlannedEffortReporting()` DI extension (fail-closed kernel default kept, host unwired for v1 per Q-D); refactored the privacy fitness test to scope effort-token prohibition to the validator file while keeping logging/serialization and `.OwnEffort`/`.Parent` forbidden project-wide; added 37 tests (state matrix, determinism, edge cases, composed-service proof, registration-wins); wrote the launch-policy behavior note. Build `-warnaserror` 0/0; all affected suites green (Works.Tests 76, ArchitectureTests 28, Server.Tests 403, Contracts.Tests 86, IntegrationTests 75). |
| 2026-06-22 | Senior Developer Review (AI) — adversarial review re-built `-warnaserror` (0/0) and re-ran every affected suite from the built xUnit v3 executables (Works.Tests 76, ArchitectureTests 28, Server.Tests 403, Contracts.Tests 86, IntegrationTests 75/3-skip). All four ACs and all seven tasks verified against code + consumed contracts. One MEDIUM finding fixed in-place: the dev record reported stale test counts (27 new / Works.Tests 66) after a QA-automation pass had grown the suite to 37 new / 76 total — counts corrected here. Outcome: **Approved**, Status → done. |

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-06-22 · **Outcome:** ✅ Approved (Status → done)

Adversarial review of the implementation against the story's claims. Every check below was
**executed**, not inferred: full-solution `dotnet build … -warnaserror -m:1` (0 warnings / 0 errors)
and each affected suite run from its built xUnit v3 executable.

### Verification performed (all green)

- **Build:** `Hexalith.Timesheets.slnx -warnaserror` → **0 Warnings, 0 Errors** (the QA test-summary note
  about "18 pre-existing PolymorphicSerializations errors" did **not** reproduce — the clean-room build is 0/0).
- **Suites:** Works.Tests **76**, ArchitectureTests **28**, Server.Tests **403**, Contracts.Tests **86**,
  IntegrationTests **75** (0 failed; 3 pre-existing skips). No regressions.
- **AC1 (concrete, source-attributed):** `Supplied(...)` returns `SourceModuleName == "Works"` with both
  `SourceFreshness` (Fresh, cursor = `LatestAcceptedSourceSequence`) **and** `SourceReferenceState` (Current);
  unit passed through verbatim, no conversion. Verified in code + `…ProviderTests` + composed-service proof.
- **AC2 (fail closed, no leakage):** missing tenant / non-success / undefined payload / deserialize failure /
  null view → `Unavailable`; cross-tenant → `Unauthorized`; not-found / no-estimate → `NotSupplied`;
  `OperationCanceledException` propagates. Privacy fitness test (`DiagnosticsPrivacyTests`) refactored
  correctly — effort tokens forbidden in the validator file only; logging/serialization/`.OwnEffort`/`.Parent`
  forbidden project-wide; positive assertion on the provider's typed return. Green.
- **AC3 (deterministic, freshness-aware, negative-path):** replay-equality test + parametrized
  rebuilt-sequence cursor theory + full negative-path matrix. Confirmed.
- **AC4 (explicit launch policy):** kernel default `UnavailableWorkPlannedEffortProvider` left unchanged;
  `src/Hexalith.Timesheets/Program.cs` contains **no** Works wiring (grep-confirmed), so reports render
  planned effort `Unavailable` until a host composes `IWorksQueryChannel` + the extension. Behavior note
  documents the post-v1 posture and the host-binding recipe.
- **Contracts:** `WorkItemView` field order, `Unit.Value`, `QueryResult.{Success,GetPayload,Failure,FromPayload}`,
  and the `WorkPlannedEffortReadModel` / `ProjectionFreshnessMetadata` / `ActualTimeReferenceStateMetadata`
  factory shapes all match the provider's usage.
- **File List:** every production and test **source** file is correctly listed and matches `git status`.

### Findings

| Sev | Finding | Resolution |
|---|---|---|
| MEDIUM | Stale test counts in Dev Agent Record / Task 5 / Change Log: reported "27 new / Works.Tests 66"; a later QA-automation pass grew the suite to **37 new / 76 total**. The story itself flags exact test counts as an enforced gate. | **Fixed** — counts corrected in Task 5 note, Task 7 verify note, and Change Log. |
| LOW (noted, no change) | AC2 enumerates "disabled" works among fail-closed states, but the implemented Q-B policy supplies estimates regardless of lifecycle `Status` (Cancelled/Suspended/Expired). | Intentional, documented decision ("according to policy") with rationale in Dev Notes + behavior note, fully tested, reversible via a one-line switch arm. **Not auto-changed** — altering it would override a recorded product decision without policy-owner input. Surfaced for awareness. |

No CRITICAL or HIGH findings. No code changes were required to the provider, DI extension, fitness test,
or test suite — the implementation matches its claims.
