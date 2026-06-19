# Test Automation Summary

## Generated Tests

### API Tests
- [x] tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs - Existing endpoint route coverage for opaque denial copy, narrow external magic-link routes, missing authority fields, and no token-inspection surface.
- [x] tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs - Added invalid-link API/service coverage for malformed token parsing, unauthorized disclosure paths, adjustment invalid-category equivalence, stale-catalog no-disclosure, and neutral rejection messages.
- [x] tests/Hexalith.Timesheets.Contracts.Tests/MagicLinkContractTests.cs - Existing contract coverage for invalid denial schema, safe magic-link response shapes, and absence of token/hash/authority/comment/approval leakage.

### E2E Tests
- [x] tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs - Added story 3.5 workflow coverage proving invalid confirmation and adjustment paths return no external disclosure for blank, malformed, unknown, terminal, tenant-mismatch, wrong-recipient, Time Entry mismatch, stale-catalog, and expired cases.
- [x] tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs - Existing workflows still cover valid issue -> confirm -> used projection -> replay denial and issue -> adjust -> evidence projection -> replay denial.

## Coverage

- Story 3.5 API surfaces: 4/4 external routes covered by endpoint route/copy tests (`confirm`, `confirm/submit`, `adjust`, `adjust/submit`).
- Story 3.5 service invalid categories: malformed, unknown/hash mismatch, null capability, expired, used, revoked, tenant mismatch, wrong recipient, wrong action, wrong scope, Time Entry mismatch, target mismatch, stale catalog, unauthorized, repeated attempt/replay.
- Story 3.5 privacy checks: no raw token, token hash, decoded token material, tenant/Party/target names, comments, command bodies, approval details, EventStore envelopes, or token-inspection routes in tested external/contract/diagnostic surfaces.
- UI feature coverage: no Timesheets UI/browser package exists; E2E coverage is in-process workflow/API behavior through xUnit v3.

## Validation

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj --no-restore -m:1 /nr:false` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -m:1 /nr:false` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` passed: 20 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` passed: 68 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` passed: 329 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` passed: 47 total, 0 failed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 41 total, 0 failed, 2 existing infrastructure/performance skips.

## Checklist

- [x] API tests generated where applicable.
- [x] E2E tests generated for the implemented workflow; no browser UI project exists.
- [x] Tests use standard xUnit v3 APIs and existing Shouldly assertions.
- [x] Tests cover happy path and critical error paths.
- [x] Generated tests run successfully through the documented xUnit v3 executable fallback.
- [x] Tests use clear descriptions, no hardcoded waits, and no order dependency.
- [x] Test summary includes coverage metrics.
