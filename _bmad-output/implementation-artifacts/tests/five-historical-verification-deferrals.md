# Five historical verification deferrals

Date: 2026-10-25. Documentation reconciliation against Timesheets
`eaa24c685864c51e2351af2c3e1cdfb31b491dcd` and its unchanged EventStore checkout
`dcd3dce42b67a305b608f03c4dab2b812791abae`. The tested worktree changes only this
report, its build spec and five additive ledger notes.

## Dispositions

| Original ledger entry | Current evidence | Disposition |
| --- | --- | --- |
| September 13: stream paging has no page/event cap | `MagicLinkStateLoadingBudget` bounds raw pages/events/bytes and awaited reads per invocation. `InvocationAllowanceChargesRepeatedStreamsAtExactAndOverBoundaries` covers pages, events, bytes and duplicates; `AdvancingGrowingHistoryStopsAtTheSharedPageAllowance` refuses after exactly three workload requests. `CumulativeSuccessfulReadsShareOneInvocationDeadline`, `NonCooperativeStreamReadIsBoundedAndCallerCancellationPropagates` and `NonCooperativeReadModelsShareInvocationDeadlineAndPropagateCallerCancellation` verify deadline/cancellation behavior. | Duplicate defect report closed against the canonical resolved total-read-budget entry and `five-magic-link-safety-deferrals.md`. |
| September 14: registration tests mutate process-wide Dapr variables and capture a token once | `RuntimeRegistrationTests` uses in-memory `IConfiguration`. `GatewayConfigurationReloadUpdatesExistingCredentialsAndNewClientEndpoints` verifies rotated/removed credentials on an existing client, a changed endpoint on a new client and unchanged process values. `GatewayUsesConfigurationDaprFallbacksWithoutProcessMutation` covers fallback reload. No `SetEnvironmentVariable` call remains in the class. | Duplicate defect report closed against the canonical resolved gateway-options work and `five-magic-link-runtime-deferrals.md`. |
| September 15 rerun: unknown authority suffixes are silently ignored | The loader's complete capability/owner event sets reject unrecognized types. Both rows of `UnknownAuthoritySuffixDeniesWhileKnownAliasesAndRejectionNoOpsSurvive` establish a valid baseline then deny an unknown capability/owner suffix without partial authority. | Duplicate defect report closed against the canonical resolved unknown-event entry and `five-magic-link-safety-deferrals.md`. |
| September 13: scripted HTTP fixtures were described as live-host acceptance | The current launch record's durable-submit, persisted-handler and live-acceptance rows explicitly separate in-process HTTP fixtures from historical local persistence and unfinished deployed/current-graph acceptance. `MagicLinkConfirmationHttpBoundaryTests` still uses `ScriptedAccessGuard` and `ScriptedEventStoreGateway`. | Historical overclaim corrected; production tenant/Party identity and deployed runtime acceptance remain OPEN under the existing entries. |
| September 12: SDK/CLI-bundle resolution lacked runtime coverage | The entry's existing October 18 update already removes CLI-bundle resolution from scope. The AppHost pins SDK 13.6.1 and sets `AspireUseCliBundle=false`. `AppHostRetainsNugetOrchestrationAndSuppressesOnlyAspire010` checks the XML setting and diagnostic suppression. Fresh restore assets independently retain `Aspire.Hosting.Orchestration.linux-x64/13.6.1` and `Aspire.Dashboard.Sdk.linux-x64/13.6.1`; see the assets inspection below. | Historical CLI-bundle diagnosis superseded; automated AppHost smoke and current NuGet-orchestration startup acceptance remain OPEN. XML checks, restored package presence and compilation establish no resource startup. |

These are five historical ledger dispositions: three duplicate defect closures
and two corrected/superseded evidence claims. No new runtime fix, package update,
deployment, migration or acceptance waiver is supplied. The original text is
preserved; removing exactly the five new dated notes reproduces the baseline
ledger byte-for-byte.

## Retained obligations

- The open ledger entry **Supply trusted tenant and actor identity to production
  magic-link management routes** owns production identity. Current kernel
  authorization, tenant and contributor defaults remain fail-closed; scripted
  fixture access does not provide a production implementation. Trusted management
  claims alone do not supply `ITimesheetsTenantAccessValidator` or
  `IContributorPartyValidator`: production validator wiring and verification are
  still required for the original valid-link acceptance obligation, alongside
  the production access/authority binding already retained in launch readiness.
- The open **Add automated AppHost runtime smoke evidence for the resolved
  Aspire and Keycloak package graph** and **Prove that the Aspire.AppHost.Sdk
  13.6.1 AppHost with NuGet-backed orchestration starts DCP, the dashboard, and
  its resources** entries own startup qualification. This run starts no AppHost,
  DCP, dashboard, Keycloak or Dapr resource.
- The lagging-replica/stale-prefix and separate-snapshot consistency questions
  remain open. Source routing through an aggregate actor does not establish
  backend failover or linearizability guarantees.
- Shared-shape layering, catalog rebuild concurrency, deployment authority,
  certified inventory/continuity and deployed shared-rebuild acceptance are
  unchanged. The overall launch verdict remains **FAIL**.

## Verification

Use SDK 10.0.402, matching Debug/source-reference restore/build, with the evaluated
EventStore version 3.127.0 and no caller version workaround:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class '*EventStoreMagicLinkConfirmationCapabilityStateLoaderTests' -class '*RuntimeRegistrationTests' -xml /tmp/five-historical-verification-server.xml
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*ScaffoldGovernanceTests' -xml /tmp/five-historical-verification-architecture.xml
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*ValidMagicLinkGetDisplaysAndPostDenialsMatchInvalidDenials' -method '*ConcreteLoaderCommitsBothActionsAfterProjectionDelivery' -xml /tmp/five-historical-verification-http.xml
```

Inspect the generated AppHost restore assets separately from the XML fitness
test. This command returned the two package identities recorded in the table:

```bash
python3 - <<'PY'
import json
from pathlib import Path
assets = json.loads(Path('src/Hexalith.Timesheets.AppHost/obj/project.assets.json').read_text())
for identity, metadata in assets['libraries'].items():
    if identity.lower().startswith(('aspire.hosting.orchestration', 'aspire.dashboard.sdk')):
        print(identity, metadata['type'])
PY
```

Restore passed. Solution build passed with **zero warnings/errors** in 56.05 s.
The three freshly built executable selections passed **204/204**, with zero
failures, errors, skips or not-run cases:

| Selection | Passed / total | Seconds |
| --- | ---: | ---: |
| Server loader and registration classes | 174 / 174 | 7.459 |
| Architecture scaffold-governance class | 28 / 28 | 1.486 |
| Two composed HTTP methods | 2 / 2 | 1.283 |

All three executions started at 06:19:11 UTC on 2026-10-25. The named unknown
suffix theory contributed both rows, the allowance theory all four dimensions,
and the growing-history case one row; these were included in the 174 Server
passes. Local raw logs and XML use the `/tmp/five-historical-verification-`
prefix and are supplemental to the commands and results retained here.
Focused checks are sufficient for this documentation-only change; no new
full-suite, live topology, performance or deployed result is claimed.
