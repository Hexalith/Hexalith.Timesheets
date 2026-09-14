- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: Codex hooks hard-code this machine path and still call deleted bmad-story-automator.
  evidence: `.codex/hooks.json` Stop/SessionStart commands use `/home/administrator/projects/hexalith/timesheets/...`; the first Stop entry still execs `.agents/skills/bmad-story-automator/scripts/story-automator`, which is not on disk.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: BMAD party-mode, forge-idea, and customization-merge tests do not pin the 6.12 override behavior.
  evidence: Override tests check only name/source; party-mode has no non-string member case; `--project-root` tests stub the resolver so `structural_merge` never runs.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: bmad-loop hook can crash on non-UTF-8 stdin.
  evidence: `json.load(sys.stdin)` in `.bmad-loop/bmad_loop_hook.py` catches JSONDecodeError and ValueError only, not UnicodeDecodeError.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: bmad-loop event filenames are not sanitized for path separators.
  evidence: `_write_event` uses `{ts}-{task_id}-{event_name}.json` with POSIX dir_fd open; a `task_id` containing `..` can walk out of the events directory.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: Copilot agent wrappers do not cover every installed BMAD skill.
  evidence: Updated skills such as bmad-agent-tech-writer and bmad-architecture have no matching `.github/agents` entry after the BMAD installer refresh.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Historical Epic 5 planning artifacts still describe sibling-only Works checkout ownership.
  evidence: `epics.md` Story 5.3, `epic-5-context.md`, and `sprint-change-proposal-2026-07-20.md` still require no Timesheets Works gitlink and default `HexalithWorksRoot` to `../Hexalith.Works` only. Live docs now pin `references/Hexalith.Works`.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: The move-spec review log still describes Story 5.3 as draft or backlog.
  evidence: `spec-move-submodules-to-references.md` BH3/BH4/BH8/BH14 and VG1 were written before this story’s tests and doc sync landed.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Move-spec `DependencyDirectionTests` assertions miss some slash and quote forms of a root Exists probe.
  evidence: The added `Exists('$(MSBuildThisFileDirectory)Hexalith.` substring does not catch `/Hexalith.` or `\\Hexalith.` variants. Those assertions came from the parallel move-spec review.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: The managed bmad:context block is stale relative to the Story 5.3 Works checkout pin.
  evidence: `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` never state the `references/Hexalith.Works` rule, still say README omits Works.Tests, pin the superseded SDK `10.0.302`, and rewrote the shared-baseline sentence. Fixes that edit those agent-context files are deferred.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Move-spec ScaffoldGovernanceTests treat any path starting with `references/` as under references/.
  evidence: `references/../Hexalith.Works` would pass `StartsWith("references/")`. That assertion is a parallel move-spec review edit, not this story’s Works checkout class.

## Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)

- The `bmad:context` block was added to `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` although this story deferred editing those files, and it ships stale: it never states the `references/Hexalith.Works` rule the story exists to pin, says "README omits Works.Tests", pins the superseded SDK `10.0.302`, and rewrites a sentence the shared baseline header declares normalized across the Codex, Claude, and Copilot entry points.
- The `references/Hexalith.EventStore` pin bump changes the AppHost's composed security topology (`AddHexalithEventStoreSecurity` now creates generated secret parameters and injects `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`_PASSWORD` into Keycloak), guarded only by a text read of `Program.cs` in `ScaffoldGovernanceTests`. An Aspire hosting test belongs to a later infrastructure story, since the AppHost is deliberately a minimal scaffold.
- `docs/launch-readiness.md:21` misstates the imported Builds catalog: it claims Dapr `1.18.4`, Aspire Hosting `13.4.6`, `Microsoft.FluentUI.Components` `4.11.6`, while the catalog at both the old (`35c3d1e`) and new (`a32cb42`) pin has `Dapr.AspNetCore` `1.18.5`, `Aspire.Hosting` `13.5.3`, `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.5-26219.1`. Story 5.2 evidence, already wrong at baseline.
- `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json` hardcodes `admin-pass`, `tenant-a-pass`, `tenant-b-pass`, `readonly-pass`, and `no-tenant-pass`, where the upstream file it was copied from uses `__HEXALITH_*_PASSWORD__` substitution placeholders. Pre-existing and outside the diff; the EventStore pin bump widens the drift.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
  summary: Reconcile the overall launch-readiness verdict while Stories 3.6 and 5.1 remain open.
  evidence: The approved 2026-09-12 correction requires `FAIL` until valid-link Magic-Link resolution and final Story 5.1 documentation synchronization complete, while the pre-existing launch record still says `CONCERNS`; final release classification belongs to Story 5.1 rather than this package-baseline story.

## Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)

- `AGENTS.md:92`, `CLAUDE.md:92`, and `.github/copilot-instructions.md:92` still say the `global.json` SDK is `10.0.302`, now two steps behind the approved `10.0.401`. The fix edits agent-context files and the spec's Never clause assigns agent-guidance synchronization to Story 5.1; the existing ledger entries above describe the pre-baseline drift and should be refreshed when 5.1 runs.
- `docs/launch-readiness.md:78` still records the overall release decision as `CONCERNS`, while `_bmad-output/implementation-artifacts/epic-5-context.md:19` (rewritten in this change) and the approved 2026-09-12 course correction both require `FAIL` until Stories 3.6 and 5.1 close. Final release classification belongs to Story 5.1. Note that the same change cites proposal §7 as approval evidence for its prerelease waiver while not honoring §3/§4 of the same document.
- The AppHost SDK `13.5.3` and `AspireUseCliBundle=true` change orchestration-runtime dependency resolution, but no test loads or starts the host: `ScaffoldGovernanceTests` parses the csproj XML and `LaunchReadinessTests` asserts prose. The only runtime proof is a one-off manual `aspire start` smoke run. An `Aspire.Hosting.Testing` lane would pull Docker/Keycloak into the default ArchitectureTests path, so it belongs to the later infrastructure story — the existing entry above covers only the composed security topology and should be widened to cover CLI-bundle resolution.
- Commit `ee76a92` is messaged `build: align SDK and Aspire package baseline` but also rewrites roughly 60 lines of Epic 5 governance text (`epic-5-context.md`). Correcting the message requires a history rewrite.
- Commit `ee76a92` was made directly on `main`; the shared agent guidance says to branch first. Already committed, so the correction is procedural.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)

- A lagging EventStore replica reporting `LatestSequence` equal to the last returned sequence would let `EventStoreMagicLinkConfirmationCapabilityStateLoader.ReadAllEventsAsync` treat a stale prefix as a complete history, folding a Revoked/Used capability as still Issued and leaving the link reusable. Unverified: settled by whether `ReadStreamAsync` guarantees read-your-writes/linearizable reads against the writing actor. Would be high severity if confirmed.
- The stream paging loop (`EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:307`) has no page or event cap, so a gateway returning `IsTruncated` pages indefinitely causes unbounded memory growth and a hung external magic-link request. Not demonstrated reachable with a trusted gateway.
- Non-`Fresh` catalog states (`Stale`, `Rebuilding`, `Degraded`, `Unknown`) are collapsed to `Unavailable` by the loader, losing the explicit freshness vocabulary AC2 requires. Fail-closed behavior is preserved. Resolution depends on how the catalog-freshness decision item is settled.
- The valid-path HTTP acceptance is proven with `ITimesheetsAccessGuard` replaced by `ScriptedAccessGuard` and `IEventStoreGatewayClient` replaced by `ScriptedEventStoreGateway`, so the claim that valid links succeed "in the live host" exceeds the evidence; production `DenyAllTimesheetsTenantAccessValidator`/`DenyAllContributorPartyValidator` still deny. The fail-closed defaults are intentional per CLAUDE.md and cannot change without a story that wires real validators.
- `src/Hexalith.Timesheets.AppHost` declares no `eventstore` resource, so the gateway's default target does not exist in the local topology. Topology changes are forbidden by the spec's Never clause and CLAUDE.md.
- The tenant-wide Activity Type catalog is persisted under the magic-link-scoped key `timesheets:magic-links:activity-type-catalog:{tenant}:v1` while `TenantActivityTypeCatalogProjectionHandler` declares itself canonical writer for the whole tenant catalog, inviting a second divergent writer later. Changing a persisted state key is a data migration, not a direct correction.
- Story artifact and agent context are stale after this change: `3-6-…md` Completion Notes still claim the index "has no live `IDomainProjectionHandler` wiring" and that the loader folds the catalog from a domain-wide (`aggregateId: null`) read — both now false; the File List omits all eight new files; and the CLAUDE.md "Known pitfalls" entry about the unwired `MagicLinkTokenHashCapabilityIndexProjection` is obsolete. The fix edits spec and agent-context files.
- Authorization for the EventStore domain-service route group mapped by `app.UseEventStoreDomainService()` (`/process`, `/replay-state`, `/query`, `/project`, `/project/v2`, the rebuild routes and `/admin/operational-index-metadata`). These are reachable anonymously on the Timesheets host, which registers no authentication or authorization middleware, alongside the anonymous magic-link routes; `MagicLinkConfirmationHttpBoundaryTests.cs:792` shows an anonymous `POST /project/v2` returning `200 OK` while mutating the token-hash index and the trust-bearing catalog read model. Per code-review decision 2 (2026-09-14) the auth scheme belongs to the platform: the Hexalith baseline forbids a domain module re-implementing hosting/DAPR plumbing. Raise upstream against Hexalith.EventStore; `docs/launch-readiness.md` records sidecar/network isolation as the interim control.

- source_spec: none
  summary: Harden projection and loader correctness across the Story 3.6 review patch items that are not part of live-host catalog freshness.
  evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal. Covers ETag-presence-instead-of-value concurrency selection in both projection handlers, the loader's same-sequence equivalence check being weaker than `ProjectionEventReader.Equivalent` with only the first MessageId entering the uniqueness set, null-guard gaps in `ProjectionEventReader` (`Matches<T>` JSON-null `EventTypeName`, null `ProjectionFreshness` in `Merge`/`ParseCursor`), the missing `[EventStoreDomain]` attribute on both handlers that leaves `AddEventStoreDomainTelemetry` registering nothing, the non-`TryAdd` gateway guard in `AddTimesheetsServerKernel`, dead code left by the refactor (`OrderedDistinct`, the first `hasMore` assignment, uncalled `MagicLinkTokenHashCapabilityIndexProjection.Rebuild`), and the bare `"index"` literal / unused logging reference / omitted `ReadModelWritePolicy` logger arguments.

- source_spec: none
  summary: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.
  evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal; code-review decision 3 resolved to option A. `Hexalith.Timesheets.Projections` references `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService` to reach four shapes, inverting layering and satisfying `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` in letter only because it string-matches direct `Include` values. Moves `MagicLinkTokenHashCapabilityIndexEntry`, `MagicLinkTokenHashCapabilityIndexReadModel`, `MagicLinkTokenHashCapabilityIndexProjection`, and `MagicLinkActivityTypeCatalogReadModelAddress`.

- source_spec: none
  summary: Bind the EventStore gateway target and API token through IConfiguration/options instead of process environment variables at registration time.
  evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal; code-review decision 4 resolved to option A. `ResolveDaprHttpEndpoint()` reads `DAPR_HTTP_ENDPOINT`/`DAPR_HTTP_PORT` and `DAPR_API_TOKEN` via `Environment.GetEnvironmentVariable` with the app id hardcoded as `"eventstore"`, so nothing is bindable, overridable per environment, or testable without process mutation; `RuntimeRegistrationTests` swaps process-wide env vars under parallel xUnit collections, and a rotated `DAPR_API_TOKEN` is never picked up after registration.

- source_spec: none
  summary: Record the two Story 3.6 review documentation outcomes in docs/launch-readiness.md.
  evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal. Code-review decision 2 (option C) requires recording sidecar/network isolation as the stated interim control for the unauthenticated EventStore SDK route group; decision 5 (option C) requires recording the `Hexalith.Timesheets.ServiceDefaults` ownership handover to the EventStore SDK, leaving removal to a story that names that project per CLAUDE.md.

- source_spec: none
  summary: Strengthen the Story 3.6 test suite where assertions cannot currently catch the regressions they imply.
  evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal. No test asserts `ReadModelBatchOperation.Concurrency`, so inverting both handler ternaries to always emit `CreateOnly` leaves every test green; the HTTP `"cross-tenant"` case became `null` and is now indistinguishable from `"unknown"`, leaving the external cross-tenant defense on one loader comparison with a single unit test; and the test read-model doubles ignore `storeName`, `Concurrency`, and operation `Kind` while `ScriptedReadModelStore` returns one global version as the ETag for every key.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-14)

- source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md
  summary: Eight prior-round deferred goals re-verified against the current tree and confirmed still accurate; not re-filed.
  evidence: Re-verified during the `eb11472..f38214d` review. ETag-presence-instead-of-value concurrency selection survives in both `FinalizeAsync` methods and no test asserts `ReadModelBatchOperation.Concurrency`, `StoreName` or `Kind`; the loader's same-sequence equivalence check at `EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:373-377` still compares only `EventTypeName`/`SerializationFormat`/`Payload` while `ProjectionEventReader.Equivalent` also compares `MessageId`, and only the first group member's `MessageId` enters the uniqueness set; `Matches<T>` still splits a possibly JSON-null `EventTypeName` and `Merge`/`ParseCursor` still dereference `ProjectionFreshness`; neither handler carries `[EventStoreDomain]` and no Timesheets type derives from `EventStoreAggregate<>`/`EventStoreProjection<>`, so `GetHandlerDomainNames` yields nothing and `AddEventStoreDomainTelemetry` registers no domain; `OrderedDistinct` can no longer filter anything now that `ReadAllEventsAsync` normalizes and de-duplicates before both call sites, the first `hasMore` assignment is always overwritten, `MagicLinkTokenHashCapabilityIndexProjection.Rebuild` has no production caller, and eleven loader fixtures still script the removed domain-wide stream; the bare `"index"` slot literal, the unused `Microsoft.Extensions.Logging.Abstractions` reference and the omitted `ReadModelWritePolicy` logger arguments all remain; the HTTP `"cross-tenant"` case is byte-identical to `"unknown"` because `MagicLinkExternalRequestContext.FromResolvedCapability` derives the request tenant from the capability itself; and `ResolveDaprHttpEndpoint` still reads `DAPR_HTTP_ENDPOINT`/`DAPR_HTTP_PORT`/`DAPR_API_TOKEN` from process environment with fixtures mutating them process-wide under parallel xUnit collections.

- source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md
  summary: The kernel-reference fitness rule cannot observe what Hexalith.Timesheets.Projections pulls in transitively.
  evidence: `DependencyDirectionTests.cs:111-130` reads only direct `Include` attribute values via `ReadIncludeValues`, so the new `Hexalith.EventStore.DomainService` project reference — which carries `FrameworkReference Microsoft.AspNetCore.App` and `PackageReference Dapr.AspNetCore` — enters the kernel closure with `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` still green. Ledger decision 3 (move the four shared shapes to `Hexalith.Timesheets.Contracts` and drop the `Projections`→`Server` reference) removes the leak and is the better fix than adding transitive-closure checks to the rule.

- source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md
  summary: The "Known pitfalls" entry in CLAUDE.md, AGENTS.md and .github/copilot-instructions.md is now false.
  evidence: All three still read "`MagicLinkTokenHashCapabilityIndexProjection` has no projection-host wiring; valid links fail closed until a story wires it". `Program.cs:15-17` now passes `typeof(TimesheetsProjectionsMarker).Assembly` to `AddEventStoreDomainService` and `MagicLinkTokenHashCapabilityIndexProjectionHandler` is the canonical writer, so the entry misdirects the next agent. Deferred because the fix edits agent-context files, which CLAUDE.md requires be kept synchronized as normalized text across all three entry points.
