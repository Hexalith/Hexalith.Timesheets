# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` - Metadata endpoint contract exports the Activity Type Catalog capability and omits authority/payload identifiers.

### E2E Tests
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs` - Activity Type Catalog operator workflow metadata exposes create, rename, update billable default, deactivate, and reactivate commands with textual state/freshness cues.
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/ActivityTypeCatalogE2ETests.cs` - Activity Type Catalog read model round-trips active and inactive rows while preserving capture-selection availability.
- [x] `tests/Hexalith.Timesheets.Server.Tests/ActivityTypeAuthorizationTests.cs` - Policy denial after tenant authorization fails before Activity Type domain dispatch.

## Coverage

- API endpoints: 1/1 Timesheets host endpoint covered (`/metadata/timesheets`).
- Activity Type Catalog workflow metadata: 5/5 tenant catalog verbs covered in generated E2E-style tests.
- Activity Type Catalog read model states: 2/2 active/inactive states covered.
- Critical error cases: tenant access denials, stale/ambiguous/unavailable authority, projection freshness denial, duplicate/unknown domain outcomes, and policy denial before dispatch are covered across the existing and generated test set.
- Browser E2E: not generated because the Timesheets module currently has no local UI project, Playwright workspace, or rendered Activity Type Catalog page; Story 1.5 exposes FrontComposer metadata and host metadata only.

## Validation

- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-build`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --no-build`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --no-build`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build`

`dotnet test` was attempted first and was blocked by VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`), so validation used the direct xUnit v3 in-process runner fallback already documented by Story 1.5.

## Checklist Status

- [x] API tests generated where applicable.
- [x] E2E-style tests generated for the metadata-exposed UI workflow; browser E2E is not applicable until a Timesheets UI runner/page exists.
- [x] Tests use standard xUnit v3 and Shouldly APIs.
- [x] Tests cover happy paths and critical error cases.
- [x] Generated tests run successfully.
- [x] Tests use semantic contract assertions; no UI locators, waits, sleeps, or order dependency apply.
- [x] Tests are saved in the existing test directories.
- [x] Summary includes coverage metrics.
