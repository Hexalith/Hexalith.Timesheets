# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` - Submit command/event JSON round-trip, authority-field omission, enum sentinel, metadata descriptor, and OpenAPI schema coverage.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAggregateTests.cs` - Draft to Submitted transition, duplicate same-submission no-op, non-Draft rejection, missing context rejection, evidence preservation, and entry-keyed validation errors.
- [x] `tests/Hexalith.Timesheets.Server.Tests/TimeEntryAuthorizationTests.cs` - Submit service fail-closed tenant/resource/contributor/policy/catalog gates, positive dispatch with server context, and partial batch behavior.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/HostMetadataEndpointTests.cs` - Host metadata contract exports the submit descriptor with blocking fields, freshness, partial submission, and persistent message-bar state while omitting caller authority.

### E2E Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/SubmitTimeEntriesForApprovalE2ETests.cs` - Generated story 2.1 workflow test records a Draft Project Time Entry, submits it for approval, projects Submitted evidence through replay/dedupe, discloses evidence through the query service, preserves recorded fields, and omits caller authority from evidence JSON.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/SubmitTimeEntriesForApprovalE2ETests.cs` - Generated partial-batch workflow test accepts a valid entry while blocking an entry whose Activity Type became inactive, with field-level correction data for the blocked entry.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` - Projection coverage applies ordered Recorded + Submitted events, dedupes duplicate submitted message IDs, preserves lineage, filters unrelated entries, and exposes freshness states.

## Coverage
- API/domain boundaries: submit contracts, EventStore payloads, metadata/OpenAPI surfaces, aggregate lifecycle, authorization gates, Activity Type freshness, and partial submission reporting are covered.
- E2E workflow: in-process record -> submit -> projection replay/dedupe -> evidence query disclosure is covered for story 2.1.
- UI metadata surface: FrontComposer submit metadata includes Draft/Submitted state vocabulary, blocking fields, projection freshness, partial submission, and persistent message-bar state. Browser E2E is not applicable because Timesheets has no local rendered UI project or Playwright workspace.
- Critical error cases: duplicate submission idempotency, non-Draft rejection, missing tenant/submitter, unresolved authority, stale catalog, inactive Activity Type, Project/Work scope mismatch, required facts, and partial-batch field errors are covered.
- Privacy: contract, metadata, evidence, architecture, and integration tests assert caller authority, EventStore envelope fields, comments, command bodies, event payloads, personal data, bearer tokens, secrets, and raw target names are not exposed where prohibited.

## Validation
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -warnaserror -m:1 /nr:false`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` - 35 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` - 194 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` - 23 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` - 17 passed.
- [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` - 8 passed, 2 expected infrastructure/performance placeholder skips.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build` was attempted first and hit the known VSTest socket permission failure in this sandbox, so validation used the direct xUnit v3 in-process executable fallback documented for this repository.

## Checklist Status
- [x] API tests generated where applicable.
- [x] E2E-style tests generated for the submit-for-approval workflow; browser E2E is not applicable until a Timesheets UI runner/page exists.
- [x] Tests use standard xUnit v3 and Shouldly APIs.
- [x] Tests cover happy path and critical error cases.
- [x] All generated and affected tests run successfully.
- [x] Tests use semantic contract/service assertions; no hardcoded waits or sleeps.
- [x] Tests are independent and do not depend on execution order.
- [x] Tests are saved in existing test directories.
- [x] Summary includes coverage metrics.
