# Owned peer transport consumed; metadata receiver gate remains open

Command: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home PYTHONPATH=scripts python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --public-port 18080 --log-directory _bmad-output/implementation-artifacts/evidence/3-6/20261103-owned-peer-consumer`

Overall exit **1**. The live journey fails **1/1** at its first metadata request, before persisted effects or gateway restart. Story 3.6 remains **in-progress** and launch remains **FAIL**.

- [Native/control transport](route-transport-before.json): both actual checksum-pinned native sidecars use mTLS, the owned Sentry, public/default identity and the isolated TLS placement/scheduler services. Exact control executable/PID/start and trust equality pass.
- [Catalog before](route-catalog-before.json) and [after](route-catalog-after.json): eight owner routes, schema 3, one 33-action gateway technical composite, finite Timesheets, unchanged four/seven grants and binding-v2 serving readbacks remain committed at generation/epoch 1.
- [Writer bootstrap](projection-writer-bootstrap.json): HTTP 200, one attempt, fresh private fixture only.
- [Source before](source-before.json) and [after](source-after.json): equal, including modified/new build inputs and built assets. Baselines: Timesheets `53786d00b31459b41f620b5b15f085ec03c6204f`; EventStore `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`.
- [Metadata outcome](metadata-outcome.json): **HTTP 503, zero body bytes**, versus the former **403 / ERR_DIRECT_INVOKE**. Receiver HTTP boundary attribution is source-derived from pinned [Dapr invocation handling](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/http/directmessaging.go), which forwards application non-2xx responses separately from JSON transport errors. The exact receiver predicate is not isolated. SDK-supported producer/signed-service/catalog context remains unqualified; resource-wide denial log categories are not attributed to this request.
- [Gateway identity](gateway-process-replacement.json): unchanged because the journey never reached restart. No replacement/rebuild proof is inferred.
- [Transport cleanup](route-transport-cleanup.json) and [fixture cleanup](cleanup.json): complete. Exact native/control owners exited; temporary trust, one Redis container, components and this run's profile were removed. Earlier protected failed profiles are untouched.

The owner [four-case qualification and final checks](../../20261103-owned-transport/README.md) are independent local transport evidence. Persisted allowed/denied/retried/single-use effects, restart/rebuild, broad-gate success and deployed acceptance remain open. [Manifest](manifest.json) hashes the retained artifacts; no raw credentials or private configuration is included.
