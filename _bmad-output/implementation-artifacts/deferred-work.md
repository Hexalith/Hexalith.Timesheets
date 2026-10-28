# Deferred Work

### DW-1: Codex hooks hard-code this machine path and still call deleted bmad-story-automator.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
archived: 2026-10-25

### DW-2: BMAD party-mode, forge-idea, and customization-merge tests do not pin the 6.12 override behavior.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
archived: 2026-10-25

### DW-3: bmad-loop hook can crash on non-UTF-8 stdin.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
archived: 2026-10-25

### DW-4: bmad-loop event filenames are not sanitized for path separators.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
archived: 2026-10-25

### DW-5: Copilot agent wrappers do not cover every installed BMAD skill.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
archived: 2026-10-25

### DW-6: Historical Epic 5 planning artifacts still describe sibling-only Works checkout ownership.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
archived: 2026-10-25

### DW-7: The move-spec review log still describes Story 5.3 as draft or backlog.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
archived: 2026-10-25

### DW-8: Move-spec `DependencyDirectionTests` assertions miss some slash and quote forms of a root Exists probe.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
archived: 2026-10-25

### DW-9: The managed bmad:context block is stale relative to the Story 5.3 Works checkout pin.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
archived: 2026-10-25

### DW-10: Move-spec ScaffoldGovernanceTests treat any path starting with `references/` as under references/.

status: done 2026-10-21
origin: migrated from legacy ledger ("source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
archived: 2026-10-25

### DW-11: The `bmad:context` block was added to `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` although this story deferred editing those files, and it ships stale: it never states the `references/Hexalith.Works` rule the story exists to pin, says "README omits Works.Tests", pins the superseded SDK `10.0.302`, and rewrites a sentence the shared baseline header declares normalized across the Codex, Claude, and Copilot entry points.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-12: The `references/Hexalith.EventStore` pin bump changes the AppHost's composed security topology (`AddHexalithEventStoreSecurity` now creates generated secret parameters and injects `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`_PASSWORD` into Keycloak), guarded only by a text read of `Program.cs` in `ScaffoldGovernanceTests`. An Aspire hosting test belongs to a later infrastructure story, since the AppHost is deliberately a minimal scaffold.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)"), 2026-10-25
location: Program.cs
reason: The `references/Hexalith.EventStore` pin bump changes the AppHost's composed security topology (`AddHexalithEventStoreSecurity` now creates generated secret parameters and injects `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`_PASSWORD` into Keycloak), guarded only by a text read of `Program.cs` in `ScaffoldGovernanceTests`. An Aspire hosting test belongs to a later infrastructure story, since the AppHost is deliberately a minimal scaffold.
status: done 2026-10-25

evidence: The `references/Hexalith.EventStore` pin bump changes the AppHost's composed security topology (`AddHexalithEventStoreSecurity` now creates generated secret parameters and injects `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`_PASSWORD` into Keycloak), guarded only by a text read of `Program.cs` in `ScaffoldGovernanceTests`. An Aspire hosting test belongs to a later infrastructure story, since the AppHost is deliberately a minimal scaffold.

Update 2026-10-25: The compiled security-construction probe now executes actual Keycloak environment callbacks, verifies unique generated secret parameter references for the username/password and application channels without resolving values, then builds/disposes without Start/Run. The 67/67 current compiled probes pass. This closes construction coverage only; runtime smoke remains DW-67/DW-106/DW-166. See [current verification](tests/five-apphost-security-runtime-deferrals.md) and [the approved spec](spec-five-apphost-security-runtime-deferrals.md).

resolution 2026-10-26: Current local Debug/source qualification passes: 199 Python regressions, 67 actual compiled probes and the complete unskipped persisted security/journey/restart run `run-y2ktjsim`. Exact source/asset identity, native NuGet DCP ownership, generated credentials, owned cleanup and shared preservation are recorded in [verification](tests/five-apphost-security-runtime-deferrals.md). Deployed acceptance and launch FAIL remain open.

### DW-13: `docs/launch-readiness.md:21` misstates the imported Builds catalog: it claims Dapr `1.18.4`, Aspire Hosting `13.4.6`, `Microsoft.FluentUI.Components` `4.11.6`, while the catalog at both the old (`35c3d1e`) and new (`a32cb42`) pin has `Dapr.AspNetCore` `1.18.5`, `Aspire.Hosting` `13.5.3`, `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.5-26219.1`. Story 5.2 evidence, already wrong at baseline. (Resolved 2026-10-18 by Story 5.2: the launch-readiness package section no longer states those values; its current verdict rows, the prerelease waiver row, and `architecture.md` version notes now carry catalog versions that `LaunchReadinessTests` derives from the imported catalog.)

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-14: `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json` hardcodes `admin-pass`, `tenant-a-pass`, `tenant-b-pass`, `readonly-pass`, and `no-tenant-pass`, where the upstream file it was copied from uses `__HEXALITH_*_PASSWORD__` substitution placeholders. Pre-existing and outside the diff; the EventStore pin bump widens the drift.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)"), 2026-10-25
location: src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json
reason: `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json` hardcodes `admin-pass`, `tenant-a-pass`, `tenant-b-pass`, `readonly-pass`, and `no-tenant-pass`, where the upstream file it was copied from uses `__HEXALITH_*_PASSWORD__` substitution placeholders. Pre-existing and outside the diff; the EventStore pin bump widens the drift.
status: done 2026-10-25

evidence: `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json` hardcodes `admin-pass`, `tenant-a-pass`, `tenant-b-pass`, `readonly-pass`, and `no-tenant-pass`, where the upstream file it was copied from uses `__HEXALITH_*_PASSWORD__` substitution placeholders. Pre-existing and outside the diff; the EventStore pin bump widens the drift.

Update 2026-10-25: The five fixed-password accounts were replaced with one generated-client account using the platform helper's ${HEXALITH_EVENTSTORE_CLIENT_USERNAME}/${HEXALITH_EVENTSTORE_CLIENT_PASSWORD} contract. Tenant, Timesheets-domain and permission claim mappers remain; credential handoff refuses unresolved values. Realm and compiled-reference checks pass. Actual realm startup/authentication remains in DW-67. See [current verification](tests/five-apphost-security-runtime-deferrals.md) and [the approved spec](spec-five-apphost-security-runtime-deferrals.md).

resolution 2026-10-26: Current local Debug/source qualification passes: 199 Python regressions, 67 actual compiled probes and the complete unskipped persisted security/journey/restart run `run-y2ktjsim`. Exact source/asset identity, native NuGet DCP ownership, generated credentials, owned cleanup and shared preservation are recorded in [verification](tests/five-apphost-security-runtime-deferrals.md). Deployed acceptance and launch FAIL remain open.

### DW-15: Reconcile the overall launch-readiness verdict while Stories 3.6 and 5.1 remain open (owner: Story 5.1).

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-16: `AGENTS.md:92`, `CLAUDE.md:92`, and `.github/copilot-instructions.md:92` still say the `global.json` SDK is `10.0.302`, now two steps behind the approved `10.0.401`. The fix edits agent-context files and the spec's Never clause assigns agent-guidance synchronization to Story 5.1; the existing ledger entries above describe the pre-baseline drift and should be refreshed when 5.1 runs. (Update 2026-10-18: `global.json` now pins `10.0.402`, so the entry points are three steps behind the current pin; the sync remains Story 5.1's.)

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-17: `docs/launch-readiness.md:78` still records the overall release decision as `CONCERNS`, while `_bmad-output/implementation-artifacts/epic-5-context.md:19` (rewritten in this change) and the approved 2026-09-12 course correction both require `FAIL` until Stories 3.6 and 5.1 close. Final release classification belongs to Story 5.1. Note that the same change cites proposal §7 as approval evidence for its prerelease waiver while not honoring §3/§4 of the same document. (Update 2026-10-18: the record now says `Overall release decision: FAIL`; this bullet is superseded by the moved Story 5.1 entry at the top of this section, which keeps the final classification open.)

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-18: The AppHost SDK `13.5.3` and `AspireUseCliBundle=true` change orchestration-runtime dependency resolution, but no test loads or starts the host: `ScaffoldGovernanceTests` parses the csproj XML and `LaunchReadinessTests` asserts prose. The only runtime proof is a one-off manual `aspire start` smoke run. An `Aspire.Hosting.Testing` lane would pull Docker/Keycloak into the default ArchitectureTests path, so it belongs to the later infrastructure story — the existing entry above covers only the composed security topology and should be widened to cover CLI-bundle resolution. (Update 2026-10-18: the Option 2 decision removed the CLI bundle; the AppHost now restores `Aspire.Hosting.Orchestration` and `Aspire.Dashboard.Sdk` from NuGet at `13.6.1`, so no CLI-bundle resolution remains to test. The remaining start-the-host gap stays with the infrastructure-owned automated AppHost runtime smoke entry.)

status: done 2026-10-25
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-19: Commit `ee76a92` is messaged `build: align SDK and Aspire package baseline` but also rewrites roughly 60 lines of Epic 5 governance text (`epic-5-context.md`). Correcting the message requires a history rewrite.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
location: epic-5-context.md
reason: Commit `ee76a92` is messaged `build: align SDK and Aspire package baseline` but also rewrites roughly 60 lines of Epic 5 governance text (`epic-5-context.md`). Correcting the message requires a history rewrite.
status: open

evidence: Commit `ee76a92` is messaged `build: align SDK and Aspire package baseline` but also rewrites roughly 60 lines of Epic 5 governance text (`epic-5-context.md`). Correcting the message requires a history rewrite.

### DW-20: Commit `ee76a92` was made directly on `main`; the shared agent guidance says to branch first. Already committed, so the correction is procedural.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-09-12)"), 2026-10-25
archived: 2026-10-25

### DW-21: A lagging EventStore replica reporting `LatestSequence` equal to the last returned sequence would let `EventStoreMagicLinkConfirmationCapabilityStateLoader.ReadAllEventsAsync` treat a stale prefix as a complete history, folding a Revoked/Used capability as still Issued and leaving the link reusable. Unverified: settled by whether `ReadStreamAsync` guarantees read-your-writes/linearizable reads against the writing actor. Would be high severity if confirmed.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
location: n/a
reason: A lagging EventStore replica reporting `LatestSequence` equal to the last returned sequence would let `EventStoreMagicLinkConfirmationCapabilityStateLoader.ReadAllEventsAsync` treat a stale prefix as a complete history, folding a Revoked/Used capability as still Issued and leaving the link reusable. Unverified: settled by whether `ReadStreamAsync` guarantees read-your-writes/linearizable reads against the writing actor.
status: done 2026-10-26

<a id="deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loading-2026-09-13"></a>
legacy_section: Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)
evidence: A lagging EventStore replica reporting `LatestSequence` equal to the last returned sequence would let `EventStoreMagicLinkConfirmationCapabilityStateLoader.ReadAllEventsAsync` treat a stale prefix as a complete history, folding a Revoked/Used capability as still Issued and leaving the link reusable. Unverified: settled by whether `ReadStreamAsync` guarantees read-your-writes/linearizable reads against the writing actor. Would be high severity if confirmed.

resolution 2026-10-26: The specific lagging-replica premise is superseded by current SDK source routing to the owning AggregateActor and its state-cache metadata barrier. Twelve persisted-owner cases include issuance-only loader evidence against used/revoked/expired owners and corrected scope across recreation. This closes the stated local replay/use mechanism; deployed actor/authority qualification remains under Story 3.6. Exact commands, receipts and scope are in [consistency verification](tests/five-magic-link-consistency-deferrals.md). Launch remains FAIL.

### DW-22: The stream paging loop (`EventStoreMagicLinkConfirmationCapabilityStateLoader.cs:307`) has no page or event cap, so a gateway returning `IsTruncated` pages indefinitely causes unbounded memory growth and a hung external magic-link request. Not demonstrated reachable with a trusted gateway.

status: done 2026-10-25
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-23: Non-`Fresh` catalog states (`Stale`, `Rebuilding`, `Degraded`, `Unknown`) are collapsed to `Unavailable` by the loader, losing the explicit freshness vocabulary AC2 requires. Fail-closed behavior is preserved. Resolution depends on how the catalog-freshness decision item is settled.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-24: The valid-path HTTP acceptance is proven with `ITimesheetsAccessGuard` replaced by `ScriptedAccessGuard` and `IEventStoreGatewayClient` replaced by `ScriptedEventStoreGateway`, so the claim that valid links succeed "in the live host" exceeds the evidence; production `DenyAllTimesheetsTenantAccessValidator`/`DenyAllContributorPartyValidator` still deny. The fail-closed defaults are intentional per CLAUDE.md and cannot change without a story that wires real validators.

status: done 2026-10-25
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-25: `src/Hexalith.Timesheets.AppHost` declares no `eventstore` resource, so the gateway's default target does not exist in the local topology. Topology changes are forbidden by the spec's Never clause and CLAUDE.md.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-26: The tenant-wide Activity Type catalog is persisted under the magic-link-scoped key `timesheets:magic-links:activity-type-catalog:{tenant}:v1` while `TenantActivityTypeCatalogProjectionHandler` declares itself canonical writer for the whole tenant catalog, inviting a second divergent writer later. Changing a persisted state key is a data migration, not a direct correction.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
location: n/a
reason: The tenant-wide Activity Type catalog is persisted under the magic-link-scoped key `timesheets:magic-links:activity-type-catalog:{tenant}:v1` while `TenantActivityTypeCatalogProjectionHandler` declares itself canonical writer for the whole tenant catalog, inviting a second divergent writer later. Changing a persisted state key is a data migration, not a direct correction.
status: done 2026-10-25

evidence: The tenant-wide Activity Type catalog is persisted under the magic-link-scoped key `timesheets:magic-links:activity-type-catalog:{tenant}:v1` while `TenantActivityTypeCatalogProjectionHandler` declares itself canonical writer for the whole tenant catalog, inviting a second divergent writer later. Changing a persisted state key is a data migration, not a direct correction.
resolution_2026_10_25: Canonical tenant catalog ownership now lives in Contracts.Models.ActivityTypes.TenantActivityTypeCatalogReadModelAddress. Writer and loader use it; the old Server facade delegates. The exact v1 physical key/store/slot remain compatibility contracts, avoiding an unapproved data migration. Evidence: six-suite implementation run (2640 passed, five declared skips, zero failures/errors), followed by the passing post-review build, affected bootstrap/release checks and six isolated NuGet consumers; see `tests/five-read-model-boundaries/README.md`.
resolution_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md

### DW-27: Story artifact and agent context are stale after this change: `3-6-…md` Completion Notes still claim the index "has no live `IDomainProjectionHandler` wiring" and that the loader folds the catalog from a domain-wide (`aggregateId: null`) read — both now false; the File List omits all eight new files; and the CLAUDE.md "Known pitfalls" entry about the unwired `MagicLinkTokenHashCapabilityIndexProjection` is obsolete. The fix edits spec and agent-context files.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-28: Authorization for the EventStore domain-service route group mapped by `app.UseEventStoreDomainService()` (`/process`, `/replay-state`, `/query`, `/project`, `/project/v2`, the rebuild routes and `/admin/operational-index-metadata`). These are reachable anonymously on the Timesheets host, which registers no authentication or authorization middleware, alongside the anonymous magic-link routes; `MagicLinkConfirmationHttpBoundaryTests.cs:792` shows an anonymous `POST /project/v2` returning `200 OK` while mutating the token-hash index and the trust-bearing catalog read model. Per code-review decision 2 (2026-09-14) the auth scheme belongs to the platform: the Hexalith baseline forbids a domain module re-implementing hosting/DAPR plumbing. Raise upstream against Hexalith.EventStore; `docs/launch-readiness.md` records sidecar/network isolation as the interim control.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
archived: 2026-10-25

### DW-29: Harden projection and loader correctness across the Story 3.6 review patch items that are not part of live-host catalog freshness.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-30: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
location: n/a
source_spec: none
reason: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal; code-review decision 3 resolved to option A. `Hexalith.Timesheets.Projections` references `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService` to reach four shapes, inverting layering and satisfying `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` in letter only because it string-matches direct `Include` values.
status: done 2026-10-25

summary: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.
evidence: Split from the Story 3.6 remediation intent (2026-09-14) as an independently shippable goal; code-review decision 3 resolved to option A. `Hexalith.Timesheets.Projections` references `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService` to reach four shapes, inverting layering and satisfying `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` in letter only because it string-matches direct `Include` values. Moves `MagicLinkTokenHashCapabilityIndexEntry`, `MagicLinkTokenHashCapabilityIndexReadModel`, `MagicLinkTokenHashCapabilityIndexProjection`, and `MagicLinkActivityTypeCatalogReadModelAddress`.
resolution_2026_10_25: Pure index DTOs and policy options moved to Contracts; the now SDK-bound index helper, reader/builder and stored payload wrappers moved to shared ReadModels. Projections no longer references Server. All 26 relocated public types retain their full names with Server type forwarding; the old four-file proposal was expanded to cover current dependencies. Evidence: six-suite implementation run (2640 passed, five declared skips, zero failures/errors), followed by the passing post-review build, affected bootstrap/release checks and six isolated NuGet consumers; see `tests/five-read-model-boundaries/README.md`.
resolution_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md

### DW-31: Bind the EventStore gateway target and API token through IConfiguration/options instead of process environment variables at registration time.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-32: Record the two Story 3.6 review documentation outcomes in docs/launch-readiness.md.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-33: Strengthen the Story 3.6 test suite where assertions cannot currently catch the regressions they imply.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-13)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-34: Eight prior-round deferred goals re-verified against the current tree and confirmed still accurate; not re-filed.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-14)"), 2026-10-25
source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md
archived: 2026-10-25

### DW-35: The kernel-reference fitness rule cannot observe what Hexalith.Timesheets.Projections pulls in transitively.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-14)"), 2026-10-25
location: DependencyDirectionTests.cs:111-130
source_spec: _bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md
reason: `DependencyDirectionTests.cs:111-130` reads only direct `Include` attribute values via `ReadIncludeValues`, so the new `Hexalith.EventStore.DomainService` project reference — which carries `FrameworkReference Microsoft.AspNetCore.App` and `PackageReference Dapr.AspNetCore` — enters the kernel closure with `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` still green. Ledger decision 3 (move the four shared shapes to `Hexalith.Timesheets.Contracts` and drop the `Projections`→`Server` reference) removes the leak and is the better fix than adding transitive-closure checks to the rule.
status: done 2026-10-25

summary: The kernel-reference fitness rule cannot observe what Hexalith.Timesheets.Projections pulls in transitively.
evidence: `DependencyDirectionTests.cs:111-130` reads only direct `Include` attribute values via `ReadIncludeValues`, so the new `Hexalith.EventStore.DomainService` project reference — which carries `FrameworkReference Microsoft.AspNetCore.App` and `PackageReference Dapr.AspNetCore` — enters the kernel closure with `Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages` still green. Ledger decision 3 (move the four shared shapes to `Hexalith.Timesheets.Contracts` and drop the `Projections`→`Server` reference) removes the leak and is the better fix than adding transitive-closure checks to the rule.
resolution_2026_10_25: Static and evaluated Debug/source and Release/package tests now prohibit Server in the owned Projections/ReadModels closure. The boundary record explicitly retains the required EventStore DomainService composition and its intentional platform runtime closure; removing Server is not claimed to remove that SDK closure. Evidence: six-suite implementation run (2640 passed, five declared skips, zero failures/errors), followed by the passing post-review build, affected bootstrap/release checks and six isolated NuGet consumers; see `tests/five-read-model-boundaries/README.md`.
resolution_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md

### DW-36: The repeated agent-context finding is consolidated into the canonical open 2026-09-16 Story 3.6 entry below.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of story-3.6 (2026-09-14)"), 2026-10-25
archived: 2026-10-25

### DW-37: Sprint-status header count disagrees with the story.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of story-3.6 (2026-09-14)"), 2026-10-25
archived: 2026-10-25

### DW-38: `RuntimeRegistrationTests` mutate process-wide environment variables.

status: done 2026-10-25
origin: migrated from legacy ledger ("Deferred from: code review of story-3.6 (2026-09-14)"), 2026-10-25
archived: 2026-10-25

### DW-39: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.

origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
location: n/a
source_spec: none
reason: Split from the Story 3.6 remaining-work intent (2026-09-15); the user chose to ship projection/loader correctness plus its falsifying tests first. Re-verified open at `fe82c7d`: `Hexalith.Timesheets.Projections.csproj:13,16` still reference `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService`.
status: done 2026-10-25

<a id="deferred-from-bmad-build-scope-split-of-story-36-remaining-work-2026-09-15"></a>
legacy_section: Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)
summary: Move the shared magic-link read-model and address shapes to Hexalith.Timesheets.Contracts and drop the Projections to Server project reference.
evidence: Split from the Story 3.6 remaining-work intent (2026-09-15); the user chose to ship projection/loader correctness plus its falsifying tests first. Re-verified open at `fe82c7d`: `Hexalith.Timesheets.Projections.csproj:13,16` still reference `Hexalith.Timesheets.Server` and `Hexalith.EventStore.DomainService`. Carries the earlier decision-3 entry forward unchanged.
resolution_2026_10_25: Resolved together with DW-30: Projections consumes Contracts and ReadModels independently of Server, including the SDK-dependent helpers and payload wrappers added since the original four-shape proposal. Forwarding, persisted shape and both dependency-mode checks pass. Evidence: six-suite implementation run (2640 passed, five declared skips, zero failures/errors), followed by the passing post-review build, affected bootstrap/release checks and six isolated NuGet consumers; see `tests/five-read-model-boundaries/README.md`.
resolution_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md

### DW-40: Bind the EventStore gateway target and API token through IConfiguration/options instead of process environment variables at registration time.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-41: Record the Hexalith.Timesheets.ServiceDefaults ownership handover to the EventStore SDK in docs/launch-readiness.md.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-42: Synchronize Story 3.6 tracking artifacts and the three agent-context entry points with the delivered implementation.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-43: Restore loader fold integrity for same-sequence events and remove the inert de-duplication path.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-44: Make the kernel's EventStore gateway registrations independent so a pre-registered client cannot suppress them.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-45: Settle whether a rebuild plan may carry LastWrite for an existing read-model row that comes back without an ETag.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-46: Guard the magic-link index rebuild path against null-deserialized entry fields the way the catalog path now guards freshness.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-47: Guard the catalog rebuild candidate's Items collection against JSON-null the way its freshness is guarded.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-48: Extend ProjectionEventReader's wire-payload hardening beyond EventTypeName to SerializationFormat and Payload.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-49: Reconcile the two rebuild handlers' malformed-candidate exception types.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: bmad-build scope split of story 3.6 remaining work (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-projection-write-and-fold-correctness.md`
archived: 2026-10-25

### DW-50: Decide how loaders should treat unknown future events in authority streams.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-51: Add a total read budget if realistic aggregate history can amplify one magic-link request.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-52: Prove or close the stale-snapshot authorization risk across the loader's separate reads.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
reason: Capability, Time Entry, and catalog state are loaded separately, so a concurrent mutation can occur between snapshots. The default topology is not live and the downstream append/concurrency behavior is outside this chunk; the decisive evidence is whether dispatch can commit based on stale loader state without EventStore optimistic revalidation.
status: done 2026-10-26

summary: Prove or close the stale-snapshot authorization risk across the loader's separate reads.
evidence: Capability, Time Entry, and catalog state are loaded separately, so a concurrent mutation can occur between snapshots. The default topology is not live and the downstream append/concurrency behavior is outside this chunk; the decisive evidence is whether dispatch can commit based on stale loader state without EventStore optimistic revalidation.

resolution 2026-10-26: Local processing revalidates current serialized owner facts and terminal history. Persisted actor recreation cases deny stale HTTP/loader evidence without another effect. The approved processing-snapshot policy bounds catalog races; it does not provide cross-aggregate atomicity or latest-source visibility. Story 3.6 deployed qualification remains open. Exact commands, receipts and scope are in [consistency verification](tests/five-magic-link-consistency-deferrals.md). Launch remains FAIL.

### DW-53: Wire and prove an EventStore resource in the default live AppHost topology.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-54: Preserve explicit non-Fresh catalog state only when the no-disclosure bundle can represent it safely.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-55: Correct the launch-readiness UI revisit condition to match the FrontComposer policy.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-15)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-56: Unknown future capability or Time Entry authority events are silently ignored after otherwise valid state. Every event emitted by current producers is recognized; a current or planned authorization-changing event would settle whether unknown-event tolerance can make a stale link usable.

status: done 2026-10-25
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading rerun (2026-09-15)"), 2026-10-25
archived: 2026-10-25

### DW-57: Capability, Time Entry, and catalog are read as separate snapshots without a final capability revision check. Establish whether downstream EventStore dispatch can commit from this stale loaded state without optimistic revalidation; the default topology does not currently provide that live path.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading rerun (2026-09-15)"), 2026-10-25
location: n/a
reason: Capability, Time Entry, and catalog are read as separate snapshots without a final capability revision check. Establish whether downstream EventStore dispatch can commit from this stale loaded state without optimistic revalidation; the default topology does not currently provide that live path.
status: done 2026-10-26

evidence: Capability, Time Entry, and catalog are read as separate snapshots without a final capability revision check. Establish whether downstream EventStore dispatch can commit from this stale loaded state without optimistic revalidation; the default topology does not currently provide that live path.

resolution 2026-10-26: Current owner revalidation and processing-snapshot semantics are proven locally: blocked authorization retains its validated catalog snapshot and a subsequent decision sees the update. Terminal/corrected owner persistence survives actor recreation. No cross-aggregate transactional or latest-source claim is made; deployed acceptance remains open. Exact commands, receipts and scope are in [consistency verification](tests/five-magic-link-consistency-deferrals.md). Launch remains FAIL.

### DW-58: Non-`Fresh` catalog states collapse to one `Unavailable` bundle instead of preserving the AC2 freshness vocabulary. This real Medium gap remains deferred because preserving individual states requires changing the approved no-disclosure bundle contract.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading rerun (2026-09-15)"), 2026-10-25
archived: 2026-10-25

### DW-59: Decide whether contributor confirmation remains valid after a Time Entry has already been approved or corrected.

origin: migrated from legacy ledger ("Deferred from: build review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-2 (2026-09-16)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
reason: `ValidateConfirmationScope` and `TimeEntry.ValidateExternalConfirmation` accept an otherwise-valid recorded contribution without excluding approved or corrected lifecycle state. The stories say confirmation must not itself approve, lock, or correct the entry, but do not define whether those prior transitions disqualify confirmation.
status: done 2026-10-25

<a id="deferred-from-build-review-of-spec-3-6-implement-eventstore-backed-magic-link-state-loading-2-2026-09-16"></a>
legacy_section: Deferred from: build review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-2 (2026-09-16)
summary: Decide whether contributor confirmation remains valid after a Time Entry has already been approved or corrected.
evidence: `ValidateConfirmationScope` and `TimeEntry.ValidateExternalConfirmation` accept an otherwise-valid recorded contribution without excluding approved or corrected lifecycle state. The stories say confirmation must not itself approve, lock, or correct the entry, but do not define whether those prior transitions disqualify confirmation. An explicit product policy for confirmation relative to approval and correction would settle the risk.

resolution_2026_10_25: Closed by the user-approved 2026-10-25 recommendation: otherwise valid scoped external confirmation remains eligible after approval or correction and adds contributor evidence only. Named HTTP lifecycle cases verify unchanged approval, lock, correction values/identifiers, persisted confirmation/use evidence and replay refusal. See spec-five-magic-link-eligibility-deferrals.md and tests/five-magic-link-eligibility-deferrals.md; changed scope still refuses confirmation.

### DW-60: Determine whether capability folds must reject illegal transition sequences found in EventStore history.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: build review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-2 (2026-09-16)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
archived: 2026-10-25

### DW-61: Determine whether Time Entry folds must reject lifecycle events in illegal orders found in EventStore history.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: build review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-2 (2026-09-16)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
archived: 2026-10-25

### DW-62: Decide whether contributor confirmation must check Activity Type availability the way the display path does.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-16)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
reason: `DescribeAsync` gates on `TryResolveDisplayLabel`, which requires `IsActive && IsAvailableForCapture`, while `ConfirmAsync` receives no `ActivityTypeCatalogReadModel` at all, so `ValidateConfirmationScope` cannot see availability. An Activity Type deactivated after issuance therefore yields GET 403 / POST 200 and confirmation still emits its event.
status: done 2026-10-25

<a id="deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loading-2026-09-16"></a>
legacy_section: Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-16)
summary: Decide whether contributor confirmation must check Activity Type availability the way the display path does.
evidence: `DescribeAsync` gates on `TryResolveDisplayLabel`, which requires `IsActive && IsAvailableForCapture`, while `ConfirmAsync` receives no `ActivityTypeCatalogReadModel` at all, so `ValidateConfirmationScope` cannot see availability. An Activity Type deactivated after issuance therefore yields GET 403 / POST 200 and confirmation still emits its event. The 2026-09-16 remediation tightened display, confirm, and adjust symmetrically for tenant ownership but left availability asymmetric. Carried as BH-01 / R2-EC-01 / R3-BH-01 / R3-EC-01 across four review loops, each citing a prior ledger entry that did not exist; this is the first entry.
resolution_note: The preceding evidence is historical. The 2026-09-18 decision selected confirm-as-is: confirmation describe may display the recorded tenant-owned Activity Type after deactivation, while issuance and adjustment describe retain capture-availability checks. Implemented by `887db4718bf611f9ea505fe7500b2c6ef53c27d1` on 2026-10-03; `DescribeAsync` no longer requires those flags and the former GET 403 / successful POST mismatch no longer describes confirmation. Retain this ledger entry for traceability without closing it or re-opening the settled confirmation decision; adjustment policy remains separately deferred below.

resolution_2026_10_25: Closed in this approved five-item batch with current retired-type confirmation proof. The historical resolution_note and its then-current retention instruction are preserved; this dated disposition supersedes that retention decision without reopening confirm-as-is. Display and authoritative commit require fresh, unambiguous tenant-owned recorded-type evidence while permitting retirement. Adjustment policy is resolved separately by DW-73; Project restrictions remain open in DW-63. See tests/five-magic-link-eligibility-deferrals.md.

### DW-63: Decide whether the tenant Activity Type catalog must represent per-Project restriction rules for magic-link selection.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-16)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
reason: The magic-link path consumes the tenant catalog, while project restriction availability is computed only by `TenantActivityTypeCatalogProjection.ProjectForProject`. A tenant-owned Activity Type that is restricted for a given Project can therefore still be selected for a Project target through issuance and adjustment.
status: done 2026-10-26

summary: Decide whether the tenant Activity Type catalog must represent per-Project restriction rules for magic-link selection.
evidence: The magic-link path consumes the tenant catalog, while project restriction availability is computed only by `TenantActivityTypeCatalogProjection.ProjectForProject`. A tenant-owned Activity Type that is restricted for a given Project can therefore still be selected for a Project target through issuance and adjustment. Carried as BH-02 / R2-BH-04 / R3-BH-02 across three review loops, each citing a prior ledger entry that did not exist; this is the first entry.

resolution 2026-10-26: Approved policy is implemented: Project restrictions govern new issuance and adjustment selection, while historical confirmation is retained. The canonical tenant catalog carries source-preserving policy materialization; missing/legacy/incomplete/stale/foreign/malformed/ambiguous evidence denies Project selection. Work targets perform no inferred Project lookup. Existing deployments require a certified complete catalog rebuild. Exact commands, receipts and scope are in [consistency verification](tests/five-magic-link-consistency-deferrals.md). Launch remains FAIL.

### DW-64: Refresh the agent-context files that still describe the token-hash index as unwired.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-16)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-2.md`
archived: 2026-10-25

### DW-65: Implement durable, atomic magic-link confirmation and adjustment persistence for unfinished Story 3.3/3.4 behavior.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: build planning for Story 3.6 (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
archived: 2026-10-25

### DW-66: Remove the launch-readiness instruction to scaffold forbidden Timesheets UI projects.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: build review of Story 3.6 review-evidence increment (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
archived: 2026-10-25

### DW-67: Add automated AppHost runtime smoke evidence for the resolved Aspire and Keycloak package graph.

origin: migrated from legacy ledger ("Deferred from: build review of Story 3.6 review-evidence increment (2026-09-17)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
reason: Existing fitness tests inspect AppHost source and project text but do not start the topology, so an Aspire SDK/package incompatibility can compile while `security` or `timesheets` fails at runtime. An automated Aspire testing lane changes package and topology scope excluded by this increment and remains infrastructure-owned.
status: done 2026-10-26

summary: Add automated AppHost runtime smoke evidence for the resolved Aspire and Keycloak package graph.
evidence: Existing fitness tests inspect AppHost source and project text but do not start the topology, so an Aspire SDK/package incompatibility can compile while `security` or `timesheets` fails at runtime. An automated Aspire testing lane changes package and topology scope excluded by this increment and remains infrastructure-owned.

Update 2026-10-25: The existing isolated runner now has explicit --security-smoke startup, security/eventstore/timesheets readiness, actual generated-credential authentication and scope checks, and owned cleanup/preservation. The 2026-10-25 attempt refused unchanged admission because the foreign Dapr application parties-five-erasure-20261025 was present. Nothing started; no runtime closure is claimed. See [current verification](tests/five-apphost-security-runtime-deferrals.md) and [the approved spec](spec-five-apphost-security-runtime-deferrals.md).

resolution 2026-10-26: Current local Debug/source qualification passes: 199 Python regressions, 67 actual compiled probes and the complete unskipped persisted security/journey/restart run `run-y2ktjsim`. Exact source/asset identity, native NuGet DCP ownership, generated credentials, owned cleanup and shared preservation are recorded in [verification](tests/five-apphost-security-runtime-deferrals.md). Deployed acceptance and launch FAIL remain open.

### DW-68: Decide whether authoritative Time Entry folds must reject approved-correction events without a preceding approval transition.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: build review of Story 3.6 review-evidence increment (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
archived: 2026-10-25

### DW-69: Decide whether Time Entry aggregate and evidence-projection folds must reject nested adjustment-scope disagreement the way the magic-link loader now does.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
archived: 2026-10-25

### DW-70: Decide whether magic-link Time Entry folding must reject correction `PreviousValues` whose recorded scope disagrees with folded state.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-3.md`
archived: 2026-10-25

### DW-71: Rewrite historical Story 3.6 Completion Notes that still describe the token-hash index as unwired.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-4.md (2026-09-17)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-4.md`
archived: 2026-10-25

### DW-72: Confirm whether Aspire publishes the Timesheets internal HTTP endpoint off the pod network when `isExternal` is omitted.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading (2026-09-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-73: Decide whether a magic-link adjustment may move a Time Entry off a retired recorded Activity Type.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
location: MagicLinkConfirmationCapabilityCommandService.cs:246
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
reason: `DescribeAdjustmentAsync` passes `requireCaptureAvailability: true` for the recorded `capabilityState.ActivityTypeId`. `AdjustAsync` (`MagicLinkConfirmationCapabilityCommandService.cs:246`) checks only the requested `command.ActivityTypeId`, and `ValidateAdjustmentScope` does not tie the two together.
status: done 2026-10-25

<a id="deferred-from-code-review-of-spec-3-6-implement-eventstore-backed-magic-link-state-loading-6md-2026-10-03"></a>
legacy_section: Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)
legacy_context: Increment `887db47^..887db47` (confirmation display and host verification patches).
summary: Decide whether a magic-link adjustment may move a Time Entry off a retired recorded Activity Type.
evidence: `DescribeAdjustmentAsync` passes `requireCaptureAvailability: true` for the recorded `capabilityState.ActivityTypeId`. `AdjustAsync` (`MagicLinkConfirmationCapabilityCommandService.cs:246`) checks only the requested `command.ActivityTypeId`, and `ValidateAdjustmentScope` does not tie the two together. After deactivation the adjust GET returns the opaque 403, while a POST naming another active tenant type returns 202. Pre-existing: `887db47` changed only confirmation display.

resolution_2026_10_25: Closed by the user-approved 2026-10-25 recommendation: adjustment may replace a retired recorded type with an active type allowed by the supplied catalog capture gates. Display and submission require valid historical recorded-type evidence; selecting the retired type again refuses without partial domain writes. HTTP persisted previous/adjusted evidence and replay controls pass. Project-specific restriction evidence remains open under DW-63. See tests/five-magic-link-eligibility-deferrals.md.

### DW-74: Reconcile the launch-readiness package waiver rows with the current imported catalog, and scope the fitness pin to those rows.

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
archived: 2026-10-25

### DW-75: Capture the cause of the 2026-10-03 AppHost smoke in which the Timesheets resource did not stay up.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
location: src/Hexalith.Timesheets.AppHost/Program.cs:22
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
reason: `aspire wait timesheets` exited 18; `aspire describe` showed Timesheets `Finished` and security `Unhealthy`. No console log, exit code or exception was recorded.
status: done 2026-10-26

summary: Capture the cause of the 2026-10-03 AppHost smoke in which the Timesheets resource did not stay up.
evidence: `aspire wait timesheets` exited 18; `aspire describe` showed Timesheets `Finished` and security `Unhealthy`. No console log, exit code or exception was recorded. `src/Hexalith.Timesheets.AppHost/Program.cs:22` still states that the host "comes up and fails closed". This extends the existing AppHost runtime-smoke entry (2026-09-18 section, "Add automated AppHost runtime smoke evidence…") and does not replace it.

resolution 2026-10-26: Superseded historical diagnosis. The exact October 3 cause remains unknown because no console exception or application-exit receipt survives. Archived DW-82 separately records the October 4 port-8080 collision and exit 134; the later public-port override fixes that demonstrated conflict, and October 26 local qualification supplies successor startup/journey proof. No cause is retroactively attributed to October 3. See [reconciliation](tests/five-local-runtime-evidence-deferrals.md#historical-startup-reconciliation).

### DW-76: Register an authentication scheme so the host's trusted-context accessor can receive tenant and actor claims in a deployment.

origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
location: src/Hexalith.Timesheets/Program.cs
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
reason: Neither `src/Hexalith.Timesheets/Program.cs` nor `AddEventStoreDomainService` calls `AddAuthentication`/`UseAuthentication`. `HttpContextTimesheetsTrustedContextAccessor` therefore reads an anonymous principal, and administrator issue/revoke fail closed in a deployment.
status: done 2026-10-25

summary: Register an authentication scheme so the host's trusted-context accessor can receive tenant and actor claims in a deployment.
evidence: Neither `src/Hexalith.Timesheets/Program.cs` nor `AddEventStoreDomainService` calls `AddAuthentication`/`UseAuthentication`. `HttpContextTimesheetsTrustedContextAccessor` therefore reads an anonymous principal, and administrator issue/revoke fail closed in a deployment. The 2026-10-03 HTTP tests inject claims through a test `IStartupFilter`. The concern pairs with the upstream route-group authorization entry above. Update 2026-10-09: workload authentication now runs through the SDK and Timesheets host, but supplies no Timesheets `tenant_id` or Party actor claim. The remaining management-identity work is cross-referenced in the public-route compatibility entry below.
resolution_note: Superseded diagnosis / open identity handoff (2026-10-22), verified in `spec-close-five-historical-story-tracking-deferrals.md`. The SDK registers workload authentication and the host invokes authentication/authorization middleware; existing credential tests reject missing/forged assertions. This closes only the historical no-scheme/no-middleware diagnosis. Production tenant/Party identity remains open under “Supply trusted tenant and actor identity to production magic-link management routes”; fixture claims and local Development bindings are not deployed identity evidence. See `tests/five-historical-story-tracking-deferrals.md`.

resolution_2026_10_25: Closed only for the superseded no-scheme/no-middleware diagnosis after rerunning composed HTTP credential regressions. The host authentication/authorization middleware and SDK workload scheme already exist. Missing/forged credentials are rejected, while production tenant/Party identity remains open in DW-113; fixture claims do not close it. Original evidence and resolution_note are preserved. See tests/five-magic-link-eligibility-deferrals.md.

### DW-77: Reconcile the 37 unchecked Story 3.6 `[Review][Patch]` items before the story is marked done.

status: done 2026-10-04
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
archived: 2026-10-25

### DW-78: Refresh the agent-context SDK version and build instruction.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md (2026-10-03)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-6.md`
archived: 2026-10-25

### DW-79: Preserve the external Works reference's Configuration so the bare Timesheets solution build selects the intended dependency mode.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: build guidance review patches for Story 3.6 (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md`
archived: 2026-10-25

### DW-80: Restore a working `dotnet test` path for the Timesheets test projects under SDK `10.0.401`.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md`
archived: 2026-10-25

### DW-81: Extend the agent-context refresh entry above ("Refresh the agent-context SDK version and build instruction") to cover the test runner, now that the Build decision is made.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md`
archived: 2026-10-25

### DW-82: Resolve the fixed Timesheets AppHost public port 8080 colliding with other Hexalith AppHosts.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-7.md`
archived: 2026-10-25

### DW-83: Bring test method names in line with the baseline PascalCase rule.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-8.md (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-8.md`
archived: 2026-10-25

### DW-84: Make the AppHost endpoint comment use the declared endpoint name.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-8.md (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-8.md`
archived: 2026-10-25

### DW-85: Semantic analysis of escaped or computed configuration keys and escaped C# property identifiers remains outside the bounded source fitness check.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: alternate-configuration and literal-parsing patches for Story 3.6 (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-9.md`
archived: 2026-10-25

### DW-86: Perform the deployed magic-link Activity Type ownership inventory and approved remediation before rollout.

origin: migrated from legacy ledger ("Deferred from: historical review reconciliation for Story 3.6 (2026-10-04)"), 2026-10-25
location: docs/launch-readiness.md
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-10.md`
reason: The tenant-only issuance, adjustment and loader policy is implemented, but no deployed capability or TimeEntry history access was available during this build. `docs/launch-readiness.md` retains the release owner’s obligation to inventory affected histories, obtain authorization for correction, revoke/reissue affected capabilities, and explicitly accept or separately remediate legacy shapes.
status: open

<a id="deferred-from-historical-review-reconciliation-for-story-36-2026-10-04"></a>
legacy_section: Deferred from: historical review reconciliation for Story 3.6 (2026-10-04)
summary: Perform the deployed magic-link Activity Type ownership inventory and approved remediation before rollout.
evidence: The tenant-only issuance, adjustment and loader policy is implemented, but no deployed capability or TimeEntry history access was available during this build. `docs/launch-readiness.md` retains the release owner’s obligation to inventory affected histories, obtain authorization for correction, revoke/reissue affected capabilities, and explicitly accept or separately remediate legacy shapes. The original historical review item is now marked Defer for this remaining deployment work; no migration-free claim is made.

### DW-87: Record correlation-safe projection transport-failure outcomes without logging exception payloads or protected material.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: historical review reconciliation for Story 3.6 (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-10.md`
archived: 2026-10-25

### DW-88: Decide whether to retain the test-only static magic-link index Rebuild helper or retire its unsupported surface.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: historical review reconciliation for Story 3.6 (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-10.md`
archived: 2026-10-25

### DW-89: Complete store-component isolation and optimistic-concurrency enforcement in the projection and HTTP read-model doubles.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: historical review reconciliation for Story 3.6 (2026-10-04)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-10.md`
archived: 2026-10-25

### DW-90: State the status intent of future build specs as "in-progress until finalization" so reviews stop re-raising the settled finalization question.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-91: Replace the drifted `docs/launch-readiness.md` line pointers in the spec-6 waiver-reconciliation entry with the row names.

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-92: Authorize admin issue/revoke/expire requests before the loader replays capability streams or reads the tenant catalog.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-93: Reconcile the agent guidance "Leave `AddTimesheetsServerKernel` fail-closed" with the spec-mandated EventStore loader registration in the kernel.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-94: Give the magic-link loader a correlation-safe internal outcome channel so dependency outages and integrity failures are distinguishable from invalid links.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-95: Establish how the token-hash index and tenant catalog are populated for histories that predate handler deployment.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-96: Confirm that unfenced shared rebuilds cannot drop live index or catalog writes made between aggregate enumeration and FinalizeAsync.

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
location: MagicLinkTokenHashCapabilityIndexProjectionHandler.cs:152
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
reason: Unverified; medium if confirmed. Index `FinalizeAsync` (`MagicLinkTokenHashCapabilityIndexProjectionHandler.cs:152`) calls `ReplaceTenant`, which replaces every entry of the tenant with the rebuild candidate.
status: done 2026-10-26

summary: Confirm that unfenced shared rebuilds cannot drop live index or catalog writes made between aggregate enumeration and FinalizeAsync.
evidence: Unverified; medium if confirmed. Index `FinalizeAsync` (`MagicLinkTokenHashCapabilityIndexProjectionHandler.cs:152`) calls `ReplaceTenant`, which replaces every entry of the tenant with the rebuild candidate. Catalog `FinalizeAsync` (`TenantActivityTypeCatalogProjectionHandler.cs:154`) writes only the candidate's items. A link issued, or an Activity Type changed, after enumeration but before Finalize's read would be dropped, or overwritten and marked `Fresh`. Settle it by checking whether EventStore pauses named ordinary deliveries for non-epoch `IAsyncDomainSharedProjectionRebuildHandler`s during a session. The SDK's fenced `IAsyncDomainSharedProjectionEpochHandler` (`references/Hexalith.EventStore/docs/guides/named-projection-authoring.md`, "Fenced shared projections") is the documented answer if it does not.

resolution 2026-10-26: The catalog now uses the SDK tenant-scoped epoch/catch-up path, alongside the existing global candidate-index epoch. Building fences owner-sequenced deliveries with retryable backpressure; accepted pending work survives cancellation/reconstruction. Local tests cover add/rename/deactivate/restriction changes, retry and malformed persisted policy. Certified deployed inventory/continuity, obsolete-writer fencing and shared-rebuild acceptance remain open under Story 3.6. Exact commands, receipts and scope are in [consistency verification](tests/five-magic-link-consistency-deferrals.md). Launch remains FAIL.

### DW-97: Declare the Timesheets public magic-link listener external in the deployed app model, or correct the AppHost comment that says it is.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-98: Bound the cross-tenant magic-link token-hash index: prune terminal capabilities and size or shard the single key.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-99: Redact query-string values from request logs in the shared EventStore ServiceDefaults.

status: done 2026-10-24
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-100: Reject null capability identifiers on revoke and expire before dereferencing them.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-14 independent review (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md`
archived: 2026-10-25

### DW-101: Capture exported log scopes and exception text in the HTTP privacy test harness.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-14 independent review (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md`
archived: 2026-10-25

### DW-102: Verify malformed-body and exception-path diagnostics cannot disclose query values (unverified, medium if confirmed).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-14 independent review (2026-10-05)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md`
archived: 2026-10-25

### DW-103: Require the capability id, contributor, Time Entry id and audit source in magic-link issuance validation.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md (2026-10-08)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md`
archived: 2026-10-25

### DW-104: Refresh the CLAUDE.md `timesheets` block's SDK pin.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md (2026-10-08)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md`
archived: 2026-10-25

### DW-105: Integrate Timesheets public capability routes and authenticated internal HTTP fixtures with the current EventStore domain-service security contract.

status: done 2026-10-09
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-15 verification and review (2026-10-08)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-15.md`
archived: 2026-10-25

### DW-106: Configure required internal credentials for a live Timesheets AppHost runtime smoke.

origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-15 verification and review (2026-10-08)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-15.md`
reason: On 2026-10-09, `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 because `APP_API_TOKEN` and JWT Authority/SigningKey were missing. Owner: AppHost runtime/security configuration.
status: done 2026-10-26

summary: Configure required internal credentials for a live Timesheets AppHost runtime smoke.
evidence: On 2026-10-09, `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 because `APP_API_TOKEN` and JWT Authority/SigningKey were missing. Owner: AppHost runtime/security configuration. This is distinct from the earlier package-graph smoke entry and must be closed with a credentialed live startup and persisted-topology evidence.

Update 2026-10-25: Generated application-channel/signing checks and optional generated-realm authentication are now part of the existing opt-in persisted journey. The 2026-10-25 security attempt stopped at preflight before startup; the default live xUnit skip and compiled construction proof do not qualify stored record/issue/index/use/replay/restart outcomes on the current graph. See [current verification](tests/five-apphost-security-runtime-deferrals.md) and [the approved spec](spec-five-apphost-security-runtime-deferrals.md).

resolution 2026-10-26: Current local Debug/source qualification passes: 199 Python regressions, 67 actual compiled probes and the complete unskipped persisted security/journey/restart run `run-y2ktjsim`. Exact source/asset identity, native NuGet DCP ownership, generated credentials, owned cleanup and shared preservation are recorded in [verification](tests/five-apphost-security-runtime-deferrals.md). Deployed acceptance and launch FAIL remain open.

### DW-107: Refresh Story 5.2 launch-readiness catalog observations after the prior Builds update.

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-15 verification and review (2026-10-08)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-15.md`
archived: 2026-10-25

### DW-108: Make the launch-readiness full-suite fitness assertion check current evidence instead of a historical zero-failure sentence.

status: done 2026-10-09
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-08)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`
archived: 2026-10-25

### DW-109: Replace substring-based numeric privacy exclusions with assertions that distinguish business values from permitted diagnostic correlation and timestamp fields.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-16 independent review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-16.md`
archived: 2026-10-25

### DW-110: Refresh the launch posture in the agent entry points' `timesheets` block.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: code review of spec-3-6-implement-eventstore-backed-magic-link-state-loading-16.md (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-16.md`
archived: 2026-10-25

### DW-111: Check protected magic-link material in HTTP response headers as well as bodies.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-17 independent review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-17.md`
archived: 2026-10-25

### DW-112: Check protected material in early non-403 magic-link HTTP responses before asserting the expected status.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 spec-17 independent review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-17.md`
archived: 2026-10-25

### DW-113: Supply trusted tenant and actor identity to production magic-link management routes.

origin: migrated from legacy ledger ("Deferred from: public-route compatibility review (2026-10-09)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-restore-public-capability-routes.md`
reason: EventStore workload authentication yields a workload caller and operation, but no Timesheets `tenant_id` or Party claim. The management HTTP success fixture adds those claims through test-only `MagicLinkFixtureClaimsTransformation`; production has no equivalent.
status: open

<a id="deferred-from-public-route-compatibility-review-2026-10-09"></a>
legacy_section: Deferred from: public-route compatibility review (2026-10-09)
summary: Supply trusted tenant and actor identity to production magic-link management routes.
evidence: EventStore workload authentication yields a workload caller and operation, but no Timesheets `tenant_id` or Party claim. The management HTTP success fixture adds those claims through test-only `MagicLinkFixtureClaimsTransformation`; production has no equivalent. Its default access guard fails closed. This continues the spec-6 authentication entry above: workload authentication now runs, while Timesheets tenant and Party claims remain unavailable in production. Define and test a supported server-side identity source before claiming live issuance or revocation.

### DW-114: Give magic-link management endpoints a route-specific workload policy.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: public-route compatibility review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-restore-public-capability-routes.md`
archived: 2026-10-25

### DW-115: Add the missing server-side authorization decision to magic-link expiry before durable submission.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: public-route compatibility review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-restore-public-capability-routes.md`
archived: 2026-10-25

### DW-116: Set cache and referrer controls on public token-bearing magic-link responses.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: public-route compatibility review (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-restore-public-capability-routes.md`
archived: 2026-10-25

### DW-117: Refresh the managed repository agent context for the current launch posture and magic-link projection wiring.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: Story 3.6 public-route review reconciliation (2026-10-09)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-18.md`
archived: 2026-10-25

### DW-118: Decide how public magic-link routes should handle unexpected loader or command-service exceptions.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: fail-closed Story 3.6 review (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-close-remaining-acceptance-gaps.md`
archived: 2026-10-25

### DW-119: Wire and measure token-hash index projection readiness before treating a newly issued link as immediately resolvable.

origin: migrated from legacy ledger ("Deferred from: fail-closed Story 3.6 review (2026-10-10)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
reason: The production projection handler is discovered, and local HTTP fixtures deliver through the mapped projection route. A token returned after committed issuance may still be looked up before the rebuildable index advances; that visibility delay is unmeasured in a deployed topology.
status: open

summary: Wire and measure token-hash index projection readiness before treating a newly issued link as immediately resolvable.
evidence: The production projection handler is discovered, and local HTTP fixtures deliver through the mapped projection route. A token returned after committed issuance may still be looked up before the rebuildable index advances; that visibility delay is unmeasured in a deployed topology. A live issue-to-use journey would settle end-to-end readiness.
update_2026_10_10: The original unwired-index observation was superseded by the discovered projection handler; the remaining item is deployed visibility timing.

### DW-120: Add a production EventStore recording writer for Time Entry owner streams before live magic-link acceptance.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: fail-closed Story 3.6 review (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
archived: 2026-10-25

### DW-121: Wire production capability and Time Entry read-model projection handlers for stored magic-link events.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: fail-closed Story 3.6 review (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
archived: 2026-10-25

### DW-122: Resolve magic-link helper collision cases before broadening production command routing.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: fail-closed Story 3.6 review (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
archived: 2026-10-25

### DW-123: Decide whether `ExistingTimeEntry` magic links are usable, or stop issuing them.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
archived: 2026-10-25

### DW-124: Define the authorization principal for system-initiated magic-link expiry (medium, unverified).

origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-10)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-durable-magic-link-submission.md`
reason: With a null `Actor`, `MagicLinkEventStoreDomainProcessor.ProcessTransitionAsync` builds the management context from `envelope.UserId`, which is the workload identity when the assertion binds no actor (`:275`). The production kernel is `DenyAll`, so whether a concrete access guard denies system expiry or matches a same-named Party is unknown.
status: done 2026-10-25

summary: Define the authorization principal for system-initiated magic-link expiry (medium, unverified).
evidence: With a null `Actor`, `MagicLinkEventStoreDomainProcessor.ProcessTransitionAsync` builds the management context from `envelope.UserId`, which is the workload identity when the assertion binds no actor (`:275`). The production kernel is `DenyAll`, so whether a concrete access guard denies system expiry or matches a same-named Party is unknown. The concrete management/system-expiry authorization design settles it; see the existing `Expire` guard entry above.

resolution_2026_10_25: Closed by explicit principal separation: actorless expiry requires IMagicLinkSystemExpiryAuthorizer for the gateway-verified workload and authoritative tenant/capability/entry/target scope; the kernel and compatibility constructor default to denial. Explicit Party actors retain the management gate. Name-collision, forged/missing evidence, default denial, authorized persistence and actor-recreation controls pass. No deployed provider or runtime acceptance is claimed. See tests/five-magic-link-eligibility-deferrals.md.

### DW-125: Re-run vulnerable, deprecated, and transitive package audits against the current 13.6.1 catalog graph.

status: done 2026-10-18
origin: migrated from legacy ledger ("Deferred from: code review of 3-6-implement-eventstore-backed-magic-link-state-loading.md (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-126: Decide and enforce a deadline for magic-link stored-event readback after terminal status.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: Story 3.6 review of polling and projection scope (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`
archived: 2026-10-25

### DW-127: Bind magic-link receipt events to the submitted command's correlation and causation identity.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: Story 3.6 review of polling and projection scope (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`
archived: 2026-10-25

### DW-128: Verify complete prior-value audit snapshots for magic-link adjustments.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: Story 3.6 review of polling and projection scope (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`
archived: 2026-10-25

### DW-129: Enforce checkpoint tenant scope consistently in Timesheets read-model projections.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: Story 3.6 review of polling and projection scope (2026-10-10)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`
archived: 2026-10-25

### DW-130: Reconcile an uncertain committed magic-link use across separate HTTP retries.

origin: migrated from legacy ledger ("Deferred from: Story 3.6 review of polling and projection scope (2026-10-10)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md`
reason: Public endpoints pass a new `HttpContext.TraceIdentifier` on each request. The durable service keeps a message ID stable only within one request, so a later retry after an opaque committed outcome can use a new ID and receive a replay denial.
status: done 2026-10-26

summary: Reconcile an uncertain committed magic-link use across separate HTTP retries.
evidence: Public endpoints pass a new `HttpContext.TraceIdentifier` on each request. The durable service keeps a message ID stable only within one request, so a later retry after an opaque committed outcome can use a new ID and receive a replay denial. Define an idempotency or status-reconciliation policy without exposing capability state.

resolution 2026-10-26: Optional Idempotency-Key binds the complete resolved intent and token hash. Authorized retained-key retries verify the original stored command status and exact use/effect pair, including original audit time and adjustment prior values; mismatched keys, token, payload and terminal/no-receipt attempts remain opaque. Persisted HTTP race/recovery and recreated-service regressions pass. See [verification](tests/five-retry-recovery-tooling-deferrals.md).

### DW-131: Commit and pin the EventStore-owned AppPort and metadata changes in a later authorized repository update.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: local Aspire/Dapr topology review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-local-aspire-dapr-topology.md`
archived: 2026-10-25

### DW-132: Inventory deployed read-model components and rebuild or migrate prior magic-link index and catalog data before deployment.

origin: migrated from legacy ledger ("Deferred from: local Aspire/Dapr topology review (2026-10-11)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-local-aspire-dapr-topology.md`
reason: Local preflight found no Timesheets Dapr sidecar or usable prior store, while the shared read-model addresses now name `timesheets-readmodels`. Any deployed `statestore` data or component wiring is unobserved; the release decision remains FAIL pending its separate inventory and journey.
status: open

summary: Inventory deployed read-model components and rebuild or migrate prior magic-link index and catalog data before deployment.
evidence: Local preflight found no Timesheets Dapr sidecar or usable prior store, while the shared read-model addresses now name `timesheets-readmodels`. Any deployed `statestore` data or component wiring is unobserved; the release decision remains FAIL pending its separate inventory and journey.
followup_note: Open acceptance retained 2026-10-23 by `spec-consolidate-five-operational-followups.md`. This task includes full certified source histories predating handler deployment even when no read-model rows exist, population and verification of both token-hash index and tenant catalog before traffic, and documentation of the chosen replay/rebuild/migration procedure and its verification in the deployment runbook. Applicable hosting modes, actual component wiring and prior statestore content remain unqualified. No automatic replay or operational acceptance is established; see `tests/five-operational-followups.md`.

### DW-133: Include keyed-only domains in the legacy operational metadata path when that path must serve them.

status: done 2026-10-24
origin: migrated from legacy ledger ("Deferred from: local Aspire/Dapr topology review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-local-aspire-dapr-topology.md`
archived: 2026-10-25

### DW-134: Detect colliding local AppHost identities and Redis database use before running topology probes on shared developer machines.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: local Aspire/Dapr topology review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-local-aspire-dapr-topology.md`
archived: 2026-10-25

### DW-135: Reconcile an uncertain committed magic-link use across separate HTTP retries.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: Story 3.6 local persisted journey review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-20.md`
archived: 2026-10-25

### DW-136: Verify deployed read-model component wiring and migration independently of the opt-in local journey.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: Story 3.6 local persisted journey review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-20.md`
archived: 2026-10-25

### DW-137: Establish independently verified legacy owner delivery continuity and pending-journal recovery before enabling the v1 migration in a real environment.

origin: migrated from legacy ledger ("Deferred from: Story 3.6 local persisted journey review (2026-10-11)"), 2026-10-25
location: n/a
source_spec: `spec-3-6-migrate-v1-magic-link-index-to-v7.md`
reason: The index inventory contains candidates, not authoritative owner event sequences. SDK ReadOwnerCheckpointAsync preserves existing checkpoints independently of generation; JournalAsync returns DeliveryGap for sequence 2 when acceptedSequence is zero both before and after index migration.
status: open

summary: Establish independently verified legacy owner delivery continuity and pending-journal recovery before enabling the v1 migration in a real environment.
evidence: The index inventory contains candidates, not authoritative owner event sequences. SDK ReadOwnerCheckpointAsync preserves existing checkpoints independently of generation; JournalAsync returns DeliveryGap for sequence 2 when acceptedSequence is zero both before and after index migration. Existing legacy checkpoint/capture absence is not repaired by candidate reconstruction. Verify every retained owner's current SDK sequence/provenance from certified histories and complete source coverage through the actual EventStore handoff mechanisms, including the previously recorded four pending deliveries. Real inventory/authority/enforcement evidence is absent here; never infer or seed these checkpoints from unique-only legacy index rows. Story 3.6 and release remain FAIL.

### DW-138: Direct AppHost starts with bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 enable development fixture authorization while retaining shared component defaults.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 local persisted journey review (2026-10-11)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-isolate-live-dapr-fixture-storage.md`
archived: 2026-10-25

### DW-139: The deferred bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 startup gap is closed by a separate scoped increment.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: AppHost fixture startup hardening (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-harden-live-dapr-fixture-startup.md`
archived: 2026-10-25

### DW-140: Verify whether any deployed direct legacy shard must be rejected before v7 bootstrap.

origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
reason: The bootstrap checks selected legacy shards and the direct v1 single root. No released direct-shard writer is established here; deployed key-layout inspection or historical writer evidence would settle whether a direct shard can be present.
status: done 2026-10-25

<a id="deferred-from-final-review-of-story-36-materialization-2026-10-13"></a>
legacy_section: Deferred from: final review of Story 3.6 materialization (2026-10-13)
summary: Verify whether any deployed direct legacy shard must be rejected before v7 bootstrap.
evidence: The bootstrap checks selected legacy shards and the direct v1 single root. No released direct-shard writer is established here; deployed key-layout inspection or historical writer evidence would settle whether a direct shard can be present.
resolution_2026_10_25: Both fresh v7 bootstrap paths now conservatively refuse any persisted direct legacy shard, including empty, malformed and JSON-null rows. Forty passing regressions cover both paths, boundary shards, selected-shard early return, failures/cancellation, retry, bounded concurrency and preserved legacy/recovery state. No deployed layout was inventoried; deployed inventory and obsolete-writer fencing remain separate open acceptance under DW-132 and Story 3.6. Evidence: six-suite implementation run (2640 passed, five declared skips, zero failures/errors), followed by the passing post-review build, affected bootstrap/release checks and six isolated NuGet consumers; see `tests/five-read-model-boundaries/README.md`.
resolution_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md

### DW-141: Recover a committed external recording when its submit response is lost.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-142: Verify sealed v6 migration resume against a changed source or damaged staged root.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-143: Bind deployed migration authorization to the intended operator as well as its certified operation.

origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
reason: The endpoint requires authentication, while the authority port receives operation and receipt identifiers without ClaimsPrincipal. No permitted production authority is installed; review the eventual provider and ingress operator policy before enabling the route.
status: open

summary: Bind deployed migration authorization to the intended operator as well as its certified operation.
evidence: The endpoint requires authentication, while the authority port receives operation and receipt identifiers without ClaimsPrincipal. No permitted production authority is installed; review the eventual provider and ingress operator policy before enabling the route.

Update 2026-10-27: Both internal migration endpoints now require a separately registered, default-deny principal/operation/evidence binding before migration IO. Local mapped-route tests prove cross-caller replay refusal, independent certification and preserved stored contents. No production provider is installed; intended-operator/ingress policy and deployed acceptance remain open. This is non-closing local evidence for DW-143. See [local follow-ups](tests/remaining-local-deferred-followups.md).

### DW-144: Retire obsolete same-generation capability projection fragments after repeated large-view updates.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-145: Establish subscriber-driven projection delivery independently of manual mapped-route dispatch.

origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
reason: The isolated journey explicitly invokes `/project/v2` after reading stored events. Its 1/1 pass proves the composed mapped route and private pub/sub wiring, while automatic deployed subscription and shared rebuild acceptance remain unchecked.
status: open

summary: Establish subscriber-driven projection delivery independently of manual mapped-route dispatch.
evidence: The isolated journey explicitly invokes `/project/v2` after reading stored events. Its 1/1 pass proves the composed mapped route and private pub/sub wiring, while automatic deployed subscription and shared rebuild acceptance remain unchecked.

### DW-146: Validate the complete selected v6 shard in the local Python candidate diagnostic.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-147: Distinguish selected-format read failure from an absent v7 format in the local index diagnostic.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-148: Put compiled AppHost fixture-control refusal probes in a routine verification lane.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: final review of Story 3.6 materialization (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md`
archived: 2026-10-25

### DW-149: The local recording lost-response follow-up from the final materialization review is implemented.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: Recording response-loss recovery (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-22.md`
archived: 2026-10-25

### DW-150: Extend the local diagnostic sanitizer to redact OTLP/API-key headers before retaining or displaying monitoring output.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: Story 3.6 deployed qualification planning (2026-10-13)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-23.md`
archived: 2026-10-25

### DW-151: Address deferred projection storage and migration hardening in separate increments.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: diagnostic credential-redaction scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-152: Address deferred BMAD hook and test-coverage maintenance in separate increments.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: diagnostic credential-redaction scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-153: Refresh stale repository guidance and reconcile launch documentation separately.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: diagnostic credential-redaction scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-154: The deferred local OTLP/API-key diagnostic sanitization gap is closed.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: Local diagnostic telemetry and API-key redaction (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-redact-diagnostic-telemetry-credentials.md`
archived: 2026-10-25

### DW-155: Harden the deferred BMAD hooks in a separate increment.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: projection checkpoint scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-156: Refresh stale repository guidance in a separate increment.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: projection checkpoint scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-157: The deferred cross-tenant checkpoint freshness gap is closed across Timesheets projection folds.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: Projection checkpoint tenant isolation (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-enforce-projection-checkpoint-tenant-scope.md`
archived: 2026-10-25

### DW-158: Shared-state verification now refuses active baselines and reports the specific changed services without exposing state keys or values.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: Shared-state preservation admission and diagnostics (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-fix-shared-state-preservation-checks.md`
archived: 2026-10-25

### DW-159: Diagnose the EventStore process-stop timeout that prevents the isolated journey's restart assertion.

status: done 2026-10-21
origin: migrated from legacy ledger ("Resolution: Shared-state preservation admission and diagnostics (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-fix-shared-state-preservation-checks.md`
archived: 2026-10-25

### DW-160: Harden BMAD hooks and remove obsolete hook references in a separate increment.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: EventStore restart timeout scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-161: Address projection storage and migration hardening in separate increments.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: EventStore restart timeout scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-162: Refresh synchronized repository guidance in a separate increment.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: EventStore restart timeout scope selection (2026-10-18)"), 2026-10-25
source_spec: none
archived: 2026-10-25

### DW-163: Resolve the upstream braces/micromatch semantic-release advisory chain before merging or activating Timesheets CI/CD.

status: done 2026-10-24
origin: migrated from legacy ledger ("Deferred from: EventStore restart timeout scope selection (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-implement-timesheets-ci-cd.md`
archived: 2026-10-25

### DW-164: Align the existing Works EventStore-version forwarding with the consuming source-build catalog in a separate build increment.

status: done 2026-10-24
origin: migrated from legacy ledger ("Deferred from: EventStore restart timeout scope selection (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-direct-isolated-eventstore-execution.md`
archived: 2026-10-25

### DW-165: The selected local restart timeout now has a reviewed and verified direct-execution correction.

status: done 2026-10-22
origin: migrated from legacy ledger ("Resolution: isolated EventStore restart timeout (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-direct-isolated-eventstore-execution.md`
archived: 2026-10-25

### DW-166: Prove that the `Aspire.AppHost.Sdk` `13.6.1` AppHost with NuGet-backed orchestration starts DCP, the dashboard, and its resources, including the DCP-owned compiled gateway.

origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)"), 2026-10-25
location: scripts/run_isolated_magic_link_dapr.py
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
reason: Story 5.2 moved the AppHost from the CLI-bundle DCP to NuGet `Aspire.Hosting.Orchestration.linux-x64`/`Aspire.Dashboard.Sdk.linux-x64` `13.6.1` (`AspireUseCliBundle=false`). No test starts the host, the 2026-10-18 `scripts/run_isolated_magic_link_dapr.py` journey refused at preflight because foreign Dapr apps were running, and the direct-process proof later published in Timesheets `9523d94983a346d866246a2c45c3515b1bc54580` ran on the CLI-bundle DCP.
status: done 2026-10-26

<a id="deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-10-18"></a>
legacy_section: Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)
summary: Prove that the `Aspire.AppHost.Sdk` `13.6.1` AppHost with NuGet-backed orchestration starts DCP, the dashboard, and its resources, including the DCP-owned compiled gateway.
evidence: Story 5.2 moved the AppHost from the CLI-bundle DCP to NuGet `Aspire.Hosting.Orchestration.linux-x64`/`Aspire.Dashboard.Sdk.linux-x64` `13.6.1` (`AspireUseCliBundle=false`). No test starts the host, the 2026-10-18 `scripts/run_isolated_magic_link_dapr.py` journey refused at preflight because foreign Dapr apps were running, and the direct-process proof later published in Timesheets `9523d94983a346d866246a2c45c3515b1bc54580` ran on the CLI-bundle DCP. Its source worktree was based on Timesheets `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63`, both with uncommitted implementation changes; implementation was later published in EventStore `9b66153515687463fc1b1e929f8b0e73d8145e2c`. Publication revisions are not the tested source identities. The `_bmad-output/implementation-artifacts/evidence/3-6/20261018-direct-eventstore-local-proof/historical-live/direct-process-proof.json` receipt covers the earlier 128.350 s run; the final 131.536 s journey is retained in `_bmad-output/implementation-artifacts/evidence/3-6/20261018-direct-eventstore-local-proof/post-review/verification.json`. Rerun the isolated journey once the shared placement is free, with `HexalithEventStoreVersion=3.125.1` set for the Debug source-reference build and in the script's environment (as the proof published in Timesheets `9523d94983a346d866246a2c45c3515b1bc54580` did; `scripts/run_isolated_magic_link_dapr.py` forces the Debug source graph but takes that property only from the caller) until the Works forwarding entry closes, and confirm the compiled gateway's parent is still a fixture-bound `dcp`/`dcpctrl` process; the automated lane remains with the existing "automated AppHost runtime smoke evidence" entry. Update 2026-10-20 (Story 5.2 round-9 review): `3.125.1` is superseded. At `929ea22c977385590d77d95f73af86e8b051c4fc` the Works default and the catalog both selected EventStore `3.126.0`, so forcing `3.125.1` would pin an older EventStore than both. Set `HexalithEventStoreVersion` to the catalog's EventStore version, and only when the Works default differs from it. At this review a foreign Projects AppHost holding Dapr app ID `eventstore` and five other Dapr applications were running, so the isolated journey's preflight would still refuse.

Update 2026-10-25: The --security-smoke runner now rebuilds the matching Debug/source AppHost after idle admission, captures source/build identities, requires actual fixture-bound NuGet DCP/dashboard processes and dashboard HTTP readiness, and retains the existing direct-DCP compiled-gateway replacement/exit proof. The 2026-10-25 foreign-Dapr refusal prevented this runtime lane; historical receipts remain historical. See [current verification](tests/five-apphost-security-runtime-deferrals.md) and [the approved spec](spec-five-apphost-security-runtime-deferrals.md).

resolution 2026-10-26: Current local Debug/source qualification passes: 199 Python regressions, 67 actual compiled probes and the complete unskipped persisted security/journey/restart run `run-y2ktjsim`. Exact source/asset identity, native NuGet DCP ownership, generated credentials, owned cleanup and shared preservation are recorded in [verification](tests/five-apphost-security-runtime-deferrals.md). Deployed acceptance and launch FAIL remain open.

### DW-167: Qualify the release-gate `Build` and `Tests (full suite)` rows and the README/`docs/ci-cd.md` source-reference test instructions with the `HexalithEventStoreVersion` requirement.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-168: Reconcile the launch record's classification vocabulary (`implemented`, `waived`, `post-v1`, `blocked`) with the approved `implemented/unfinished/waived/post-v1` dispositions.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-169: Point Story 5.1's version ground truth at the re-baselined Story 5.2 package state (owner: Story 5.1).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-170: Fix the agent entry points' "Running and verifying" flow, which runs `bin/Debug` test executables that its own bare restore/build never produces (owner: Story 5.1).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-18)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-171: Correct the agent entry points and the `dotnet test` runner entry, which still say the repository has no `.github/workflows`, and the stale `README.md` build and test lines (owner: Story 5.1).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-19)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-172: Refresh the release-gate `Build` and `Tests (full suite)` rows, which still call the round-20 runs "latest" (owner: Story 5.1).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-19)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-173: Correct the stale "Current Timesheets HEAD pins EventStore `3099cb27`" claims in the durable confirm/adjust row and the overall release decision (owner: Story 5.1).

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: code review of spec-5-2-reconcile-package-currency-and-platform-dependency-versions (2026-10-20)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
archived: 2026-10-25

### DW-174: Reassess catalog-owned EventStore `3.125.1 -> 3.126.0` package drift through the Builds/platform owner.

status: done 2026-10-20
origin: migrated from legacy ledger ("Deferred from: Story 5.2 round-7 patch verification review (2026-10-20)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2.md`
archived: 2026-10-25

### DW-175: Run the installed BMAD party-mode and forge-idea regression suites in CI.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: review of BMAD customization override regression coverage (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-pin-bmad-customization-override-behavior.md`
archived: 2026-10-25

### DW-176: Harden the streamed magic-link index partition decoder independently of legacy shared-candidate decoding.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: review of five projection rebuild integrity closures (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-projection-rebuild-deferrals.md`
archived: 2026-10-25

### DW-177: Name the standalone kernel read dependencies explicitly in the synchronized managed agent guidance.

status: done 2026-10-21
origin: migrated from legacy ledger ("Deferred from: review of five runtime-guidance handoffs (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-runtime-guidance-deferrals.md`
archived: 2026-10-25

### DW-178: Align local index diagnostic readiness with the selected epoch lifecycle and pending delivery state.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: review of five developer-tooling and diagnostic closures (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-tooling-diagnostic-deferrals.md`
archived: 2026-10-25

### DW-179: Reconcile omitted-scope legacy adjustment delivery between direct folds and persisted evidence handlers.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: review of five replay-integrity closures (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-replay-integrity-deferrals.md`
archived: 2026-10-25

### DW-180: Align authoritative and direct evidence approved-correction decision attribution with the persisted handler fence.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: review of five replay-integrity closures (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-replay-integrity-deferrals.md`
archived: 2026-10-25

### DW-181: Align authoritative and direct evidence rejected-correction attribution with the persisted handler fence.

status: done 2026-10-22
origin: migrated from legacy ledger ("Deferred from: review of five replay-integrity closures (2026-10-21)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-replay-integrity-deferrals.md`
archived: 2026-10-25

### DW-182: Assess complete SDK typed epoch and scope-boundary validation for the local selected-index diagnostic.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: review of five magic-link operability closures (2026-10-22)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-close-five-magic-link-operability-deferrals.md`
archived: 2026-10-25

### DW-183: Abort an interrupted v6 migration when selected-source format/root/single metadata becomes invalid before resumed validation.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: review of five magic-link operability closures (2026-10-22)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-five-magic-link-runtime-deferrals.md`
archived: 2026-10-25

### DW-184: Establish safe v6 migration abort recovery when pending deliveries encounter corrupt selected metadata.

origin: migrated from legacy ledger ("Deferred from: review of five magic-link safety deferrals (2026-10-23)"), 2026-10-25
location: n/a
source_spec: `_bmad-output/implementation-artifacts/spec-five-magic-link-safety-deferrals.md`
reason: Review B1 traced the unchanged normal FoldCatchUpAsync through SDK AbortAsync/CatchUpAsync: journaled issuance rereads corrupt selected format/root/single metadata and refuses it, retaining the journal in Aborting. The same baseline abort path is reachable when selected metadata becomes invalid after Begin.
status: open

<a id="deferred-from-review-of-five-magic-link-safety-deferrals-2026-10-23"></a>
legacy_section: Deferred from: review of five magic-link safety deferrals (2026-10-23)
summary: Establish safe v6 migration abort recovery when pending deliveries encounter corrupt selected metadata.
evidence: Review B1 traced the unchanged normal FoldCatchUpAsync through SDK AbortAsync/CatchUpAsync: journaled issuance rereads corrupt selected format/root/single metadata and refuses it, retaining the journal in Aborting. The same baseline abort path is reachable when selected metadata becomes invalid after Begin. The new empty-journal retry tests prove release only when abort can drain safely; they do not authorize deleting corrupt metadata, discarding journaled events or bypassing integrity checks. Define and verify the certified source-repair/catch-up procedure before claiming pending-journal recovery or deployed readiness.

resolution 2026-10-26: Default-denied original-layout certification and complete unchanged v6 source guards permit only an exact SDK-reserved metadata repair. Immutable original corrupt evidence and ETags survive interruption; conflicting repair, catch-up and abort closeout refuse while reserved. Normal journal drain releases the existing abort fence without promoting v7 or inventing owner checkpoints. Format/root/single and malformed-shape recovery, partial delete, restart, changed-source refusal and second-epoch cases pass. Production certification and deployed acceptance remain unavailable. See [verification](tests/five-retry-recovery-tooling-deferrals.md).

publication update 2026-10-26: SDK implementation c613e55aefd0650c1e64ed0580c8495657585a89 is pushed. The complete Timesheets wiring and regression patch is implemented, independently reviewed and tested, but held in evidence/five-retry-recovery-tooling-deferrals/certified-repair-pending.patch.gz because no published SDK package contains the API. Exact-source SDK CI still fails on pre-existing missing Tenants Routing declarations. This item stays open until that release gate clears, the consuming package pin advances and the module patch is applied/pushed. Bounded large-inventory qualification and production certification remain separate gates.

### DW-185: Resume an interrupted v6 migration abort through an operation-and-inventory-authorized recovery path.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: review of five magic-link safety deferrals (2026-10-23)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-five-magic-link-safety-deferrals.md`
archived: 2026-10-25

### DW-186: Qualify candidate-index and catalog read deadlines separately from authority-history allowances.

status: done 2026-10-23
origin: migrated from legacy ledger ("Deferred from: review of five magic-link safety deferrals (2026-10-23)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-five-magic-link-safety-deferrals.md`
archived: 2026-10-25

### DW-187: Remove duplicate gateway app-channel parameter registration before current-graph Timesheets AppHost runtime qualification.

status: done 2026-10-24
origin: migrated from legacy ledger ("Deferred from: review of five magic-link safety deferrals (2026-10-23)"), 2026-10-25
source_spec: `_bmad-output/implementation-artifacts/spec-five-local-apphost-boundary-deferrals.md`
archived: 2026-10-25

### DW-188: Assess the unrelated fast-uri, ip-address, and undici npm release-tooling advisories in a separate dependency increment.

origin: migrated from legacy ledger ("Deferred from: five build and hosting deferrals advisory refresh (2026-10-24)"), 2026-10-25
location: evidence/five-build-and-hosting-deferrals/20261024/npm-audit.json
source_spec: `_bmad-output/implementation-artifacts/spec-five-build-and-hosting-deferrals.md`
reason: Fresh root `npm audit --json` on 2026-10-24 exits 1 with three vulnerable packages (fast-uri and ip-address moderate; undici high), while the original braces GHSA-vfj7-8cjw-p6xm was withdrawn on 2026-10-23 and is absent. Exact advisory URLs, dependency paths and suggested fixes are retained in `evidence/five-build-and-hosting-deferrals/20261024/npm-audit.json`.
status: done 2026-10-26

<a id="deferred-from-five-build-and-hosting-deferrals-advisory-refresh-2026-10-24"></a>
legacy_section: Deferred from: five build and hosting deferrals advisory refresh (2026-10-24)
summary: Assess the unrelated fast-uri, ip-address, and undici npm release-tooling advisories in a separate dependency increment.
evidence: Fresh root `npm audit --json` on 2026-10-24 exits 1 with three vulnerable packages (fast-uri and ip-address moderate; undici high), while the original braces GHSA-vfj7-8cjw-p6xm was withdrawn on 2026-10-23 and is absent. Exact advisory URLs, dependency paths and suggested fixes are retained in `evidence/five-build-and-hosting-deferrals/20261024/npm-audit.json`. Owner: CI/CD release tooling. Review compatible updates and rerun lockfile audit/signatures through that owner; this advisory correction changes no dependency, lockfile, publish gate, or launch verdict.

resolution 2026-10-26: Affected nonbundled dependencies were updated within their existing ranges. Because current npm tarballs retain vulnerable bundled children, a dedicated integrity-pinned two-package install repairs only ip-address 10.7.5 and undici 6.29.1; root postinstall and explicit CI/release steps cover normal and ignored-script installs. Actual installed versions match both locks; final audit is zero, dependency tree is valid, and registry signatures pass. Direct tooling pins and scanners are unchanged. See [verification](tests/five-retry-recovery-tooling-deferrals.md).

### DW-189: Reconcile the 15 incoming EventStore fixture findings in the tracked reusable-content secret guard without weakening its checks.

origin: migrated from legacy ledger ("Deferred from: integrated upstream validation of five build and hosting deferrals (2026-10-24)"), 2026-10-25
location: tests/five-build-and-hosting-deferrals.md
source_spec: `_bmad-output/implementation-artifacts/spec-five-build-and-hosting-deferrals.md`
reason: Integrated baseline `6e927633d920eafcefa2635a124810b541264489` contains all 15 reported lines unchanged in Admin UI, PayloadProtection and Sample test fixtures. The full Server executable completed 5,757 tests with one failed guard; the owned privacy-test literal was then replaced by a generated per-test marker, and focused scanning retains only the 15 incoming lines.
status: done 2026-10-26

<a id="deferred-from-integrated-upstream-validation-of-five-build-and-hosting-deferrals-2026-10-24"></a>
legacy_section: Deferred from: integrated upstream validation of five build and hosting deferrals (2026-10-24)
summary: Reconcile the 15 incoming EventStore fixture findings in the tracked reusable-content secret guard without weakening its checks.
evidence: Integrated baseline `6e927633d920eafcefa2635a124810b541264489` contains all 15 reported lines unchanged in Admin UI, PayloadProtection and Sample test fixtures. The full Server executable completed 5,757 tests with one failed guard; the owned privacy-test literal was then replaced by a generated per-test marker, and focused scanning retains only the 15 incoming lines. Exact paths, baseline fingerprints and final broad/focused results are retained by `tests/five-build-and-hosting-deferrals.md` and its dated receipts. Owner: Hexalith.EventStore test/security maintainers. This is a pre-existing integrated-baseline verification blocker, not a passing broad gate; do not add scanner exclusions merely to suppress it.

resolution 2026-10-26: Integrated upstream 80993d367f8bcafb3e6eda97568ebf0825dd1821 already replaces all 15 selected credential-shaped literals with generated fixture values. Admin/Sample target lanes pass 7 each and policy lifecycle passes 22; the unchanged full tracked-content guard passes 1/1 in a disposable focused compilation against the normal EventStore root, including newly staged SDK sources. Full Server.Tests compilation remains blocked separately by upstream CatalogConstruction CS0234 for missing sibling Tenant routing types; no exclusions or scanner weakening were applied. See [verification](tests/five-retry-recovery-tooling-deferrals.md).

### DW-190: Qualify the combined fresh-v7 bootstrap read cost against its delivery deadline.

status: done 2026-10-26
origin: review of five read-model boundary deferrals, 2026-10-25
source_spec: _bmad-output/implementation-artifacts/spec-five-read-model-boundary-deferrals.md
location: src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs
reason: Unverified medium latency risk: refusing direct legacy layouts adds a direct single-root and 256 bounded shard reads before selected-layout inspection. Peak concurrency/cancellation are verified, but no current deployed combined-path latency acceptance is established.
evidence: Blind review B10. Measure the combined direct and selected bootstrap path with controlled read delays and the actual delivery cancellation/deadline, then include it in current-graph runtime qualification. No timeout failure has been demonstrated and no runtime acceptance is claimed; deployment inventory and obsolete-writer fencing remain required.

resolution 2026-10-26: Local combined bootstrap qualification is complete: both paths measure all direct/selected reads and metadata, nine logical/raw/snapshot/cancellation/recovery cases pass, and current-source run-5b2g61ov completes the persisted journey/restart with identical source hashes, owned cleanup and matching shared preservation. The deployed latency remainder is explicitly retained as open DW-194; this closure grants no deployed acceptance. See [verification](tests/five-local-runtime-evidence-deferrals.md).

### DW-191: Verify ownership of the dashboard socket answering the local health probe.

status: done 2026-10-26
origin: review of five AppHost security/runtime deferrals, 2026-10-26
source_spec: _bmad-output/implementation-artifacts/spec-five-apphost-security-runtime-deferrals.md
location: scripts/run_isolated_magic_link_dapr.py
evidence: The previous checker validated PID/start, restored executable ancestry, process URL and direct HTTP health, but did not map the socket inode to the process. Positive kernel socket-inode ownership evidence and an actual DCP run are required.

resolution 2026-10-26: Actual current-source run-5b2g61ov binds the NuGet dashboard to two kernel listener sockets and sends /health to verified numeric loopback before/after identical PID/start/inode checks. Native IPv4/IPv6/mapped, foreign/shared listeners, changed identity and poisoned-localhost regressions pass. See [verification](tests/five-local-runtime-evidence-deferrals.md).

### DW-192: Bind local qualification to the evaluated MSBuild dependency roots.

status: done 2026-10-26
origin: review of five AppHost security/runtime deferrals, 2026-10-26
source_spec: _bmad-output/implementation-artifacts/spec-five-apphost-security-runtime-deferrals.md
location: scripts/run_isolated_magic_link_dapr.py
evidence: The runner resolved the default EventStore source executable while the consuming graph supported explicit root overrides. The approved local increment scopes this fixture to the evaluated default graph and refuses alternate roots before resource startup; ordinary build override support remains unchanged. Verification must compare the actual evaluated graph and hashed execution inputs with receipts.

resolution 2026-10-26: The fixture admits only its evaluated default source graph before resource creation; all eight root-setting deletion mutations are detected. Current-source qualification retains 19 graph nodes, 278 unique evaluated import fingerprints, 926 execution/restore hashes, participating repository and imported Builds identities, with identical before/after snapshots. Ordinary build root overrides are unchanged. See [verification](tests/five-local-runtime-evidence-deferrals.md).

### DW-193: Use direct HTTP transport for credential-bearing local journey calls.

status: done 2026-10-26
origin: review of five AppHost security/runtime deferrals, 2026-10-26
source_spec: _bmad-output/implementation-artifacts/spec-five-apphost-security-runtime-deferrals.md
location: scripts/local_aspire_magic_link_journey.py
evidence: The prior call helper used urllib.request.urlopen with workload-assertion/app-channel headers, permitting default redirects and environment proxies. The existing owned direct opener is reusable; real redirect/proxy tests and current-source journey evidence must verify the correction.

resolution 2026-10-26: Journey credential and metadata calls use the existing direct opener; real 302/307 and poisoned-proxy cases preserve status/bytes without forwarding credentials. The actual persisted current-source journey passes 1/1 with no skips and matching cleanup/preservation. See [verification](tests/five-local-runtime-evidence-deferrals.md).

### DW-194: Qualify deployed combined bootstrap latency against effective delivery budgets.

status: open
origin: deployed remainder of DW-190, retained by the approved five-local-runtime-evidence spec, 2026-10-26
source_spec: _bmad-output/implementation-artifacts/spec-five-local-runtime-evidence-deferrals.md
location: docs/launch-readiness.md
reason: Controlled local measurements and the manual isolated journey do not establish deployed automatic-delivery latency or the deployed client's effective timeout configuration.
evidence: [Local measurements](tests/five-local-runtime-evidence-deferrals.md#bootstrap-measurement-and-cancellation) cover exact direct/selected logical inventories and metadata, complete raw SDK state IO including batch reads, both bootstrap paths, the installed default ten-second reference budget, and cooperative cancellation/recovery. Owner: release/platform owner under Story 3.6. Before deployment acceptance, establish actual client options and cancellation propagation, current component/network latency across ordinary and streamed bootstrap, certified inventory, obsolete-writer fencing and automatic delivery. Keep deployed acceptance unfinished and launch FAIL until those gates pass.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-local-runtime-evidence-deferrals.md`
  summary: Medium, unverified — establish whether the restored native dashboard can share its listener with a process whose descriptors cannot be inspected.
  evidence: Review B6 shows that additional inaccessible holders cannot be ruled out by the observable FD inventory. The qualified dashboard's own socket ownership is positively verified; no supported native lifecycle transferring that listener to an inaccessible holder is demonstrated. An actual restored-process handoff and socket-inode trace would settle whether the probe recipient can differ. Do not infer system-wide exclusivity from visible owners alone.

Update 2026-10-27 to the dashboard visibility/handoff follow-up: local diagnostics retain pre/post-health and post-journey native observations plus aggregate visibility limits. Real inherited/SCM_RIGHTS process regressions are synthetic lifecycle evidence only. No native transfer defect is demonstrated; inaccessible holders, unobserved transfer and exclusivity remain unresolved. See [investigation and limits](tests/remaining-local-deferred-followups.md#native-dashboard-investigation). This update does not close the obligation or any deployed gate. Final local run `run-pjnjhza6` retains four identical native PID/start/inode observations through the gateway restart journey, with source equality, completed cleanup and shared preservation. No transfer is demonstrated; the obligation remains open.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-local-runtime-evidence-deferrals.md`
  summary: Medium — independently verify effective composed gateway HTTP timeout options and cancellation propagation into a blocked projection read.
  evidence: Review B10 confirms that current tests use the installed ten-second default with direct coordinator cancellation, while the manual journey uses its separate socket timeout. They do not constitute a composed HTTP attempt-timeout test. Reuse the actual gateway defaults and mapped projection endpoint to resolve configured options and observe cancellation reaching a blocked read; retain deployed acceptance and DW-194 separately.
 Resolution 2026-10-26: Done locally. The actual EventStore ServiceDefaults unnamed HTTP client resolves ten-second attempt/thirty-second total budgets; real sender timeouts cancel blocked mapped-route bootstrap reads while preserving pending journal and accepted owner checkpoint. Cold mapped-route retry drains the original delivery and selects the persisted index. [Verification](tests/five-retry-recovery-tooling-deferrals.md) retains exact commands and initial diagnostic corrections. Deployed DW-194 remains open.

- source_spec: `_bmad-output/implementation-artifacts/spec-five-retry-recovery-tooling-deferrals.md`
  summary: Qualify or extend certified abort repair beyond its bounded source-guard manifest capacity.
  evidence: Independent review B6 verifies a maximum of 4096 guards and 256 KiB serialized request data, while the existing v6 migration streams a global multi-tenant inventory with only a per-tenant 10000-owner bound. Larger inventories had no certified abort recovery before this increment and remain unsupported by the bounded repair path. Before deployment, measure the certified full shard/page inventory against those limits; a scalable chunked immutable guard manifest and restart/catch-up proof are needed to extend support. Default certification remains denied and launch remains FAIL.

- source_spec: `_bmad-output/implementation-artifacts/spec-remaining-deferred-work-scope.md`
  summary: Medium — retain privacy-safe failure-time dashboard ownership observations if richer handoff diagnosis is required.
  evidence: Independent review B4 confirms the existing ownership checker raises a fixed error before retaining conflicting owner/inode observations. The new positive lifecycle snapshots do not repair that pre-existing failure-time trace limitation. No native transfer is demonstrated. An actual failed native ownership observation with sanitized expected/observed PID-start/inode tracing would settle the handoff diagnosis; inaccessible holders and transfers between observations remain unresolved.

Local implementation update 2026-10-27: [Failure-time diagnostic follow-up](tests/remaining-local-deferred-followups.md#failure-time-ownership-diagnostic-follow-up--2026-10-27) retains partial expected numeric PID/start, listener inodes/visible holders, fixed failure stages and visibility limits before initial/post-journey refusals. Independently reviewed controlled real Python sharing/transfer and incomplete-visibility regressions verify on-disk evidence preservation and owned cleanup through execute/finally; the serialized full runner suite passes 193 tests without skips/failures/errors. Failed-health diagnostics and post-journey rediscovery refusals retain available observations without granting success. This completes the local diagnostic implementation only. The attempted native baseline refused an existing AppHost before startup; that foreign session was preserved. Actual failed native ownership tracing, inaccessible holders, unobserved native transfers and system-wide exclusivity remain **open**. No deployed acceptance or launch FAIL gate changes.

Native qualification update 2026-10-28, applying to both unnumbered dashboard follow-ups above: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home SSL_CERT_FILE=/home/administrator/.aspire/dev-certs/https/B574B914F76DCDEA2E5B39D008818B6436BDD657F3619ECEB1BF89052DCFD148.crt python3 scripts/run_isolated_magic_link_dapr.py --security-smoke --public-port 18080 --log-directory /tmp/timesheets-native-qualification-20261028/runtime` exits 0 in 282.586 seconds on Timesheets `69f3622b17de70bf3e9043f96577f2b6d1a4cbd9`, with one live persisted journey passing in 74.349 seconds without skips/errors/failures. Matching Debug/source restore selects actual native Dashboard/DCP 13.6.3; four pre/post-health observations around the journey retain PID/start `3875660/5772873` and inodes `36423797/36423798`. Source/asset equality, gateway PID/start replacement, all 22 captured exits, owned cleanup and shared preservation pass. Native samples record 38–39 inaccessible processes and 45 inaccessible descriptors, without claiming exclusivity. Serialized command `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v` passes 193 tests, including six synthetic failure/cleanup cases. No native ownership refusal occurred, so actual failed-native diagnostic survival remains unqualified; the watcher observed no failure receipts. A separate post-run convenience inventory assertion failed without retaining the offending response; the next read-only Aspire/Dapr capture reported zero rows. Its cause remains unclassified, with no stop or repair inferred. Exact commands, package/source identities, receipts and limits are in [the dated qualification](tests/remaining-local-deferred-followups.md#restored-native-diagnostic-qualification--2026-10-28). Successful native observations are qualified locally; actual failed native tracing, inaccessible holders, unobserved native transfers and system-wide exclusivity remain **open**. Every numbered unresolved obligation, deployed acceptance gate and launch **FAIL** is preserved.

Final verification update 2026-10-28: both independent review layers and post-fix evidence-audit rechecks complete; ten synthetic contradictory-evidence cases reject while the unchanged native baseline passes. These are evidence-checker corrections, not native diagnostic fixes. Five default compiled suites pass; IntegrationTests exits 1 with 978 passed, one failed and five skipped (all six: 2,782 passed / one failed / five skipped). The focused failed method's five cases pass separately; the broad failure remains open below. Later external changes to 11 dependency checkout heads are preserved untouched and excluded from publication. Committed gitlinks and launch FAIL remain unchanged; all 926 native-snapshot static files/build outputs still match. Neither the native run nor existing built-asset tests qualify the later dependency source/import graph. Exact commands, results and identities remain in the dated report and receipts above.

- source_spec: `_bmad-output/implementation-artifacts/spec-native-dashboard-diagnostic-qualification.md`
  summary: Medium — investigate the broad-suite HTTP claims-accessor read-set assertion that passes in the isolated five-case method run.
  evidence: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -noColor` exits 1: `MagicLinkConfirmationHttpBoundaryTests.AdminStateLoadsUseTheHostsHttpClaimsAccessor(useMismatchedClaims: True, claimVariant: "standard")` fails at line 1264 because a synthetic SDK shared-epoch read is outside the expected catalog epoch/generation set. Full output also has HTTP resilience timeouts, without established causation. The same executable with `-method '*AdminStateLoadsUseTheHostsHttpClaimsAccessor' -noColor` exits 0, five passed in 2.837 seconds; all 926 native build/static hashes remain unchanged. Root cause and any production impact are unverified; establish the suite-context read origin before changing fixture/read-set expectations or authorization. This is outside native dashboard diagnostics, which passed. Owner: Timesheets integration-test maintainer. See `tests/remaining-local-deferred-followups.md#restored-native-diagnostic-qualification--2026-10-28` and `evidence/dashboard-native-qualification-20261028/verification.json` / `focused-claims.json`.


Resolution 2026-10-28 for the immediately preceding broad-suite HTTP claims-accessor follow-up: the local fixture audit defect is demonstrated and corrected. The original unexpected tenant-1 catalog epoch is read by SDK promoted cleanup outside HTTP; a barrier-controlled real cleanup replay on the published historical gitlinks makes the former global read-window assertion fail on that exact address. Request-scoped full store/key observations preserve the strict assertion; an actual foreign read inside HTTP is still rejected. The final bound red/green receipts identify source inputs and assemblies. Historical and current focused lanes each pass nine cases, and the current full serial IntegrationTests suite passes **981 / zero failed / five skipped** (986 total; 718.468 suite seconds). The original broad failure remains historical FAIL evidence: the separate unmodified historical full run passed 979 with five skips and did not naturally reproduce it. Exact original worker/scheduler timing was not recorded and is not reconstructed. The isolated passing deadline test emits the historical timeout-event pattern, while the cleanup replay emits no timeouts; no historical timeout-to-claims causation is claimed. See [investigation, exact commands, identities and limits](tests/remaining-local-deferred-followups.md#http-claims-accessor-read-isolation--2026-10-28), `evidence/http-claims-read-isolation-20261028/experiment-identity-chain.json` and `current-full-serial.json`. Authorization, tenant isolation, admission guards, timeouts, published gitlinks, launch **FAIL**, deployed gates and all native-dashboard visibility limits remain unchanged. The local fix is complete; original unrecorded natural timing and deployed/runtime acceptance remain limits, not passing qualifications.

- source_spec: `_bmad-output/implementation-artifacts/spec-http-claims-read-isolation.md`
  summary: Restore current main's full Debug/source solution graph after its published EventStore gitlink raises three Aspire dependency floors above root pins.
  evidence: On Timesheets `787a3d178e5d9bf135fe5138f1f8ee6b4247fa31`, EventStore `41456b6ec8b8baf8e0ac4d0ff2f5e86f7a10be16` and Builds `81ab049208678bd6616828824729c0ddac02f7d1`, `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` exits 1 with NU1109: AppHost pins Aspire.Hosting/Redis 13.6.3 and Keycloak 13.6.3-preview.1.26521.2 below upstream 13.6.4 / 13.6.4-preview.1.26523.11. The narrow IntegrationTests project graph restores/builds and passes as recorded above; that does not qualify the solution/AppHost graph. Owner: build/AppHost integration maintainer in a story naming those projects. No pin, warning gate or source/gitlink was changed to suppress this separate pre-existing blocker. Exact command/output: `evidence/http-claims-read-isolation-20261028/current-restore.json` and `.log.gz`.
