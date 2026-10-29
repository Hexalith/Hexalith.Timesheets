# Story 3.6 current prerequisite intake — 2026-10-29

The continuation remains **in progress**. Existing native access works, but no
executable deployment/recovery/authority approval bundle is ready. This packet
adds current observations while preserving the earlier evidence and all gates.

| Repository | Observed HEAD |
| --- | --- |
| Timesheets | `bc97acea37435fea73cc2fa818d27a1d053933fa` |
| Platform | `8de90958bc41b7fd01b7ed22b8ab364447a800e4` |
| EventStore | `41456b6ec8b8baf8e0ac4d0ff2f5e86f7a10be16` |

These identifiers were read directly from Git. All thirteen dependency checkout
heads and statuses are recorded in [the preservation baseline](preservation-before.json).

## Current native observation

From the owning Platform repository:

```bash
python3 -B eng/timesheets-deployment/observe_native.py \
  --output /home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/3-6/20261029t092023z-continuation-intake/native-observation.json
```

The collector exited **0** in **4.345 seconds**, from
`2026-10-29T09:21:18.691375+00:00` through
`2026-10-29T09:21:23.036254+00:00`. [Its command receipt](native-command.json)
and [sanitized result](native-observation.json) retain the invocation, each native
query's exact arguments, observation cut and exit code.

- Target: context `jpiquot@local`, API `https://192.168.1.30:6443` and
  `kube-system` UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, unchanged across
  the first and final requests.
- TLS: CA and IP SAN verification enabled; TLS 1.3. Peer certificate DER SHA-256
  `c4d7ae040d6084bd8c16de24c44b6534432913de66a0a99734535ca29d037e4e`.
  The separate TLS peer probe presents no client certificate; the native identity
  query authenticates `jpiquot` with credential ID
  `X509SHA256=fe03137074838e0331196425e1f009838c147ebfb3abaf30c0f594d2269e6e00`,
  matching the selected public client certificate.
- Native rules include wildcard resource and nonresource permissions at this cut.
  No historical revocation or Timesheets application/migration authority is inferred.
- Observed totals: 24 namespaces, 126 deployments/statefulsets/daemonsets/Services,
  119 pods, six ingresses, 14 Dapr components, zero subscriptions and ten Dapr
  configurations. Neither staging namespace nor an identifiable Timesheets
  object appears among these queried kinds.
- Gateway and HTTPRoute queries each exit **1**, classified by the collector as
  `resource-type-absent`. Other recorded native queries exit **0**.

The existing inline client configuration is frozen in sealed anonymous memory
for fixed child clients. No private credential, environment value, Secret object
or raw native object is retained. Query scope is bounded: renamed installations,
complete legacy source coverage, pending deliveries and deployed end-state are
unestablished. The current client is `kubectl v1.36.1`; its resolved executable
and hash are recorded, without inferring upgrade/recovery tool qualification.

## Operational blockers

[The local prerequisite inspection](prerequisite-inspection.json) records:

- The Platform handoff is `BLOCKED_NOT_FOR_EXECUTION`, with
  `approvalGranted=false` and `executableDispatcherExists=false`.
- Both approved staging values files and
  `/tmp/timesheets-hosting-helm-4.2.0/helm` are absent. The direct
  `ls -l /tmp/timesheets-hosting-helm-4.2.0/helm` command exits **2**, reporting
  `No such file or directory`. No substitute binary is selected.
- Both retained application/environment chart archives match the handoff's
  SHA-256 values. Local byte matches establish no released or deployed artifact.
- The latest allocation found in the explicitly inspected local
  `/home/administrator/hexalith-recovery-evidence/4-0` directory remains RID
  `20261017t143202z`, with exclusive ceiling `2026-10-18T14:32:02Z`. Its five
  payload lengths/hashes match its manifest; all six operational results remain
  null and twelve prerequisite categories remain unestablished. The expired
  packet, earlier candidate and original verifier refusal receive no writes or
  verifier replay. No replacement is allocated because timestamp refresh alone
  would not advance a concrete executable handoff.
- Qualified current capture/restore/cleanup, immutable complete Release closure,
  actual workload signer/client grants, server-side providers, complete legacy
  inventory/continuity and enforced maintenance remain unestablished. Platform
  staging stories 4.14–4.22 remain backlog; recovery Story 4.27 remains in progress.

The October 22 spec entry's EventStore `authority-overlay.json` path is absent
in this checkout (`cat` exits **1**). The current
[authority documentation](../../../../../references/Hexalith.EventStore/docs/security/workload-binding-authority.md#validation)
expressly withdraws present qualification authority from 187/187 and the two
83-case historical runs. No withdrawn authority qualification is rerun. The
retained October 28 runtime evidence still leaves Aspire 13.6.4 unqualified;
native API access does not replace that evidence.

## Verification and limits

The existing collector safety lane ran from Platform:

```bash
python3 -B -m unittest discover -s eng/timesheets-deployment -p test_inspect.py -v
```

It exited **0**, **16/16 passed**, with the unittest runner reporting 0.021 s.
It covers wrong endpoint, disabled TLS verification, changed/unavailable cluster
identity, immutable context, bounded collection and omission of sensitive values.
[Verification](verification.json) records the result, matrix scope, local checks
and preservation comparison. The production signing/grant matrix row has no
new current qualification in this intake.

No code, dependency, gitlink, deployment, recovery, migration, permission change,
domain write, build, runtime journey, signing, staging, commit or push is performed.
Only the continuation's new additive entry and this evidence packet change.
The live task stays unchecked, Story 3.6 stays `in-progress`, spec-23 stays
`draft`, 4.1 stays closed and launch remains **FAIL**.
