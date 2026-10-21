# Five runtime-guidance handoffs — verification, 2026-10-21

Documentation worktree based on Timesheets
`283cac642790b3da85467ca43c3252b3b2416319`, using SDK `10.0.402`.
The Builds catalog and Works default both select EventStore `3.126.0`; this run
needs no version override. Dependencies and runtime code are unchanged.

| Deferred goal | Disposition |
|---|---|
| ServiceDefaults handover | Added the explicit EventStore SDK ownership record; local scaffold/project-reference cleanup remains owned by a story naming that project. |
| UI revisit policy | Verified previously delivered FrontComposer host ownership and the prohibition on Timesheets UI projects; reconciled the canonical ledger entry. |
| Kernel defaults | Narrowed all three managed entry points to the fail-closed authorization/reference/policy/unavailable defaults and documented the concrete loader's EventStore dependencies. |
| Explicit Debug/source verification | Verified previously delivered matching restore/build commands and conditional catalog-version guidance; reconciled the canonical ledger entry. |
| MTP runner and workflows | Removed stale VSTest rejection claims; documented individual `dotnet test --project` commands and retained direct executables. Workflow existence was already documented. |

From the repository root, the matching restore and build passed:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Build: zero warnings, zero errors, 16.24 s. Each project runs individually with
the following command, substituting its full project name:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test --project tests/<Project>/<Project>.csproj --configuration Debug --no-build -p:UseHexalithProjectReferences=true
```

All six individual MTP commands returned exit 0: **2203 tests, 2198 passed, 5 intentional skips, zero failures**.

| Project | Passed | Skipped | MTP summary duration |
|---|---:|---:|---|
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 10s 552ms |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 995ms |
| Hexalith.Timesheets.Server.Tests | 558 | 0 | 2s 400ms |
| Hexalith.Timesheets.Projections.Tests | 549 | 0 | 9m 23s 038ms |
| Hexalith.Timesheets.IntegrationTests | 820 | 5 | 9m 35s 475ms |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 944ms |

The five default skips retain the two reserved infrastructure/performance facts,
the two TIMESHEETS_PERF opt-ins, and the TIMESHEETS_LIVE_DAPR opt-in. No opt-in
was enabled. These runs verify the current Debug/source build and documented MTP
runner; they supply no AppHost runtime qualification or deployed acceptance.
Existing checks cover UI ownership, normalized entry-point synchronization,
SDK-owned shared defaults, fail-closed kernel seams, and concrete loader resolution
with explicit EventStore read dependencies. No new regression tests were added
for these prose corrections.

A read-only comparison to the baseline passed: exactly five canonical resolution
notes and five predecessor supersession links were added; every original ledger line and all historical launch receipts
remain unchanged. The three agent entry points are byte-identical; their shared
baseline above `bmad:context` is unchanged. The current guidance no longer claims
the configured runner fails through VSTest. Runtime/deployment qualification,
release dispositions, approvals, and story status are unchanged. The overall
release verdict remains FAIL. Works propagation/forwarding follow-ups and all
other unselected runtime/privacy/migration work remain open.

Independent review produced five findings: three ledger-linkage corrections
were patched, one managed-guidance clarity suggestion was deferred under the
build workflow, and the provenance-stamp finding was rejected because the
original generation stamp and this scoped verification have separate records.

Final preservation/scope checks and `git diff --check` passed. The Conventional
Commit message passed `npm run commitlint -- --edit /tmp/timesheets-runtime-guidance-commit-message.txt`.
