---
title: 'Prepare and qualify Story 3.6 deployment, authority and runtime'
type: 'feature'
created: '2026-10-17'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'a376b71d43330a716f836c3086e562e505511dd3'
context: []
---

<frozen-after-approval reason="User expressly authorized implementation, local testing, fresh offline preparation and existing-access read-only inspection on 2026-10-17">

## Intent

Close Story 3.6's missing technical hosting and authority capabilities and collect
actual current operational facts. Prepare exact live commands and recovery for
owner approval; continue independent local work while deployment depends on it.
The user is sole owner and contributor and reviews their own project documents.

## Boundaries & Constraints

**Always:** Work in the repository owning each capability. Persist Timesheets
domain state only through EventStore. Keep production gates closed until actual
configuration authorizes the operation. Bind observations to time, source,
target and command. Preserve the expired candidate, original verifier refusal,
past evidence, unrelated edits, dependencies and gitlinks. Use actual global.json.

**Live boundary:** Before deployment, recovery, migrations, permission changes or
other live mutation, prepare exact commands, target, expected effects and recovery
for approval. Existing-access read-only inspection and local implementation/tests
are authorized now. Ask only for genuinely unavailable inputs or final concrete
operational approval. Do not infer approval from elapsed time.

**Completion:** Story 3.6 remains incomplete until deployed TLS/client identity,
Fresh catalog, persisted confirm/adjust, one concurrent winner, replay denial,
restart persistence, subscriber/shared-rebuild and legacy authority gates pass.
Local fixtures and offline preparation retain their narrower meaning.

## I/O & Edge-Case Matrix

| State | Expected outcome |
| --- | --- |
| Wrong native endpoint, disabled TLS verification or changed cluster UID | Stop inspection before dependent requests |
| Credentials or environment values in native objects | Omit from retained observation |
| Missing grant, image, deployment or historical source | Record unavailable and keep dependent acceptance closed |
| Expired historical candidate or refused verifier | Preserve unchanged; prepare separate fresh prerequisite |
| Properly scoped signed production binding | Accept only exact authorized operation/resources and valid signature |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Platform/eng/timesheets-deployment/` owns hosting packages
  and sanitized native observation; use the architecture's maintained Helm fallback.
- `references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.md`
  and `.json` own the current operational handoff. Platform manages staging and
  production configuration, deployment, promotion and recovery; Timesheets owns
  module contracts, authoritative inventory and domain acceptance. The earlier
  Timesheets packet proposal remains historical and unchanged.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/` and
  `Hexalith.EventStore.ServiceDefaults/` own reusable workload assertion transport
  and production signing, never Timesheets domain hosting boilerplate.
- `references/Hexalith.Platform/eng/recovery-preparation/` owns fresh create-only
  prerequisites. Read current sole-owner policy and evidence-production plan;
  documentary self-review does not assert observed runtime or native rights.
- `scripts/run_isolated_magic_link_dapr.py` owns disposable local acceptance;
  never point it at deployment or shared writable infrastructure.
- `docs/deployed-magic-link-acceptance.md`, original spec-23 and checkpoint retain
  prior evidence; add current intake without rewriting pinned historical bytes.

## Tasks & Acceptance

- [x] Implement missing hosted package and reusable production assertion capability;
  verify focused behavior and defaults without installing live grants.
- [x] Collect sanitized current TLS/client identity, access rights and installed
  resources using existing context and verify negative inspection boundaries.
- [x] Prepare separate fresh recovery prerequisite packet preserving expired
  originals/refusal, exact commands and explicit operational blockers.
- [x] Build with pinned SDK and run focused unit/integration and disposable
  persistence journey; record broad blockers separately from successful checks.
- [x] Reconcile acceptance, checkpoint and launch status; prepare executable live
  approval only when exact inputs and recovery are available.
- [ ] Qualify and execute the owner-approved live deployment/recovery/authority
  enrollment and deployed acceptance after the blocked operational prerequisites
  are met. This keeps the active continuation and parent story in progress.

**Acceptance Criteria:** Given existing access, when inspected, then current
observations identify exact context/cluster/certificate/rights and installed
resources or honest absence. Given a live mutation, when proposed, then its
reviewable command/target/effect/recovery precede owner approval. Given only
local passing evidence, when tracking is updated, then deployed Story 3.6 remains
in progress with each missing factual prerequisite named.

## Implementation Notes

User's current authorization supersedes earlier preparation-only restrictions
for this new continuation. Original spec-23 and its frozen intent stay intact.
Delegated investigations own disjoint Platform hosting/recovery and EventStore
authority surfaces; root owns native inspection and Timesheets evidence.

Final reviewed Debug solution build passes zero warnings/errors; six pre-review
default suites pass 1,899 cases with five declared skips. Reviewed focused checks
pass 83 authority/rate cases, 45 Platform cases and one composed HTTP persistence
regression. A fresh disposable runtime passes 1/1 with existing host assets and
exact owned cleanup/shared preservation. Initial and reviewed local Debug archive
identities are separate, with the reviewed copy retained in private local custody.
No released or deployed production artifact is claimed. All review patches and
follow-up checks preserve earlier evidence as dated snapshots.

## Spec Change Log

## Review Triage Log

All three independent layers reviewed the same 610,404-byte multi-repository
snapshot before triage. Each finding is retained below, including duplicates.
Patches preserve old evidence and remain inside the user's authorized technical
implementation; no new operational permission is inferred.

| ID | Verdict / route | Verified evidence and disposition |
| --- | --- | --- |
| B1 | medium / patch | Failed initial cluster GET followed the optional-query continue branch; stop before dependent queries. |
| B2 | medium / patch | subprocess capture accumulated unbounded bytes before checking 16 MiB; bound reads while collecting. |
| B3 | medium / patch | Deployment annotations came from root metadata, missing Dapr pod-template settings; use controller template annotations. |
| B4 | medium / patch | Config/route summaries omitted actual ACL/backend/acceptance fields required to inspect future installed wiring; retain a typed nonsecret subset. |
| B5 | high / patch | PostAsync buffered response before the 64 KiB check; enforce headers-only and bounded body handling. |
| B6 | medium / patch | Each bound request downloaded metadata and keys twice beyond token acquisition; cache metadata by trust config with controlled refresh. |
| B7 | medium / patch | Actual Program global limiters put all new authority routes into unrelated anonymous buckets; isolate bounded authority partitions. |
| B8 | medium / patch | Configured issuer URL path could disagree with Program's mounted authority prefix; fail closed on disagreement. |
| B9 | high / patch | Global discovered endpoint survived authority rotation and could receive a new secret; key discovery by current trust identity and test rotation. |
| B10 | low / patch | Current report's packet index did not yet exist in the review snapshot; create the index before final verification. |
| E1 | high / patch | Duplicate of B5, same new bound response path and ineffective ordering; preserve and cover with the same patch. |
| E2 | medium / patch | Duplicate of B2; bounded-stream collector patch covers the demonstrated over-budget path. |
| E3 | high / patch | Subsequent kubectl calls re-read mutable ambient config; explicitly pin endpoint/TLS and compare context/cert/UID at the end. |
| E4 | medium / patch | Duplicate of B1; unavailable identity must stop, not merely a mismatching successful UID. |
| E5 | low / patch | Correctly hashed JSON array triggered AttributeError before bounded refusal; validate root/nested types. No execution was allowed. |
| E6 | medium / patch | Documented relative owner-policy input always refused the absolute-path custody guard; fix the documented absolute command. |
| V1 | medium / patch | Grant/certificate fixtures outlived token duration; expiry clipping deletion survived tests. Add short-boundary assertions. |
| V2 | medium / patch | Synthetic handler ignored the transmitted client secret, hiding serialization failures; exercise real form credentials through mapped HTTP authority. |
| V3 | medium / patch | Independently reproduced B1: all later native queries ran after failed UID lookup; cover unavailable identity explicitly. |
| O1 | medium / patch | Private registry image had no pull-Secret reference in the candidate pod. Require an existing named pull Secret without granting app access to its data. |
| F1 | medium / patch | Follow-up B4: standard Ingress path backends were still omitted; typed service backend projection and regression added. |
| F2 | medium / patch | Follow-up B4: flattened route/Gateway conditions lost parent/listener identity; retain exact owner references and test multiple parents. |
| F3 | high / patch | Follow-up E3: before/after comparisons could miss transient credential/CA changes; freeze the existing inline client in sealed anonymous memory used by every network child. Kernel-seal/closure and rejection tests pass; no credential copy is persisted. |
| F4 | medium / patch | Final B4 follow-up: default-only Ingress service backend was omitted; include defaultBackend.service and assert it explicitly. |

All rows are addressed. Independent targeted follow-ups confirm authority expiry,
actual transmitted credentials, bounded reads, rotation, metadata refresh, mapper
metadata/global limiting, recovery input shapes, pull Secrets and immutable native
context. The last Ingress default-backend correction passes its explicit local
assertion. Same-authority signing-key rotation can deny new issuance until the
five-minute metadata refresh; this fail-closed availability limitation is documented.

## Verification

Run owner-repository focused tests, Timesheets Debug source-reference restore/build,
all six test executables individually and the disposable Dapr acceptance runner.
Inspect unified diffs without staging or Git mutations. Record source revisions,
changed-file hashes and successful and failed commands in the new evidence packet.

## Local resumption — 2026-10-22

Offline inspection at **2026-10-22T18:03:01.309419+00:00** checked these clean
`main` source identities directly with `git rev-parse HEAD` in each repository:

| Repository | Checked source revision |
| --- | --- |
| Timesheets | `482a55ecd7bc20fca855c706a10b0135b8ef1f21` |
| Platform | `c65d87a6f8f4a05fa3bea4e24ac778bf23f12cd6` |
| EventStore | `5ce5f1d84b88c808cc8a526b9decf149de977e55` |

The [Platform handoff](../../references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.json)
still reports `BLOCKED_NOT_FOR_EXECUTION`, `approvalGranted=false` and
`executableDispatcherExists=false`. Both approved staging values files remain
absent. Its exact tool `/tmp/timesheets-hosting-helm-4.2.0/helm` is also absent:
`ls -la /tmp/timesheets-hosting-helm-4.2.0/helm` exited **2**, reporting
`No such file or directory`. Both retained chart archives exist and their SHA-256
digests match the handoff; those local matches supply no release or deployment
acceptance. Platform staging foundation/deployment stories 4.14–4.22 remain
`backlog`, and recovery Story 4.27 remains `in-progress`.

EventStore's [2026-10-18 authority withdrawal](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/6-4-historical-retirement-20261018/authority-overlay.json)
withdraws present qualification authority from the dependent **187/187** and
both **83-case** authority preparation/review results, with replacement evidence
recorded as null. The earlier implementation and review paragraphs above retain
their dated meaning; they must be read together with this withdrawal. Original
source snapshots and receipts remain preserved. The newer [2026-10-18 direct
EventStore proof](evidence/3-6/20261018-direct-eventstore-local-proof/README.md)
records local SDK 10.0.402 qualification and explicitly excludes deployed and
legacy acceptance; it supplies no replacement authority qualification.

The latest recovery allocation found in the scoped local `4-0` custody directory
and Platform Story 4.27 evidence remains unsigned/incomplete RID
`20261017t143202z`. Its unchanged exclusive ceiling **2026-10-18T14:32:02Z** is
past the observation time. No newer recovery/enrollment record or approved
staging values were found in those inspected locations; this bounded local
inspection makes no claim about uninspected sources. The original expired
candidate and verifier refusal remain unchanged. No remote or native request
was made, so the 2026-10-17 TLS/client/rights observations remain historical.

The remaining live task stays unchecked. A concrete operational approval still
depends on qualified current recovery/adapters, retained immutable Release
closure, Platform environment values/foundations, actual issuer/signer/client
grants, server-side providers and full legacy inventory/continuity/maintenance
evidence. The missing pinned tool must also be supplied and qualified before
its command can be reviewed for execution. Story 3.6 stays `in-progress`;
spec-23 stays `draft`, the 4.1 operational gate stays closed and release stays
**FAIL**. Only this additive spec entry changes; no earlier evidence, code,
dependency, gitlink, task status or parent status is rewritten. Local JSON/path/
package inspection passed; unchanged builds, suites, old verifiers and the
disposable runtime were not repeated for this documentation-only resumption.

## Current prerequisite intake — 2026-10-29

The [new bounded intake](evidence/3-6/20261029t092023z-continuation-intake/README.md)
records Timesheets `bc97acea37435fea73cc2fa818d27a1d053933fa`, Platform
`8de90958bc41b7fd01b7ed22b8ab364447a800e4` and EventStore
`41456b6ec8b8baf8e0ac4d0ff2f5e86f7a10be16`. Existing-access read-only native
inspection completed at **2026-10-29T09:21:23.024621+00:00**, collector exit
**0**. It pinned the expected API and verified TLS, matched the expected cluster
UID before and after inspection, and authenticated `jpiquot` with the selected
client certificate. Current native rules were recorded without claiming
revocation history or Timesheets application/migration authority.

The queried resources contain neither staging namespace nor an identifiable
Timesheets workload, Service, pod, ingress, component, subscription or Dapr
configuration. Gateway and HTTPRoute queries each exit **1**, classified as
resource-type absent. This bounded census cannot exclude renamed installations
or certify complete legacy coverage. No Secret object, environment value or raw
native object is retained.

The Platform handoff remains `BLOCKED_NOT_FOR_EXECUTION`, with approval and
dispatcher false. Both approved values files and its exact Helm binary remain
absent; both retained chart archives match their declared SHA-256 digests. In
the inspected local recovery custody directory, the latest RID remains
`20261017t143202z`, expired at **2026-10-18T14:32:02Z**, with all six operational
results null and twelve evidence categories unestablished. No replacement is
allocated merely to refresh preparation time. Qualified current recovery and
adapters, complete immutable Release closure, actual signer/client grants and
server-side providers, and certified legacy continuity/maintenance still block
an executable approval bundle.

The retired EventStore authority-overlay path referenced in the October 22
snapshot is absent in the current checkout; its [current authority
documentation](../../references/Hexalith.EventStore/docs/security/workload-binding-authority.md#validation)
still expressly withdraws the earlier 187/187 and two 83-case results. They were
not rerun or reinstated. The existing collector's **16/16** safety tests pass,
including endpoint/TLS/cluster refusal, credential suppression and immutable
context checks. Those checks and current native access establish neither
production workload authority nor runtime acceptance. The current Aspire
13.6.4 runtime remains unqualified under the retained October 28 evidence.

The remaining live task stays unchecked, Story 3.6 remains `in-progress`,
spec-23 remains `draft`, the 4.1 operational gate stays closed and release stays
**FAIL**. This intake changes no code, dependency, gitlink, historical evidence,
expired packet or original verifier refusal. No deployment, recovery, migration,
grant change, domain write, build or runtime journey is performed.
