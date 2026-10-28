---
title: 'Attribute HTTP claims-boundary reads to their owning request'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — approved narrow investigation and demonstrated fixes in the invocation">

## Intent

Investigate the full-suite claims-accessor failure recorded by `f1a9f9737e386db95b9bb7657098c047f5efaee5`. Establish isolated source/build identity from published gitlinks, identify the extra read's operation and reproduce the demonstrated interleaving before changing assertions. Preserve the existing workspace, unrelated dependency checkouts and all foreign runtime sessions.

Make only evidence-supported fixes and add a deterministic regression. Preserve strict request read-set checks, authorization, tenant isolation, admission guards, timeouts, launch FAIL, deployed gates and every dashboard visibility limit. Run focused verification and full IntegrationTests serially; retain original broad failure and remaining uncertainty. Complete independent review, Conventional Commit validation and push the narrow changes to main, as explicitly authorized by the user.

</frozen-after-approval>

## Implementation Notes

- Source inspection maps the historical unexpected key exactly to the setup tenant-1 catalog. The mismatched request expects tenant-2. SDK `CommitAsync` schedules detached promoted cleanup; `CatchUpAsync` can return without draining it. Each cleanup stage reads the epoch even without pending cleanup data. The fixture's store-wide `List<string>` neither attributes reads nor supports concurrent writers safely.
- Use a thread-safe global observation queue with the actual host HTTP trace identifier. Assert every read of the exact authorized request, retaining global background observations. No production or dependency implementation change is planned.
- Reproduce with the SDK's real idempotent catalog commit and completion barriers around its cleanup store read. Capture the caller stack and absent HTTP context; show the original global assertion rejects that read while the request's strict assertion passes. Preserve assertions for issuance, revocation and rejected missing identifiers.
- Historical clean suite: isolated checkout `/tmp/timesheets-claims-investigation-20261028` at the recorded commit and all thirteen published gitlinks. Final work: `/tmp/timesheets-claims-fix-20261028`, based on current main `787a3d178e5d9bf135fe5138f1f8ee6b4247fa31` and its published gitlinks. Build Debug/source only with SDK 10.0.402. No AppHost, Dapr or container lifecycle operations are needed for these in-process tests.
- Update the existing follow-up report and ledger with exact commands, source/build manifests, red/green evidence, review findings and unresolved limits. An isolated or later broad pass cannot rewrite the historical failure. Cross-class scheduling pressure and historical timeout causation require evidence before attribution.

- The historical unmodified full run did not reproduce the original failure (979 passed, five skipped); its 23,542 recorded source/build hashes match before/after. The original broad failure remains evidence. Controlled historical global-window mutation fails at the same final whitelist predicate on exactly the original tenant-1 epoch; restored request snapshot passes all nine focused cases on both graphs.
- The regression pre-drains the exact setup coordinator, schedules a fresh real SDK cleanup, gates its identified chunk-cleanup read, then drains the whole task in finally. Test-only exact-signature reflection uses the SDK's existing internal drain; any signature change fails loudly. No production visibility or cleanup budget changes.
- A proposed equality between host trace and the last authorization correlation was disproved by four accepted focused cases. Successful command submission authorizes a new envelope and overwrites the last guard call. The final audit selects the actual host trace directly; independent original guard-context checks remain.
- Current-main solution restore fails NU1109 on three published Aspire package floors; the narrow IntegrationTests restore/build succeeds. No dependency pin or AppHost edit is authorized or needed for this test-only correction.

## Review Triage Log

| Finding | Verdict | Disposition and evidence |
| --- | --- | --- |
| Gate could claim metadata-only second read | medium | Patched stack classification before claim; exact setup coordinator pre-drained. Controlled stacks identify fresh chunk cleanup. |
| Authorization correlation may differ from audited HTTP trace | medium | Select host trace directly. Intermediate four-case failures disprove proposed equality; both reviewers rechecked successful downstream envelope path. |
| Negative assertion duplicated rather than shared | medium | Shared AssertAdminCatalogReadSet now used by original theory and actual HTTP injected-read control. |
| Release without full cleanup drain | medium | Exact-signature test-only SDK drain bridge; pre-drain setup and finally drain explicitly scheduled fresh task, preserving SDK budget. |
| Missing store name in observations | low | Added complete address and strict store assertion alongside unchanged key/count rules. |

Both independent reviewers found no remaining actionable source defects after rechecks. No source findings deferred. The original unrecorded natural scheduler timeline, separate solution-restore blocker and existing launch/deployed/dashboard limits remain explicitly recorded in the report and ledger.


Additional evidence triage:

- Low, patched: reconstructed current setup argv omitted its executed `--quiet` flag; command inventory corrected without changing any checkout.
- Medium, patched: early red receipt did not hash untracked regression/binary bytes. Repeated red and restored historical/current focused experiments with per-command source/artifact hashes after the broad suite; experiment-identity-chain.json binds builds and runs and proves only the one-line deliberate mutation differs. Earlier attempts remain labelled.
- Final full serial IntegrationTests: 986 total, 981 passed, five skipped, zero failures/errors/not-run, 718.468 suite seconds. All 22,135 source/configuration/Debug hashes and 159 top-level test artifacts match before/after. Other five suites, Release/package, performance, live Dapr and deployed acceptance were not rerun or claimed.

Both final independent evidence rechecks passed: all30 logs, eleven nonoverlapping test intervals, bound source/assembly chain, all22135 current file hashes, actual XML rows and final report links/limits verified. Conventional Commit validation passed; publication is explicitly authorized by the invocation. No code/evidence review finding remains unresolved.
