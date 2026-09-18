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

The repeated 2026-09-14 agent-context findings are consolidated into the canonical open 2026-09-16 Story 3.6 entry below; they do not create additional work items.

## Deferred from: code review of story-3.6 (2026-09-14)

- The repeated agent-context finding is consolidated into the canonical open 2026-09-16 Story 3.6 entry below.
- **Sprint-status header count disagrees with the story.** `_bmad-output/implementation-artifacts/sprint-status.yaml:2` records "2 decisions resolved and applied, 5 patch action items open, 3 deferred" while `3-6-implement-eventstore-backed-magic-link-state-loading.md` carries 24 unchecked `[ ]` review items across rounds. Deferred because the fix edits other spec/tracking artifacts.
- **`RuntimeRegistrationTests` mutate process-wide environment variables.** `tests/Hexalith.Timesheets.Server.Tests/RuntimeRegistrationTests.cs:45-112` sets `DAPR_HTTP_ENDPOINT`, `DAPR_HTTP_PORT` and `DAPR_API_TOKEN` process-wide while xUnit runs test classes in parallel, adding two more writers to a coupling already recorded here. `DAPR_API_TOKEN` is additionally read once at registration time, so a rotated token never takes effect. Deferred because the existing `IConfiguration`-binding ledger item owns the real fix.

## Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)

- source_spec: none
  summary: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.
  evidence: Split from the Story 3.6 remaining-work intent (2026-09-15); the user chose to ship projection/loader correctness plus its falsifying tests first. Re-verified open at `fe82c7d`: `Hexalith.Timesheets.Projections.csproj:13,16` still reference `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService`. Carries the earlier decision-3 entry forward unchanged.

- source_spec: none
  summary: Bind the EventStore gateway target and API token through IConfiguration/options instead of process environment variables at registration time.
  evidence: Split from the Story 3.6 remaining-work intent (2026-09-15). Re-verified open at `fe82c7d`: `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs:49,52,98-100` still call `Environment.GetEnvironmentVariable` for `DAPR_HTTP_ENDPOINT`/`DAPR_HTTP_PORT`/`DAPR_API_TOKEN`, and `RuntimeRegistrationTests` still mutates them process-wide under parallel xUnit collections. Carries the earlier decision-4 entry forward unchanged.

- source_spec: none
  summary: Record the Hexalith.Timesheets.ServiceDefaults ownership handover to the EventStore SDK in docs/launch-readiness.md.
  evidence: Split from the Story 3.6 remaining-work intent (2026-09-15). The decision-2 half of the earlier documentation goal is superseded: `docs/launch-readiness.md:73-88` now records the enforced `InternalSurfaceGuard` port split as a wired control instead of the planned sidecar/network-isolation note. Decision 5 remains open -- `ServiceDefaults` appears nowhere in the readiness record, and removal stays with a story that names that project per CLAUDE.md.

- source_spec: none
  summary: Synchronize Story 3.6 tracking artifacts and the three agent-context entry points with the delivered implementation.
  evidence: Split from the Story 3.6 remaining-work intent (2026-09-15); this is the only remaining goal that lets `3-6-implement-eventstore-backed-magic-link-state-loading` leave `in-progress`. The story's Completion Notes still claim the index has no live `IDomainProjectionHandler` wiring and that the loader folds a domain-wide read, its File List omits the files added after `eb11472`, its review checkboxes read 24 unchecked items that later increments closed, `sprint-status.yaml:2` disagrees with that count, and the "Known pitfalls" entry in `CLAUDE.md`, `AGENTS.md` and `.github/copilot-instructions.md` is false now that `src/Hexalith.Timesheets/Program.cs:15-17` registers the Projections assembly.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Restore loader fold integrity for same-sequence events and remove the inert de-duplication path.
  evidence: Split from that spec at the 2026-09-15 token gate (~2900 tokens) so the Projections layer ships first. `EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:382-385` compares only `EventTypeName`, `SerializationFormat` and `Payload` where `ProjectionEventReader.Equivalent` (`ProjectionEventReader.cs:82-90`) compares eight fields, so two events at one sequence differing only by `MessageId` collapse silently instead of being rejected as ambiguous. `OrderedDistinct` (`:397-411`) is now a pass-through because `:369-394` already normalizes and `:386` throws on repeated MessageIds, leaving `appliedMessageIds` (`:166`, `:219`) write-only. Also covers the degenerate HTTP `"cross-tenant"` case at `MagicLinkConfirmationHttpBoundaryTests.cs:1036`, which maps to `null` and is byte-identical to the `"unknown"` arm at `:1032`, leaving the loader comparison at `:89` on one unit test (`EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs:181`).

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Make the kernel's EventStore gateway registrations independent so a pre-registered client cannot suppress them.
  evidence: Split from that spec at the 2026-09-15 token gate. `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs:47` is the only non-`TryAdd` guard in `AddTimesheetsServerKernel`; a caller that pre-registers just `IEventStoreGatewayClient` silently skips the `EventStoreGatewayClientOptions` configuration at `:49`, the typed/named `HttpClient`, `AddEventStoreDaprServiceInvocation` at `:50-52`, and the `ICommandStatusLocationBuilder` that `AddEventStoreGatewayClient` registers internally. This block is the same code the deferred `IConfiguration`-binding goal rewrites, so the two should be scheduled together.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Settle whether a rebuild plan may carry LastWrite for an existing read-model row that comes back without an ETag.
  evidence: Deferred from that spec's review as unverified. `ReadModelBatchConcurrency`'s remarks state "A missing ETag is never silently translated into last-write behavior", and the index plan's value is merged from `current.Value` through `ReplaceTenant` (`MagicLinkTokenHashCapabilityIndexProjection.cs:75-88`), which carries other tenants' entries, so an unconditional write across the dispatcher's read-to-commit window would lose a concurrent update. The concrete path is refuted today: staging rejects `foreignEnvelope || !SatisfiesConcurrency` (`ReadModelBatchProtocol.cs:471-472`), which blocks envelope-wrapped rows for every mode including Unconditional, and the envelope path (`ResolveVisibleAsync`, `:315-327`) is the only way either shipped store returns an existing row with an empty ETag. What would settle it: an `IReadModelStore` implementation that returns an existing, non-envelope row with an empty ETag.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Guard the magic-link index rebuild path against null-deserialized entry fields the way the catalog path now guards freshness.
  evidence: Deferred from that spec's review as pre-existing. Neither `AccumulateAsync` nor `FinalizeAsync` in `MagicLinkTokenHashCapabilityIndexProjectionHandler` calls `Guarded`, while `ReplaceTenant` reads `pair.Value.Tenant.TenantId` and `CanonicalCandidate` reads the same chain -- all non-nullable by declaration and all populated by a JSON `null` exactly as `ProjectionFreshness` is. The spec's matrix named the one field the catalog owns rather than the defect class, so the sibling handler got neither a guard nor a test.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Guard the catalog rebuild candidate's Items collection against JSON-null the way its freshness is guarded.
  evidence: Deferred from that spec's review as pre-existing. `TenantActivityTypeCatalogProjectionHandler.FinalizeAsync` rewrites only `ProjectionFreshness` inside the guarded block, so a candidate deserialized with `"items":null` passes through untouched and the plan publishes a `Fresh` catalog whose `Items` is null, moving the dereference to every consumer of the read model.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Extend ProjectionEventReader's wire-payload hardening beyond EventTypeName to SerializationFormat and Payload.
  evidence: Deferred from that spec's review as pre-existing and outside the one field the spec named. Once an event matches, `Deserialize` still trusts `SerializationFormat` (a JSON-null format produces the misleading "does not use JSON serialization" message) and `Payload`, and `Equivalent` compares `left.Payload.AsSpan().SequenceEqual(right.Payload)`, where a JSON-null payload reads as an empty span and compares equal to another empty payload -- so two distinct malformed events at one sequence can be treated as equivalent instead of ambiguous.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
  summary: Reconcile the two rebuild handlers' malformed-candidate exception types.
  evidence: Deferred from that spec's review. Bringing the catalog rebuild path under `Guarded` reclassifies a malformed-candidate `InvalidOperationException` from `FromCandidate` as the internal `ProjectionFoldException`, while `MagicLinkTokenHashCapabilityIndexProjectionHandler.FromCandidate` still raises `InvalidOperationException` for the same condition -- asserted as such at `MagicLinkStateProjectionHandlerTests.cs:378`. Two handlers on one `IAsyncDomainSharedProjectionRebuildHandler` contract now report the same condition with two types. No caller depends on either today; `DomainSharedProjectionRebuildDispatcher` catches `Exception` and returns `Indeterminate`/`HandlerFailure` for both.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Decide how loaders should treat unknown future events in authority streams.
  evidence: `EventStoreMagicLinkConfirmationCapabilityStateLoader.Deserialize` returns null for an unrecognized event and folding continues, so a future revocation-like event after otherwise valid state could be ignored. Every current capability and Time Entry event type is recognized; a current producer or planned event with authorization-changing semantics would settle this maybe-false risk.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Add a total read budget if realistic aggregate history can amplify one magic-link request.
  evidence: `ReadAllEventsAsync` pages until the pinned latest sequence is reached and has no total page or event cap. EventStore limits each page and current domain flows keep these aggregates bounded; evidence of a realistically oversized or continuously advancing stream causing request amplification would settle the risk.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Prove or close the stale-snapshot authorization risk across the loader's separate reads.
  evidence: Capability, Time Entry, and catalog state are loaded separately, so a concurrent mutation can occur between snapshots. The default topology is not live and the downstream append/concurrency behavior is outside this chunk; the decisive evidence is whether dispatch can commit based on stale loader state without EventStore optimistic revalidation.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Wire and prove an EventStore resource in the default live AppHost topology.
  evidence: `Hexalith.Timesheets.AppHost/Program.cs` deliberately declares no EventStore resource, so a valid link cannot complete AC1 in the default live topology. `docs/launch-readiness.md` already records this High gap as an infrastructure-owned waiver, and repository policy prevents this chunk from changing topology.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Preserve explicit non-Fresh catalog state only when the no-disclosure bundle can represent it safely.
  evidence: The loader collapses stale, rebuilding, degraded, unknown, absent, and malformed catalogs into the same unavailable bundle even though AC2 names the states explicitly. The story and code already defer this Medium gap because exposing an honest category requires revisiting the approved indistinguishable-response design.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Correct the launch-readiness UI revisit condition to match the FrontComposer policy.
  evidence: `docs/launch-readiness.md:54` says a future UI-bearing story should scaffold `Hexalith.Timesheets.UI` and `.UI.Tests`, while current repository guidance forbids a Timesheets UI project and assigns future UI to FrontComposer. This is a pre-existing Story 5.1 documentation issue outside the loader-core chunk.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading rerun (2026-09-15)

- Unknown future capability or Time Entry authority events are silently ignored after otherwise valid state. Every event emitted by current producers is recognized; a current or planned authorization-changing event would settle whether unknown-event tolerance can make a stale link usable.
- Capability, Time Entry, and catalog are read as separate snapshots without a final capability revision check. Establish whether downstream EventStore dispatch can commit from this stale loaded state without optimistic revalidation; the default topology does not currently provide that live path.
- Non-`Fresh` catalog states collapse to one `Unavailable` bundle instead of preserving the AC2 freshness vocabulary. This real Medium gap remains deferred because preserving individual states requires changing the approved no-disclosure bundle contract.

## Deferred from: build review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-2 (2026-09-16)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Decide whether contributor confirmation remains valid after a Time Entry has already been approved or corrected.
  evidence: `ValidateConfirmationScope` and `TimeEntry.ValidateExternalConfirmation` accept an otherwise-valid recorded contribution without excluding approved or corrected lifecycle state. The stories say confirmation must not itself approve, lock, or correct the entry, but do not define whether those prior transitions disqualify confirmation. An explicit product policy for confirmation relative to approval and correction would settle the risk.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Determine whether capability folds must reject illegal transition sequences found in EventStore history.
  evidence: `MagicLinkCapabilityState.Apply` accepts a later issuance event after a used, revoked, or expired terminal event and thereby reopens the folded capability. Sanctioned writers do not emit that transition. Evidence that the EventStore command boundary can admit such a sequence, or an inventory of one in deployed streams, would establish a reachable integrity defect.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Determine whether Time Entry folds must reject lifecycle events in illegal orders found in EventStore history.
  evidence: `TimeEntryState.Apply` can fold sequences such as a correction while Draft or another Recorded event after recording, although sanctioned writers reject those transitions before emission. Evidence that the EventStore command boundary can admit such sequences, or an inventory of one in deployed streams, would establish a reachable integrity defect.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-16)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Decide whether contributor confirmation must check Activity Type availability the way the display path does.
  evidence: `DescribeAsync` gates on `TryResolveDisplayLabel`, which requires `IsActive && IsAvailableForCapture`, while `ConfirmAsync` receives no `ActivityTypeCatalogReadModel` at all, so `ValidateConfirmationScope` cannot see availability. An Activity Type deactivated after issuance therefore yields GET 403 / POST 200 and confirmation still emits its event. The 2026-09-16 remediation tightened display, confirm, and adjust symmetrically for tenant ownership but left availability asymmetric. Carried as BH-01 / R2-EC-01 / R3-BH-01 / R3-EC-01 across four review loops, each citing a prior ledger entry that did not exist; this is the first entry.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Decide whether the tenant Activity Type catalog must represent per-Project restriction rules for magic-link selection.
  evidence: The magic-link path consumes the tenant catalog, while project restriction availability is computed only by `TenantActivityTypeCatalogProjection.ProjectForProject`. A tenant-owned Activity Type that is restricted for a given Project can therefore still be selected for a Project target through issuance and adjustment. Carried as BH-02 / R2-BH-04 / R3-BH-02 across three review loops, each citing a prior ledger entry that did not exist; this is the first entry.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
  summary: Refresh the agent-context files that still describe the token-hash index as unwired.
  evidence: `CLAUDE.md`, `AGENTS.md`, and `.github/copilot-instructions.md` each still state that `MagicLinkTokenHashCapabilityIndexProjection` "has no projection-host wiring; valid links fail closed until a story wires it". The shipped projection handlers, this story's 2026-09-16 supersession notice, and the new `LaunchReadinessRecordsStructuredMagicLinkOwnershipAndTimingWaivers` fitness test (which asserts the readiness document must not contain "no projection-host wiring") all contradict it. The fix edits agent-context files, which is outside a code review's remit. Consolidated here from repeated findings since 2026-09-13; every agent session loads this as fact about a security-relevant path.

## Deferred from: build planning for Story 3.6 (2026-09-17)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Implement durable, atomic magic-link confirmation and adjustment persistence for unfinished Story 3.3/3.4 behavior.
  evidence: The live POST endpoints return `202 Accepted` from in-memory domain results without calling `IEventStoreGatewayClient.SubmitCommandAsync`, so neither the Time Entry event nor capability-use event is persisted and concurrent reuse is not prevented. The current EventStore gateway accepts one aggregate per submission and exposes no atomic multi-aggregate API; sequential submissions would violate the approved atomicity requirement. A dedicated high-priority remediation must choose a platform coordination or aggregate-boundary design and prove persisted end state, concurrency, replay rejection, and partial-failure safety.

## Deferred from: build review of Story 3.6 review-evidence increment (2026-09-17)

The managed agent-context drift found again in this review points to the canonical open 2026-09-16 Story 3.6 entry above; no duplicate work item is added here.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Remove the launch-readiness instruction to scaffold forbidden Timesheets UI projects.
  evidence: The NFR13 revisit condition still directs a future story to create `Hexalith.Timesheets.UI` and `.UI.Tests`, contrary to repository policy assigning future UI to FrontComposer. This is pre-existing Story 5.1 documentation drift rather than a change caused by the review-evidence increment.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Add automated AppHost runtime smoke evidence for the resolved Aspire and Keycloak package graph.
  evidence: Existing fitness tests inspect AppHost source and project text but do not start the topology, so an Aspire SDK/package incompatibility can compile while `security` or `timesheets` fails at runtime. An automated Aspire testing lane changes package and topology scope excluded by this increment and remains infrastructure-owned.

VG-02 is resolved rather than deferred: the current package-readiness inventory records `Microsoft.NET.Test.Sdk` `18.10.1`; the `18.10.0` occurrences remain only in dated historical planning and review records.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Decide whether authoritative Time Entry folds must reject approved-correction events without a preceding approval transition.
  evidence: `LoadTimeEntryAsync` accepts `TimeEntryApprovedCorrected` after only `TimeEntryRecorded`; sanctioned writers do not emit that sequence, but malformed EventStore history can be folded as approved correction evidence. The behavior predates this increment and needs the broader illegal-transition state-machine decision already identified for Time Entry history.

## Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md (2026-09-17)

The independent review reconfirmed the single AppHost runtime-smoke VG-01 item recorded above: `references/Hexalith.Builds` resolves Aspire `13.5.4` and `Aspire.Hosting.Keycloak` `13.5.4-preview.1.26464.4`; Timesheets fitness tests require the readiness record to distinguish the historical 2026-09-15 smoke from the unverified current graph, but no automated test starts the topology. No duplicate work item is added here.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Decide whether Time Entry aggregate and evidence-projection folds must reject nested adjustment-scope disagreement the way the magic-link loader now does.
  evidence: `EventStoreMagicLinkConfirmationCapabilityStateLoader.LoadTimeEntryAsync` throws when explicit nested adjustment scopes disagree with authoritative lineage, then fail-closes. `TimeEntryState.Apply(TimeEntryAdjustedThroughMagicLink)` and `TimeEntryEvidenceProjection` still take top-level `ActivityTypeScope` and store nested snapshots as-is, so malformed history can publish contradictory evidence. Spec-3 forbade changing those folds; a later integrity story would have to own the fold-level guard.

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
  summary: Decide whether magic-link Time Entry folding must reject correction `PreviousValues` whose recorded scope disagrees with folded state.
  evidence: `LoadTimeEntryAsync` applies `TimeEntryCorrected` and `TimeEntryApprovedCorrected` with no previous-scope check, while the new adjustment guard covers only `TimeEntryAdjustedThroughMagicLink`. Sanctioned writers emit both snapshots or omit both. Spec-2 rejected adding this guard as malformed-history-only; settling it needs the same illegal-history state-machine decision already ledgered for Time Entry folds.

## Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-4.md (2026-09-17)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-4.md`
  summary: Rewrite historical Story 3.6 Completion Notes that still describe the token-hash index as unwired.
  evidence: Completion Notes and the 2026-06-22 senior-review carry-forward still say there is no live `IDomainProjectionHandler` wiring, which contradicts host discovery and the 2026-09-16 supersession in the same file. Pre-existing; this terminal-patches increment did not rewrite historical completion notes.

## Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-18)

Implementation File List chunk (`24a37c1c...307b2d7`, 36 runtime/contracts/server/projection files). Re-verified and not re-filed: DenyAll live-host valid-journey, missing AppHost EventStore resource, loader AC2 freshness collapse, Projections→Server layering move, and kernel gateway/`DaprClient` composition.

- source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
  summary: Confirm whether Aspire publishes the Timesheets internal HTTP endpoint off the pod network when `isExternal` is omitted.
  evidence: `src/Hexalith.Timesheets.AppHost/Program.cs:16` comments that the internal listener is "Declared non-external so it is not published off the pod network", but `:33` calls `WithHttpEndpoint(name: "internal", port: 8081, isProxied: false)` with no `isExternal: false`. `InternalSurfaceGuard` still refuses domain-service paths on any port but the configured one. What would settle it: Aspire 13.5.3 publish/Kubernetes default for an unspecified `isExternal` on a second HTTP endpoint. Unverified medium if that default is external.
