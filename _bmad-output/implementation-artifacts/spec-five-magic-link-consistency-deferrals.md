---
title: 'Resolve five magic-link consistency deferrals'
type: 'bugfix'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e411a1047350c8c7dd648983a22b1546425676cf'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-21, DW-52, DW-57, DW-63 and DW-96 record uncertainty about magic-link authorization across separate reads and concurrent catalog rebuilds. Current owner serialization addresses terminal capability races, but Project restrictions are absent from magic-link selection and the tenant catalog rebuild can overwrite live deliveries.

**Approach:** Establish the catalog decision policy, add focused persistence/concurrency evidence, correct the remaining catalog behavior, and reconcile all five entries against their actual guarantees. Review and verify before the requested commit and push on main.

**Approved decisions (2026-10-26):** Apply Project restrictions to issuance and adjustment selection; retain historical confirmation semantics. Use the catalog snapshot validated inside actor processing: later updates affect subsequent decisions. Projection Fresh does not establish latest-source visibility or cross-aggregate atomicity. The user approved the recommended implementation and continuation; commit/push on main remains authorized.

## Boundaries & Constraints

**Always:** Preserve server authority, opaque public denial, tenant isolation, the tenant-owned Activity Type rule, historical confirmation policy, pure aggregates and EventStore persistence. Preserve the canonical catalog address and use SDK projection concurrency mechanisms. Keep deployed acceptance and launch FAIL explicit.

**Never:** Infer a Project from a Work identifier, trust caller-supplied restrictions, change sibling-owned data, introduce another host, bypass integrity or pending-delivery recovery, or claim transactional cross-aggregate consistency from a second read alone. Do not deploy or mutate operational data.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Terminal owner | Issuance-only capability read; owner already used/revoked/expired | Reject confirm/adjust without another effect | Opaque denial |
| Corrected owner | HTTP intent predates owner correction | Evaluate current owner facts | Refuse invalid scope |
| Catalog race | Catalog changes during awaited authorization | Use the validated processing snapshot; later decisions observe the update | No unsupported atomicity claim |
| Project selection | Tenant type excluded by Project restriction | Deny new issuance/adjustment selection; retain historical confirmation | Unavailable policy evidence fails closed |
| Shared rebuild | Live addition/rename/deactivation during rebuild | Preserve accepted deliveries through SDK fencing/catch-up | Cancellation retains recoverable work |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs`: current owner terminal checks, separate catalog reads, atomic use/effect result.
- Adjacent `EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`: actor stream paging and canonical tenant catalog validation.
- Adjacent `MagicLinkConfirmationCapabilityCommandService.cs`: tenant-only capture selection and historical confirmation checks.
- `src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjection.cs`: `ProjectForProject` already expresses restriction semantics; `Project` omits them.
- Adjacent `TenantActivityTypeCatalogProjectionHandler.cs`: unfenced live writer; `FinalizeAsync` replaces candidate items using the latest persisted ETag.
- `src/Hexalith.Timesheets.Contracts/Models/ActivityTypeCatalogReadModel.cs` and `Events/ActivityTypes/ProjectActivityTypeCatalogRestrictionConfigured.cs`: add only the policy evidence needed for target-specific selection. No authoritative restriction stream identity is established by the current command contract; do not invent one. Prefer extending the canonical catalog's event-derived snapshot over a second policy service. Distinguish legacy/missing restriction materialization from a verified unrestricted snapshot; do not silently grant availability from absent evidence.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs`: existing SDK epoch example; do not duplicate its global index architecture for a tenant catalog.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkEventStoreActorPersistenceTests.cs`: real AggregateActor/state-manager fixture with actor recreation.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs`: catalog live/rebuild coverage; greater-cursor coverage alone does not prevent lost writes.
- Read-only EventStore `StreamsController`/`AggregateActor`: stream reads route to the owning actor; metadata follows its state-cache barrier. This supersedes the specific replica premise, not deployed consistency qualification.

## Tasks & Acceptance

**Execution:**

- [x] Integration test file above — add stale-capability/current-terminal-owner and current-corrected-owner cases; assert persisted end-state across actor recreation.
- [x] Processor/service files above and `tests/Hexalith.Timesheets.Server.Tests/MagicLinkEventStoreDomainProcessorTests.cs` — implement/document processing-snapshot semantics and exercise a catalog update while authorization is blocked, followed by a later decision.
- [x] Catalog model/projection/loader/service files above — implement target-aware Project restrictions in the canonical catalog snapshot. Reuse restriction semantics, preserve tenant-only ownership and historical confirmation, and cover allowed/excluded/unrestricted, legacy/missing/stale/ambiguous evidence, tenant isolation and Work targets without inferred Project lookup. Put each added C# type in its own file.
- [x] Catalog handler above and new `tests/Hexalith.Timesheets.Projections.Tests/TenantActivityTypeCatalogEpochTests.cs` — replace unfenced rebuild publication with the SDK tenant-scoped epoch/catch-up path; cover live additions, renames, deactivation, retry, cancellation and persisted recovery without changing the canonical address.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`, adjacent `tests/five-magic-link-consistency-deferrals.md`, and `docs/launch-readiness.md` — record each disposition, exact receipts, chosen semantics and remaining deployment gates. Never close a broader risk using narrower owner-only evidence.

**Acceptance Criteria:**

- Given stale HTTP/loader evidence, when owner processing runs, then terminal history and current owner facts prevent unauthorized duplicate effects.
- Given a concurrent catalog change, when processing and rebuilding overlap, then the approved decision policy is explicit and accepted live deliveries survive publication/recovery.
- Given passing checks and independent review, when publishing, then a commitlint-validated Conventional Commit reaches origin/main without unrelated changes or force.

## Implementation Notes

2026-10-26: Read-only investigation completed. The user requested pragmatic comparisons before selecting the two policies. Neither question is approved. The draft must be revised after the answers, then pass the workflow's spec approval checkpoint before implementation.

2026-10-26: The user instructed "apply recommended", approving both recommended policies and continuation on the presented spec. The decisions above replace the open questions; implementation is authorized. Only this owning repository changes; supplied references remain read-only. Use sequential implementation and independent review through the build workflow.

Focused current-source baseline build: Projections.Tests Debug/source, warnings as errors, passed with zero warnings/errors. Existing catalog projection tests were invoked separately; receipts are in `/tmp/timesheets-five-consistency-baseline-build.log` and `/tmp/timesheets-five-consistency-catalog-baseline.log`. These are baseline checks, not evidence that the deferred races are fixed.

Before code edits, `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-five-consistency-baseline-runtime` started the exact AppHost, verified readiness, and passed record/issue/confirm/adjust/replay and initial persisted rebuild checks. The post-restart Time Entry rebuild returned `projection_partial_retry`, causing the one live test to fail (exit 1). Owned cleanup and shared preservation passed. Evidence: `/tmp/timesheets-five-consistency-baseline-runtime/run-621apvzj`; retain the baseline failure independently and qualify final runtime behavior after implementation.

## Verification

- Matching Debug/source solution restore/build from AGENTS.md; run all six built xUnit executables individually after implementation.
- Focused catalog epoch and persisted actor cases first; existing loader and catalog-selection suites must remain passing.
- Follow the required isolated Aspire baseline procedure before code edits; no AppHost edits are planned.
- `git diff --check`; validate the exact commit message with commitlint; fetch/inspect divergence and push main after successful review/verification.

## Implementation verification checkpoint

Focused matrix coverage passes: terminal/corrected owner persistence (12 cases), blocked-authorization processing snapshots (three cases within the 262-case Server lane), Project selection and evidence integrity (same Server lane), and shared rebuild/retry/cancellation/recovery (seven epoch cases within the 219-case projection lane). Changed HTTP/owner coverage passes 19 cases. Final solution restore/build passes with zero warnings/errors. The full six-executable gate is running; its initial Architecture result found one stale source assertion expecting the unfenced interface, corrected to require the epoch interface. That suite will be rebuilt/rerun. Full runtime and review receipts remain pending before completion/publication.

## Review Triage Log

All three independent layers reviewed `/tmp/timesheets-five-consistency-review-5lthq6kl.patch`. Every finding was classified before grouping. All retained corrections are direct implementation/test/documentation patches with no new product API or unresolved intent choice.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1: empty terminal parking | medium | SDK CatchUp requires at least one logical Write at the retry limit; the new empty callback throws on attempt two. Patch P1: use the existing index handler's payload-free parking pattern and verify repeated failure/reconstruction. |
| B2: pure fold claims complete coverage | medium | The public pure projector can mark a partial, empty-policy input complete without a certified tenant inventory; an empty restriction collection passes shape validation. Patch P2: leave pure folds incomplete and certify only the rebuild path. |
| B3: missing coordinator reads direct catalog | medium | LoadCatalog's fallback ignores the existing explicit legacy-read option, so an omitted coordinator can read an obsolete direct record despite selected pending/degraded state. Patch P3: require the existing compatibility opt-in for direct fallback. |
| B4: malformed old direct policy blocks repair | medium | Finalize validates old direct policy although a complete, tenant-validated candidate is authoritative; a malformed legacy projection therefore prevents its own documented replacement. Patch P4: preserve the old data while allowing certified replacement into a selected generation; test the valid-candidate recovery path. |
| B5: MessageId uniqueness across deliveries | low | Per-envelope SDK dispatch does not reproduce the complete-history uniqueness check, so malformed manually supplied histories can diverge. The actual EventPersister generates a fresh sortable identifier per event (lines 138–144); no ordinary producer of repeated identifiers is demonstrated. Reject the proposed additional cross-delivery identity state/SDK change: uncommon invalid-source case and materially more than a direct correction. This is not a general claim about arbitrary imported histories. |
| B6: fixture rewrites production store methods | medium | The fixture's interface GetAsync/TrySaveAsync translate canonical keys to selected keys, hiding the production handler's distinct direct-record inspection. Patch P5: retain translation only in explicit test inspection/mutation helpers; exercise migration against ordinary store behavior. |
| B7: pre-cancelled catch-up only | medium | The cancellation test cancels before the fold, leaving cancellation after work begins unverified. Patch P6: interrupt after folding/preparing mutations, recreate the coordinator, and assert exact persisted recovery and no duplicate effects. |
| B8: snapshot race confounds policy and deactivation | medium | Later denial is explained by inactive state even if Project restrictions are ignored. Patch P7: add policy-only races with the Activity Type kept active. |
| B9: composed restriction boundary absent | medium | Existing HTTP fixture policy is seeded/rebuilt empty; service restriction tests construct snapshots and epoch tests stop at selected readback. Patch P8: deliver restrictions through the named HTTP route and assert issuance/adjustment denial, preserved confirmation and tenant isolation with persisted effects checked. |
| B10: bootstrap/recovery procedure missing | low | Fresh ordinary delivery intentionally leaves policy incomplete; operators need the steps that establish first complete coverage and recover failed rebuilds. Patch P9: document the existing authorized SDK procedure and its verification, retaining deployed gates. |
| E1: empty parking at retry limit | medium | Same SDK rejection as B1, independently traced. Group with P1; retain both findings. |
| G1: restrictive allow-list not asserted | medium | Pre-verified regression gap: replacing configured.IsRestricted with false survives the current epoch assertions before the later unrestricted update. Patch P10: verify the selected restrictive flag, Project and exact tenant allow-list before removing restrictions. |

P1–P10 are distinct root causes except the explicit B1/E1 duplicate. No finding requires changing the approved frozen intent.

## Final verification and review outcome

All review patches P1–P10 are applied and verified. Final solution build has zero warnings/errors; Contracts 108, Architecture 118, Server 744 and Works 76 complete executable cases pass. The complete pre-review projection gate passes 755 cases; all 220 affected catalog cases pass after review fixes, including the added recovery case. Final complete IntegrationTests passes 953 cases with five existing skips and no failures/errors/not-run. Python regressions pass 208/208; compiled guards pass 67/67.

Final current-code isolated run-przq32wz passes one live test without skips in 69.895 seconds, with all 926 source/execution fingerprints identical, generated credentials accepted, captured processes exited, owned cleanup complete and shared preservation matching. Earlier baseline failure, foreign-runtime refusal and pre-baseline failure are retained separately. The unrelated Projects AppHost exited independently; no foreign process was stopped. No safety timeout or preservation check was weakened.

Persistent receipts and exact scope are in [the verification report](tests/five-magic-link-consistency-deferrals.md) and [machine-readable audit](evidence/five-magic-link-consistency-deferrals/verification.json). The frozen block remains unchanged. No new ledger deferrals were added; B5 was rejected under the low/nonordinary-case rule. Existing deployment catalog rebuilds and Story 3.6 deployed acceptance remain open; launch remains FAIL. The exact Conventional Commit message has passed commitlint; user-authorized commit/push follows final diff/divergence checks.
