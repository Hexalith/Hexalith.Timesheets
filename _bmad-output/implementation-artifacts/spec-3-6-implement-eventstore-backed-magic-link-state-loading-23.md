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

- [ ] `docs/deployed-magic-link-acceptance.md` — record selected target,
  artifact/source identity, operator, supported inspection mechanism and
  actual binding/authorization providers. Attach read-only prerequisite
  evidence or exact missing items before proposing any write command.
- [ ] `docs/deployed-magic-link-acceptance.md` — verify full legacy coverage,
  owner continuity, pending-delivery recovery and subscriber/shared rebuild
  behavior. Prepare target-specific commands after qualification.
- [ ] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` and
  `docs/local-magic-link-dapr-proof.md` — retain local and deployed results
  separately, with exact commands, failure provenance and cleanup evidence.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`,
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

## Spec Change Log

## Review Triage Log

## Verification

Use README source-reference restore/build with the pinned SDK; expect zero
warnings/errors. Run the supported isolated runner for local evidence only;
require one passing executed test, exact owned cleanup and matching shared
preservation. After evidence edits run the ArchitectureTests executable with
`-class '*LaunchReadinessTests' -noColor`; retain the honest open gates.
