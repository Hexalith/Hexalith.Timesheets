---
title: 'Close Story 3.6 holistic production-review patches'
type: 'bugfix'
created: '2026-10-05'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ee87e39f9f43e442e5426bb090afbe70be26492a'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Thirteen Story 3.6 review patches remain: request logs expose query tokens, null identifiers throw, and fail-closed guards lack coverage.

**Approach:** Apply the accepted logging filter, harden runtime boundaries, add regressions, and correct documentation. Record evidence for every patch.

## Boundaries & Constraints

**Always:** Preserve opaque external failures, server-side authorization, EventStore persistence, existing tenant-only Activity Type policy, and `.editorconfig`. Capture every log category in privacy assertions. Preserve all historical decisions and deferred work.

**Never:** Add topology, authentication, durable submission, index pruning, dependencies, UI, or submodule changes. Do not claim these patches establish persisted single-use behavior or release readiness; release remains `FAIL`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
|----------|---------------|-------------------|
| Privacy | All four magic-link routes with query tokens | No raw token in any captured log category |
| Paging | Valid issuance prefix followed by empty page below latest sequence or marked truncated | Opaque unavailable bundle; no accepted partial fold |
| Guard | Mixed-case protected path on public/unconfigured listener | 404; index/catalog unchanged |
| Claims | Tenant fallback, actor fallback, conflicting tenant claims | Authorization and loader use identical prioritized context |
| Null identifier | Null capability id | Null result; zero gateway/read-model I/O |
| Projection | Null index Entries; project-scoped Activity Type | Deterministic HandlerFailure; no tenant-catalog contamination |
| Legacy correction | Project scope followed by scope-less rejected correction | Existing project scope retained |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`: reuse logger, claims filter, and admin assertions. Tenant: `tenant_id` → `tenant`; actor: `party_id` → `NameIdentifier`, retaining a tenant claim.
- `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`: reuse `WithPagedStream`, `WithPageTransform`, and `ShouldBeOpaqueFailClosed`. Transform an actual terminal-event page; the fixture skips literal empty pages.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs`: reuse write counters, snapshots, and null-freshness test.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets/Program.cs` — filter `Microsoft.AspNetCore.Hosting.Diagnostics` at Warning before host construction.
- [x] `src/Hexalith.Timesheets/Runtime/InternalSurfaceGuard.cs` — make prefix storage private; preserve case-insensitive matching; describe refusal before endpoint execution.
- [x] `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` — return null for null capability id before context/read access; retain existing paging logic.
- [x] `src/Hexalith.Timesheets.Server/Runtime/ITimesheetsTrustedContextAccessor.cs`, `src/Hexalith.Timesheets.Server/Runtime/UnavailableTimesheetsTrustedContextAccessor.cs`, `src/Hexalith.Timesheets/Runtime/HttpContextTimesheetsTrustedContextAccessor.cs` — add XML type/member/constructor documentation; claims remain evidence for authorization.
- [x] `src/Hexalith.Timesheets.AppHost/Program.cs`, `src/Hexalith.Timesheets.Projections/ReadModelRebuildConcurrency.cs` — correct public endpoint name and limit null-deserialization claim to JSON-null with ETag; malformed JSON throws before concurrency selection.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — remove both log-category restrictions; prove the hosting filter suppresses Information; extend admin parity to fallback/conflicting claims.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` — add `/PROJECT/v2`, `/Process`, `/Admin/operational-index-metadata`; test mixed-case projection refusal on configured public port.
- [x] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` — add null-id/no-I/O test and two paging theory rows with valid entry/index/catalog. Below-latest row advertises latest=2 without truncation, then stalls at cursor=1. Truncated row stalls at cursor=1 with latest=1. Assert opaque bundle and capability cursors `[0,1]`.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` — assert null Entries maps to Failed/HandlerFailure with zero writes; project-scoped delivery leaves absent and existing tenant catalogs untouched.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/TimeEntryEvidenceProjectionTests.cs` — add project-scoped legacy rejected-correction regression beside approved-correction coverage.
- [x] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, `docs/launch-readiness.md` — record all thirteen dispositions and exact verification; align routing/endpoint descriptions; retain unresolved launch gates and truthful story status.

**Acceptance Criteria:**
- Given thirteen patches, when verification finishes, then each has an evidence-backed closure in the story.
- Given existing behavior and deferred decisions, when all six test projects run individually, then no existing test regresses and release limitations remain explicit.

## Implementation Notes

- Loaded both frontmatter context files in full and followed the root-declared Hexalith baseline, persistence rules, repository build/configuration and boundary guidance. `global.json` resolves SDK `10.0.401`; `.editorconfig` requires LF.
- Implemented all eleven execution tasks and all thirteen underlying holistic-review patches; the story contains an evidence row for each and preserves all prior findings/decisions/deferrals.
- All-category privacy now covers valid and denied requests across the four query-token routes. The hosting-category test proves Information suppression and Warning delivery. Claims fallback/conflict tests compare both issue and revoke authorization with the loader's scoped reads and accessor observations.
- Retained existing paging logic. The new rows transform an actual terminal-event page into an empty stalled page and use otherwise-valid entry/index/catalog fixtures; both required weakenings were falsified, with exact source restoration before final verification.
- Scope remains limited to the accepted hardening, tests and documentation. The story/sprint remain in-progress pending the parent workflow's independent review and finalization; release FAIL and all named acceptance gaps remain.
- Parent audit read the baseline diff including this untracked spec. All eleven execution tasks and both acceptance criteria are satisfied; every matrix row has registered coverage in the passing focused/full lanes. All thirteen original findings retain their text and have individual closure evidence.
- Independent review completed all three layers. Two grouped defects were auto-fixed: provider-specific Information overrides are clamped by post-configuring existing logger rules, and issuance denies missing/null identifiers before reads, authorization or generation. Three pre-existing findings were appended to the deferred-work ledger. Story/sprint transition to review; release remains FAIL.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Route | Evidence |
|---|---|---|---|
| Blind 1 | medium | patch | The concrete loader now returns null for a null identifier, and the issue endpoint treats null state as a new capability. Otherwise-valid HTTP issuance was reproduced returning 202 with null id; deny malformed input before loading or token generation. |
| Blind 2 | medium | defer | Revoke and expire dereference `command.CapabilityId.Value` before loading; null bodies reproduce 500. Both dereferences exist unchanged at the baseline, so this is pre-existing management-route hardening. |
| Blind 3 | high | patch | Provider-specific Console Information configuration overrides the new provider-neutral rule and emits query-bearing request logs. Clamp the hosting category after configuration for provider-specific rules as well. |
| Blind 4 | medium | defer | The unchanged test logger discards scopes and exception text although EventStore logging exports scopes. A sentinel probe confirms these observables are missing; this is pre-existing privacy-harness coverage. |
| Blind 5 | maybe-false | defer, unverified medium | The existing malformed-body tests inspect responses rather than diagnostics. No current Warning/Error token leak was demonstrated. Capturing scopes/exceptions and exercising malformed/throwing HTTP paths with query sentinels would settle the suspected disclosure channel. |
| Blind 6 | low | reject | Existing fallback helpers both skip whitespace, and the requested tenant/actor/conflicting rows cover their stated matrix. Extra blank/missing-claim cases would strengthen pre-existing coverage but require additional fixture branches for uncommon claims; no current divergence was shown. |
| Blind 7 | low | reject | Expire currently supplies the same prioritized claim lists to the shared endpoint helper as issue/revoke. No current fallback-order defect was found; adding an expiry-ready fixture for hypothetical future call-site drift exceeds a direct correction. |
| Blind 8 | false | reject | These are adversarial responses for the current hand-written pager, which does not call the SDK validator. The deliberately inconsistent truncated metadata reaches its stall guard; two requests and mutation failures prove the intended boundary is exercised. No well-formed-stream success claim is made for this row. |
| Blind 9 | false | reject | Both live handlers use `ReadModelWritePolicy.UpdateAsync`, whose only persistence call is counted `TrySaveAsync` at the canonical key. These cases either return before that policy or throw before its write; expected-key contents/ETag plus the counter cover every write reachable in these paths. The uncounted SaveAsync helper is not called. |
| Edge 1 | high | patch, grouped with Blind 3 | Provider-specific configuration reproduced Information logs and raw query values despite the category filter. Same defect and correction as Blind 3. |
| Verification 1 | medium | patch, grouped with Blind 1 | The reviewer independently reproduced missing-id issuance returning 202 with null id under the concrete loader and fresh authorized fixture. Same malformed-input defect as Blind 1. |

Review patch closures: `HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration` covers provider defaults/categories and preserves unrelated Debug behavior; `IssuanceWithMissingCapabilityIdIsOpaqueAndPerformsNoProtectedWork` covers omitted/explicit-null JSON with successful valid-issuance control, unchanged persisted snapshots and zero reads, authorization or extra generation. The focused follow-up build and 12/12 selected HTTP cases passed. Parent source/diff audit confirms both corrections; no review loopback was required.

## Verification

- Restore: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`.
- Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
- Run focused changed classes, then all six README xUnit executables individually, including Works.Tests. Performance lanes retain their default skips.
- Verify paging tests reject temporary throw-to-break and removed-continuation mutations; restore source exactly afterward.
- Pre-edit baseline at `ee87e39f9f43e442e5426bb090afbe70be26492a`: focused loader 69/69, projections 97/97, HTTP/guard 31/31 passed using existing Debug executables. These are baseline results, not patch verification.
- Runtime baseline: AppHost started; `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 (port 8080 occupied); `aspire stop` succeeded. Keep this blocker separate from in-process evidence.

### Patch verification (2026-10-05, SDK 10.0.401)

- The prescribed restore exited 0; prescribed Debug/source-reference solution build passed with zero warnings/errors, including the rebuild after mutation restoration.
- Focused direct classes: loader 72/72; projection handler plus Time Entry evidence 101/101; HTTP boundary plus guard 39/39.
- Ran all six README executables individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`: ArchitectureTests 56/56, Contracts.Tests 90/90, Server.Tests 482/482, Projections.Tests 150/150, IntegrationTests 118 total/114 pass/4 default skips, Works.Tests 76/76. Final total: 972 tests, 968 pass, 4 intentional skips, 0 failures.
- Paging theory direct-runner selector: `-method 'Hexalith.Timesheets.Server.Tests.EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.LoadTokenStateAsyncFailsClosedWhenContinuationStalls'`. Baseline 2/2 pass; temporary stalled-cursor throw-to-break mutation: build exit 0, test exit 1 with 2/2 failures; removed `fromSequence < latestSequence` continuation: build exit 0, test exit 1 with 1/2 failure. Loader bytes restored exactly (SHA-256 `641ada70b9cd57693242943a0be78b1d5ebc8e143a1902422c3e2da9a77fca9a`); restored full Server suite passed 482/482.
- Initial Architecture/Integration broad source scans rejected “tokens” in a new Program.cs explanatory comment. Rewording it as “sensitive query values” corrected both; their unchanged full suites passed on rerun.
- The approved pre-edit AppHost result above remains the runtime blocker; this implementation did not claim new passing topology or durable single-use evidence. No dependencies, submodule pointers, authentication or durable submission were changed.

### Final post-review verification

- Parent reran the exact source-reference Debug restore/build commands above: exit 0, zero warnings/errors.
- All six test executables passed individually: ArchitectureTests 56/56; Contracts.Tests 90/90; Server.Tests 482/482; Projections.Tests 150/150; IntegrationTests 122 total, 118 passed, four existing skips; Works.Tests 76/76. Final inventory: 976 total, 972 passed, four intentional skips, zero failures.
- This includes all matrix cases and all four new review-remediation theory cases. Paging mutation evidence remains applicable because the loader bytes are unchanged by the review fixes.
