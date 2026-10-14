---
title: 'Qualify deployed magic-link acceptance for Story 3.6'
type: 'chore'
created: '2026-10-13'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Story 3.6's only remaining unchecked acceptance task requires deployed
proof. The loader, durable commands, mapped projections and local journey
already exist. Local fixtures cannot establish installed coverage, legacy
owner continuity, trusted resource bindings, subscriber delivery or shared
rebuild acceptance.

Qualify a named deployment through read-only inspection, then prepare its
operator's concrete acceptance run. Perform maintenance or domain writes
only after the exact target, authority, source cut and operation are
established and approved. Keep each unmet prerequisite explicit.

## Boundaries & Constraints

**Always:** Use Hexalith.EventStore. Require selected-index resolution,
authoritative capability/Time Entry folds, a Fresh catalog, real server-side
access gates and signed tenant/domain/actor bindings. Verify persisted
end-state, preserve unrelated contributions and historical failures, and
record exact commands, full revisions and sanitized outcomes.

**Never:** Substitute fixture claims, tenant-only snapshots or unique-only
legacy rows for certification. Do not enable Development fixture authority
in deployment, fabricate owner checkpoints, clear legacy keys or bootstrap
v7 over v1. Do not run the hardcoded local journey against deployment.
Preserve default-deny authority. Do not expose credentials, tokens, comments
or payloads; stage, commit, push; update dependencies/gitlinks; or change
AppHost/ServiceDefaults. Qualification alone cannot mark the parent or
release PASS.

</frozen-after-approval>

## Qualification Result

The 2026-10-14 resume at clean Timesheets
`9ecda28dc7ff540d83fad7ba1e5d585bdc33d0ae` inspects Platform
`92a02924894f6f2f219801a90e5bf25716abd028`. Its new staging dependency
handoff supplies upgrade preparation only. Eight fresh bounded native reads
confirm server v1.34.9, absent staging namespaces and unavailable Gateway
resource type, with unchanged cluster UID/counts and no matching names/images.
The next Platform operational dependency is fresh signed recovery/readback
and an exact 4.27 retirement plan/prerequisites/approval, followed by accepted
retirement. Hosted release and all four deployed owner requirements remain
missing. See [the current recheck](../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-staging-dependency-recheck).
No intent gap or independent Timesheets runtime fix was established. Keep this
spec draft; do not request implementation approval while it remains insufficient.

The prior resume at clean Timesheets `b9f968221a014c95869f3add8e16eda398b989c8`
inspects Platform `416878a74975b1023b3a00ab5d35aad4ce6365d9`. Explicit
Development/source host composition now supplies local listener, Dapr and
read-model bindings; production/certification defaults remain denied or
unavailable. Fresh native reads still find both staging namespaces absent and
no matching names/images. Retained release identities and all deployed authority,
continuity and subscriber/shared-rebuild prerequisites remain missing. See
[the latest recheck](../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-development-binding-recheck).
This draft remains unready; no user intent gap or separate Timesheets runtime
fix was established. Do not request implementation approval while these
external dependencies remain unresolved.

The previous resume at Timesheets
`aefd76772e84018442eb21b36549b8499a488721` inspects Platform
`ce6f2d34398803644b29d02ba57625898190ce20`, which now retains an offline
staging candidate outside active composition. Fresh native reads confirm
both proposed staging namespaces are absent. Offline regeneration refused
missing Platform-local nested inputs; no plan was published and none were
initialized. P1 now has a read-only HTTP adapter, with operational enrollment
still absent. Exact results and remaining owner gates are in
[the candidate recheck](../../docs/deployed-magic-link-platform-candidate-recheck.md).
The original qualification report remains byte-for-byte unchanged because
Platform pins its dated hash. This progress does not make the spec ready
for development. Earlier qualification below is retained as history.

The user directed discovery from Platform. Its designated cluster is
`https://192.168.1.30:6443`; context `jpiquot@local` currently authenticates as
`jpiquot` and supports native reads. Administrator owns operations; Jérôme
Piquot maintains the generic P1 contract. Planned domains are staging
`hexalith.com` and production `tache.ai`. Current node/UID match Platform's
retained inventory. Details and exact commands are in
[the qualification record](../../docs/deployed-magic-link-acceptance.md).

No identifiable Timesheets deployment, production resource-binding issuer or
Timesheets inventory/maintenance provider was found. Platform excludes
Timesheets from MVP acceptance; P1 remains offline and operationally
unenrolled. These are missing external prerequisites, not unresolved target
preferences. Keep this spec draft until installation/coverage/authority are
established and a concrete acceptance plan is approved.

The 2026-10-13 build resume rechecked this conclusion from clean Timesheets
`main` at `28ffeaee83e9414ce9e205e57f674e4c29a6e40b`. Four native reads
passed with the same cluster UID, 23 namespaces, 100 workload/service
objects and 111 pods; no matching Timesheets names/images were found.
Independent source investigation found no production binding/inventory/
migration provider or separate Timesheets fix that would close these gates.
Exact commands and discovery limits are in the qualification record.

**Planning status:** Not ready for development. Missing application
enrollment, production bindings, certified source/maintenance evidence and
installed subscriber/shared-rebuild proof are external dependencies. Target
discovery is answered; no repeated deployment-information question is needed.

## Code Map

- `references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md`
  — Platform-owned recovery/retirement/upgrade and staging/release prerequisites;
  consume as evidence only. Preparation grants no operational authority.
- `docs/deployed-magic-link-platform-candidate-recheck.md` — latest offline
  candidate/native target evidence; preserve the original qualification report
  because Platform's preparer pins its dated hash.
- `src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs`
  — requires a resource-binding issuer; requests tenant/domain/actor bindings.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs`
  — authenticated internal routes; defaults deny authority/inventory.
- `src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkV1VerifiedInventoryProvider.cs`
  — independently certified full coverage and enforced maintenance receipts.
  Reuse the existing v1 migration in this directory after source continuity
  and pending-delivery recovery are established.
- `scripts/run_isolated_magic_link_dapr.py` — local journey/preservation only;
  retain its original assertions and ownership checks.

## Tasks & Acceptance

**Execution:**

- [x] `docs/deployed-magic-link-platform-candidate-recheck.md` — record selected target,
  artifact/source identity, operator, supported inspection mechanism and
  actual binding/authorization providers. Attach read-only prerequisite
  evidence or exact missing items before proposing any write command.
- [ ] `docs/deployed-magic-link-acceptance.md` — verify full legacy coverage,
  owner continuity, pending-delivery recovery and subscriber/shared rebuild
  behavior. Prepare target-specific commands after qualification.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` and
  `docs/local-magic-link-dapr-proof.md` — retain local and deployed results
  separately, with exact commands, failure provenance and cleanup evidence.
- [x] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `_bmad-output/implementation-artifacts/3-6-resume-checkpoint.md`, and
  `docs/launch-readiness.md` — reconcile evidence without closing the deployed
  task until all required receipts and persisted outcomes exist.

**Acceptance Criteria:**

- Given accessible target evidence, when qualification runs, then each
  prerequisite names its actual authority/source revision or remains missing.
- Given absent/revoked certification, when maintenance is considered, then
  no migration, checkpoint seeding or domain command is issued.
- Given an approved qualified run, when links are exercised, then persisted
  end-state proves fresh loading, atomic use/effect, one race winner, replay
  denial and restart persistence, plus deployed subscriber/shared-rebuild proof.
- Given only local evidence, when tracking is reconciled, then Story 3.6 and
  Epic 3 stay in progress and release stays FAIL.

## Implementation Notes

- Initial tree clean on main at `11976c2d8000bfc83f1d816a085836beecf8f6d1`.
  Epic 3 context is valid; Story 3.5 supplies continuity. Prior specs are preserved.
- No established cause/fix for the historical metadata 500. Baseline private
  journey passed 1/1 in 137.078s using pre-existing assets. Fresh SDK 10.0.401
  restore/build passed, zero warnings/errors, 17.64s; fresh journey passed
  1/1 in 126.554s. Both cleanup/preservation checks passed; details are in the
  test summary. No runtime/test source changed.
- Initial investigation found no target; the subsequent user-directed Platform
  investigation recovered infrastructure/access but found no qualified
  Timesheets deployment or binding/inventory/maintenance provider. The earlier
  request for deployment details is answered by the qualification record.
- Readiness evidence checks passed 13/13 in 0.156s; whitespace validation passed.
- Read-only Platform/native qualification used Platform
  `2c3f0fd4befe4e7e2aa1a3f7ce8ae1d5e1808f3c` and Timesheets
  `6a95dbb10721fbe22a7fe89e9c021454aed43b45` on 2026-10-13. No Platform,
  runtime or cluster mutation occurred. See the record for exact scope/limits.
- Build resume at `28ffeaee83e9414ce9e205e57f674e4c29a6e40b` repeated
  bounded read-only qualification and reconciled six evidence/tracking files.
  Readiness passed 13/13 in 0.195s; whitespace validation passed. Frozen
  intent is preserved verbatim. External prerequisites remain missing; no
  implementation-ready approval or dependent domain operation is proposed.

- Latest planning resume inspected Platform's offline staging candidate and
  rechecked its proposed namespaces; neither exists. The preparer refused
  missing nested inputs without publishing output. Seven evidence/tracking
  files were reconciled, preserving frozen intent and the pinned original
  qualification report. Readiness passed 13/13 in 0.120s; whitespace and
  retained preparation/declaration checksums passed. Existing local proof is
  retained separately; deployed proof remains blocked. No runtime/build,
  deployment, domain, dependency or Git mutation was issued by this run.

- Latest Development binding recheck resumed at clean Timesheets
  `b9f968221a014c95869f3add8e16eda398b989c8`, consuming Platform
  `416878a74975b1023b3a00ab5d35aad4ce6365d9`. Five bounded native reads
  confirmed the same census and absent staging namespaces. Seven evidence/
  tracking files were reconciled; readiness passed 13/13 in 0.154s and
  whitespace validation passed. Frozen intent and pinned report/preparation
  bytes remain unchanged. No runtime/deployment/domain/dependency/Git mutation
  occurred; production and deployed acceptance prerequisites remain missing.

- The 2026-10-14 planning resume consumes the new Platform staging dependency
  handoff and eight read-only native results. Intent gaps: none established;
  irreversibles: deployment/maintenance/domain writes remain gated and were
  not performed; footprint: seven Timesheets evidence/tracking documents only,
  no new callable surface. Independent investigation found no separate runtime
  fix. External prerequisites still fail the Ready for Development sufficiency
  standard; frozen intent and historical evidence remain preserved.
  Existing readiness assets passed 13/13 in 0.170s; whitespace and frozen-intent/
  pinned-report/Platform-input preservation checks passed, including the
  canonical preparation checksum. No fresh build or deployed proof is claimed.

## Spec Change Log

## Review Triage Log

## Verification

Use README source-reference restore/build with the pinned SDK; expect zero
warnings/errors. Run the supported isolated runner for local evidence only;
require one passing executed test, exact owned cleanup and matching shared
preservation. After evidence edits run the ArchitectureTests executable with
`-class '*LaunchReadinessTests' -noColor`; retain the honest open gates.
