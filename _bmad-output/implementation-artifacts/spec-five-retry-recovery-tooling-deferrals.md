---
title: 'Resolve five retry, recovery and tooling deferrals'
type: 'bugfix'
created: '2026-10-26'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '61db140db055dca2c9e4e2064f57ede8960f9003'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open deferrals remain actionable locally: DW-130 (uncertain committed use across HTTP retries), DW-184 (corrupt metadata blocking migration abort), DW-188 (npm advisories), DW-189 (upstream fixture secret findings), and the unnumbered composed HTTP timeout follow-up from local-runtime review B10.

**Approach:** Implement safe retry reconciliation and certified abort recovery, repair tooling findings, and verify actual HTTP cancellation. Review and verify all five, then commit and push main as requested. This explicit five-item batch includes independently shippable changes.

**Approved decision (2026-10-26):** Use an optional client `Idempotency-Key`. Callers omitting it retain existing behavior; callers retaining a key may reconcile the same complete intent only after authoritative original receipt verification. The user approved the recommended policy, this spec and continuation through implementation, verification and push to main.

## Boundaries & Constraints

**Always:** Retain opaque denials, server authorization, tenant isolation, single-use effects and EventStore-only persistence. Repair certification defaults to denied. Preserve pending deliveries, owner checkpoints and corrupt evidence. Keep deployment acceptance and launch FAIL explicit.

**Never:** Trust caller identity as authority, add a host, weaken scanners, initialize nested submodules, modify Works, deploy, mutate operational data, infer certified history from index rows, or release a recovery fence before normal journal drain. Keep unrelated dependencies unchanged.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Opaque commit | Same authorized retry identity and complete intent | Reconcile original stored use/effect; no second effect | Unknown outcome stays opaque |
| Mismatched retry | Changed token, action, payload or tenant | Never inherit another request's success | Opaque denial |
| Abort repair | Matching certificate, witness and unchanged v6 shards | Restore certified metadata through SDK; drain original journal | Missing proof retains fence |
| Interrupted repair | Restart, cancellation or changed binding | Resume identical repair or refuse | Preserve evidence and deliveries |
| HTTP deadline | Real sender timeout during projection read | Read observes cancellation; pending work survives | Cold retry recovers |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs`: supplies fresh TraceIdentifier; consumed-capability validation precedes submission.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs`: CreateMessageId includes that request ID; verification also expects the current timestamp and adjustment prior values. Identifier substitution alone is insufficient.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`: RecoverAbortAsync verifies ownership but cannot repair selected metadata. Its normal catch-up fold must remain intact.
- EventStore `src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs`: existing Begin/Stage/Commit reject Aborting; catch-up is not a guarded repair API.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`: authenticated mapped projection forwarding and replaceable stores. EventStore ServiceDefaults installs the actual unnamed HTTP resilience pipeline.

## Tasks & Acceptance

**Execution:**

- [x] Endpoint and submission files above; `tests/Hexalith.Timesheets.Server.Tests/MagicLinkDurableSubmissionServiceTests.cs` and the HTTP fixture — implement the chosen retry contract using original committed evidence, including original timestamps/prior values. Cover opaque-after-commit retries, concurrent attempts, payload conflicts, terminal capabilities and actor recreation; assert exact persisted effects.
- [x] EventStore coordinator above and its adjacent new repair request/receipt types; `tests/Hexalith.EventStore.Client.Tests/Projections/SharedProjectionEpochCoordinatorTests.cs` — add bounded certified repair under an exact operation/inventory/epoch/generation reservation. Persist immutable repair identity and original corrupt evidence; retain concurrency preconditions through restart. Conflicting repair, catch-up and abort completion must refuse while reserved. Use SDK storage operations; no Timesheets physical-key writes.
- [x] V6 migration above, an adjacent default-denied certification port and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV6MigrationTests.cs` — certify unchanged complete v6 shards and original metadata absence; restore that layout through guarded SDK repair, then existing abort/catch-up. Test format/root/single corruption with pending delivery, refusal, partial failure and cold recovery. No v7 promotion or invented owner checkpoint.
- [x] `package-lock.json` — update affected transitive packages within compatible ranges, including bundled npm dependencies. Retain direct tooling pins when compatible; verify clean install, audit, signatures and release checks.
- [x] In EventStore, fixture files `AdminUiDaprTargetTests.cs`, `SampleUiDaprTargetTests.cs`, `ChangingGetterPayload.cs`, `RuntimeSubstitutionPayload.cs`, `WholeObjectSelectionPayload.cs`, and `PolicyLifecycleTests.cs` — replace the 15 recorded credential-shaped literals with generated fixture values, preserving assertions and scanner rules.
- [x] HTTP fixture above — use actual gateway sender defaults, resolve effective resilience options, block bootstrap IO and verify cancellation, persisted pending state and cold recovery through the mapped route. Retain deployed DW-194.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md`, adjacent `tests/five-retry-recovery-tooling-deferrals.md` and `docs/launch-readiness.md` — record five evidence-backed dispositions and publication receipts.


**Acceptance Criteria:**

- Given each selected deferral, when its regression and relevant existing suites run, then its stated guarantee is demonstrated without substituting local proof for deployment acceptance.
- Given passing verification and independent review, when publishing, then validated Conventional Commits reach the owning main branches, with a separate parent gitlink commit referencing the pushed EventStore revision.

## Implementation Notes

- Parent final verification: all six rebuilt executables pass (Architecture 118, Contracts 108, Server 746, Works 76, Projections 771, Integration 963 passed plus five declared skips). The broader SDK projection lane passes 733 with two declared skips; unchanged tracked-secret guard passes. The earlier handoff Integration process ended without a completion receipt and is retained as incomplete, superseded by the parent-owned final run.
- Published API inspection confirms EventStore Client 3.127.0 lacks the domain-bound IssueAsync overload used by the corrected fixture; 3.128.2 includes it. The consuming pin advances to published 3.128.2 with matching source restore/build and focused post-pin gates. Certified repair APIs remain unpublished. Parent will push the four independently package-compatible items and retain the tested dependent repair wiring in the committed pending patch until an approved SDK package exists; no broken Release graph will be pushed.
- SDK source commit c613e55aefd0650c1e64ed0580c8495657585a89 passed pinned commitlint and normal hooks and is verified on origin/main. The fast-forward integration to 7af8a2a74dff103f286abad144e4959e3e30b902 had the same tree as the tested SDK baseline. Package publication is not source push; exact-main SDK and Tenants CI failures remain external release gates.

- 2026-10-26: Root npm tooling pins remain unchanged. Registry `npm@11.21.1` and even `npm@12.2.1` tarballs retain bundled `ip-address@10.7.2`/`undici@6.29.0`. Ordinary update, audit fix and overrides cannot replace bundled children. A lock-only nested update passed audit/signatures while clean install retained vulnerable files; that trial was discarded and is not passing evidence.
- The bounded repair uses root postinstall and a dedicated two-package integrity lock. Supported `npm ci` installs only `ip-address@10.7.5` and `undici@6.29.1` into an isolated temporary prefix, then copies those complete package directories into npm's two corresponding child locations. The root lock records their actual final versions, resolutions and integrity. No installed JavaScript or npm tarball is edited, and other npm package files remain byte-identical to the registry artifact. Root npm ci can print the audit snapshot it computed before postinstall; final actual-file versions, dependency-tree validation, separate audit and signature verification settle the installed-tree result.
- Required pre-edit isolated runtime baseline safely refused admission: a Dapr app was running or inventory was unsafe; no AppHost started. Receipt: `evidence/five-retry-recovery-tooling-deferrals/baseline/run-o6cat3h1`. No foreign runtime was stopped.
- EventStore clean main fast-forwarded from `dcd3dce42b67a305b608f03c4dab2b812791abae` to `458f0cf38e43452655e6c307fa80debdf595a8db` after fetch. The fetched history already contains `80993d367f8bcafb3e6eda97568ebf0825dd1821 test(fixtures): use runtime credentials and private canaries`, covering the listed credential literals; verification is against that integrated source, without scanner exclusions.


## Spec Change Log

- 2026-10-26: Implementation-only adjustment: reproducible two-child npm bundle repair plus explicit ignored-script CI/release integration replaces the insufficient lock-only/ordinary update approach. No frozen intent changed.
- 2026-10-26: Required upstream integration added exact-domain workload assertion issuance. The existing routing fixture now provides and asserts that overload for command/stream calls; command-status retains its tenant-only contract.
- 2026-10-26: New helper types moved into their own files under the baseline's one-type rule. Final affected tests use isolated output to preserve active broad-run assets.


## Review Triage Log

All three independent layers reviewed `/tmp/timesheets-five-independent-review-e54rhkgd.patch`. Each finding was classified before grouping. The frozen intent remains unchanged.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 — changed Activity Type retry | medium | Loader line 190 requires the original Activity Type even after a legitimate adjustment changes it. Receipt reconciliation is unreachable. Patch the used-capability read path without loosening new-use authorization; cover the replacement-type HTTP fixture. |
| B2 — interrupted drain after repair | medium | Migration rechecks original source digest and SDK rejects prepared journal state before recognizing completed repair. Ordinary drain changes those facts. Patch identical reserved/completed recovery ordering; cover prepared and applied drain interruptions. |
| B3 — copy-on-write metadata | medium | GlobalScope enables COW; effective inherited reads differ from direct physical-key deletes, and a retained pointer can reference missing storage. Patch through existing SDK COW mutation bookkeeping and effective-value checks; cover a nonzero inherited generation. |
| B4 — unpublished SDK dependency | medium | The consuming Release pin remains 3.127.0 and lacks the new API. This is a mandatory publication gate: publish/verify the SDK package before advancing the pin or pushing dependent module code. Current upstream CI already fails on missing Tenants Routing types; no release gate will be bypassed. |
| B5 — certificate inventory digest | medium | FindNextTenantAsync hashes logical contribution rows, while the certificate claims complete unchanged shard/page inventory. Allocation metadata and empty-key changes can preserve the row digest. Patch the new certificate check to bind the canonical complete source guard inventory; no public field addition is needed. |
| B6 — large inventory repair capacity | medium | The new bounded path refuses more than 4096 guards or 256 KiB, while the pre-existing streaming migration has no corresponding global bound. Larger inventories already lacked certified abort repair; this does not regress a previously working recovery path. Defer a scalable guard manifest and explicitly retain capacity qualification before deployment; do not claim unbounded recovery. |
| B7 — persisted JSON null | medium | ETag can establish a physical row while JsonNode is null. Mixed damaged metadata then violates the new request pairing, refusing recovery. Patch observations/guards to retain the literal null and distinguish absence, including effective COW reads. |
| B8 — durable reservation validation | medium | ValidateState ignores the new receipt/completed fields; malformed persisted combinations can bypass reservation refusal. Patch validation of digest, bounds and exact durable binding; cover corrupt receipt/flag states. |
| B9 — source drift during final delete | medium | The final completion loop checks metadata absence but does not recheck source guards after the last delete. An injected source change can be accepted as completed. Add the final source check and a mutation-at-delete regression. |
| B10 — original comment decision | medium | Keyed verification regenerates expected server publication fields through today's authority, so a policy change rejects an otherwise identical original receipt. Patch keyed reconciliation to validate original issued fields from authoritative stored evidence and compare caller-owned content; retain current authority for new writes and external disclosure. |
| E1 — interrupted post-repair drain | medium | Independently confirms B2 at migration lines 67–68 and SDK guard ordering. Group with B2; retain this row. |
| V1 — certificate key-set negative coverage | medium | Pre-verified regression gap: the helper always supplies matching witness/keys/digest. Add refusal cases for non-null foreign keys, witness and digest, asserting unchanged bytes, journal and fence. |
| V2 — successful npm replacement coverage | medium | Pre-verified broken-verification gap: automated script tests only exercise pre-install refusal, while audits can trust the lockfile. Add successful deterministic CLI execution checking target payloads and unrelated files, plus real script postconditions on installed artifacts. |
| V3 — interrupted post-repair drain | medium | Independently confirms B2 and E1; group with B2 and retain all three rows. |
| R1 — constructor compatibility | medium | Parent inspection: changing the public migration constructor from three to four CLR parameters removes the existing binary signature despite the optional argument. Restore the original three-parameter overload as a direct compatibility correction. |
| R2 — new helper style/documentation | low | Parent inspection: newly separated helper members omit required XML documentation and use non-Allman braces. Apply direct style/documentation corrections only; no behavior test is needed for those edits. |

The retained implementation findings are direct corrections using existing surfaces; no new intent decision is required. B4 remains a parent-owned publication gate. B6 is retained as an explicit bounded-capacity follow-up.


## Verification

- Run the required isolated AppHost baseline before code edits; retain exact failure and cleanup evidence if blocked.
- Run matching Debug/source solution restore/build from AGENTS.md, then all six built xUnit executables individually. Run focused retry, migration and composed cancellation cases first.
- In EventStore run coordinator, affected fixture and `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets` lanes; report unrelated broad blockers separately without exclusions.
- Run `npm ci`, `npm audit --json`, `npm audit signatures`, and `npm run test:release-tooling`; validate the exact commit message with pinned commitlint in each owning repository.
- Run `git diff --check`; fetch and inspect divergence before pushing without force. EventStore is clean but currently five commits behind its existing remote tracking main; integrate required upstream changes before owned edits and revalidate.

## Publication outcome boundary

All local implementation and independent review corrections are complete. Four module items can ship against published SDK 3.128.2. DW-184 stays open and this spec stays in-review: the SDK source is pushed, while its exact-source package release is blocked by pre-existing Tenants Routing CI failures. The complete tested module implementation and regression changes are retained in `evidence/five-retry-recovery-tooling-deferrals/certified-repair-pending.patch.gz`; active compiled source excludes those unavailable APIs. This preserves a compilable package graph without discarding completed implementation. Resume after a package containing the SDK repair API is published, then apply the patch, advance the consuming pin and verify/push. No production release or deployment occurred.
