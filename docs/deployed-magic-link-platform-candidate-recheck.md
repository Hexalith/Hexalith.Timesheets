# Story 3.6 Platform candidate recheck — 2026-10-13

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
