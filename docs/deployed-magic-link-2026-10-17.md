# Story 3.6 deployment, authority and runtime continuation

Story 3.6 remains **in progress**, with deployed acceptance **NOT RUN** and release
**FAIL**. This continuation implements missing technical capabilities and records
fresh observations; it does not install a deployment, authorize migration or accept
an expired recovery point. The owner self-reviews project documentation under the
current sole-owner policy. Runtime and authority claims below come from actual
configuration, tests and observations, with their limits stated.

The [sanitized acceptance packet](../_bmad-output/implementation-artifacts/evidence/3-6/20261017t142841z-deployment-authority-runtime/README.md)
contains exact commands, original results, source attribution and failed attempts.
The original [deployment qualification](deployed-magic-link-acceptance.md) remains
unchanged because Platform's historical preparer pins its exact bytes.

## Deployment identity and installed wiring

| Field | Current evidence |
| --- | --- |
| Native target | `jpiquot@local`, `https://192.168.1.30:6443` |
| Cluster identity | `kube-system` UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557` |
| Server / node | Kubernetes `v1.34.9`; `node1` Ready, UID `46d8d528-420d-4501-a6d8-12680d8a6ac6` |
| Timesheets deployment identity / installed artifact | Unavailable: no Timesheets workload/image in the inspected resource kinds |
| Proposed application / data namespaces | `hexalith-staging` / `hexalith-staging-data`; both absent from the complete namespace listing |
| Proposed public endpoint | `https://timesheets.hexalith.com`; no installed Timesheets ingress or Gateway route observed |
| Proposed internal endpoint | `http://timesheets-internal.hexalith-staging.svc.cluster.local:8081`; no installed Service |
| Subscriber / shared rebuild | No Timesheets delivery registration, sidecar or read-model component observed; Gateway API resource types unavailable |

At **2026-10-17T14:28:55Z**, the bounded census contains 24 namespaces, 120
deployments/statefulsets/daemonsets/services, 117 pods, 14 Dapr components and
zero declarative Dapr subscriptions. EventStore exists in `ext-secrets-1-isolated`
under another story; this work does not adopt or change it. A name/image census
cannot exclude arbitrary renaming or certify a global historical source inventory.
Zero declarative subscriptions is not itself a defect: Timesheets uses the shared
SDK's gateway projection dispatch and rebuild protocol rather than a module-owned
pub/sub consumer.

Platform now has maintained [application and environment Helm packages](../references/Hexalith.Platform/eng/timesheets-deployment/hosted-package-preparation.md).
They keep public 8080 and private 8081 separate, restrict public routes, bind
the fixed `timesheets-readmodels` component, scope the Dapr caller/secret/network
policies and include shared-rebuild paths. Missing immutable image, qualified
trust, Secret references and network inputs refuse rendering. The Timesheets HTTP
host opts into SDK container publishing; AppHost and ServiceDefaults are unchanged.
Local chart archives are prepared artifacts, not installed or released images.

SDK container publishing also succeeded to the local Debug archive
`/tmp/timesheets-story36-20261017-source-debug.tar.gz` (61,386,240 bytes), archive
SHA-256 `ba02223104839a33ea1d6031f7f329accda2ce2c3d4de775d410c3f81ce1c41c`.
It targets Linux amd64 and runs as `app`; no registry push occurred. Its source
identity is Timesheets HEAD `a376b71d43330a716f836c3086e562e505511dd3` plus the
recorded uncommitted source changes and referenced technical changes. This local
Debug archive is not a production Release image or an installed artifact revision.
After the review patches, a separately rebuilt Debug archive is retained at
`/home/administrator/hexalith-recovery-evidence/timesheets-3-6/20261017t142841z-deployment-authority-runtime/reviewed-source-debug.tar.gz`,
SHA-256 `0dd15a4de359b8f644fd156d5844168bbd7ce2dd53fa884c4fcc661375953aa3`.
The original archive remains unchanged; neither is deployed.

## Actual native TLS, identity and current rights

The kubeconfig has TLS verification enabled. An actual separate TLS connection
verified the API server's chain against that configured public CA and its IP SAN
`192.168.1.30`: TLS 1.3, `TLS_AES_128_GCM_SHA256`, ALPN `http/1.1`, OpenSSL 3.5.5.
The served leaf SHA-256 is
`c4d7ae040d6084bd8c16de24c44b6534432913de66a0a99734535ca29d037e4e`.
This TLS connection did not present a client certificate. Existing kubectl did
authenticate successfully as `jpiquot`, group `system:authenticated`.
Its authenticated credential ID matches the configured public client certificate's
DER SHA-256 exactly:
`fe03137074838e0331196425e1f009838c147ebfb3abaf30c0f594d2269e6e00`.
Private keys, tokens, credentials and raw native objects were not exported into
the acceptance packet.

Current `auth can-i --list` reports wildcard resource and nonresource permissions
at its observation cut. Actual `jpiquot-cluster-admin` binding UID
`fe4fd46f-c040-4719-931f-b9af54cedcde`, resourceVersion `8310`, binds this user to
`cluster-admin`; twelve additional exact entitlement checks returned `yes`.
No permission was added. These observations establish current native ability;
they do not establish complete historical grant/revocation lineage or Timesheets
domain/migration authorization. They do not qualify the expired candidate's
different frozen transport/parser profile.
The reviewed collector repeated the observation at **14:51:33Z** using one sealed
anonymous in-memory copy of the existing inline certificate context for all
native requests. It rejects credential plugins, proxies and external key paths,
pins endpoint/TLS settings and checks identity/context continuity. The repeated
census and native identity agree with the initial cut; old observations remain
unchanged. No credential bytes were written to disk or acceptance records.

## Production resource-binding capability and legacy prerequisites

EventStore now provides an opt-in [RSA workload authority and validating client](../references/Hexalith.EventStore/docs/security/workload-binding-authority.md).
The server checks current expiring grants, the entire requested scope, mandatory
binding keys and a protected client credential; the client and receiver validate
the signed result. Dedicated `Authentication:WorkloadJwtBearer` settings preserve
the public human issuer. Tokens are bounded to 120 seconds and are not cached by
this profile. Revoking loaded grant configuration stops new issuance; already
issued tokens need receiver/network/key retirement or their bounded expiry.
The default remains disabled. No actual signer, client, grant or HTTPS authority
endpoint was enrolled. Local cryptographic/HTTP tests prove software behavior;
production binding acceptance remains unestablished.

Timesheets' domain access gates, migration authority and verified-inventory
provider remain fail-closed. The new broker does not manufacture those receipts.
The exact missing legacy evidence is:

- Complete owning installation coverage and an immutable cross-tenant issuance
  cut, including terminal capabilities, omitted collisions and independently
  verified erasure exclusions, with exact count and ordered contribution digest.
- Authoritative owner continuity and retained identities/recovery of pending
  deliveries. The historical four uncovered deliveries remain unresolved; their
  recovery has not been inferred from the new isolated runtime test.
- Enforced source-writer and delivery quiescence through promotion or abort,
  current receipt validity/revocation, qualified migration authority and an exact
  maintenance/resume plan. None is established by native cluster-admin rights.

No direct state mutation, legacy seeding, clearing, replay recovery or migration
was performed. Absence of a current Timesheets deployment does not certify an
empty historical inventory.

## Actual runtime and fresh preparation

The disposable local Dapr journey passed **1/1 in 126.032s** with existing built
assets. Its original assertions verify Fresh catalog loading, persisted confirm
and adjust outcomes, one concurrent-use winner, replay denial, composed Time Entry
rebuild, actor restart and fresh use after restart. All captured process identities
exited, the three owned containers and component copies were removed, and shared
broker/state/retained TTL/scheduler fingerprints matched. It is fresh local
runtime evidence; the new production authority is separately tested and was not
enrolled in this run. Deployed application TLS, subscriber/shared-rebuild and
persisted journey acceptance remain **NOT RUN**.

Fresh offline prerequisite RID **`20261017t143202z`** is prepared at
`2026-10-17T14:32:02Z` with exclusive ceiling **`2026-10-18T14:32:02Z`**.
Its packet SHA-256 is
`d14efe6e8e82d983187416392ccc77207b446827f149caf3b45ed4b1ae90939a`.
[Owning preparation and exact commands](../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t143112z-fresh-recovery-prerequisite/README.md)
retain useful current observations but make no current-recovery claim.
The old candidate stays expired at its original `2026-10-16T16:27:56Z` ceiling;
the original verifier refusal stays exit 1. Neither was replayed or rewritten.

## Operational approval boundary

Hexalith.Platform manages both staging and production: environment provisioning,
configuration, identity/secret delivery, release composition, deployment,
promotion and recovery. Timesheets supplies its domain contracts, authoritative
inventory and acceptance checks, and consumes Platform's actual environment
evidence. The [Platform-owned handoff](../references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.md)
records that responsibility explicitly. Missing environment resources are
Platform work; the owner is not being asked to configure them in Timesheets.

The Platform hosting and recovery handoffs identify proposed commands, exact
target, effects, failure retention and recovery boundaries. They explicitly
remain **unready for execution**: retained production images/packages, a qualified
environment layer, protected authority enrollment, full legacy continuity and
qualified current capture/restore adapters do not exist in the inspected evidence.
A command containing invented inputs or unresolved placeholders is not a final
approval request. This continuation performs no deployment, recovery, migration,
permission change, publication or Git mutation. Those actions still require the
owner's approval of a fully bound concrete operation.
The [current Platform-owned blocked proposal](../references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.json)
retains the actual reviewed chart/tool hashes and staging target, with future
values-file locations in Platform. Its environment/application stages and
forward-only scale-to-zero/ingress-closed failure procedure belong to Platform's
managed release lifecycle. Required approved values files do not exist; no
executor or authorization is supplied. The [earlier Timesheets packet proposal](../_bmad-output/implementation-artifacts/evidence/3-6/20261017t142841z-deployment-authority-runtime/operational-plan.json)
and its manifest remain unchanged as historical preparation.
