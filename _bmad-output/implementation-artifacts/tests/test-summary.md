# Test Automation Summary

## Generated Tests

### API Tests
- [x] tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs - Existing endpoint route coverage for narrow adjustment display/submit routes, request authority-field exclusion, opaque denial copy, and no token-inspection route.
- [x] tests/Hexalith.Timesheets.Contracts.Tests/MagicLinkContractTests.cs - Existing adjustment command/display/OpenAPI contract validation with raw-token, token-hash, target, tenant, contributor, and approval-field exclusion checks.
- [x] tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs - Existing adjustment service coverage for allowed fields, server-derived scope/source metadata, invalid values, wrong action, and no capability use on failed adjustment.

### E2E Tests
- [x] tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityWorkflowE2ETests.cs - Added story 3.4 issue -> safe adjustment display -> invalid adjustment no-use -> valid adjustment -> used capability projection -> Time Entry evidence projection -> replay rejection workflow coverage.
- [x] tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs - Existing projection coverage for adjusted effective draft values, previous/adjusted value lineage, duplicate delivery, and safe source evidence.
- [x] tests/Hexalith.Timesheets.Projections.Tests/MagicLinkConfirmationCapabilityProjectionTests.cs - Existing projection coverage for safe adjusted outcome category on used capabilities.

## Coverage

- Story 3.4 API routes: 2/2 adjustment routes covered by endpoint/static route tests.
- Story 3.4 workflow paths: safe display, valid adjustment, no approval transition, capability used outcome, Time Entry evidence projection, duplicate-delivery projection, and replay rejection covered.
- Story 3.4 critical error paths: invalid duration, confirm-only capability, wrong/used capability, stale/unavailable catalog, invalid/inactive/ambiguous Activity Type, scope mismatch, tenant/contributor/target mismatch, and non-editable server-derived fields covered across server, contract, projection, and integration lanes.
- Story 3.4 privacy checks: raw token, token hash exposure, decoded/token material, command-body leakage, target/contributor/tenant request authority, comments beyond policy, and token-inspection route absence covered.
- UI feature coverage: no Timesheets UI package or browser surface exists for this story; display/UI behavior is covered through safe display contracts, route surface, metadata, labels/units fields, and server-side no-disclosure behavior.

## Validation

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed with 0 warnings and 0 errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -m:1 /nr:false` was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` passed: 39 total, 0 failed, 2 skipped by existing infrastructure/performance skip policy.
- `git diff --check` passed.

## Checklist

- [x] API tests generated where applicable.
- [x] E2E tests generated for the implemented workflow; no browser UI project exists in this story.
- [x] Tests use standard xUnit v3 APIs and existing Shouldly assertions.
- [x] Tests cover happy path and critical error paths.
- [x] Generated tests run successfully through the documented xUnit v3 executable fallback.
- [x] Tests have clear descriptions, no hardcoded waits, and no order dependency.
- [x] Test summary includes coverage metrics.
