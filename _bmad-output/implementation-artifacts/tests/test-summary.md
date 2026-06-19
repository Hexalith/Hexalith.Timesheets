# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ApprovedEntryCorrectionsE2ETests.cs` - In-process approved correction command-service workflow appends `TimeEntryApprovedCorrected`, preserves source approval lineage, and keeps approved entries locked from direct edits.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ApprovedEntryCorrectionsE2ETests.cs` - Approved correction corrected-target authorization fails closed before authority resolution or aggregate dispatch.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ApprovedEntryCorrectionsE2ETests.cs` - Approved correction stale Activity Type catalog fails closed before aggregate dispatch with typed `ProjectionUnavailable` rejection.

### E2E Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ApprovedEntryCorrectionsE2ETests.cs` - Record, submit, approve, add approved correction, replay through projection, disclose through query service, and verify effective values, approved correction evidence, approval evidence, lock evidence, duplicate-delivery dedupe, display hydration, safe serialization, metadata vocabulary, and OpenAPI schema presence.

## Coverage
- Story 2.6 approved correction workflow: 1/1 primary in-process command-service plus projection/query workflow covered.
- API/service blockers: corrected target authorization and stale Activity Type catalog covered in generated integration tests.
- Existing focused lanes already cover approved correction contracts, aggregate idempotency and validation, service gate ordering, projection out-of-order/duplicate behavior, metadata/OpenAPI schemas, architecture/privacy checks, and rejected-correction regression coverage.
- Browser UI E2E: not applicable; the Timesheets module currently exposes FrontComposer metadata/read-model behavior and has no bespoke Timesheets UI project.

## Validation
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -m:1 /nr:false` attempted; blocked by local VSTest socket permission.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -warnaserror -m:1 /nr:false` passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 24 total, 0 failed, 2 explicit infrastructure/performance skips.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed: 19 total, 0 failed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 49 total, 0 failed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed: 249 total, 0 failed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed: 32 total, 0 failed.

## Checklist Result
- [x] API tests generated where applicable.
- [x] E2E tests generated for the in-process workflow surface.
- [x] Tests use xUnit v3, Shouldly, and existing service/projection/query APIs.
- [x] Tests cover the approved correction happy path.
- [x] Tests cover critical authorization and stale-freshness error cases.
- [x] Tests use semantic service/query outcomes and safe JSON assertions; no browser locators apply because no Timesheets UI project exists.
- [x] Tests have clear descriptions and no hardcoded waits.
- [x] Tests are independent and do not require ordering.
- [x] Summary includes coverage metrics.

## Next Steps
- Run the same test projects under CI VSTest where socket permissions are available.
