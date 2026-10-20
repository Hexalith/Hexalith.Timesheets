# Launch Readiness Record

## Current Assessment — 2026-10-20

Timesheets is **neither story-complete nor launch-complete**. Story 3.6 remains in progress, Story 3.7 remains in progress, Story 5.2 remains review, and Story 5.1 is done following the user’s scoped reconciliation acceptance and sign-off. Overall release decision: **FAIL**. FR14 deployed acceptance, legal-hold sign-off, tenant-policy configuration, and product stakeholder acceptance remain unfinished; Story 5.1 sign-off does not close their owning gates. Package currency separately remains **CONCERNS**.

This assessment covers the changed worktree based on Timesheets `d72899b4489aaca0b9a9b96d0ac267165920caf8`, with Builds `6a38cb0f2e74977d7457fc59e6af79457ed8196a`, EventStore `6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e`, and Works `64602575b902c0f8d3bfaa41a27565eee5682639`. SDK `10.0.402` (`rollForward: latestPatch`) and AppHost SDK `13.6.1` retain the later Story 5.2 approvals. The catalog and Works defaults both select EventStore `3.126.0`; no source-build version override was needed for this run. Their forwarding-policy divergence remains open and unwaived.

The latest historical local proof is the passing persisted journey: **2026-10-18, 1 passed / 0 failed / 0 skipped, 131.536 s**, on the earlier AppHost graph; [its receipts](../_bmad-output/implementation-artifacts/evidence/3-6/20261018-direct-eventstore-local-proof/) retain their original source identities. It exercised recording, issuance, confirm/adjust, single-use concurrency, restart, and selected readback. The current NuGet-orchestration AppHost graph has no passing runtime qualification; the owning automated smoke lane remains deferred. No AppHost, deployed resource, or foreign infrastructure was mutated for Story 5.1. Historical evidence below does not refresh either gate.

Related evidence: [current commands/counts](../_bmad-output/implementation-artifacts/tests/5-1-test-summary.md), [performance evidence](performance-evidence.md), and [owns-versus-references boundary](boundary-decision-record.md). The [approved 2026-09-12 correction](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md) resolves Q6 server-side tenant/Project governance, Q7 deterministic CSV v1, and Q8 sensitive comments with external disclosure only under explicit policy/redaction. Richer comment schemes and structured API/webhook export are post-v1; missing trust-bearing policy fails closed.

| Story | Sprint status | Acceptance boundary | Record |
|---|---|---|---|
| 3-3-confirm-time-through-magic-link | done | Contracts, domain/service confirmation, and metadata only; external comments are policy-conditional. | [Story 3.3](../_bmad-output/implementation-artifacts/3-3-confirm-time-through-magic-link.md) |
| 3-6-implement-eventstore-backed-magic-link-state-loading | in-progress | Deployed FR14 authority/inventory/continuity/shared-rebuild acceptance remains unfinished. | [Story 3.6](../_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md) |
| 3-7-prove-magic-link-no-disclosure-at-the-http-boundary | in-progress | Deployed privacy boundaries remain open. | [Story 3.7](../_bmad-output/implementation-artifacts/3-7-prove-magic-link-no-disclosure-at-the-http-boundary.md) |
| 4-7-surface-timesheets-dashboard-overview | done | Dashboard contracts/metadata/server composition/in-process proof; rendering belongs to the consuming host. | [Story 4.7](../_bmad-output/implementation-artifacts/4-7-surface-timesheets-dashboard-overview.md) |
| 5-1-final-launch-readiness-gate-and-documentation-sync | done | User accepted and signed off the completed reconciliation; remaining release prerequisites stay open. | [Story 5.1](../_bmad-output/implementation-artifacts/5-1-final-launch-readiness-gate-and-documentation-sync.md) |
| 5-2-reconcile-package-currency-and-platform-dependency-versions | review | Approved current baseline and dated audit evidence; review/acceptance remains open. | [Story 5.2](../_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md) |

The user’s 2026-10-20 instruction “accept and sign-off” is recorded in the [Story 5.1 acceptance record](../_bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate/story-acceptance.json). It accepts the completed reconciliation and FAIL assessment and permits Story 5.1 closure. It supplies no deployed qualification, tenant/legal policy approval, configured tenant policy, product UAT, or approval to release.

## Launch-Scope Classification

Atomic dispositions are `implemented`, `unfinished`, `waived`, and `post-v1`. `unfinished` means in-scope acceptance remains incomplete; a runtime blocker is evidence, not a scope waiver. Compound labels such as `implemented / waived` identify implemented controls separately from an approved residual limitation. Every waived row names approval evidence, owner, risk, and revisit condition. The approved correction carries existing unrelated waivers forward; it does not approve later unverified risks. Unapproved residuals remain unfinished.

| Item | Verified state | Classification (implemented/unfinished/waived/post-v1) | Owner | Risk | Revisit condition | Evidence / approval source |
|---|---|---|---|---|---|---|
| Unavailable defaults | Host keeps fail-closed defaults for live Works validation and planned-effort reporting unless explicit opt-in wiring supplies live channels. | waived | Story 1.10 / Story 4.8 / platform | Work writes are denied and planned effort is unavailable in the default host. | Host binds `IWorksQueryChannel`, calls `AddTimesheetsWorksReferenceValidation()`, and calls `AddTimesheetsWorksPlannedEffortReporting()`. | `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`; `docs/boundary-decision-record.md` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Skipped lanes | Five default skips: two static infrastructure/performance reservations, two opt-in in-process performance tests (`TIMESHEETS_PERF=1`), and the isolated local Dapr journey (`TIMESHEETS_LIVE_DAPR=1`). Local fixtures now exist; the reserved lanes do not claim runtime or deployed acceptance. | waived | Data-bearing runtime story | Runtime paths are reserved, not proven by default suites. | Run the owned opt-in fixtures when their prerequisites hold; replace static reservations only in their owning runtime/performance stories. | `tests/Hexalith.Timesheets.IntegrationTests/InfrastructureLaneTests.cs`; `tests/Hexalith.Timesheets.IntegrationTests/PerformanceEvidenceLaneTests.cs` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Deferred integrations | Live Works host channels remain waived; export-preview service behavior is implemented while its HTTP endpoint is post-v1. | waived / post-v1 | Platform / owning feature stories | Existing service behavior can be mistaken for launch-active runtime integration. | Works host supplies live channels through explicit opt-in; a separate approved story may map a preview route. | `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`; `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Legal-hold policy | Default indefinite Time Entry and approval/correction audit retention is implemented; export and magic-link audit metadata follow documented tenant defaults. Legal-hold override still needs tenant/legal sign-off. | unfinished | Story 1.4 / tenant + legal | No tenant/legal approval closes this launch gate; this is not a waiver. | Tenant/legal sign-off confirms the launch policy and configuration. | `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` |
| Tenant policy configuration | Q8 resolves the sensitive-comment baseline; it does not configure tenant-specific retention, legal hold, or comment policy. `TimesheetsEvidencePolicyOptions.FailClosedDefault` leaves all three policy-configured flags false. | unfinished | Tenant / legal / platform; Story 1.4 | Trust-bearing approval, correction, confirmation, and export cannot infer tenant policy from the documented baseline. | The owning tenant/platform supplies authorized policy configuration and sign-off before enabling dependent actions. | `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyOptions.cs`; PRD §9/Q8 |
| Comment sensitivity | Q8 resolved by the approved 2026-09-12 decision: comments are sensitive unstructured evidence; internal display is policy-controlled; diagnostics/export/external display exclude comments by default. Explicit external policy/redaction is required; missing trust-bearing policy fails closed. Richer classification/redaction is post-v1. | implemented / post-v1 | Story 1.4 / PM | Richer tenant schemes remain outside v1; legal-hold sign-off is tracked separately. | A policy-bearing post-v1 story names richer classification/redaction needs. | `src/Hexalith.Timesheets.Contracts/openapi/timesheets-evidence-policy.v1.md`; PRD Q8 |
| Export format | Q7 resolved: deterministic approved-time CSV v1 is the verified launch contract. Structured API/webhook export is post-v1. | implemented / post-v1 | Story 4.5 / PM | Downstream billing may need a structured contract beyond evidence CSV. | A named downstream consumer sponsors a structured contract story after v1. | `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`; `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/approved-time-export-v1-project-work-boundaries.csv` |
| Secondary magic-link identity verification | v1 uses single-use scoped expiring links; secondary identity verification is explicitly deferred. | post-v1 | Story 3.2 / PM | High-value or billable links rely on scoped single-use controls only. | Post-v1 security/product review decides secondary verification. | PRD Q4 |
| Performance evidence | NFR10 and NFR11 in-process paths pass measured p95 targets; EventStore-backed persistence/Dapr wire path is waived. | implemented / waived | Stories 1.11 and 4.10 | In-process p95 does not prove persisted runtime topology latency. | Realistic EventStore/Dapr/Aspire persisted fixtures exist and the wire-path lane runs. | `docs/performance-evidence.md`; `tests/Hexalith.Timesheets.IntegrationTests/*PerformanceLaneTests.cs` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Live Works reference validation in host | `WorksQueryWorkReferenceValidator` is concrete, but the default host keeps `DenyAllWorkReferenceValidator` because no live `IWorksQueryChannel` is bound. | waived | Story 1.10 / platform | Live Work capture remains denied in the running host. | Host binds the Works query channel and opts into Works reference validation. | `src/Hexalith.Timesheets.Works/WorksQueryWorkReferenceValidator.cs`; `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Live Works planned-effort reporting | `WorksQueryWorkPlannedEffortProvider` is concrete and covered by Works tests; the host keeps `UnavailableWorkPlannedEffortProvider`. | waived | Story 4.8 / platform | Planned-vs-actual reports show planned effort as unavailable in the default host. | Host wires the provider over a live Works channel. | `src/Hexalith.Timesheets.Works`; `tests/Hexalith.Timesheets.Works.Tests` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Magic-link HTTP no-disclosure | The full current IntegrationTests executable covers equivalent opaque denial and protected-material checks. All four token-bearing GET/POST routes set `Cache-Control: no-store` and `Referrer-Policy: no-referrer`; scopes, malformed diagnostics, and deployed ingress remain separate evidence. | implemented / unfinished | Stories 3.6 and 3.7 / platform | In-process TestServer and captured logs do not establish ingress, sidecar, log-scope, exception, or malformed-request privacy. | Story 3.7 verifies remaining privacy boundaries in the deployed topology before public exposure. | `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`; public-route compatibility and spec-17 entries in `_bmad-output/implementation-artifacts/deferred-work.md` |
| Magic-link Activity Type ownership inventory | v1 issue, display, confirm, and adjust paths accept tenant-owned Activity Types only while Project and Work remain valid time-entry targets. At issuance, issuance validates the catalog-selected tenant ownership but does not pre-load the target Time Entry, so target identity and Activity Type agreement remain use-time checks. For existing links, folded Time Entry scope is the authoritative use-time ownership gate; behind the canonical tenant-only catalog writer, duplicate and ownership-shape catalog checks are defence in depth while the zero-match presence gate remains required. The repository cannot inspect deployed capability or TimeEntry histories. | implemented / unfinished | Story 3.6 / release owner | A project-owned capability over normal project-owned Time Entry evidence is reissuable only after an authorized correction changes that Time Entry to a tenant-owned Activity Type, followed by capability revocation and reissuance. An Activity-Type-changing correction, including a change between tenant-owned Activity Types, denies every outstanding link whose capability still names the prior Activity Type, but that denial is not permanent: without revocation at the first Activity-Type change, an unused A-scoped link can become admissible again after an A-to-B-to-A sequence restores the same Activity Type ID and scope. The denial is opaque and operators receive no automatic signal, so Activity Type ID reuse can revalidate an unused old link. A legacy scope-less Project-to-Tenant correction can retain Project scope; a legacy scope-less Tenant-to-Project correction can retain Tenant scope while pointing at a project-owned Activity Type ID absent from the tenant catalog. Both mismatch shapes fail closed and cannot be repaired or reclassified by token reissue alone. No approval for deployed-history risk acceptance is established; inventory/remediation is unfinished. | Before rollout, the release owner inventories deployed capability and TimeEntry histories, authorizes correction of repairable project-owned Time Entries to tenant-owned Activity Types, then revokes and reissues their affected capabilities. Operational correction handling revokes affected capabilities at the first Activity-Type change, before any later ID reuse can revalidate an old link, and reissues only the links still required. The owner also either explicitly accepts both legacy scope/Activity-Type mismatch shapes or owns a separate append-only remediation for histories that token reissue alone cannot repair. | `src/Hexalith.Timesheets.Server/MagicLinks`; `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs`; `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` |
| Magic-link invalid-token timing | Historical 2026-10-05 evidence: all four external routes returned equivalent invalid-link status, headers, content type, normalized body, and raw UTF-8 response length. Current in-process HTTP equivalence and privacy journeys pass; live topology remains unverified. Blank or hash-derivation-rejected tokens perform zero read-model and EventStore I/O; an unknown well-formed hash necessarily performs one token-index lookup. | implemented / unfinished | Story 3.6 / security | Backend work and distributed dependency timing are not constant across invalid-token classes even though the HTTP representation is opaque. Public-exposure abuse-control/timing acceptance has no recorded approval and remains unfinished. | Revisit before public exposure without suitable abuse controls, if token entropy is weakened, or if evidence shows known invalid tokens can be practically classified by timing. | `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`; `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` |
| Magic-link live end-to-end resolution | FR14 remains in v1 scope. The host registers the canonical token-hash index and tenant Activity Type catalog projection handlers, Time Entry evidence handlers, and EventStore processing. After mapped delivery, valid Fresh-catalog GET displays return 200 and valid confirm/adjust POSTs return 202 only after Completed EventStore status and exact stored-event readback. The run-only AppHost declares an EventStore resource. The latest isolated local Dapr/EventStore journey passed 1/1 on 2026-10-18 in 131.536 s on the earlier AppHost graph; this is historical local proof, not current-graph runtime qualification. | unfinished | Stories 3.6 and 3.7 | The local proof does not establish deployed Dapr persistence, production access/authority, certified legacy inventory/continuity, or independently certified shared-rebuild coverage. Current NuGet-orchestration runtime qualification is unfinished. | Inventory deployed streams read-only, obtain trusted production authority and source coverage, then verify all routes with concurrent single-use and restart checks in the deployed topology. Story 3.6 owns acceptance. | `src/Hexalith.Timesheets.AppHost/Program.cs`; `docs/local-magic-link-dapr-proof.md`; `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs`; `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` |
| Export preview | `ApprovedTimeExportService.PreviewAsync` is side-effect-free and tested; no dedicated HTTP route maps the preview capability. | implemented / post-v1 | Story 4.9 | Metadata can imply a callable preview route that does not exist. | A story maps a preview endpoint, or metadata removes the HTTP-route implication. | `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`; `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` |
| Time-zone and period policy | Tenant time zone, UTC audit instants, tenant-local period keys, and DST/boundary export tests are implemented; carry-forward approval behavior remains open. | implemented / post-v1 | Stories 2.7 and 4.6 / PM | Carry-forward handling for existing approved entries remains unresolved. | Period-approval policy decision covers carry-forward behavior. | `tests/Hexalith.Timesheets.IntegrationTests`; PRD Q1 |
| NFR13 UI behavior | Timesheets supplies contracts and FrontComposer metadata. No Timesheets UI project exists or may be added. A consuming FrontComposer host owns rendered Fluent UI V5 composition and WCAG 2.2 AA evidence. | post-v1 | Consuming FrontComposer host owner | Contracts/metadata and service tests do not prove rendered keyboard, focus, responsive, or browser accessibility behavior. | The consuming FrontComposer host supplies component/browser/accessibility acceptance; do not scaffold Timesheets UI projects. | README; architecture; approved 2026-09-12 course correction §3/§4.6–4.8. |
| Production deployment evidence | No accepted deployed Timesheets installation/qualification is recorded; local hosting proof does not close deployed release acceptance. | unfinished | Release owner | Hosted release artifacts, production authority/bindings, certified inventory/continuity, and deployed shared-rebuild proof remain missing. | Deployment evidence is produced at release/deployment time. | Repository artifacts |
| External stakeholder acceptance | Story 5.1 reconciliation acceptance is recorded; product-level stakeholder UAT remains unaccepted. | unfinished | PM/PO | Scoped documentation sign-off supplies no product UAT or approval to waive it. | Product UAT or stakeholder acceptance of the release is captured. | [Story 5.1 scoped acceptance](../_bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate/story-acceptance.json) |
| Works staleness detection | `WorkItemView` exposes `LatestAcceptedSourceSequence` but not enough freshness data for the adapter to detect stale Works state fail-closed. | waived | Platform / Works owner | Stale Works state can be accepted as current by the adapter. | Works supplies the freshness contract required for fail-closed validation; resolve through its owner without copying Works-owned state. | Story 1.10 Dev Agent Record; `src/Hexalith.Timesheets.Works` Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Platform prerelease dependency exception | On 2026-10-18 the Timesheets restored package graph includes Aspire `13.6.1`, Dapr `1.18.10`, Keycloak `13.6.1-preview.1.26506.6`, and CommunityToolkit Aspire Dapr `13.6.1-beta.935`; the relevant Fluent UI V5 policy-surface catalog entry is `5.0.1`, now stable, so only the Keycloak and CommunityToolkit Dapr integrations remain prerelease in the Timesheets graph. This is not a complete shared-catalog prerelease inventory; for example, the catalog prerelease `Aspire.Hosting.Kubernetes` is outside the Timesheets graph. As of 2026-10-20, Timesheets directly pins only its AppHost SDK and the separately published `Hexalith.Works.Contracts` `1.0.2`, and does not override the platform-owned prereleases. | waived | Platform / Hexalith.Builds and Hexalith.EventStore | Prerelease platform integrations can change behavior before stable release and can be mistaken for approved stable direct Timesheets dependencies. | Platform publishes and validates stable replacements or explicitly approves the prerelease integrations for release. | `Directory.Packages.props`; `references/Hexalith.Builds/Props/Directory.Packages.props`; `_bmad-output/planning-artifacts/architecture.md`; approved 2026-09-12 course correction, `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md` §7 Approval: [approved 2026-09-12 course correction §3](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach), carrying forward the existing named waiver only. |
| Doc-vs-test-project divergence | Architecture and guidance list the six actual test projects, including Works.Tests; consumer-host rendering owns UI tests, and no Timesheets UI project is targeted. | implemented | Story 5.1 | Stale test maps hide actual coverage ownership. | Keep architecture project maps aligned with `src/` and `tests/`. | `_bmad-output/planning-artifacts/architecture.md`; filesystem project list |

## Release-Gate Decision Table

Verification scope: **2026-10-20**, SDK `10.0.402`, the changed worktree and full revisions above, Debug/source references. Exact argv, logs, six XML reports, counts, and skip reasons are in [the Story 5.1 test summary](../_bmad-output/implementation-artifacts/tests/5-1-test-summary.md). This run does not refresh the dated package audits, opt-in performance measurements, historical local journey, or deployed acceptance.

| Gate | Verdict | Evidence | Notes |
|---|---|---|---|
| Build | PASS | `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`; matching `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`. 2026-10-20 pre-sign-off solution build passed with 0 warnings and 0 errors (6.03 s); the post-sign-off Architecture project rebuild also passed with 0 warnings/errors (0.94 s). | No EventStore version override was needed because both defaults equal `3.126.0`. This is compile-time evidence, not current-graph runtime evidence; current automated AppHost smoke remains deferred. Historical HXW0002 and MSB3243/CS1704 failures remain dated below. |
| Package currency | CONCERNS | Dated Story 5.2 audits: SDK `10.0.402`, `Aspire.AppHost.Sdk` `13.6.1`, 2026-10-20 round-8 package graph at `929ea22c977385590d77d95f73af86e8b051c4fc`, 1,954 Release passes, five skips, zero failures. See Package-Currency Verdict. | No package audit was rerun by Story 5.1. Root npm findings, transitive tooling failure, approved prereleases, and the unwaived Works forwarding issue remain visible. |
| Tests (full suite) | PASS | 2026-10-20 exact pass/fail/skip counts: ArchitectureTests 65/0/0; Contracts.Tests 92/0/0; Server.Tests 558/0/0; Works.Tests 76/0/0; Projections.Tests 437/0/0; IntegrationTests 727/0/5. Total: 1955 passed, 0 failed, 5 skipped (1960 cases). [Six XML reports and commands](../_bmad-output/implementation-artifacts/tests/5-1-test-summary.md#current-suite-results). | Latest complete executables ran individually; five declared default skips and the initial Projection failure/retry remain explicit. Local proof does not replace a deployed persistence test. |
| Privacy/logging scans | CONCERNS | Current ArchitectureTests includes all 12 passing DiagnosticsPrivacyTests, including the source logging scan; full IntegrationTests supplies HTTP privacy assertions. Exact commands/scope are in the summary. | In-process capture does not qualify ingress/sidecar logs, scopes, exception text, or malformed-request diagnostics. Story 3.7 remains in progress. |
| Projection rebuild/idempotency | PASS | Current Projections.Tests: 437 passed, 0 failed, 0 skipped. Composed IntegrationTests also exercises replay and selected readback; exact receipts and fallback scope are in the summary. | Rebuildable read models remain non-authoritative; deployed shared-rebuild authority/coverage remains unfinished. The initial full run failed one SDK scratch-marker cleanup case; the focused 1/1 and complete 437/437 retry passed. This intermittent cleanup risk stays visible in the summary. |
| Export golden files | PASS | Current IntegrationTests include `ApprovedTimeExportIntegrationTests` and `Exports/Golden/approved-time-export-v1-project-work-boundaries.csv`. | Verified v1 CSV; comments excluded by default, no finance calculations, no structured API/webhook launch contract. |
| Magic-link HTTP no-disclosure | PASS / CONCERNS | Current full IntegrationTests covers equivalent opaque denial, all four token routes, protected-material assertions, `Cache-Control: no-store`, and `Referrer-Policy: no-referrer`. | In-process evidence only; deployed ingress/sidecar and remaining diagnostic boundaries are unfinished. |
| Magic-link durable confirm/adjust persistence | FAIL | EventStore actor tests prove exact stored use/effect batches and actor recreation; current composed HTTP tests verify Completed status and matching stored-event readback. Historical isolated local journey passed 1/1 on 2026-10-18 in 131.536 s. | Deployed Dapr persistence, trusted production authority, certified legacy inventory/continuity, and shared-rebuild acceptance remain unfinished. Historical local proof predates the current AppHost graph. |
| Performance evidence | PASS / WAIVED | Historical in-process NFR10/NFR11 measurements in `docs/performance-evidence.md`; opt-in tests remain skipped in this default run. | Full EventStore-backed wire path remains waived under the cited approval, owners, risk, and revisit condition; no new latency claim is made. |
| Legal-hold sign-off | FAIL | Owner: tenant/legal; Story 1.4. No tenant/legal sign-off artifact accepts the launch retention and legal-hold override policy. | Acceptance evidence required: dated tenant/legal approval of retention, hold activation/release authority, and the authorized deployed override configuration. The documented default is not sign-off. |
| Tenant-policy configuration | FAIL | Owner: tenant/platform with legal approval; Story 1.4. `TimesheetsEvidencePolicyOptions.FailClosedDefault` leaves retention, legal-hold, and comment-policy configured flags false. | Acceptance evidence required: authorized tenant-specific configuration, approval provenance, and deployed fail-closed/allowed-action verification for the dependent approval, correction, confirmation, and export paths. |
| Tenant-isolation/security | PASS | Current Server.Tests and IntegrationTests cover tenant/resource gates, fail-closed defaults, loader integrity, and tenant-only magic-link Activity Types; see exact counts in the summary. | Tests do not certify production authority or deployed policy configuration. |
| Works checkout ownership | PASS | Current ArchitectureTests include `WorksCheckoutGovernanceTests`; root `.gitmodules` and git index pin only `references/Hexalith.Works`. | Does not claim live Works host integration. |

Overall release decision: **FAIL**. Story 3.6 and Story 3.7 remain in progress; Story 5.2 remains review; Story 5.1 reconciliation is accepted and done under the scoped user sign-off; remaining release gates stay open. Atomic magic-link submission, EventStore-backed recording, local actor commit/reload, and composed-host projection registration are implemented. Deployed acceptance remains open, including production authority, certified legacy inventory/continuity, and shared-rebuild coverage. Current-graph runtime qualification is also unfinished. Consumer-host rendering/accessibility, Works host activation, export-preview HTTP, performance wire path, legal-hold sign-off, package concerns, and stakeholder acceptance retain the distinct classifications above.

## Package-Currency Verdict

Current package-currency verdict (Story 5.2 re-baselined evidence, audited on 2026-10-18 from the worktree based on `180d9fd9a635ed44ba896688d6a06e38a33e9f28`, committed as `b85e810`): **CONCERNS**. The Timesheets-owned baseline is .NET SDK `10.0.402` (`rollForward: latestPatch`) and `Aspire.AppHost.Sdk` `13.6.1`, aligned with the imported stable Aspire catalog line. The AppHost keeps NuGet-backed orchestration with `AspireUseCliBundle=false` and suppresses only `ASPIRE010`, the 2026-09-13 Option 2 decision already used by `Hexalith.EventStore`. After the forced no-cache restore, both the package-mode and source-reference AppHost assets hold one Aspire `13.6.1` line, including `Aspire.Hosting.Orchestration.linux-x64` and `Aspire.Dashboard.Sdk.linux-x64`, and no Aspire `13.5.x` package; before this change the same assets mixed `Aspire.Hosting.AppHost` `13.5.3` with `Aspire.Hosting` `13.6.1` and had no orchestration or dashboard package. The 2026-10-18 direct, vulnerable, and deprecated NuGet audits, run on the package-mode graph, were clean at that date. Update (2026-10-20, round 7; modified worktree based on `2d6b689ec45fd1f73fb7a3ff09e8950c90a2fd07`, committed as `c5ac6005a48d973d96f7eff6496c72d0728432f0`): the approved root CPM update adopts Works Contracts `1.0.2`; the forced no-cache package-mode restore and warnings-as-errors Release build passed. The direct rerun reports no Timesheets-owned direct package updates; only catalog-owned `Hexalith.EventStore.*` `3.125.1 -> 3.126.0` remains. Vulnerability and deprecation reruns remain clean across all 15 projects. EventStore `3.126.0` was published after the 2026-10-18 audit and requires a Builds-owned catalog update; no sibling or gitlink is changed here. Update (2026-10-20, round 8; worktree based on `929ea22c977385590d77d95f73af86e8b051c4fc` with documentation and ledger edits only, committed as `525859841e3dc329f65970b7067d3ef3bd1a91fa`): the workspace sync commit `929ea22` moved the `references/*` gitlinks after the round-7 audit. At that commit the `references/Hexalith.Builds` catalog `6a38cb0f2e74977d7457fc59e6af79457ed8196a` selected EventStore `3.126.0`, Commons `2.30.3`, and PolymorphicSerializations `1.19.5`, and Works `64602575b902c0f8d3bfaa41a27565eee5682639` defaulted and forwarded `HexalithEventStoreVersion` `3.126.0`; these catalog-owned values are dated observations that later workspace syncs can move. After a forced no-cache package-mode restore and a non-incremental warnings-as-errors Release build, the direct, vulnerable, and deprecated audits were clean across all 15 projects, with no direct package update, and all six Release suites ran 1,959 tests: 1,954 passed, 5 intentional skips, 0 failures. Root npm is applicable because release tooling added a root `package.json` and `package-lock.json`; its eight high `npm audit` findings remain visible as CONCERNS. The solution/AppHost transitive-outdated tooling failure remains visible, the other 14 project fallbacks were reviewed with no pin, and the platform-owned prereleases remain an explicit waiver rather than a hidden stable-package claim. The Works `HexalithEventStoreVersion` forwarding divergence is open and unwaived: Works forwards its own hard-coded default rather than the consuming catalog's version. The values diverged on 2026-10-18 (`3.124.0` versus catalog `3.125.1`) and coincided at `3.126.0` at `929ea22`; see the platform row. Approval evidence: the 2026-10-18 option-A renegotiation recorded in the Spec Change Log of `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md` approves the SDK `10.0.402` and `Aspire.AppHost.Sdk` `13.6.1` baseline; the 2026-09-13 Option 2 decision recorded in the same spec approves NuGet-backed orchestration; and the approved 2026-09-12 course correction in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md` §7 approves the bounded Story 5.2 package updates and the recorded platform prerelease exceptions. The latest passing AppHost-starting run is the 2026-10-18 isolated journey on the worktree based on Timesheets `72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63`, both with uncommitted implementation changes; implementation was later published in EventStore `9b66153515687463fc1b1e929f8b0e73d8145e2c` and the proof in Timesheets `9523d94983a346d866246a2c45c3515b1bc54580` (`scripts/run_isolated_magic_link_dapr.py`, live journey 1/1 in 131.536 s; receipts in `_bmad-output/implementation-artifacts/evidence/3-6/20261018-direct-eventstore-local-proof/`). It ran on the then-current Builds catalog `332b2c1`, but with the pre-change `Aspire.AppHost.Sdk` `13.5.3` and its CLI-bundle DCP; the earlier historical AppHost smoke passed on 2026-09-15 after the public/internal endpoint split. That evidence predates the NuGet-orchestration AppHost, so it does not validate the current restored graph. On 2026-10-18 the compiled AppHost passed all 35 no-resource configuration probes (`python3 scripts/check_magic_link_apphost_guards.py --configuration-only`), but `python3 scripts/run_isolated_magic_link_dapr.py` refused at preflight with `A Dapr application is already running or inventory is unsafe; no AppHost started` because a foreign Tenants AppHost and 13 foreign Dapr applications were running; an ordinary `aspire start` was not attempted because it would join the shared placement with a duplicate `eventstore` app ID. No foreign runtime was started or stopped. Current runtime-smoke evidence remains deferred to the infrastructure-owned automated AppHost lane recorded in the deferred-work ledger.

The 2026-10-20 round-7 audit, build and affected-test outputs are retained in the
[verification packet](../_bmad-output/implementation-artifacts/evidence/5-2/20261020-round7/README.md);
the round-8 restore, build, audit and test outputs at `929ea22` are retained in the
[round-8 packet](../_bmad-output/implementation-artifacts/evidence/5-2/20261020-round8/README.md).

| Dimension | Verdict | Evidence | Rationale |
|---|---|---|---|
| Direct Timesheets NuGet package currency | PASS | On 2026-10-20 in round 7 (committed as `c5ac600`), after the approved root CPM update to Works Contracts `1.0.2` and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx --force --no-cache -m:1 /nr:false` (the configuration-free package-mode graph), `dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore --format json` covered all 15 Timesheets projects and reported no Timesheets-owned direct package updates. The only direct update it reported was catalog-owned `Hexalith.EventStore.*` `3.125.1 -> 3.126.0`, published after the 2026-10-18 audit; that earlier no-update result remains dated historical evidence. On 2026-10-20 in round 8 at `929ea22`, where the catalog selected EventStore `3.126.0`, the same command after a forced no-cache restore covered all 15 projects and reported no direct package updates, so the round-7 EventStore drift is dated round-7 evidence. The round-8 package listing at `929ea22` (`dotnet list Hexalith.Timesheets.slnx package --no-restore --format json`) resolves `MinVer` `8.0.0`; ASP.NET Core MVC testing, DI, and logging abstractions `10.0.13`; Extensions resilience/service discovery `10.11.0`; OpenTelemetry `1.20.0`; `coverlet.collector` `10.1.0`; `Microsoft.NET.Test.Sdk` `18.11.0`; `NSubstitute` `6.2.0`; `Shouldly` `4.3.0`; `xunit.runner.visualstudio` `4.0.1`; `xunit.v3` `4.0.2`; the six catalog-owned `Hexalith.EventStore.*` direct packages (`Aspire`, `Client`, `Contracts`, `DomainService`, `Server`, and `Testing`) `3.126.0`, a dated observation at `929ea22`; and the root-pinned `Hexalith.Works.Contracts` `1.0.2`. AppHost-generated direct assets resolve at `13.6.1`. | `Directory.Packages.props` imports the centrally managed `Hexalith.Builds` catalog; project `PackageReference` items remain versionless, the AppHost SDK is the only Timesheets-owned project SDK pin, and no `.sln` was introduced. |
| Vulnerable and deprecated NuGet packages | PASS | On 2026-10-18, on the same package-mode graph (after the forced no-cache configuration-free restore), `dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive --no-restore` and `dotnet list Hexalith.Timesheets.slnx package --deprecated --include-transitive --no-restore` both covered all 15 projects and reported no vulnerable or deprecated packages from `https://api.nuget.org/v3/index.json`. The 2026-10-20 round-7 reruns of both commands (with `--format json`) on the restored Works Contracts `1.0.2` package-mode graph again covered all 15 projects with no vulnerable or deprecated packages, as did the round-8 reruns at `929ea22` on the EventStore `3.126.0` graph. Root npm advisories are recorded in the `Root npm applicability` row. | No compatibility, security, or deterministic-build reason was found for adding an explicit transitive pin. |
| Root npm applicability | applicable / CONCERNS | The Timesheets root carries `package.json` and `package-lock.json` for release and Conventional Commit tooling. On 2026-10-18, `npm ci` exited 0 (added 361 packages, audited 505). `npm outdated` exited 1 with two entries: `@commitlint/cli` and `@commitlint/config-conventional` at current and wanted `21.2.2`, latest `21.2.4`. `npm audit` exited 1 with 8 high, 0 critical, 0 moderate, and 0 low findings, all from braces `GHSA-vfj7-8cjw-p6xm` through `micromatch`, `@semantic-release/commit-analyzer`, `semantic-release`, `@semantic-release/exec`, `@semantic-release/github`, `@semantic-release/npm`, and `@semantic-release/release-notes-generator`; npm offers only `npm audit fix --force`, which would install the breaking `@semantic-release/commit-analyzer@6.3.3`. Manifests under `references/Hexalith.*` remain sibling-owned and excluded. | Owner: CI/CD release tooling (`docs/ci-cd.md` and the `spec-implement-timesheets-ci-cd.md` deferred-work entry); Story 5.2 changes no npm manifest, lockfile, or dependency version. Risk: a high-severity denial-of-service chain in local and CI release-tooling dev dependencies; the npm graph is not packed into the five NuGet packages. The shared dependency-review gate runs only on pull requests to `main` and evaluates dependency changes introduced by the pull request, not the full existing lockfile ([action documentation](https://github.com/actions/dependency-review-action#overview)). These existing braces findings stay outside that gate unless a pull request changes the affected packages; no workflow runs an npm vulnerability audit; the pinned commitlint workflow and the shared release workflow (`domain-release.yml@544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, called by `release.yml`) run `npm audit signatures` for provenance verification. The CI/CD workflows have run on direct pushes to `main` since `c4695a5`, and no pull request exists, so the gate has not evaluated these findings (checked 2026-10-20). The two `@commitlint` patch releases are exact-pinned tooling drift with no reported advisory. Revisit condition: an upstream braces/micromatch fix or a separately reviewed resolution lands through the release-tooling owner, or that owner refreshes the exact `@commitlint` pins. |
| Transitive drift | reviewed, no pin / tooling unavailable | On 2026-10-18, on the package-mode graph (after the forced no-cache configuration-free restore), solution-level `dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive --no-restore` and the AppHost project fallback both failed exactly with `error: Sequence contains no matching element`; this lane is not reported clean. On the source-reference graph (after the Debug `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.125.1` restore), `dotnet package list --project src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --outdated --include-transitive --no-restore` fails with the same error. As a manual AppHost fallback on that same source-reference graph, `dotnet list src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj package --include-transitive --no-restore --format json` succeeded; comparing its 82 packages with the nuget.org flat-container indexes found 22 stable packages behind latest, all reached through Aspire hosting and orchestration (for example `KubernetesClient` `19.0.2 -> 21.0.8`, `Nerdbank.MessagePack` `1.2.4 -> 1.4.82`, `Grpc.AspNetCore` `2.83.0 -> 2.84.0`, `Polly.Core` `8.7.0 -> 8.8.0`, and `System.Drawing.Common` `6.0.0 -> 10.0.13`), plus the two waived prereleases; `Aspire.Hosting.Keycloak` has no stable release. The package-mode AppHost transitive graph was not separately compared. Update (2026-10-20, round 7): on the freshly restored package-mode graph with Works Contracts `1.0.2`, the solution-level `--outdated --include-transitive --no-restore --format json` audit and its AppHost fallback again exited 1 with `error: Sequence contains no matching element`. The other 14 project fallbacks exited 0 and reported 26 distinct transitive package IDs behind latest (the earlier 21 plus five catalog-owned EventStore packages now at `3.125.1 -> 3.126.0`); the only direct drift remains the catalog-owned EventStore line. No transitive pin was added. Update (2026-10-20, round 8, at `929ea22`): on the freshly restored package-mode graph with catalog EventStore `3.126.0`, the solution-level audit and its AppHost fallback again exited 1 with `error: Sequence contains no matching element`; the other 14 project fallbacks exited 0 and reported 19 distinct transitive package IDs behind latest (the round-7 26 minus the five EventStore IDs, `Hexalith.Commons.UniqueIds`, and `Hexalith.PolymorphicSerializations`) and no direct drift. No transitive pin was added. The manual AppHost registry fallback was not rerun in round 7 or round 8, so its 2026-10-18 source-reference result above remains the latest AppHost-only drift evidence. On 2026-10-18, the other 14 per-project `--outdated --include-transitive --no-restore --format json` fallbacks, run on the package-mode graph, succeeded and reported 21 distinct drifted transitive packages and no direct update at that date. Runtime and platform drift: `Google.Protobuf` `3.35.0 -> 3.36.2`; gRPC `2.80.0 -> 2.84.0`; `Hexalith.Commons.UniqueIds` `2.30.1 -> 2.30.3`; `Hexalith.PolymorphicSerializations` `1.19.3 -> 1.19.5`; `Microsoft.Bcl.Cryptography` `10.0.2 -> 10.0.13`; configuration environment variables `10.0.0 -> 10.0.13`; Polly `8.4.2 -> 8.8.0`; and `System.Threading.RateLimiting` `8.0.0 -> 10.0.13`. Test-stack drift: `Castle.Core` `5.1.1 -> 5.2.1`; `DiffEngine` `11.3.0 -> 20.8.2`; `EmptyFiles` `4.4.0 -> 8.20.1`; `Microsoft.ApplicationInsights` `2.23.0 -> 3.2.0`; `Microsoft.Bcl.AsyncInterfaces` `6.0.0 -> 10.0.13`; `System.CodeDom` `6.0.0 -> 10.0.13`; `System.Diagnostics.EventLog` `6.0.0 -> 10.0.13`; and `System.Management` `6.0.1 -> 10.0.13`. | The drift arrives through EventStore, Dapr, ServiceDefaults resilience, the catalog-owned Hexalith package versions, or the test stack, and the vulnerability/deprecation audits are clean. Adding a root transitive pin would promote package dependencies without a compatibility, security, or deterministic-build reason. Owner of the tooling gap: Timesheets release maintainer, with the failing evaluation in .NET SDK `dotnet list`/`dotnet package list`. Risk: drift carried only by the non-packable AppHost graph is invisible to the official lane and depends on the manual registry fallback. Revisit condition: a later SDK patch evaluates the AppHost graph, or the AppHost becomes a shipped artifact. |
| Platform and submodule alignment | waived / open divergence | Root Timesheets pins SDK `10.0.402` and AppHost SDK `13.6.1`; the current imported catalog aligns Aspire packages at stable `13.6.1` (its `HexalithAspireAppHostSdkVersion` is also `13.6.1`) and the Dapr family at stable `1.18.10`, which the restored host and test graphs resolve. Timesheets has no direct Dapr or Fluent UI package. The restored AppHost graph carries the platform-owned prereleases `Aspire.Hosting.Keycloak` `13.6.1-preview.1.26506.6` and `CommunityToolkit.Aspire.Hosting.Dapr` `13.6.1-beta.935` through `Hexalith.EventStore.Aspire`; the relevant Fluent UI V5 policy-surface catalog entry is `5.0.1`, now a stable line. Not covered by this waiver: source-reference Debug builds inherit the Works-owned `HexalithEventStoreVersion` default instead of the consuming catalog's value. On 2026-10-18 that default was `3.124.0` against the catalog's EventStore `3.125.1`: the README restore/build passed, but Server.Tests, Projections.Tests, and IntegrationTests then failed 135, 351, and 249 cases loading `Hexalith.EventStore.Contracts, Version=3.125.1.0` beside a copied `3.124.0.0` assembly, so that verification passed `-p:HexalithEventStoreVersion=3.125.1`. At `929ea22` (2026-10-20), Works `6460257` defaulted to `3.126.0` and the catalog `6a38cb0` selected `3.126.0`, so the values coincided; whenever they differ, source-reference lanes pass the catalog's value as `-p:HexalithEventStoreVersion=<catalog version>`. The default README source-reference lane was not rerun at that commit. `references/Hexalith.Builds/Props/Directory.Packages.props` and `references/Hexalith.Works/Directory.Build.props` remain submodule-owned. | Waived (platform prereleases only). Owner: Platform / Hexalith.Builds and Hexalith.EventStore. Approval: the approved 2026-09-12 course correction in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md` §7 (recorded prerelease exceptions). Risk: the AppHost depends on prerelease platform integrations that can change before stable release, and documentation could misstate them as stable Timesheets direct pins. Revisit condition: the platform publishes and validates stable Keycloak and CommunityToolkit Dapr integration lines or explicitly approves their continued prerelease use for release. Open, unwaived divergence: the Works EventStore-version forwarding. Owner: Hexalith.Works (EventStore-version forwarding in `references/Hexalith.Works/Directory.Build.props`). Risk: whenever the Works default and the catalog differ, default source-reference builds produce test hosts that cannot load the catalog EventStore assembly, so default local verification fails unless the version is passed explicitly; the values coincided at `929ea22` only because both were set to `3.126.0`. Revisit condition: Works forwards the consuming catalog's EventStore version, or a separately approved Timesheets build increment aligns it; tracked by the deferred-work entry "Align the existing Works EventStore-version forwarding with the consuming source-build catalog in a separate build increment". |

Historical package evidence below is dated and does not describe the current restored graph.

**2026-10-03 catalog observation:** the imported catalog now declares Aspire packages at stable `13.6.0`, the Dapr family at stable `1.18.10`, `Aspire.Hosting.Keycloak` `13.6.0-preview.1.26479.8`, and `CommunityToolkit.Aspire.Hosting.Dapr` `13.6.0-preview.1.261001-0243`; the Fluent UI V5 policy-surface catalog entry is `5.0.0`. SDK `10.0.401` and `Aspire.AppHost.Sdk` `13.5.3` remain Timesheets-owned pins. These are observed configuration values, not a new package-currency audit or a compatibility verdict. The audits below remain dated 2026-09-17 evidence; Story 5.2 owns their refresh. (Update 2026-10-20: the 2026-10-18 current package-currency verdict above is that refresh.) This Story 3.6 increment changed no dependencies or submodule pointers.

The 2026-10-03 runtime smoke failed: the first `aspire start` timed out during restore; a subsequent `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` started the AppHost, but `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18 because Timesheets failed to start. `aspire describe` reported Timesheets `Finished` and security `Unhealthy`. `aspire stop` completed cleanup. This supplies no passing live-topology or durable magic-link evidence; the existing infrastructure ownership and release decision remain.

The required pre-edit retry on 2026-10-04 started the AppHost with `--no-build`, but `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` again exited 18. Describe reported Timesheets Finished with exit code 134 and security Healthy; `aspire logs timesheets --tail 35` identified `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. `aspire stop` succeeded. This is a captured local port conflict, not a diagnosis of the historical October 3 failure or passing live-topology evidence.

**Superseded 2026-09-17 package-currency verdict (catalog rechecked 2026-10-10):** **CONCERNS**. Those audits ran under .NET SDK `10.0.401` (`rollForward: latestPatch`; `global.json` later moved to `10.0.402` in `c4695a5`) with `Aspire.AppHost.Sdk` `13.5.3` and `AspireUseCliBundle=true`; the CLI-bundle choice was reversed by the 2026-09-13 Option 2 decision and is applied by the 2026-10-18 evidence above. On 2026-09-17, `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore` reported no direct package updates for all 15 Timesheets projects, resolving `MinVer` `8.0.0`, ASP.NET Core MVC/DI and logging abstractions `10.0.12`, Extensions resilience/service discovery `10.10.0`, OpenTelemetry `1.18.0`, `coverlet.collector` `10.0.1`, `Microsoft.NET.Test.Sdk` `18.10.1`, `NSubstitute` `6.2.0`, `Shouldly` `4.3.0`, `xunit.runner.visualstudio` `4.0.0`, and `xunit.v3` `4.0.1`, with AppHost-generated direct assets at `13.5.3`. The vulnerable and deprecated `--include-transitive --no-restore` audits covered all 15 projects and were clean. Root npm was not applicable then: the root had no `package.json`, `package-lock.json`, `npm-shrinkwrap.json`, `pnpm-lock.yaml`, or `yarn.lock`. Solution-level and AppHost transitive-outdated evaluation failed exactly with `error: Sequence contains no matching element`; the other 14 project fallbacks reported drift including `ByteAether.Ulid` `1.4.0 -> 1.4.1`, `Google.Protobuf` `3.35.0 -> 3.36.1`, gRPC `2.80.0 -> 2.83.0`, `Microsoft.Bcl.Cryptography` `10.0.2 -> 10.0.12`, configuration environment variables `10.0.0 -> 10.0.12`, Polly `8.4.2 -> 8.8.0`, `System.Threading.RateLimiting` `8.0.0 -> 10.0.12`, and test-stack drift including Castle, DiffEngine, EmptyFiles, Application Insights, async interfaces, Microsoft.Testing `2.4.0 -> 2.4.1`, CodeDom, EventLog, and System.Management; no pin was added. The 2026-10-10 Story 3.6 catalog recheck recorded Aspire packages at stable `13.6.1`, the Dapr family at stable `1.18.10`, Keycloak `13.6.1-preview.1.26506.6`, CommunityToolkit Dapr `13.6.1-beta.935`, and Fluent UI V5 `5.0.1` as catalog evidence while the AppHost still pinned `13.5.3`; it was not a restored-graph audit.

## Works Checkout Ownership

This Timesheets repository is the umbrella pin for Works. The sole default checkout is `references/Hexalith.Works` (`<workspace>/references/Hexalith.Works`). A repository-root `Hexalith.Works` gitlink, `.gitmodules` path, or `$(MSBuildThisFileDirectory)Hexalith.Works` probe is forbidden. Do not initialize `Hexalith.Timesheets/Hexalith.Works`. A caller-supplied `HexalithWorksRoot` is preserved as read-only input; otherwise MSBuild resolves `references/Hexalith.Works` first and sibling `../Hexalith.Works` second. This section does not claim live Works host integration: default host wiring still denies live Work validation and planned-effort reporting until explicit opt-in.

| Dimension | Verdict | Evidence | Rationale |
|---|---|---|---|
| Default Works checkout | PASS | `.gitmodules` path `references/Hexalith.Works`; gitlink `160000` at that path; `Directory.Build.props` probes `references\Hexalith.Works` then `..\Hexalith.Works`. | This clone owns the umbrella pin. Stories 1.10 and 4.8 consume Works Contracts from that checkout. |
| Repository-root Works gitlink or probe | PASS | `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/WorksCheckoutGovernanceTests.cs` rejects a root `Hexalith.Works` path, gitlink, and `$(MSBuildThisFileDirectory)Hexalith.Works` probe. | No forbidden root checkout is present; a second checkout would silently bind a different Works commit. |
| Live Works host integration | waived | Unchanged Story 1.10 / 4.8 host defaults. | Approval, owners, risk, and revisit conditions are in [Launch-Scope Classification](#launch-scope-classification). Checkout ownership is not live host wiring. |

## Host Controls and Historical Runtime Scope

### Control for the domain-service route group

`src/Hexalith.Timesheets/Program.cs` calls `UseEventStoreDomainService()`, which maps the canonical
EventStore SDK routes — `POST /process`, `/replay-state`, `/query`, `/project`, `/project/v2`,
`/project/v2/reconcile`, `/project/rebuild/{v1,stage,commit,abort,verify}`, `/project/rebuild/shared/v1`
and `/admin/operational-index-metadata`. Each canonical route carries its exact workload policy;
the SDK fallback protects other unmarked routes. Public capability and metadata endpoints require
an exact-route opt-in accepted by the startup inventory. The port guard also confines SDK routes
to the internal listener, so a public-port request cannot reach a protected endpoint.

**Control: an enforced port split.** `InternalSurfaceGuard` runs before endpoint execution and refuses the
domain-service path prefixes on any port but the configured internal one, answering `404` rather
than `403` so a refusal does not confirm the route exists. It is fail-closed: with
`Timesheets:InternalSurface:Port` unset the surface is refused everywhere, so a deployment that
forgets to configure the split loses projection delivery instead of publishing a write surface.
`src/Hexalith.Timesheets.AppHost/Program.cs` declares the split — a `public` endpoint and a
non-proxied `internal` endpoint on port 8081 — and passes that port to the host, which is what makes
this a wired control rather than an assumption about deployment topology.

Historical evidence (2026-10-05): `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` proves all nine
protected routes and three mixed-case spellings are refused when nothing is configured, and that the
public magic-link and metadata routes stay reachable. `MagicLinkConfirmationHttpBoundaryTests` also
refuses lower- and mixed-case projection delivery on the configured public port without changing the
persisted index or catalog, then admits the same delivery on the internal port. Other in-process
journeys opt in explicitly via `AllowOnAnyPort`, because `TestServer` has no listener and reports
`Connection.LocalPort` as `0`. ASP.NET Core can match an endpoint before the guard runs; refusal
prevents that endpoint from executing. The 2026-10-09 in-process rerun passes public magic-link and metadata reachability and all guard cases. See the Magic-link HTTP no-disclosure gate.

**Host request-log privacy control (2026-10-05).** Before host construction, `Program.cs` filters
`Microsoft.AspNetCore.Hosting.Diagnostics` at Warning and post-configures existing filter predicates
to enforce that restriction through provider-specific overrides while preserving unrelated rules.
This suppresses Information request-start and request-finish messages containing query strings.
HTTP privacy assertions capture every logger category, checking raw token, hash, protected fixture identifiers and the exact sensitive fixture comment everywhere; generic vocabulary exclusions apply only to `Hexalith.Timesheets*` logs and response bodies. They exercise valid and denied query-token
requests on all four magic-link routes; the hosting-category regression checks that Information is
suppressed and Warning remains enabled. The capture harness omits log scopes and exception text,
although EventStore ServiceDefaults exports scopes; malformed-body and exception-path diagnostics
remain unverified, with both gaps tracked in deferred work.
This is the accepted interim host control. Shared EventStore ServiceDefaults query-string redaction
remains deferred, and this in-process evidence does not cover ingress or sidecar access logs.

**Historical live `aspire start` evidence (2026-09-15; predates the current Builds catalog), against real Kestrel listeners rather than TestServer.**
All resources reported `Healthy` (`aspire-dashboard`, `security`, both `security-*` parameters, and
the `timesheets` project on its `public` and `internal` endpoints); `aspire stop` completed cleanly.
Observed on the public listener (`:8080`): `GET /metadata/timesheets` → `200`; `GET` magic-link
confirm → `403` (the opaque denial, fail-closed with no EventStore resource); `POST /process`,
`/replay-state`, `/query`, `/project`, `/project/v2`, `/admin/operational-index-metadata` → `404`
on every one. On the internal listener (`:8081`): `POST /admin/operational-index-metadata` → `200`
and `POST /project/v2` → `400` (the SDK's own envelope validation), i.e. reachable. The split
was therefore verified end to end for that dependency graph and was not inferred from the in-process suite. This remains useful endpoint-split evidence, but it is not current Aspire/Keycloak runtime compatibility evidence after the Builds pointer change.

**Residual risk:** the guard matches by path prefix, so an SDK route added outside those prefixes
would not be covered; and `AllowOnAnyPort` republishes the surface if ever set in a deployed host.
`/health`, `/alive` and `/ready` remain unconditional under the SDK defaults, where the now-uncalled
`MapTimesheetsDefaultEndpoints` gated `/health` behind `IsDevelopment()`.

**Revisit condition:** the owning runtime/deployment story verifies the existing EventStore resource,
Dapr sidecars, listener split, and caller-identity access controls in the deployed topology. It must
review new SDK routes against the guarded prefixes and exclude deployment use of `AllowOnAnyPort`.
The resource already exists in the local AppHost; deployment qualification remains unfinished under
Stories 3.6/3.7. This documentation does not authorize additional topology.

Verdict vocabulary: `PASS`, `CONCERNS`, `FAIL`, `WAIVED`.

## Historical Receipts — preserved; superseded by Current Assessment

The following entries retain their original dates, source identities, counts, and then-current claims. They do not define current status, approval, or acceptance.

### Deployment and authority implementation — 2026-10-17

[Current acceptance](deployed-magic-link-2026-10-17.md) and the
[sanitized packet](../_bmad-output/implementation-artifacts/evidence/3-6/20261017t142841z-deployment-authority-runtime/README.md)
record actual native TLS/client identity and current access rights, absent Timesheets
installation, implemented Platform hosting packages and default-disabled EventStore
production resource binding. Six default suites pass 1,899 tests with five skips;
a fresh isolated persistence journey passes 1/1 with cleanup/preservation. Those
results do not supply deployed acceptance or certified legacy continuity/maintenance.
Fresh unsigned prerequisite 20261017t143202z expires 2026-10-18T14:32:02Z; the old
candidate remains expired and original verifier exit 1 is retained. Exact live argv
and forward-only recovery are prepared as blocked while qualified Release,
environment, issuer/provider and current recovery/legacy facts are unavailable.
Story 3.6/Epic 3/4.27 remain in progress, spec-23 draft, 4.1 closed and release FAIL.
No live mutation or operational approval occurred. Earlier entries are dated history.

### Owner policy and expired candidate — 2026-10-17

[Current reconciliation](../_bmad-output/implementation-artifacts/evidence/3-6/20261017t100251z-owner-policy-and-expiry/README.md)
records Platform's newer sole-owner documentary policy: the owner prepares and
reviews their own project documents without a distinct human reviewer, external
authority registry or new certificate/key. Deployed authorization and factual
acceptance requirements remain unchanged. Candidate `20261015t162756z` expired
at **2026-10-16T16:27:56Z**; no replacement or operational approval is ready.
The historical exact-verifier exit 1 is preserved. Story 3.6/Epic 3/4.27 remain
in progress, spec-23 draft, 4.1 operational gate closed and release **FAIL**.
Earlier observations below remain dated history.

### Native measurement adapter qualified offline — 2026-10-16

Explicit offline authorization produced a separate create-only supplemental
packet for RID `20261015t162756z`; original v4, signed originals, expired recovery
and all historical files remain unchanged. Supplemental SHA-256:
`a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631`. All **70 synthetic tests**, **13 LaunchReadiness** and
independent follow-up pass; **24 findings** were addressed. Six source/test files,
1,337 runtime files/nine libraries, 23 fixed GET selectors, typed suppression,
output custody, authority plan and stop/limit contracts are bound. Five selector
families remain unsupported. Earlier packet revisions and failure history persist.

The [current next prerequisite](../_bmad-output/implementation-artifacts/evidence/3-6/20261016t065614z-native-measurement-qualification/NEXT-PREREQUISITE.md) names unresolved server/client/TLS,
independent native authority/revocation, protected credential delivery and live
runtime/response bindings. **No executable measurement approval is ready**;
public live activation refuses before real config, credentials or sockets.
No authentication, network/API/provider/IAM request, preflight replay or
operational stage ran. The first integrity check at **06:56:14 UTC** matched
56 supplemental files, 140 v4 files and all 495 preservation files; candidate
was not expired. Its ceiling stays **2026-10-16T16:27:56Z** without refresh.
Story 3.6/Epic 3/4.27 stay in progress, spec-23 draft, 4.1 closed, release **FAIL**.
Earlier checkpoint entries retain their dated meaning.

Timesheets is neither story-complete nor launch-complete: Story 3.6 remains in progress, Story 5.2 remains in progress, and the final Story 5.1 reconciliation remains ready for development. The original classification was anchored at baseline `b0c41aaa3ad899ca13d49cd66e4f26f3b8e3ae5f` on 2026-06-26; that anchor does not scope the whole record, because later entries, sections, and rows carry their own dated baselines (for example, the package-currency verdict below is anchored at the 2026-10-18 worktree based on `180d9fd9a635ed44ba896688d6a06e38a33e9f28`, committed as `b85e810`, and carries dated 2026-10-20 updates from the round-7 rerun committed as `c5ac6005a48d973d96f7eff6496c72d0728432f0` and the round-8 rerun on the worktree based on `929ea22c977385590d77d95f73af86e8b051c4fc`, committed as `525859841e3dc329f65970b7067d3ef3bd1a91fa`). The record distinguishes concrete implemented behavior from unavailable defaults, skipped lanes, deferred integrations, package-currency evidence, and post-v1 decisions.

The **2026-10-15 19:51 UTC user-reported local preflight receipt** matches all
five reviewed v4 input bindings and reports schema pass with zero API calls;
authority, provider/IAM and execution gates remain closed. The unchanged packet
passes its offline recheck. The [current next prerequisite](../_bmad-output/implementation-artifacts/evidence/3-6/20261015t195103z-local-static-transport-receipt/NEXT-PREREQUISITE.md)
is offline qualification of exact measurement adapters before separate bounded
measurement approval. All 31 blockers persist; Story 3.6 remains in progress
and release **FAIL**. The receipt refreshes no policy/capture time; earlier
entries below retain their dated meanings.

The **2026-10-15 fresh 4.0 preparation** allocates unsigned candidate
`20261015t162756z` under explicit preparation-only authorization. It binds
local sources/tools/custody and preserves expired `20261014t112353z` unchanged.
The [current handoff](deployed-magic-link-platform-candidate-recheck.md#fresh-40-offline-policy-and-bundle-preparation--2026-10-15)
requires exact approval for the Administrator protected local static transport
preflight next. It grants no authentication or API/provider/IAM work. Missing
current measurements and fresh operational adapters remain blockers before
distinct measurement and signing/publication/execution approvals. The explicit
v4 packet passes offline checks; no operational stage ran.
Spec-23 stays draft, Story 3.6/Epic 3/4.27 stay in progress, 4.1's operational
gate stays closed and release remains **FAIL**. Historical entries below
retain their dated meanings.

The **2026-10-15 expired-recovery checkpoint** supersedes current use of the
historical acceptance below: recovery `20261014t112353z` expired at
**11:40:42 UTC**. All eight original signatures remain authentic; the actual
validation/readback and failure history are preserved. Concurrent offline
preparation is reused and grants no operational acceptance. The [current
handoff](deployed-magic-link-platform-candidate-recheck.md#expired-recovery-and-offline-prerequisite-preparation--2026-10-15)
requires separate exact authorization for fresh **4.0** policy/bundle and
signed recovery before operational **4.1** external-etcd/node/configuration
recovery and independent recovered-native access. No replacement bundle is
allocated, no timestamp extended, and no completed stage replayed. Existing
scope/incident/window/roles and separate retirement/upgrade/deployment approvals
remain in force within their exact limits. Spec-23 stays draft; 4.27/Story 3.6/
Epic 3 stay in progress, 4.1's operational gate closed and release **FAIL**.

The latest **11:29 UTC** reconciliation authenticates the concurrent final
Administrator M1/M2 attestation/signature and all eight signed originals, with
fresh matches for **16** exact immutable record/signature versions. Non-dry
validation passed **4,809/4,809**, all **4,714** manifest versions matching.
Fresh Story 4.0 recovery is accepted **only until 2026-10-15T11:40:42Z**.
The [current handoff](deployed-magic-link-platform-candidate-recheck.md#fresh-signed-recovery-accepted-within-deadline--2026-10-15-1129-utc)
names fresh 4.1 external-etcd/node/configuration recovery and independent
recovered-native access as the next separately scoped prerequisite; preparation
supplies no operational proof. On expiry preserve this RID and obtain separate
exact authorization for fresh recovery; no timestamp extension or automatic
replacement/replay. Retirement, upgrades and deployment keep separate exact
approvals. Story 3.6/Epic 3 and 4.27 stay in progress, 4.1 stays closed, spec-23
stays draft and release remains **FAIL**. Barriers below retain their dates.

The **2026-10-15 11:24 UTC** recovery reconciliation supersedes the dated
missing-signature barriers below. All four manifest/proof signatures and
publications exist; seven input signatures and all eight requested immutable
versions independently verify. The signed backup gate and concurrent non-dry
validation (**4,809/4,809**, **4,714** matching manifest versions) are complete.
Current M1/M2 preparation passes; the Administrator's honest final attestation
and validation signature remain missing. See [the current barrier](deployed-magic-link-platform-candidate-recheck.md#signed-recovery-reconciled-final-attestation-pending--2026-10-15-1124-utc)
for exact results and the existing protected action. Deadline remains
**2026-10-15T11:40:42Z**; expiry requires separately authorized fresh recovery,
preserving this RID. No completed stage was replayed. Retirement, upgrades and
deployment retain separate exact approvals. Spec-23 stays draft, Story 3.6/Epic 3
stay in progress, 4.1 stays closed and release remains **FAIL**.

The 2026-10-15 recovery checkpoint verifies authentic policy/cleanup signatures,
four current immutable readbacks and preserved source/cleanup identities.
The existing manifest and three restore proofs still need Administrator
signatures/publication; the backup gate, final non-dry validation and honest
M1/M2 remain pending. Policy freshness ends 2026-10-15T11:40:42Z. See
[the current checkpoint](deployed-magic-link-platform-candidate-recheck.md#signed-cleanup-and-concurrent-records-reconciled--2026-10-15)
for exact results, concurrent attribution, preserved failures and signing action.
Existing decisions persist; retirement, upgrades and deployment retain separate
exact approvals. No deployed acceptance gate closes. Spec-23 remains draft,
Story 3.6/Epic 3 remain in progress and release remains **FAIL**.

Related evidence:

The newer **10:20 UTC** reconciliation confirms all four manifest/proof
signatures and publications remain absent (eight provider HEADs **404**).
Current original signatures/readbacks, all 15 protected storage bindings and
resumed intake pass. Backup-gate signing, non-dry validation and honest current
Administrator M1/M2 remain pending. See [the current signature barrier](deployed-magic-link-platform-candidate-recheck.md#current-signature-and-publication-barrier--2026-10-15-1020-utc)
for exact results, the existing protected action and unchanged freshness limit.
This supplies no deployed acceptance or release-gate closure.

- `docs/performance-evidence.md` records NFR10/NFR11 measured in-process performance evidence and the deferred EventStore-backed wire-path waiver.
- `docs/boundary-decision-record.md` records the owns-vs-references boundary and the fail-closed sibling-module integration defaults.

Concurrent synchronization advanced Timesheets to
`e2a9e0898f5e30f2b9f3457ece6ddd55d4c999ba` and observed Platform to
`55bee680e6901a45946dd8bf1e4a4060952bd7ea`. Owner recovery continuation,
source resume and isolated cleanup are recorded; the cleanup still needs
protected Administrator signing and authentic final recovery validation.
No hosted Timesheets release, production providers or deployed acceptance
was added. [The reconciled checkpoint](deployed-magic-link-platform-candidate-recheck.md#latest-concurrent-owner-checkpoint)
preserves both source snapshots and distinguishes external owner outcomes
from this run's checks. Spec-23 stays draft, Story 3.6/Epic 3 stay in progress
and release stays **FAIL**.

The user-approved 2026-10-14 resume preserves existing working-tree changes
at Timesheets HEAD `9fa572c6afb0782bf434e98b6e1b30674eb265d2`, observing
Platform `4b9cb5624159de0782dd3329cdcc3b2d66010509` and EventStore
`210b90da172cc7ca946fbb06cfd44390bffa707b`. New configured Party JWT-read
admission supplies no Timesheets production bindings or registered providers.
Eight native reads at 19:26:22 UTC confirm unchanged census, absent staging
namespaces and unavailable Gateway type. Hosted release, production authority,
certified inventory/maintenance/continuity and installed subscriber/shared-rebuild
acceptance remain missing. See [the current recheck](deployed-magic-link-platform-candidate-recheck.md#latest-authorized-working-tree-recheck)
for exact identities, commands and limits. Spec-23 stays draft, Story 3.6/Epic 3
stay in progress and release stays **FAIL**. Focused Architecture verification
supplies document evidence only; no new solution build or local/deployed journey
is claimed. Earlier evidence remains dated below.

The latest 2026-10-14 recheck at Timesheets
`9fa572c6afb0782bf434e98b6e1b30674eb265d2` consumes Platform
`2bd78603e164265e3b6c00af9950429cff5510f9`. New offline retirement
preparation supplies no production plan, approval or accepted retirement.
Eight native reads still find both staging namespaces absent, no Gateway
resource type and an unchanged census. Hosted release, production providers,
certified inventory/maintenance/continuity and deployed subscriber/shared-rebuild
proof remain missing. See [the current recheck](deployed-magic-link-platform-candidate-recheck.md#latest-offline-retirement-preparation-recheck)
for exact source/receipt identities, commands and limits. Spec-23 stays draft;
Story 3.6/Epic 3 stay in progress and release stays **FAIL**. No new build or
local/deployed journey is claimed. Prior evidence remains dated below.

The earlier 2026-10-14 recheck at Timesheets
`9ecda28dc7ff540d83fad7ba1e5d585bdc33d0ae` consumes Platform
`92a02924894f6f2f219801a90e5bf25716abd028`. Its new staging dependency
handoff supplies upgrade preparation only. Fresh native reads confirm server
v1.34.9, both staging namespaces absent and no Gateway resource type. The
next Platform dependency is fresh signed recovery/readback and an exact 4.27
retirement plan/prerequisites/approval, followed by accepted retirement.
Hosted release, production bindings, certified inventory/maintenance/continuity
and deployed subscriber/shared-rebuild gates remain open. See
[the current recheck](deployed-magic-link-platform-candidate-recheck.md#latest-staging-dependency-recheck)
for exact commands, dependency ordering and bounded-discovery limits. No
Timesheets runtime fix was established; spec-23 stays draft, Story 3.6/Epic 3
stay in progress and release stays **FAIL**. No new build or local/deployed
journey is claimed. The following evidence retains its original identity.

The prior recheck at Timesheets `b9f968221a014c95869f3add8e16eda398b989c8`
consumes Platform `416878a74975b1023b3a00ab5d35aad4ce6365d9`, which adds
explicit Development/source Timesheets composition. Local listener/Dapr/read-model
bindings now exist; hosted staging enrollment, retained release artifacts and
production authority remain absent. Five fresh native reads found both proposed
namespaces NotFound and no matching workload names/images. Certified inventory,
maintenance/source continuity and deployed subscriber/shared-rebuild evidence
remain missing. [The candidate recheck](deployed-magic-link-platform-candidate-recheck.md#latest-development-binding-recheck)
records exact evidence and unchanged preparation/report hashes. Spec-23 remains
draft; Story 3.6/Epic 3 remain in progress and release stays **FAIL**. No new
runtime build or local/deployed journey is claimed by this documentation recheck.

The prior qualification at Timesheets
`aefd76772e84018442eb21b36549b8499a488721` consumes Platform
`ce6f2d34398803644b29d02ba57625898190ce20`, which now retains an offline
staging candidate outside active composition. Its proposed application/data
namespaces both returned NotFound; the native census still found no Timesheets
name/image matches. Offline preparation refused absent Platform-local nested
inputs and published no plan. Hosted deployment/release, production bindings,
certified inventory/maintenance/continuity and deployed subscriber/shared-rebuild
evidence remain missing. See [the candidate recheck](deployed-magic-link-platform-candidate-recheck.md)
for exact commands, retained preparation identity and discovery limits. Spec-23
remains draft; Story 3.6/Epic 3 remain in progress; release remains **FAIL**.
No current-source build or new local journey is claimed by this documentation
recheck. The following results retain their original dates and revisions.

Source `11976c2d8000bfc83f1d816a085836beecf8f6d1` has fresh
SDK 10.0.401 source-reference restore/build evidence, zero warnings/errors in
17.64s, and an unchanged isolated local journey passing **1/1 in 126.554s**.
Exact owned cleanup and shared-state/TTL/scheduler preservation passed. The
earlier metadata HTTP 500 did not recur, so the latest-tree local-proof gap
is closed; its historical cause remains unexplained. No runtime code or tests
changed and the six default suites were not rerun. The draft deployed
qualification identified Platform's designated cluster and existing native
access. A fresh read-only recheck at Timesheets
`28ffeaee83e9414ce9e205e57f674e4c29a6e40b` found no identifiable Timesheets
workload or production binding/inventory/migration providers. Application
enrollment and trusted evidence sources remain external prerequisites; see
[the qualification record](deployed-magic-link-acceptance.md) for exact
commands and bounded-discovery limits. Local mapped delivery does not
establish certified legacy
inventory, resource-binding/migration authority, enforced quiescence, source
continuity, pending-delivery recovery, or deployed subscriber/shared-rebuild
acceptance. Story 3.6 remains in progress; release remains **FAIL**.
Exact commands/results and evidence limits are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).
The following paragraphs retain historical evidence.

The recording-response recovery increment adds bounded observation after a
failed submit response and exact receipt checks for recording and terminal
transitions. Public POST denials now share GET privacy headers. The
source-reference Debug solution build using SDK 10.0.401 passed with zero
warnings/errors; six individual suites passed 1,899 tests with five declared
skips in 1,904 cases, and the focused HTTP selection passed 11/11. Independent
review corrections are verified. A fresh local Dapr attempt failed at
projection metadata HTTP 500 before domain commands.
Owned processes eventually exited and verified follow-up cleanup removed all
three private containers/components; shared DB0 changed during that cleanup
interval and no cause is attributed. A diagnostic retry refused a foreign
Works `eventstore` sidecar. This tree has no passing new live journey and
does not supply certified legacy inventory, migration/resource-binding
authority, source continuity, pending-delivery recovery, or deployed
subscriber/shared-rebuild acceptance. Story 3.6 remains in progress and
release readiness remains **FAIL**. Exact results are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

The earlier 2026-10-13 redaction tree enforces separate server authorization for
unredacted external comment display and keeps version-1 stamped comments
hidden on adjustment GET. SDK 10.0.401 solution restore/build passed with
zero warnings/errors; six individual test suites passed 1,872/1,877 cases
with five declared skips. The supported isolated Dapr journey passed 1/1 in
125.125 seconds with private-resource cleanup and retained shared-state
preservation; Python magic-link checks passed 65/65. Focused review checks
passed for HTTP 408 retry, v6 page bounds, negative index IDs, blank operator
requests, and token GET privacy headers. The broad suites and live journey
precede those small patches. Local proof does not certify the retained
shared v1 index, deployed migration authority, source continuity, or a
deployed shared rebuild. Story 3.6 and release readiness remain **FAIL**.

The reviewed 2026-10-13 isolated fixture passed its full local magic-link
journey 1/1 in 138.093 seconds using private resources, with preservation
and cleanup checks passing. Earlier-tree materialization handler tests passed
72/72 for Time Entry evidence and 56/56 for capability evidence; two focused
composed-route checks pass 1/1 each. This does not certify the retained shared
direct v1 index or a deployed migration. Cross-tenant inventory, installed
migration authority, source continuity, and deployed shared rebuild acceptance
remain open. Story 3.6 and release readiness remain **FAIL**.

The scoped v1-to-v7 operator migration now has local code-level evidence:
SDK `10.0.401` solution restore/build passed with zero warnings/errors, and
post-review migration/digest checks passed 64/64 and composed ingress passed
37/37. Three independent reviews are triaged and their patches verified;
the pre-existing source-owner continuity prerequisite is deferred. The final
six-suite run passed 1,867 tests with five expected skips and zero failures.
The migration is default-denied without independent complete cross-tenant
inventory, real operator authority and enforced source/delivery quiescence;
authoritative owner delivery continuity/handoff remains unestablished, and
the previously observed pending legacy journal requires verified recovery.
No live journey was run, and no deployed certification is claimed. Story 3.6
and release remain **FAIL**. Current commands and evidence are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

The earlier isolated 2026-10-12 source-reference restore/build with SDK `10.0.401`
passed with zero warnings/errors, and the narrow Python proof passed 9/9.
The isolated Timesheets AppHost had exactly one `eventstore` on app port
8082 and one `timesheets` on 8081. Its opt-in journey failed 0/1 in
76.410 seconds after catalog, non-fixture denial, and recorded Time Entry
effective-value/lineage checks. Index delivery reported
`delivery_state_unavailable` and the safe diagnosis
`legacy-direct-root=present-unverified entries=72`. Confirm/adjust and
restart acceptance were not reached. Only this AppHost was stopped;
Dapr/Aspire were empty afterward and Redis was not cleared. A certified
full cross-tenant legacy inventory and authorized migration operator are
absent; ordinary bootstrap and tenant-slice rebuild both reject the direct
v1 root. The two live tasks remain unchecked. Story 3.6 and release remain
**FAIL**; production authority and deployed coverage/shared-rebuild gates
remain open. Exact commands/results are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md).

Earlier 2026-10-12 isolated Timesheets runs had one `eventstore` on app
port 8082 and one `timesheets` on 8081. Three opt-in magic-link journeys
failed: 45.166 seconds at a recording-retry 403, then 79.433 seconds and
67.923 seconds at index `delivery_state_unavailable`. The shared store still
contains a direct v1 index of 72 entries, which the first V7 bootstrap
rejects before publishing a root. Redis was not cleared, and only that
AppHost was stopped. These runs do not supply deployed evidence. Story 3.6
and release remain **FAIL**; exact commands are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md).

The round-24 code-level index proof passed the focused numeric index class
3/3 after routing its 34,800-contribution, four-tenant test through the real
rebuild handler. Repeated A and B replacement/prune preserved C and D across
more than 400 selected pages and retained shared-hash ambiguity. A separate
401-page, one-shard selected directory resolved an interior candidate through
the concrete loader; all selected contributions from a 10,000-input one-shard
rebuild matched the input and resolved, with an interior loader check. The
full Projections lane passed 322/322 in 453.930 seconds. The
prior SDK retirement-cleanup failure remains intermittent and its batch
status is unknown. These local code-level results do not supply the blocked
live Dapr or deployed evidence. Story 3.6 and release remain **FAIL**; exact
commands and results are in [the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

The round-23 code-level work bounds the first V7 legacy-shard scan to 16
concurrent reads and adds a final-shard legacy guard test. The full default
Integration lane passed 702/707 with five declared skips before that final
edit and again on the final tree in 504.340 seconds; focused post-edit
Projection tests passed 3/3. Python V7 selection and effective evidence
checks pass 8/8. The latest live preflight found Folders' unrelated
`eventstore` Dapr ID, then Folders restarted within ten seconds of two
empty preflight reads. The Timesheets AppHost was not started. Those
two `/project/v2` failures were the latest local live outcome until the
isolated runs above. Story 3.6
and release remain **FAIL**; exact checks are in [the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md).

The round-22 worktree has a V7 copy-on-write global magic-link index with
tenant rosters and selected-generation rechecks. Its source-reference build,
Server suite, focused 10,000-owner rebuild, normal 10,000-candidate HTTP
delivery, and narrow Python checks pass. A full Projections run exposed a
failure in the new large tenant-replacement case during SDK retirement
cleanup. A fresh full diagnostic rerun passed 321/321, but the first
failure's batch status did not reproduce; at that point the tenant-bounded
rebuild task stayed open pending diagnosis. The
isolated latest-tree Dapr journey failed twice at capability-index projection
admission with `delivery_state_unavailable`, before selected candidate and
effective evidence readback. The Timesheets AppHost was stopped, Dapr and
Aspire were empty, and Redis was not cleared. Exact results are in
[the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md). Deployed inventory,
authority, and certified live shared rebuild remain unproven. Story 3.6 and
the release decision remain **FAIL**.

The round-20 worktree validates selected directory and collision page
integrity, compares persisted recording and adjustment comments against an
independent server decision, binds sessionless global Abort to operation
tenant, requires full replay on comment-policy enablement, and resolves a
65-hash mapped directory candidate through the concrete loader before and
after tenant rebuild. The warning-as-error source-reference solution build
and all six default Timesheets test executables passed. Focused 10,000-owner
capacity and mapped delivery cases passed. The latest-tree Dapr journey is
pending: an unrelated Folders Aspire runtime owns a Dapr `eventstore` ID,
so the unique-ID isolation preflight did not pass. That runtime was left
untouched; no Timesheets AppHost was started and Redis was not cleared.
Deployed gates and the overall release decision remain **FAIL**. Exact
current results are in [the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md).

The round-19 worktree closes five implementation tasks: bounded numeric
directory pages keep a maximum-length 10,000-owner single-shard index below
the selected-value limit; owner admission prevents earlier-page duplicate
issuance; adjustment display requires cleared redaction; global rebuild
retries bind operation ID to tenant; and the local Dapr fixture reports safe
valid-GET denial categories. The warning-as-error source-reference solution
build passed. Six default Timesheets lanes passed **1,759 cases with five
expected skips**. The final isolated Dapr journey passed **1/1 in 106.313
seconds** after bounded fixture waits accounted for EventStore's configured
60-second anonymous actor rate-limit window. Three earlier round-19 runs
failed at the second recording (403) and twice at the negative owner read
(503 from actor 429); the round-18 user-requested retry failure also remains recorded.
The AppHost was stopped and Dapr/Aspire were empty. Deployed inventory,
authority, and certified live shared rebuild remain open, so Story 3.6 and
release remain **FAIL**. Exact outcomes are in [the test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md)
and [local proof](local-magic-link-dapr-proof.md).

The round-18 worktree adds server-issued adjustment comment display, bounded numeric index pages for a 10,000-owner same-hash inventory, total 10,000-event mapped admission, and positive comment-bearing HTTP readbacks. A warning-as-error source-reference build passed, and the six default Timesheets lanes passed **1,753 cases with five expected skips** before a focused page-address correction. Focused tests after that correction passed. The isolated live Dapr journey then passed **1/1 in 118.195 seconds** with persisted confirm/adjust, race, restart, and selected readbacks; Dapr and Aspire were empty after stopping the Timesheets AppHost. Independently certified deployed inventory, authority, and live shared rebuild remain open, so Story 3.6 and release remain **FAIL**.

The 2026-10-12 round-17 worktree closes the seven post-review implementation tasks: token-hash ambiguity is durable and legacy unique-only indexes fail closed; malformed mapped issuance parks; comment publication requires a server-issued per-comment decision and versioned selected replay; EventStore preserves the prior global rebuild tenant, complete-prefix MessageId uniqueness, and safe duplicate-domain catalog rejection; a nonempty Time Entry staged commit is retried after host restart with exact selected lineage. The source-reference Debug solution build passes with zero warnings. Architecture 56/56, Contracts 92/92, Server 530/530, Works 76/76, and Projections 304/304 pass. The Integration full run exposed two assertions expecting the old unstamped comment shape; both and the new restart case pass after the assertion update. SDK Client and DomainService broad runs retain one cleanup-timing and one absent nested-Tenants guardrail failure respectively. The SDK Server full run had one anchored-suffix fixture failure; after adding the required MessageId digest to that fixture, its idempotency class passes 26/26. This is in-process evidence. Deployed owner inventory, installation authority, and certified live shared rebuild remain open; Story 3.6 and release remain **FAIL**. Exact commands and limits are in [the Story 3.6 test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

The round-17 isolated local Dapr journey then passed **1/1 in 127.469 seconds** with one `eventstore` and one `timesheets` app ID, persisted confirm/adjust, restart, and selected readbacks. Dapr and Aspire were empty after stopping the Timesheets AppHost. This local pass does not close the deployed gates above.

The 2026-10-12 round-16 source-reference solution build passed with
`-warnaserror`, zero warnings, and zero errors. All six default executables
passed on the current tree: Architecture 56/56, Contracts 92/92, Server
523/523, Works 76/76, Projections 301/301, and Integration 687 passed with
five expected skips out of 692: **1,735 passed, five skipped, zero failed**.
The composed and SDK tests cover a selected 256-shard global token index
with 10,000 high-entropy candidates, tenant-safe prune, incremental mapped
Time Entry delivery, failed commit/restart, and authenticated host readback.
The actor-grounded 10,000-owner composed producer passed. The isolated local
Dapr journey passed **1/1 in 116.727 seconds** after its fixture was updated
to read the selected global shard; its first attempt failed at the retired
single-key readback. Dapr and Aspire were empty after stopping this AppHost.
This local proof does not certify deployed authority, independent source
coverage, or a certified live shared rebuild. SDK changes remain uncommitted
worktree code; Story 3.6 and release remain `FAIL`.

Historically, the 2026-10-12 round-15 source-reference solution build passed with
`-warnaserror`, zero warnings, and zero errors. All six default executables
passed: Architecture 56/56, Contracts 92/92, Server 523/523, Works 76/76,
Projections 293/293, and Integration 682 passed with five declared skips out
of 687: **1,722 passed, five skipped, zero failed**. Focused EventStore
coordinator 28/28, retry worker 10/10, orchestrator 79/79, catalog 8/8,
DomainService host 89/89, and part-route authorization 14/14 passed. The
normal source path delivered over 64 MiB from bounded actor pages without the
legacy whole-history array; composed tests covered 10,000-event mapped replay,
all three Timesheets named routes, partial failure, and durable retry.

The round-15 isolated local magic-link Dapr journey passed **1/1 in 112.376
seconds** on 2026-10-12, with unique `eventstore` and `timesheets` Dapr IDs,
persisted confirm/adjust, one-winner race, restart, and exact selected
readbacks. Both Dapr and Aspire were empty after stopping this AppHost. This
local proof does not certify deployed authority, independent source coverage,
or a certified live shared rebuild. The SDK changes remain uncommitted worktree
code; Story 3.6 and release remain open.

Historically, the 2026-10-12 round-14 Timesheets source-reference solution build passed
with `-warnaserror`, zero warnings, and zero errors. All six default
executables passed: Architecture 56/56, Contracts 92/92, Server 523/523,
Works 76/76, Projections 274/274, and Integration 682 passed with five
declared skips out of 687. Overall: **1,703 passed, five skipped, zero
failed**. EventStore's partitioned rebuild class passed 32/32, source
producer 17/17, and roster actor 7/7; the actor-grounded Timesheets
integration passed 2/2, including a 10,000-owner case. Both repositories
pass `git diff --check`. The SDK changes remain uncommitted worktree code.

The round-14 isolated local magic-link Dapr journey passed **1/1 in
67.664 seconds** on 2026-10-12 after a bounded retry for the new exact
post-restart lineage probe. The first run failed 0/1 after the EventStore
restart when that read-only stream probe hit a transient HTTP/Dapr transport
failure. The passing rerun verified persisted confirm and adjust, a
one-winner race, restart, exact selected Time Entry lineage, and capability
readbacks. Strict pre/post Dapr checks found one `eventstore` and one
`timesheets` app ID; Dapr and Aspire were empty after stopping this AppHost.
[The local proof](local-magic-link-dapr-proof.md) records commands and limits.
It does not certify deployed authority, shared-rebuild source coverage, or a
live shared Begin/rebuild.

Historically, the 2026-10-12 round-13 Timesheets source-reference solution build passed
with `-warnaserror`, zero warnings, and zero errors. All six default
executables passed: Architecture 56/56, Contracts 92/92, Server 523/523,
Works 76/76, Projections 267/267, and Integration 679 passed with five
declared skips out of 684. Overall: **1,693 passed, five skipped, zero
failed**. Focused EventStore tests also passed for paged Begin, 1,001
colliding source owners, and durable rebuild retry. Both repositories pass
`git diff --check`. These results are worktree evidence; the EventStore SDK
changes are uncommitted.

Historically, the round-13 isolated local magic-link Dapr journey passed **1/1 in
125.174 seconds** on 2026-10-12. Strict pre/post checks found exactly one
`eventstore` and one `timesheets` Dapr app ID; both Dapr and Aspire were empty
after stopping the Timesheets AppHost. The fixture verified persisted confirm
and adjust, a one-winner race, EventStore restart, and exact selected Time
Entry and capability readbacks. [The local proof](local-magic-link-dapr-proof.md)
records the command and limits. This local run does not certify deployed
authority, shared-rebuild source coverage, or live shared Begin/rebuild.

Historically, the round-11 isolated local magic-link Dapr journey passed **1/1 in
114.866 seconds** on 2026-10-11. Strict pre/post checks each found exactly
one `eventstore` and one `timesheets` app ID; both Dapr and Aspire were empty
after stopping the Timesheets AppHost. The fixture verified persisted confirm
and adjust, a one-winner race, restart, and exact selected Time Entry and
capability readbacks with real persisted global positions. Two preceding
current-tree race attempts failed `[403,403]`: one after EventStore had stored
use/confirmation/rejected competitor, fixed by concurrent exact-status
polling and stored-event readback; one before use was submitted because signed
workload reads were rate-limited in the anonymous bucket, fixed by a finite
verified-workload partition. [The local proof](local-magic-link-dapr-proof.md)
records the command, narrow regressions, and limits. It predates the round-12
SDK changes; the current-tree round-13 run is recorded above.

The 2026-10-12 round-12 worktree builds the Timesheets source-reference
solution with `-warnaserror` and zero warnings or errors. Architecture,
Contracts, Server, Works, Projections, and Integration executables passed
56/56, 92/92, 523/523, 76/76, 264/264, and 675 passed with five declared
skips: **1,686 passed, five skipped, zero failed**. The full Integration run
preceded the final EventStore hash-page edit; final-tree composed near-limit
commit/readback and hash-page tamper/cleanup tests passed afterward.
EventStore's partitioned DomainService class passed 30/30 and the producer
and gateway class passed 16/16. The source producer now bounds history memory
and serialized Dapr requests; the opted-in Timesheets handler streams owner
contributions. Scratch chunk hashes are paged so the maximum modeled
30,008-chunk manifest passes the actual 1-MiB fenced SDK batch gate. A
10,000-owner, three-events-each producer case and near-1-MiB event/terminal
cases passed. The round-12 worktree had no new live Dapr run; the round-13
local pass is recorded above.

The 2026-10-11 round-11 worktree adds an EventStore actor-owned shared Begin
producer, a partitioned source roster and write/capture fence, a
GlobalAdministrator operator route, durable restart stages, and completed
erasure global-position evidence. Composed Timesheets capability rebuilds
passed with two and 10,000 high-entropy owners; the two-owner case also
pruned its previously selected row after actor-reported erasure. The source
producer denies domains without mandatory writer-fence configuration, and
installation coverage authority defaults deny. No independent virgin-store
certificate, all-writer-enforcement receipt, or complete deployed legacy
actor-key census is available, and no certified live shared-rebuild Dapr run
has passed.
The Debug source-reference solution built with zero warnings and errors.
All six current Timesheets lanes passed with 1,676 passed and five declared
skips; affected EventStore Client, DomainService, and Server subsets passed
87/87, 43/43, and 61/61. These results do not certify deployed source
coverage or a live shared rebuild.
The deployed shared rebuild and Story 3.6 release gates remain **FAIL**.

Historically, the round-10 worktree built with zero warnings and errors in Debug
source-reference mode using the documented Commons-root override. All six
default Timesheets suites pass: Architecture 56/56, Contracts 92/92, Server
523/523, Projections 255/255, Works 76/76, and
Integration 669 passed with five declared skips: **1,671 passed,
five skipped, zero failed**. The Projections full run includes the
greater-than-64-MiB capacity case. EventStore Client passes 1,487/1,487 and
DomainService passes 732/734, retaining two historical guardrail/catalog
failures. The 10,000-owner scratch cleanup regression passes 1/1, including
selected reads and live delivery during recoverable cleanup. SDK tests also
cover actor-proven empty Time Entry deletion and sealed erased-owner capture.
The production Begin inventory producer is absent from this checkout, so the
shared erasure authority remained contractual and deployed proof was open.
At that checkpoint the round-10 live Dapr rerun was held and the earlier
round-9 issuance HTTP 403 was the latest live observation. The current
producer and latest-tree local pass are recorded above. Story 3.6 remains in
progress and launch remains **FAIL**.

The earlier round-9 read-model worktree built with zero warnings and errors
using the documented Commons-root source-reference override. The EventStore
Client suite passed **1,487/1,487** and DomainService passed **727/729**,
with only the same two previously recorded nested-Tenants guardrail and
named-endpoint catalog-registry failures. Current Timesheets Architecture,
Contracts, Server, Projections, Works, and Integration suites passed
**56/56, 92/92, 523/523, 252/252, 76/76, and 669 passed with five expected
skips**: 1,668 passed, five skipped, zero failed. Projections ran as 251
standard cases and one separate greater-than-64-MiB capacity case; the
changed Time Entry handler class was rerun 36/36 after the final test-helper
edit. EventStore stream controller tests passed 26/26; the broad EventStore
Server run was stopped after roughly seven CPU-heavy minutes without a result.
The latest opt-in local attempt, on an earlier round-9 checkpoint, failed
**0/1 in 83.705 seconds** after catalog persistence and
non-fixture denial, at capability issuance HTTP 403; its diagnostic stream
read returned 404. Timesheets logged EventStore stream-read timeouts, while
EventStore logged rate limiting and projection retry activity. One
`eventstore` ID was present during the run; the cause of the opaque denial
remains unproved. The Timesheets AppHost was stopped. The current tree has not
been rerun live after the captured-envelope and rebuild-head changes while
the unrelated duplicate Dapr app ID is being cleared. Local acceptance remains
provisional and launch remains **FAIL**.

The preserved round-8 read-model tree passed all six default Timesheets suites:
**1,652 passed, five expected skips**. EventStore Client passed 1,482/1,482;
DomainService passed 725/727, retaining the two previously documented
nested-Tenants and catalog-registry failures. Focused EventStore partitioned,
fenced, and coordinator/streamed suites passed 16/16, 17/17, and 23/23.
New tests cover lossless 10,000-event Time Entry delivery and mapped rebuild,
event-envelope lineage, capture continuity, cancellation, and private scratch
closeout. Six Timesheets test projects and the AppHost built with zero warnings
or errors using the recorded Commons-root override. The unmodified
source-reference solution command still fails with duplicate
`Hexalith.Commons.UniqueIds` CS1704.

Three round-8 opt-in Dapr attempts began with one healthy Timesheets and
one healthy EventStore app. They passed catalog persistence and non-fixture
denial; the furthest run also passed the confirm race and composed Time Entry
rebuild readback. The attempts failed, respectively, on capability issuance
HTTP 403 (77.897 seconds), a confirm race with two HTTP 403 responses
(31.062 seconds), and adjust issuance HTTP 403 after confirm passed
(31.637 seconds). EventStore logs showed rate limiting and named-projection
retry activity, but do not prove the cause of these opaque denials. The
Timesheets AppHost was stopped after the runs. Local acceptance remains
provisional; production binding issuance, deployed read-model wiring,
history inventory, and deployed acceptance remain open. Story 3.6 stays
in progress and launch remains **FAIL**.

An earlier 2026-10-11 materialized read-model tree passed all six default
Timesheets test executables with **1,639 passed and five expected skips**.
Always-run composed-host tests persisted capability issuance/use through the
mapped `/project/v2` route and deleted an existing Time Entry row through an
applied full replay. EventStore Client passed 1,479/1,479 and focused
DomainService rebuild/dispatch tests passed 55/55; the full DomainService run
retained two previously recorded unrelated failures (581/583). That
checkpoint's opt-in Dapr journey had no clean pass: it reached persisted catalog delivery,
then failed its combined non-fixture issue/missing-owner assertion while an
unrelated ChatBot AppHost introduced a duplicate `eventstore` Dapr ID. A
diagnostic proved duplicate routing can make the signed missing-owner read
alternate between HTTP 500 and 404; the non-fixture issuance still returned
403. The captured failing assertion did not retain both status codes, so its
exact failing leg remains unknown. A follow-up attempt stopped at strict
duplicate-ID preflight. A final clean-preflight run passed persisted catalog,
non-fixture denial, both composed Time Entry rebuild readbacks, confirm race,
and adjust journey, then exited 1 on HTTP 401 reading an owner stream after
EventStore restart; a duplicate `eventstore` ID had appeared again during the
run. The Timesheets AppHost was stopped and ChatBot was left untouched. Local
acceptance is provisional; production binding issuance,
deployed read-model wiring, history inventory, and deployed acceptance remain
open. Story 3.6 stays in progress and launch remains **FAIL**.

The 2026-10-12 numeric global-index increment replaces fresh v7 tenant-slice
rebuild's global owner materialization with copy-on-write trie paths and a
paged, 10,000-owner tenant roster. Fresh ordinary issuance starts v7 after
checking for selected v6 shards and legacy single-root state; existing v6
shards keep their selected ordinary path until migration, and single-root
state fails closed. A separate internal operator migration can
validate selected v6 shards, stage tenant rosters under private numeric
checkpoints, then switch one v7 root. It scans the entire selected v6 index
once per tenant, so operator duration and owner-sequenced delivery backpressure
grow as O(tenants × global rows); schedule it in a maintenance window. The
`/admin/timesheets/magic-link-index/migrate-v6` route requires the internal
listener, authenticated operator access, and a server-side certified-inventory
authority. Its default authority denies migration. No deployed authority or
certified inventory receipt is bound, so this code does not change the
**FAIL** release gate. Selected v6 shard, page, and owner-admission values are
retired after v7 promotion; older platform-retained generations remain subject
to the EventStore retention contract. The local Dapr journey is still blocked
by the unrelated Folders duplicate `eventstore` ID and is not claimed here.

An earlier 2026-10-11 [opt-in local Dapr journey](local-magic-link-dapr-proof.md#run-the-persisted-journey)
passed 1/1. It exercised real EventStore actors and persisted stream readback,
SDK-delivered Fresh catalog and token index, confirm and adjust effects,
recording retry, a one-winner race, and replay after EventStore restart.
Development-only signed fixture identities were used. This closes the local
persistence evidence gap, while production binding issuance, deployed read-model
wiring, deployed-history inventory and deployed acceptance remain open.
Story 3.6 stays in progress and launch remains **FAIL**. Older preflight and
delivery-only observations below remain dated history.

A prior 2026-10-11 materialized read-model revision has in-process SDK
coverage for Time Entry evidence, generation-selected capability joins,
poison-event parking, retryable selected-read outages, high-entropy
10,000-owner partitioned rebuild, large single-owner and capability values,
and applied-generation pruning. Owner-sequence handoff and private scratch
cleanup passed focused restart and retry tests. All six default Timesheets
executables passed with 1,634 passed and five skipped. An always-on
composed-host test also committed the named Time Entry rebuild through the
mapped endpoint and read back persisted evidence. The isolated opt-in
Dapr fixture's 1/1 passing run in 160.146 seconds predates that
checkpoint. Two then-current isolated attempts reached stored delivery but stopped on
command-status HTTP 500 and repeated opaque adjust POST 403 responses. ChatBot
then restarted with a duplicate `eventstore` Dapr ID, blocking a further run
at preflight. A later run reached catalog delivery and a stored recording,
then received HTTP 429 on the first capability stream read when ChatBot
restarted with a duplicate `eventstore` ID. That host was left untouched and
the Timesheets AppHost was stopped. This
is local proof only. Story 3.6 stays in progress and launch remains **FAIL**
for production identity issuance, deployed read-model wiring, deployed-history
inventory, and deployed acceptance.

The 2026-10-10 Story 3.6 recording increment keeps the overall release decision at **FAIL**. The external submission route now sends recording to the keyed Timesheets EventStore processor and acknowledges only a timely `Completed` status with matching stored events. Local HTTP and actor tests prove record-then-confirm, actor recreation, one recording across retries, policy-required submission evidence, and opaque denial on uncertain receipts. Valid confirm and adjust submissions still require the same verified EventStore use batch. The local tests do not establish live Dapr persistence, production binding issuance, deployed-history inventory, or live host acceptance.

The 2026-10-11 [local Dapr preflight](local-magic-link-dapr-proof.md) found no `timesheets` Dapr app ID in `dapr list`. The running EventStore AppHost belonged to a separate integration test and was left untouched. The checked-in EventStore AppHost does not attach Timesheets or allow-list its trusted effects, and the Timesheets AppHost has no EventStore resource or sidecar. The local `statestore` scope excludes Timesheets. The spec therefore stopped before a live test/fixture or persisted journey; no local Dapr acceptance is claimed. Story 3.6 remains in progress and the release decision remains **FAIL**.

A later 2026-10-11 [connected local topology proof](local-magic-link-dapr-proof.md#connected-local-topology-proof-on-2026-10-11) started distinct healthy EventStore and Timesheets Dapr applications. Signed command/status/stream calls, signed synthetic SDK delivery and persisted readback for both catalog and candidate index, and Timesheets actor-store denial passed. This proves the local delivery infrastructure; no actor-originated Timesheets event or complete magic-link acceptance journey passed. Story 3.6 remains in progress and release remains **FAIL**.

Story 5.3 review-patch evidence was rerun on 2026-09-12 from the worktree based on `5b56d289c79fa040a306a0db700e9d1f42733baf`, using SDK `10.0.400` and the approved expanded submodule-pin scope: Builds `a32cb422749352cce8dec948aa3e78c8f00eb4cf`, Conversations `73bcee6f04479d4743d5a65ce929728e22687d7d`, EventStore `6b0247acc0b3ef60eb00c0f9ac9cbd367f85da20`, FrontComposer `1b3608c9b039dbba1be0884d92a2a6d54054e370`, Projects `4f05a352edd67c4d5595913ee584539c1948dd58`, and Tenants `2fac18396ff11a4459de053b3ebb7ddfe7c13e30`. Works remained pinned at `42c43180df201883512369c680db1d091f57b02d`.


#### Earlier release verification and decision table (historical)

Current durable-submission worktree verification (2026-10-10): source-reference Debug restore and warning-as-error build pass. All six Timesheets lanes pass (1561 tests passed, four declared skips). A signed, tenant- and domain-bound EventStore workload channel carries command submit, message-primary status, and stream read; human routes retain their policies. The Timesheets HTTP fixture proves valid confirm/adjust and issue/revoke/expire only after Completed status and stored-event readback, one-winner concurrency, legacy terminal denial, and opaque non-Completed outcomes. The protected real `/process` route dispatches to the keyed processor. Separate EventStore actor state-store tests prove local commit/reload for both effects and proposed-entry terminal markers. Actor-clock regressions reject expiry reached during awaited issuance authorization or either use action; undefined numeric link actions are rejected, including after JSON deserialization. The authority-mode workload issuer cannot bind resource claims, so it fails closed and requires a trusted resource-binding issuer before deployment. No live Dapr-backed EventStore acceptance has run; deployed histories and old-writer shutdown remain release gates.

Current fail-closed verification (2026-10-10), worktree based on `61d6fc7a31544985bed5458972f81b7050b9c775`: source-reference Debug restore/build passed with zero warnings/errors. Focused loader 80/80 and HTTP boundary 462/462 passed. Six individual lanes ran 1425 tests: 1420 pass, one pre-existing Architecture package-verdict failure, four intentional skips. Valid GET remains 200; valid confirm/adjust POSTs return the same opaque 403 as invalid tokens, with zero gateway submit attempts in the fixture. Well-formed non-Fresh catalog status is kept internally with no usable authority or detail. A read-only inventory could not query deployed streams: the AppHost has no EventStore resource, no deployed endpoint or read-only access is provided, and no relevant credentials are exposed in the environment. Whether deployed capability and Time Entry histories exist is unknown. Durable single-use, persisted concurrency, and live-host proof remain open. Release remains `FAIL`. Exact commands and limits are in [the Story 3.6 test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

Current post-review verification (2026-10-09), Timesheets `61dc18bdb3e28ed8bbee64488954b8f42b90c4e7` with local EventStore `f87affaefef5693fd80452eea02999e46fa1888b`: the SDK focused inventory/public-route selection passed 22/22, the ExtensionsTests class 81/81, and the full SDK lane 548/549 with one absent nested-Tenants guardrail subject. Source-reference Debug restore/build passed with zero warnings/errors; both HTTP classes passed 482/482; all four scratch-root HTTP privacy cases passed. Six individual Timesheets lanes: 1415 total, 1410 pass, one existing Architecture package-verdict failure, four intentional skips. The new EventStore commit is not in local `origin/main` and has not been pushed, so the Timesheets pointer is not yet proven fetchable by a fresh clone. This is in-process evidence; durable submission and live-topology proof remain open. Release remains `FAIL`. Exact commands and captures are in [the Story 3.6 test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

Prior upstream-pointer verification (2026-10-09), Timesheets `46265ba71138617ac387bd68e63aa0d7e5685947` with EventStore `fe4e7559f17ed53eea005b0ae5a4f6a358221780`: the SDK focused inventory/public-route selection passed 19/19, the ExtensionsTests class 78/78, and the full SDK lane 545/546 with one absent nested-Tenants guardrail subject. Source-reference Debug restore/build passed with zero warnings/errors; both HTTP classes passed 482/482; all four scratch-root HTTP privacy cases passed. Six individual Timesheets lanes: 1415 total, 1410 pass, one existing Architecture package-verdict failure, four intentional skips. That EventStore commit is contained in local `origin/main`; no fresh fetch or push is claimed. This is in-process evidence; durable submission and live-topology proof remain open. Release remains `FAIL`.

Prior public-route compatibility verification (2026-10-09), changed worktree based on `3e1ca73ba51f0c91467b74ac3dc85f88b6526b7e`: the EventStore SDK focused public-route and startup tests passed 24/24; source-reference Debug restore/build passed with zero warnings/errors; both requested HTTP classes passed 482/482; all four scratch-root HTTP privacy cases passed. Six individual Timesheets lanes: 1415 total, 1410 pass, one pre-existing Architecture package-verdict failure, four intentional skips. Aspire start exited 0, wait exited 18 because APP_API_TOKEN and JWT Authority/SigningKey are missing, and stop exited 0. This is in-process evidence; durable submission and live-topology proof remain open. Release remains `FAIL`.

Prior Story 3.6 spec-17 verification (2026-10-09), changed worktree based on `c75de3c2f6328e63e77682b6d3cac6c54a7ded87`: source-reference Debug restore/build passed with zero warnings/errors; the privacy helper theory passed 432/432 with tenant identifiers permitted in diagnostics and forbidden in external bodies. Six individual lanes: 1408 total, 1361 pass, 43 failures, four existing skips. The 42 HTTP failures remain unexpected 401s, and Architecture has the unchanged package-verdict failure. Four scratch-root HTTP cases reran against this build and failed at 401 before privacy assertions. AppHost start succeeded, wait exited 18 on missing internal credentials, and stop succeeded. The EventStore route contract and a passing HTTP rerun remain deferred. Release remains `FAIL`.

Prior Story 3.6 spec-16 final post-review patch verification (2026-10-09), changed worktree based on `bb7fe1fb93681a0c77eb8eab76a19e2232326def`: the README source-reference Debug restore/build passed with zero warnings/errors. The privacy assertion theory passed 420/420 without HTTP or a running host, including exact fixture-comment protection in every category. All six individual test lanes ran: 1396 total, 1349 pass, 43 failures and four existing skips. The same 42 HTTP authorization failures and one stale package-verdict architecture failure remain; the four scratch-root HTTP privacy cases returned unexpected 401s in the pre-review run, before the post-review rebuild. Since `da172a8effe484be96c17aaef1c007e1ecb8f27c`, every external magic-link request and `/metadata/timesheets` returns 401, regressing live-host AC1/AC3/AC4. The EventStore route contract fix and required scratch-root rerun remain deferred. Explicit-AppHost start succeeded, wait exited 18 because internal credentials are missing, and stop succeeded. Independent review corrected eight test/record findings and deferred the pre-existing numeric-substring diagnostic false-positive risk. Release remains `FAIL`.

Prior Story 3.6 privacy-assertion and record-patch verification (2026-10-08), changed worktree based on `aba488b18c4a4b96e08442538c41481c752c0382`: source-reference Debug restore/build passed with zero warnings/errors, but the six individual test lanes report 976 total, 929 pass, 43 failures and four existing skips. Architecture has one stale package-verdict failure (current imported Aspire 13.6.1 versus the dated 13.6.0 text); Integration has 42 unexpected 401s after the prior EventStore pointer update introduced workload fallback authorization. This blocks the HTTP privacy regression before it reaches the corrected log assertions. Independent helper verification passed 144 checks and does not establish HTTP-boundary conformance. The current explicit-AppHost wait exited 18; logs report missing APP_API_TOKEN and JWT Authority/SigningKey configuration, and stop succeeded. Public capability-route integration with the SDK, authenticated internal fixtures and current runtime credentials must be reconciled; no authorization rule was bypassed. Release remains `FAIL`.

Prior Story 3.6 holistic production-review patch closure (2026-10-05), changed worktree based on `ee87e39f9f43e442e5426bb090afbe70be26492a`: all thirteen accepted patches are implemented with individual closure evidence in the story. The prescribed source-reference Debug restore/build passed with zero warnings/errors, and all six test executables ran individually: 976 total, 972 pass, four existing skips, zero failures. The closure run before independent review passed focused loader/projection/HTTP-guard checks at 72/72, 101/101 and 39/39. The paging regressions rejected both temporary weakenings (throw-to-break: two failures; removed latest-sequence continuation: one failure); loader source was restored byte-for-byte and rebuilt. Independent review reproduced and fixed provider-specific Information overrides and null-id issuance; four additional HTTP cases preserve unrelated logging and valid issuance while proving early opaque denial. Three pre-existing findings were appended to the ledger. Administrator claims tests cover fallback and conflicting evidence using an assigned test principal and substituted authorization guard, so they do not prove authenticated deployment. The approved spec's pre-edit AppHost baseline remains blocked: `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 with port 8080 occupied, and `aspire stop` succeeded. Durable atomic submission, concurrent single-use, live-topology evidence, non-Fresh distinctions and deployed Activity Type inventory remain open; release remains `FAIL`.

Prior Story 3.6 review-patch increment (2026-10-05), changed worktree based on `c5fcf987046018c980c5def7c2d3e1a9ce310abe`: the source-reference Debug restore/build passed with zero warnings and errors, and all six individual test executables passed (957 total, 953 pass, four declared skips). The eight current review patches are closed, including fail-closed internal-surface defaults and non-monotonic MessageId ordering regressions. The pre-edit Aspire start succeeded but Timesheets finished with exit code 134, so there is still no live persisted EventStore proof. Durable writes, non-Fresh freshness distinctions and deployed Activity Type inventory remain open; the release decision remains `FAIL`.

Prior Story 3.6 historical-review reconciliation (2026-10-04), changed worktree based on `2b03356b4866900559c772c402301ac89c084ef5`: the matching README source-reference Debug restore/build passed with zero warnings/errors, and all six individual test projects passed (957 total, 953 pass, four declared skips, zero failures). The new complete-bundle regression covers ordered, reversed and shuffled/duplicated capability and Time Entry lifecycle events. All 37 historical unchecked patches have evidence-backed dispositions: 28 implemented/superseded, nine retaining deferred work. Checked Defer items represent triage, not implementation completion; deployed ownership inventory, live EventStore topology, explicit non-Fresh diagnostic distinctions and durable single-use persistence remain open. The pre-edit Aspire start succeeded, but the explicit-path `aspire wait timesheets --timeout 30` exited 18; Timesheets exited 134 because port 8080 was already in use, and `aspire stop` succeeded. The production source, configuration verifier and submodule pointers are unchanged; the prior mutation results below remain dated evidence and were not rerun for this documentation/test increment.

Prior Story 3.6 alternate-configuration and literal-parsing increment (2026-10-04), changed worktree based on `30fc6d3092cbb376c500176ceeef351109e3fa7f`: reverified the matching source-reference Debug restore/build commands below with zero warnings/errors and all six individual test lanes (956 total, 952 pass, four declared skips, zero failures). The initial ArchitectureTests catalog-reader failure was corrected by resolving the shared unconditional version property; its focused rebuild and 55/55 rerun passed; the added element/ancestor-condition regression raises the final ArchitectureTests lane to 56/56. The strengthened source check rejected 21 configuration mutations, including colon-key and command-line Port overrides, host Configure/in-memory AllowOnAnyPort bypasses, both interpolated-verbatim URL-hidden override forms, actual end-of-line AppHost comments, and the retained casing/comment/directive cases. Baseline, eight harmless literal/URL controls and restored-source checks passed. The exact mutations and runner are retained in `_bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py`; both production sources were restored byte-for-byte. This is bounded lexical source fitness evidence with the existing nested-interpolation and runtime/deployment-configuration limitations. Escaped or computed configuration keys and escaped property identifiers also remain semantic-analysis limits; the exact passing counterexamples and their owner are recorded in the [alternate-configuration deferred-work entry](../_bmad-output/implementation-artifacts/deferred-work.md#deferred-from-alternate-configuration-and-literal-parsing-patches-for-story-36-2026-10-04). Unrelated InternalSurface URLs now pass, and block-comment/directive substitutes retain the terminating semicolon outside the disabled region. The pre-edit explicit-path Aspire check again failed with `aspire wait timesheets --timeout 30` exit 18, Timesheets exit 134, and port 8080 already in use; `aspire stop` succeeded. No passing current-topology or durable-write evidence is claimed. The earlier build baseline in the table is retained as dated provenance.

| Gate | Verdict | Evidence | Notes |
|---|---|---|---|
| Build | PASS | The latest round-20 Timesheets source-reference Debug solution build passed with `-warnaserror`, zero warnings, and zero errors. The round-10 build also passed using `-p:HexalithCommonsRoot=/tmp/no-commons`. Historical passes: on 2026-10-04, the changed worktree based on `57770d380c724c94233b78ac021a8f10cd5d6f39` passed `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` with 0 warnings and 0 errors. The same commands passed again on 2026-10-05 for the changed worktree based on `c5fcf987046018c980c5def7c2d3e1a9ce310abe`, with zero warnings and errors. The same commands passed for the spec-14 worktree based on `ee87e39f9f43e442e5426bb090afbe70be26492a` on 2026-10-05 with zero warnings/errors. Spec-15 source-reference Debug restore/build passed on 2026-10-08; spec-16 and spec-17 passed the same commands on 2026-10-09, all with zero warnings/errors. The 2026-10-09 public-route compatibility worktree also passed that build. The round-9 worktree source-reference Debug build passed with zero warnings/errors using `-p:HexalithCommonsRoot=/tmp/no-commons`; the same build without that override failed earlier on 2026-10-11 with MSB3243/CS1704 duplicate `Hexalith.Commons.UniqueIds` versions `2.30.1.0` and `1.0.0.0`. | On 2026-10-03, the bare build and its explicit-Debug retry failed with HXW0002 and missing Works references (176 errors); the supported source-reference mode and matching restore passed. Owner, root cause, and candidate configuration-propagation fixes are recorded in [the Story 3.6 build-guidance ledger entry](../_bmad-output/implementation-artifacts/deferred-work.md#deferred-from-build-guidance-review-patches-for-story-36-2026-10-04). The 2026-09-15 runtime smoke is not current-graph runtime evidence; current automated AppHost smoke remains deferred, and the 2026-10-03 manual attempt failed as recorded above. The dated Story 5.3 SDK `10.0.400` evidence remains historical. |
| Package currency | CONCERNS | Story 5.2 re-baselined restored-graph package listing and audits on 2026-10-18 under SDK `10.0.402` with `Aspire.AppHost.Sdk` `13.6.1` (NuGet-backed orchestration; only `ASPIRE010` suppressed), plus root `npm ci`, `npm outdated`, and `npm audit` on the same date. The 2026-09-17 SDK `10.0.401` audits are historical. Dated 2026-10-20 NuGet reruns: round 7 (committed as `c5ac600`) adopted Works Contracts `1.0.2` and reported only catalog-owned EventStore `3.125.1 -> 3.126.0` direct drift; round 8 at `929ea22` (committed as `5258598`), where the catalog selected EventStore `3.126.0`, reported no direct update, clean vulnerable and deprecated audits, and 1,959 Release tests (1,954 passed, 5 intentional skips, 0 failures). | Direct NuGet, vulnerable, and deprecated audits are clean at `929ea22` (2026-10-20); root npm is applicable, with eight high `npm audit` findings and two exact-pinned `@commitlint` patch updates kept visible; transitive drift is reviewed with no pin while the solution/AppHost tooling failure remains visible; platform prerelease entries are waived with owner, risk, and revisit condition; the Works EventStore-version forwarding divergence is open and unwaived (its values coincided at `3.126.0` at `929ea22`), with owner, risk, and revisit condition in the platform row and the deferred-work entry "Align the existing Works EventStore-version forwarding with the consuming source-build catalog in a separate build increment". |
| Tests (full suite) | PASS | The latest round-20 Timesheets executables all exited 0 individually: ArchitectureTests, Contracts.Tests, Server.Tests, Projections.Tests, IntegrationTests, and Works.Tests. The quiet runner did not print case totals. The latest isolated opt-in local Dapr pass remains the historical round-19 1/1 result in 106.313 seconds; a round-20 run awaits unique Dapr app IDs. | All six executables ran on the round-20 implementation tree. Five default skips for infrastructure and performance remain explicit. A separate historical focused HTTP class run hit the tracked numeric-substring assertion false positive. The local proof does not replace a deployed persistence test. |
| Privacy/logging scans | CONCERNS | Current static privacy scans and the 432-case helper theory pass. The full IntegrationTests lane passed; a separate focused HTTP boundary run passed 567/568, with the generic `"60"` assertion falsely matching a permitted timestamp. The four scratch-root HTTP cases and the earlier combined HTTP classes retain 2026-10-09 evidence. | The numeric-substring assertion follow-up remains open. In-process capture omits log scopes and exception text; malformed-request diagnostics and ingress/sidecar logs remain unverified. |
| Projection rebuild/idempotency | PASS | `tests/Hexalith.Timesheets.Projections.Tests/*ProjectionTests.cs`; round-19 Projections.Tests 307/307 pass, including stored submission lineage, tenant-scoped recording and terminal evidence, independent owner/capability stream sequence handling, and legacy terminal-before-issuance rejection. | Projection coverage passed with 0 skips. |
| Export golden files | PASS | `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs`; `tests/Hexalith.Timesheets.IntegrationTests/Exports/Golden/approved-time-export-v1-project-work-boundaries.csv`. | Golden-file cases passed in the round-10 IntegrationTests lane: 669 pass and five intentional skips. |
| Magic-link HTTP no-disclosure | PASS / CONCERNS | The 2026-10-10 full IntegrationTests lane passed, including the HTTP boundary class; a separate focused class run passed 567/568 with one false positive from the generic `"60"` substring matching a permitted timestamp. The 2026-10-09 combined HTTP classes passed 482/482 and four scratch-root privacy cases also passed. | This is in-process route, projection-store, and captured-log evidence. The numeric-substring assertion risk is already tracked in `deferred-work.md`. The production authority-mode path still lacks a trusted binding issuer; the local run uses generated Development credentials. Header values are compared across invalid responses but are not scanned for protected material; token-bearing `?t=` responses lack `Cache-Control: no-store` and a restrictive `Referrer-Policy`. Ingress/sidecar logging is outside this test boundary. |
| Magic-link durable confirm/adjust persistence | FAIL | Local EventStore actor state-store tests prove exact stored use/effect batches and actor recreation for confirm and adjust, plus proposed-entry revoke/expire markers. The HTTP fixture proves Completed status plus matching readback, one-winner concurrency, replay denial, revoke/expire terminal writes, and opaque non-Completed outcomes. Signed submit/status/stream HTTP route cases, a composed-host assertion-source binding case, processor origin/actor pairs, and stored-receipt integrity regressions for issue/use/confirm/adjust/expire pass. The fixture persists stored batches even for `EventsStored` and `PublishFailed`; both still yield an opaque 403. The latest round-19 isolated [local Dapr journey](local-magic-link-dapr-proof.md#round-19-isolated-local-run-2026-10-12) passed 1/1 in 106.313 seconds, including persisted confirm/adjust, one-winner race, restart, and exact selected readbacks. | Local EventStore/Dapr acceptance passed on the round-19 implementation tree. Deployed Dapr persistence remains unexercised. The authority-mode issuer cannot sign tenant/domain/actor resource bindings; add a trusted issuer before rollout. A production EventStore recording writer shares the keyed `timesheets` processor. The in-process external submission test uses an authenticated workload fixture; it does not establish production external caller authentication or the default fail-closed access guard. Capability and Time Entry read-model handlers are registered in the composed host, but deployed delivery, complete shared-rebuild inventory, and selected readback remain unverified. Inventory unknown deployed histories and stop legacy terminal writers before enabling the new owner stream. Measure the configured five-second polling deadline and the `PublishFailed`-after-commit opaque outcome in the deployed topology. Current Timesheets HEAD pins EventStore `3099cb27`; the SDK changes in this worktree are uncommitted. |
| Performance evidence | PASS / WAIVED | `docs/performance-evidence.md`. | In-process NFR10/NFR11 pass; EventStore-backed wire path WAIVED. |
| Tenant-isolation/security | PASS | `tests/Hexalith.Timesheets.Server.Tests/AuthorizationServiceTests.cs`, `AuthorityBoundaryTests.cs`, `FailClosedDefaultsTests.cs`, `TimeEntryAuthorizationTests.cs`; Server.Tests 523/523 pass. | Fail-closed defaults, loader integrity, and tenant-only magic-link Activity Type ownership passed. |
| Works checkout ownership | PASS | Story 5.3 fitness tests in `WorksCheckoutGovernanceTests.cs`; `.gitmodules` and git index pin `references/Hexalith.Works` only. | Default checkout is the umbrella `references/` pin; a root gitlink is forbidden. This gate does not claim live Works host integration. |

Overall release decision: **FAIL**. Stories 3.6 and 3.7 remain in progress, Story 5.2 remains in progress, and final Story 5.1 reconciliation remains ready for development. Atomic magic-link submission, EventStore-backed recording, local actor commit/reload, and composed-host registration of capability and Time Entry read-model handlers are implemented. The round-19 2026-10-12 isolated opt-in local Dapr/EventStore journey passed record, issue, confirm, adjust, race, restart, exact selected lineage, and capability readback checks in 106.313 seconds. Deployed acceptance remains open. The run-only AppHost has an EventStore resource, but the authority-mode workload issuer cannot sign tenant/domain/actor bindings. The shared-rebuild Begin producer now exists in code but has no independent installed coverage authority, all-writer receipt, certified legacy owner census, or live Dapr proof; deployed histories are unknown, and old capability-stream terminal writers must be stopped before rollout. Current Timesheets HEAD pins EventStore `3099cb27`; this worktree's SDK changes are uncommitted. Existing Works, export, performance, UI, legal-hold, package, privacy, and stakeholder gates retain their separate dispositions above.

### Initial v1 migration code evidence (2026-10-12)

The separate internal authenticated `migrate-v1` route is implemented with
server-resolved inventory receipts and EventStore private epochs. The complete
retained issuance stream includes terminal capabilities and omitted collisions,
uses bounded tenant/append pages, and binds digest/count, source cut, independent
coverage and enforced quiescence to durable progress. Resume and promotion
revalidate these bindings. Local simulated migration tests passed 42/42;
composed ingress tests passed 31/31, with both focused source-reference Debug
builds at zero warnings/errors. Evidence and exact commands are in
[the Story 3.6 test summary](../_bmad-output/implementation-artifacts/tests/3-6-test-summary.md).

The production provider is unavailable and migration authority remains default-deny.
No deployed inventory/installation proof, real operator authority or enforced
writer/delivery quiescence exists here. The required empty global journal and
verified recovery of pending legacy delivery identities have not been established.
No AppHost, live journey or Redis migration ran in this increment. This is
implementation evidence only; the earlier live failure and all deployed gates
remain open. Story 3.6 remains in progress. Release remains **FAIL**.

Abort-cleanup recovery was subsequently completed through an immutable
EventStore read-model witness retaining the exact operation/receipt/source
binding after SDK checkpoint deletion. The new recreation test verifies
mismatched/unauthorized refusal, matching cleanup with a revoked lookup,
unchanged legacy selection, and successful fresh-operation persisted v7
readback. The pre-review focused v1 suite passed 43/43 with no skips; its Debug
build passed with zero warnings/errors. Exact commands/logs are in the
Story 3.6 test summary. No live or deployed proof was added; Story 3.6 stays
in progress and release stays **FAIL**.
