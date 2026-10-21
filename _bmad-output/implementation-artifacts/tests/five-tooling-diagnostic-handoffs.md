# Five developer-tooling and diagnostic closures — 2026-10-21

Inspection baseline: Timesheets `ebf544bbb8c8c2618b5422696404aac5b5b47906`, clean main. The selected scope and independent review triage are in [the build spec](../spec-close-five-tooling-diagnostic-deferrals.md). Exactly five existing ledger entries gain resolution notes; original evidence remains intact.

- The v6 diagnostic now validates its complete manifest and every selected directory/collision page before an inline or paged unique candidate can be reported. It checks complete content digests, identities, shard membership, ordering and the selected generation/root recheck. Running the previous committed helper against a corrupt unrelated collision page reproduced an inline success; the new helper rejects that same state.
- The snapshot reader uses the existing `allow_missing=True` read seam, which represents only HTTP 204/404 as absence. Unavailable or malformed v7 format reads cannot silently select v6. The live journey delegates to the tested pure helper; its valid-GET diagnostics use verified v6 candidate lookup too.
- CI now runs the installed party-mode and forge-idea suites plus both Claude mirrors in four separate subprocesses, checks mirror contents, and rejects empty, all-skipped or failing suites. Python 3.11+ is required. Temporary negative probes rejected missing/misdirected/stale wrappers, changed/missing mirrors and empty/failing/all-skipped suites; a mixed active/skipped suite remained accepted.
- The historical Copilot finding is stale: all 13 installed BMAD personas with `[agent]` metadata have wrappers. The cited technical-writer persona is absent; architecture is a workflow. The CI-discovered fitness check verifies the current persona inventory and wrapper LOAD targets without creating workflow agents.
- The three normalized agent entry points match and name default-store DaprClient composition through AddEventStoreDomainService, plus the IEventStoreGatewayClient/IReadModelStore/IReadModelBatchStore standalone test seams. Runtime registrations and fail-closed defaults are unchanged.

## Python and serializer verification

Final changed-lane commands pass:

```bash
python3 -m unittest discover -s scripts/tests -v
python3 scripts/test_magic_link_index_selection.py -v
python3 -m py_compile scripts/magic_link_index_selection.py scripts/local_aspire_magic_link_journey.py scripts/test_magic_link_index_selection.py scripts/tests/test_bmad_tooling.py
```

Results: **88 release/tooling tests passed**, including the four isolated BMAD suite runs; **16 index tests passed**. Before review patches, `python3 -m unittest discover -s scripts -v` also passed all **108** then-existing local fixture, persisted-lineage and index tests. The final targeted index suite includes three additional review regressions; unrelated fixture code was unchanged.

The review used a temporary SDK 10.0.402 project to serialize production-shaped string-array contracts with System.Text.Json. Nine hard-coded collision digests cover punctuation, controls, accented and supplementary characters. A directory fixture binds UTF-16 ordinal ordering independently of Python code:

```json
[["directory-hash","\uD83D\uDE00","capability-1"],["directory-hash","\uE000","capability-2"]]
```

Its canonical UTF-8 SHA-256, without a trailing newline, is `D2DCB63E1696D5BCF8D292552CBA5225230079CDF10D151EE633DE6D5F260E12`. The traversal test accepts this .NET ordering and rejects reversed rows even when their digest is recomputed.

## Runtime evidence boundary

`aspire ps --format Json` and `dapr list --output json` both exited zero and found a foreign Tenants AppHost and seven Dapr apps, including `eventstore`. That occupied app identity prevents safely starting Timesheets. No runtime was started or stopped. These resource-free checks do not qualify current-graph AppHost startup, live Dapr, deployed authority, migration certification or launch acceptance.

Independent review produced seven patched diagnostic/CI findings and one preexisting epoch-readiness finding. The latter is recorded separately in the ledger: generation/root equality does not evaluate lifecycle phase, journal, offboarding or conflict evidence. Candidate-shape validation is not a replacement for the journey's successful production GET.

## Matching Debug/source build and six suites

Both catalog and Works defaults select EventStore `3.126.0`; no historical version override was used. The following matching commands passed; the build produced zero warnings and zero errors:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Each `tests/<Project>/bin/Debug/net10.0/<Project>` executable ran individually and returned exit zero:

| Project | Passed | Skipped | Seconds |
| --- | ---: | ---: | ---: |
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 11.116 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.445 |
| Hexalith.Timesheets.Server.Tests | 558 | 0 | 1.824 |
| Hexalith.Timesheets.Projections.Tests | 549 | 0 | 577.239 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.273 |
| Hexalith.Timesheets.IntegrationTests | 820 | 5 | 580.343 |

Total: **2203 tests, 2198 passed, 5 intentional skips, zero failures/errors/not-run**. The five skips are the unchanged infrastructure, performance and live-Dapr reservations or opt-ins. These default-suite results do not establish live runtime acceptance.

Final normalization, scope and `git diff --check` checks passed. The Conventional Commit message passed `npx --no-install commitlint --edit /tmp/timesheets-five-tooling-commit-message.txt`. Fetch/divergence inspection immediately before finalization showed main synchronized with origin/main.
