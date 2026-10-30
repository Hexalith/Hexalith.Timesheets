# Hexalith Timesheets

Hexalith Timesheets is a domain module for trusted time capture, approval, confirmation, reporting, and finance export.

Shared read-model DTOs and addresses live in `Contracts`. The `ReadModels` library holds Timesheets-specific SDK index helpers and stored payload wrappers, and is consumed by both `Server` and `Projections`. Projections has no Server dependency. Existing CLR namespaces are retained through Server type forwarding; persisted payload identities and state keys remain compatible. See the [boundary decision record](docs/boundary-decision-record.md).

## Local Codex Hooks

For BMAD Loop orchestration with Codex, use Codex CLI 0.139 or newer and install `bmad-loop` using its [installation guide](https://github.com/bmad-code-org/bmad-loop/blob/main/docs/setup-guide.md). Run this command from each checkout root:

```bash
bmad-loop init --cli codex --no-skills
```

This generates `.codex/hooks.json` with the absolute path to your installed relay. That file is local and ignored by Git; `.codex/config.toml` remains shared. Repeat setup after moving the `bmad-loop` installation or in a fresh checkout.

Select Codex in the existing `[adapter]` section of local `.bmad-loop/policy.toml`; `init --cli codex` registers hooks and leaves the template's default adapter in place:

```toml
[adapter]
name = "codex"
```

Before starting an unattended loop, run `codex` once in the checkout and review and accept its workspace-trust and hook-trust prompts. Spawned sessions cannot answer those first-run dialogs. See BMAD's [first-run setup notes](https://github.com/bmad-code-org/bmad-loop/blob/main/docs/setup-guide.md) for details.

Existing checkouts upgrading from the tracked hook file should run setup again; remove any old hook that calls the deleted `bmad-story-automator` before doing so. Normal interactive Codex use does not require BMAD Loop hooks.

## Build and Test

Use the pinned .NET SDK `10.0.402` (`rollForward: latestPatch`), with a writable CLI home in restricted environments:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Use the same source-reference mode for restore and build. CI/CD uses Release/published packages; configuration-free restore defaults to that graph. Do not reuse assets between dependency modes. Timesheets selects published EventStore `3.128.4` for its gateway and certified abort-repair APIs. Root package overrides honor the published source graph's Aspire hosting, OpenTelemetry and MCP dependency floors; the AppHost SDK and Aspire overrides align with the imported catalog at `13.6.4`. Works source references receive the evaluated consuming EventStore version, including explicit caller overrides, on direct and SDK-added transitive edges. No version workaround is required. [Build and hosting verification](_bmad-output/implementation-artifacts/tests/five-build-and-hosting-deferrals.md) records differing-default and explicit-override regressions.

After building, run each test project individually. `global.json` selects Microsoft.Testing.Platform; use `--project` and the same configuration and dependency mode as the build:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test --project tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-build -p:UseHexalithProjectReferences=true
```

Repeat for Contracts.Tests, Server.Tests, Projections.Tests, IntegrationTests, and Works.Tests using their respective project paths. The built xUnit v3 executables are also supported and run the same suites directly:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests
```

After the Debug/source build, run the compiled AppHost configuration probes as part of local verification:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only
```

Configuration probes exit before resource construction; model probes inspect the completed endpoint annotations before Build/Run. Both require no Docker, Dapr, Aspire CLI, runtime admission lock, or idle runtime. CI runs them in its blocking `apphost-configuration` job after a fresh Release/package AppHost build, passing `--configuration Release`. Package builds must reject the private compiled-gateway adapter; local Debug/source builds verify its valid selection as well. This lane checks configuration admission and endpoint names, ports, `IsExternal` and explicit proxy modes (`IsExplicitlyProxied=false` for both listeners); it supplies no running-topology or deployed acceptance evidence.

`src/Hexalith.Timesheets.AppHost` declares the local anonymous public routes on port `8080`, the guarded internal listener on `8081`, and the EventStore gateway on `8082`. Both Timesheets endpoint annotations currently have `IsExternal=false`; anonymous route metadata does not configure deployment ingress, and this annotation alone does not prove deployed network isolation. Publish-mode exposure flags remain unchanged.

For local runs, set `Timesheets__LocalPublicPort` (configuration key `Timesheets:LocalPublicPort`) to an integer from `1024` through `65535`, excluding the internal, gateway, sidecar and reserved infrastructure listener ports. The default remains `8080`. Both the default and an override are checked against normalized control addresses and the Redis endpoint in validated fixture components; the optional Redis diagnostic environment key does not define that endpoint. Publish mode rejects any override. The isolated journey accepts the same selection explicitly:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --public-port 18080
```

The runner checks the selected port, excludes it from every private infrastructure port allocation, and passes it to the AppHost and all public journey requests. The standalone projection probe derives its public origin from the selected Timesheets process configuration. Runtime child environments discard probe-only settings and their provider aliases. Runtime runner and infrastructure-backed guard checks share a nonblocking OS advisory lock at `/tmp/hexalith-timesheets-local-runtime.lock`, independent of checkout and `TMPDIR`. They retain it through admission, execution, owned cleanup and shared-service preservation, then release it without deleting the lock file. Contention, unavailable locking or an unsafe lock path causes an immediate refusal without holder details. This serializes cooperating tools only; the existing idle-runtime prerequisite still applies and foreign apps are never stopped.

The test-only Roslyn fitness check decodes identifiers and constant strings in the two checked entry points. It requires the immutable internal port `8081`, its export on the Timesheets resource, and the canonical host options binding. It rejects additional qualified or post-configuration bindings, unsupported checked configuration expressions, and direct Timesheets annotation mutations. These are bounded source-wiring checks; they do not establish arbitrary C# program semantics.

The capture and governance command performance lane (NFR10 command-acknowledgement evidence) is **skipped by default** and opted in with `TIMESHEETS_PERF=1`, so it never enters the fast baseline. Set the variable on the same executable invocation:

```bash
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.CaptureAndGovernanceCommandPerformanceLaneTests"
```

See `docs/performance-evidence.md` for the measured p95 numbers and the NFR10 verdict.

The report, export, and dashboard query performance lane (NFR11 evidence) shares the same `TIMESHEETS_PERF=1` opt-in and is **skipped by default**. It records p95 over seeded in-process report, ledger, export, preview, dashboard, and Works planned-effort paths:

```bash
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class "Hexalith.Timesheets.IntegrationTests.ReportExportDashboardQueryPerformanceLaneTests"
```

See `docs/performance-evidence.md` for the measured p95 numbers, the NFR11 verdict, and the EventStore-backed wire-path waiver.

See `docs/launch-readiness.md` for the final story-complete versus launch-complete classification record and release-gate decision. The current overall release decision is `FAIL`: deployed magic-link authority, inventory/continuity, and shared-rebuild acceptance remain unfinished. The isolated local persisted journey passed on 2026-10-18 on the earlier AppHost graph; current NuGet-orchestration runtime qualification remains open. Package currency separately remains `CONCERNS`; other integrations retain explicit waivers or post-v1 decisions.

The default gateway binds `Timesheets:EventStoreGateway` through host configuration. `Endpoint` must be an HTTP/HTTPS origin without credentials, a path, query, or fragment; invalid endpoints fall back to `HttpPort`, then `http://localhost:3500`. `AppId` defaults to `eventstore`; `ApiToken` is optional. `DAPR_HTTP_ENDPOINT`, `DAPR_HTTP_PORT`, and `DAPR_API_TOKEN` configuration keys supply fallbacks (the composed host's normal environment provider includes Dapr sidecar settings). Configuration reload changes the endpoint for newly resolved clients and changes the target and credential on the next send by existing clients. Set `ApiToken` to an empty string to remove a configured fallback credential. Caller-provided gateways and transports retain their own configuration.

Standalone `ServiceCollection` consumers should register their caller-owned `IConfiguration` before `AddTimesheetsServerKernel()`. Include an environment provider when the `DAPR_*` fallbacks come from process environment settings:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
services.AddSingleton<IConfiguration>(configuration);
services.AddTimesheetsServerKernel();
```

Without a supplied configuration, standalone composition uses an empty configuration and the approved defaults: `http://localhost:3500`, target `eventstore`, and no API token.

As of 2026-10-10, valid magic-link GET displays return `200` and valid confirm and adjust POST routes return `202` in the in-process HTTP fixture after EventStore reports `Completed` and the exact stored batch is read back. Invalid and uncertain outcomes return the shared opaque `403`. Status polling defaults to a five-second deadline with 100-ms intervals and is configurable through `Timesheets:MagicLinks:SubmissionPolling`. Stored-event reads have a separate two-second `ReadbackTimeout` (positive and at most thirty seconds), bounded by the remaining submission deadline, including non-cooperative transports; caller cancellation propagates. Set `Timeout`, `PollInterval`, and `ReadbackTimeout` under that section. Actor persistence tests prove rejection replay and valid work after recreation. These fixtures do not prove deployed Dapr persistence. The loader retains only sanitized status for well-formed non-Fresh catalogs. Deployed capability and Time Entry histories could not be inventoried with the available access; Story 3.6 remains in progress.

For confirm and adjust submissions, clients may send one optional `Idempotency-Key` header value. Retain the same key, token and complete request body across retries after an uncertain outcome. Keys must contain non-whitespace content, be at most 256 characters and contain no control characters; multiple values are rejected. An authorized retry returns `202` only after verifying the original command receipt and stored effects, without applying another effect. Changed keys, tokens or request content cannot inherit the original success; unresolved outcomes retain the opaque `403`. Omitting the header preserves the existing single-use behavior and cannot reconcile a committed use through this retry contract.

As of 2026-10-23, authority replay admits only the complete known event set for each capability or Time Entry stream, including stored/domain aliases and `MagicLinkCommitRejected` no-ops. Unknown event types deny resolution without publishing partial state. Each loader invocation shares raw history cost allowances across all stream reads, including repeated owner reads: 64 pages, 10,000 events and 16 MiB payload by default. One five-second invocation deadline covers candidate-index/coordinator/tree reads, history replay and catalog reads; standalone catalog calls receive their own deadline without charging history pages. Configure `MaxPages`, `MaxEvents`, `MaxPayloadBytes`, and `Timeout` under `Timesheets:MagicLinks:StateLoading`; startup validation requires positive values capped at 256 pages, 100,000 events, 128 MiB, and thirty seconds. Raw duplicates count before normalization, and asynchronous reads remain bounded when a transport ignores cancellation; caller cancellation propagates and late completion cannot publish a result.

All four public confirm/adjust handlers retain the opaque `403` for invalid links. Unexpected handler faults return the same fixed opaque `500` ProblemDetails with `Cache-Control: no-store` and `Referrer-Policy: no-referrer`. Internal Error event 37002 contains only a fixed operation and platform trace identifier; exception details, tokens, domain identifiers, and payloads are excluded. Diagnostic provider failures do not alter either response, and genuine request cancellation propagates. New `ExistingTimeEntry` issuance is rejected before token generation and at durable validation; historical events and enum values remain readable, with existing use denial preserved. This explicitly narrows Story 3.2 issuance scope without changing approval or correction behavior.

An interrupted v6 migration revalidates selected format/root and selected/direct single-root metadata inside operation-owned abort handling. Invalid metadata refuses promotion; abort releases that operation's fence only when its original journal can drain safely, retaining the selected generation. Fresh malformed sources reserve no fence, foreign operation/inventory attempts cannot abort it, and pre-abort transient reads or caller cancellation remain retryable. Fence-release evidence covers empty journals and healthy pending deliveries, including interruption after committed private-stage deletion. Each migration now persists an immutable scope/operation/inventory/epoch/source-generation witness after SDK Begin and before abort handling. A fresh authorized `RecoverAbortAsync` resumes an interrupted Aborting operation without promoting its private stage; the migration-only call refuses success after completing abort cleanup. Missing, foreign or corrupt witnesses refuse Timesheets source, witness and fence writes; SDK status access may reconcile its own control/discovery indexes. Recovery rechecks epoch/source/operation/phase after reading the witness. Operation IDs must remain unique and maintenance must be serialized: the SDK abort API cannot atomically bind an expected epoch/inventory after that last check. Witness initialization failure leaves Building retryable, and Aborting never creates a witness. Pending deliveries against corrupt selected metadata require independently certified original-layout and complete unchanged-source evidence. Certification defaults denied, including the original three-argument constructor and a missing verifier. With an exact server-side certificate, recovery uses the published SDK repair API to restore only proven original metadata absence; the immutable SDK receipt retains all original corrupt JSON and ETags, including JSON-null and inherited copy-on-write provenance. Interruption resumes the identical durable repair binding, then drains the original journal without selecting v7. It never discards deliveries or derives certification from measured membership or a fingerprint alone. No live migration was executed. [Safety verification](_bmad-output/implementation-artifacts/tests/five-magic-link-safety-deferrals.md) retains the earlier evidence; [recovery and verification evidence](_bmad-output/implementation-artifacts/tests/five-recovery-and-verification-deferrals.md) records these closures. Launch remains `FAIL` and deployed acceptance remains unfinished.

The current abort-repair source digest is a compatibility contract with the independently supplied certificate verifier. Observe all 256 logical shard keys, including absent shards, and every candidate and directory page referenced by a valid v6 manifest. Each guard has fields in this declaration order: `LogicalKey`, `ExpectedETag`, `ContentDigest`, `SourceGeneration`, `PointerETag`. Preserve the effective physical generation and pointer ETag. Absence has an empty ETag and a null content digest. Present content, including literal JSON null, has an uppercase hexadecimal SHA256 over the UTF-8 bytes of `JsonNode.ToJsonString()` (use the literal `null` for a present JSON-null value); preserve its observed ETag. Sort the complete guard array by `LogicalKey` with ordinal comparison, serialize it with the default `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes` options (PascalCase property names, declaration order and explicit null fields, without a Web naming policy), then compute uppercase hexadecimal SHA256 over those exact bytes. This format uses the observed JSON representation, not a separately normalized or sorted JSON-object format. `ComputeAbortRepairSourceFingerprintAsync` measures that digest and grants no authority or original-absence certificate. Verifiers must match this format exactly; changes to fields, ordering, JSON options, null representation, encoding or digest casing require an explicit versioned certificate-compatibility transition that preserves existing reserved-repair retry bindings. Do not silently recertify an existing binding under a changed format. [Repair evidence](_bmad-output/implementation-artifacts/tests/remaining-repair-and-guidance-deferrals.md) records the bounded 4096-guard / 256 KiB SDK request limits; production certification and deployed inventory acceptance remain open.

Capability view replacement publishes its complete new pointer before retiring the exact validated prior digest's fragments through resumable SDK batches when the total fits 128 operations, including conversion to inline storage. Larger live folds retain the validated flat base as reachable storage and publish a bounded, token-free transition plus a scoped continuation pointer; reconstruction verifies complete-view digest/length and rejects nested or ambiguous continuations. Rebuild staging keeps the flat representation. Use a complete-stop deployment: pause delivery, stop all old consumers/readers/writers, replace every instance with compatible code, then resume delivery. Mixed-version rollout is unsupported because continuation readers and writers activate together. Unsupported complete-view capacity leaves the journal head retryable. The read-only index diagnostic validates required typed containers, scalars, writer scope, checkpoints (including nonempty, aligned and sequence-bounded captured digests), retained names and counters for supported Open/drained snapshots. Unsupported staging/chunk state refuses with a fixed privacy-safe error. This conservative subset supplies neither complete SDK validation parity nor ETag admission; successful production resolution remains required. All 786 attributed underscore test methods were renamed to PascalCase with equivalent discovered cases and no identifier collisions; historical receipts retain their original names.

The integration test project contains in-process workflow coverage for metadata endpoints, AI-assisted Time Entry capture, submission, approval authority, entry approval/rejection, rejected-entry correction, approved-entry correction, period submission, period approval/rejection, external contribution submission/confirmation, magic-link capability issue/revoke/confirm/adjust workflows, operational Time Entry queries, Approved-Time Ledger queries, Project/Work actual-time reports, approved billable-time export, and the Timesheets dashboard overview. The default IntegrationTests run retains five declared skips: two reserved infrastructure/performance placeholders, two opt-in in-process performance tests, and the isolated live Dapr test. Opt-in local persistence proof exists; deployed acceptance and EventStore-backed wire-path performance remain separate open evidence.

Historical 2026-10-05 evidence proved magic-link no-disclosure at three layers: service/workflow tests cover the confirmation, adjustment, replay, and invalid-link matrix; `MagicLinkConfirmationHttpBoundaryTests` exercises the live confirm/adjust GET and POST routes through the in-process HTTP host and asserts every invalid case returns an equivalent opaque response (identical status, content type, ProblemDetails body, and headers, with no tenant, target, token, expiry-reason, or capability-state leakage); and `MagicLinkConfirmationCapabilityEndpointTests` verifies route shape and authority-field exclusion. The default registered `IMagicLinkConfirmationCapabilityStateLoader` is the concrete `EventStoreMagicLinkConfirmationCapabilityStateLoader`, which resolves the token hash and folds capability, scoped Time Entry, and fresh Activity Type catalog state from EventStore-backed events; the host replaces the fail-closed trusted-context default with `HttpContextTimesheetsTrustedContextAccessor` and discovers the canonical token-hash index and tenant catalog projection handlers. That historical valid HTTP journey delivers both read models through the mapped projection route, reaches all four confirm/adjust routes, and asserts that neither read model was seeded directly. Those historical invalid links returned the opaque 403. The EventStore update `da172a8effe484be96c17aaef1c007e1ecb8f27c` caused a 401 regression in rebuilt HTTP journeys; the 2026-10-09 public-route increment repaired that regression in process, including `/metadata/timesheets` and all four external capability routes. The 2026-10-09 local EventStore pointer `f87affaefef5693fd80452eea02999e46fa1888b` contained that repair, the GET-only canonical inventory fix, and the later weak-policy guard; the 2026-10-09 rebuilt two-class HTTP selection passed 482/482 on this pointer. That pointer statement is dated 2026-10-09 history. At assessment baseline Timesheets `d72899b4489aaca0b9a9b96d0ac267165920caf8` (2026-10-20), the EventStore gitlink is `6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e`; this is a dated source observation. See the Magic-link HTTP no-disclosure gate in `docs/launch-readiness.md`; deployed acceptance and current-graph runtime qualification remain open.

## Boundary Summary

Timesheets owns time-entry, timesheet-period, approval, confirmation, ledger, reporting, and export behavior. It references stable Tenant, Party, Project, and Work identifiers only.

The sole default Hexalith.Works checkout is `<workspace>/references/Hexalith.Works`. Do not initialize `Hexalith.Timesheets/Hexalith.Works`. A caller-supplied `HexalithWorksRoot` is preserved for controlled standalone or CI builds and is not mutated by Timesheets. When unset, MSBuild resolves `references/Hexalith.Works` first and `../Hexalith.Works` second.

After cloning, initialize every root-declared dependency needed by the full solution with `git submodule update --init` from the Timesheets repository root. Do not add `--recursive`. For Works-only evaluation or adapter work, `git submodule update --init -- references/Hexalith.Works` is the narrower optional command. Never initialize a nested `Hexalith.Timesheets/Hexalith.Works` checkout.

Authoritative domain state must flow through Hexalith.EventStore. Do not add SQL, Redis, Dapr state-store writes, broker-backed CRUD, local JSON files, or direct projection mutation as Timesheets state. Projections are rebuildable read models and are not the write-side source of truth.

Tenant and resource authority is resolved by server-side gates before aggregate load, command dispatch, projection read, export, or disclosure. JWT claims and caller-submitted context are evidence, not authority.

Magic-link issue, revoke, and expire routes require a signed `domain-service:process` workload assertion and server-side authorization. Service consumers must use `MagicLinkConfirmationCapabilityCommandService.ExpireAsync` for authorized expiry. The obsolete synchronous `Expire` method now always returns an authorization-required rejection.

Timesheets supplies contracts and FrontComposer-compatible metadata. A consuming FrontComposer host owns rendered Fluent UI V5 composition, responsive behavior, and browser/WCAG 2.2 AA evidence. Do not add a Timesheets UI project.
