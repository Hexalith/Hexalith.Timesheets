# Test Automation Summary

## Generated Tests

### API and Service Tests
- [x] `tests/Hexalith.Timesheets.IntegrationTests/ActualTimeReportQueryServiceIntegrationTests.cs` - AI Work report workflow now proves an `AiAgent` filter returns automated-agent effort only, even when a human row uses the same Party reference.
- [x] Existing service tests continue to cover tenant-first denial, fail-closed report reader behavior, row-level authorization, authorized-only hydration, and Work planned-effort lookup gating.

### Projection and Contract Tests
- [x] `tests/Hexalith.Timesheets.Projections.Tests/ActualTimeReportProjectionTests.cs` - Added explicit AI-agent Party filter coverage that excludes human rows for the same Party reference and keeps AI units separate from actual minutes.
- [x] Existing contract and metadata tests cover AI effort fields, null unavailable token metrics, provider-reported zero tokens, source metadata, filters, cursor/freshness fields, FrontComposer projection view metadata, status vocabularies, and forbidden finance/ownership language.

## Coverage

- API/service workflows: AI Work report query through `ActualTimeReportQueryService`, authorization, hydration, freshness metadata, Works planned-effort attribution, paging, and drill-in compatibility with `ReadTimeEntryEvidence`.
- Projection coverage: AI wall-clock runtime, model/tool runtime, AI billable effort, token counts, not-reported token nulls, provider source metadata, correction/superseded AI metrics, duplicate/replay idempotence, deterministic paging, and AI-agent filtering.
- UI surface coverage: FrontComposer metadata descriptors for Project/Work actual-time reports include AI effort filters, AI metric fields, unavailable-token copy, status badges, projection freshness, and no EventStore/finance authority language.
- Error/degraded coverage: tenant denial, unavailable readers, unsafe row denials, insufficient-role row filtering, stale/rebuilding/unavailable freshness states, and unavailable planned-effort states.

## Validation

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test ...` was blocked before test execution by local MSBuild/VSTest socket permissions: `System.Net.Sockets.SocketException (13): Permission denied`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` passed.
- Direct xUnit v3 fallback passed:
  - Contracts: 79 passed.
  - Projections: 76 passed.
  - Server: 358 passed.
  - Integration: 46 passed, 2 existing infrastructure/performance reservations skipped.
  - Architecture: 20 passed.

## Checklist Result

- [x] API/service tests generated where applicable.
- [x] E2E/integration tests generated for the AI effort reporting workflow.
- [x] Tests use standard xUnit v3, Shouldly, and existing project patterns.
- [x] Tests cover happy path plus critical filtering, denial, unavailable, and degraded cases.
- [x] Tests use metadata assertions for the FrontComposer UI surface.
- [x] Tests have clear descriptions and no hardcoded waits or sleeps.
- [x] Tests are independent and pass through the repository fallback runner.
- [x] Summary includes coverage metrics and validation evidence.
