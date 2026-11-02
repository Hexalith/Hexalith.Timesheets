# Story 3.6 authorized route-catalog attempt — 2026-11-02

The [updated spec](../../../spec-3-6-resume-private-route-catalog-qualification.md) authorizes ordinary stops of blocking local AppHosts and this fresh attempt. The canonical-lock inventory and supported preflight found empty Aspire/Dapr inventories and available required ports, including public port 18080. No stop was necessary. Exact matching Debug/source [restore](debug-source-restore.json) and [warnings-as-errors build](debug-source-build.json) exited **0**, in **4.357s** and **27.460s**; the build reports **zero warnings/errors**.

The [single supported invocation](supported-runner.json) was:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --public-port 18080 --log-directory /home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/3-6/20261102t184333z-authorized-route-catalog/runner
```

It ran **2026-11-02T18:44:34.740722+00:00 → 18:46:14.436133+00:00**, **99.699s**, exit **2**. [Aspire startup](runner/run-jsy1mlus/46-aspire-start.log.gz) exited **2** because the AppHost rejected the selection at `Program.cs:78`: **Development route catalog requires a complete isolated Development fixture selection.** The runner had not selected a Development host environment. It never reached SDK catalog readback, receiver/workload observations, fixture protocol activation or the persisted journey. Shared Redis was neither read nor locked; no fixture domain write or deployed operation occurred.

A [configuration-only compiled reproduction](environment-startup-reproduction.json), which exits before constructing resources, reproduced the guard refusal with environment selection unset (exit **-6**) and validated the identical private selection with Development explicit (exit **0**). After recording the defect and repair plan in the spec, the [runner repair](source-repair.json) pins `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT` and `ASPIRE_ENVIRONMENT` to Development for catalog mode and discards conflicting inherited aliases. Ordinary mode, AppHost authorization and custody guards are unchanged. The Python suite passes **217/217**, including inherited-environment/ordinary-mode checks and the compiled AppHost policy/owner mutation regression using the runner-derived environment. No second live invocation followed this repair.

The runner removed all **three** exact owned disposable containers, verified their absence and observed no remaining fixture processes. Its original [cleanup receipt](runner/run-jsy1mlus/cleanup.json) remains **incomplete**: no complete committed SDK custody exists, so the protected private owner profile is intentionally retained. The [cleanup disposition](cleanup-disposition.json) verifies its fixture identity and 0700 directory/0600 input modes without copying private inputs into this packet. This is no complete owned-cleanup or runtime qualification claim.

Current verification:

| Gate | Observed result |
| --- | --- |
| Debug/source restore and warnings-as-errors build | Exit 0; zero build warnings/errors |
| Python fixture regressions | 217 passed; exit 0; 94.004s test duration |
| Architecture | 118 total, 117 passed, 1 failed; exit 1; 41.892s |
| Contracts | 108 passed; exit 0; 1.436s |
| Server | 746 passed; exit 0; 9.860s |
| Works | 76 passed; exit 0; 0.601s |
| Projections | 787 total, 786 passed, 1 failed; exit 1; 774.458s |
| Integration | 856 total, 850 passed, 1 canceled failure, 5 declared skips; exit 58; 1,225.609s |
| Focused bootstrap latency after broad test processes exited | 2 passed; exit 0; 6.166s; original broad failure retained |
| Focused catalog HTTP fallback | 15 passed; exit 0; 10.661s |
| Focused LaunchReadiness after record updates | 16 passed; exit 0; 0.350s |

The Architecture failure is `DiagnosticsPrivacyTests.SourceLoggingDoesNotIncludeSensitivePayloadOrIdentifierMaterial`: its substring scan treats the AppHost's `LogicalSecretName` validation line as logging and rejects `secret`. The [source comparison](architecture-failure-source-comparison.json) proves both files are unchanged from this attempt's baseline. The failure is retained without expanding this repair's scope. Live/performance opt-ins are unset for the six default executable runs; their default skips cannot establish live acceptance.

The Projections failure is `MagicLinkBootstrapLatencyTests.DelayedCombinedInventoryPersistsV7WithinGatewayAttemptBudget(streamed: True)`: its unchanged budget canceled the streamed stage at `BootstrapObservingReadModelStore.cs:85` / test line 68 through the EventStore shared projection coordinator. The original broad failure remains recorded; no cause is inferred from timing alone.

Integration was gracefully interrupted after exceeding the selected 1,200s wait limit. The [cancellation receipt](integration-bounded-cancellation.json) verifies the exact owned child and records actual SIGINT delivery at **1,225.950s** of native process lifetime. xUnit reported a cancellation failure in `KeyedHttpRetryReconcilesOpaqueCommittedOriginalEffects(adjust: False)`, not a passing full suite. No broad run was repeated. The separately recorded [focused latency rerun](focused-bootstrap-latency.json) passes both theory cases with the original budget, after the other test processes exited; it neither erases the broad failure nor identifies its cause. The [focused catalog fallback](focused-catalog-http.json) passes 15 in-process cases for owner enrollment, the eight-route receiver inventory and unchanged state on catalog admission refusal. These are configuration/in-process checks only.

[Full source provenance](source-provenance.json) retains Timesheets `2c25a83ef71d066c5ea124922ed251aad2dd8ddc` and all 13 clean root-declared dependency revisions with matching gitlinks. The runner's [pre-start source snapshot](runner/run-jsy1mlus/source-before.json.gz) and [dependency graph](runner/run-jsy1mlus/dependency-graph.json.gz) bind the failed attempt's inputs/assets; no post-lifecycle source-consistency proof exists. [Artifact retention](artifact-retention.json) records lossless compression and original hashes for sanitized runner diagnostics and source snapshots. Earlier packets remain unchanged.

Actual catalog receivers/policies, valid/forged/undeclared/keyed requests and single-use behavior, exact EventStore/Dapr stored effects, restart/rebuild continuity, live source consistency and complete owned cleanup remain unqualified. Deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and the resume spec remain **in-progress**; launch remains **FAIL**.

[Qualification](qualification.json) distinguishes observed failures, focused results and unobserved gates. [Static verification](verification.json) and [packet hashes](SHA256SUMS) cover artifact integrity, historical evidence, source revisions, frozen intent and local links. No staging, commit, branch, push or dependency edit occurred.
