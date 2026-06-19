# Test Automation Summary

## Generated Tests

### API and Service Tests
- [x] `tests/Hexalith.Timesheets.Server.Tests/ActualTimeReportAuthorizationTests.cs` - Actual-time report query authorization, fail-closed reader behavior, row filtering, Project/Work-specific row authorization, post-authorization hydration, and Works planned-effort lookup gating.
- [x] `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs` - Server kernel registration of actual-time report query services and fail-closed report/planned-effort defaults.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs` - Seeded Project and Work report query flows with authorization, display hydration, paging, freshness metadata, evidence drill-in compatibility, and Works planned-effort attribution.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` - Metadata endpoint catalog coverage for Project and Work actual-time report descriptors.

### Projection and Contract Tests
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/ActualTimeReportContractTests.cs` - Report query/read-model serialization, authority-field exclusion, planned-effort availability states, metadata filters/fields/actions/status vocabularies, and forbidden ownership/finance language checks.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs` - Project and Work rollups, approved and selected-entry sources, duplicate/replay idempotence, correction/superseded handling, deterministic sorting/cursor paging, projection freshness states, and period/date/billable/contributor-category filters.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests` - Existing privacy, metadata, and performance-evidence lane checks remain in the affected validation set.

## Coverage

- API/service coverage: tenant-first denial, unavailable reader, insufficient-role row filtering, fail-closed unsafe denials, exact Project vs Work authorization requests, display hydration only after authorization, planned-effort lookup only for disclosed Work rows, and separate actual/provided freshness states.
- E2E/integration coverage: Project and Work report service workflows, seeded paging, report hydration, `ReadTimeEntryEvidence` drill-in compatibility, metadata catalog exposure, and Works source attribution.
- Projection coverage: Project and Work grouping by target, period, contributor, activity type, billable state, approval state, contributor category, actual minutes, source row count, corrections, superseded rows, replay dedupe, stable ordering, cursor paging, and stale/rebuilding/unavailable freshness mapping.
- UI surface coverage: FrontComposer metadata descriptor tests cover FilterBar-style fields, FluentDataGrid report fields, related actions, status-badge vocabularies, projection freshness, and no raw EventStore/finance ownership language. This story publishes metadata, not a hand-built browser UI.
- Performance evidence: common actual-time report latency remains documented with the existing explicit skipped performance lane until realistic EventStore-backed fixtures exist.

## Validation

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj --no-restore -m:1 /nr:false` was blocked by local VSTest socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`).
- Direct xUnit v3 fallback passed:
  - `tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` - 78 passed.
  - `tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` - 71 passed.
  - `tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` - 357 passed.
  - `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` - 45 passed, 2 skipped by existing infrastructure/performance lane reservations.
  - `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` - 20 passed.

## Checklist Result

- [x] API/service tests generated where applicable.
- [x] E2E/integration tests generated for the implemented Project and Work report workflows.
- [x] Tests use standard xUnit v3, Shouldly, and existing project patterns.
- [x] Tests cover happy paths and critical error/degraded/denial cases.
- [x] Tests use semantic report metadata assertions for the UI surface.
- [x] Tests have clear descriptions and no hardcoded waits or sleeps.
- [x] Tests are independent and pass through the repository fallback runner.
- [x] Summary includes coverage metrics and validation evidence.
