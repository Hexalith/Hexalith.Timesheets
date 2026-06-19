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

The integration test project contains in-process workflow coverage for AI-assisted Time Entry capture. Infrastructure and performance evidence tests remain isolated with explicit skips until EventStore, Dapr, Aspire, and realistic persisted-state fixtures are added.

## Boundary Summary

Timesheets owns time-entry, timesheet-period, approval, confirmation, ledger, reporting, and export behavior. It references stable Tenant, Party, Project, and Work identifiers only.

Authoritative domain state must flow through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, broker-backed CRUD, local JSON files, or direct projection mutation as Timesheets state. Projections are rebuildable read models and are not the write-side source of truth.

Tenant and resource authority is resolved by server-side gates before aggregate load, command dispatch, projection read, export, or disclosure. JWT claims and caller-submitted context are evidence, not authority.

Future UI surfaces must be FrontComposer-compatible and use Blazor Fluent UI V5 through the established Hexalith shell. This scaffold intentionally does not create a Timesheets UI project.
