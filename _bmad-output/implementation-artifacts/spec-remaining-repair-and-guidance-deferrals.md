---
title: 'Resolve remaining repair and guidance deferrals'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6c0f0b58c4b501c361e7bba465b848547f3043f9'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferrals were requested, but only three work areas are locally actionable. DW-184 is newly unblocked: published EventStore 3.128.4 contains its repair API.

**Approach:** Integrate the held repair patch, qualify manifest bounds, and synchronize SDK guidance. Review, verify, commit and push main. The user approved this three-item scope and continuation on 2026-10-28.

## Boundaries & Constraints

**Always:** Default-deny certification; retain authorization, witnesses, corrupt evidence, journals and owner checkpoints. Use SDK repair only. Keep production acceptance, historical failures and launch FAIL explicit.

**Never:** Fabricate closures, rewrite history, modify dependencies/gitlinks, initialize nested submodules, deploy, extend platform limits in Timesheets, or weaken guards. Native visibility/failure-tracing obligations remain open.

## I/O & Edge-Case Matrix

| Given | When | Then |
| --- | --- | --- |
| Certified unchanged v6 source and exact witness | SDK recovery runs | Drain original journal; preserve evidence; never promote v7 |
| Missing certificate, source drift, foreign operation or corrupt witness | Recovery runs | Refuse; retain fence and deliveries |
| Interrupted repair | Cold retry runs | Resume exact binding or refuse |
| Manifest exceeds 4096 guards or 256 KiB request | Repair runs | Refuse; preserve stored state and fence |
| Synchronized guidance | Notes refresh | SDK 13.6.4; distinguish historical/current runtime proof |

</frozen-after-approval>

## Code Map

- `Directory.Packages.props`: advance EventStore 3.128.2 to verified 3.128.4; preserve other floors/source policy.
- `_bmad-output/implementation-artifacts/evidence/five-retry-recovery-tooling-deferrals/certified-repair-pending.patch.gz`: seven-file reviewed patch. Hash matches committed manifest; apply-check passes. Adapt old Server imports to ReadModels only if required.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`: restore repair/fingerprinting and adjacent certificate types; preserve default-deny constructor.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV6MigrationTests.cs`: patch fixtures cover persisted recovery/refusal; add valid near/over-bound manifests against actual SDK request limits.
- `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`: correct SDK 13.6.1 and stale current-runtime wording together.

## Tasks & Acceptance

- [x] Verify package provenance, apply patch and update consumer pin; fix demonstrated compatibility issues only.
- [x] Qualify capacity through persisted-state tests above; leave extension/deployed inventory open.
- [x] Synchronize three guidance files; verify normalized equality and SDK configuration.
- [x] Add implementation-artifact report `tests/remaining-repair-and-guidance-deferrals.md`; update `deferred-work.md` and `docs/launch-readiness.md` with precise dispositions/evidence.
- [ ] Independently review/verify, validate Conventional Commit, fetch/inspect divergence, commit owned changes and push main without force.

**Acceptance Criteria:**

- Given the published package, when tests run, then the recovery matrix passes and defaults deny repair.
- Given capacity evidence, when recorded, then larger/deployed inventories remain unqualified.
- Given verified changes, when published, then remote main contains them without unrelated changes.

## Implementation Notes

- User approval received after rereading this draft; Open Questions is resolved. The implementation handoff owns code, focused verification and report drafts. The parent owns final verification, independent review, Git staging/commit and publication; the implementation agent must not stage, commit or push. Parent performs and records the required runtime baseline before dispatch; do not rerun runtime qualification in the handoff.
- Parent baseline completed before source changes: `/tmp/timesheets-repair-guidance-20261028/baseline-runtime.json` and `baseline-runtime/run-nzdrukv5/` retain the supported security-smoke command. Exit 1 after 278.713 seconds: live journey 1 passed (67.862 seconds), but shared preservation refuses Redis DB0 persistent content drift (829212 to 829223 keys). Cause is unclassified; do not infer task or foreign ownership. Cleanup is complete (three owned containers removed); subsequent Aspire/Dapr inventories are empty. Preserve this failed baseline, do not rerun/repair runtime, and do not claim runtime qualification. Parent will retain the relevant receipts with final evidence.
- Planning only; initial main clean. No implementation or publication performed.
- NuGet/dotnet-inspect confirms 3.128.4 `GetAbortRepairAsync`/`RepairAbortMetadataAsync`. Release run 40656509578 succeeds at `717a06e16c3afd81b904eaf4de646b74b13262d2`; SDK repair commit `c613e55aefd0650c1e64ed0580c8495657585a89` is its ancestor. Recheck at implementation.
- Independent ledger audit: DW-19 requires history rewriting; eight obligations require deployed evidence. Three proposed work areas are not five closures.

## Spec Change Log

## Review Triage Log

- Parent verification correction: the initial full Architecture lane fails one stale assertion requiring current runtime success. Current classification wording and that assertion now require the evidenced unqualified runtime and failed preservation baseline, retaining all unfinished/FAIL checks. Full Architecture rerun is pending after the serial batch; other source code is stable. Contracts 108, Server 746, Projections 778 and Works 76 pass; Integration is running. The initial broad receipts remain red history. Parent deliberately changed only the Architecture source after its initial execution; final input comparison must record that difference rather than claim whole-tree equality.

All three review layers completed; edge-case hunter returned no findings. Findings were graded individually before grouping.

| ID | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| B1 | medium / patch | Two current launch summaries still call local qualification passing. Date those successes as historical and retain current unqualified status. |
| B2 | medium / patch | README still says recovery never deletes corrupt metadata; update it for independently certified SDK repair and immutable retained evidence. |
| B3 | low / reject | SDK request-capacity ArgumentException is collapsed to the existing opaque 404 contract. This remains a fail-closed refusal with retained fence/work. Distinct operator status is a cosmetic preference for an unavailable-by-default provider; changing exception branches is not warranted by a demonstrated correctness failure. |
| B4 | maybe-false / defer | Full-manifest collection before SDK bounds is observable; intolerable memory/latency on a supported certified inventory is unverified. Larger-inventory support was explicitly retained open in the approved qualification scope. Record the unverified operational concern; measurement against a real certified inventory and budget would settle it. |
| B5 | medium / patch | New cold retries cover only one corrupt metadata value. Add a three-value repair interrupted after its first committed deletion, preserving every original value and successful cold drain. |
| B6 | medium / patch | New fixtures do not independently establish inherited COW source/metadata provenance and pointer-drift refusal. Add persisted inherited-layout success/refusal coverage without changing production repair. |
| B7 | medium / patch | Candidate-page inclusion is exercised by exact receipt guard counts/byte cases, but directory-page inclusion/ETag drift is not. Group with V1 for the directory-page gap; retain ordinary source/pending state assertions. |
| B8 | medium / patch | Add guarded-source drift after reservation/committed deletion and prove cold retry preserves receipt, remaining metadata and pending delivery. |
| B9 | low / patch | Document the digest ordering, SDK record fields/default JSON/UTF-8/uppercase SHA256 and compatibility policy for external verifiers; helper measurement never grants certification. |
| B10 | medium / patch | Focused package compatibility excludes HTTP/Works/AppHost nodes. Parent will additionally restore/build the complete Release/package solution with isolated artifacts, preserving HXT0002 and source Debug outputs. |
| V1 | medium / patch | Pre-verified missing directory-page receipt guard and identical-content ETag-drift refusal; group with B7. |
| V2 | medium / patch | Pre-verified existing null-source case also fails by digest mismatch, so it cannot detect deletion of the explicit JSON-null source check. Add fresh migration refusal before epoch reservation with a healthy shard plus a present JSON-null shard. |

Review corrections complete: B1/B2/B5/B6/B7/B8/B9/V1/V2 are addressed. The implementation agent added nine persisted cases and corrected operator guidance; all four production repair files still match the held patch. Final source and actual published-package migration classes each pass 97/97. Independent verification-gap recheck confirms both filed gaps closed, with no remaining concrete issue. B10 is satisfied by the full isolated 16-project Release/package restore/build. B3 remains rejected; B4 is retained as an unverified operational follow-up. No layer was skipped.

## Verification

- AGENTS.md matching Debug/source solution restore/build, focused migration class, then all six individual suites; isolate package-mode compatibility checks.
- Inspect architecture/package tests; run compiled AppHost configuration-only probes. Load Aspire lifecycle guidance before mandatory runtime baseline; preserve admission refusals.
- Check instruction equality, links, evidence and `git diff --check`; retain exact results/blockers. Skips grant no runtime acceptance.

- Final verification completed: matching full Debug/source and isolated full Release/package builds pass with zero warnings/errors; Architecture 118/118 and package migration 97/97 pass. Initial six-suite run retains 2,806 passed/one stale Architecture assertion failed/five existing skips; the corrected full Architecture lane and final 97-case source migration lane supply explicitly composite coverage of 2,816 passed/five skips/zero remaining failures. Only the two test assemblies changed after the initial batch. This is not a claimed single all-six rerun. The second stale build-note assertion failure is also retained. Exact commands/XML, source identities, 16-node package graph and case evidence are under `evidence/remaining-repair-and-guidance-20261028/parent-final/`. Publication remains explicitly authorized and is recorded after remote verification; no runtime/deployed acceptance is granted.
