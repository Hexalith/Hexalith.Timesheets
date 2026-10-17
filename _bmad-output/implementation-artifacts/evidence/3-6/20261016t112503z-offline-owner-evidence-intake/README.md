# Story 3.6 offline owner-evidence intake — 2026-10-16

**No operational approval is ready.** Offline intake and two independent reviews
found no supplied current identity/authority/qualification evidence that closes
the [sealed prerequisite](../20261016t103632z-native-enrollment-execution-bindings/NEXT-PREREQUISITE.md).
The exact verifier now refuses a concurrent Platform sprint-status change.
This directory records acceptance evidence only; it creates no recovery,
activation revision, enrollment, operational receipt or replacement timestamp.

## Exact input and current result

The supplied manifest is
`/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision-sealed/packet-v1.json`,
SHA-256 `c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d`.
It still selects `enrollment-execution-binding-v4.json`, SHA-256
`c42bfe8cd0534cafd652872a2abbc7f2171e9bb5ac6083ad7ac4e7755d453622`.
All ten enrollment witnesses remain null; authority/live flags remain false,
the gate closed and the real `measurement-output` directory absent.

Actual UTC was **2026-10-16T11:25:36Z** for the exact verifier and
**2026-10-16T11:26:41Z** for the local hash diagnostic, before the unchanged
**2026-10-16T16:27:56Z** ceiling. Preparation remains **2026-10-15T16:27:56Z**.
These are observation times only. No policy/capture time was refreshed and no
replacement recovery was allocated. Expiry stops dependent work on this candidate.

```bash
/usr/bin/python3.14 -I -S -B /home/administrator/projects/hexalith/platform/eng/recovery-preparation/verify_enrollment_revision.py --sha256 c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d
```

This invocation ran once and exited **1** with
`offline-revision-integrity-or-custody-refusal` and closed gate.
[The exact result](exact-verifier-result.json) retains command, stdout, stderr
and exit code. Static review first established that the exact source and pinned
sibling perform only offline nonsecret reads. No adapter, preflight or live
entry point was executed.

[Separate local hash diagnostics](integrity-diagnostic.json) establish only:

| Input | Current byte/custody observation |
| --- | --- |
| Final sealed revision | 88 payloads / 89 files match; exact file layout, uid 1000, directories 0700/files 0600 |
| Original unsealed preparation | All 75 named files and original modes match; sealed copies retain their bytes; 67 files 0600 and eight logs 0644 |
| Original supplement | All 55 payloads and manifest match; anchor remains `a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631` |
| Initial 557-file preservation inventory | 556 match; Platform sprint-status differs, with additional drift beyond its sealed exception |
| Sources | Four revision and six original source files match |
| Runtime inventory | 1,337 file hashes and nine library records match; actual loaded-provider/TLS/HTTP behavior remains unqualified |

This diagnostic does not replace a passing exact verifier or establish authority.
The immutable inventories retain the 140 v4 files and 348 historical files,
including expired recovery, signed originals and failures. The original whole
verifier remains refused; no original manifest or exception has been rebased.

## Newly observed preservation blocker

Platform remains at `9256c675186b020929d67230a22892bb13488bc2`;
Timesheets remains at `ee95f90f529812c744761090691a23196c971c78`.
The sealed exception pins Platform sprint-status to
`f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43`.
Current working-tree bytes hash to
`c94f8fedcc4d9e362985c3056c2a2b3e6628ef74b525ed377b92f7cf920a12c5`:
an existing uncommitted Story 1.8 change from `backlog` to `in-progress`.
Independent comparison against HEAD confirms that difference. This is distinct
from the previously recorded Story 1.7 change. It existed before this intake and
was not initiated, reverted, accepted or incorporated into a new seal here.

The preservation owner must explicitly disposition both the prior drift and this
additional change, retaining the original inventories, exact refusal and history.
An explained change cannot by itself make the sealed verifier pass. Any later
proposal must resolve preservation through a separately reviewed exact revision;
this intake authorizes no such activation or rebase.

## Evidence the owners must supply

The following are **requested artifact names**, not files claimed to exist or
accepted values. Owners must identify the accountable people and their authorized
signing roles. Supply already-produced nonsecret originals from independently
authorized qualification; this list does not authorize acquisition or execution.

Every attestation must bind the exact original payload bytes/digest, target and
resource identity, signer identity and authority, observation/validity interval,
revocation cut and applicable predecessor/head. Supply its detached signature
and public verification material. A key supplied beside its own assertion cannot
establish trust. Public root authenticity, permitted signing purpose and current
revocation status need independent establishment outside that evidence chain.
Matching hashes, historical metadata, copied receipts and caller assertions close
none of these gaps. No private certificate key, token or kubeconfig may be supplied.

| Owner role | Exact requested nonsecret artifacts | Required independent attestation |
| --- | --- | --- |
| Organization security/trust authority, independently authenticated by the reviewer | `public-trust-roots/` containing the applicable public signer keys/certificates; `trust-root-establishment.json` and its detached signature | Establish each root fingerprint, authorized signer/role/purpose, provenance channel, validity and revocation status independently. Separate API CA trust from grant, custody and qualification signer authority. No applicable independently established root is supplied. |
| Administrator and API server PKI owner | `server-ca.pem`, `server-intermediates.pem`, `server-peer.pem`, `server-trust-attestation.json`, `server-peer-attestation.json`, and each attestation's detached signature | Authenticate the server trust source for `https://192.168.1.30:6443`; bind exact public CA DER/SPKI fingerprints and permitted chain, current leaf DER/SPKI pin, issuer/serial/validity, IP SAN `192.168.1.30`, server usage and current chain/revocation validation. Explicitly declare a direct-root chain if no intermediate exists. Kubeconfig/KubeSphere endpoint metadata cannot bootstrap this trust. |
| Administrator, credential custodian and independent enrollment reviewer | `client-public.pem`, `client-identity-attestation.json`, `protected-enrollment-delivery-attestation.json`, with detached signatures from the responsible custodian and reviewer | Bind exactly one chosen current certificate identity, DER/SPKI fingerprints, subject, issuer, authenticated groups, client usage and validity to context `jpiquot@local`, endpoint and config SHA-256 `f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c`. Attest protected exclusive delivery, identified custodian/recipient/channel, custody, single consumption, prior pair verification for that public identity and no fallback. Record only the verification result and public binding; private material remains outside intake. |
| Independent native authorization/RBAC owner and grant signer | `native-grant-current.json` and detached signature; referenced exact native role/binding/public subject records | Authenticate the current principal/groups, resource/cluster identity, native binding UID/version/roleRef and complete applicable role rules, independent of KubeSphere. Bind this grant to the one client identity and applicable signer trust. Recovered native use or a broad role name proves no current grant. |
| Native grant/revocation authority and independent offsite custodian | `native-grants-and-revocations.jsonl`, original record signatures, `native-current-head-attestation.json`, `offsite-custody-attestation.json`, `post-cut-revocations.jsonl`, and detached attestation signatures | Supply the complete authenticated sequence from its trusted origin, every predecessor and grant/revocation, signer transitions, independently authenticated latest sequence/hash/head and freshness, offsite retention/readback, and every post-cut revocation. An empty post-cut set needs an authenticated completeness/current-head statement. A hash chain alone cannot establish completeness or prevent an older head being presented as current. |
| Independent native authorization owner/reviewer | `selector-rights-current.json` and detached signature | Cover every exact ordered `(ordinal,id,GET,path)` in [ordered-get-selectors.json](ordered-get-selectors.json), including `/version`, `/readyz?verbose`, the native binding and four named absence checks. Bind principal/certificate, resolved grants/roles, target, observation/validity and current revocation head/cut. Explicitly establish rights for all 23; historical successful GETs or caller-supplied lists are insufficient. Future execution must revalidate current rights/revocations at each step. |
| Platform runtime/security maintainer and independent qualification reviewer | `runtime-execution-inventory.json`, `openssl-provider-configuration-attestation.json`, `tls-peer-client-qualification.json`, `http-response-compatibility.json`, and detached signatures | Bind exact source/interpreter/stdlib/native-library/provider/configuration/environment bytes and actual loaded provider behavior. Supply previously separately authorized qualification proving strict pinned CA/peer/IP-SAN and chosen client behavior under the bound TLS profile, with prohibited ambient/default configuration disabled. Supply actual per-selector status, exact Content-Length, header/body sizes, media type and sanitized schema compatibility; explicit absence semantics and no transfer/compression/pagination/redirect/fallback. Include qualification authorization reference, current observation/validity and exact runtime/identity bindings. Mocks, version strings and file hashes alone cannot qualify behavior. No live handshake/request is permitted by this intake. |
| Platform preservation owner and independent preservation reviewer | `preservation-drift-disposition.json` and detached signature | Name the original inventories and all old/new sprint-status hashes, full commit provenance, the prior Story 1.7 drift and current Story 1.8 change, preservation impact and explicit disposition. Retain both preparation directories, signed originals, expired recovery, failures and exact nonzero verification. Do not rewrite sealed exceptions or accept an unexplained current baseline. |
| Conditional future stage only: Administrator and independent activation reviewer | After every preceding prerequisite passes: `activation-revision-manifest.json`, `activation-independent-review.json`, and `bounded-operational-approval-record.json`, with authenticated review/approval signatures | These artifacts are currently unavailable and are not requested for execution now. The separately reviewed revision and distinct approval must bind final exact source/runtime/manifest bytes, the one fixed identity, all 23 selectors, protected custody/output slots, limits, unchanged ceiling and stop conditions. This row specifies a future evidence requirement; it is not an approval prompt, activation implementation or execution permission. |

All twelve IDs in the original [gap ledger](../20261016t103632z-native-enrollment-execution-bindings/evidence-gaps-v1.json)
remain unresolved. After the substantive evidence and preservation prerequisites
are satisfied, an exact activation revision still needs separate independent
review. Only then can a distinct bounded operational approval prompt be produced.
Live activation is currently unimplemented/disabled. No executable approval prompt
is emitted by this intake.

## Independent review and historical limits

`independent_evidence_intake` examined the explicit nonsecret owner evidence under
Platform 4-0/4-26/4-27 (109/1,140/85 filenames), and the current preparation,
supplement, unsealed and sealed directories (140/56/75/89 files). It found no
new signed payload, applicable independently established authority root or public
CA/peer/client certificate. The current public candidate files
`20261015t162756z/trust/allowed_signers.txt` and `trust/recipient.pub` do not
establish those authorities: `bindings-v4.json` explicitly records
`publicTrust.authenticationPerformed=false` and `operationalResults.signatures=null`.

The historical sanitized owner observations explicitly record missing fingerprint,
`managementGrantLineageAuthenticated=false`, `signed=false`, or
`actualSignedRepositoryOrOffsiteMirrorVerified=false`. Their exact paths and hashes
remain in the original gap ledger. The old 4.27 audit's eight signatures concern
RID `20261014t112353z`, expired **2026-10-15T11:40:42Z**; that audit receipt is
unsigned. None proves current server/client/rights authority. No signature
verification was attempted here without an independently established applicable
public root and new signed payload.

`sealed_integrity_review` independently checked all 88 sealed members, four new
and six original sources, the untouched 75 preparation files/modes, closed/null
bindings and the additional sprint-status drift. Both reviewers performed no
tests, verifier execution, credential reads, network or file/Git mutations.
Their conclusions are independent review observations, not owner attestations.

Final artifact review at **11:29:09Z** found one completeness correction: name
the owner and exact required bindings for the conditional future approval record.
The table now includes that requirement without emitting an approval prompt.
[Independent review observations](independent-reviews.json) retain the correction
and its disposition; they authenticate no external owner or authority.

[The final preservation comparison](preservation-after.json) at **11:29:23Z**
matches all **725 session input files**, including both preparation directories
and original source/history inventories. Both repository heads, indexes, staged
diffs and gitlinks are unchanged; the four-file scoped Timesheets
`git diff --check` exits 0. Protected status checks all pass. This session-relative
preservation result leaves the sealed verifier's concurrent-drift refusal intact.

## Fixed scope and status

The 23 ordered GETs, endpoint/context/config binding, 31 v4 prerequisites and
unsupported selector exclusions remain unchanged. Limits remain one attempt per
selector, 15s/request, 300s/lane, 8KiB headers, 256KiB/response, 4MiB/lane and
64KiB final receipt, with exact Content-Length. No retry, alternate identity,
proxy, redirect, DNS, pagination, arbitrary command or expanded scope is permitted.
Detailed node, pod/init-image, PVC/PV and current CNPG-controller selector families
remain unsupported. First failure/unknown, tampering, rights loss, expiry, clock
regression, drift, timeout, overflow or collision stops dependent work.

No implementation, build or test was repeated. Prior 135 synthetic and 13 built
LaunchReadiness results remain dated historical results. No actual kubeconfig or
private credential read, authentication, network/API/provider/IAM request,
preflight replay, signing, publication, infrastructure change, operational
capture/restore/retirement/upgrade/deployment, domain write, staging, commit, push,
dependency or gitlink update occurred. Adapter sources remain in owning Platform;
this new acceptance evidence and small resume links remain in Timesheets.

Spec-23 remains **draft**; Story **3.6/Epic 3/4.27 in progress**; **4.1's operational
gate closed**; release **FAIL**. Platform's 4.1 sprint item remains in-progress;
the closed operational gate is not a claim of story completion.
