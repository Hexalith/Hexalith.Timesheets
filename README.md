# Hexalith Timesheets

Hexalith Timesheets is a domain module for trusted time capture, approval, confirmation, reporting, and finance export.

## Build and Test

Use a writable CLI home in restricted environments:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Use the same source-reference mode for restore and build. The bare solution build currently loses the external Works project's configuration and fails with `HXW0002`; the owned follow-up is in `_bmad-output/implementation-artifacts/deferred-work.md`, under "Deferred from: build guidance review patches for Story 3.6 (2026-10-04)".

After building, run each test project through its xUnit v3 executable. With the pinned SDK `10.0.401`, the current `dotnet test ... --no-build` configuration fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later." The verified executable commands also work in environments where VSTest sockets are blocked:

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

See `docs/launch-readiness.md` for the final story-complete versus launch-complete classification record and release-gate decision. The current overall release decision is `FAIL`: live magic-link acceptance is regressed and durable writes remain unfinished. Package currency separately remains `CONCERNS`; other integrations retain explicit waivers or post-v1 decisions.

The integration test project contains in-process workflow coverage for metadata endpoints, AI-assisted Time Entry capture, submission, approval authority, entry approval/rejection, rejected-entry correction, approved-entry correction, period submission, period approval/rejection, external contribution submission/confirmation, magic-link capability issue/revoke/confirm/adjust workflows, operational Time Entry queries, Approved-Time Ledger queries, Project/Work actual-time reports, approved billable-time export, and the Timesheets dashboard overview. Infrastructure and performance evidence tests remain isolated with explicit skips until EventStore, Dapr, Aspire, and realistic persisted-state fixtures are added.

Historical 2026-10-05 evidence proved magic-link no-disclosure at three layers: service/workflow tests cover the confirmation, adjustment, replay, and invalid-link matrix; `MagicLinkConfirmationHttpBoundaryTests` exercises the live confirm/adjust GET and POST routes through the in-process HTTP host and asserts every invalid case returns an equivalent opaque response (identical status, content type, ProblemDetails body, and headers, with no tenant, target, token, expiry-reason, or capability-state leakage); and `MagicLinkConfirmationCapabilityEndpointTests` verifies route shape and authority-field exclusion. The default registered `IMagicLinkConfirmationCapabilityStateLoader` is the concrete `EventStoreMagicLinkConfirmationCapabilityStateLoader`, which resolves the token hash and folds capability, scoped Time Entry, and fresh Activity Type catalog state from EventStore-backed events; the host replaces the fail-closed trusted-context default with `HttpContextTimesheetsTrustedContextAccessor` and discovers the canonical token-hash index and tenant catalog projection handlers. That historical valid HTTP journey delivers both read models through the mapped projection route, reaches all four confirm/adjust routes, and asserts that neither read model was seeded directly. Those historical invalid links returned the opaque 403. Current rebuilt HTTP journeys stop at 401, including every external magic-link route and `/metadata/timesheets`, regressing Story 3.6 AC1/AC3/AC4 since the EventStore update `da172a8effe484be96c17aaef1c007e1ecb8f27c`. See the Magic-link HTTP no-disclosure gate in `docs/launch-readiness.md`; the supported public-capability route contract remains deferred to EventStore.

## Boundary Summary

Timesheets owns time-entry, timesheet-period, approval, confirmation, ledger, reporting, and export behavior. It references stable Tenant, Party, Project, and Work identifiers only.

The sole default Hexalith.Works checkout is `<workspace>/references/Hexalith.Works`. Do not initialize `Hexalith.Timesheets/Hexalith.Works`. A caller-supplied `HexalithWorksRoot` is preserved for controlled standalone or CI builds and is not mutated by Timesheets. When unset, MSBuild resolves `references/Hexalith.Works` first and `../Hexalith.Works` second.

After cloning, initialize every root-declared dependency needed by the full solution with `git submodule update --init` from the Timesheets repository root. Do not add `--recursive`. For Works-only evaluation or adapter work, `git submodule update --init -- references/Hexalith.Works` is the narrower optional command. Never initialize a nested `Hexalith.Timesheets/Hexalith.Works` checkout.

Authoritative domain state must flow through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, broker-backed CRUD, local JSON files, or direct projection mutation as Timesheets state. Projections are rebuildable read models and are not the write-side source of truth.

Tenant and resource authority is resolved by server-side gates before aggregate load, command dispatch, projection read, export, or disclosure. JWT claims and caller-submitted context are evidence, not authority.

Future UI surfaces must be FrontComposer-compatible and use Blazor Fluent UI V5 through the established Hexalith shell. This scaffold intentionally does not create a Timesheets UI project.
