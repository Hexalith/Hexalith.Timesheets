# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` - Story 1.9 AI metric domain validation covers automated-agent provider metrics, human/external rejection, unknown source/token availability, non-negative unit fields, unavailable placeholders, and unchanged `DurationMinutes`.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` - AI-agent capture uses the existing tenant, Project/Work, contributor Party, policy, and Activity Type catalog gates before domain dispatch.
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` - AI metric contracts round-trip units, source metadata, token availability, nullable unavailable counts, provider-reported zero token counts, read-model evidence, and authority-field omission.

### E2E Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/AiAssistedTimeCaptureMetricsE2ETests.cs` - Generated story 1.9 workflow test records an AI-agent Project Time Entry, projects evidence through replay/dedupe, discloses hydrated evidence through the query service, keeps AI metrics separated from `DurationMinutes`, preserves not-reported provider tokens as `null`, and rejects human provider-reported AI metrics.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` - Projection tests preserve provider-reported AI metrics through duplicate replay and keep unreported token metrics null.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` - Privacy fitness tests cover token values, prompts, responses, raw commands, comments, personal data, bearer tokens, secrets, and provider payloads in diagnostics/artifacts.

## Coverage

- API/domain boundaries: AI metric acceptance, validation failures, authorization fail-closed behavior, and catalog dispatch gating are covered.
- E2E workflow: in-process capture -> domain event -> projection replay/dedupe -> evidence query disclosure is covered for story 1.9.
- UI metadata surface: Time Entry Detail remains metadata-driven through FrontComposer descriptors; browser E2E is not applicable because Timesheets has no local rendered UI project or Playwright workspace.
- Token semantics: provider-reported zero remains distinct from unavailable/not-reported token metrics; missing provider tokens remain `null`, never synthesized as `0`.
- Privacy: generated and existing tests assert authority fields, payloads, token values, prompts/responses, secrets, and personal data are not exposed in contracts, metadata, diagnostics, or evidence JSON.

## Validation

- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -warnaserror -m:1 /nr:false`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` - 33 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` - 180 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` - 21 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` - 17 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` - 5 passed, 2 expected infrastructure/performance placeholder skips.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build` was attempted first and hit the known VSTest socket permission failure in this sandbox, so validation used the direct xUnit v3 in-process executable fallback.

## Checklist Status

- [x] API tests generated where applicable.
- [x] E2E-style tests generated for the AI-assisted capture workflow; browser E2E is not applicable until a Timesheets UI runner/page exists.
- [x] Tests use standard xUnit v3 and Shouldly APIs.
- [x] Tests cover happy path and critical error cases.
- [x] All generated and affected tests run successfully.
- [x] Tests use semantic contract/service assertions; no hardcoded waits or sleeps.
- [x] Tests are independent and do not depend on execution order.
- [x] Tests are saved in existing test directories.
- [x] Summary includes coverage metrics.
