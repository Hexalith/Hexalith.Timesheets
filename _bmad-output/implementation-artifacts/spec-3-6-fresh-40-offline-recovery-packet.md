---
title: 'Prepare the fresh Story 4.0 recovery packet for Story 3.6'
type: 'chore'
created: '2026-10-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 7db070f90d727889a3f431d6ea32f7a3c1dace2d
context: []
---

<frozen-after-approval reason="explicit preparation-only authorization in this session">

## Intent

Prepare a fresh, concrete, unsigned Story 4.0 policy/bundle with exact local
source, tool and custody bindings. The user authorized preparation after reading
Story 3.6's checkpoint and NEXT-PREREQUISITE.md. This authorization supplies no
operational authority. The existing expired recovery must remain unchanged.

## Boundaries & Constraints

Work in `/home/administrator/projects/hexalith/platform` for Platform files and
`/home/administrator/projects/hexalith/timesheets` for Timesheets records. Preserve
all existing changes, decisions, signed originals and failure history. Do not
stage, commit, push, branch, update dependencies/submodules or use skills under
references. Follow the permitted Hexalith baseline. Platform currently has
unrelated dirty changes; the user explicitly requires preservation. Do not edit
those files. No network/provider/API calls, credential loading for authentication,
capture, restore, decryption, infrastructure mutation, signing, publication,
retirement, upgrades, deployment or completed-stage replay. Hashing existing
non-secret sources/public trust roots is permitted. Never read private key or
credential contents. Preparation outputs are local files only.

Allocate one fresh UTC RID under the existing private recovery custody root,
create only, mode 0700 directories/0600 files. Do not run legacy preparers or
operational tools. Preserve `20261014t112353z` byte-for-byte. Retain historical
source pins with explicit historical labels; current unmeasured identities must
remain null and named closed blockers. Do not copy old provider observations,
proofs, signatures or acceptance into fresh output slots. Keep policy preparation
time distinct from capture/acceptance, with the unchanged 24h policy-age ceiling.
Keep Story 3.6/Epic 3/4.27 in progress, spec-23 draft, 4.1 closed, release FAIL.

</frozen-after-approval>

## Code Map

- Platform `eng/kubernetes-upgrade/GUARDED-RECOVERY-PREPARATION.md` and
  `recovery_readiness.py`: existing 4.1 preparation; preserve unchanged.
- `/home/administrator/hexalith-recovery-evidence/4-0/20261014t112353z`:
  expired signed originals and tools; read/hash only. Policy has retained
  destination, roles, canaries, 1800s RPO/86400s age/30d retention decisions.
- `/home/administrator/hexalith-recovery-evidence/4-0/final-run-tools`:
  historical tools; never execute, including policy/preflight/selftest commands.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261014t131927z-s427-private-oidc-correction`:
  accepted private OIDC source and incident history. Preserve loopback 38080/28080,
  disabled proxies/redirects, all eight checks and failed-original lineage.
- `/home/administrator/hexalith-retirement-investigation-evidence/20261014t144239z-s427-accepted-oidc-continuation`:
  split Memories safeguards/semantic wrapper, actual C3, one final pre/census,
  <=900s drain, paired stores, immediate approved conditional resume.
- Previous reviewed packet at private directory
  `20261015t140709z-s427-expired-recovery-preparation`: immutable historical
  preparation input; do not replay helper/preparer or rewrite it.

## Tasks & Acceptance

**Execution:**
- [x] Platform `eng/recovery-preparation/` — create offline packet verifier and
  focused tests. It must have no operational mode/import of frozen legacy tools,
  authenticate no approval, and always report operationalGate closed. Validate
  exact hashes, custody, no symlinks/path escape, absent operational artifacts,
  null missing measurements and preserved historical inventory. Tampering must
  fail; complete offline preparation may pass with named operational blockers.
- [x] Fresh private `4-0/<new-RID>/` — create unsigned candidate policy, source/tool/
  custody bindings, inert frozen historical source, preservation inventory,
  ordered inert operation plan, checksum inventory and packet README. Bind all
  retained decisions, new attempt-scoped target proposals and existing public
  signer/recipient trust bytes. Hash current local binaries without running them.
  Legacy scripts lack fresh safe adapters: label reference-only and keep them
  outside runnable tools/RID layout. Name that as a real blocker.
- [x] Fresh packet `NEXT-OPERATIONAL-APPROVAL-v4.md` — exact immediate approval
  for Administrator protected local static transport preflight only. Bind the
  source/tool/custody and sanitized receipt; future network measurement selectors
  remain inert pending executor qualification and distinct measurement approval,
  before revised exact execution/signing approval. No broad execution grant.
- [x] Platform sanitized evidence under implementation-artifacts/evidence/epic-4/
  4-0/<new-RID>-offline-preparation — record hashes, limits, checks and blockers.
  Do not update Platform sprint status or unrelated source/configuration.

**Acceptance Criteria:**
- Given the expired RID, when preparation and local checks finish, then every
  inventoried original is unchanged and no operational stage has run.
- Given missing current measurements or unsafe historical executors, when the
  concrete packet is verified, then they remain explicit blockers, no current
  recovery is claimed, and operationalGate remains closed.
- Given tampered/missing/escaped/symlinked packet inputs or a fabricated completed
  stage, when the verifier runs, then it rejects the packet without remote calls.

## Implementation Notes

Review follow-up produced a separate local-only static transport preflight and
an additive explicit v4 packet. Current kubeconfig contents were not read during
preparation; the immediate proposed approval covers only the protected local
preflight. Future native/provider measurement selectors stay inert pending exact
executor qualification and separate approval. V1/v2/v3 are retained history.

The parent will update Timesheets checkpoint, story and launch records after
independent validation. The preparation-only user instruction authorizes this
implementation; no intermediate spec approval is required. No operational
approval is granted. Intent gaps: none. Irreversibles: none within preparation.

## Spec Change Log

- 2026-10-15: Review narrowed the immediate proposed approval to the protected
  local static transport preflight. Future native/provider measurements remain
  blocked until exact executors/runtime/authority are qualified and separately
  approved. This stays within the user's offline preparation scope.
- 2026-10-15: Preparation scope completed and verified in explicit v4. This
  prep spec is done; parent Story 3.6/Epic 3/4.27 remain in progress, spec-23
  draft, 4.1 closed, release FAIL. The user's explicit preservation/status
  instructions override workflow commit and parent review-status steps. No
  staging, commit or sprint-status edit was performed.

## Review Triage Log

All three review layers reported before triage. Findings are recorded individually before grouping. Fixes affect internal preparation artifacts and checks; no domain/platform public API or operational authority is added. The local transport preflight is prepared and tested with synthetic input only. User preservation requirements govern additive revisions; sealed v1/v2 and expired originals are never reverted or overwritten.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | medium | Historical source pins only validate two flags, permitting omission of concrete endpoints/UIDs/storage. Reconstruct exact inventoried historical sources; patch. |
| B2 | medium | Nested independent-authority current/lineage fields are unchecked. Compare the complete historical authority record, retaining false/null current evidence; patch. |
| B3 | medium | Active checksums do not enforce the retained manifest payload map, permitting an internally inconsistent original packet. Validate retained maps against active files; patch. |
| B4 | medium | Metadata is reopened after initial checksums and there is no final complete recheck. Revalidate parsed bytes and final inventory, with race regressions; patch. |
| B5 | low | parse_constant does not reject exponent overflow such as 1e999. Add finite parsed-float validation and regression; direct patch. |
| B6 | low | Stage one names the original approval file while the active handoff names v2. Add the reviewed plan revision with explicit active approval filename/digest; patch. |
| B7 | low | The Timesheets receipt is pending and its linked verification.json does not yet exist. Complete the already planned sibling receipt before handoff; patch. |
| B8 | medium | The unopened kubeconfig has no reviewed static transport gate before proposed GETs. Bind a local-only preflight and restrict the immediate request to its protected local run; network lanes remain blocked; patch. |
| B9 | medium | The future pod selector omits init-container names/imageIDs, leaving dependency image inventory incomplete. Correct the inert future selector; patch. |
| B10 | medium | The future PV output omits local/host/CSI backing identities needed for comparison with retained storage pins. Correct the inert selector; patch. |
| B11 | medium | Provider methods lack a bound executor/runtime and cannot be an immediately executable approval. Explicitly block that lane and narrow the concrete next action to the prepared local preflight; patch. |
| E1 | medium | Synthetic replacement between checksum and semantic load returns a pass for changed bindings. Same metadata race as B4; patch. |
| E2 | low | A list of historical root strings passes set equality and reaches uncaught .items AttributeError. Require an object and structured refusal; direct patch. |
| E3 | medium | The second metadata-race claim independently reproduces a manifest mismatch after semantic reopen. Same defect as B4/E1; patch. |
| E4 | low | Malformed nested roots can omit the promised structured closed-gate report. Same nested-shape defect as E2; direct patch. |
| V1 | medium | Pre-verified mutation evidence: deleting the Memories guard leaves all 24 tests green because the named combined test changes only OIDC. Add rejection subtests for every Memories requirement and active revision; patch. |


Follow-up review before final handoff:

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| F1 | medium | Follow-up mutation replaced the Memories comparison with a key-only check and all 33 packet tests still passed. Add value changes for every boolean/drain/store requirement, plus value-guard mutation detection; patch. |
| F2 | medium | Follow-up jointly rewrote original README, SHA256SUMS and packet.json and resealed the active map; preservation still passed. Pin prior manifests to independently retained reviewed digests; patch. |
| F3 | medium | Local preflight collects child pipes with communicate before applying caps. Enforce streaming byte/deadline bounds and kill/reap the owned child on overflow; patch. |

## Verification

All 19 findings above were patched. Targeted independent follow-up found no
remaining defects in historical anchors, streaming bounds, secret-safe receipts
or Memories omission/value enforcement. Removed/key-only Memories guards cause
16/8 assertion failures respectively, without errors.

Independent parent verification passed 34 packet tests and 12 synthetic local
preflight tests using isolated Python (`-I -S -B`). Actual explicit v4 packet
verification passes closed with 31 blockers. An independent checksum/custody
pass matches 139 payloads, 140 packet files, all 132 retained prior files,
348 historical files, 107 frozen sources, eleven binary roles and five source
files. Manifest SHA-256:
`1ed5eec5579adeedf7e49519fcba125656caacbaaeaffb6509c49b6575714b14`.
All eight historical signatures authenticate; the older inventory's 161/162
result records pre-existing unrelated sprint drift. Failure history is retained.
Timesheets LaunchReadiness passes 13/13 on SDK 10.0.401. Actual kubeconfig and
local preflight were not run; no broad live or operational suite ran.

See the [final receipt](evidence/3-6/20261015t162631z-fresh40-offline-preparation/verification.json)
and [exact next approval](evidence/3-6/20261015t162631z-fresh40-offline-preparation/NEXT-PREREQUISITE.md).
