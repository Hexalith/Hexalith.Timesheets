# Hexalith Timesheets

Hexalith Timesheets is a domain module for trusted time capture, approval, confirmation, reporting, and finance export.

## Local Codex Hooks

For BMAD Loop orchestration with Codex, install `bmad-loop` on your machine, then run this command from each checkout root:

```bash
bmad-loop init --cli codex --no-skills
```

This generates `.codex/hooks.json` with the absolute path to your installed relay. That file is local and ignored by Git; `.codex/config.toml` remains shared. Review and trust the generated commands in Codex when prompted. Repeat setup after moving the `bmad-loop` installation or in a fresh checkout.

Existing checkouts upgrading from the tracked hook file should run setup again; remove any old hook that calls the deleted `bmad-story-automator` before doing so. Normal interactive Codex use does not require BMAD Loop hooks.

## Build and Test

Use the pinned .NET SDK `10.0.402` (`rollForward: latestPatch`), with a writable CLI home in restricted environments:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Use the same source-reference mode for restore and build. CI/CD uses Release/published packages; configuration-free restore defaults to that graph. Do not reuse assets between dependency modes. Works forwards its own EventStore default; when it differs from the consuming catalog, pass `-p:HexalithEventStoreVersion=<catalog version>` to both local commands until the owned build follow-up closes. At the 2026-10-20 baseline both defaults are `3.126.0`.

After building, run each test project through its xUnit v3 executable. With the pinned SDK `10.0.402`, the current `dotnet test ... --no-build` configuration fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later." The verified executable commands also work in environments where VSTest sockets are blocked:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests
```

The capture and governance command performance lane (NFR10 command-acknowledgement evidence) is **skipped by default** and opted in with `TIMESHEETS_PERF=1`, so it never enters the fast baseline. Set the variable on the same executable invocation:

```bash
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.CaptureAndGovernanceCommandPerformanceLaneTests"
```

See `docs/performance-evidence.md` for the measured p95 numbers and the NFR10 verdict.

The report, export, and dashboard query performance lane (NFR11 evidence) shares the same `TIMESHEETS_PERF=1` opt-in and is **skipped by default**. It records p95 over seeded in-process report, ledger, export, preview, dashboard, and Works planned-effort paths:

```bash
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.ReportExportDashboardQueryPerformanceLaneTests"
```

See `docs/performance-evidence.md` for the measured p95 numbers, the NFR11 verdict, and the EventStore-backed wire-path waiver.

See `docs/launch-readiness.md` for the final story-complete versus launch-complete classification record and release-gate decision. The current overall release decision is `FAIL`: deployed magic-link authority, inventory/continuity, and shared-rebuild acceptance remain unfinished. The isolated local persisted journey passed on 2026-10-18 on the earlier AppHost graph; current NuGet-orchestration runtime qualification remains open. Package currency separately remains `CONCERNS`; other integrations retain explicit waivers or post-v1 decisions.

As of 2026-10-10, valid magic-link GET displays return `200` and valid confirm and adjust POST routes return `202` in the in-process HTTP fixture after EventStore reports `Completed` and the exact stored batch is read back. Invalid and uncertain outcomes return the shared opaque `403`. Status polling defaults to a five-second deadline with 100-ms intervals and is configurable through `Timesheets:MagicLinks:SubmissionPolling`. Actor persistence tests prove rejection replay and valid work after recreation. These fixtures do not prove deployed Dapr persistence. The loader retains only sanitized status for well-formed non-Fresh catalogs. Deployed capability and Time Entry histories could not be inventoried with the available access; Story 3.6 remains in progress.

The integration test project contains in-process workflow coverage for metadata endpoints, AI-assisted Time Entry capture, submission, approval authority, entry approval/rejection, rejected-entry correction, approved-entry correction, period submission, period approval/rejection, external contribution submission/confirmation, magic-link capability issue/revoke/confirm/adjust workflows, operational Time Entry queries, Approved-Time Ledger queries, Project/Work actual-time reports, approved billable-time export, and the Timesheets dashboard overview. The default IntegrationTests run retains five declared skips: two reserved infrastructure/performance placeholders, two opt-in in-process performance tests, and the isolated live Dapr test. Opt-in local persistence proof exists; deployed acceptance and EventStore-backed wire-path performance remain separate open evidence.

Historical 2026-10-05 evidence proved magic-link no-disclosure at three layers: service/workflow tests cover the confirmation, adjustment, replay, and invalid-link matrix; `MagicLinkConfirmationHttpBoundaryTests` exercises the live confirm/adjust GET and POST routes through the in-process HTTP host and asserts every invalid case returns an equivalent opaque response (identical status, content type, ProblemDetails body, and headers, with no tenant, target, token, expiry-reason, or capability-state leakage); and `MagicLinkConfirmationCapabilityEndpointTests` verifies route shape and authority-field exclusion. The default registered `IMagicLinkConfirmationCapabilityStateLoader` is the concrete `EventStoreMagicLinkConfirmationCapabilityStateLoader`, which resolves the token hash and folds capability, scoped Time Entry, and fresh Activity Type catalog state from EventStore-backed events; the host replaces the fail-closed trusted-context default with `HttpContextTimesheetsTrustedContextAccessor` and discovers the canonical token-hash index and tenant catalog projection handlers. That historical valid HTTP journey delivers both read models through the mapped projection route, reaches all four confirm/adjust routes, and asserts that neither read model was seeded directly. Those historical invalid links returned the opaque 403. The EventStore update `da172a8effe484be96c17aaef1c007e1ecb8f27c` caused a 401 regression in rebuilt HTTP journeys; the 2026-10-09 public-route increment repaired that regression in process, including `/metadata/timesheets` and all four external capability routes. The 2026-10-09 local EventStore pointer `f87affaefef5693fd80452eea02999e46fa1888b` contained that repair, the GET-only canonical inventory fix, and the later weak-policy guard; the 2026-10-09 rebuilt two-class HTTP selection passed 482/482 on this pointer. That pointer statement is dated 2026-10-09 history. At assessment baseline Timesheets `d72899b4489aaca0b9a9b96d0ac267165920caf8` (2026-10-20), the EventStore gitlink is `6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e`; this is a dated source observation. See the Magic-link HTTP no-disclosure gate in `docs/launch-readiness.md`; deployed acceptance and current-graph runtime qualification remain open.

## Boundary Summary

Timesheets owns time-entry, timesheet-period, approval, confirmation, ledger, reporting, and export behavior. It references stable Tenant, Party, Project, and Work identifiers only.

The sole default Hexalith.Works checkout is `<workspace>/references/Hexalith.Works`. Do not initialize `Hexalith.Timesheets/Hexalith.Works`. A caller-supplied `HexalithWorksRoot` is preserved for controlled standalone or CI builds and is not mutated by Timesheets. When unset, MSBuild resolves `references/Hexalith.Works` first and `../Hexalith.Works` second.

After cloning, initialize every root-declared dependency needed by the full solution with `git submodule update --init` from the Timesheets repository root. Do not add `--recursive`. For Works-only evaluation or adapter work, `git submodule update --init -- references/Hexalith.Works` is the narrower optional command. Never initialize a nested `Hexalith.Timesheets/Hexalith.Works` checkout.

Authoritative domain state must flow through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, broker-backed CRUD, local JSON files, or direct projection mutation as Timesheets state. Projections are rebuildable read models and are not the write-side source of truth.

Tenant and resource authority is resolved by server-side gates before aggregate load, command dispatch, projection read, export, or disclosure. JWT claims and caller-submitted context are evidence, not authority.

Timesheets supplies contracts and FrontComposer-compatible metadata. A consuming FrontComposer host owns rendered Fluent UI V5 composition, responsive behavior, and browser/WCAG 2.2 AA evidence. Do not add a Timesheets UI project.
