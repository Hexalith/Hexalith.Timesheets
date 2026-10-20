# Story 5.1 Gate Receipts — 2026-10-20

This packet records local Debug/source-reference verification of the changed Timesheets worktree based on `d72899b4489aaca0b9a9b96d0ac267165920caf8`. Builds is `6a38cb0f2e74977d7457fc59e6af79457ed8196a`, EventStore is `6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e`, and Works is `64602575b902c0f8d3bfaa41a27565eee5682639`. SDK is `10.0.402`, runtime `10.0.13`; catalog and Works EventStore defaults both equal `3.126.0`, so no version override was used.

The [readable command/count summary](../../../tests/5-1-test-summary.md) and [launch assessment](../../../../../docs/launch-readiness.md) define the evidence scope. Overall launch remains **FAIL** for unfinished deployed acceptance and owning-story prerequisites, independently of local test results. Package audits and performance measurements retain their original dates; no AppHost, deployed service, or foreign infrastructure was changed.

- [Initial six-suite execution manifest](execution.json): exact argv, UTC start/end, and exit code per executable. Performance and live-Dapr opt-ins were unset.
- [Projection follow-up manifest](projection-followup.json): the failing-method fallback, then one complete retry only after that fallback passed. The initial full Projection failure is retained, and a focused pass cannot replace a broad result.
- [Current suite counts](counts.json): exact XML assembly counts and the selected complete report for each suite.
- [Artifact checks](artifact-checks.json): normalized entry points, preserved shared baseline, changed files, link checks, and diff hygiene.
- [Receipt hashes](receipt-hashes.json): original and compressed byte counts and SHA-256 hashes, plus the retained raw `/tmp` location. XML/log receipts use lossless gzip with zero gzip timestamp; hashes bind both representations.

The initial broad Projections run had 436 passed, one failed, no skips in 552.166 s. `MagicLinkNumericIndexTests.TenantReplacementPreservesCandidatesBeyondFourHundredGlobalPages` raised `SharedProjectionWriteUnavailableException` from unchanged EventStore `SharedProjectionEpochCoordinator`, with inner `InvalidOperationException: Private scratch marker cleanup is incomplete`. Its focused fallback passed 1/1 in 19.276 s. Complete retry counts and any remaining risk are reported separately in the summary; neither this fallback nor documentation changes erase the initial failure.

Initial Architecture/reporting-guard receipts and the final Architecture verification are distinguished in the summary. The first focused documentation run had one newly introduced scope-guard failure; its receipt is also preserved alongside the passing correction. Raw argv still names the XML/log output path used at collection time; compressed receipts preserve those exact bytes.

Pre-sign-off checkpoint results: **1,955 passed, zero failed, five intentional skips** (1,960 cases). The complete Projection retry passed 437/437 in 491.085 s. Parent final source-reference solution build: 0 warnings/errors, 6.03 s; full Architecture: 65/65, including 16 readiness cases. The initial Projection failure remains an intermittent runtime risk.

[Historical receipt preservation correction](history-preservation-correction.md) restores three archived rows from the baseline and records the complete preservation audit and narrow verification. The attribution field names pre-existing parent workflow handoff changes accurately.

The [2026-10-20 review corrections](review-corrections.md) have separate focused execution evidence. [Source binding](review-handoff-source-binding.json) captures the current code/configuration and a durable diff before that build/run, with all root dependency revisions. [Earlier-run provenance](earlier-run-provenance.json) does not retroactively associate this later source capture with the earlier six-suite or history-correction runs. [Failed-attempt inventory](failed-attempts.json) retains the original failed broad and focused attempts.

[Parent final verification](parent-final-result.json) selects the unfiltered full Architecture receipt after review corrections. Its [source binding](parent-final-source-binding.json), [archive](parent-final-sources.tar.gz), [diff](parent-final-source.diff.gz), and [input stability proof](parent-final-input-stability.json) bind that build/full run to its captured inputs. Other five complete-suite receipts retain their original source-provenance limits. All 13 review findings are resolved; release and story acceptance remain open.

After count/status publication, the [focused readiness result](parent-published-result.json) passed 16/16 (0.372 s). The [published-input source binding](parent-published-source-binding.json) and [input stability proof](parent-published-input-stability.json) cover that check; no binary changed after the full Architecture run.

[Final preservation audit](parent-final-preservation-checks.json) verifies historical receipts, all six source archives/diffs, unchanged executable/test inputs, 13 clean pinned root dependencies, and the final review status.

The user subsequently instructed “accept and sign-off”; [scoped human acceptance](story-acceptance.json) closes Story 5.1’s completed reconciliation. Earlier review checkpoints above retain their original status. Launch remains FAIL, with deployed acceptance, legal/policy gates, product UAT, and Story 5.2 review still open.

Post-sign-off verification: Architecture project build 0 warnings/errors (0.94 s), readiness 16/16 (0.365 s), full Architecture 65/65 (11.192 s). [Execution outcomes](signoff-result.json), [source binding](signoff-source-binding.json), [input stability](signoff-input-stability.json), and [preservation audit](signoff-preservation-checks.json) bind the acceptance and updated guard. [Current selected counts](counts.json) remain 1,955 passed, zero failed, five skips; no other suite was rerun.
