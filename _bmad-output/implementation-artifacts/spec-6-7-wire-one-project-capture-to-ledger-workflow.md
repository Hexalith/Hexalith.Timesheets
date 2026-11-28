---
title: 'Wire one Project capture-to-ledger workflow'
type: 'feature'
created: '2026-11-28'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '67eca813fccf7e887db2a56079e0b77d1f09da16'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Timesheets' EventStore processor handles only external contributions, magic links, and finance export receipts; `IApprovedTimeLedgerProjectionReader` defaults to `Unavailable*`, and no release composition binds tenant, Party, Project, activity, approval, evidence-policy, and ledger-reader providers for a Project capture-to-ledger workflow.

**Approach:** Within `Hexalith.Timesheets`, enroll EventStore command receivers for Project capture, submission, approval, and additive approved correction alongside a delivered projection-backed ledger reader and `QueryApprovedTimeLedger` query handler; expose both EventStore SDK routes (`/process`, `/project/v2`, `/query`) and authenticated human HTTP endpoints (`/api/timesheets/time-entries`, `/api/timesheets/approved-ledger`); bind opt-in host composition providers (gateway adapters using `Projects`/`Parties`/`Tenants` Contracts in the non-packed HTTP host plus configured approval-authority and evidence-policy providers) while keeping `AddTimesheetsServerKernel` fail-closed and preserving AppHost topology; verify end-to-end events, lineage, and fail-closed refusals; and document the seven-provider inventory, supported entry points, tested dependency versions, and Story 6.8/Platform prerequisites.

## Boundaries & Constraints

**Always:**
- **Owning composition decision (Option A):** Keep all edits inside `Hexalith.Timesheets`; wire opt-in release composition in the non-packed `Hexalith.Timesheets` host while leaving `AddTimesheetsServerKernel` fail-closed (`DenyAll*`, `DefaultApprovalAuthoritySourceProviders`, `FailClosedDefault`, `Unavailable*`) and documenting Platform/Tenants prerequisites for Story 6.8.
- **Supported entry points decision (Option A):** Expose EventStore SDK receivers (`/process` for capture, submission, approval, and additive approved correction; `/project/v2` for delivered ledger projection; `/query` for `QueryApprovedTimeLedger`) AND authenticated `HumanPolicy` HTTP endpoints under `/api/timesheets/time-entries` and `/api/timesheets/approved-ledger`, keeping `InternalSurfaceGuard` intact.
- **Provider binding decision (Option A):** Reference `Hexalith.Projects.Contracts` (2.0.0), `Hexalith.Parties.Contracts` (4.0.2), and `Hexalith.Tenants.Contracts` (8.1.1) only in `src/Hexalith.Timesheets` (dual Debug project-ref / Release package-ref; published NuGet closure unchanged; update host allowlist in `DependencyDirectionTests.cs`), bind gateway-backed Tenant/Party/Project validators plus explicit host-configured approval-authority (replacing conflicting precedence-20 defaults) and `TimesheetsEvidencePolicyOptions`.
- Keep `Contracts` infrastructure-free, `ReadModels` independent of `Server`/`DomainService`, `Projections` free of loading `Server`, and `Server` + `Projections` + host route catalogs matching every installed command and named projection once.
- Persist writes only via `Hexalith.EventStore` with pure aggregates (`Handle`/`Apply`); store sibling IDs only; exclude secrets, tokens, PII, comments, and payloads from logs and `Program.cs`.
- Keep existing HTTP host, `AppHost`, and `ServiceDefaults` topology (no second host or UI project), one type per `.cs` file, XML docs on public/internal members, and zero warnings under `-warnaserror`.

**Never:**
- Relax reusable kernel defaults or use `LiveDaprFixture` / `AllowOnAnyPort` as release authority.
- Mutate read models from command handlers, copy sibling fields, claim Story 6.8 deployed/restart acceptance, or modify/stage `references/` submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path Project workflow | Authorized tenant, active Party/Project/ActivityType, configured policy, Project approver | Capture -> submit -> approve -> additive approved correction persist owner events; delivered projection populates ledger reader with current effective + superseded original rows and approval/correction lineage | N/A |
| Cross-tenant write or read | Different tenant or cross-tenant row lineage | Fails closed before protected data or accepted write; zero events persisted | Opaque denial |
| Unavailable required provider | Any of the 7 required providers unavailable/unconfigured | Fails closed before protected data or accepted write; zero events persisted | Opaque refusal |
| Unconfigured kernel | `AddTimesheetsServerKernel()` alone | Writes and ledger reads stay denied/unavailable | `DenyAll*` / `Unavailable*` |
| Route discovery | Isolated `Server`, isolated `Projections`, composed `Server`+`Projections`, host | All enrolled commands and projections match once with valid schemas and receivers | Non-zero exit / exception |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` (`L17-74`) & `MagicLinks/MagicLinkEventStoreDomainProcessor.cs` (`L29-610`) -- Enroll capture, submission, approval, and additive approved correction commit commands, adapters, and processor handlers.
- `src/Hexalith.Timesheets.Server/TimeEntries/{TimeEntryCommandService,TimeEntrySubmissionCommandService,TimeEntryApprovalCommandService,TimeEntryCorrectionCommandService,TimeEntryState}.cs` -- Existing guarded domain services and state fold to invoke from command receivers.
- `src/Hexalith.Timesheets.Server/ApprovedTimeLedger/{ApprovedTimeLedgerQueryService,IApprovedTimeLedgerProjectionReader}.cs` -- Existing fail-closed ledger query service and reader port; add projection-backed `IApprovedTimeLedgerProjectionReader` and `IDomainQueryHandler` for `QueryApprovedTimeLedger`.
- `src/Hexalith.Timesheets.Server/ApprovalAuthority/TimesheetsApprovalAuthorityResolver.cs` (`L118-173`) -- Precedence-20 tie rule: host Project approver binding must replace/exclude conflicting precedence-20 default providers to avoid `AmbiguousAuthority`.
- `src/Hexalith.Timesheets.ReadModels/` & `src/Hexalith.Timesheets.Projections/{ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs,ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs,TimesheetsProjectionRouteCatalogDeclarations.cs}` -- Add shared ledger read-model address/epoch helpers, stored TimeEntry event wrappers, delivered `ApprovedTimeLedgerProjectionHandler` (`IAsyncDomainSharedProjectionEpochHandler` / `IReadModelStore`), and projection catalog entry without loading `Server`.
- `src/Hexalith.Timesheets/{Hexalith.Timesheets.csproj,Program.cs,Runtime/,Endpoints/}` -- Add conditional Debug/Release sibling contract refs (`Projects`, `Parties`, `Tenants`), gateway validators, configured approval/policy/ledger opt-in composition, and `HumanPolicy` endpoints under `/api/timesheets/time-entries` and `/api/timesheets/approved-ledger`; keep `Program.cs` free of banned lexical terms (`TenantId`, `PartyId`, `ProjectId`, `WorkId`, `TimeEntryId`, `Token`, `Secret`).
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` (`L28-154`) -- Keep fail-closed defaults intact (`FailClosedDefaultsTests.cs`).
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs` (`L176-210`) -- Allow `Hexalith.{Projects,Parties,Tenants}.Contracts` on the non-packed HTTP host only (dual-mode condition enforced).
- `tools/package-consumers/`, `scripts/validate-consumer-package-references.py`, `tests/`, `docs/` -- Update discovery/package-consumer assertions, add end-to-end + negative tests, and document 7-provider inventory, supported entry points, tested versions, and Story 6.8 prerequisites (`launch-readiness.md` remains `FAIL`).
- **Keep unchanged:** `AppHost`, `ServiceDefaults`, `Contracts` infrastructure neutrality, `tools/release-packages.json`, `.github/workflows/`, `references/` gitlinks.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.ReadModels/` & `src/Hexalith.Timesheets.Projections/` -- Add shared ledger read-model address/epoch helpers, stored TimeEntry event wrappers, delivered `ApprovedTimeLedgerProjectionHandler`, and `TimesheetsProjectionRouteCatalogDeclarations` entry -- Backs ledger reads by delivered projections while keeping `Projections` isolated.
- [x] `src/Hexalith.Timesheets.Server/` -- Enroll capture, submission, approval, and additive approved correction receivers in processor + `TimesheetsRouteCatalogDeclarations`, add projection-backed `IApprovedTimeLedgerProjectionReader` + `QueryApprovedTimeLedger` `IDomainQueryHandler` + command submission orchestration, keep kernel defaults fail-closed -- Delivers command receivers and ledger reader.
- [x] `src/Hexalith.Timesheets/{Hexalith.Timesheets.csproj,Runtime/,Endpoints/}` -- Add conditional sibling Contracts refs, bind opt-in host composition (Tenant/Party/Project gateway adapters, activity catalog, approval authority, evidence policy, ledger reader), and map `HumanPolicy` endpoints under `/api/timesheets/time-entries` and `/api/timesheets/approved-ledger` -- Completes host composition without altering AppHost topology.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs`, `tools/package-consumers/`, `scripts/validate-consumer-package-references.py`, & `tests/` -- Update host sibling-contract allowlist, discovery/consumer assertions, and add end-to-end + negative tests covering the I/O Matrix -- Verifies events, lineage, and fail-closed gates.
- [x] `docs/` -- Document the 7-provider inventory, supported entry points, tested dependency versions, and Story 6.8/Platform prerequisites -- Satisfies AC1/AC3 documentation.

**Acceptance Criteria:**
- Given one tenant's 7-provider host composition, when capture -> submit -> approve -> additive approved correction execute via supported entry points, then EventStore stores ordered owner events and the projection-backed ledger reader returns effective current and superseded original rows with approval and correction lineage.
- Given a different tenant or any unavailable required provider, when workflow commands or ledger reads execute, then the host fails closed with zero protected disclosure and zero accepted writes.
- Given isolated `Server`, isolated `Projections`, composed `Server`+`Projections`, and the composed host, when route discovery and package validation run, then all enrolled commands and projections match once, kernel defaults stay fail-closed, AppHost topology is unchanged, and entry points + tested versions are documented.

## Implementation Notes

- The host uses a dedicated HTTPS authority gateway for sibling queries and the existing EventStore workload transport for owner submission and status.
- The ledger uses a tenant-selected SDK generation with an actor-, filter-, and snapshot-bound cursor. The workflow requires a complete Activity Type catalog policy snapshot.
- The [host setup and entry points](../../docs/project-capture-to-ledger.md) name the seven providers, dependency versions, and deployed qualification prerequisites.

## Spec Change Log

## Review Triage Log

| Layer | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| Blind | Physical generation read admits a fenced ledger | high | patch | The epoch can be offboarded even when the generation-zero key remains reachable. Check the selected generation's availability, degradation and freshness. |
| Blind | A terminal refusal consumes the message identity | medium | defer | The SDK records zero-event no-ops as terminal. A new attempt after a known refusal needs a fresh identity. Re-admitting a terminal command is an EventStore retry policy decision. |
| Blind | Unobserved commit looks like known refusal | medium | patch | The status observer can time out after dispatch. Give an indeterminate result an opaque status that preserves same-key recovery. |
| Blind | SDK query ignores its signed target | high | patch | The handler validates the caller but an omitted Project filter lets the host choose its configured Project regardless of the aggregate binding. Match the effective filter to the verified target. |
| Blind | Unknown Tenant freshness permits membership admission | high | patch | The Gateway classifies the real `get-tenant` result as computed. Use the scoped `get-user-tenants` membership query with complete paging and Current projection-backed evidence. |
| Blind | Omitted Project fields deserialize as positive authority | high | patch | Active and Complete are ordinal zero and SDK deserialization accepts omitted constructor fields. Require explicit lifecycle and admission fields. |
| Blind | Legacy prefix stalls ordinary delivery | medium | defer | The local fold requires sequence one. A historical rejection prefix needs an authoritative opening ledger generation before live delivery. Qualification of historical cutover belongs to Story 6.8. |
| Blind | Policy change can strand compact folds | medium | patch | The shared catch-up path cannot rehydrate a prefix whose comments were already dropped. Keep the bounded workflow's comment publication disabled. |
| Blind | Shared rebuild candidate has a tenant size bound | medium | defer | The SDK default is 1 MiB. The one-Project story uses a bounded local fixture. Story 6.8 must qualify the intended tenant volume and Story 6.9 owns representative capacity measurement. |
| Blind | Host-wide comment flag can affect other tenants | high | patch | The configured options reach all Time Entry projections. Suppress publication in this host composition while retaining evidence policy gates. |
| Blind | Refusals do not exercise signed Project command scope or review authority | high | patch | The positive actor fixture injects verified origin. Test actor and tenant mismatch at the SDK boundary and exercise a configured tenant member outside the approver list. |
| Edge cases | Degraded, unavailable or offboarded ledger permits writes | high | patch | Checking the physical key bypasses the SDK's selected epoch and can leave persisted events even if ledger publication fails. |
| Edge cases | First recognized event has an unrecognized legacy prefix | medium | defer | Full history rebuild preserves skipped source lineage. The first recognized suffix lacks that prefix during live cutover. Prove opening generation preparation in Story 6.8. |
| Edge cases | Comment enablement repeatedly fails catch-up | medium | patch | The reused suffix fold asks for replay after a policy version change. This composition will publish ledger evidence with comments suppressed. |
| Edge cases | Candidate limit blocks tenant rebuild | medium | defer | Accumulation holds all owner folds under the SDK's bytes limit. Carry the volume limit into deployment qualification. |
| Verification | Unavailable reader substitution bypasses the real store path | high | patch | Exercise a fenced or unavailable delivery state with the production reader registered and assert unchanged owner events. |
| Verification | Configured non-approver is untested | high | patch | The original approval refusal only removes all approved Parties from configuration. Verify denial for an active member in the configured tenant. |
| Verification | New cursor path is untested | medium | patch | The new reader emits the continuation but the positive fixture requested both rows at once. Exercise the continuation with the composed host. |
| Verification | Same-key retry is untested | medium | patch | Each successful command runs once in the original fixture. Repeat capture and check the owner history and response. |
| Verification | Rebuilt rows are never disclosed in the fixture | medium | patch | A rebuilt snapshot was used only behind denied reads. Inspect the promoted generation through the deployed route shape in the local fixture. |
| Verification | Actual SDK offboarding bypasses physical key readiness | high | patch | The SDK writer refuses offboarded delivery. Require an enrolled, readable ledger epoch before acceptance. |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false && DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` -- expected: exit 0, zero warnings
- All 6 built Debug xUnit v3 executables under `tests/<Project>/bin/Debug/net11.0/<Project>` -- expected: exit 0, zero failures
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -m:1 /nr:false && DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -p:UseHexalithProjectReferences=false -warnaserror -m:1 /nr:false` -- expected: exit 0, zero warnings
- `python3 -m unittest discover -s scripts/tests -v` and packed consumer validation (`pack-release-packages.py`, `validate-nuget-packages.py`, `validate-consumer-package-references.py`) -- expected: exit 0

**Results:**

- Debug/source and Release/package solution builds passed with zero warnings and errors under .NET SDK 11.0.100.
- The six Debug executables passed 3,259 tests with zero failures and five expected Integration skips. Architecture 95, Contracts 109, Server 1,052, Projections 802, Works 76, and Integration 1,125 passed.
- All 22 Project workflow tests passed in Debug and Release after review. The in-process EventStore actor stored the four owner events. SDK delivery and a local rebuild returned current and superseded values with their approval and correction lineage. The workflow also verified signed paging, same-key recovery after an unconfirmed status, and zero stored events or disclosed rows on the refusal cases.
- Configured and composed host catalog checks passed (five cases). Standalone projection discovery passed. All six package archives and all seven NuGet-only consumers passed at `0.0.0-ci-test`; the composed inventory contains ten commands, five projections and one query.
- All 34 package-tooling tests, 273 isolated-fixture tests and 63 Release AppHost configuration/model checks passed.
- The pre-edit `run-with-temp env Configuration=Debug AspireUseCliBundle=true UseHexalithProjectReferences=true aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` returned exit 0. `aspire describe` reported both HTTP projects healthy and the EventStore Dapr sidecar finished with exit code 1. The exact Timesheets AppHost was stopped successfully. Deployed qualification remains in Story 6.8.
