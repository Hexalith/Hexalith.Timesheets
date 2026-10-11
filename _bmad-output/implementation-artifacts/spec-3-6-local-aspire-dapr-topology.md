---
title: 'Connect a local Timesheets Aspire/Dapr topology to EventStore'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: 'dd1028808b6592b9ff869d2dce5c1c24a5ad7cf5'
review_loop_iteration: 0
context:
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - 'docs/boundary-decision-record.md'
---

<frozen-after-approval reason="user-authorized intent">

## Intent

**Problem:** Story 3.6 cannot run a local persisted acceptance journey because the Timesheets AppHost has no EventStore actor service, `timesheets` Dapr sidecar, signing arrangement, or scoped persisted read-model component.

**Approach:** Compose the existing Timesheets HTTP host and a real EventStore gateway in the Timesheets AppHost for local Aspire run mode. Give Timesheets only an isolated read-model component and prove sidecar identity, signed command/status/stream calls, and real projection delivery with persisted readback.

## Boundaries & Constraints

**Always:** Preserve existing uncommitted Story 3.6 edits. Keep the public/internal listener split and send Dapr domain-service calls to the internal listener. Isolate actor state and read models in distinct Redis databases and component scopes. Use development-only resource-bound workload signing and app-channel tokens; register `timesheets` as a trusted-effect caller. Keep Story 3.6 in progress and release at FAIL until its separate live journey passes.

**Never:** Grant Timesheets the EventStore actor state component, use direct domain-state persistence, weaken production authorization, change publish/deployed topology, add a second host, or claim Story 3.6 acceptance from startup. Do not stage, commit, push, or update submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Local topology | `aspire run` with local Dapr services | Healthy `eventstore` and `timesheets` app IDs, internal 8081 sidecar target | Startup fails visibly if resources unavailable |
| Signed gateway | Timesheets workload and isolated tenant | Command, status, and stream routes authenticate with bound assertions | Missing or wrong binding is denied |
| Projection delivery | Isolated synthetic event delivered through the signed SDK named projection route | Handler persists to the Timesheets read-model component and value survives readback | No direct read-model writes or Story 3.6 journey claim |
| State isolation | Timesheets Dapr component inventory | Only Timesheets read-model component in its sidecar; no actor store or actor data | Actor-store access is refused |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.AppHost/Program.cs` -- owns the local composition; retain its existing listener chain and `AddHexalithEventStoreSecurity` call. Override EventStore's conflicting 8080 endpoint locally.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- add a build-only EventStore gateway project reference because platform project metadata suppresses its build.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/{AspireDaprDomainModuleOptions,AspireDaprDomainModuleAspireExtensions,HexalithEventStoreDomainModuleExtensions}.cs` -- platform-owned optional sidecar AppPort seam; preserve the default for existing consumers.
- `references/Hexalith.EventStore/tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- verify explicit AppPort reaches sidecar options and unchanged defaults.
- `src/Hexalith.Timesheets.Server/MagicLinks/{MagicLinkTokenHashCapabilityIndexProjection,MagicLinkActivityTypeCatalogReadModelAddress}.cs` -- both loader and projection handler use these component addresses; use a dedicated component name consistently if chosen.
- `src/Hexalith.Timesheets.AppHost/DaprComponents/` -- local EventStore actor state on a separate Redis DB and Timesheets-only persisted read-model YAML; never load actor YAML in Timesheets sidecar.
- `docs/local-magic-link-dapr-proof.md` -- append exact live topology commands and evidence; preserve no-acceptance claim. `docs/launch-readiness.md` keeps release FAIL.

## Tasks & Acceptance

**Execution:**
- [x] Add the optional platform AppPort seam and focused platform test in the EventStore owning repository; the AppHost test project could not build in this worktree, while live Dapr reported the intended port.
- [x] Wire run-only EventStore gateway, domain registration, trusted-effect caller, generated channel tokens, shared per-run Development signing, endpoint separation, and Timesheets isolated sidecar in the Timesheets AppHost.
- [x] Add isolated, scoped, persisted Dapr state component definitions and route both Timesheets read models to the dedicated component.
- [x] Run focused builds/tests, start the topology, and record real Dapr identity, signed gateway, both named projection deliveries, persisted readback, public route denial, and actor-state denial evidence.
- [x] Update local proof/readiness records while keeping Story 3.6 in progress and release at FAIL.

**Acceptance Criteria:**
- Given local run mode and available Dapr dependencies, when Aspire starts, then `eventstore` and `timesheets` are distinct healthy Dapr applications and Timesheets' sidecar targets internal port 8081.
- Given a resource-bound Timesheets assertion, when command, status, and stream routes are invoked, then the gateway accepts the scoped calls and rejects missing or mismatched credentials.
- Given an isolated synthetic event, when a signed Dapr invocation delivers it to the SDK named projection route, then the Timesheets read-model component contains the projected value on persisted readback; this proves the delivery infrastructure, not actor-originated Story 3.6 acceptance.
- Given Timesheets sidecar component scope, when it attempts access to the actor store, then the request is denied.

## Implementation Notes

The user explicitly authorized implementation in this turn. The pre-existing Story 3.6 spec's topology exclusion is superseded only for this local prerequisite. The existing `docs/local-magic-link-dapr-proof.md` is user work and should be appended carefully. The projection matrix deliberately exercises the real SDK/Dapr read-model delivery path with a synthetic event because the default Timesheets processor fails closed before it can commit an initial event; the separate Story 3.6 live journey must prove actor-originated delivery.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| Blind 1: unpinned EventStore gitlink | `medium`: a clean checkout cannot use the new platform overload until these EventStore edits are committed and pinned; the user explicitly forbids staging, committing, or pushing in this task. | Defer to a later authorized commit/pin; keep the submodule changes in its owning repository. |
| Blind 2: read-model name in other host modes | `maybe-false`: both constants now name `timesheets-readmodels` globally, but this repository has no wired deployed Timesheets component to inspect. A deployed component inventory would settle whether an address migration is needed. | Defer to the deployment gate; do not change deployed topology here. |
| Blind 3: prior read-model migration | `maybe-false`: the preflight found no Timesheets Dapr sidecar or usable `statestore` locally, so no local prior index/catalog was demonstrated; deployed state is unknown. An inventory would settle it. | Defer to the deployment gate. |
| Blind 4: async keyed processor | `medium`: `DomainServiceRequestRouter` accepts keyed `IAsyncDomainProcessor`, while the new metadata gate checks only `IDomainProcessor`; an async-only domain loses named routes. | Patch the gate and focused test. |
| Blind 5: keyed-domain query types | `medium`: query handlers are collected into `queryTypesByDomain`, but the new `DomainMetadata` constructor passes an empty query list. | Patch the metadata and focused test. |
| Blind 6: legacy metadata omits keyed-only domains | `medium`: the versionless path still derives domains from discovered aggregates; this predates this change and is not used by the v2 projection proof. | Defer as a separate platform concern. |
| Blind 7: endpoint and AppPort tests | `false`: live signed metadata returned both named routes and `dapr list` showed app port 8081; the AppPort unit test could not build because of unrelated AppHost test-project errors, which the proof record states. | Reject as a missing-behavior claim; retain the test-build limitation. |
| Blind 8: probe run selection | `medium`: both probes select the first process and Dapr list match; another run could make their evidence ambiguous. | Patch to require unique matching identities. |
| Blind 9: fixed app IDs and Redis databases | `maybe-false`: app ID `timesheets` is required, and the probes use random tenant keys; another simultaneous run using the same app IDs or Redis databases could interfere. No such overlap was shown during the successful run. | Defer shared-host collision hardening; fail probes on duplicate app IDs. |
| Blind 10: synthetic state cleanup | `low`: the probe leaves a small, unique synthetic catalog entry; deleting a slice of the shared candidate index would require direct projection mutation, and the proof explicitly records the residual state. | Reject a cleanup patch for this local proof. |
| Blind 11: candidate-index delivery | `medium`: the script only dispatched and read the catalog, so the required index path was unproved despite sharing the component. | Patch the signed index dispatch and persisted readback. |
| Blind 12: public/internal HTTP boundary | `medium`: live proof established sidecar target 8081, but did not request the SDK route on public 8080. | Patch the local probe. |
| Blind 13: stale spec task boxes | `low`: the new spec says all tasks remain open after the proof passed. | Direct correction after review verification. |
| Edge 1: async-only keyed processor | `medium`: same verified router/metadata mismatch as Blind 4. | Patch with Blind 4. |
| Edge 2: keyed-domain queries | `medium`: same verified empty query metadata as Blind 5. | Patch with Blind 5. |
| Edge 3: invalid AppPort | `low`: the new optional port is passed through without range validation; an invalid value prevents Dapr invocation. | Patch with a 1–65535 guard and unit cases. |
| Edge 4: prior store migration | `maybe-false`: no local prior Timesheets component existed; deployed state remains unobserved. | Defer with Blind 3. |
| Verification gap: candidate-index delivery | `medium`: the live probe verifies the catalog route and readback only; index delivery can fail independently. | Patch with Blind 11. |

## Verification

The Timesheets AppHost, EventStore Aspire, and EventStore DomainService project builds passed with zero warnings or errors. The two focused DomainService metadata cases passed (2/2), and the Timesheets projection-handler class passed (70/70). `aspire run`, `aspire describe`, `dapr list`, and both local probe scripts passed: distinct healthy app IDs, `timesheets` sidecar on internal port 8081, signed command/status/stream access, public SDK route 404, signed catalog and candidate-index `/project/v2` deliveries completed with persisted readback, and actor-store denial. `git diff --check` passed in both repositories; the Aspire session was stopped afterward.

The full Timesheets solution build failed in Works.Contracts missing EventStore types; a source-reference variant failed on duplicate UniqueIds assemblies. The EventStore AppHost test project could not compile because of unrelated Admin UI/sample API graph errors, so the new AppPort unit cases remain unrun. These limits do not turn the synthetic projection proof into actor-originated Story 3.6 acceptance. Exact live observations and commands are in `docs/local-magic-link-dapr-proof.md`. Story 3.6 remains in progress and release remains FAIL. No files were staged, committed, or pushed.
