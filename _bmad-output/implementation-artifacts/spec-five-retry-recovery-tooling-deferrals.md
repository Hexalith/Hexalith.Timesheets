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
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`, adjacent `tests/five-retry-recovery-tooling-deferrals.md` and `docs/launch-readiness.md` — record five evidence-backed dispositions and publication receipts.


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

### Resumed independent review — 2026-10-30

All three layers reviewed the complete diff from the original baseline to current main. The edge-case layer returned no findings. Each blind-hunter finding and the pre-verified verification gap receive a separate verdict below; historical findings retain their original identifiers above.

| ID | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| RR-B1 — large repair inventory | medium / carried defer | Carried B6: the SDK still enforces 4096 guards / 256 KiB. The completed repair/guidance follow-up qualifies exact byte bounds and retains scalable manifests/deployed inventory as open; no second implementation or duplicate deferral is needed. |
| RR-B2 — repeated polling budget | low / reject | Static tracing confirms that concurrent processing after an Issued snapshot can cause two bounded service polls. Missing receipts return immediately and consumed capabilities cannot dispatch again. The documented option bounds each submission operation, not total HTTP latency including loader/authorization; no duplicate write or deadline/cancellation violation was demonstrated. Introducing a shared endpoint timer and cancellation branches for this uncommon overlapping-request delay exceeds a direct cosmetic correction. |
| RR-B3 — removed Activity Type | false | The supported lifecycle deactivates/reactivates an existing catalog row; it does not remove it. The loader requires presence/tenant shape, not IsActive, for receipt reconciliation. Independent reviewer retracing withdraws the unsupported deletion scenario; a non-Fresh catalog intentionally denies. |
| RR-B4 — original previous values | false | Keyed reconciliation deliberately verifies authoritative original event effects rather than the now-adjusted snapshot, as required by the approved intent. The previous/next pair still verifies target, contributor, original Activity Type, scopes, category and AI metrics; unkeyed fresh submission retains the independent trusted snapshot. The finding supplies no reachable invalid authoritative commit. |
| RR-B5 — publication version upgrade | false | The only issued version is the current contract constant. No retained older version or upgrade exists in this diff; accepting arbitrary unknown publication versions would weaken existing evidence checks. A future schema migration needs its own version compatibility decision. |
| RR-B6 — dashboard continuity | false | The runner's intermediate lifecycle receipt explicitly leaves qualification incomplete. The later `dashboard-native-qualification-20261028/audit-native.py` requires identity equality and independently compares before/after ownership before qualifying evidence. Reviewer retracing confirms a replacement cannot pass that audit. |
| RR-B7 — install rollback | false | Copy failure exits the repair/postinstall unsuccessfully, and CI requires that success before audit. No transactional install guarantee exists; another clean npm ci rebuilds the installed tree and reruns repair. A partly copied failed installation cannot be reported as passing by this script. Reviewer withdraws the contract-violation claim. |
| RR-B8 — indefinite child install | maybe-false / reject | No outer spawn timeout exists, but npm applies bounded fetch retry/HTTP timeouts and the nested install disables lifecycle scripts. Reviewer retracing establishes no reachable indefinite operation under supported configuration. A reproduced hang outside those bounds would settle the claim; absent that evidence an additional outer timer is low-impact hardening with extra failure branches. |
| RR-B9 — client documentation | low / patch | README omitted the implemented optional header contract. Add key retention, validation bounds, complete-intent binding and opaque/no-key behavior directly; no behavior change. |
| RR-B10 — cold host verification | false | `ClientRetryUsesOriginalCommittedTimeAndPriorValuesWithoutResubmitting` already constructs a fresh submission service over retained gateway status/stream evidence for both actions. The service has no mutable retry receipt cache; concrete-loader HTTP tests independently cover receipt resolution. Replacing the fixture host adds no demonstrated missing state dependency. |
| RR-V1 — multiple header values | medium / patch | Pre-verified regression gap accepted. Add both-route HTTP rejection cases with valid unused capabilities, zero submissions and exact stored-stream equality, followed by a valid successful use proving fixture validity and capability preservation. |
| RR-P1 — current SDK source HTTP admission | medium / defer | Fresh Debug/source execution fails before retry logic: both the added header cases and unchanged `KeyedHttpRetryReconcilesOpaqueCommittedOriginalEffects` cases receive 503 from `/project/v2` during `ProjectValidStateAsync`. The current SDK checkout adds `DomainRouteCatalogFilter`/`DomainRouteCatalogGuard` backed by platform route-catalog authority. The isolated published EventStore 3.128.4 graph builds and passes all four cases. This is a source-checkout compatibility blocker; retain platform admission rather than substituting a permissive guard. |


### RR-P1 independent review — 2026-10-30

Three independent layers reviewed the original-baseline cumulative diff and the current fixture follow-up. The cumulative diff includes intervening work from other completed specs. Every finding is classified individually before grouping; the two runtime-observation findings predate this resumed change.

| ID | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| RP-B1 — scheduler protocol exception | medium / defer | `read_only_shared_snapshot` catches neither `BadStatusLine` nor `IncompleteRead` (`HTTPException`), so malformed scheduler responses can bypass the sanitized failure receipt. Present in HEAD before this resume. Group with RP-E1; retain a runtime-tooling follow-up without executing live qualification. |
| RP-B2 — scheduler endpoint transport | medium / defer | Scheduler range uses ordinary `urlopen`, whereas `fixture_urlopen` disables redirects and proxies. A configured proxy or redirect can change the observed endpoint. Present in HEAD before this resume; record separately from exception handling. |
| RP-B3 — omit-dev npm installation | low / reject | The private root npm package contains only development/release tooling, and its supported clean-install/audit lanes require that tooling. Omitting all dev dependencies leaves no application or release tooling to install. Adding a conditional postinstall path solely for that unused mode exceeds a direct correction. |
| RP-B4 — duplicate migration shard reads | low / reject | The v6 scan performs both a raw null/ETag observation and a typed read. This adds IO in a default-denied recovery operation, but no supported-inventory failure is demonstrated. Reworking raw deserialization is more than a direct correction; deployed scan/memory qualification remains in the existing open manifest follow-up. |
| RP-B5 — retry-key boundary coverage | medium / patch | Both endpoints explicitly enforce nonblank, no control characters and at most 256 characters; existing tests cover multiple values only. Add both-route refusal cases followed by successful use with a 256-character key and exact stream preservation. |
| RP-B6 — complete-intent conflict coverage | medium / patch | Existing HTTP conflicts cover duration, token, key and comment text, while service date, activity, billable state and caller comment policy have no keyed conflict cases. Add representative adjustment conflicts and opposite-action refusal, preserving exact stored effects. |
| RP-B7 — exact cold projection result | medium / patch | Deadline recovery verifies journal/checkpoint/root but does not resolve the original hash. Add exact tenant/capability candidate assertions after recovery and in the catalog-refusal successful control. |
| RP-B8 — outbound generation binding | medium / patch | The new gateway control checks only path/assertion. Capture root/epoch, compare committed readback headers, and prove an unactivated catalog refuses before any custom transport invocation. |
| RP-B9 — independent transport ownership | false | `RouteCatalogInvocationHandler` refuses a factory returning the same handler instance before sending. Returning/reusing the original cannot leave the positive test green. Platform disposal remains owned by the real handler; no fixture bypass replaces that lifecycle. RP-B8 also adds explicit clone-use observation. |
| RP-B10 — stale progress records | low / reject | Editing this build's spec is not a review fix. The ordinary completion step will reconcile final status/results while retaining the original failing receipts. |
| RP-E1 — scheduler protocol exception | medium / defer | Independent reproduction raises `BadStatusLine` with private response text and no sanitized receipt. Same pre-existing root cause as RP-B1; one deferred entry retains both observations. |
| RP-V1 — keyed stored-comment mismatch | medium / patch | Pre-verified gap: stored-comment tampering tests use the unkeyed branch; policy-change retry tests retain valid effects. Add initial keyed verification and reconciliation cases with fixed key/token/body and only persisted text changed, asserting opaque denial and no second effect. |

## Verification

- Run the required isolated AppHost baseline before code edits; retain exact failure and cleanup evidence if blocked.
- Run matching Debug/source solution restore/build from AGENTS.md, then all six built xUnit executables individually. Run focused retry, migration and composed cancellation cases first.
- In EventStore run coordinator, affected fixture and `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets` lanes; report unrelated broad blockers separately without exclusions.
- Run `npm ci`, `npm audit --json`, `npm audit signatures`, and `npm run test:release-tooling`; validate the exact commit message with pinned commitlint in each owning repository.
- Run `git diff --check`; fetch and inspect divergence before pushing without force. EventStore is clean but currently five commits behind its existing remote tracking main; integrate required upstream changes before owned edits and revalidate.

## Historical publication outcome boundary — 2026-10-26

All local implementation and independent review corrections are complete. Four module items can ship against published SDK 3.128.2. DW-184 stays open and this spec stays in-review: the SDK source is pushed, while its exact-source package release is blocked by pre-existing Tenants Routing CI failures. The complete tested module implementation and regression changes are retained in `evidence/five-retry-recovery-tooling-deferrals/certified-repair-pending.patch.gz`; active compiled source excludes those unavailable APIs. This preserves a compilable package graph without discarding completed implementation. Resume after a package containing the SDK repair API is published, then apply the patch, advance the consuming pin and verify/push. No production release or deployment occurred.

Four items are published in Timesheets content 5002ab27df1787624d4ad304cfc6bf78b00df903 and separate SDK gitlink 63817540d0d3f953338177f216894b3d601f1fa4, both remotely verified. Spec status remains in-review solely because the fifth item cannot be shipped without the blocked SDK package. The main working tree is clean and the complete pending implementation is retained in the committed archive.

## Earlier resumed checkpoint — 2026-10-30

The historical package blocker is resolved by the completed [remaining repair/guidance spec](spec-remaining-repair-and-guidance-deferrals.md). Published EventStore 3.128.4 supplies the reviewed SDK repair API; Timesheets implementation `1b17c2e012d1fbc889686392cad98e2ee06f4547` integrated the held patch and was remotely verified on main. The current branch contains that exact implementation as an ancestor, and DW-184 already records module publication complete. The four other items retain their original publication receipts. The historical pending patch must not be reapplied.

This resume corrects the stale package-blocker record and adds the accepted HTTP rejection regression plus client documentation. The original five items are published, but this resumed workflow stays in-review because fresh source HTTP validation exposes a separate platform route-catalog admission blocker (RR-P1). No production repair provider, runtime qualification or deployed acceptance is granted. Capacity extension, deployed inventory and Story 3.6 remain open; launch remains FAIL.

Matching current Debug/source restore/build and the final Integration test rebuild pass with zero warnings/errors. Four full suites pass (Architecture 118, Contracts 108, Server 746, Works 76); the fresh focused migration class passes 97. Source HTTP tests fail at setup (two new cases and two unchanged retry cases); isolated published-package compatibility passes those four cases. The full Projections attempt was interrupted after the separate HTTP blocker was established and retains a partial 760-case summary with one cancellation failure, not a passing broad receipt. Full Integration was not started. The [resume receipts](evidence/five-retry-recovery-tooling-deferrals/resume-20261030/checks.json) preserve commands, outcomes and compressed logs. Changes remain uncommitted; step-05 completion/publication is not reached because required source verification fails.

## RR-P1 fixture admission — 2026-10-30

RR-P1 is resolved locally through the supported current-source fixture protocol. The source-only Integration build constant keeps the newer API calls out of the published EventStore 3.128.4 graph. `HttpRouteCatalogFixture` retains the platform authority, Development trust validator, receiver filter/guard and invocation handler. It uses an isolated serialized CAS authority cell; observes registered named handlers, effective endpoint policies, capability fingerprints and nonsecret workload settings; then prepares, readies and commits the complete fixture catalog. Calls carry the committed root and epoch from authority readback. The capturing gateway fixture has its own observed auxiliary operation, readiness attestation and independently owned invocation transport. No dependency source, package pin, gitlink, AppHost or production registration changes.

All four originally failing current-source HTTP cases pass. The final fixed-output focused run passes **32/32**, including eleven catalog-refusal cases, both-route invalid-key and maximum-length cases, unchanged original multiple-header rejection tests, keyed stored-comment tampering, complete-intent conflicts, exact recovered index candidates, unactivated outbound refusal and committed gateway headers. Both bootstrap budget cases pass **2/2** in isolation without changing the ten-second budget. Three independent review layers ran; every finding is triaged above, accepted test patches are applied, and the verification review follow-up reports no remaining gaps.

The current SDK's unchanged whole-content secret scanner fails **1/1** with three pre-existing route-catalog fixture findings (`CatalogBackendHttpTests.cs:51`, `CatalogRefusalDiagnosticsTests.cs:16`, `CatalogTransportIsolationTests.cs:106`). Original affected credential-fixture lanes pass Admin UI **7**, Sample UI **7**, PolicyLifecycle **22**; the coordinator class passes **38** and abort repair passes **13**. The SDK checkout remains clean. The failed scan is retained without exclusions or dependency edits, and the spec remains **in-review** until that required gate passes. New runtime-observation findings are separate pre-existing deferrals; no live qualification was run.

The two first broad attempts were stopped after Integration outputs were rebuilt during execution and are superseded, not passing receipts. The fixed-output full Projections suite passes **787/787** in **949.004 s** with no skips; the corrected full Integration suite passes **1004 tests with 5 declared skips**, zero failures, in **1048.269 s**. The preceding complete Integration run had **1001 passed, 2 failed, 5 declared skips** in **947.177 s**; it exposed the canonical-path and command-admission cases below and is superseded. The current instruction requests a local Conventional Commit; no push or deployment is part of this resume. Launch remains **FAIL**.

The current source SDK intentionally refuses noncanonical internal method paths and `/process` commands lacking an owner-declared route/admission facet. The public-listener test retains both incoming casing probes and uses canonical `/project/v2` as its successful internal control; an eleventh catalog case verifies internal uppercase refusal. Timesheets supplies no command idempotency declaration, so the projection fixture does not manufacture one. The source process case verifies HTTP 503, unchanged gateway state and the registered keyed processor's separate forged-origin rejection; package mode retains the original mapped HTTP 200/rejection checks. Real command enrollment is a separately recorded follow-up, with no source runtime acceptance. Independent edge-case review of these corrections returns no findings.

Current exact commands and lossless logs: [RR-P1 receipts](evidence/five-retry-recovery-tooling-deferrals/resume-20261030/rr-p1/checks.json). The enclosing resume receipts and all original publication evidence remain unchanged.

Final ordinary source verification passes all six executables: Architecture **118**, Contracts **108**, Server **746**, Works **76**, Projections **787**, Integration **1004 passed / 5 declared skips** (**2839 passed** total). Matching solution restore and the final warnings-as-errors solution build pass with zero warnings/errors. Focused HTTP passes **32**, migration **97**, and isolated bootstrap **2**. Current SDK coordinator **38**, abort repair **13**, Admin UI **7**, Sample UI **7** and PolicyLifecycle **22** pass. `npm ci`, final audit (zero vulnerabilities), signatures and **99** release-tooling tests pass; the exact local commit message passes pinned commitlint. The preserved SDK scan failure remains the unpassed required gate, so status stays **in-review**, not done. Source `/process` command enrollment remains separately unqualified and default-denied. No live runtime qualification, push, dependency-source edit or deployed acceptance occurred; launch remains **FAIL**.
