# Test Automation Summary — Story 3.6 (EventStore-Backed Magic-Link State Loading)

> **Superseded on 2026-09-17.** The 2026-06-22 generation record below is retained as historical
> evidence only. Its framework versions, server count, and claims that HTTP/projection-host coverage
> did not exist no longer describe the repository.

## Current Verification

- Date / build baseline: 2026-10-04, changed worktree based on `30fc6d3092cbb376c500176ceeef351109e3fa7f` (alternate-configuration and literal-parsing increment).
- SDK: .NET `10.0.401` (`rollForward: latestPatch`).
- Restore: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` passed.
- Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings and errors.
- The historical bare solution build and its explicit-Debug retry on 2026-10-03 failed with `HXW0002` and missing Works serialization references (176 errors): solution traversal removes the external project's Configuration, requiring the supported source-reference option. The first source-reference build hit `NETSDK1064` for unrestored Roslynator.Analyzers 4.15.0; restoring with the same source-reference mode resolved it. No package pins or submodule pointers changed.
- Direct xUnit v3 executables, each invoked individually as `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>`: ArchitectureTests 56/56; Contracts.Tests 90/90; IntegrationTests 110 total, 106 pass, 4 declared skips; Projections.Tests 146/146; Server.Tests 478/478; Works.Tests 76/76.
- Final inventory: 956 tests, 952 passed, 4 declared infrastructure/performance skips, 0 failures. The existing performance lanes were left disabled.
- The first ArchitectureTests invocation in this increment failed one of 55 cases because the shared catalog now expresses CommunityToolkit.Aspire.Hosting.Dapr as `$(HexalithAspireHostingDaprVersion)`. The existing catalog reader was adapted to resolve exact property references to unconditional defaults, preserving the concrete-version assertion and excluding the unrelated Folders-only override. The focused ArchitectureTests Debug build passed with zero warnings/errors; its rerun passed 55/55. The other five unchanged lanes passed in this increment as recorded above.
- Prior review coverage (retained): three server availability combinations, default loader resolution, canonical deactivation delivery plus all four external HTTP routes, configured-port projection delivery with persisted index/catalog assertions, public-port refusal of a dispatch proven to persist internally, all four public routes after internal delivery, and two HTTP claim-scoped administrator flows for new and existing capabilities through the actual host accessor and authorization request context. Existing opaque-denial coverage remains passing.
- Current increment: the existing `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` fitness check counts exactly one case-insensitive Timesheets InternalSurface configuration-key prefix in comment-stripped AppHost source, covering double-underscore exports, colon keys and command-line settings while accepting unrelated InternalSurface URLs. It rejects literal `AllowOnAnyPort` in both AppHost and host source and preserves both interpolated-verbatim prefix orders (`@$"` and `$@"`). The retained verifier `python3 _bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py` passed baseline, eight harmless literal/URL controls and restored-source checks; all 21 unsafe mutations were rejected, including colon-key/WithArgs overrides, host Configure/in-memory bypasses, interpolated-verbatim URL-hidden overrides and an actual end-of-line AppHost export comment with the chain semicolon on the next line. Block-comment and directive substitutes also retain their semicolon outside the disabled region. Both production files were restored byte-for-byte. This remains bounded lexical source fitness evidence; nested interpolation, escaped identifiers/keys, computed keys and runtime/deployment configuration need separate semantic verification. The verifier is a separate focused command; the one added catalog-reader regression raises the inventory to 956 tests.
- Cumulative coverage includes the concrete loader, canonical projection delivery, four-route HTTP no-disclosure, correction-scope contract metadata, serialized scope replay, and legacy mismatched-retry rejection.
- The administrator HTTP flows inject `HttpContext.User` through `ClaimsStartupFilter` and substitute `ScriptedAccessGuard`; they prove claim-to-accessor and authorization-context mapping only, not authenticated deployment. The host currently registers no authentication scheme.
- The configured-port tests simulate `Connection.LocalPort` before the production guard in TestServer and bind the literal `Timesheets:InternalSurface:Port` setting through `IWebHostBuilder.UseSetting`. The endpoint fitness test ties the AppHost's `Timesheets__InternalSurface__Port` export to the Timesheets resource's unconditional, distinct 8080/8081 listeners and the host options section. They prove host configuration/middleware behavior, not a real listener, network isolation, durable command submission, or concurrent single-use consumption. Adding an in-memory configuration source at the deferred minimal-host callback initially failed with a disposed ConfigurationManager; the setting-based fixture passed.
- Historical runtime smoke (2026-10-03): the first Aspire start timed out during restore. A subsequent no-build start succeeded, but `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18 because Timesheets failed to start; describe showed Timesheets Finished and security Unhealthy. `aspire stop` completed cleanup. No passing current-topology evidence is claimed.
- Runner guidance: the current `dotnet test tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj --no-build` invocation exits 1 with Microsoft.Testing.Platform's unsupported VSTest-target error on .NET 10; README now uses the verified direct executables by default. Runner configuration remains unchanged.
- Current runtime check (2026-10-04, repeated before this increment): `aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` succeeded; `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18. Describe reported Timesheets Finished (exit 134); `aspire logs timesheets --tail 18` captured `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. All commands used the explicit AppHost path and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`; `aspire stop` succeeded. This environment conflict does not explain the historical October 3 failure or prove a running magic-link journey.

- Independent review patched four findings (unrelated URLs, ancestor conditions, disabled-region semicolons and interpolation controls) and grouped two verified semantic-analysis limitations into one deferred ledger item. Separate focused reproductions confirmed escaped and concatenated keys plus an escaped property identifier still pass the lexical check, with exact source-byte restoration. The final ArchitectureTests regression covers both element and ancestor conditions; its post-review lane has 56 tests.

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-22
**Engineer:** QA automation (generation only — no code review)
**Feature under test:** `EventStoreMagicLinkConfirmationCapabilityStateLoader` + rebuildable token-hash → capability index

## Framework Detected

- .NET 10 / xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `6.0.0-rc.1` (existing project conventions reused — no new framework introduced).
- No UI/HTTP surface exists in this backend-only story (the HTTP-boundary `WebApplicationFactory` proof is owned by Story 3.7), so "E2E" here means **API/service-level loader tests** exercised through the real EventStore SDK seams via scripted gateway/read-model stubs.

## Generated Tests

All added to `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`
(extending the dev's existing 16 loader tests — no rewrite).

### Gap tests applied (8 new cases)

- [x] `LoadTokenStateAsync_fails_closed_for_blank_token_without_any_read` (Theory: `""`, `"   "`) — **AC3**: blank/malformed token collapses to the identical opaque state *before* any index/EventStore read.
- [x] `LoadTokenStateAsync_fails_closed_when_token_hashing_rejects_malformed_token` — **AC3**: `DeriveHash` `ArgumentException` path fails closed with no reads (previously uncovered catch branch).
- [x] `LoadTokenStateAsync_returns_folded_expired_state_proving_index_is_not_expiry_authority` — **AC3**: parity with revoked/used — expiry truth lives only in the folded aggregate; the index stays a non-authoritative candidate resolver.
- [x] `LoadActivityTypeCatalogAsync_folds_fresh_tenant_catalog_for_admin_paths` — **AC1/AC4**: admin (tenant-less) catalog happy path returns a Fresh folded catalog from the domain-wide read.
- [x] `LoadActivityTypeCatalogAsync_folds_only_tenant_scoped_items_and_reflects_rename_and_deactivation` — **AC2**: project-scoped activity types excluded; rename/deactivate fold applied deterministically.
- [x] `LoadActivityTypeCatalogAsync_folds_events_across_continuation_pages` — **AC2**: the `ReadAllEventsAsync` continuation-token paging loop accumulates every page (previously entirely uncovered).
- [x] `LoadCapabilityAsync_folds_terminal_state_for_admin_revoke_and_expire_paths` — **AC1**: admin revoke/expire observe an already-terminal capability as terminal, not as a fresh issue.

### Test-harness extensions (no production code changed)

- `ThrowingTokenGenerator` stub (proves the malformed-token catch path).
- `ScriptedGatewayClient.WithPagedStream(...)` — multi-page continuation-token support so the paging loop is exercised.
- New deterministic event builders: `Expired()`, `SecondActivityCreated()`, `ProjectScopedActivityCreated()`.

## Coverage

- **Loader (`LoadTokenStateAsync` / `LoadCapabilityAsync` / `LoadActivityTypeCatalogAsync`):** all three methods now have happy-path + fail-closed + admin-path + determinism + paging coverage.
- **Acceptance-criteria mapping:**
  - **AC1** ✅ valid token resolves + folds capability/Time Entry/Fresh catalog; admin paths fold existing state.
  - **AC2** ✅ fold determinism across orderings/duplicates, catalog filtering, multi-page rebuild, index rebuild from `Issued`.
  - **AC3** ✅ full invalid set externally indistinguishable: blank, malformed, hashing-throw, unknown-hash, missing-aggregate, hash-mismatch, cross-tenant, missing-Time-Entry, read-throw, revoked, used, expired.
  - **AC4** ✅ fail-closed on missing trusted tenant + explicit Unavailable catalog on read failure.
- **Server.Tests lane:** **403 passed / 0 failed / 0 skipped** (was 395; +8 new cases).

## Verification

```
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/...csproj -warnaserror -m:1 /nr:false
→ 0 Warning(s), 0 Error(s)

DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
→ Total: 403, Errors: 0, Failed: 0, Skipped: 0
```

(`dotnet test` is blocked by local VSTest socket permissions — `SocketException (13): Permission denied` — so the README direct xUnit v3 executable fallback was used, per the story's Debug Log convention.)

## Notes / Deferrals

- The full HTTP-boundary equivalence test (`WebApplicationFactory` / `Mvc.Testing`) remains deferred to **Story 3.7** — the Timesheets module has no runtime host fixtures yet (all read-side readers are `Unavailable` stubs). Service-level no-disclosure equivalence across the AC3 invalid set is already proven by `MagicLinkConfirmationCapabilityCommandServiceTests`.
- Tests are independent (each builds its own scripted gateway/read-model), use deterministic fixed timestamps (no `Date.now`/sleeps/waits), and assert end-state (folded state / freshness), not mock call counts.

## Next Steps

- Run the suite in CI alongside the existing ArchitectureTests / Contracts / Projections / Integration lanes.
- When Story 3.7 lands the runtime host, lift the loader equivalence proofs to the HTTP boundary.
