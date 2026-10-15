# Story 3.6 Platform candidate recheck — 2026-10-14

## Signed cleanup and concurrent records reconciled — 2026-10-15

This resume starts at Timesheets `c9f71a9fc8db778ab364a955d0b30afde3104204`
and the separate owning Platform checkout
`55bee680e6901a45946dd8bf1e4a4060952bd7ea`. Platform's working spec and
new evidence are local changes; the Timesheets `references/Hexalith.Platform`
checkout was not advanced. Original recovery scope, accepted OIDC incident
correction, October 7–31 window, conversation incident channel and Administrator
roles remain in force within their existing limits.

**Cleanup signing is complete.** The original policy and exact cleanup record
both authenticate as `jpiquot@itaneo.com` under `hexalith-recovery`, pinned
ED25519 fingerprint `SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`.
Cleanup SHA-256 remains
`3ae690daad23e6a0f028ab28ca023ba9a04d4c72f810d9129b117125d08d5b78`;
its actual signature SHA-256 is
`c346a6a9692227302e4c804012bb1df2a0b1b3121f1c76a65a77c794bd6d280c`.
The prior unsigned-only signing action was not replayed.

At **2026-10-15T08:49:13.225649Z**, independent read-only reconciliation
passed. Fresh validator HEAD/GET/retention reads matched these exact immutable
versions, byte lengths and local SHA-256 values:

| Record beneath `evidence/4-0/20261014t112353z/` | Provider version | Bytes |
| --- | --- | --- |
| `policy/recovery-policy.json` | `1791981906616560` | 17,780 |
| `policy/recovery-policy.json.sig` | `1791981907303892` | 314 |
| `cleanup/restore-target-cleanup.json` | `1792053509510188` | 9,535 |
| `cleanup/restore-target-cleanup.json.sig` | `1792053509947219` | 314 |

All four use AES256 and COMPLIANCE lock, with matching retention API dates in
the future (November 13 for policy/signature and November 14 for cleanup/signature).
Policy age was **76,105.019278 seconds**, below 86,400. The policy age limit ends
**2026-10-15T11:40:42Z (13:40:42 Europe/Paris)**; the maintenance window does not
extend it. Four protected namespace UIDs, all 15 source PVC/PV bindings and the
CNPG controller UID/watch scope were preserved. Recorded isolated target
namespaces/storage claims and temporary copies were absent.

A separate executor began at `20261015t084001z` before this resume's private
`20261015t084153z-s427-signed-cleanup-resume` custody. During inspection it
completed the retained records prerequisite, original manifest and approved
private-OIDC proof consumer. These are concurrent executor outcomes. Its
manifest reports **4,714 immutable versions read back**; this resume separately
matched **73 local evidence files**, the manifest/proof digests, pass results,
RPO values (Keycloak 29s, OpenBao 127s, Memories 86s), isolation and exact
accepted incident provenance. The 4,714-version readback was not replayed.

| Existing unsigned record | SHA-256 |
| --- | --- |
| `manifest/evidence-manifest.json` | `9d16ecb2c1074fa72e131bd448b1230bee2581d4877c83576c78d8b14365f9d2` |
| `keycloak/keycloak-restore-proof.json` | `f6570b31b4f138a2ffe2e44bfbb5c464d0462898ab7f496f795fa52a4c168734` |
| `openbao/openbao-restore-proof.json` | `fc61e1a0ef9f0a29c47f9c003bef73e7467ce08c35fcbef181d8a14acd481545` |
| `memories/memories-restore-proof.json` | `bc9725e06ab30fe02959b2db6370cfc58750e6420d05d9fa2b19f691326c1ee1` |

**Next unresolved prerequisite:** Administrator protected-terminal signing and
publication of these four existing records using the
[exact guarded action](/home/administrator/hexalith-retirement-investigation-evidence/20261015t084001z-s427-signed-cleanup-continuation/NEXT-PROTECTED-SIGNING-ACTION.md),
SHA-256 `a4cb7d9ce0ff2ccc51332e93bb4ab5525fc42a0417b2a20de0fd3b39d7873bfe`.
The guard SHA-256 is
`630f42e092adb467b10fedfce05ec3aa91b6a6f575d55c70c85db378cf119b06`;
its local mode passed with zero remote requests and no signing. The full guard
rechecks current freshness, exact inputs, protected storage, target absence
and resumed intake before the Administrator signs. No repeated scope or
incident approval is requested. Afterwards, authentic original signatures and
fresh exact-version readbacks must precede the backup gate, its signature,
non-dry final validation and honest actual M1/M2 final attestation/signature.
At reconciliation all four signatures, the gate and final validation were absent;
accepted fresh recovery remains **false**.

This run also preserves its own bounded prerequisite wrapper's exit **1**
(0.136s, no stdout), encrypted diagnostics and zero later dependent dispatches.
The exact encrypted error is not claimed. A separate offline diagnosis with
provider access disabled refused overwrite of an already existing proof;
that later diagnosis does not prove the earlier error. Independent read-only
reconciliation then established the current state without replaying records,
capture, restore or cleanup. The failed canonical OIDC record remains unchanged
at SHA-256 `014463ef76fd99431f6c268edbc41274b5031157f683a1630011956cb5738578`.

The private reconciliation receipt
[`readonly-reconciliation.json`](/home/administrator/hexalith-retirement-investigation-evidence/20261015t084153z-s427-signed-cleanup-resume/readonly-reconciliation.json)
has SHA-256 `1a30ac7073f35747fdf09c617fcfff1addffcb021c4894df9a376d80b6be8731`.
The separate [unsigned Platform verification receipt](/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261015t084153z-s427-signed-cleanup-resume/verification.json)
has SHA-256 `756984df297d338707a3d1d1c004378047bd2e3be7ed309da733551712fab033`;
its 29 private checksums and 13 archived source members verified. Concurrent
Platform handoff additions retain their own capture times and source identities.
Raw diagnostics remain encrypted; no recipient-decryption readback is claimed.
This resume performed zero remote writes or stage replays. Historical evidence,
frozen intent and pinned report/plan/declaration bytes remain preserved.
Retirement, 4.1 node/etcd mutation, upgrades and Timesheets deployment/activation
retain their separate exact approvals. Recovery progress supplies no hosted
release, production authority, certified source/continuity or installed
subscriber/shared-rebuild acceptance. Spec-23 stays draft, Story 3.6/Epic 3
stay in progress, Platform 4.27 stays in progress, 4.1's operational gate stays
closed and Timesheets release stays **FAIL**. Verification is in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

## Latest concurrent owner checkpoint

Concurrent synchronization advanced Timesheets to
`e2a9e0898f5e30f2b9f3457ece6ddd55d4c999ba` and the observed Platform
checkout to `55bee680e6901a45946dd8bf1e4a4060952bd7ea` during this run.
The Platform/Works checkout moves and committed in-progress evidence additions
were external activity; this build run issued no Git mutation. Initial and
updated public input snapshots are retained separately. Existing evidence,
frozen intent, the pinned original qualification report, retained plan and
candidate declaration remain preserved.

The updated [staging handoff](../references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md#accepted-oidc-continuation-resumed-memories-and-unsigned-cleanup--2026-10-14)
now records actual approved recovery continuation, Memories resume and isolated
target cleanup. Its [sanitized unsigned receipt](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t144239z-s427-accepted-oidc-continuation/verification.json),
recorded at `2026-10-14T15:40:28.942839+00:00`, has SHA-256
`defb0ec095cfe1ab5deca0f4e23f2c7206f7eeccbc4d3dff9f7f5ad1021919d2`.
It records 32 completed authorized top-level commands, source resume at
15:18:55 UTC, absent isolated target namespaces/PVs, zero cleanup-signing
attempts and zero manifest/proof/gate/non-dry-validation dispatches. These
are inspected owner-reported outcomes, not operations or recovery checks
performed by this Timesheets run. Private recovery/signing originals were
not inspected.

The immediate owner dependency is **Administrator protected-terminal signing
of the exact cleanup record**, followed by authentic signature verification,
immutable readbacks and final non-dry recovery validation. The established
recovery scope, incident decision, window and owner roles need no repeated
selection. The receipt still records `acceptedFreshRecovery=false`,
`Story4_27Accepted=false`, `Story4_1GateOpen=false` and
`Timesheets3_6Accepted=false`. Accepted recovery, exact retirement qualification/
approval/acceptance and the later hosted release/provider/source/deployed
acceptance gates remain required; the continuation establishes no Timesheets
deployment or activation.

New `DaprPlatformHmacBinding` custody adapters require an independently enrolled
`IDaprPlatformHmacAuthority`; no production authority or host invocation was
found. They provide custody key/profile resolution, not Timesheets resource-bound
workload assertions or inventory certification. Timesheets composition, its
retained plan and runtime/test source remain unchanged. The 19:26:22 UTC native
observations below retain their original capture identity. All four owner
requirements remain unmet; spec-23 stays draft, Story 3.6/Epic 3 stay in progress
and release stays **FAIL**.

## Latest authorized working-tree recheck

The user approved resuming over the existing evidence edits and changed root
submodule checkouts. Timesheets HEAD remains
`9fa572c6afb0782bf434e98b6e1b30674eb265d2`; the observed Platform checkout is
`4b9cb5624159de0782dd3329cdcc3b2d66010509`, and EventStore is
`210b90da172cc7ca946fbb06cfd44390bffa707b`. These are checkout identities,
not a clean-tree or released-artifact claim. Existing contributions are preserved.

Platform's new configured JWT reader admits Party `ResolvePartyIdentity`
requests with authenticated service, payload, tenant and actor checks.
`PlatformIdentityOptions.JwtReaders` defaults empty. This adds a Party-read
capability; it supplies no Timesheets resource-binding issuer, registered
access/reference providers or certified inventory/migration authority.
`PlatformTimesheetsBinding.ValidateMode` and `PlatformAppModel.Compose`
still require Development/source operation. Hosted production composition and
retained Release identities remain unavailable. The source investigation found
no independent Timesheets runtime defect that closes the deployed gates.

At **2026-10-14 19:26:22 UTC**, all eight exact commands under
[the staging recheck](#latest-staging-dependency-recheck) were repeated with
the retained client, existing context and 15-second request timeout. The first
six exited **0**: client **v1.34.12**, server **v1.34.9**, cluster UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, **23 namespaces**, **100
workload/service objects** and **111 pods**. The inspected names/namespaces
and pod container/init-container images had no `timesheet`, `eventstore` or
`platform` matches. The staging namespace command exited **1**, with both
`hexalith-staging` and `hexalith-staging-data` NotFound; the Gateway command
exited **1**, because `gateways` is unavailable. Raw responses were reduced
in memory, without printing or retaining credentials, Secrets or domain
histories. This bounded census cannot exclude renamed applications or certify
complete domain history.

The current EventStore issuer still cannot supply production resource-bound
assertions: symmetric resource-binding mode excludes Production, and client
credentials refuse bound requests. Timesheets keeps its denied access/reference
defaults, denied migration authority and unavailable certified inventory.
Its loader, durable processing, mapped projections and resumable v1-to-v7
migration already exist and remain reusable once their owner gates qualify.

The retained Platform plan remains `prepared-blocked`, with deployment and
activation unauthorized, acceptance **FAIL**, no live evidence and null release
digests. The original qualification report, candidate declaration and plan
remain unchanged; captured preparation/handoff snapshots are retained alongside
the concurrent owner update above. No offline regeneration
was attempted; the required Platform-local nested Timesheets host is absent,
and no nested dependency was initialized. Fresh signed recovery/readback and
exact 4.27 qualification/approval/accepted retirement remain the next Platform
operational dependency. All four [owner requirements](#conditions-for-resuming-acceptance)
remain unmet. Spec-23 stays draft, Story 3.6/Epic 3 stay in progress and release
stays **FAIL**. Focused verification is recorded in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

## Latest offline retirement preparation recheck

Clean Timesheets `main` at `9fa572c6afb0782bf434e98b6e1b30674eb265d2`
consumes Platform `2bd78603e164265e3b6c00af9950429cff5510f9`.
Platform's [offline retirement prerequisites](../references/Hexalith.Platform/eng/cluster-management/STAGING-PREREQUISITES.md)
now provide a local packet preparer and conditional first-census, renewed
source-bound namespace/catalog/fresh-node fixture, second-plan and original
signature-verification proposals. They leave private trust, principal,
static native configuration, recipients, signing and freshness inputs unresolved.
The native context does not establish those authorities.

The [sanitized unsigned receipt](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261014t100941z-s427-reviewed-preparation/verification.json)
for `20261014t100941z-s427-reviewed-preparation` records retained preparation
sources and historical integrity checks, **284/284** software tests and
**31** preparation tests within that total. These are Platform's retained
results, not checks rerun by this Timesheets investigation. The receipt has
SHA-256 `acce726247d5b97bb84bfa5175955a55dc210de67aa86425ea4b3d4a8bce1898`.
It explicitly records no production source observation or plan, no mutation
authority, a closed live gate and no accepted retirement. Its historical
signature/readback checks establish neither fresh recovery nor independent
off-node acceptance. Private packet and recovery originals were not inspected.

At **2026-10-14 10:57:18 UTC**, the eight exact commands listed under
[the earlier staging recheck](#latest-staging-dependency-recheck) were repeated
with the same retained client, context and 15-second request timeout. The first
six exited **0**: client **v1.34.12**, server **v1.34.9**, cluster UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, **23 namespaces**, **100
workload/service objects** and **111 pods**. No inspected name/namespace or
pod container/init-container image matched `timesheet`, `eventstore` or
`platform`. The namespace command exited **1**, with both `hexalith-staging`
and `hexalith-staging-data` NotFound. The Gateway command exited **1** because
the server has no `gateways` resource type. Responses were reduced in memory;
no raw JSON, credentials, Secrets or domain histories were printed or retained.
This census cannot exclude renamed applications or certify domain history.

An independent source investigation confirmed unchanged production gaps:
`EventStoreGatewayWorkloadAssertionSource.IssueAsync` requires
`IWorkloadAssertionIssuer.CanBindResources`; production signing remains
unqualified. The server kernel retains denied access/reference providers;
the internal migration registration retains denied authority and unavailable
certified inventory. Mapped materialization and shared-rebuild handlers already
exist, but supply no installed subscriber or continuity evidence. No independent
Timesheets runtime fix that closes these external gates was established.

The next operational dependency remains fresh signed recovery/readback and
an exact source-bound Platform 4.27 plan, prerequisite packet and approval,
followed by accepted retirement/preservation. The new offline packet closes
none of those gates. Later supported-target, Rancher/access, runner, staging,
release/provider/source and deployed-acceptance prerequisites remain in the
Platform handoff. No signing, fixture, retirement or deployment proposal was
executed by this Timesheets run.

The retained Timesheets plan still reports `prepared-blocked`, deployment and
activation unauthorized, acceptance **FAIL**, no live evidence and no release
digests. Its file/canonical hashes and the pinned original qualification report
remain unchanged. Offline enrollment preparation/tests were not repeated:
this checkout still lacks the nested Timesheets host input. The new handoff's
statement that its mapped project is present describes its own checkout;
historical setup errors are not a new result here. No nested dependency was
initialized. Spec-23 remains draft, Story 3.6/Epic 3 remain in progress and
release stays **FAIL** pending the four owner requirements below. Target
discovery is answered; implementation approval cannot supply missing evidence.

## Latest staging dependency recheck

Clean Timesheets `main` at `9ecda28dc7ff540d83fad7ba1e5d585bdc33d0ae`
consumes Platform `92a02924894f6f2f219801a90e5bf25716abd028`.
Platform now retains a [staging dependency handoff](../references/Hexalith.Platform/eng/timesheets-deployment/staging-dependency-handoff.md).
Its successor upgrade-preparation tooling records two separately gated hops;
it supplies neither accepted infrastructure work nor Timesheets enrollment.
The Development/source binding described below remains the available host binding.

The next operational prerequisite in that handoff is fresh signed recovery/
readback and a fresh exact Platform 4.27 retirement plan, prerequisite packet
and approval, followed by accepted retirement/preservation. Post-retirement
census/recovery and an approved successor procedure precede separately qualified
upgrade hops; private Rancher/access, runner relocation and staging/release
qualification remain later dependencies. The historical backup gate expired
on October 2. Preparation receipts, completed tooling and the extended maintenance
window do not establish operational acceptance or authorize execution. This
Timesheets run did not execute any of those procedures or regenerate a plan.

At **2026-10-14 07:32:39 UTC**, these bounded native reads used the retained
client directly, with the existing context and credentials:

```text
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s version --client -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get --raw=/version
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get namespace kube-system -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get namespaces -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get deployments,statefulsets,services -A -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get pods -A -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get namespaces hexalith-staging hexalith-staging-data -o json
/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl --context jpiquot@local --request-timeout=15s get gateways -A -o json
```

The first six exited **0**: client **v1.34.12**, server **v1.34.9**,
cluster UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, **23 namespaces**,
**100 workload/service objects** and **111 pods**. The name/namespace and pod
container/init-container image scan found no `timesheet`, `eventstore` or
`platform` matches. The namespace command exited **1**, with both requested
namespaces NotFound. The Gateway command exited **1** because the server has
no `gateways` resource type. Responses were projected in memory; raw JSON,
credentials, Secrets and domain histories were neither printed nor retained.
This census cannot exclude renamed applications, certify complete domain
history or establish the recovery/acceptance gates above.

Independent source investigation found unchanged default-deny migration
authority and unavailable certified inventory. The assertion source still
requires `IWorkloadAssertionIssuer.CanBindResources`; Platform supplies only
Development signing. Existing mapped projections and shared rebuild handlers
do not supply installed subscriber or owner-continuity evidence. No separate
Timesheets runtime change was established that closes these external gates.

The retained plan remains `prepared-blocked`, with deployment and activation
unauthorized, acceptance **FAIL**, no live evidence and no retained release
digests. The plan, declaration and original qualification report retain the
SHA-256 values recorded below; canonical preparation checksum verification
also passes. No nested input was initialized and no offline regeneration,
runtime startup, domain operation, deployment or Git mutation was performed.
Prior local tests and Platform preparation checks retain their original
source identities; none was rerun as deployed acceptance.

Spec-23 remains draft because its sufficiency gate is unmet. Story 3.6/Epic 3
stay in progress and release stays **FAIL**. Resume deployed acceptance after
the four owner requirements below are established. Target discovery is already
answered; neither another deployment-information question nor implementation
approval can supply those missing prerequisites.

## Latest Development binding recheck

Clean Timesheets `main` at `b9f968221a014c95869f3add8e16eda398b989c8`
consumes Platform `416878a74975b1023b3a00ab5d35aad4ce6365d9`.
Compared with the prior Platform snapshot below, an explicit Development/source
Timesheets host binding now exists. This closes the local composition gap;
hosted staging enrollment and Story 3.6 deployed acceptance remain missing.

`PlatformTimesheetsBinding.ValidateMode` requires source mode, and `SelectRun`
requires the exact hash-bound Timesheets candidate in the selected Builds plan.
`PlatformAppModel.Compose` requires a Development run. The opt-in supplies
separate public/internal listeners, internal Dapr routing, the Timesheets-only
read-model component and gateway registration. It preserves the owner's
default-deny access/reference/migration providers, unavailable certified
inventory and disabled live-fixture grants. See Platform's
[binding qualification](../references/Hexalith.Platform/eng/timesheets-deployment/local-host-binding-qualification.md)
and [binding source](../references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformTimesheetsBinding.cs).
Its recorded build/test passes supply source evidence; no live startup or
deployed acceptance is recorded. Those checks were not rerun by this recheck.

At **2026-10-13 18:58 UTC**, the four census commands listed below again
exited **0**: cluster UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`,
**23 namespaces**, **100 deployments/statefulsets/services**, **111 pods**,
and no matching names/namespaces or pod container/init-container images.
The same candidate-specific namespace command exited **1**, with both
`hexalith-staging` and `hexalith-staging-data` NotFound. Responses were
projected in memory; no credentials, raw responses, Secrets or domain histories
were printed or retained. This bounded census does not certify domain history
or exclude arbitrarily renamed applications.

The retained plan remains `prepared-blocked`, with deployment/activation
unauthorized, acceptance **FAIL**, no live evidence and null release image,
chart, provenance and rollback identities. Its dated statement that no
Development binding exists predates the new code; it is preserved as a
historical preparation record. Plan file SHA-256 remains
`04b24f38d1910390bb075b8fe27e8f159ba202b6aeacf35f517bcc272a8043dc`;
candidate file SHA-256 remains
`b86836a73c5b4c1c6bccdd45f3fc908c467a0f9ccfb2aa02c71025bb7e808618`.
The original qualification report retains its pinned hash below. Offline
regeneration was not repeated: the required Platform-local nested inputs
remain absent, and none were initialized.

Independent source investigations found no production resource-binding issuer,
certified Timesheets inventory/migration provider or installed subscriber/shared
rebuild evidence. No separate Timesheets runtime fix was established. All four
owner requirements below still apply; spec-23 remains draft, Story 3.6/Epic 3
remain in progress and release stays **FAIL**. Only Timesheets evidence/tracking
documents changed; no deployment, domain/maintenance operation, runtime build,
dependency update or Git mutation was issued.

## Prior offline candidate qualification

Timesheets source `aefd76772e84018442eb21b36549b8499a488721` consumes
Platform `ce6f2d34398803644b29d02ba57625898190ce20`. The initial Timesheets
tree was clean on `main`. This recheck reads the new Platform preparation
and native cluster state; it does not establish deployed acceptance.

**Result:** A concrete offline staging candidate now exists. It remains
outside active composition, its proposed namespaces are absent, and the
production provider and deployed-evidence gates remain open. Story 3.6 and
Epic 3 stay in progress; release stays **FAIL**.

## New preparation evidence

Platform's [preparation README](../references/Hexalith.Platform/eng/timesheets-deployment/README.md)
and [retained plan](../references/Hexalith.Platform/eng/timesheets-deployment/plan.json)
propose staging on `jpiquot@local`, API `https://192.168.1.30:6443`, application
namespace `hexalith-staging`, data namespace `hexalith-staging-data`, public
hostname `timesheets.hexalith.com`, and private port 8081. These are proposed
bindings, not installed endpoints or granted domain permissions.

The retained plan has preparation hash
`80aaf70967771c671e9245982a906ea9b77d56bdb6b568ee60015ad4e33305c7`,
Platform snapshot `422e7bf13e48a56b3d308eb1cfdd763c17a4b26b`, Timesheets
candidate `0e336693234c70735a3c8cd289a1c195a21e49fd`, and direct Timesheets
gitlink/observed checkout `a662ff130779524cbd759f76a018c5b330608eb3`.
It explicitly records source-enrollment drift, `prepared-blocked`,
`deploymentAuthorized=false`, `activationAuthorized=false`, `acceptance=FAIL`,
and no collected live evidence or retained release digests. Its identities
are preparation inputs, not the current source's release provenance.

`PlatformServerBindings.ReferenceProjects` and `ReferenceImages` contain no
`timesheets/host` binding. `TimesheetsEnrollmentTests` explicitly verifies
that the candidate selects its dependency closure outside active composition.
`PlatformAppModel.Compose` still rejects non-Development environments.
Platform hosting/release stories 4.14–4.17 and 4.22 remain in backlog.

P1 now includes a read-only HTTP adapter. Its
[contract](../references/Hexalith.Platform/docs/contracts/p1-receipt-provider-v1.md)
still supplies no operationally enrolled issuer, operator inventory, trusted
clock acquisition or accepting composition. It concerns Memories PG2/C1;
searches of Platform `src/` and `tests/` found no Timesheets inventory,
migration-authority or resource-binding issuer implementation.

## Fresh native reads

All four census commands exited **0**:

```text
kubectl --context jpiquot@local --request-timeout=15s get namespace kube-system -o json
kubectl --context jpiquot@local --request-timeout=15s get namespaces -o json
kubectl --context jpiquot@local --request-timeout=15s get deployments,statefulsets,services -A -o json
kubectl --context jpiquot@local --request-timeout=15s get pods -A -o json
```

Cluster UID remained `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, with **23
namespaces**, **100 deployments/statefulsets/services**, and **111 pods**.
No inspected name/namespace or pod container/init-container image matched
`timesheet`, `eventstore` or `platform`. This bounded census cannot exclude
arbitrarily renamed workloads or certify complete domain history.

The candidate-specific command exited **1**, with both namespaces NotFound:

```text
kubectl --context jpiquot@local --request-timeout=15s get namespaces hexalith-staging hexalith-staging-data -o json
```

Responses were projected in memory to counts, names, namespace identity and
NotFound outcomes. Raw responses and credentials were neither printed nor
retained; no Secrets or domain histories were queried.

## Offline regeneration limit

The existing preparer was invoked before Timesheets documentation edits:

```text
python3 /home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform/eng/timesheets-deployment/prepare.py --platform-root /home/administrator/projects/hexalith/timesheets/references/Hexalith.Platform --source-root /home/administrator/projects/hexalith/timesheets --output /tmp/timesheets-36-platform-candidate-db3y1ydi/plan.json
```

It exited **1**: `Preparation refused: invalid, changed or unavailable
input/output. No deployment was performed.` No plan was published. Required
Platform-local inputs are absent: the nested Builds schema/validator, owner
manifests under its nested EventStore/Tenants/Memories references, and its
direct Timesheets checkout. Repository instructions forbid initializing
these nested dependencies for this task; none were initialized. The retained
plan was inspected without claiming a current-source regeneration or schema
validation pass.

The [original qualification report](deployed-magic-link-acceptance.md) remains
byte-for-byte unchanged at SHA-256
`f3fa3e844f0e23f732371e80258945feede0866b0608e5768457433ebacb2118`.
Platform's preparer pins that dated report. This supplement records newer
facts without invalidating its consumed report or rewriting historical evidence.

## Conditions for resuming acceptance

The named candidate resolves target planning. Before an executable deployed
acceptance plan can be approved, the responsible owners still need to qualify:

1. Hosted composition, environment bindings and retained Release artifacts,
   including approved target identities and real access/reference gates.
2. Production signed per-request tenant/domain/actor bindings accepted by
   EventStore; Development signing and fixture authority remain excluded.
3. Complete certified legacy coverage, an immutable source cut, enforced
   writer/delivery quiescence, migration authority, owner continuity and
   pending-delivery recovery.
4. Installed subscriber delivery, shared rebuild and persisted magic-link
   acceptance on the exact release and target.

Timesheets' default-deny migration authority and unavailable inventory
provider remain. Existing mapped handlers and passing isolated manual
delivery supply local source evidence only. No independent Timesheets code
fix that closes these external gates was established. Draft spec-23 remains
unready; target-discovery questions need not be repeated. No infrastructure
approval, deployment, domain write, migration or legacy repair was performed.
