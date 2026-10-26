# Five AppHost security and runtime deferrals — 2026-10-26

DW-12, DW-14, DW-67, DW-106 and DW-166 have passing **local Debug/source** qualification. Overall launch remains **FAIL**; deployed acceptance is unfinished.

The source baseline is Timesheets `39e1455250206d6a59476f88c685d03f64887f81` and EventStore `dcd3dce42b67a305b608f03c4dab2b812791abae`. [Verification metadata](../evidence/five-apphost-security-runtime-deferrals/verification.json) records source hashes, commands, package versions and exact receipt paths. No dependency or submodule changed.

## Current verification

- Matching Debug/source warnings-as-errors build passes with zero warnings/errors. The restore/build graph follows root AGENTS.md; no version workaround is used.
- Python script regression suites: **199 passed** (166 runner, 30 index selection, three evidence lineage), 23.353 s. This includes actual redirect refusal, poisoned-proxy bypass, credential TLS, ownership, source/asset capture, readiness, stable-identity delivery retries and fail-closed cleanup. [Log](../evidence/five-apphost-security-runtime-deferrals/review-final-python.log.gz).
- Actual compiled configuration/model/construction probes: **67 passed**. Both ordinary and security graphs construct/dispose without startup. Actual secret references, fixture-only health registration and startup ordering are inspected without resolving secrets. [Results](../evidence/five-apphost-security-runtime-deferrals/compiled/run-wpray717/guards.json).
- Actual Dapr read-model store over a yielding per-key-ETag byte backend: **3 passed**, including recovery after optimistic conflicts and durable selection of both owners; changed content under an existing identity is refused and original data remains. [XML](../evidence/five-apphost-security-runtime-deferrals/review-final-batch.xml.gz).
- Ordinary-generation regression: **1 passed**; privacy checks: **12 passed**; architecture suite after readiness wiring: **118 passed**. Their separate receipts are linked in verification metadata. After document reconciliation, all **16 readiness-document checks** pass; the final full Debug/source build has zero warnings/errors and preserves every qualified asset hash. The initial obsolete smoke-deferral assertion failure is retained separately.

Independent review completed with eight small fixes verified. A separate checkout without dependency submodules passes all 166 runner tests; deleting the source guard in memory now fails its targeted test. Two unverified provenance questions and the pre-existing journey HTTP opener are recorded as three follow-ups in the ledger. Initial parallel Python checks contended on the shared admission lock; their receipts are retained separately and both suites pass when run sequentially.

The six full baseline xUnit executables ran individually: **2,693 passed, zero failed, five intentional default skips (2,698 cases)**. These full-suite receipts precede the runtime compatibility corrections; the focused tests above and the current live acceptance below verify the changed paths. A default skip is never runtime acceptance. [Commands and results](../evidence/five-apphost-security-runtime-deferrals/suites/results.json).

| Executable | Passed | Failed | Skipped | Seconds | Receipts |
| --- | ---: | ---: | ---: | ---: | --- |
| Hexalith.Timesheets.ArchitectureTests | 118 | 0 | 0 | 57.767 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.ArchitectureTests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.ArchitectureTests.log.gz) |
| Hexalith.Timesheets.Contracts.Tests | 108 | 0 | 0 | 0.477 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Contracts.Tests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Contracts.Tests.log.gz) |
| Hexalith.Timesheets.Server.Tests | 720 | 0 | 0 | 7.729 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Server.Tests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Server.Tests.log.gz) |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0 | 0.826 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Works.Tests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Works.Tests.log.gz) |
| Hexalith.Timesheets.Projections.Tests | 737 | 0 | 0 | 1037.109 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Projections.Tests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.Projections.Tests.log.gz) |
| Hexalith.Timesheets.IntegrationTests | 934 | 0 | 5 | 971.662 | [XML](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.IntegrationTests.xml.gz), [log](../evidence/five-apphost-security-runtime-deferrals/suites/Hexalith.Timesheets.IntegrationTests.log.gz) |

Incomplete initial suite attempts remain in [their manifest](../evidence/five-apphost-security-runtime-deferrals/suites/incomplete-attempts.json). They are not counted as passing executions.

## Current actual runtime

The exact command in verification metadata exited **0**: **one passed, zero failed/errors/skipped/not-run**, 61.718 s. [Live test log](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/55-Hexalith.Timesheets.IntegrationTests--class.log.gz).

The unmocked journey requires generated signing/app-channel credentials, generated Keycloak login and tenant/domain/permission claims, private actor/read-model/pubsub/control endpoints, negative tenant denial without owner events, persisted record/issue/index/rebuild, confirm race, adjustment, replay denial, actor restart and a fresh post-restart link. Keycloak login and the existing generated development workload authentication are separate qualified paths; this does not claim Keycloak tokens authenticate the gateway.

- [Runtime host](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/runtime-host.json): exact restored NuGet DCP/dashboard 13.6.1 processes and package hashes, one bound daemon/controller tree, direct dashboard `/health` success.
- [Readiness](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/runtime-readiness.json): gateway HTTP 200, healthy required sidecar/actor-placement/store/channel checks, ready actor runtime and registered AggregateActor. Optional missing configstore remains Degraded in this isolated topology.
- [Bootstrap](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/projection-writer-bootstrap.json): normal authenticated platform operator API activates v2 only after a durable exact-owned empty private-store backup and source checks. No deployed/shared marker is changed.
- [Generated security credentials](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/security-runtime.json): accepted in memory on the exact owned HTTPS Keycloak endpoint; values and tokens are absent from receipts.
- [Native restart](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/gateway-process-replacement.json): native PID/start replaced under the same NuGet DCP controller, zero helper arguments and old identity exited.
- [Source before](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/source-before.json) equals [source after](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/source-after.json), including untracked build-input manifests, 12 source/build hashes and both native/DLL pairs for the executed Timesheets host and integration tests.
- [Cleanup](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/cleanup.json) completed; [captured processes](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/recorded-process-exit.json) all exited, the exact security container disappeared after normal Aspire stop, three private infrastructure containers and component copies were removed.
- [Shared preservation](../evidence/five-apphost-security-runtime-deferrals/runtime/run-y2ktjsim/shared-preservation.json) matched Redis DB0/13/14 persistent values, retained TTL values/deadlines and scheduler identity/KV/revision.

The dashboard's direct anonymous health route is mapped by the installed package's exact [endpoint source](https://github.com/microsoft/aspire/blob/3751e615ad660621f2a7aa4c390ef48e08a0eed8/src/Aspire.Dashboard/DashboardEndpointsBuilder.cs#L16-L19) and [route constant](https://github.com/microsoft/aspire/blob/3751e615ad660621f2a7aa4c390ef48e08a0eed8/src/Shared/DashboardUrls.cs#L15-L16). Redirect refusal and direct proxy-free HTTP preserve endpoint ownership. Credential HTTPS retains normal certificate and hostname validation; SSL_CERT_FILE is scoped to this invocation, without trust-store changes.

## Matrix and dispositions

| Approved matrix row | Executed evidence | Disposition |
| --- | --- | --- |
| Security construction | Actual callback/parameter references, unique secrets and malformed-reference driver refusals | Passed; DW-12 done |
| Realm import | Generated placeholders, complete synthetic profile, actual credential login and required claims | Passed; DW-14 done |
| Ordinary run | Actual ordinary graph, existing wait/reference and credential defaults, no new health annotation | Passed |
| Security smoke | Validated fixture, actual Keycloak/resource readiness, outside-fixture and publish refusals | Passed; DW-67 done |
| Admission | Retained foreign-runtime and shared-baseline refusals start no AppHost; unsafe inventory tests pass | Passed |
| Current graph | Actual NuGet processes, dashboard health, stored journey, restart, source identity, cleanup and preservation | Passed; DW-106/DW-166 done |

## Historical attempts and limits

Earlier admission refusals, Docker publication failures, startup and credential diagnostics, delivery conflicts and incomplete JSON run outcomes remain under `runtime/` and in the spec's implementation history. Repeated historical command logs are retained losslessly in the [archive](../evidence/five-apphost-security-runtime-deferrals/historical-command-logs.tar.gz), with an internal SHA256 manifest and [index](../evidence/five-apphost-security-runtime-deferrals/historical-command-logs.json); current/first-success runtime logs and latest compiled-probe logs remain directly readable. In particular, `run-vwrnlvd2` failed shared DB0 preservation; its cause is not attributed to foreign activity. Later matching receipts qualify only their own runs. Temporary exception observation was removed before successful qualification. The first complete success (`run-ob02opgh`, 70.017 s) remains separately retained.

This is local Debug/source evidence. Release/published-package runtime, deployed FR14 authority/inventory/continuity/shared rebuild, consuming-host UI/accessibility and external release approvals remain outside this closure. Historical evidence retains its original scope. Independent review and publication status are recorded separately in the spec and verification metadata.
