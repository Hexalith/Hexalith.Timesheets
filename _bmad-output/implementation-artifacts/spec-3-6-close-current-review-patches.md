---
title: 'Close current Story 3.6 review patches'
type: 'bugfix'
created: '2026-10-05'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Eight open Story 3.6 review patches leave two security and determinism regressions unguarded and make the review, deferred-work, and verification records misleading.

**Approach:** Add the two focused assertions, correct the six cited record defects, and mark each reviewed patch resolved with evidence. Keep the story in progress and preserve the separately recorded live-topology, durable-write, catalog-freshness, and release gates.

</frozen-after-approval>

## Implementation Notes

- Clean `main` baseline: `c5fcf987046018c980c5def7c2d3e1a9ce310abe`. The latest Story 3.6 review lists exactly eight unchecked patches; older 37 findings are already reconciled (28 implemented/superseded, nine deferred). This increment closes those eight without changing production code or submodule pointers.
- Reused `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` to pin both fail-closed options defaults. Changed the complete-fold fixture's capability and Time Entry MessageIds so lexical order conflicts with sequence order while all nine ordering/duplicate variants remain covered.
- Corrected the ledger's reconciliation count, added the original entry's resolution note, fixed its heading, clarified the test-double disposition, linked the specific configuration limitation, and labeled prior review evidence. Recorded the current patch disposition in the story, test summary, and launch readiness.
- Pre-edit Aspire start succeeded, but `aspire describe` showed Timesheets `Finished` with exit code 134; Aspire was stopped. This is not a passing live EventStore journey. No production code or topology changed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` passed. The matching `dotnet build` with `--no-restore -warnaserror` passed with zero warnings/errors. All six direct xUnit v3 executables passed individually: Architecture 56, Contracts 90, Server 479, Projections 146, Integration 106 pass/four declared skips, Works 76; 957 total, 953 pass, four skips.
- Blind Hunter's seven findings were checked against their cited records and corrected. Build/test gate evidence now cites this increment, the security count is current, the earlier test baseline is labeled, the original ledger item says Resolved, and story/sprint status moved to review. No new acceptance work was inferred from those documentation findings.

## Review Triage Log

- Low / patch — Sprint header still reported eight patches open while all eight were checked. Updated its comment and story status to `review` through the workflow's status sync; AC and release gaps remain named.
- Low / patch — Launch readiness said Story 3.6 was in review while sprint/story were still in progress. The workflow's review transition now aligns all three records; review does not mean acceptance or release completion.
- Low / patch — Build gate cited only the 2026-10-04 baseline. Added the 2026-10-05 changed-worktree restore/build result while retaining dated history.
- Low / patch — Full-suite Tests gate cited only the 2026-10-04 run. Replaced that row's evidence date/baseline with the six-project run for this increment.
- Low / patch — Tenant-isolation/security gate still said Server.Tests 478/478, while the current executable passed 479/479. Corrected the count.
- Low / patch — The test summary placed a 2026-10-04 baseline directly below the new current result. Labeled it as prior evidence.
- Low / patch — The original 37-item reconciliation entry retained historical "remain unchecked" evidence but lacked an explicit closure word. Its resolution note now begins "Resolved" and identifies that evidence as historical; no unestablished ledger schema field was added.
