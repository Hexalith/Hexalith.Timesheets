# Test Automation Summary — Story 3.6 (EventStore-Backed Magic-Link State Loading)

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
