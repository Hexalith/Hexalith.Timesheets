---
title: 'Consume the umbrella-owned Hexalith.Works checkout'
type: 'refactor'
created: '2026-09-08'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 5.3 requires Timesheets to consume one umbrella-owned Hexalith.Works source so Stories 1.10 and 4.8 cannot silently bind a second commit. The July 2026 story assumed a nested Timesheets clone with a root `Hexalith.Works` gitlink. This clone later moved every dependency under `references/`, still gitlinks `references/Hexalith.Works`, and has no enclosing superproject.

**Approach:** Apply one approved Works ownership model to Git metadata, `HexalithWorksRoot` resolution, fitness tests, and docs. Keep the Works adapter and its Contracts-only project reference unchanged. Do not initialize or mutate any Works checkout.

## Boundaries & Constraints

**Always:** Preserve a caller-supplied `HexalithWorksRoot`. Keep `Hexalith.Timesheets.Works` pointing at `$(HexalithWorksRoot)\src\Hexalith.Works.Contracts\Hexalith.Works.Contracts.csproj` only. Keep Stories 1.10 and 4.8 green. Use `.slnx`, Central Package Management, and warnings-as-errors. One C# type per file. Read `hexalith-git-instructions.md` before any gitlink or `.gitmodules` edit.

**Never:** Change domain, host wiring, EventStore persistence, or UI. Do not reintroduce a repository-root `./Hexalith.Works` probe. Do not instruct anyone to initialize `Hexalith.Timesheets/Hexalith.Works`. Do not edit files inside `references/Hexalith.Works`. Do not bump other submodule pointers. Do not add `Hexalith.*` NuGet package references.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Default evaluation | `HexalithWorksRoot` unset; approved default checkout present | Property resolves to the approved default path | Restore fails closed if the path is missing; do not create or init a checkout |
| Explicit override | Caller sets `HexalithWorksRoot` | The supplied path is preserved | Treat as read-only; do not init or mutate Works |
| Root-local regression | `.gitmodules`, git index, or `Directory.Build.props` reintroduces repo-root `Hexalith.Works` | Fitness tests fail | N/A |
| Adapter continuity | `Hexalith.Timesheets.Works.Tests` after alignment | Existing 1.10 and 4.8 tests stay green | Report the exact command and counts |

</frozen-after-approval>

## Open Questions

- Works checkout ownership after the `references/` relocation — options: **A. Literal Story 5.3** (drop every Timesheets Works gitlink, including `references/Hexalith.Works`; default only to `../Hexalith.Works`. This clone has no superproject and no `../Hexalith.Works`, so restore/build break unless a sibling checkout or `-p:HexalithWorksRoot` is added.) / **B. This repo is the umbrella** (keep `references/Hexalith.Works`; reject only a repo-root `Hexalith.Works` declaration, gitlink, or `$(MSBuildThisFileDirectory)Hexalith.Works` probe; keep the current `references/` then sibling probes; add fitness tests and fix stale docs. Local builds keep working.)

## Code Map

- `.gitmodules` lines 28–30 and git index -- `[submodule "Hexalith.Works"]` at `references/Hexalith.Works` (`160000` @ `42c43180df201883512369c680db1d091f57b02d`). No root `Hexalith.Works` gitlink.
- `Directory.Build.props` lines 7 and 14 -- caller-supplied `HexalithWorksRoot` wins; else `references\Hexalith.Works` then `..\Hexalith.Works`. No `.\Hexalith.Works` probe. Change Works only; leave other `Hexalith*Root` probes.
- `src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj` line 17 -- keep the Contracts-only `$(HexalithWorksRoot)` `ProjectReference`.
- `src/Hexalith.Timesheets.Works/WorksQueryWorkReferenceValidator.cs`, `WorksQueryWorkPlannedEffortProvider.cs`, and `tests/Hexalith.Timesheets.Works.Tests/` -- 1.10 / 4.8 behavior; re-run, do not rewrite.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/` -- `DependencyDirectionTests` is name-presence only; `ScaffoldGovernanceTests` is nested-`.git` only; `LaunchReadinessTests` is the Story 5.2 doc-guard pattern; `RepositoryRoot` locates the repo. Add `WorksCheckoutGovernanceTests.cs`.
- `docs/launch-readiness.md`, `docs/boundary-decision-record.md` line 32, `README.md`, and `_bmad-output/planning-artifacts/architecture.md` lines 262–275 and 890–892 -- stale or missing checkout guidance; do not flip overall launch `CONCERNS` to `PASS`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- `5-3-consume-umbrella-owned-hexalith-works-checkout: backlog`.
- `_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md` -- later relocate that pinned Works under `references/`; do not undo it unless option A is chosen.
- Do not change -- files under `references/Hexalith.Works/`, adapter mapping, host deny/unavailable defaults, or other module probes.

## Tasks & Acceptance

**Execution:**
- [ ] `.gitmodules` and the Works gitlink -- apply the approved ownership decision -- remove Timesheets-owned Works metadata or keep `references/Hexalith.Works` and reject only repo-root `Hexalith.Works`.
- [ ] `Directory.Build.props` -- keep caller-supplied `HexalithWorksRoot`; apply the approved default probe set; never restore `$(MSBuildThisFileDirectory)Hexalith.Works` -- match the decision without touching other module probes.
- [ ] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/WorksCheckoutGovernanceTests.cs` -- new fitness class asserting `.gitmodules`, gitlink, no root-local probe, and explicit-property preservation -- fail if the forbidden Timesheets-owned root checkout returns.
- [ ] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- guard launch-readiness checkout-ownership evidence -- same honesty pattern as package currency.
- [ ] `docs/launch-readiness.md` -- record the Works checkout verdict without claiming live Works host integration -- Story 5.1 / 5.2 evidence shape.
- [ ] `docs/boundary-decision-record.md` -- replace the root-level submodule claim with the approved checkout path -- stop stale ownership language.
- [ ] `README.md` -- name the approved default Works checkout; forbid `Hexalith.Timesheets/Hexalith.Works` init; include `Works.Tests` in the test command list -- docs AC.
- [ ] `_bmad-output/planning-artifacts/architecture.md` -- sync Workspace Dependency Ownership and the checkout note with the approved model -- remove the architecture vs repo contradiction.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- move `5-3-consume-umbrella-owned-hexalith-works-checkout` through in-progress then done -- keep sprint status honest.

**Acceptance Criteria:**
- Given the approved ownership model, when Timesheets Git metadata is inspected, then it matches that model and the Git index has no repository-root `Hexalith.Works` gitlink.
- Given `HexalithWorksRoot` is unset, when Timesheets projects evaluate, then the property resolves to the approved default path and no Timesheets-root `./Hexalith.Works` probe exists.
- Given a controlled build supplies `HexalithWorksRoot`, when projects evaluate, then the supplied path is preserved and Timesheets does not initialize or mutate Works.
- Given checkout-governance fitness tests run, when `.gitmodules`, the Git index, and `Directory.Build.props` are inspected, then they fail on a reintroduced Timesheets-owned root Works declaration, gitlink, or root-local path probe.
- Given the approved Works source is available, when restore, build, ArchitectureTests, and Works.Tests run with warnings as errors, then they pass and Stories 1.10 and 4.8 stay green.
- Given README, architecture, boundary, and launch-readiness docs are read, when Works setup is described, then they name the approved default checkout and do not tell anyone to initialize `Hexalith.Timesheets/Hexalith.Works`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

July 2026 Story 5.3 assumed Timesheets lived at `<umbrella>/references/Hexalith.Timesheets` and must drop a duplicate root `Hexalith.Works` so MSBuild used sibling `../Hexalith.Works` (the umbrella pin). This workspace is `/home/administrator/projects/hexalith/timesheets` with no superproject. `../Hexalith.Works` is absent; `../works` exists but does not match the probe. Current props already prefer `references/Hexalith.Works`.

Fitness tests should read text and git metadata only. Do not shell out to `git submodule update`. For gitlink absence, parse `git ls-files --stage` output or an equivalent checked-in index read; do not require a live sibling clone.

Example of the forbidden root probe (must not return):

```xml
<HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Works</HexalithWorksRoot>
```

## Verification

**Commands:**
- `git ls-files --stage -- Hexalith.Works references/Hexalith.Works` -- expected: matches the approved ownership model; no root `Hexalith.Works` gitlink.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot` -- expected: approved default when unset; caller value when `-p:HexalithWorksRoot=` is passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds if the approved checkout exists.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: 0 warnings, 0 errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` -- expected: all pass after the new fitness tests.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests` -- expected: existing 1.10 / 4.8 tests stay green.
