# Owned Development peer-transport qualification — 2026-11-03

Local transport is qualified; Story 3.6 remains in progress and launch remains **FAIL**.
The user's approved spec authorizes the owning EventStore capability and its consumption by the existing fixture. Production enrollment, package upgrades and Git mutations are excluded.

Baselines are Timesheets `53786d00b31459b41f620b5b15f085ec03c6204f` and EventStore `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`, with uncommitted changes bound by the consumer's exact before/after source inventory. [Source IDs](source-ids.json), [commands/results](checks.json), [owner cases and custody](owner-results.json), and [failures/limits](failures-and-limits.json) retain the evidence. [The manifest](manifest.json) hashes this packet's artifact bytes.

## Checks

- Prescribed root Debug/source restore/build: pass, zero build warnings/errors. The final consumer's [build receipt](../20261103-owned-peer-consumer/run-tvhk9h3o/07-dotnet-build.txt) binds the runtime graph.
- Owning Debug/source test-project build: pass. [Build log](owner-build.txt).
- Clean owning `-method *PublicDevelopmentTransport*` lane under `LocalRuntimeAdmission`: **4/4 pass**, exit **0**, **21.257s**. [Console](owner-transport-live.txt), [XML](owner-transport-live.xml).
- Final Python fixture lane: **234/234 pass**, exit **0**, **59.060s**. [Log](python-fixture-regression.txt).
- Individual root executables: Architecture **118/118**, Contracts **108/108**, Server **746/746**, Works **76/76** pass. Integration ends at **780 total / 4 failures / 5 skips**, exit **58**: three pre-existing source-syntax fitness failures plus cancellation of an unrelated 10,000-candidate measurement. Projections ends at **760 total / 1 cancellation failure**, exit **58**, during its unrelated 10,000-owner measurement. Neither broad gate passes. Exact names/results are retained in `failures-and-limits.json`; historical failures remain unchanged.

## Matrix

| Requirement | Actual witness | Result |
| --- | --- | --- |
| Authenticated peer delivery and separate application-channel refusal | `PublicDevelopmentTransportAuthenticatesPeersAndPersistsActorsAcrossNativeRestart` | Pass |
| Real persisted actor state, independent stored readback, native restart and actual scheduler reminder | Same native actor case | Pass |
| Wrong app/namespace and foreign trust; stored marker unchanged | Same native actor case | Pass |
| Missing/expired trust, unavailable Sentry, duplicate/alternate/missing native flags | `PublicDevelopmentTransportRefusesIncompleteIdentityWithoutFallback` | Pass |
| Partial startup; exact owners exit and protected incomplete custody remains | `PublicDevelopmentTransportRetainsPrivateCustodyAfterPartialStartupFailure` | Pass |
| Live-sidecar teardown refusal and successful later cleanup with immutable attempt receipts | Native actor case and retained custody receipts | Pass |
| Actual pinned CLI launched from public Aspire enrollment, resolved secret parameters and modeled native coordinates | `PublicDevelopmentTransportAspireCliLaunchUsesOwnedSecretInputs` | Pass |
| Native/control PID/start, executable, TLS flags, trust equality and receipt tampering | Python `TransportVerificationTests` positive and negative cases | Pass |
| False cleanup success, surviving trust or recorded process, incomplete cleanup | Python `TransportVerificationTests.test_cleanup_refuses_claimed_success_until_trust_and_recorded_processes_are_gone` | Pass |
| Ordinary fixture mode and existing closed ACL/owner/source/custody guards | Full Python fixture suite | Pass |
| Persisted Timesheets effects, retry/single-use, gateway restart/rebuild and deployed acceptance | Consumer stops at metadata | Open |

## Consumer boundary and cleanup

The first [consumer attempt](../20261103-owned-peer-consumer/run-9g26dgfh/cleanup.json) failed before native sidecars because the pinned CLI resolves its runtime installation root as `.dapr/bin/daprd`. A focused actual CLI probe reproduced that missing-owned-executable branch. The owner layout was repaired and qualified before retry. That first attempt's catalog profile is retained because no committed catalog custody exists; its control owners/trust were removed.

The [supported retry](../20261103-owned-peer-consumer/run-tvhk9h3o/README.md) verifies actual native mTLS/control trust, the unchanged eight-route/33-action catalog and serving binding-v2 readbacks, writer bootstrap, matching source inventories and complete exact cleanup. First metadata returns **503 / empty body**, replacing **403 / ERR_DIRECT_INVOKE**. Pinned Dapr source distinguishes raw receiver HTTP forwarding from JSON transport errors. The exact receiver predicate and the supported signed-service/catalog-context producer remain unqualified. Resource-wide workload logs are not attributed to this request. No authority headers or application gates were altered.

The earlier full owner class invocation reported five passed cases but received controlled SIGINT (exit 58); its older plaintext case selects machine-default control endpoints and is excluded from this isolated qualification. Its native processes exited. No shared-state preservation conclusion is drawn from it. The clean four-case owner lane above is the transport gate.

Successful owner/consumer lifecycles removed their temporary trust after native/control exit. The deterministic partial-startup witness intentionally retains private credentials and failed custody for recovery; no credential/config bytes are copied into this packet. Earlier protected failed fixture profiles remain retained. [Final runtime inventory](final-runtime-inventory.json) and consumer cleanup receipts record the final disposition.
