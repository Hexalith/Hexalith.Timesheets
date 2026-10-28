---
title: 'Restore the published Debug/source solution graph'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the Debug/source solution restore failure recorded by Timesheets `740130d2db103cb7a6b96d9a399a3bf4ba5d5fc4`. Investigate the evaluated graph, imported catalog and AppHost SDK in an independent checkout at the published root gitlinks. Make the smallest supported version alignment, including Directory.Packages.props and the AppHost configuration where demonstrated necessary. Preserve topology, warnings, gitlinks, the HTTP claims-read regression, the original workspace and all foreign runtime sessions.

Run matching full-solution Debug/source restore and warnings-as-errors build, existing AppHost configuration-only probes, then all six compiled suites individually and serially. Record exact commands, complete source identities, results and limitations in the report and deferred-work ledger. Preserve historical broad FAIL evidence, launch FAIL, deployed gates and native-dashboard visibility limits. Complete independent review, Conventional Commit validation and push to main as explicitly authorized.

</frozen-after-approval>

## Implementation Notes

- Original and remote main start at `740130d2db103cb7a6b96d9a399a3bf4ba5d5fc4`. Work occurs in `/tmp/timesheets-debug-source-20261028`, with independent Git indexes/refs and all thirteen published root dependency revisions; no nested submodules are initialized.
- Unmodified solution restore reproduces all three NU1109 downgrades. The imported Builds catalog at `81ab049208678bd6616828824729c0ddac02f7d1` already selects Hosting/Redis/Testing 13.6.4 and Keycloak 13.6.4-preview.1.26523.11. Four Timesheets post-import overrides select older versions. Remove those redundant overrides; preserve unrelated package pins.
- SDK 13.6.3 implicitly selects AppHost, Dashboard and orchestration 13.6.3. Inspect pins-only build/configuration results before final SDK alignment. Keep NuGet orchestration and the existing ASPIRE010 notice suppression. Update the existing literal governance test only if the SDK changes.
- Commands and before/after source/artifact manifests are captured under `evidence/debug-source-restore-20261028/`. No resource lifecycle or state commands are authorized by this build-only verification scope.

- Pins-only restore/build and configuration probes pass, but the existing catalog SDK governance test fails (expected 13.6.4, actual 13.6.3). Final fix aligns the SDK and its existing literal pin. Retain four explicit Aspire consumer floors at the imported versions because current package-evidence guards intentionally inspect root overrides; this avoids changing their assertions. No new tests are needed. Current-version documentation must follow the already-published catalog; historical receipt versions remain untouched.

- Final decision clarification: the earlier instruction to remove overrides is the superseded pins-only experiment, including its comment edit. The final implementation retains all four explicit consumer floors at the catalog's versions. The exact intermediate file and Git-diff hashes are verified by the retained patch. Frozen intent is unchanged.
- Required full Debug/source restore and warnings-as-errors solution build pass; 67 no-resource AppHost checks pass. Six serial compiled suites pass 2,785 tests with five existing skips and no failures/errors/not-run. All nine HTTP claims-read cases pass; both HTTP regression files remain byte-identical. Original workspace/dependency states match; 49 of 66 sampled baseline runtime processes disappeared without any task-issued lifecycle/state command, so continuous foreign-session preservation and disappearance cause/timing remain unqualified.

- Preservation timeline clarification: the earlier 49-process count is the interim sample (17 matching / 49 absent). The final post-suite receipt at 2026-10-28T10:08:32.284151+00:00 records 15 matching / 51 absent out of 66. Cause/timing remain unclassified; no continuous session preservation is claimed.
- After the documentation corrections, the complete compiled ArchitectureTests suite passes again: 118/118, no skips/errors/failures, 35.159 suite seconds; source and artifact identities remain unchanged during that command.

## Review Triage Log

- **R1 — low, rejected:** Four matching root Aspire overrides could repeat the downgrade on a later catalog advance. The published current graph resolves at 13.6.4. Existing package-evidence guards intentionally inspect explicit root floors, which also preserve older-catalog dependency floors. Removing them requires a broader policy/test change; this task retains the smallest four-value alignment and SDK contract. A future catalog move must align consumer floors and SDK in the same increment.
- **R2 — low, defer:** AGENTS.md, CLAUDE.md and Copilot managed guidance still state AppHost SDK 13.6.1. The same stale literal exists in base 740130d2db103cb7a6b96d9a399a3bf4ba5d5fc4, whose actual AppHost SDK is already 13.6.3. Exact build commands are correct. Instruction-entry edits are deferred by the build workflow; preserve synchronization and managed-context ownership in a separate refresh.
- **R3 — low, patched:** README current consumer EventStore version is stale at 3.127.0. Root CPM selects 3.128.2. Correct the current value and explain the catalog/floor relationship without changing any package.
- **R4 — low, patched:** October 28 current package verdict leaves the older fresh npm audit undated. Explicitly date that audit October 24; no new package audit or clean advisory result is claimed.
- **R5 — low, clarified in appended implementation notes:** Append-only implementation notes retain the preliminary removal plan alongside the final retained-floor decision. Retain the chronological notes and explicitly identify removal as the superseded pins-only experiment. Frozen intent remains unchanged.
- **R6 — medium, patched:** Pins-only experiment retains hashes but initially lacks the exact uncommitted source patch. pins-only.patch and pins-only-source-reconstruction.json reconstruct the four deletions and accompanying comment. The complete file SHA-256 and exact Git diff SHA-256 3b6ff6bd85bb810e755f945208a70009b5fba5be1781a635165ad97af8f62da4 both equal the contemporaneous pins-only receipt.

Final independent read-only recheck: no remaining findings. Exact suite/XML/input/artifact, patch, links and commit-message validation checks pass; final preservation limits are accurate.
