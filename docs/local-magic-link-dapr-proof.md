# Local magic-link Dapr proof

The preflight below records the earlier blocked state. The connected local topology proof later in this document is the current infrastructure result; Story 3.6 acceptance remains open.

## Preflight on 2026-10-11

**Blocked before the acceptance journey.** The checked-in EventStore AppHost can run
EventStore, Tenants, and its sample domain, but it does not attach Timesheets as a
Dapr domain module or admit `timesheets` as a trusted effect submitter. The
Timesheets AppHost explicitly has no EventStore resource or Dapr sidecar. The
only running EventStore AppHost observed during preflight belonged to a separate
EventStore integration test, so its resources were left untouched. No
`timesheets` Dapr application was registered. Reaching a real Timesheets
record/issue/use path would require platform-owned topology work that this
spec forbids adding here. No live test or fixture was added, no test state was
written, and no local or deployed acceptance is claimed.

| Check | Exact command and observed result |
| --- | --- |
| SDK and container daemon | `dotnet --version` → `10.0.401` (the checked-in `global.json` pin); `docker info --format '{{.ServerVersion}}'` → `29.8.2`. |
| Dapr applications | `dapr list` exited 0 and listed `eventstore`, `eventstore-admin`, `tenants`, `tenants-api`, `sample`, `sample-api`, `sample-blazor-ui`, and `eventstore-admin-ui`; it listed no `timesheets` application. |
| Running AppHost ownership | `aspire ps --non-interactive` listed `Hexalith.EventStore.AppHost.csproj` as running at PID 8448. `readlink /proc/8448/cwd` returned `/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0`; its command line ran `SignalRDaprProjectionDispatchProofTests`. The process was externally owned and was not stopped or restarted. |
| Repository AppHost attachment | `rg -n 'AddEventStoreDomainModule|WithEventStoreTrustedEffectSubmitter|timesheets' references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` found sample/Tenants attachment and only a comment explaining trusted submitter setup; it found no Timesheets attachment or `timesheets` allow-list call. `src/Hexalith.Timesheets.AppHost/Program.cs` states that EventStore topology is deferred and attaches no Dapr sidecar. |
| Persisted read-model access | `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` scopes `statestore` to `eventstore`, `eventstore-admin`, and `tenants` and explicitly directs new domain services to a separate scoped component. Timesheets' token index and Activity Type catalog use `statestore` through `DaprReadModelStore`; adding a Timesheets sidecar alone would still leave those reads and writes unavailable. |
| Credentials | `env | cut -d= -f1 | rg -i '^(TIMESHEETS|EVENTSTORE|ASPIRE|DAPR|AUTHENTICATION|APP_API_TOKEN|JWT)' | sort` produced no variable names. No secret values were read or captured. EventStore's local signing credentials are generated per AppHost run and are not connected to a Timesheets resource in the checked-in topology. |
| Isolated state and projections | No Timesheets sidecar, isolated test identity, or delivery route exists in the current topology. The token-hash index and fresh Activity Type catalog therefore cannot be populated and inspected through real delivery for this journey. Existing local Redis and the externally owned runtime were not modified. |

The decisive prerequisite is a platform-owned local topology that attaches the
existing Timesheets HTTP host with Dapr app ID `timesheets`, configures the
EventStore gateway and workload signing, admits the required trusted effect
calls, and provides a platform-approved scoped read-model store for delivery
and loading of the index and catalog. It must also provide isolated test
identities/state and a development fixture access guard without weakening
production authorization. The
authority-mode issuer still cannot bind tenant/domain/actor claims and remains
a separate production gate.

## Re-run procedure after the prerequisite is delivered

Do not run the opt-in lane against the current topology: it cannot reach a
Timesheets Dapr application. Once the platform owner supplies the connected
topology and its local credential and projection-delivery procedure, record the
exact start, credential acquisition, isolated-state setup, projection delivery,
stored-stream/read-model inspection, restart, and cleanup commands here before
claiming acceptance. The current repository supplies no working commands for
those dependent steps. Keep signing keys, credentials, raw links, and token
hashes out of command captures.

The intended Timesheets verification command, after the connected topology and
`MagicLinkLiveDaprTests` exist, is:

```bash
TIMESHEETS_LIVE_DAPR=1 tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

That lane must fail if an opted-in prerequisite disappears. It must require
`Completed` plus exact stored-event readback before HTTP success, inspect the
stored record/issue/use/effect events and delivered index/catalog, then repeat
after actor or process restart and race concurrent use requests. One stored
use/effect batch and common opaque denial for every loser are required. The
current preflight did not reach any of these observations. No processes were
started by this preflight, so no cleanup command was run.

## Connected local topology proof on 2026-10-11

This later run supersedes the topology findings in the preflight above. The
preflight remains a record of what was unavailable before the local AppHost
composition was added. From the repository root, these commands produced the
observations below:

| Command | Observed result |
| --- | --- |
| `dotnet build src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --no-restore -m:1 -v:q` | Passed with zero warnings and errors. |
| `dotnet build references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj --no-restore -m:1 -v:q` and the corresponding `Hexalith.EventStore.DomainService.csproj` build | Both passed with zero warnings and errors. |
| From `references/Hexalith.EventStore`: `tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method '*AdminOperationalIndexMetadata_Create_IncludesNamedRouteForKeyedProcessorDomain' -method '*NamedProjectionMetadata_IncludesAsyncOnlyKeyedProcessorDomain'` | Two passed, zero failed. |
| `aspire run --non-interactive` followed by `aspire describe --non-interactive --format Json` | EventStore and Timesheets projects and their Dapr sidecars reported `Running` and `Healthy`. EventStore listens on local 8082; Timesheets retains public 8080 and internal 8081. |
| `dapr list -o json` | Distinct `eventstore` and `timesheets` app IDs; their sidecars target app ports 8082 and 8081 respectively. |
| `python3 scripts/local_aspire_gateway_probe.py` | A resource-bound Timesheets assertion reached the command processor (invalid probe command rejected 422), status returned `Rejected` (200), and stream read returned 200. Missing assertions returned 401; wrong-tenant assertions returned 403 on command, status, and stream routes. No domain event was committed by this probe. |
| `python3 scripts/local_aspire_projection_probe.py` | Signed metadata returned dispatch v2 and both Timesheets named routes. A signed direct request to public `http://127.0.0.1:8080/project/v2` returned 404; the sidecar invocation reached the internal listener. A synthetic `ActivityTypeCreated` delivery returned 200/`Completed`, and a separate Dapr GET through `timesheets-readmodels` returned the catalog item with `Fresh` cursor 1. A synthetic `MagicLinkConfirmationCapabilityIssued` delivery returned 200/`Completed`; a separate Dapr GET verified its token-hash candidate index entry mapped to the expected isolated tenant and capability without printing the hash. Missing and wrong-operation assertions returned 401 and 403. The Timesheets sidecar listed only `timesheets-readmodels`; GET against `statestore` returned 400. |

The two local-only scripts require unique Timesheets process and Dapr app IDs,
bind the process to its Aspire-provided Dapr endpoint, and read the generated
Development signing key from its environment in memory on Linux. They print no
key, app-channel token, raw link, or token hash. The
synthetic projection request invokes the SDK route; it does not write directly
to the read-model component. Its isolated tenant entry remains in the local
read-model Redis database. This is delivery-infrastructure proof only. No
EventStore-actor-originated Timesheets event, record/issue/use journey,
single-use race, or restart acceptance passed here; Story 3.6 remains in
progress and release remains **FAIL**.

Broader build/test checks remain blocked outside this topology increment:
`dotnet build Hexalith.Timesheets.slnx --no-restore -m:1 -v:q` failed in
`Hexalith.Works.Contracts` with missing EventStore event contract types;
`dotnet build Hexalith.Timesheets.slnx --no-restore -p:UseHexalithProjectReferences=true -m:1 -v:q`
failed with CS1704 for duplicate `Hexalith.Commons.UniqueIds` assemblies
(versions `2.30.1.0` and `1.0.0.0`). From the EventStore repository,
`dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-restore -p:BuildProjectReferences=false -m:1 -v:q`
failed with CS0006 because its AppHost reference assembly was absent; the
ordinary project graph also failed in the existing Admin UI
(`INotificationService`) and sample API (missing assets). Its new AppPort unit
case could not run. The Timesheets projection-handler class passed (70/70)
through its built test executable after `dotnet test` hit the repository's
VSTest-on-.NET-10 tooling error.
