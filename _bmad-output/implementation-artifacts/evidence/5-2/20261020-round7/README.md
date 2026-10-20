# Story 5.2 round-7 verification — 2026-10-20

[verification.json.gz](verification.json.gz) retains raw stdout/stderr and exit
codes for the four solution NuGet audits and all 15 per-project transitive-outdated
fallbacks, plus restore/build/test logs, exact commands, dependency-input and
project-assets hashes, and derived audit counts. JSON is compressed to avoid
repeating package data across projects in the repository.

The source was the modified Timesheets worktree based on
`2d6b689ec45fd1f73fb7a3ff09e8950c90a2fd07`, committed as
`c5ac6005a48d973d96f7eff6496c72d0728432f0` (the receipt's input hashes match that
commit's files). The SDK was `10.0.402`; all builds used
Release/package mode. The approved root pin changed Works Contracts to `1.0.2`;
the catalog and sibling pointers were unchanged.

Restore covered 15 projects and the solution build passed with no warnings/errors.
Works.Tests passed 76/76 and ArchitectureTests passed 64/64, including the final
post-review assertions. The only direct drift was six catalog-owned EventStore IDs
at `3.125.1 -> 3.126.0` (17 project/package occurrences). Vulnerability and deprecation
audits were clean across 15 projects. Transitive-outdated failed for the solution
and AppHost with `error: Sequence contains no matching element`; the other 14
fallbacks succeeded and reported 26 distinct transitive drift IDs. These failures
remain unavailable evidence, and no transitive pin was added.

To inspect the complete receipt from this directory:

```bash
gzip -dc verification.json.gz | python3 -m json.tool
```

To inspect only the coverage and package-count calculations:

```bash
python3 - <<'PY'
import gzip
import json
with gzip.open('verification.json.gz', 'rt') as receipt:
    print(json.dumps(json.load(receipt)['counts'], indent=2))
PY
```

The receipt does not include new npm or live AppHost evidence. Existing npm
advisories, unqualified NuGet-orchestration runtime proof, the Works source-build
version-forwarding mismatch and final launch reconciliation remain open under
their existing owners. The EventStore package drift has a new Builds/platform
handoff in the deferred-work ledger.
