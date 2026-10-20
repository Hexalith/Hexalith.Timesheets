# Story 5.2 scoped acceptance — 2026-10-20

The user instructed “apply recommended” after the final acceptance walkthrough.
The [acceptance record](story-acceptance.json) accepts Story 5.2's approved
dependency baseline, dated audit reconciliation, and completed round-9 patches.
Story 5.2 moves from `review` to `done`. Package currency stays **CONCERNS**;
Stories 3.6/3.7 stay `in-progress`; launch stays **FAIL**. This sign-off grants no
release authorization or new waiver.

This is the changed documentation worktree based on Timesheets
`f82c446f62d9645735989c8fa2c12257dff4a8c9`. No production/test source, AppHost,
dependency, submodule, workflow, or agent entry point changed. The
[verification inputs](verification-inputs.json) bind the acceptance, changed
documents, existing test source/configuration, and root dependency identities
before the build and focused run; all 23 captured inputs remained unchanged.

Local SDK `10.0.402` Debug/source-reference verification:

- Architecture project build: exit 0, zero warnings, zero errors.
- Existing `LaunchReadinessTests`: exit 0, **16 passed, zero failed/skipped/errors**.
- Diff hygiene: `git diff --check` passed.
- Existing evidence: all 290 tracked evidence files retained their captured
  hashes; frozen Story 5.2 intent and historical triage are unchanged.

[Execution details](execution.json) retain exact argv, environment, UTC times,
durations, and exit codes. [Verification results](verification.json) map the
original log/XML names to lossless gzip receipts and record both raw and
compressed SHA-256 hashes. The command's original XML path is retained in argv;
its bytes are now in [launch-readiness.xml.gz](launch-readiness.xml.gz).
[Historical receipt hashes](historical-receipt-hashes.json) preserve the earlier
evidence inventory, including the unchanged Story 5.1 acceptance and Story 5.2
round-7/round-8 packets.

The other Architecture tests, other five suites, NuGet/npm audits, performance
lanes, AppHost runtime qualification, and deployed acceptance were not rerun.
Earlier counts and source-provenance limits retain their original dates and
scope. npm findings, transitive audit limits, approved prerelease exceptions,
unwaived Works forwarding, and the retained Projection cleanup risk remain with
their existing owners. No deployed, legal/policy, or product UAT gate is closed.
