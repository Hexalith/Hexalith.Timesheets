---
title: 'Support independent and composed route discovery'
type: 'feature'
created: '2026-11-28'
status: 'done'
baseline_commit: 'e28c715ce90da75e86e55f1b0eefb1c955cbfc8b'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Hexalith.Timesheets.Projections` holds the sole `[assembly: RouteCatalogDeclarationSource]` and reflectively loads `Hexalith.Timesheets.Server` in `TimesheetsProjectionRouteCatalogDeclarations` to merge Server commands and operations. A Projections-only NuGet consumer fails with `FileNotFoundException`, `Server` lacks a discovery attribute, and the package-validation lane lacks a composed `Server` + `Projections` consumer.

**Approach:** Give `Projections` (4 named projections, zero `Server` loading) and `Server` (6 commands) independent `[assembly: RouteCatalogDeclarationSource]` declarations, place single-assembly composed discovery on the `Hexalith.Timesheets` HTTP host, point `AppHost` split-owner preparation to `"Hexalith.Timesheets.dll"`, preserve complete owner admission, and validate both isolated and composed packed consumers in the package-validation lane.

## Boundaries & Constraints

**Always:**
- Keep `Contracts` infrastructure-free, `ReadModels` independent of `Server`/`DomainService`, and `Projections` free of any compile-time or runtime load of `Hexalith.Timesheets.Server`.
- Preserve all 26 `TypeForwardedTo` forwards in `Server`, public CLR identities, stored JSON shapes, and `TenantActivityTypeCatalogReadModelAddress`.
- Enroll `[assembly: RouteCatalogDeclarationSource]` on `Server` (6 commands) and `Projections` (4 named projections) so multi-assembly and composed-host discovery return 10 unique routes, 10 operations, and 10 receiver methods (6 `process`, 4 `project/v2`), failing closed on missing or duplicate entries.
- **Migration & versioning decision (SemVer major `3.0.0`):** Narrow `TimesheetsProjectionRouteCatalogDeclarations` (`Routes`, `Operations`, `ReceiverMethods`) to the 4 named projections and keep `Commands` returning an empty list (`[]`) with migration XML docs (no `[Obsolete]`), preserving EventStore Aspire's reflection contract without CS0618 errors under `-warnaserror`; document the v3.0.0 breaking migration from combined v2.x members to independent `[Server, Projections]` or host-composed discovery.
- **AppHost authorization:** Update only the declaration DLL basename in `src/Hexalith.Timesheets.AppHost/Program.cs:260` from `"Hexalith.Timesheets.Projections.dll"` to `"Hexalith.Timesheets.dll"` (zero topology, resource, or package changes).
- Keep one C# type per `.cs` file, XML docs on public/internal members, and zero warnings under `-warnaserror`.

**Never:**
- Load `Hexalith.Timesheets.Server` via reflection or `AssemblyLoadContext` from `Hexalith.Timesheets.Projections`.
- Add packages to `tools/release-packages.json`, add a host or UI project, alter AppHost topology, touch `ServiceDefaults` or `.github/workflows/`, or weaken fail-closed defaults or package provenance/audit checks.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Projections-only consumer | Packed `Hexalith.Timesheets.Projections` only | Enumerates 4 projections (`Projections`, `Routes`, `Operations`, `ReceiverMethods`, `CompiledRouteCatalogDeclarations`) and empty `Commands`; `Server` never loaded | Non-zero exit if `Server` loads or count/handlers mismatch |
| Server-only consumer | Packed `Hexalith.Timesheets.Server` only | Enumerates 6 commands including Epic 6 additions via `[RouteCatalogDeclarationSource]` and `ReceiverMethods` (`process`) | Non-zero exit if count != 6 or commands missing |
| Composed consumer | Packed `Server` + `Projections` together | Discovers 10 unique routes, 10 operations, 10 receiver methods (6 `process`, 4 `project/v2`) matching all installed commands and `IAsyncDomainProjectionHandler`s | Non-zero exit on missing, extra, or duplicate declaration |
| Composed HTTP host | `Hexalith.Timesheets` (`[Server, Projections]` + `TimesheetsHostRouteCatalogDeclarations`) | 10-route observation, `DevelopmentDomainRouteCatalog.Targets`, and single-assembly composed admission succeed without duplicates | `RouteCatalogUnavailableException` / `RouteCatalogValidationException` |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Projections/TimesheetsProjectionRouteCatalogDeclarations.cs` (`L14-68`) -- Remove `ReadServerOwner<T>` and `AssemblyLoadContext`/`Reflection` Server load; return projections-only `Routes`, `Operations`, `ReceiverMethods`, and empty `Commands` (`[]`) with v3.0.0 migration XML docs.
- `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` (`L12-72`) & `TimesheetsRouteCatalogAdapters.cs` (`L3-13`) -- Add `[assembly: RouteCatalogDeclarationSource(typeof(TimesheetsRouteCatalogDeclarations))]`; fix stale "four" XML doc.
- `src/Hexalith.Timesheets/Runtime/TimesheetsHostRouteCatalogDeclarations.cs` (new) -- Single-assembly composed `[assembly: RouteCatalogDeclarationSource]` aggregating `Server` + `Projections` with duplicate-route guards.
- `src/Hexalith.Timesheets/Program.cs` (`L38-44`) & `src/Hexalith.Timesheets.AppHost/Program.cs` (`L259-261`) -- Keep `[Server, Projections]` host catalog wiring; change `AppHost/Program.cs:260` declaration DLL from `"Hexalith.Timesheets.Projections.dll"` to `"Hexalith.Timesheets.dll"`.
- `scripts/validate-consumer-package-references.py`, `tools/package-consumers/{Projections,Server}.cs`, `scripts/tests/test_product_packages.py` -- Expand isolated `Projections` (4 projections, empty `Commands`, zero `Server` load) & `Server` (6 commands, fix stale `!= 5`) consumers; add composed `Server` + `Projections` consumer and Python unit tests.
- `tests/Hexalith.Timesheets.{Projections,Server}.Tests/` & `tests/Hexalith.Timesheets.IntegrationTests/{MagicLinkConfirmationHttpBoundaryTests.RouteCatalog.cs,MagicLinkConfirmationHttpBoundaryTests.Retry.cs,HttpRouteCatalogFixture.cs}` -- Standalone package discovery tests + composed host admission & duplicate/missing checks.
- `docs/ci-cd.md` -- Record v3.0.0 migration/versioning guidance and composed consumer validation.
- **Keep unchanged:** `Contracts`, `ReadModels`, `Server/Compatibility/ReadModelTypeForwards.cs`, `ServiceDefaults`, `tools/release-packages.json`, `.github/workflows/`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Projections/TimesheetsProjectionRouteCatalogDeclarations.cs` -- Drop `ReadServerOwner<T>`, return projections-only `Routes`, `Operations`, `ReceiverMethods`, and empty `Commands` (`[]`) with v3.0.0 migration XML docs -- Enables standalone `Projections` discovery.
- [x] `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs` & `TimesheetsRouteCatalogAdapters.cs` -- Add `[assembly: RouteCatalogDeclarationSource]` on `Server` and fix adapter XML doc -- Enables independent `Server` discovery.
- [x] `src/Hexalith.Timesheets/Runtime/TimesheetsHostRouteCatalogDeclarations.cs` & `src/Hexalith.Timesheets.AppHost/Program.cs` -- Add composed host declaration source with duplicate guards and point `AppHost/Program.cs:260` to `"Hexalith.Timesheets.dll"` -- Preserves composed host & Aspire admission.
- [x] `scripts/validate-consumer-package-references.py`, `tools/package-consumers/{Projections,Server}.cs`, & `scripts/tests/test_product_packages.py` -- Validate standalone `Projections` (no `Server` load), standalone `Server` (6 commands), and composed `Server` + `Projections` (10 unique routes/operations/receiver methods) plus unit tests -- Covers package-validation lane ACs.
- [x] `tests/Hexalith.Timesheets.{Projections,Server}.Tests/` & `tests/Hexalith.Timesheets.IntegrationTests/` -- Add standalone package discovery tests and update route-catalog/retry/fixture tests for dual-assembly + composed-host discovery -- Locks AD-8 behavior.
- [x] `docs/ci-cd.md` -- Document v3.0.0 migration/versioning for `TimesheetsProjectionRouteCatalogDeclarations` and composed validation -- Captures upgrade contract.

**Acceptance Criteria:**
- Given packed candidate packages in `scripts/validate-consumer-package-references.py`, when the isolated `Projections` consumer runs without `Server`, then it enumerates all 4 named projections (`Routes`, `Operations`, `ReceiverMethods`, `CompiledRouteCatalogDeclarations`) with empty `Commands` and zero `Server` load, and v3.0.0 migration/versioning for combined members and attribute ownership is documented.
- Given a second clean NuGet consumer installing candidate `Server` + `Projections`, when it discovers routes, operations, and receiver methods, then all 6 installed commands and 4 named projections match exactly once and any missing or duplicate declaration fails.
- Given the composed `Hexalith.Timesheets` host, when Development route-catalog bootstrap, `HttpRouteCatalogFixture`, and composed host checks run, then complete 10-route owner admission is preserved without duplicates.

## Implementation Notes

- Projections and Server now provide their own declarations. The HTTP host composes their inventories for single-assembly discovery, and the existing Development registration continues to supply both owner assemblies.
- The package lane executes the checked-in consumer sources with exact local candidates. The composed consumer verifies command payloads, projection handlers, operation IDs, and receiver bindings.
- The workspace advanced independently to `abb272b258296fa5fcfbf9e1f585e34a96f02eb4` during implementation. Final verification includes that EventStore package-compatibility commit.

## Spec Change Log

## Review Triage Log

| Layer | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| Blind | AppHost split-owner load is outside configuration-only checks | medium | defer | The probe exits before owner preparation. The private runtime fixture covers this path under Story 3.6. |
| Blind | Configured host test supplies its own owner array | medium | patch | The bootstrap callback could omit an owner while the original test still reads both assemblies. |
| Blind | Direct host Operations access lacks the completeness guard | medium | patch | The SDK can return matching operations and routes for the discovered subset when an owner attribute is absent. |
| Blind | Packed composed consumer admits swapped handler bindings | medium | patch | Independent comparisons of handler names and route IDs preserve both sets after a swap. |
| Blind | Packed composed consumer admits an incorrect operation schema | medium | patch | A valid operation digest can still describe a different payload unless the consumer compares its expected schema. |
| Blind | Final assembly census can miss a transient Server load | low | patch | An assembly loaded and released by a collectible context can disappear before the final census. |
| Blind | Migration guide omits coordinated package upgrade | medium | patch | A mixed v2/v3 discovery pair can hide the commands or declare them twice. |
| Blind | Packed README lacks a migration link | low | patch | The README is the packaged entry point and the guidance lives in the repository documentation. |
| Blind | Review diff includes the independent 6.5 commit | false | reject | The full diff is anchored to the workflow's recorded starting revision. Git attributes both hunks to the separate compatibility commit, and neither is part of the story's working changes. |
| Edge case | Composed consumer compares handler sets and operation IDs independently | medium | patch | Swapping handlers or a rehashed schema leaves those sets unchanged. |
| Edge case | Configured bootstrap could omit an owner | medium | patch | The original test stops before executing the registered observer. |
| Verification | Normal checks skip real split-owner preparation | medium | defer | The private Dapr runner is the supported end-to-end lane, and occupied Dapr inventory prevented its baseline from starting. |
| Verification | Tests do not verify the configured Program's owner enrollment | medium | patch | The fixture replaces the observer, and the configuration probe constructs its own owner list. |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false && DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` -- exit 0, zero warnings
- Run all 6 built Debug xUnit v3 executables under `tests/<Project>/bin/Debug/net11.0/<Project>` -- exit 0, zero failures
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -m:1 /nr:false && DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -p:UseHexalithProjectReferences=false -warnaserror -m:1 /nr:false` -- exit 0, zero warnings
- `run-with-temp bash -ec 'python3 scripts/pack-release-packages.py "$TMPDIR/nupkgs" 0.0.0-ci-test && python3 scripts/validate-nuget-packages.py "$TMPDIR/nupkgs" && python3 scripts/validate-consumer-package-references.py "$TMPDIR/nupkgs"'` -- exit 0 (six isolated consumers and the composed consumer passed)
- `python3 -m unittest discover -s scripts/tests -v` & `python3 scripts/check_magic_link_apphost_guards.py --configuration-only --configuration Release` -- exit 0

**Results:**

- Final Debug/source and Release/package solution restore and build passed with zero warnings and errors. The Debug build used a disposable checkout after the shared workspace produced mismatched `Hexalith.Commons.UniqueIds` assembly versions.
- Package archives, all six isolated consumers, and the composed consumer passed at `0.0.0-ci-test`. The Projections consumer observed zero Server loads. The composed consumer matched all ten route bindings and operation schema digests, with six `process` and four `project/v2` receivers.
- The focused Projections and Server discovery cases passed. The configured HTTP bootstrap, composed discovery, missing-owner, and wrong-schema checks passed in final Debug and Release builds.
- All 33 Python tests and the Release configuration-only AppHost guards passed. A disposable SDK probe also executed `PrepareEventStoreDevelopmentOwnerAsync` using the checked-in AppHost artifact names. It verified 10 routes, 10 operations, 6 policies, and 10 receiver methods before resource start.
- Full Debug Architecture, Contracts, Server, Works, and Projections suites completed with 95, 109, 1,052, 76, and 802 passing tests respectively. Projections test assembly bytes matched after the final build. The Server rerun had one existing 100 ms timing failure, and its one focused retry passed all three cases.
- The final full Debug Integration suite passed 1,103 tests with five expected skips and zero failures. Across all six suites on the verified Debug snapshot, 3,237 tests passed.
- A separate recording fix advanced the checkout to `7069f3d7b441c26e86793da3ffff7dc8d0334cc6`. After restoring the non-overlapping story changes, the Release solution build, all seven packed consumers, and 118 focused route discovery and recording tests passed on that revision.
- The live AppHost baseline command `run-with-temp python3 scripts/run_isolated_magic_link_dapr.py --public-port 18086` exited 1 because a Dapr application was already running or inventory was unsafe. It started no AppHost. Private runtime and deployed admission remain in Story 3.6.
