---
title: 'Compose Timesheets EventStore route catalog from owner declarations and actual receivers'
type: 'feature'
created: '2026-11-02'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '9b496ecda022adb51b8faac0caab5102f96049bf'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="The user requested completion and push of this Story 3.6 increment">

## Intent

**Problem:** Timesheets declares four internal EventStore commands, but the host does not enroll their assembly in the supported route-catalog bootstrap. Its in-process HTTP fixture invents projection declarations, so current admission proof does not certify the complete owner inventory.

**Approach:** Declare every installed named projection with an operation schema, enroll both owner assemblies through the EventStore SDK, and make the composed-host tests use the SDK's actual receiver and authorization observations. Verify accepted commands and projections, forged and undeclared requests, and keyed retries against persisted effects.

## Boundaries & Constraints

**Always:** Keep the four exact command payloads and all installed named projection handlers in one finite owner inventory. Retain server-side workload authorization, canonical POST receiver policies, generation binding, default-deny admission, and EventStore/Dapr state ownership. Treat local composed-host proof separately from deployed qualification; keep Story 3.6 in progress and launch FAIL until deployed gates pass.

**Never:** Add direct Redis or Dapr state access for Timesheets domain state, fabricate receiver or authorization observations, introduce permissive admission, add topology, or change AppHost/ServiceDefaults. Do not interpret the SDK's Development bootstrap as production authority without an independently bound deployed catalog.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Valid | Declared command or named projection, signed workload, committed generation | Canonical receiver executes and EventStore-backed end state reflects one effect | None |
| Forged | Invalid workload or generation binding | Receiver refuses before state change | No permissive fallback |
| Undeclared | Unknown command or projection, or missing owner declaration | Catalog preparation or receiver refuses | No state change |
| Keyed retry | Repeated accepted command with same semantic key | One durable effect and reconciled response | Conflicting intent refuses |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` — four existing internal command routes, operation schemas and adapters; preserve exact inventory and payload semantics.
- `src/Hexalith.Timesheets.Projections/` — four installed named handlers: token-hash index, capability, tenant Activity Type catalog and Time Entry evidence. Add a separate assembly declaration source with `Routes` and `Operations` tied to those exact types and projection names.
- `src/Hexalith.Timesheets/Program.cs` — register `AddEventStoreDevelopmentDomainCatalog` with Server and Projections assemblies. `UseEventStoreDomainService` already installs the SDK receiver fence; do not install it twice.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DevelopmentDomainRouteCatalog.cs` — supported owner enrollment and `Targets` observation compare installed payloads/handlers and mapped `/process` and `/project/v2` policies.
- `tests/Hexalith.Timesheets.IntegrationTests/HttpRouteCatalogFixture.cs` — currently synthesizes projections; use compiled owner declarations and SDK `Targets` while retaining its in-memory authority transport and read-model end-state checks.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.RouteCatalog.cs` and `.Retry.cs` — extend composed-host valid/forged/undeclared/keyed-retry proofs.
- `_bmad-output/implementation-artifacts/deferred-work.md`, `docs/launch-readiness.md` — record exactly which local composition gaps closed and which deployed gates remain.

## Tasks & Acceptance

**Execution:**
- [x] Add exact projection declaration source and schema contracts beside installed handlers; reject incomplete or mismatched inventory through the SDK.
- [x] Enroll Server and Projections owners from `Program.cs` through the supported SDK registration, inert without the private Development bootstrap configuration.
- [x] Replace fixture-created projection declarations and target synthesis with compiled owner inventory and SDK receiver/authorization observation.
- [x] Extend composed-host tests for valid, forged, undeclared and keyed-retry paths, asserting persisted read models and command effects.
- [x] Run focused and solution build/test gates and update deferred-work and launch records with observed outcomes.

**Acceptance Criteria:**
- Given all four command payloads and four named handlers are installed, when the SDK observes the mapped host, then it certifies exactly their owner routes and protected canonical receivers.
- Given a missing or mismatched owner declaration or forged workload/generation, when preparation or dispatch runs, then it refuses without a Timesheets state change.
- Given the same keyed command is retried, when the composed host reconciles it, then exactly one durable effect exists and conflicting intent cannot reuse that key.
- Given local verification passes, when the records are updated, then deployed acceptance remains unclaimed and launch remains FAIL.

## Implementation Notes

The Projections assembly is the sole compiled declaration source for the four Server command policies and four exact named projections, satisfying the SDK's single-assembly Aspire owner contract. It reads the installed Server policies through the selected assembly load context, keeping the existing Projections dependency boundary. The HTTP host enrolls both assemblies through the SDK Development registration, which is inert without private bootstrap configuration; its normal EventStore/Dapr state path and default-deny authorization remain unchanged. The HTTP fixture reads the compiled source, asks the SDK to inspect installed handlers and mapped protected receivers, and commits operation schemas in a schema-v3 catalog. Tests reject missing ownership or changed schema at activation before any read-model write and inspect configured host enrollment before its private worker starts.

Matching Debug/source restore and post-review warnings-as-errors solution build passed with zero warnings/errors. Focused catalog HTTP checks passed 25/25; six focused protected command, keyed retry/concurrency, persisted rebuild and forged-credential checks passed. Post-review Architecture, Contracts, Server, Projections and Works executables passed 118/118, 108/108, 746/746, 787/787 and 76/76; post-edit LaunchReadinessTests passed 16/16. A first full Integration attempt was stopped after about 15 minutes without a summary. A second was interrupted by SIGINT after 1,229.135 seconds; xUnit reported 782 total, one cancellation failure in the 9,998-owner rebuild case, five expected skips, zero errors and zero not run (exit 58). Neither attempt grants a broad Integration pass. Deferred-work and launch records retain deployed acceptance and launch FAIL.

## Spec Change Log

- Review found that Aspire split-owner preparation selects one declaration assembly and requires its command inventory. Combined the existing command policies and projection routes in the Projections declaration source while preserving the Server adapter source; this avoids a locally valid catalog that Aspire cannot prepare. Kept the SDK target observation and fail-closed command semantics.
- Review found that schema-v2 fixture activation discarded operation schemas. Updated the fixture to commit schema v3 and added activation/schema and configured host enrollment checks; this avoids published schema drift passing local checks.

## Review Triage Log

| Reviewer finding | Verdict / route | Evidence |
| --- | --- | --- |
| Blind: separate command and projection owner sources cannot compose as one Aspire owner | medium / patch | `DevelopmentRouteCatalogAspireExtensions.BuildCatalog` reads one `owner.Contracts` assembly; now one Projections source contains all eight routes. |
| Blind: projection source lacks required `Commands` property | medium / patch | Aspire's owner reader requires it; the source now exposes the four installed Server command policies. |
| Blind: Program registration has no regression test | medium / patch | The configured-host test now inspects the SDK bootstrap worker and host observer registration before startup. Private bootstrap capture remains a deployed gate. |
| Blind: fixture discards operation schemas | medium / patch | Fixture now commits schema v3 with all eight operation digests and observes the same schemas. |
| Blind: fixture omits finite receiver surface and executable hashes | false / reject | `DevelopmentDomainRouteCatalog.Targets` observes actual protected canonical endpoints for this local claim; finite sidecar and loaded-image qualification remain explicitly outside local fixture proof. The SDK's private bootstrap captures them when configured. |
| Blind: missing and mismatched declaration tests stop at pure `Targets` | medium / patch | New fixture activation cases now refuse missing ownership and changed schema before state writes. |
| Blind: stable route IDs lack literal assertion | low / patch | The composed inventory test now pins all eight exact IDs. |
| Blind: broad built suites were not completed | medium / patch | All six built executables were invoked individually after the review correction. Five passed; the broad Integration run was interrupted in its 9,998-owner case and is recorded as incomplete, with separate focused catalog and retry passes. |
| Blind: local Release/package graph was not built | false / reject | Repository guidance requires matching Debug/source local restore and build; Release/package is a separate CI graph and mixing them is explicitly prohibited. |
| Edge: duplicate named handler could execute twice | false / reject | SDK `DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed` rejects duplicate domain/projection routes before named dispatch. |
| Edge: schema drift passes the v2 fixture | medium / patch | Schema-v3 activation and the changed-schema refusal case now bind operation digests. |
| Verification: removing host enrollment would leave tests green | medium / patch | Configured-host prestart test now fails without the SDK worker and observer registrations. |
| Verification: projection schemas are read but not checked | medium / patch | Schema-v3 catalog and operation identity checks reject changed shapes during activation. |

## Verification

**Commands:**
- `dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore matching source graph.
- `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings and errors.
- Run focused composed-host Integration cases and all six built xUnit v3 executables individually — affected cases pass, with any bounded incomplete run recorded separately.
- `git diff --check` — no whitespace errors.
