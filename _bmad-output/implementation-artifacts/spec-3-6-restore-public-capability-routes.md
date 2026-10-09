---
title: 'Restore Story 3.6 public capability HTTP routes under EventStore security'
type: 'bugfix'
created: '2026-10-09'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3e1ca73ba51f0c91467b74ac3dc85f88b6526b7e'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The EventStore security update makes four public magic-link routes and `/metadata/timesheets` return unexpected 401 responses. It also makes the existing internal HTTP fixtures submit projection requests without the credentials now required by the SDK.

**Approach:** Add a narrowly scoped public-endpoint opt-in to the EventStore domain-service SDK and its startup inventory. Apply it only to Timesheets' four capability routes and metadata. Keep capability validation in the server-side loader and command service; authenticate protected internal HTTP fixture requests with the configured application-channel token and signed workload assertion. Restore public-port 404 refusal for internal routes and retain all Story 3.6 acceptance gaps and live-topology limits.

## Boundaries & Constraints

**Always:** Canonical EventStore and Dapr sidecar routes require their existing policies even if tagged as public. A bare `AllowAnonymous` remains an inventory violation. Public capability requests must still resolve and authorize their capability on the server. Protected projection and management fixture requests carry valid workload credentials; anonymous management requests remain denied.

**Never:** Weaken the workload fallback, make issuance/revocation/expiry routes public, bypass the internal-port guard, mutate projection state directly, add host topology, or claim Story 3.6 complete while its remaining acceptance gaps are open.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Public capability | Four GET/POST magic-link routes | Valid state reaches existing handlers; invalid state receives opaque 403 | No unexpected 401 |
| Public metadata | GET `/metadata/timesheets` | 200 without workload credentials | No unexpected 401 |
| Internal delivery | Authenticated POST `/project/v2` on allowed internal port | Projection persists index/catalog through mapped SDK route | Missing/forged credentials denied |
| Public internal route | POST canonical route on public port | 404 before authentication | No route disclosure |
| SDK inventory | Public opt-in, bare anonymous, or tagged canonical/sidecar endpoint | Only eligible explicitly tagged endpoints pass | Fail startup for invalid metadata |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs` — inventory currently rejects every non-probe anonymous endpoint; preserve canonical and sidecar policy checks.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs` — public SDK endpoint convention belongs beside existing policy extensions.
- `references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` — existing positive and negative route-inventory cases.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` — four public capability handlers; issuance management stays under fallback authorization.
- `src/Hexalith.Timesheets/Program.cs` — route mapping, public metadata, and guard/middleware order.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — internal projection dispatch and privacy journeys; inject signed workload credentials for `/project/v2`.
- `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` — public metadata/capability reachability and protected public-port refusal.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` — current-readiness fitness assertions must inspect current rows rather than historical blocked or zero-failure text.
- `_bmad-output/implementation-artifacts/deferred-work.md` and `docs/launch-readiness.md` — existing open Story 3.6 and live-topology evidence; amend only with observed outcomes.

## Tasks & Acceptance

**Execution:**
- [x] Add an EventStore per-route public opt-in and inventory tests for permitted metadata/capability routes and rejected bare, canonical, sidecar, mismatched, and broad opt-ins.
- [x] Apply the contract to five Timesheets endpoints; place the internal-surface guard before authorization while keeping authorization before endpoint execution.
- [x] Configure the integration fixture JWT and app-channel contract and sign protected internal projection and management requests.
- [x] Run focused SDK tests, the four public routes and metadata checks, both full requested HTTP test classes, and the four scratch-root privacy cases. Record exact live-topology result separately.

**Acceptance Criteria:**
- Given an anonymous public request, when each magic-link route or metadata is called, then it reaches the designated handler without an unexpected 401 while invalid capabilities remain opaque.
- Given an internal projection request with valid configured credentials, when delivered through `/project/v2`, then the test read models contain the projected index and catalog; absent credentials are denied.
- Given public-port calls to canonical EventStore routes, when routing runs, then they return 404; a public marker cannot weaken canonical or sidecar policy.
- Given a scratch content root containing privacy vocabulary, when all four specified HTTP privacy cases run, then they pass without disclosing protected values.

## Implementation Notes

The user explicitly requested implementation, so this spec records that authorized scope. The separate Story 3.6 acceptance and release gates remain open.

The requested starting commit `7f224dc92888762ece55b32690e4ee99731b2972` is an ancestor of the clean starting head `3e1ca73ba51f0c91467b74ac3dc85f88b6526b7e`; its later EventStore pointer update exposed the 401 regression. The SDK change was committed in its owning repository as `f615dd5d3266b84d119609f55550ebc9d0c6f770` before the Timesheets pointer update.

The SDK inventory accepts an explicit exact-route marker only on literal single-method GET/POST endpoints and rejects markers on canonical, sidecar, templated, mismatched, or group routes. Timesheets marks four capability routes and metadata. The test fixture signs protected projection and management calls; public requests carry no workload credentials. Test-only claim transformation preserves administrator context after workload authentication. The current-readiness fitness tests were adjusted to inspect current evidence after the dated HTTP result changed.

SDK public-route/startup checks passed 24/24. The source-reference Debug solution build passed with zero warnings/errors. Both requested HTTP classes passed 482/482; the scratch-root privacy selection passed 4/4 after the final fixture change. Six individual Timesheets lanes total 1415 tests: 1410 pass, one pre-existing Aspire package-verdict failure, four intentional skips. The ArchitectureTests rerun after readiness-fitness corrections returned 55/56, with only that package-verdict failure. The full IntegrationTests lane reran after review patches: 557 pass and four intentional skips.

Explicit AppHost start exited 0, `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18, and `aspire stop` exited 0. Timesheets logs report missing `APP_API_TOKEN` and JWT Authority/SigningKey. This does not establish a passing live topology or durable atomic magic-link use; Story 3.6 and release remain open.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| EventStore gitlink is not committed | medium | patch | The rendered bmad-build workflow requires a local commit, and a clean Timesheets checkout needs a committed SDK pointer. The SDK was committed in its owning repository first; the Timesheets commit records that gitlink. |
| Management success tests use test-only tenant and actor claims | medium | defer | `MagicLinkFixtureClaimsTransformation` supplies claims absent from the production workload principal. The current fail-closed server guard remains unchanged, so the test proves authenticated fixture behavior but cannot prove live management issuance. |
| Management fallback accepts any valid workload operation | medium | defer | `CreateAnyWorkloadPolicy` requires an operation claim but not a management-specific value. This predates the route opt-in; issue/revoke still run server-side access checks, but explicit management policy deserves separate work. |
| Expire lacks the issue/revoke access guard | medium | defer | `MagicLinkConfirmationCapabilityCommandService.Expire` calls `HandleExpire` directly. It remains behind workload fallback and produces only an in-memory domain result in this host; future durable wiring must resolve this authorization gap. |
| Public HTTP tests inject a `TestAuth` principal | medium | patch | `InternalSurfaceGuardTests` now sends genuinely anonymous requests to all four capability routes and verifies opaque 403 responses. |
| Public token responses lack no-store and referrer controls | medium | defer | The existing routes carry `?t=` and do not set cache or referrer headers. This predates the SDK compatibility fix and needs response-policy tests across valid and denied paths. |
| Inventory permits duplicate public method/path mappings | low | reject | A host could explicitly map two identical noncanonical routes, but the current host does not. Detecting all effective routing ambiguities adds startup machinery beyond this route contract. |
| Mixed authorization and duplicate-marker rejection untested | low | patch | Two new SDK inventory cases cover both existing rejection branches; the focused SDK selection passed 24/24. |
| Independent channel-token and operation checks untested | medium | patch | The internal projection test now uses a valid assertion without the channel token and a valid assertion with the wrong operation; both are refused. |
| Source fitness test weakened its EventStore prohibition | low | patch | The test now removes only the intended route-contract using and extension name before applying its original broad `EventStore` prohibition. |
| Readiness control prose says SDK routes have no policy | low | patch | The control description now names exact canonical workload policies, the fallback, the public opt-in, and the port guard. |
| Verification-gap reviewer: production management identity unproven | medium | defer | The workload evaluator creates `NameIdentifier=workload:<caller>` and no tenant or Party claim; the fixture transformation supplies them. This is the same production identity gap as the second row, retained separately to record the independent finding. |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -m:1` — SDK compile.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx -m:1` — source-reference consumer compile.
- Run the built xUnit v3 executables with `-class` for the two requested classes and the exact scratch-root `-method` invocation from `tests/3-6-test-summary.md` under the implementation artifacts.
