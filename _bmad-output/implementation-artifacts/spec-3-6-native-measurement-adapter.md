---
title: 'Qualify a bounded native Kubernetes measurement adapter offline'
type: 'chore'
created: '2026-10-16'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 7db070f90d727889a3f431d6ea32f7a3c1dace2d
context: []
---

<frozen-after-approval reason="explicit user authorization for offline preparation, qualification and independent review in this session">

## Intent

Prepare and synthetically qualify an exact read-only native Kubernetes
measurement adapter and separate create-only supplemental packet for RID
20261015t162756z. Reviewed packet-v4.json is immutable input, SHA-256
1ed5eec5579adeedf7e49519fcba125656caacbaaeaffb6509c49b6575714b14.
Read the recorded user-reported preflight receipt observed at
2026-10-15T19:51:03.842182Z; do not replay it or infer authority from it.

## Boundaries & Constraints

Work in the owning Platform checkout /home/administrator/projects/hexalith/platform
for adapter and packet preparation, and Timesheets for its handoff. Preserve
existing changes, signed originals, failure history, every packet revision and
expired recovery 20261014t112353z. No staging, commits, push, branching or dependency
updates. Read the root-declared references/Hexalith.AI.Tools baseline; never load
skills under references. User authorization overrides workflow dirty-tree and
intermediate approval pauses for this already authorized offline work.

No actual configuration/credential reads, authentication, API/provider/IAM/network
requests, preflight replay, infrastructure operations, capture, restore,
decryption, signing, publication, retirement, upgrade, deployment or completed
stage replay. Provider/IAM/SSH/Docker remain blocked. Test with synthetic inputs
and mocked transports only. Live activation must fail closed before loading any
configuration or credential while exact authority/runtime bindings are unresolved.

Preserve policy preparation 2026-10-15T16:27:56Z and ceiling
2026-10-16T16:27:56Z; check real UTC and report expiry without refreshing either.
Story 3.6/Epic 3/4.27 stay in progress, spec-23 draft, 4.1 closed, release FAIL.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
| --- | --- | --- |
| Qualified synthetic run | Exact sealed bindings and mock responses | Typed sanitized bounded receipt; closed gates |
| Tampering or scope expansion | Wrong source/runtime/packet hash, selector or identity | Refuse before transport; no fallback |
| Failure, timeout, overflow | Mock error, redirect, malformed/oversized response, deadline | Stop; zero dependent requests/retries |
| Secret injection | Sensitive values in body, diagnostic or malformed input | No raw body/diagnostics/secret in receipt |
| Expired or unresolved binding | Ceiling reached or null authority | Refuse live activation before credential access |

</frozen-after-approval>

## Code Map

- Platform eng/recovery-preparation/: existing verify_packet.py and
  static_transport_preflight.py and their tests are frozen v4 sources; preserve.
- Private /home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z:
  packet-v4.json, bindings-v4.json, FUTURE-MEASUREMENTS-v4.md,
  static-transport-preflight-v4.json and policy/recovery-policy.candidate.json
  are nonsecret immutable planning inputs; no latest or fallback lookup.
- Platform evidence/epic-4/4-0/20261015t195103z-local-static-transport-receipt/
  validation.json: user-reported schema pass, five matching hashes, no authority.
- eng/kubernetes-upgrade/GUARDED-RECOVERY-PREPARATION.md: closed independent
  native access requirement; retained transport is not independent proof.
- Timesheets 3-6-resume-checkpoint.md and linked current NEXT-PREREQUISITE.md:
  preserve historical prose and prepend a dated result after review.

## Tasks & Acceptance

**Execution:**
- [x] Platform eng/recovery-preparation/native_measurement_adapter.py and
  test_native_measurement_adapter.py: implement a fixed request-plan and typed
  sanitizer with bounded single-attempt transport contract, no generic command
  dispatch, no credential fallback, no live CLI activation. Freeze exact selectors
  from v4; any intentionally unsupported selector remains explicitly blocked.
  Prefer direct HTTPS with fixed IP/port, strict TLS, no proxy, redirect, retry,
  discovery or pagination; do not execute network code in qualification.
- [x] New private sibling supplemental directory under existing recovery custody:
  create-only 0700 directories/0600 files. Bind source/tests, exact interpreter,
  stdlib/extensions/dynamic dependencies and any tool bytes without running tools.
  Bind immutable v4 and receipt inputs, fixed transport/config/context/endpoint,
  exact selectors and bounded output slots, authority-check plan, deadlines,
  byte/item/page limits and stop conditions. Inventory and verify preservation
  before/after; no writes within the v4 RID or historical directories.
- [x] Platform new evidence directory: retain initial failures and review
  snapshots, exact synthetic commands/results, source/dependency inventory and
  blockers. Independent reviewers must examine actual bytes and tests; address
  findings and rerun affected checks before final sealing.
- [x] Timesheets checkpoint, current handoff/test-summary/story and new evidence:
  record qualification, unchanged ceiling, exact supplemental digest, blockers
  and next prerequisite without changing protected statuses.

**Acceptance Criteria:**
- Given the exact v4 digest and receipt, when prepared offline, then originals
  and history remain unchanged and no operational request or secret read occurs.
- Given qualified synthetic transport, when each supported selector is processed,
  then only fixed paths/methods and typed nonsecret output are possible; bounds,
  tampering, timeout, secret suppression and first-failure behavior are tested.
- Given null identity/CA/client credential/revocation/CNPG bindings, when an
  operational proposal is assessed, then each missing binding is a blocker,
  never historical evidence promoted to current authority. Present executable
  approval only when all necessary bindings and qualification pass; otherwise
  document the concrete bounded scope and why approval is not ready.

## Implementation Notes

No intent gaps or irreversible actions exist within authorized offline scope.
Unresolved operational facts are blockers, not questions that permit assumptions.
The parent will independently inspect changes and tests without staging.

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| P1 | medium | Empty/partial runtime and source maps passed; require exact mandatory members. Route: patch. |
| P2 | medium | External inputs were not rechecked before success; add final full byte/layout checks. Route: patch. |
| P3 | medium | Count-only preservation admitted aliases/omitted status files; derive exact canonical member set from v4 plus fixed receipt/status paths. Route: patch. |
| P4 | low | Malformed dependency map could escape structured refusal; enforce dictionary shape. Route: patch. |
| P5 | medium | Parent replacement after open could retain old FD success; compare path/ancestor identity again after reading. Route: patch. |
| P6 | medium | Candidate before-map could refresh original bytes; independently check original v4/history records and pin before-map digest. Route: patch. |
| P7 | medium | Ancestor symlink swap could read excluded bytes before refusal; traverse retained no-follow directory descriptors. Route: patch. |
| B1 | medium | Sibling SourceFileLoader could execute unbound pyc; compile bounded fixed source bytes. Route: patch. |
| B2 | medium | Source binding read before size/custody checks; use bounded descriptor reads. Route: patch. |
| B3 | medium | Sanitizer could exceed request deadline; check request deadline after sanitization. Route: patch. |
| B4 | medium | Final digest fields could exceed output cap; check complete final receipt. Route: patch. |
| B5 | medium | Old workload generation could masquerade as current readiness; match generation/observedGeneration. Route: patch. |
| B6 | medium | Service routing fields were discarded despite selected ports; preserve safe typed routing bindings. Route: patch. |
| B7 | low | Header strip could erase invalid control characters; validate raw values and trim HTTP whitespace only. Route: patch. |
| B8 | medium | Continuation Link accepted despite pagination stop; explicitly refuse it and test it. Route: patch. |
| B9 | medium | Unexpected empty directories escaped layout checks; compare complete directory sets. Route: patch. |
| B10 | low | Exponent overflow passed strict JSON; reject floating metadata values. Route: patch. |
| B11 | medium | Expiry was sampled before final checks; sample immediately before result. Route: patch. |
| E1 | medium | Source symlink/FIFO/oversize read repeats B2; same bounded source patch, retained as independent finding. Route: patch. |
| E2 | low | Cleanup exception could escape fixed TLS refusal; suppress cleanup diagnostics. Route: patch. |
| V1 | medium | Dropping openbao selector left all20 tests green; independently specify exact ordered23-selector plan. Route: patch. |
| V2 | medium | Wrong PostgreSQL output values left all20 tests green; assert exact emitted typed values. Route: patch. |
| V3 | medium | Direct helper tests missed verify caller boundary; add complete synthetic success and late external mutation tests. Route: patch. |
| B1b | medium | Follow-up found the verifier test loader could still consume unbound pyc; replaced it with bounded source compilation and eligible-cache regression. Independent follow-up closed it. Route: patch. |

## Verification

Run the new adapter tests with /usr/bin/python3.14 -I -S -B and synthetic data
only. Verify supplemental manifests and immutable input inventories offline;
recheck actual UTC. Existing packet/preflight sources stay unchanged. Run the
existing built Timesheets LaunchReadiness test executable if available; no
restore or live suite. Exact commands and results go in new evidence.

## Final Result

Offline preparation complete:70 synthetic tests,13 LaunchReadiness and independent clean follow-up;24 findings addressed. Supplemental manifest a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631. Operational authority/enrollment/runtime bindings remain blockers; no executable approval is ready. Protected parent states are unchanged. All earlier failures/revisions and495 preservation bindings remain unchanged. No staging, commit or push.
