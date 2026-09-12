---
title: 'Move repository submodules under references'
type: 'refactor'
created: '2026-07-20'
status: 'done'
baseline_commit: 'd4dd622'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/CLAUDE.md'
  - '{project-root}/.gitmodules'
  - '{project-root}/Directory.Build.props'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Timesheets repository declares most Hexalith dependencies at
repository-root paths, while the shared workspace conventions and agent
baseline expect root-declared dependencies under `references/`. The checkout
also contains a stale duplicate `Hexalith.Builds` gitlink at the root even
though its active declaration already points to `references/Hexalith.Builds`.

**Approach:** Relocate every Timesheets-owned root submodule checkout to
`references/<module>`, remove the stale root `Hexalith.Builds` gitlink, update
`.gitmodules` and the parent MSBuild dependency probes, and preserve each
submodule's checked-out commit and repository content.

## Boundaries & Constraints

**Always:** Keep the same remote URLs and submodule commits; use only the
existing root-declared submodules; keep the operation non-recursive; make
parent build properties resolve local dependencies from `references/` while
preserving supported sibling fallbacks; leave submodule content unchanged.

**Ask First:** Any request to change a submodule commit, remote URL, nested
submodule, dependency version, or product source code is outside this intent.

**Never:** Do not initialize or update submodules from remote; do not edit
files inside a submodule; do not leave root-level Hexalith gitlinks or stale
root path declarations; do not commit or push.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Existing checkout | Initialized root submodules and the existing `references/Hexalith.Builds` checkout | All unique submodules are available at `references/<module>` with their original commits | Stop if a target path is occupied by unrelated content |
| Duplicate Builds gitlink | Root `Hexalith.Builds` and `references/Hexalith.Builds` both tracked | The stale root gitlink is removed and the existing references checkout remains | Stop if the two pointers cannot be distinguished without changing content |
| Fresh clone metadata | Updated `.gitmodules` and gitlink paths | `git submodule status` and non-recursive initialization metadata address only `references/` paths | Report the exact failing Git command |
| Build resolution | Projects evaluate root dependency properties | Local dependency roots resolve under `references/`, and supported sibling fallbacks remain available | Report the exact MSBuild or restore failure |

</frozen-after-approval>

## Code Map

- `.gitmodules` -- declares the parent repository's submodule names, paths, and remotes.
- `Directory.Build.props` -- resolves local and sibling Hexalith project roots for MSBuild.
- `Directory.Packages.props` -- imports centralized package versions from the Builds reference and must retain a valid references-first import.
- `Hexalith.*` and `references/Hexalith.*` -- gitlink worktrees whose tracked paths are being normalized.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs` -- reads `.gitmodules` paths.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs` -- reads `Directory.Build.props` dependency probes.

## Tasks & Acceptance

**Execution:**
- [x] `.gitmodules` -- change every declared submodule path to `references/<module>` and remove no remote or URL -- align metadata with workspace ownership.
- [x] `Hexalith.*` gitlinks -- move the ten root worktrees and remove the stale root `Hexalith.Builds` duplicate while preserving the existing references pointer -- normalize the tracked dependency layout.
- [x] `Directory.Build.props` -- change local dependency probes and values from root paths to `references/` paths while retaining explicit-property and sibling fallbacks -- keep project evaluation valid after the move.
- [x] `Directory.Packages.props` -- verify and adjust only if needed so the references-first Builds props import remains valid -- preserve centralized package resolution.
- [x] Fitness tests -- assert every `.gitmodules` path is under `references/` and that `Directory.Build.props` keeps `references\` probes without a repository-root `$(MSBuildThisFileDirectory)Hexalith.` probe -- pin the moved layout.

**Acceptance Criteria:**
- Given the parent repository metadata is inspected, when all declared submodule paths are listed, then every path is under `references/` and all original remotes remain unchanged.
- Given the parent Git index is inspected, when gitlinks are listed, then there is exactly one gitlink per declared module, every gitlink is under `references/`, and no root `Hexalith.*` gitlink remains.
- Given the moved worktrees are checked, when each submodule HEAD is compared with its pre-move commit, then every commit is unchanged and each worktree remains a valid Git submodule.
- Given a local Timesheets project is evaluated, when its dependency root properties are queried, then local dependencies resolve from `references/` and the Builds package props import succeeds.
- Given repository validation runs, when Git consistency, whitespace checks, restore, and the focused architecture/build checks execute, then they pass without recursive submodule initialization or submodule content changes.

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace or conflict-marker errors.
- `git submodule status` -- expected: one valid entry per `references/` submodule.
- `git ls-files -s | awk '$1 == "160000"'` -- expected: all gitlinks are under `references/`.
- `dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot` -- expected: a valid resolved dependency path.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds using the relocated references.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-restore -m:1 /nr:false` then the built assembly `-method Hexalith.Timesheets.ArchitectureTests.FitnessTests.ScaffoldGovernanceTests.Nested_submodules_are_not_initialized_inside_root_level_submodules` and `-method Hexalith.Timesheets.ArchitectureTests.FitnessTests.DependencyDirectionTests.Directory_build_props_detects_required_root_level_sibling_modules` -- expected: both pass.

## Review Triage Log

- BH1 / WorksCheckoutGovernanceTests.cs missing — **false** — that class is specified by draft Story 5.3, not by this move spec. This spec's acceptance is git/MSBuild inspection, not a Works-only fitness type.
- BH2 / LaunchReadinessTests not extended for checkout ownership — **false** — launch-readiness vocabulary tests already match this spec's one-line Builds path retarget; extra Works-ownership guards are Story 5.3 work, not a missing outcome of the move.
- BH3 / BDR, README, architecture.md, sprint-status still describe root/sibling Works — **defer** — those documents and `5-3-…: backlog` are Story 5.3 remaining work. This intent only relocates checkouts and parent probes.
- BH4 / launch-readiness.md omits Works-as-default-checkout language — **defer** — same 5.3 doc-sync gap. This change only retargeted the Builds catalog path, which is present at `docs/launch-readiness.md`.
- BH5 / every relocated gitlink SHA changed vs baseline — **defer** — at relocate `cfd9e62` most pins were preserved; HEAD pins (`42c4318` Works, `6da79ae` Commons, etc.) come from later `830e44e` / `9a799f2` / `e3ea9ce` pointer updates, not from the path move.
- BH6 / stale root Builds `cf04c41` overwrote references `f0750ca` — **false** — HEAD has a single `references/Hexalith.Builds` gitlink at `35c3d1e` and no root `Hexalith.Builds`. The duplicate-pointer failure mode does not exist in the current tree.
- BH7 / `.editorconfig` dropped the PolymorphicSerializations inside-namespace override — **false** — `Hexalith.Timesheets.slnx` does not include those projects, so Timesheets compilation does not apply the parent `outside_namespace` rule to them. The relocate had already retargeted the override; a later `/pushall` deleted the block.
- BH8 / two incompatible specs landed as one change — **false** — `Directory.Build.props` correctly follows this spec (every `Hexalith*Root` probe). Spec 5.3 is still `draft` and assumes the `references/` layout already exists.
- BH9 / MagicLink `RemoveAll<DaprReadModelStore>()` unmentioned in either spec — **false** — that line is from later `a8bcd59`, not the relocate. Replacing `IReadModelStore` is the test's existing isolation; the extra remove is not a move-spec defect.
- BH10 / most of the since-baseline diff is BMAD installer noise — **false** — a review-scope observation, not a bad outcome at a product location.
- BH11 / `.codex/hooks.json` hard-codes this machine path — **defer** — real for other clones, introduced by the later BMAD-loop hook install, not by the submodule path move.
- BH12 / Copilot agent wrappers omit some installed skills — **defer** — BMAD agent-manifest coverage from the later skill refresh, not caused by this story.
- BH13 / Fluent UI fitness scan still walks the whole repo while DependencyDirectionTests was narrowed to `src/`+`tests/` — **false** — `No_fluent_ui_v4_component_package_is_introduced` still filters `Hexalith.Timesheets` in the path, so `references/` csproj files are not selected. The DependencyDirection narrowing is later `a8bcd59`.
- BH14 / Story 5.3 remains draft while this spec is marked done — **false** — that status split is correct: the layout move shipped; 5.3 fitness/docs are a separate unfinished story.
- ECH1 / other Dapr read-model interfaces still registered after `DaprReadModelStore` removal — **false** — leftover factories fall back to `GetRequiredService<DaprReadModelStore>()`, but the HTTP-boundary tests resolve the replaced `IReadModelStore`. No demonstrated throw on those routes.
- ECH2 / `bmad_loop_hook.py` does not catch `UnicodeDecodeError` on stdin — **defer** — `json.load(sys.stdin)` can raise `UnicodeDecodeError` outside the current `except`. BMAD-loop hook, not this story.
- ECH3 / event filename interpolates `task_id`/`event_name` without sanitizing separators — **defer** — POSIX `openat` can walk `..` in the name. Inputs are orchestrator/hook-config controlled; not this story.
- ECH4 / Codex hook command uses an absolute Timesheets path — **defer** — same leftover as BH11.
- ECH5 / `references/Hexalith.Commons` pin is `6da79ae`, not pre-move `ea1fc45` — **defer** — same later pointer updates as BH5. Relocate `cfd9e62` still had `ea1fc45`.
- ECH6 / `references/Hexalith.Builds` pin is `35c3d1e`, not pre-change `f0750ca` — **defer** — same later Builds pointer update as BH5.
- VG1 / fitness tests do not pin the `references/` layout — **medium** — `DependencyDirectionTests` only required property names; `ScaffoldGovernanceTests` did not require `path = references/…`. A root gitmodules path or a restored `$(MSBuildThisFileDirectory)Hexalith.` probe would stay green.
- VG2 / party-mode and forge-idea override tests ignore omitted icon/title — **defer** — BMAD 6.12 merge tests, not this story.
- VG3 / `resolve_party` non-string member tokens untested — **defer** — same BMAD party-mode suite gap; forge-idea already covers the case.
- VG4 / `--project-root` callers mock `resolve_customization.py` so merge is never executed — **defer** — BMAD script test gap from the later skill refresh.
- VG5 / Codex Stop still invokes deleted `bmad-story-automator` — **defer** — `.codex/hooks.json` still references that missing script; leftover of the BMAD-loop install, not the submodule move.
- VG6 / gitlink SHAs do not match the keep-commits rule — **defer** — same later pin updates as BH5 (verification-gap other finding).
- VG7 / `.codex/hooks.json` hard-codes this checkout path — **defer** — same as BH11 (verification-gap other finding).
