---
title: 'Close five deferred integration handoffs'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred integration handoffs remain unresolved in the ledger: a gateway override suppresses supporting registrations, the Works source-reference edge can lose Configuration and select its own EventStore version, the static index Rebuild helper has no documented support boundary, and delivered cache/referrer controls lack a reconciled disposition.

**Approach:** Register platform gateway support independently while preserving caller overrides; forward the consuming Configuration and catalog EventStore version to Works.Contracts; retain and document the pure candidate-index helper with focused regression coverage; verify cache/referrer controls across the public routes and reconcile exactly those five canonical ledger entries. Verify, independently review, commit, and push the bounded change to main as requested.

</frozen-after-approval>

## Implementation Notes

- Planning facts: no unresolved intent gaps, domain migrations, deployment actions, dependency updates, or destructive operations. Small reversible footprint: Server registration and its tests, Works project-reference metadata and focused architecture tests, existing helper documentation/tests, ledger and verification artifacts. No AppHost, sibling checkout, agent-entry-point or public-response policy changes.
- User explicitly selected five deferred items as the batch; preserve this scope. Duplicate historical ledger observations are not additional closures.
- Baseline: Timesheets e85fad137be152c884e20efbe078fdb42346b0fc, clean main. Read the Hexalith baseline, state/Git instructions, tracked root guidance, build configuration, boundary decision and architecture. Local SDK 10.0.402; catalog EventStore 3.126.0.
- Runtime baseline: aspire ps --format Json reports a foreign Tenants AppHost. The local isolated-fixture preflight explicitly refuses any running AppHost/Dapr app. Do not start or stop resources or infer runtime/deployed acceptance from this increment.
- Code map: Server/Runtime/ServiceCollectionExtensions.AddTimesheetsServerKernel currently gates the entire SDK gateway registration on IEventStoreGatewayClient absence. Always use SDK registration and remove only its newly added fallback gateway descriptors when an override exists; preserve caller descriptor multiplicity/lifetimes and SDK TryAdd behavior for command-status support. IConfiguration/token-rotation binding remains a separate open goal.
- Code map: src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj owns the external Works.Contracts edge. Forward Configuration and HexalithEventStoreVersion through AdditionalProperties; preserve the conditional Debug/source graph and package Release graph, auditing, explicit Works root and caller version override. Do not edit Works or dependency pointers. Exercise actual MSBuild traversal with a harmless isolated probe, including deliberately different defaults and consuming overrides.
- Code map: Server/MagicLinks/MagicLinkTokenHashCapabilityIndexProjection.Rebuild is a public pure issuance-to-candidate fold used by two loader fixtures. Retain its signature for compatibility, document that it neither persists nor certifies owner continuity, and verify null input, duplicate issuance, ordering, and ambiguity semantics against FromContributions.
- Code map: public magic-link response helper already applies Cache-Control no-store and Referrer-Policy no-referrer. MagicLinkConfirmationHttpBoundaryTests has all-route assertions; verify success and opaque denial without treating local HTTP evidence as deployed qualification.
- Verification: matching Debug/source solution restore and warnings-as-errors build, six built xUnit executables individually, focused changed behavior checks, and git diff --check. Record exact commands/results and any blocker separately. One context-free blind-hunter review is required; triage all findings and patch bounded defects before finalizing.

- Implementation: gateway support regression failed before the fix with missing ICommandStatusLocationBuilder. SDK support now registers even when callers supply gateway descriptors; the complete caller sequence/lifetimes survives. Default fail-closed Location and caller-defined Location policies have regressions.
- Implementation: actual SDK assignment discards Configuration even from AdditionalProperties. Set ShouldUnsetParentConfigurationAndPlatform=false only on the Works adapter and forward catalog version via its Works.Contracts edge. No upstream checkout/pointer changed. The isolated test copies the actual adapter project and executes real AssignProjectConfiguration/MSBuild traversal against a harmless fake sibling with a deliberately conflicting version; it needs no restore or infrastructure.
- Verification correction: the first architecture test build caught xUnit1051 for stream-reader cancellation, then its initial probes required nonexistent package assets. Added test cancellation and SkipResolvePackageAssets only to the isolated assignment probe (no restore/build gate changed). All three focused traversal scenarios now pass.
- Verification correction: full IntegrationTests exposed the header assertion in a generic denial-capture helper reused by protected management routes. Restrict that assertion to the public-route denial matrix; preserve protected management behavior and privacy-first capture ordering. Final rerun pending.
- Updated README/CI guidance and added a dated launch-record annotation for the Works consumer correction without rewriting historical receipts or changing release verdicts. The managed agent entry points still contain conservative historical conditional-override guidance; their own refresh is outside this increment.

- Scope correction after review: the initial version-forwarding implementation affected build traversal only; NuGet restore drops AdditionalProperties. Removed that metadata and its false ledger resolution, restored the conditional override guidance, and left the original version-forwarding entry open. The user's delegated selection of five deferred items is fulfilled by substituting the stale AppHost endpoint-comment follow-up: the existing comment and declaration already both say public. No AppHost edit is needed. The frozen planning block above is preserved as the original plan; this execution note supersedes its version-forwarding portion.
- Final five canonical closures: independent gateway support; Works Configuration preservation; retained pure Rebuild support boundary; cache/referrer handler controls; endpoint-comment/declaration naming reconciliation.
- Review compatibility correction: obtain the SDK's own default Location descriptor from an unbuilt registration collection and TryAdd it alongside options/HTTP-factory support. Configure sidecar origin/routing only for the fallback gateway. This preserves an existing SDK client's complete options and named pipeline, rather than merely its descriptor, and makes repeated composition idempotent without reimplementing the platform's Location policy.

## Review Triage Log

- Medium, patched: unconditional SDK client configuration overwrote a preconfigured caller origin and routing. Supporting services now register without changing caller transport; the real SDK gateway regression also retains a separate ConfigureHttpClient override.
- Medium, patched: repeated kernel calls added duplicate routing handlers. The default client path remains conditional; repeated default/custom composition tests verify a single routing configuration and preserve caller registrations.
- Medium, verification pending: the initial Configuration closure omitted the actual no-global-mode Debug solution lane. Run matching restore/build without UseHexalithProjectReferences; preserve the normal explicit-source lane independently.
- Medium, patched: first-hop version tests did not prove NuGet restore consistency. Actual restore graph inspection confirms it drops per-reference AdditionalProperties. Removed the proposed version fix and false closure, preserving the original open ledger entry and conditional override guidance; no automatic version-alignment claim survives.
- Low, patched: helper ambiguity coverage used only equal capability IDs across tenants. Added distinct capability IDs within one tenant and asserted their canonical order alongside cross-tenant ambiguity.
- Low, patched: ledger claimed every captured denial had header assertions, while only the public matrix was covered. Narrowed the claim to the actual handler-response matrix/success scope; protected management and framework binding errors remain outside it.

## Final Implementation (supersedes chronological notes above)

The preceding Implementation Notes and pending review dispositions preserve the planning/implementation history. The final source does not remove caller descriptors or forward a catalog version through reference metadata. Gateway supporting services reuse the SDK's default Location descriptor from an unbuilt collection; only an absent unkeyed gateway triggers default origin/routing configuration. Existing caller transports and repeated-registration behavior are verified.

Both the adapter and Works.Tests preserve Configuration; the latter owns the SDK-added transitive reference from restore assets. There is no propagated ShouldUnset metadata. Six isolated SDK-assignment cases cover both reference owners; actual matching Debug restore/build without a global mode property passed with zero warnings/errors after the earlier 177-error reproduction. The normal explicit-source graph was restored and its final solution build also passed with zero warnings/errors (4.36 s). The version-forwarding item remains open, and existing conditional override instructions remain current.

The final five ledger annotations concern gateway support, Works Configuration, pure-helper support, handler cache/referrer controls, and the already-correct endpoint name. Reviewer follow-up confirms all original material findings addressed. Full final suite verification follows below when complete; earlier receipt counts retain their stated scope.

## Final Verification

- Matching explicit Debug/source restore and solution build pass; final build zero warnings/errors in 4.36 s. The additional no-global-mode Configuration-deferral restore/build also passes, zero warnings/errors (build 5.60 s).
- Six final executables completed individually after all source/test corrections: Architecture 109 passed, Contracts 92, Server 624, Works 76, Projections 614, Integration 892 passed/five skipped. Total 2,407 passed, five intentional opt-in skips, zero failures/errors/not-run. Commands, times, earlier failures and evidence boundaries are recorded in tests/five-integration-handoffs.md.
- Review finding 3's pending Configuration verification is now satisfied. Reviewer follow-up confirms all original material findings addressed and withdraws its grandchild concern: the second affected edge is the SDK's transitive reference in Works.Tests, covered by the final six-case fixture. No new review deferral was added; the original EventStore-version item remains open and replaces no current acceptance gate.
- Exactly five canonical ledger entries are resolved. No AppHost, deployed resource, dependency pin, sibling checkout, gitlink or agent entry point changed. Foreign AppHost/Dapr inventory continues to prevent runtime baseline startup; local proof supplies no current-graph/deployed acceptance.
- Git whitespace checks and Conventional Commit message validation pass. Only final verification records and explanatory documentation changed after the final executable checks.
