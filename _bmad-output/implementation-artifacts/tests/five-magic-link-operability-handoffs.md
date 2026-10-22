# Five magic-link operability handoffs — 2026-10-22

Inspection baseline: Timesheets `068b20cea2af22fa203d3e1eb325b00c1834687a`, EventStore `5ce5f1d84b88c808cc8a526b9decf149de977e55`, Builds `359fee24aeca4d9eabed4b425d4cfa2520b2adda`, Works `d60c29569de975bd280098e2e2f37406a30cf705`. SDK 10.0.402 and both EventStore defaults 3.126.0. No dependency or submodule pointer changed.

| Canonical deferred item | Delivered evidence |
| --- | --- |
| Projection transport diagnostics | Both live handlers emit fixed projection/outcome and platform trace identity. Tests inspect exact structured fields, absence of fault objects/protected material, provider failure, and cancellation. |
| Test-store isolation and concurrency | Composite component/key identity, per-row versions, unambiguous ETags, refused stale/foreign/create-conflict writes, and unchanged refused-write contents/ETags. HTTP tests already use the platform store. |
| Loader failure diagnostics | Optional logging distinguishes fixed read operations and exception-type categories while preserving opaque results. Tests cover four read stages, production-coordinator wrapped outages, complete field allowlists, throwing providers, cancellation, and unknown links. |
| Streamed contribution decoding | Existing candidate validator and deterministic exception boundary now protect streamed bytes. Sixteen malformed cases yield no operations or durable mutation. JSON-null sentinel, cancellation immediately before malformed bytes, and cursor faults after valid contributions have controls. |
| Local diagnostic epoch readiness | Before/after epoch admission requires a drained Open selection without offboarding/handoff or unresolved conflict/parking evidence; overflow, malformed lifecycle fields, depth, and conservative capacity checks refuse safely. Final candidate checks inherit readiness checks. |

The local diagnostic remains conservative developer evidence. It compares raw JSON, not ETags; capacity bounds may refuse some large valid snapshots. Full SDK typed-state and scope-boundary validation remains a separately recorded follow-up. The production loader's degraded-only behavior was not changed. No local check substitutes for deployed authority, runtime, migration, or release acceptance.

The required blind review produced eight findings: seven were patched and one preexisting full-validation limitation was deferred. See the spec's complete triage log. The two preexisting MCP configuration edits are excluded from this work.

Validation commands:

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`
- Each `tests/<Project>/bin/Debug/net10.0/<Project>` executable individually, including Works.
- `python3 -m unittest discover -s scripts -p 'test_magic_link_index_selection.py' -v`
- `python3 -m unittest discover -s scripts/tests -v`
- `git diff --check` and Conventional Commit validation before commit.

Earlier runs found and corrected a no-logger fitness assumption, an empty-roster test expectation, and the distinction between owner-terminal reads and effective-entry replay. A known-failed projection run was stopped to rebuild its correction; it supplies no acceptance result.

Runtime baseline command `aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` started the selected AppHost. `aspire describe --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --format Json` reported Timesheets Finished with exit 134. The exact AppHost was stopped successfully before builds; no other running AppHost was stopped. This remains a runtime limitation, not a passing gate, and the launch verdict remains FAIL.

## Final verification

The final Debug/source solution build passed with zero warnings and zero errors. All six freshly built executables ran individually after the behavior-changing review patches. Subsequent test XML comments changed no test behavior.

| Project | Passed | Skipped | Seconds |
| --- | ---: | ---: | ---: |
| Hexalith.Timesheets.ArchitectureTests | 103 | 0 | 10.458 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 | 0.542 |
| Hexalith.Timesheets.Server.Tests | 619 | 0 | 1.929 |
| Hexalith.Timesheets.Projections.Tests | 614 | 0 | 528.478 |
| Hexalith.Timesheets.IntegrationTests | 890 | 5 | 522.523 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | 0.307 |

Total: **2,394 passed, five intentional skips, zero failures/errors/not-run**. The infrastructure and performance opt-ins remain unchanged. The 226-test focused projection run also passed. Python diagnostic tests passed 25/25; tooling discovery passed 88/88. Whitespace and Conventional Commit checks passed. No runtime or deployed acceptance is inferred.
