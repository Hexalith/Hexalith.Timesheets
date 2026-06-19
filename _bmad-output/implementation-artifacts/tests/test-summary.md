# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Timesheets.Server.Tests/ExternalContributionCommandServiceTests.cs` - External contribution command API orchestration, submitted-policy path, Work authority denial, policy denial, and idempotent confirmation retry.

### E2E Tests

- [x] `tests/Hexalith.Timesheets.IntegrationTests/ExternalContributionWorkflowE2ETests.cs` - In-process external contribution record, confirm, projection, and authorized evidence-read workflow.

## Coverage

- API command branches: submit draft, submit-plus-submit-policy, tenant denial, Project denial, Work denial, Party denial, policy denial, inactive Activity Type, duplicate submit retry, confirm success, confirm Party denial, duplicate confirm retry.
- E2E workflows: 1/1 Story 3.1 external contribution workflow covered in-process.
- UI E2E: not applicable; Story 3.1 is API-only and creates no UI surface.

## Validation

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- `dotnet test` for Server and Integration was blocked by local VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- Direct xUnit fallback passed:
  - Contracts.Tests: 59/59
  - Server.Tests: 293/293
  - Projections.Tests: 40/40
  - ArchitectureTests: 19/19
  - IntegrationTests: 31 passed / 2 skipped reserved infrastructure-performance lanes

## Checklist

- [x] API tests generated.
- [x] E2E tests generated for the API-only workflow.
- [x] Tests use xUnit v3, Shouldly, and existing in-process service/projection patterns.
- [x] Tests cover happy path plus critical fail-closed and idempotency cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and deterministic.
