# Hexalith Timesheets

Hexalith Timesheets is a domain module for trusted time capture, approval, confirmation, reporting, and finance export.

## Build and Test

Use a writable CLI home in restricted environments:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-build
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-build
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --no-build
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --no-build
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build
```

If `dotnet test` is blocked by local VSTest socket permissions, build first and run the xUnit v3 executables directly:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests
```

The capture and governance command performance lane (NFR10 command-acknowledgement evidence) is **skipped by default** and opted in with `TIMESHEETS_PERF=1`, so it never enters the fast baseline. Set the variable on the same invocation, mirroring the fallback-command style above:

```bash
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build
# or, if VSTest sockets are blocked, the built executable directly:
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.CaptureAndGovernanceCommandPerformanceLaneTests"
```

See `docs/performance-evidence.md` for the measured p95 numbers and the NFR10 verdict.

The integration test project contains in-process workflow coverage for metadata endpoints, AI-assisted Time Entry capture, submission, approval authority, entry approval/rejection, rejected-entry correction, approved-entry correction, period submission, period approval/rejection, external contribution submission/confirmation, magic-link capability issue/revoke/confirm/adjust workflows, operational Time Entry queries, Approved-Time Ledger queries, Project/Work actual-time reports, approved billable-time export, and the Timesheets dashboard overview. Infrastructure and performance evidence tests remain isolated with explicit skips until EventStore, Dapr, Aspire, and realistic persisted-state fixtures are added.

Magic-link endpoint tests currently verify route shape, authority-field exclusion, and shared opaque denial copy through source assertions. The executable magic-link confirmation, adjustment, replay, and no-disclosure matrix coverage lives in service/workflow tests. The default registered `IMagicLinkConfirmationCapabilityStateLoader` is fail-closed and unavailable; live host display/confirm/adjust requires a concrete EventStore-backed loader.

## Boundary Summary

Timesheets owns time-entry, timesheet-period, approval, confirmation, ledger, reporting, and export behavior. It references stable Tenant, Party, Project, and Work identifiers only.

Authoritative domain state must flow through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, broker-backed CRUD, local JSON files, or direct projection mutation as Timesheets state. Projections are rebuildable read models and are not the write-side source of truth.

Tenant and resource authority is resolved by server-side gates before aggregate load, command dispatch, projection read, export, or disclosure. JWT claims and caller-submitted context are evidence, not authority.

Future UI surfaces must be FrontComposer-compatible and use Blazor Fluent UI V5 through the established Hexalith shell. This scaffold intentionally does not create a Timesheets UI project.
