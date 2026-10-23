# Five local AppHost boundary deferrals — 2026-10-23

Implementation baseline: `40eb8590153e0585b419f5c29e956354d1725a5b`.
The user approved the [five-item spec](../spec-five-local-apphost-boundary-deferrals.md)
and requested publication to main. Exactly five original ledger entries receive
additive resolution notes; historical evidence remains intact.

| Ledger item | Delivered result | Current evidence |
| --- | --- | --- |
| Omitted internal external-exposure flag | Actual compiled run/publish endpoint annotations report internal `IsExternal=false`, port 8081. | Three real endpoint-model probes cover default, alternate public port and publish mode, requiring explicit unproxied mode for both listeners. This does not establish deployed network isolation. |
| Fixed public port 8080 | Optional run-only `Timesheets:LocalPublicPort` and runner `--public-port` preserve the default and propagate explicit selection to public requests. | Compiled admission checks reject invalid/reserved/control-plane collisions and publish overrides; real journey initialization checks runner/host agreement before actual URL expressions are evaluated. The standalone projection probe uses validated process configuration too. |
| Escaped/computed configuration bypasses | Test-only Roslyn decodes identifiers and constant strings and refuses unsupported expressions within the checked chain. | All three recorded bypasses, ordinary spellings, unsupported expressions, export ownership, immutable internal port, additional binding forms and direct annotation mutation are covered by passing focused tests. |
| Misleading public ingress comment | AppHost and README distinguish local anonymous routes from separately configured deployment ingress. | Current model reports both listeners non-external; no endpoint exposure flag changed. |
| Concurrent local probe admission | Both runtime drivers share a canonical nonblocking advisory lock through admission, execution, cleanup and preservation checks. | Real subprocess contention across checkout/TMPDIR, graceful and abrupt owner exit, unsafe/unavailable lock refusal, both-driver admission and failure cleanup tests pass. |

The lock at `/tmp/hexalith-timesheets-local-runtime.lock` is retained after
release and protects participating tools. Existing foreign-runtime preflight
remains necessary. Configuration-only/model probes acquire no runtime lock and
start no resources. All C# additions remain in individual type files. The only
new package reference is test-only `Microsoft.CodeAnalysis.CSharp`, using the
existing catalog version 5.9.0; no dependency version or gitlink changed.

Verification:

- Matching Debug/source solution restore and warnings-as-errors build passed with zero warnings/errors.
- Root reran the final focused IntegrationTests selection: 24/24 passed, zero failures/errors/skips/not-run, in 4.501 seconds. The earlier pre-review selection had 16 cases; the new eight cases close review gaps.
- Final Python fixture tests passed 112/112. The earlier pre-review run passed 108 cases; its count is superseded for the final source.
- Final real compiled probes passed 62/62: 59 configuration checks and three endpoint models. Default/trimmed control collisions, actual component Redis endpoints with absent/misleading diagnostic keys and explicit proxy modes are covered.
- All probe results report `passed=true`; cleanup reports complete, no components created and zero containers removed.
- All six default executables passed: 2,577 passed, five expected skips, zero failures/errors/not-run (2,582 total). Per-suite results and sanitized logs are retained in `default-suites.json` and its adjacent compressed logs.

The unfiltered run began on the first implementation before review corrections. The final focused rerun covers every changed C# verification path, and the affected project builds, Python and compiled-probe lanes were repeated after those corrections. No full post-review runtime/deployed qualification is inferred from these checks.

The matrix audit maps endpoint qualification to the three compiled model cases;
port validation to the compiled local-port cases and Python selected-listener,
environment and actual journey-expression tests; source fitness to the 24
focused integration cases, including all 20 syntax cases; and contention to the real subprocess admission and
both-driver lifecycle tests. These tests ran rather than being skipped.

Commands use the repository's pinned SDK and the same Debug/source graph:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithEventStoreVersion=3.127.0 -warnaserror -m:1 /nr:false
python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only
```

The final focused command was:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*InternalSurfaceConfigurationSyntaxTests' -class '*MagicLinkConfirmationCapabilityEndpointTests' -noColor
```

Each default suite runs individually as
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project> -noColor`.
Projects are ArchitectureTests, Contracts.Tests, Server.Tests, Projections.Tests,
IntegrationTests and Works.Tests, each prefixed `Hexalith.Timesheets.`.
[Retained current receipts](../evidence/completed-followups/20261023-five-local-apphost-boundaries/) include source hashes, both affected builds, Python/syntax/export logs, compiled probe results, cleanup and the initial failed-model receipt. `verification.json` records the root-focused command/count and review dispositions. Full-suite receipts are retained there too; temporary originals are at `/tmp/timesheets-five-local-apphost-suites-fv2nokmq`.

The first model attempt constructed additional unchanged token wiring and exited
-6. Its exact driver command was the compiled-probe command above; the failing
subprocess added `--guard-probe=model-default`,
`--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=0`, `--TIMESHEETS_LIVE_DAPR_MODEL_ONLY=1`
and `--AppHost:Operation=run`. The sanitized receipt is
`/tmp/timesheets-isolated-dapr-proof-akbdood4/50-Hexalith.Timesheets.AppHost---guard-probemodel-default.log`.
It suppresses the credential-related exception line. Source tracing confirms
`AddHexalithEventStore` already registers the gateway's app-channel parameter,
while the pre-existing AppHost later calls the same generator again; the helper
does not reuse an existing parameter. The endpoint-only probe now returns after
endpoint-affecting wiring and before these unchanged token calls. It qualifies
endpoint annotations, not completion of a runnable graph. No authentication
wiring was changed to obtain a passing model probe.

Read-only runtime inventory found a foreign Tenants AppHost and Dapr applications,
including EventStore on 8080. No foreign process was stopped and no Timesheets
runtime was launched. The current-graph runtime smoke, actual external ingress,
deployed inventory/authority/shared rebuild, and the catalog rebuild race remain
open. Neither the lock nor source fitness proves arbitrary-program or deployed
configuration safety. Launch remains **FAIL**.

Independent review completed across all three configured layers. Thirteen findings were triaged in twelve patch groups; all patches are implemented. The gap reviewer independently repeated the hardcoded-8080 initialization mutant and confirmed it now fails; the edge reviewer verified that explicit proxy-mode qualification closes its original finding. One additional pre-existing duplicate-token composition limitation is tracked separately in the ledger. All six default suites and final focused checks passed. Commitlint, ledger-byte/frozen-intent preservation, source hashes and staged whitespace checks pass. A fresh root-only fetch found zero divergence from origin/main before the authorized commit/push.


| Default executable | Passed | Skipped | Wall seconds |
| --- | ---: | ---: | ---: |
| Hexalith.Timesheets.ArchitectureTests | 109 | 0 | 11.935 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.695 |
| Hexalith.Timesheets.Server.Tests | 693 | 0 | 7.87 |
| Hexalith.Timesheets.Projections.Tests | 697 | 0 | 912.889 |
| Hexalith.Timesheets.IntegrationTests | 910 | 5 | 824.365 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.484 |
