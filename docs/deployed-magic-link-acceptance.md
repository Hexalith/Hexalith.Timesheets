# Story 3.6 deployed magic-link qualification

Observed read-only on **2026-10-13**, from Timesheets source
`6a95dbb10721fbe22a7fe89e9c021454aed43b45` and Platform source
`2c3f0fd4befe4e7e2aa1a3f7ce8ae1d5e1808f3c`. Both working trees were
clean before this documentation update. Platform is the user-selected source
for the deployment information; its checkout was inspected without changing
or initializing any submodule.

**Result:** Platform's designated infrastructure and existing native access
are identified. No qualified Timesheets deployment, resource-binding issuer
or certified Timesheets inventory/maintenance provider was found. Story 3.6
and Epic 3 remain in progress; release remains **FAIL**.

## Story 3.6 build resume recheck (2026-10-13)

Resuming from clean Timesheets `main` at
`28ffeaee83e9414ce9e205e57f674e4c29a6e40b`, with Platform still at
`2c3f0fd4befe4e7e2aa1a3f7ce8ae1d5e1808f3c`, repeated the bounded native
census below. All four commands exited **0**. Responses were projected in
memory to counts, namespace UID and matching names/images; no raw response
or credentials were printed or retained.

```text
kubectl --context jpiquot@local --request-timeout=15s get namespace kube-system -o json
kubectl --context jpiquot@local --request-timeout=15s get namespaces -o json
kubectl --context jpiquot@local --request-timeout=15s get deployments,statefulsets,services -A -o json
kubectl --context jpiquot@local --request-timeout=15s get pods -A -o json
```

The namespace UID remains `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`.
Counts remain **23 namespaces**, **100 deployments/statefulsets/services**
and **111 pods**. No namespace/workload/service name or pod container/
init-container image matched `timesheet`, `eventstore` or `platform`.
The same bounded-discovery limits below apply; renamed workloads cannot be
excluded by this check.

An independent source investigation confirmed the default-deny Timesheets
migration authority, unavailable verified-inventory provider and absent
production resource-binding provider. Existing mapped projection/rebuild
handlers and local manual delivery do not prove an installed subscriber.
No independent Timesheets code change that closes these external gates was
established. Spec-23 stays **draft** because deployment/enrollment,
production bindings, certified coverage/maintenance/continuity and deployed
subscriber/shared-rebuild evidence remain missing. No domain or maintenance
operation was issued.

## Recovered environment and access information

| Requested information | Evidence-backed value | Scope |
| --- | --- | --- |
| Infrastructure target | Existing Kubernetes installation `192.168.1.30`; API `https://192.168.1.30:6443` | Platform designation and current kubeconfig agree; not a selected Timesheets application deployment |
| Environment domains | Staging `hexalith.com`; production `tache.ai` | Planned Platform environment mapping; no Timesheets URL/namespace was found |
| Read-only access | Existing `kubectl` context `jpiquot@local`; authenticated subject `jpiquot`, group `system:authenticated` | Successful direct native reads; no credential or grant was created |
| Cluster identity | `kube-system` namespace UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557` | Matches Platform's retained 2026-10-04 `sourceClusterUid` convention |
| Current node | `node1`, Ready, kubelet `v1.34.9` | Current read-only observation |
| Responsible role | Platform Administrator, with sole-owner/no-deputy qualification decision | Existing governance; native authentication does not establish Timesheets production admission |
| Named provider contact | Jérôme Piquot, Platform P1 technical maintainer and selected policy decision signer; Security owns operational adoption | P1 contract ownership, not an enrolled Timesheets migration authority |
| Resource-binding authority | No production implementation found in Platform source/composition | Timesheets needs per-request tenant/domain/actor bindings; a generic OIDC issuer does not establish that capability |
| Certified inventory/maintenance receipts | No Timesheets provider or receipt found | Kubernetes census, infrastructure maintenance approvals and generic P1 fixtures are different evidence scopes |

Sources: Platform's [hosting/roles specification](../references/Hexalith.Platform/_bmad-output/specs/spec-platform/SPEC.md),
[management qualification](../references/Hexalith.Platform/eng/cluster-management/QUALIFICATION.md),
[sole-owner decision](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085020z-owner-authorization/authorization.json),
[retained native census](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085143z-census/inventory.json),
and [P1 contract](../references/Hexalith.Platform/docs/contracts/p1-receipt-provider-v1.md).

## Current native observations

All commands below exited **0**, using the existing credentials without
printing them. API objects were projected in memory to names, counts, image
names, identity and readiness; no Secret objects or domain histories were
queried and raw responses were not retained.

```text
kubectl config current-context
kubectl config get-contexts
kubectl config view --minify -o json
kubectl --context jpiquot@local --request-timeout=15s get namespace kube-system -o json
kubectl --context jpiquot@local --request-timeout=15s get namespaces -o json
kubectl --context jpiquot@local --request-timeout=15s get deployments,statefulsets,services -A -o json
kubectl --context jpiquot@local --request-timeout=15s get pods -A -o json
kubectl --context jpiquot@local --request-timeout=15s auth whoami -o json
kubectl --context jpiquot@local --request-timeout=15s get nodes -o json
```

The configuration response was used only to read the API server field;
credential, certificate and plugin fields were never printed or saved.
The live census observed **23 namespaces**, **100 deployments/statefulsets/
services**, and **111 pods**. No workload/service namespace or name contained
`timesheet`, `eventstore` or `platform`; no pod container/init-container image
contained those terms either. No identifiable Timesheets deployment was found
within those checks. This bounded census cannot exclude an arbitrarily renamed
workload, establish complete installed module coverage, or certify a global
issuance history. It supplies no Timesheets application namespace, endpoint,
artifact digest, history cut or authoritative owner continuity.

Native read identity and the retained cluster UID match are useful access
evidence. They confer neither a Timesheets server-side role nor authority to
issue a magic-link migration receipt. No port-forward, domain request,
migration, provisioning or cluster mutation was performed.

## Platform source findings and outstanding prerequisites

Platform's current [MVP specification](../references/Hexalith.Platform/_bmad-output/specs/spec-platform/SPEC.md)
explicitly excludes Timesheets from MVP acceptance; later enrollment uses the
same declaration schema. Its [AppHost](../references/Hexalith.Platform/apphost.cs)
has Agents and Works opt-ins; the Works composition is a Development-only
preview. No Timesheets opt-in or resource registration was found.
[Sprint tracking](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml)
keeps staging namespace, secrets, Dapr, durable broker and first-release
deployment stories 4.14–4.17 and 4.22 in backlog. These records describe
missing implementation; the existing cluster alone does not supply it.

Searches for `IWorkloadAssertionIssuer`, `CanBindResources`,
`IMagicLinkV1VerifiedInventoryProvider`, `IMagicLinkIndexMigrationAuthority`
and `MagicLinkV1InventoryReceipt` found no implementation in Platform's
`src/` or `tests/`. The current EventStore
[JWT workload issuer](../references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtWorkloadAssertionIssuer.cs)
documents that authority-mode client-credentials tokens cannot carry
per-request resource bindings. Timesheets'
[assertion source](../src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs)
refuses an issuer without `CanBindResources`. Development symmetric signing
is not a production provider.

Platform's generic P1 implementation supplies offline encoding/verification.
Its [contract](../references/Hexalith.Platform/docs/contracts/p1-receipt-provider-v1.md)
explicitly retains `NO_ENROLLED_BOOTSTRAP`, `NO_SUPPORTED_RECEIPT_ISSUER`,
`NO_VERIFICATION_KEY` and `NO_RETRIEVAL_OR_STATUS_ENDPOINT`, and no enrolled
client identity or mapping. That contract concerns the Memories PG2/C1
decision packet; it is not an implementation of the Timesheets
[verified inventory port](../src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkV1VerifiedInventoryProvider.cs).
Infrastructure backup, recovery or Kubernetes management receipts cannot
stand in for the required complete cross-tenant issuance inventory,
immutable source cut, enforced writer/delivery quiescence and migration
operator authorization.

Before a deployed acceptance run, Platform and the relevant technical owners
must provide:

1. An enrolled Timesheets application deployment with exact environment,
   namespace, artifact identity, internal/public endpoints and real access
   gates.
2. A production resource-binding issuer accepted by the EventStore gateway.
3. Independently verified full legacy coverage and maintenance evidence,
   including owner source continuity and pending-delivery recovery, with
   default-deny migration authority replaced only by a qualified provider.
4. Installed subscriber delivery and shared-rebuild acceptance on the target.

Those are external implementation/installation prerequisites, not values the
user needs to reconstruct from memory. No deployed write commands can yet be
bound to a qualified target. Preserve the passing isolated local evidence
separately; it does not close these gates.
