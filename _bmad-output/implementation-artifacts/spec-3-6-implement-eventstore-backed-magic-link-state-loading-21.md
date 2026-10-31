---
title: 'Prove Story 3.6 live magic-link loading against a direct v1 index'
type: 'bugfix'
created: '2026-10-12'
updated: '2026-10-30'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36'
story_key: '3-6-implement-eventstore-backed-magic-link-state-loading'
context:
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 has no passing latest-tree isolated live journey. Ordinary issuance on `magic-link-token-hash-capability-index` throws when the direct v1 key exists, and the dispatcher reports `delivery_state_unavailable`. The last isolated runs failed there.

**Approach:** Prove valid-link loading and selected effective Time Entry evidence on one latest-tree isolated journey. Ordinary delivery stays fail-closed while that v1 key exists. Apply the recorded migration decision only as a separate explicit operation. Local proof does not close deployed certification.

## Boundaries & Constraints

**Always:** Resolve the candidate through the selected index version. Compare effective duration, billable state, approval state, target, and contributor with the stored events. Keep packed lineage, token-free diagnostics, duplicate-id preflight, and the existing recording-retry delays. Cover approval, metrics, comment, and a directory candidate in the narrow Python tests. Preflight one `eventstore` and one `timesheets` id.

**Never:** Do not clear a projection store, bootstrap v7 over the direct v1 key, or treat unique-only legacy rows as use authority. Do not change AppHost or ServiceDefaults, stop another AppHost, or weaken opaque 403. Do not mark Story 3.6 or release readiness PASS. Do not add live approval, comment, or metric submission just to repeat the narrow tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Clean index | No direct v1 root and no legacy shard | First issuance bootstraps v7 and the journey reads that candidate | Replay stays an opaque denial |
| Direct v1 root | `StateKey` has a value | Ordinary issuance and v6 migration publish no v7 root | Retryable `delivery_state_unavailable` |
| Legacy v6 shard | A selected shard exists and no v7 root | Ordinary delivery stays on the shard path | Invalid manifest fails closed |
| Store outage | Legacy scan cannot read | No partial v7 root | Retryable unavailable; cancellation propagates |

</frozen-after-approval>

## Code Map

- `scripts/local_aspire_magic_link_journey.py:642` — already verifies selected issuance, effective stored values, packed lineage, replay/race/rebuild and restart. Preserve retries and duplicate-id preflight.
- `scripts/test_magic_link_index_selection.py` and `scripts/test_magic_link_evidence_lineage.py` — existing directory, approval, metrics and comment cases; investigation passes 30/30 and 3/3.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs:309` — ordinary v1 refusal and bounded shard scan; preserve.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs:13` — separate certified `migrate-v1` already exists; authority denies and inventory is unavailable by default.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs` and `MagicLinkV1MigrationTests.cs` — persisted bootstrap/refusal, outage/cancellation, certified migration and recovery checks.
- `scripts/run_isolated_magic_link_dapr.py:1778` — supported runner: empty runtime inventory, private infrastructure, unchanged shared baseline and exact owned cleanup.

## Tasks & Acceptance

**Execution:**

- [x] `scripts/local_aspire_magic_link_journey.py` and the two Python test files above — verified existing assertions; focused checks passed 30/30 and 3/3; no redundant implementation.
- [x] The two Projections test files above — matching Debug/source restore/build passed; existing classes passed 74/74 and 62/62, zero failures/skips. Existing v6 fresh-metadata refusal method additionally passed 4/4, including direct-v1 refusal, to cover the frozen matrix's v6 boundary.
- [x] `scripts/run_isolated_magic_link_dapr.py` — one supported isolated attempt exited 1 at runtime-inventory admission; sanitized command/source/admission/cleanup receipts and the unobserved preservation disposition are retained. No retry or migration.
- [x] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, and `docs/launch-readiness.md` — appended exact results; history, parent in-progress and release FAIL preserved.

**Acceptance Criteria:**

- Given fresh isolated stores and admitted infrastructure, when the journey completes, then selected issuance and effective evidence match stored events through use and restart; qualification also requires preservation and cleanup success.
- Given an admission, baseline or journey failure, when this attempt stops, then evidence states the exact terminal phase without claiming a passing live journey.
- Given direct v1 data, when ordinary delivery or uncertified migration runs, then existing tests prove refusal and retained legacy state.
- Given local-only results, when documentation is updated, then deployed acceptance stays open and Story 3.6/release remain FAIL.

## Implementation Notes

Existing selected-index, effective-value, lineage, replay/race/rebuild/restart assertions, token-free diagnostics, duplicate-id preflight and recording delays were preserved. The scoped implementation is evidence-only. The required classes do not execute the v6 migrator, so the existing `MagicLinkV6MigrationTests.FreshInvalidSelectedMetadataNeverReservesAFence` method was selected separately; no new test or source edit was needed.

The single attempt stopped at admission before any shared baseline observation or resource creation. Current live qualification remains incomplete. [Exact commands, frozen-matrix test mapping, inline sanitized receipt and source/log hashes](tests/3-6-test-summary.md#spec-21-focused-verification-and-terminal-admission-refusal--2026-10-30) are retained in the test summary.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1: temporary focused reports | medium, patch | Only hashes and totals survive removal of temporary logs/XML; retain sanitized, retrievable focused reports in repository evidence and keep original hash provenance. |
| Blind 2: exact environment claim | low, patch | The receipt records only the explicit DOTNET_CLI_HOME override, not inherited/effective environment. Narrow the claim and retain observed safe tool versions; do not invent missing environment evidence. |
| Blind 3: ambiguous admission reason | low, patch | The original Dapr command exited 0 and returned a valid eight-row list. Preserve that aggregate observation without foreign identifiers, while retaining the exact runner refusal. |
| Blind 4: selected-shard write claim | low, patch | LegacyIndexPreventsFreshOrdinaryV7Bootstrap(true) checks returned shard-targeted operations without committing them. Correct the summary to this observed scope. |
| Blind 5: v6 command absent from recipe | low, rejected | The non-frozen recipe omits the extra method, while execution results and the linked summary retain its exact command and 4/4 result. The workflow rejects findings whose fix edits this build's spec; no implementation/evidence defect remains. |
| Blind 6: historical latest-attempt wording | low, rejected | Design Notes use latest for the pre-execution 2026-10-28 context; Execution Results and all evidence documents identify 2026-10-30 as the new attempt. The workflow rejects this spec-only wording fix. |
| Parent 1: ignored report copies | low, patched | Seven newly retained .log copies were ignored by Git. Renamed only those sanitized copies to .txt and updated links/provenance; all twelve evidence artifacts now appear in the reviewable untracked-file inventory. |

Edge-case review returned no findings; verification-gap review found no gaps. Each blind finding was classified before grouping. Findings 1–4 are independent, direct evidence corrections and route to patch; there are no intent-gap, bad-spec or deferred entries.

Review closure: all four evidence corrections are complete. Ten sanitized focused reports plus provenance and privacy-safe runtime observations are retained under `evidence/3-6/20261030-spec21-focused/`. Parent verification matched original/sanitized hashes, exact sanitization, all 140 C# case outcomes, report links and the original eight-row inventory aggregate; frozen intent and earlier evidence bytes are unchanged. Formatting passes. No source or runtime/test rerun was needed for these evidence-only patches; the authorized runtime attempt remains exactly one. No finding was deferred.

The scoped spec completes under its approved terminal-refusal alternative. Parent Story 3.6 stays in-progress because current runtime and deployed acceptance are unfinished; it is not advanced to review. Changes remain uncommitted under the repository safeguard requiring an explicit Git task.

## Design Notes

The frozen block describes the 2026-10-12 starting state. Completed `spec-3-6-migrate-v1-magic-link-index-to-v7.md` records the authorized separate migration; completed `spec-3-6-isolate-live-dapr-fixture-storage.md` supplies the approved isolated runner. These settle the former Open Question; no live migration here.

Latest retained attempt, `tests/shared-state-preservation-investigation.md` (2026-10-28), timed out observing shared Redis DB0 before resources. Earlier journey success failed preservation. Neither qualifies the current runtime. Preserve the strict guard and historical failures.

Intent gaps: none. Side effects: admitted runner-owned infrastructure and EventStore fixture writes. Footprint: this spec and three evidence documents; no new API. Source: `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`.

## Verification

- `python3 scripts/test_magic_link_index_selection.py` — 30 pass.
- `python3 scripts/test_magic_link_evidence_lineage.py` — 3 pass.
- Restore the Projections test project with `-p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`; build it with `--configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`. Prefix both with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`. Run its built executable separately with `-class '*MagicLinkGlobalIndexEpochTests'` and `-class '*MagicLinkV1MigrationTests'`; expect zero failures/skips.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-spec21-20261030` — retain exact exit/result; success requires 1/1 live test plus preservation and cleanup. A refusal is a documented incomplete live gate.
- `git diff --check` — clean.

### Execution results — 2026-10-30

- Python selected-index **30/30** (0.057s) and lineage **3/3** (0.001s), exit **0**; existing directory, approval, metrics and comment cases retained.
- Matching Debug/source Projections restore and warning-as-error build exited **0**; build **6.06s**, zero warnings/errors. Built global-index **74/74** (22.602s), v1-migration **62/62** (18.323s), additional existing v6 refusal method **4/4** (1.076s), all exit **0**, zero errors/failures/skips/not-run. No solution build or six-suite rerun is claimed.
- Exact supported runner command above ran once at **2026-10-30T17:39:31.295966+00:00 → 2026-10-30T17:39:31.713406+00:00**, exit **1**, **0.417432s**. Terminal phase **runtime-inventory admission**: “A Dapr application is already running or inventory is unsafe; no AppHost started”. Zero AppHost/infrastructure starts or component copies; cleanup complete, zero containers removed; source snapshots matched. Shared baseline/preservation and the live test never ran. No retry or migration.
- Raw runner evidence remains temporary at `/tmp/timesheets-spec21-20261030/run-mmwu9279`; privacy-safe original receipt contents, hashes and counts are retained inline in the linked summary. This refusal is an incomplete live gate. Story 3.6 remains **in-progress** and release remains **FAIL**; historical failures and deployed acceptance remain unchanged.
