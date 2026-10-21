# Five deferred magic-link management boundary closures

Verified 2026-10-21 from clean-main baseline `7de2ac5d0052111b5f6d72564617005d4f2e5df8`.
Scope and review triage: [build spec](../spec-close-five-management-boundary-deferrals.md).

The five ledger resolutions cover null transition IDs, required issuance identity/audit fields, authorized expiry, authorization before management state reads, and the existing Process workload policy. Exactly five resolution notes were added; all original ledger evidence and other entries remain intact. No dependencies, AppHost topology, submodule pointers or agent entry points changed.

## Verification

Matching Debug/source commands succeeded, with zero build warnings/errors:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

The imported catalog and Works EventStore defaults both select `3.126.0`; no verification override was needed. Each `tests/<Project>/bin/Debug/net10.0/<Project>` executable ran individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home` and returned exit zero:

| Project | Passed | Skipped | Seconds |
| --- | ---: | ---: | ---: |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.510 |
| Hexalith.Timesheets.Server.Tests | 564 | 0 | 1.921 |
| Hexalith.Timesheets.Projections.Tests | 549 | 0 | 592.955 |
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 11.325 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.278 |
| Hexalith.Timesheets.IntegrationTests | 848 | 5 | 592.014 |

Total: **2237 tests, 2232 passed, 5 intentional skips, zero failures/errors/not-run**. The skips are the unchanged infrastructure/performance reservations and opt-in performance/live-Dapr lanes.

Focused regressions also passed 36/36 composed HTTP cases and 4/4 endpoint fitness cases. Commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkConfirmationHttpBoundaryTests' -method '*Management*' -method '*IssuanceRejectsIncomplete*' -method '*IssuanceReferenceDenial*'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkConfirmationCapabilityEndpointTests'
```

Tests verify omitted/null fields, zero protected work after refusal, stored state preservation, already-expired capability denial, Project/Work admission, wrong-operation signed assertions, and the actual opt-in local fixture registration. Existing successful issue/revoke/expire, no-human-actor expiry, and public capability journeys remain green in the full integration suite. Superseded intermediate runs are not counted as final verification.

## Review and compatibility

The configured independent Blind Hunter review ran. All six findings received recorded triage; corrections and subsequent fixture/source-fitness changes passed independent rechecks with no concrete defect remaining. No new deferred item was added.

Service consumers must migrate from synchronous `Expire` to `ExpireAsync`. The old method remains available, is marked obsolete, and always refuses expiry because it cannot evaluate asynchronous authorization. README and the Conventional Commit breaking-change footer document this behavior.

`aspire ps --format Json` and `dapr list --output json` identified a foreign Tenants AppHost using the eventstore app ID and port 8080. A Timesheets start would collide, so no AppHost was started or stopped. These resource-free checks do not qualify current-graph runtime or deployed acceptance; the launch verdict remains FAIL.

`git diff --check`, the exact-five ledger scope check, and `npx --no-install commitlint --edit /tmp/timesheets-management-commit-message.txt` passed. Publication uses the requested main branch.
