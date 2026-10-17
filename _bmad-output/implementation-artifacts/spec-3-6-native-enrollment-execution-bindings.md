---
title: 'Prepare native measurement enrollment and execution bindings offline'
type: 'chore'
created: '2026-10-16'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: ee95f90f529812c744761090691a23196c971c78
context: []
---

<frozen-after-approval reason="User explicitly approves offline preparation, implementation, synthetic tests and independent review; no intermediate confirmation">

## Intent

Complete the offline safeguards for the remaining enrollment/execution bindings
of Story 3.6. Create a separately reviewed create-only revision anchored to sealed
supplement SHA-256 a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631.
Missing operational facts remain explicit blockers; this increment cannot invent
trust or provide operational acceptance.

## Boundaries & Constraints

Use owning Platform /home/administrator/projects/hexalith/platform for adapters
and Timesheets /home/administrator/projects/hexalith/timesheets for acceptance.
Read their permitted Hexalith baseline. No skills under references. Preserve all
existing source bytes, packet revisions, signed originals, expired recovery and
failure history. Add new files and retain failed/reviewed revisions. No staging,
commit, push, branch, dependency or gitlink changes; no actual kubeconfig/private
credential reads, authentication, network/API/provider/IAM calls, preflight replay,
signing, publication, infrastructure change, capture, restore, retirement,
upgrade, deployment or domain writes. No Aspire start or live TLS handshake.

Keep public live enrollment/execution unconditionally disabled before arguments,
files, credentials or sockets. Synthetic qualification accepts inert fixtures only;
public identity metadata, caller assertions and matching hashes never authenticate
or grant authority. Preserve all 23 ordered GET selectors, five unsupported
families, limits and stop conditions in the sealed supplement. Preserve preparation
2026-10-15T16:27:56Z and ceiling 2026-10-16T16:27:56Z; sample actual UTC, never
refresh or allocate recovery. Spec-23 draft, Story 3.6/Epic 3/4.27 in progress,
4.1 closed, release FAIL remain mandatory.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
| --- | --- | --- |
| Complete synthetic fixture | Exact plan, one credential fixture, nonsecret witnesses | Bounded synthetic pass; zero API calls; authority false; gates closed |
| Missing trust or authority | Null/partial server, CA/peer, client, independent grants, revocation or rights | Explicit missing fields; no enrollment or execution |
| Delivery/custody fault | Wrong credential kind/digest, reuse, extra credential, unsafe slot/alias | Fixed refusal, no fallback, no overwrite |
| Runtime/response drift | Wrong OpenSSL profile, plan, unsupported selector, malformed response or limit | Stop before dependent work, no retries or raw output |
| Expiry or clock rollback | Ceiling reached, pre-preparation time, regression between steps | Stop synthetic execution simulation; offline inspection remains possible |
| Live invocation | Arbitrary args or poisoned I/O | Refuse before inspecting inputs or side effects |

</frozen-after-approval>

## Code Map

- Platform eng/recovery-preparation/native_measurement_adapter.py: request_plan,
  contract, typed sanitization, SyntheticTransport, qualify_synthetic; preserve.
- Platform eng/recovery-preparation/native_https_transport.py: fixed-IP dormant
  parser, bounded no-follow source compilation; preserve. No actual TLS request.
- Platform eng/recovery-preparation/verify_native_supplement.py: offline integrity
  verifier; preserve. Original verifier currently refuses because Platform
  sprint-status SHA differs (sealed 1d66b846fb554deefa67acb44711671376d5d354f74558ffb9b34a87a638283d,
  current 2d0c7c85dc259481180cd9c65fb327c32706e9ddbfa0c1a01fcd848da2d2d41e).
- Exact supplement path: /home/administrator/hexalith-recovery-evidence/4-0/20261016t063900z-native-measurement-supplement.
  adapter-binding-v2.json, exact-selectors-v2.json, authority/custody/runtime plans
  bind scope but lack current identity/authority. Do not modify any member.
- New revision: /home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision;
  preservation-before.json already records 557 existing safe nonsecret files.

## Tasks & Acceptance

Execution subagent owns only the first two tasks; coordinator completes evidence,
packet sealing, acceptance updates and independent reviews after implementation.

- [x] Platform eng/recovery-preparation/native_enrollment_execution.py: add dormant
  enrollment/execution safeguards. Validate exact typed nonsecret binding schema,
  completeness and scope/rights against fixed selectors; protected one-credential
  synthetic delivery consumed once, no credential logging/fallback. Separate
  independent witness requirements from hash integrity. Use explicit strict TLS
  profile/OpenSSL behavior requirements without default trust/config loading.
  Bind full output custody with descriptor-relative no-follow exclusive directory
  and file creation, one attempt, modes, sizes, no partial raw results. Exercise
  execution only through existing exact synthetic transport and typed sanitizer;
  recheck clocks and immutable bindings at step boundaries, preserve limits.
- [x] Platform eng/recovery-preparation/test_native_enrollment_execution.py: cover
  every matrix row with inert bytes/mock transport/temp directories. Do not read
  real config, credentials or create real output slots. Compile fixed siblings
  directly without pyc. Retain exact command output including failures separately.
- [x] Coordinator: create revision schema/binding, evidence-gap ledger, custody,
  OpenSSL/response compatibility requirements, source/runtime inventory, verifier,
  review snapshots and preservation receipts. Independent reviewers inspect actual
  bytes. Recheck old immutable payloads individually and record prior status drift
  without repairing originals or claiming the old full verifier passes.
- [x] Coordinator: update Timesheets checkpoint/story/handoff/test evidence with
  exact manifests/digests/results/blockers while retaining protected statuses.

Acceptance: Given absent independently established current witnesses, when assessed,
then no executable operational approval prompt is emitted. Given sealed revision,
when reverified, then actual source/runtime/payload and preservation bytes match;
all history and prior failures remain. Given complete synthetic fixtures, when
tested, then identity/revocation/rights and delivery/custody failures stop dependent
work and all receipts remain synthetic with closed authority and operational gates.

## Implementation Notes

No intent gaps or irreversible work in this offline increment. Unresolved live
facts are evidence gaps. User scope overrides workflow pauses and staging.

## Spec Change Log

## Review Triage Log

### Independent review pass 1

| ID | Verdict | Verified evidence and route |
| --- | --- | --- |
| B1 | medium | Full proposed runtime/TLS profile is unchecked beyond inventory digest. patch: compare complete fixed inert profile and regress mutated flags/interpreter/TLS. |
| B2 | medium | Wrapper/proposal kinds, schemas, RID and complete field sets are unchecked. patch: exact shape and typed fixed identity validation. |
| B3 | medium | Python dict equality admits bool/int substitutions in policy values. patch: typed canonical comparisons and substitution tests. |
| B4 | medium | Gap ledger accepts eight nulls instead of twelve exact unmet prerequisites. patch: exact IDs, unique complete entry shapes and historical-only classifications. |
| B5 | medium | Preservation-after outcome is hashed but not semantically checked. patch: exact after-map/outcome validation, including separately recorded concurrent status drift. |
| B6 | medium | Pathname cleanup can delete a foreign replacement scratch directory. patch: descriptor-bound cleanup with finalizer detached; duplicate root cause E1 retained. |
| B7 | medium | Final clock sampled before potentially slow custody check. patch: enforce clocks after custody and regress expiry during final check. |
| B8 | low | Missing field masks malformed populated enrollment kind during inspection. patch: validate populated fields while collecting missing facts. |
| B9 | medium | Exact transport instance can shadow request with a callback. patch: fixed method dispatch and exact fixture storage validation. |
| B10 | low | Retention failure retains success-only binding/runtime hash fields. patch: consistent refusal field removal. |
| E1 | medium | Reproduced root rename/replacement makes cleanup delete replacement marker. patch: same demonstrated cleanup defect as B6, retain independent finding. |
| E2 | medium | Response content_type comparison executes a supplied __eq__ callback. patch: exact inert field types before sanitizer, no callback invocation. |
| V1 | medium | Removing current client/witness validity checks leaves 31 tests green. patch: narrowed windows, future observation and between-request expiry tests. |
| V2 | medium | Replacing every measured output with empty object leaves 31 tests green. patch: independent exact selector/output assertions for returned and retained receipts. |

Concurrent Platform movement to 114e08830da2d8cca0e77cd870b8867506b4c68b
was performed outside this task. It commits current files and changes only the
unrelated Story 1.7 status from review to done. Preserve that history; no own Git
mutation occurred. Initial 557-file baseline remains immutable; its one concurrent
status change must be reported separately, never called unchanged.

### Independent review follow-up

| ID | Verdict | Verified evidence and route |
| --- | --- | --- |
| B11 | medium | Historical classifications are accepted against an enum without source association. Patch exact source-to-classification map and swapped-class regression. |
| B12 | medium | Exact SyntheticClock instance still dispatches an injected sample callback. Patch fixed method dispatch and storage validation; same root as E4. |
| E3 | medium | Post-construction poisoning of credential _kind invokes comparison callback. Patch exact stored state types before any operation; regress each mutable slot. |
| E4 | medium | Clock method/storage poisoning invokes callbacks before sample validation. Patch fixed method and exact tuple/int storage; duplicate B12 retained. |
| V3 | medium | Deleting current concurrent-status digest pin leaves 20 tests green. Patch resealed internally consistent unknown-status-drift negative test. |


## Verification

Use /usr/bin/python3.14 -I -S -B for new tests and original three synthetic suites;
retain all outputs, no package install or restore. Run existing built Timesheets
LaunchReadiness executable (13 tests), no live suite. Compare 557 preservation
files before/after, all original packet payloads, runtime inventory and index/gitlinks.

Coordinator pre-review checks: original suites 70/70, enrollment safeguards 31/31,
new revision verifier 15/15, built LaunchReadiness 13/13. Both output-slot
collision and post-write expiry/source/receipt tampering corrections are covered.
All failed logs and first source/packet candidate bytes are retained. Final
packet sealing and acceptance documentation await independent source/packet review.

## Final offline result

135 synthetic tests (27/17/26/43/22), 13 built LaunchReadiness and all three
independent review layers/follow-ups pass; all 19 finding rows are closed.
Final independent sealed-byte check at 2026-10-16T11:10:46Z passes all 88 payloads,
89 files, 75 untouched preparation originals and four current sources. No
operational evidence was substituted for missing external facts.

Final manifest is c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d
in the sibling preparation directory ending native-enrollment-execution-revision-sealed.
Active binding v4 differs only in the explicit final output root; verifier ROOT
selects that directory. Eight new unsealed logs had 0644 modes; their originals
remain untouched, and all 75 preparation files were copied into final 0700/0600
custody with their original byte/mode inventory retained. No recovery/timestamp
refresh occurred. The verifier reports 556 unchanged of 557 plus the specifically
pinned concurrent status change, and never reports the original full verifier
as passed. All original packets, signed originals, expiry and failure history persist.

[Exact final evidence and prerequisite](evidence/3-6/20261016t103632z-native-enrollment-execution-bindings/README.md)
record the fixed command, bytes, source identities, tests, independent review and
unmet authority/runtime requirements. Spec-23 stays draft, Story 3.6/Epic 3/4.27
in progress, 4.1 operational gate closed, release FAIL. User instructions override
workflow automatic status advancement/commit: no staging, commit, push, dependency
or gitlink operation was performed by this task. No operational approval prompt
is ready and no operational stage ran.

Completion at 11:12 UTC: final offline verifier exits 0 again, built fitness
passes 13/13 in 0.129s, Timesheets diff check and scoped four-file Platform check
exit 0. Concurrent Platform HEAD is 9256c675186b020929d67230a22892bb13488bc2.
Repository-wide `git diff --check` exits 2 at
`src/Hexalith.Platform.Custody/Hexalith.Platform.Custody.csproj:12` for trailing
whitespace in an unrelated concurrent change; preserve it outside adapter scope.
Both gitlink inventories remain unchanged and both staged diffs are empty.
The final manifest and 75 preparation originals still match their sealed records.
