# Five integration handoffs — 2026-10-22

Scope: the working tree based on Timesheets `e85fad137be152c884e20efbe078fdb42346b0fc`; EventStore `5ce5f1d84b88c808cc8a526b9decf149de977e55`, Builds `359fee24aeca4d9eabed4b425d4cfa2520b2adda`, Works `d60c29569de975bd280098e2e2f37406a30cf705`. No dependency, gitlink, AppHost, or sibling source change.

## Selected closures

1. A supplied gateway no longer suppresses supporting registrations. Options, HTTP-factory services and the SDK's own default command-status Location descriptor are available independently. No temporary provider or replacement Location implementation is introduced. Caller descriptors, options and named routing stay intact; default sidecar configuration applies only to the fallback client. Repeated composition does not duplicate routing. The original support regression failed before the fix with missing ICommandStatusLocationBuilder; review then exposed and corrected caller-configuration and idempotency defects in the initial implementation.
2. Configuration survives both Works reference owners: the adapter's explicit edge and the SDK-added transitive edge in Works.Tests. Actual no-global-mode Debug restore/build found the second owner; both projects now preserve configuration. The isolated regression copies each real consumer project and uses actual SDK reference assignment, including IncludeTransitiveProjectReferences, without packages or a populated sibling checkout. Release selects no external Works project. Explicit version overrides remain intact.
3. The static Rebuild helper remains supported as a pure candidate-content fold. Documentation excludes persisted generation selection, coverage/continuity certification and migration authority. Tests cover empty/null input, duplicate issuance, input order and distinct capability collisions within and across tenants. Existing loader fixtures retain it.
4. The existing shared public-handler response controls are verified and reconciled: no-store and no-referrer on all four malformed-token handler denials, the public opaque-denial matrix and concrete-loader successes (GET 200 and POST 202 after persisted projection delivery). Other captured denials, framework request-binding errors and deployed ingress acceptance are outside this asserted scope.
5. The stale endpoint-comment follow-up is reconciled: the checked-in AppHost comment already names public, matching WithHttpEndpoint(name: "public"). Source inspection and MagicLinkConfirmationCapabilityEndpointTests verify the declared endpoint identity; no AppHost code or topology changed.

The originally selected EventStore-version forwarding item remains open. Review established that NuGet restore drops the proposed per-reference AdditionalProperties even when a custom build traversal forwards it. That metadata and the false closure were removed; the existing conditional catalog-derived override remains required when sibling and consumer defaults differ. No automatic version-alignment claim is made. The endpoint-comment follow-up replaced that selection within the user's delegated five-item batch.

## Verification

Supported Debug/source mode:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Both final supported commands passed. The final solution build had zero warnings/errors (4.36 s).

The additional Configuration-deferral lane uses matching commands without the global mode property:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx -c Debug --no-restore -warnaserror -m:1 /nr:false
```

Restore passed. Initial builds with only the adapter correction failed with HXW0002 and missing serialization types (177 errors), proving the transitive test-project edge remained affected. After preserving configuration on Works.Tests as well, the actual build passed with zero warnings/errors in 5.60 s; a capture-only diagnostic rebuild also passed. The normal explicit-source restore/build was then restored. This additional lane proves its own compilation scope, not that every sibling defaults to source mode without a global switch.

Initial complete suite receipts (before final review corrections): Architecture 106/106, Contracts 92/92, Server 622/622, Works 76/76, Projections 614/614 (547.967 s), Integration 892 passed / five skipped / zero failed (542.673 s). The first integration attempt had put header assertions in a generic denial helper reused by protected management routes; that attempt was stopped after the known test defect, and the corrected complete run passed. Privacy-first capture ordering and protected management behavior remain unchanged.

Each suite is invoked separately using `tests/<Project>/bin/Debug/net10.0/<Project>`; no solution-level dotnet test is used. Five intentional opt-in skips do not qualify runtime acceptance. The isolated assignment probe sets SkipResolvePackageAssets solely to avoid restoring its harmless fixture; production restore/build retains auditing and compilation gates. Its initial scaffolding issues (xUnit cancellation, absent assets) were corrected before the passing focused checks.

## Runtime boundary

`aspire ps --format Json` reported a foreign Tenants AppHost; `dapr list --output json` also showed foreign applications, including eventstore. The local isolated preflight refuses this inventory. No AppHost or foreign resource was started/stopped. Current-graph runtime and deployed acceptance remain unfinished; release remains FAIL. Existing October 20 launch receipts retain their original scope.

## Final results after review corrections

All six final Debug/source executables completed successfully after the review fixes and restoration of the explicit source graph.

| Suite | Passed | Skipped | Seconds |
|---|---:|---:|---:|
| Hexalith.Timesheets.ArchitectureTests | 109 | 0 | 10.621 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.593 |
| Hexalith.Timesheets.Server.Tests | 624 | 0 | 2.012 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.318 |
| Hexalith.Timesheets.Projections.Tests | 614 | 0 | 526.589 |
| Hexalith.Timesheets.IntegrationTests | 892 | 5 | 505.120 |

Total: **2,407 passed, five intentional opt-in skips, zero failed/errors/not-run** (2,412 cases). Both Configuration-reference owners are covered by six isolated SDK-assignment cases. Final Server tests include real caller SDK options/routing, repeated composition, scoped/enumerable overrides, fail-closed/custom Location policy, and same-tenant distinct-capability ambiguity. Full IntegrationTests also verifies the existing endpoint declaration and all selected handler-response header cases.

The blind reviewer confirmed every original material finding addressed; its later grandchild concern was withdrawn after the actual transitive reference owner was identified and covered. Exactly five original ledger entries gained resolution annotations; no existing evidence or unrelated entry was removed. The EventStore-version forwarding entry remains open, with no false automatic-propagation claim. `git diff --check` and the Conventional Commit message check passed. No submodule pointer or MCP configuration was staged.
