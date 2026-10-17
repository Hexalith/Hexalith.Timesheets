# Story 3.6 owner-evidence delta — 2026-10-16

No newly supplied owner evidence was found in the scoped locations. No
operational approval prompt is ready. This bmad-build continuation retains the
[owner artifact requirements](../20261016t112503z-offline-owner-evidence-intake/README.md)
and all twelve unresolved IDs in the
[original gap ledger](../20261016t103632z-native-enrollment-execution-bindings/evidence-gaps-v1.json).

The user supplied the same final sealed manifest digest,
`c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`,
and no new owner artifact path or payload. That digest identifies the intake;
it establishes no authority. Completed implementation, tests, packet/source/
runtime hash sweeps and the exact verifier were not repeated.

## New observation and its limits

At **2026-10-16T11:35:57.735105Z**, a filename/mtime/ctime-only search of the
four current recovery preparation/supplement directories, Platform's explicit
4-0/4-26/4-27 evidence roots and Timesheets' Story 3.6 evidence root found no
requested owner artifact names. No owner file had metadata newer than the prior
intake observation, **2026-10-16T11:29:23.791504Z**. Two Timesheets intake
documents had 11:30:00Z metadata; their already-read contents describe the prior
final documentation correction, not newly supplied owner evidence.

Independent investigator `offline_evidence_delta` examined 1,821 filenames in
the explicit Platform owner roots and six recovery roots, compared recovery
filenames with the prior inventory, and checked the recovery root's direct
children. It found no requested owner filename, `public-trust-roots/`, new
recovery filename or newer owner-file metadata. The recovery root contains
eight existing directories; two older directories were not traversed.
[The delta receipt](delta-observation.json) records exact scopes and exclusions.

[Independent acceptance-document review](independent-review.json) found no
material inaccuracies or prerequisite omissions. It authenticates no external
owner and is not a review of an exact activation revision. New JSON syntax,
local document links and whitespace checks passed; the scoped checkpoint
`git diff --check` exited 0. The append retains all 59,059 prior checkpoint bytes
as an exact prefix and adds 1,477 bytes. No existing implementation/test or
unchanged validation was rerun.

These observations establish neither byte integrity nor authenticated external
owner authority. They are not a new passing preservation or qualification run.
Unknown locations, externally held artifacts and renamed undisclosed payloads
are outside the search conclusion. No unknown candidate payload was opened.
No applicable public trust-root establishment or new signed owner payload was
supplied, so no local signature verification was attempted.

UTC checks at **11:36:21Z** (independent investigator) and **11:36:48Z** (primary
agent) precede the unchanged **2026-10-16T16:27:56Z** ceiling. These are observation
times only. Preparation remains **2026-10-15T16:27:56Z**. Expiry stops dependent
work; no policy/capture timestamp refresh or replacement recovery is authorized.

## Exact missing owner artifacts and attestations

Every row remains unmet. Supply already-produced nonsecret originals from
independently authorized work, each with its original detached signature and
public verification material. Independently establish the applicable signer
root, authorized role/purpose, provenance, validity and current revocation
status outside the assertion's own chain. A key accompanying its own assertion,
historical metadata, hashes and caller statements cannot establish authority.
Each attestation must bind original bytes/digest, target/resource, signer and
authority, observation/validity, revocation cut and applicable predecessor/head.

| Accountable owner and independent attester | Missing artifacts and required binding |
| --- | --- |
| Organization security/trust authority, independently authenticated by the reviewer | `public-trust-roots/`, `trust-root-establishment.json` and detached signature: authenticated root fingerprints and authorized signing purposes; distinguish API CA trust from grant, custody and qualification authority. |
| Administrator and API server PKI owner | `server-ca.pem`, `server-intermediates.pem`, `server-peer.pem`, `server-trust-attestation.json`, `server-peer-attestation.json` and detached signatures: authenticated trust source, exact CA/peer DER and SPKI, chain, issuer/serial, validity/revocation, server usage and IP SAN `192.168.1.30` for `https://192.168.1.30:6443`. Explicitly attest a direct-root chain if no intermediate exists. |
| Administrator, credential custodian and independent enrollment reviewer | `client-public.pem`, `client-identity-attestation.json`, `protected-enrollment-delivery-attestation.json` and custodian/reviewer signatures: exactly one current public certificate, authenticated groups, usage/validity and fixed context/config binding; independently attest exclusive protected delivery, named channel/custodian/recipient, custody, single consumption, prior public/private pair-verification result and no fallback. No private material may enter intake. |
| Independent native authorization/RBAC owner and grant signer | `native-grant-current.json`, referenced native role/binding/public subject records and detached signature: current principal/groups, cluster/resource, binding UID/version/roleRef and complete applicable role rules, independent of KubeSphere and bound to the one client identity. |
| Native grant/revocation authority and independent offsite custodian | `native-grants-and-revocations.jsonl` with original record signatures, `native-current-head-attestation.json`, `offsite-custody-attestation.json`, `post-cut-revocations.jsonl` and detached attestation signatures: complete authenticated origin/predecessor/grant/revocation/signer-transition lineage, independently authenticated latest sequence/hash/head and freshness, offsite retention/readback and all post-cut revocations. An empty post-cut set needs authenticated completeness. |
| Independent native authorization owner/reviewer | `selector-rights-current.json` and detached signature: current rights for every exact ordered `(ordinal,id,GET,path)` in the [23-selector reference](../20261016t112503z-offline-owner-evidence-intake/ordered-get-selectors.json), bound to the client, target, resolved grants, observation/validity and current revocation head/cut. Future execution must revalidate rights/revocations at each step. |
| Platform runtime/security maintainer and independent qualification reviewer | `runtime-execution-inventory.json`, `openssl-provider-configuration-attestation.json`, `tls-peer-client-qualification.json`, `http-response-compatibility.json` and detached signatures: exact source/interpreter/stdlib/native-library/provider/configuration/environment bytes and actual loaded-provider behavior; separately authorized actual pinned CA/peer/IP-SAN/client qualification; actual per-selector status, exact Content-Length, header/body sizes, media type, sanitized schema and explicit absence semantics, without transfer/compression/pagination/redirect/fallback. Bind qualification authorization, current observation/validity and runtime/identities. Hashes/mocks/version strings are insufficient. |
| Platform preservation owner and independent preservation reviewer | `preservation-drift-disposition.json` and detached signature: original inventories, all old/new hashes, full commit provenance, prior Story 1.7 preservation drift and additional Story 1.8 sprint-status change, preservation impact and explicit disposition. Preserve originals and the exact refusal; disposition alone does not make the sealed verifier pass. |

The client binding remains context `jpiquot@local`, endpoint
`https://192.168.1.30:6443`, config SHA-256
`f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c`.
No actual configuration or private credential was read.

After all preceding substantive and preservation prerequisites pass, the
Administrator and independent activation reviewer must supply an exact
`activation-revision-manifest.json` and authenticated
`activation-independent-review.json`. They must bind final exact source/runtime/
manifest bytes, one identity, all 23 selectors, protected custody/output slots,
unchanged limits/ceiling and stop conditions. Only then may an exact bounded
operational approval prompt be produced; its separately authenticated
`bounded-operational-approval-record.json` is a later requirement. None is
available or authorized for execution by this intake.

## Preserved refusal, scope and status

The [existing exact verifier result](../20261016t112503z-offline-owner-evidence-intake/exact-verifier-result.json)
remains **exit 1** at **2026-10-16T11:25:36.164697Z**, reason
`offline-revision-integrity-or-custody-refusal`. It was not rerun. Its historical
diagnostic identifies additional Story 1.8 drift from sealed sprint-status hash
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`
to observed hash
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`.
Those are carried-forward observations, not freshly revalidated hashes. No
sealed exception, original packet or preservation baseline was rewritten.

Writes in this continuation are limited to this Timesheets acceptance directory
and an appended resume-checkpoint entry. Both preparation directories, immutable
packets, signed originals, expired recovery, failures, concurrent changes,
Platform adapters, indexes and gitlinks received no writes from this task.
The prior whole verifier remains refused. No fresh full-byte preservation claim
is made.

All 23 selectors, 31 v4 prerequisites, unsupported exclusions and first-failure/
unknown stop conditions remain unchanged. Limits remain one attempt/selector,
15s/request, 300s/lane, 8KiB headers, 256KiB/response, 4MiB/lane and 64KiB final
receipt, with exact Content-Length. No credential access, authentication,
network/API/provider/IAM request, preflight replay, signing, publication,
infrastructure change, capture, restore, retirement, upgrade, deployment, domain
write, staging, commit, push or dependency update occurred.

Intent gaps: none. External evidence prerequisites prevent the Ready for
Development sufficiency standard from passing. No activation implementation or
approval checkpoint is reached. Spec-23 remains **draft**; Story **3.6/Epic
3/4.27 in progress**; **4.1 operational gate closed**; release **FAIL**.
