# Story 5.2 round-8 verification — 2026-10-20

[verification.json.gz](verification.json.gz) retains raw stdout/stderr and exit
codes for the four solution NuGet audits, the solution package listing and all 15
per-project transitive-outdated fallbacks, plus restore/build logs, test summaries,
exact commands, dependency-input and project-assets hashes, the observed catalog
and Works EventStore values, and derived audit and test counts.

The source was the Timesheets worktree based on
`929ea22c977385590d77d95f73af86e8b051c4fc`, the workspace sync commit that moved
the `references/*` gitlinks after the round-7 audit, committed as
`525859841e3dc329f65970b7067d3ef3bd1a91fa` (the receipt's documentation hashes
match that commit; its package, project and test inputs match both). The tracked
worktree had only the round-8 review's documentation and ledger edits; no package,
project, test or gitlink file was modified. The SDK was `10.0.402`; all builds used
Release/package mode.

At that commit the `references/Hexalith.Builds` catalog
`6a38cb0f2e74977d7457fc59e6af79457ed8196a` selected EventStore `3.126.0`, Commons
`2.30.3` and PolymorphicSerializations `1.19.5`, and Works
`64602575b902c0f8d3bfaa41a27565eee5682639` defaulted and forwarded
`HexalithEventStoreVersion` `3.126.0`. These are dated observations; later
workspace syncs can move them.

The forced no-cache restore covered 15 projects and the non-incremental solution
build passed with no warnings/errors. All six Release suites ran 1,959 tests: 1,954
passed, 5 intentional skips, 0 failures. ArchitectureTests ran after the final
documentation edits. The direct audit reported no package update across 15 projects;
vulnerability and deprecation audits were clean. Transitive-outdated still failed
for the solution and AppHost with `error: Sequence contains no matching element`;
the other 14 fallbacks succeeded and reported 19 distinct transitive drift IDs. No
transitive pin was added.

To inspect the complete receipt from this directory:

```bash
gzip -dc verification.json.gz | python3 -m json.tool
```

To inspect only the derived counts:

```bash
python3 - <<'PY'
import gzip
import json
with gzip.open('verification.json.gz', 'rt') as receipt:
    print(json.dumps(json.load(receipt)['counts'], indent=2))
PY
```

The receipt does not include new npm, live AppHost or README source-reference
evidence. Existing npm advisories, unqualified NuGet-orchestration runtime proof,
the structural Works EventStore-version forwarding entry and final launch
reconciliation remain open under their existing owners.
